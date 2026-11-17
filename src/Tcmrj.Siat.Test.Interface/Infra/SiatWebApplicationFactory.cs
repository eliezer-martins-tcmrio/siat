using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Serilog;
using Serilog.Events;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Interface.Infra;

/// <summary>
/// Hospeda o SIAT real no Kestrel, numa porta livre, no ambiente <c>Testing</c>, para os testes com navegador.
/// <list type="bullet">
/// <item>Autenticação: esquema de teste no lugar de <c>Cookies</c>/<c>oidc</c> e <see cref="UserManagerServiceFalso"/>
/// (ver <see cref="AutenticacaoDeTeste"/>). O usuário de cada teste vai num contexto do navegador
/// (<see cref="NovoContextoAsync"/>).</item>
/// <item>Bancos: nenhum é tocado. <see cref="IMailQueueRepository"/> é <see cref="MailQueue"/> e
/// <see cref="IRegistroConsultaService"/> é <see cref="Registros"/>. O Serilog do DBLog é trocado por um que só escreve
/// avisos e erros no console, sem o sink SQL de AUDITORIADB; e, por garantia, as connection strings apontam para uma
/// porta fechada de 127.0.0.1 com tempo de conexão de 1 s.</item>
/// <item>Servidores: a lista fechada é trocada por <see cref="ServidorDsv"/>, <see cref="ServidorHml"/> e <see cref="ServidorPrd"/>.</item>
/// <item>Outros serviços: <see cref="ConfigurarServicos"/> (antes do primeiro uso) ou, numa subclasse,
/// <see cref="ConfigurarServicosDeTeste"/>.</item>
/// </list>
/// Use uma instância por classe de teste (<c>IClassFixture</c>): os falsos são do host, e os testes de uma classe
/// rodam em sequência.
/// </summary>
public class SiatWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ServidorDsv = "dsv-teste";

    public const string ServidorHml = "hml-teste";

    public const string ServidorPrd = "prd-teste";

    public const string NomeServidorDsv = @"TCMBD3\DEV";

    public const string NomeServidorHml = @"TCMBD4\HML";

    public const string NomeServidorPrd = @"TCMBD\PRD";

    /// <summary>Porta fechada: qualquer tentativa de conexão falha na hora.</summary>
    private const string ConexaoInexistente =
        "Data Source=127.0.0.1,9;Initial Catalog=SiatTeste;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=1;Encrypt=false";

    private readonly Lock _trava = new();
    private readonly List<Action<IServiceCollection>> _configuracoes = [];
    private bool _iniciado;
    private Uri? _endereco;

    public SiatWebApplicationFactory() => UseKestrel(0);

    /// <summary>Usuários dos testes; preenchido por <see cref="NovoContextoAsync"/>.</summary>
    public UsuariosDeTeste Usuarios { get; } = new();

    /// <summary>MailQueue em memória usada pela aplicação.</summary>
    public MailQueueRepositoryFalso MailQueue { get; } = new();

    /// <summary>Registros de consulta feitos pela aplicação.</summary>
    public RegistroConsultaFalso Registros { get; } = new();

    /// <summary>Endereço do Kestrel (por exemplo, <c>http://127.0.0.1:51234/</c>). Inicia o host no primeiro acesso.</summary>
    public Uri Endereco
    {
        get
        {
            lock (_trava)
            {
                if (_endereco is null)
                {
                    _iniciado = true;
                    StartServer();
                    var enderecos = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
                    _endereco = new Uri(enderecos.First().TrimEnd('/') + "/");
                }

                return _endereco;
            }
        }
    }

    /// <summary>Substitui ou acrescenta serviços da aplicação. Só antes do primeiro uso do host.</summary>
    public void ConfigurarServicos(Action<IServiceCollection> configurar)
    {
        ArgumentNullException.ThrowIfNull(configurar);
        lock (_trava)
        {
            if (_iniciado)
            {
                throw new InvalidOperationException("O host já foi iniciado; configure os serviços antes do primeiro uso.");
            }

            _configuracoes.Add(configurar);
        }
    }

    /// <summary>Troca todas as implementações de <typeparamref name="TServico"/> por <paramref name="instancia"/>.</summary>
    public void Substituir<TServico>(TServico instancia)
        where TServico : class =>
        ConfigurarServicos(s =>
        {
            s.RemoveAll<TServico>();
            s.AddSingleton(instancia);
        });

    /// <summary>
    /// Novo contexto do navegador (cookies e armazenamento isolados) já autenticado como <paramref name="usuario"/>,
    /// com <c>BaseURL</c> no host de teste, pt-BR e o fuso de Brasília.
    /// </summary>
    public async Task<IBrowserContext> NovoContextoAsync(IBrowser navegador, UsuarioDeTeste usuario)
    {
        ArgumentNullException.ThrowIfNull(navegador);
        var endereco = Endereco.ToString();
        var contexto = await navegador.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = endereco,
            Locale = "pt-BR",
            TimezoneId = "America/Sao_Paulo",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        });
        await contexto.AddCookiesAsync(
        [
            new Cookie { Name = AutenticacaoDeTeste.NomeCookie, Value = Usuarios.Registrar(usuario), Url = endereco },
        ]);
        return contexto;
    }

    /// <summary>Atalho: novo contexto para o usuário e uma página já aberta em <paramref name="caminho"/>.</summary>
    public async Task<IPage> AbrirAsync(IBrowser navegador, UsuarioDeTeste usuario, string caminho = "/")
    {
        var contexto = await NovoContextoAsync(navegador, usuario);
        var pagina = await contexto.NewPageAsync();
        await pagina.GotoAsync(caminho);
        return pagina;
    }

    /// <summary>Ponto de extensão das subclasses (por exemplo, registrar o falso do AuditLog).</summary>
    protected virtual void ConfigurarServicosDeTeste(IServiceCollection services)
    {
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Registrado depois do UseTcmrjDBLog do Program, este logger vence: nada vai para o sink SQL do DBLog.
        builder.UseSerilog((_, configuracao) => configuracao
            .MinimumLevel.Warning()
            .MinimumLevel.Override("Microsoft.AspNetCore.HttpsPolicy", LogEventLevel.Error)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} SIAT-teste {Level:u3}] {Message:lj}{NewLine}{Exception}"));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        // Fora de Development, os arquivos estáticos de pacotes (_framework/blazor.web.js, Blueprint) só são
        // achados pelo manifesto de desenvolvimento.
        builder.UseStaticWebAssets();

        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            // A lista fechada do projeto (servidores.json) não entra: os testes usam só a lista abaixo.
            if (configuracao is IConfigurationBuilder construtor)
            {
                foreach (var fonte in construtor.Sources.OfType<JsonConfigurationSource>()
                             .Where(f => string.Equals(f.Path, "servidores.json", StringComparison.OrdinalIgnoreCase))
                             .ToList())
                {
                    construtor.Sources.Remove(fonte);
                }
            }

            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserManagerService:ApplicationId"] = "178",
                ["TcmrjOidc:Authority"] = "https://sia.teste.invalid/scaf",
                ["TcmrjOidc:ClientId"] = "SIAT-teste",
                ["TcmrjOidc:ClientSecret"] = "segredo-de-teste",
                ["ConnectionStrings:UserManagerConnection"] = ConexaoInexistente,
                ["ConnectionStrings:AuditoriaDbConnection"] = ConexaoInexistente,
                ["ConnectionStrings:Consultas"] = ConexaoInexistente,
                ["Servidores:0:Chave"] = ServidorDsv,
                ["Servidores:0:Nome"] = NomeServidorDsv,
                ["Servidores:0:Endereco"] = "127.0.0.1,9",
                ["Servidores:0:Ambiente"] = "Dsv",
                ["Servidores:1:Chave"] = ServidorHml,
                ["Servidores:1:Nome"] = NomeServidorHml,
                ["Servidores:1:Endereco"] = "127.0.0.1,9",
                ["Servidores:1:Ambiente"] = "Hml",
                ["Servidores:2:Chave"] = ServidorPrd,
                ["Servidores:2:Nome"] = NomeServidorPrd,
                ["Servidores:2:Endereco"] = "127.0.0.1,9",
                ["Servidores:2:Ambiente"] = "Prd",
                ["Logging:LogLevel:Default"] = "Warning",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AdicionarAutenticacaoDeTeste(Usuarios);

            services.RemoveAll<IMailQueueRepository>();
            services.AddSingleton<IMailQueueRepository>(MailQueue);

            services.RemoveAll<IRegistroConsultaService>();
            services.AddSingleton<IRegistroConsultaService>(Registros);

            ConfigurarServicosDeTeste(services);

            // Depois de iniciado, a lista não muda mais (ConfigurarServicos recusa).
            foreach (var configurar in _configuracoes)
            {
                configurar(services);
            }
        });
    }
}
