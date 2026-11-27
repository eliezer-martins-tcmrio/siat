using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.Web.Configurations;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Endpoints de conexão e desconexão (specs/005-copilot-meu-consumo/contracts/conexao-github.md; T055), testados pelos
/// delegates e pelos metadados da rota, sem servidor nem rede.
/// </summary>
public sealed class EndpointsGitHubTests
{
    private readonly Mock<IAuthenticationSchemeProvider> _esquemas = new();
    private readonly Mock<IAuthenticationService> _autenticacao = new();
    private readonly LoggerGravador<RegistroConexaoGitHub> _log = new();

    public EndpointsGitHubTests()
    {
        _esquemas.Setup(e => e.GetSchemeAsync(It.IsAny<string>()))
            .ReturnsAsync((string nome) => new AuthenticationScheme(nome, null, typeof(IAuthenticationHandler)));
    }

    private void SemEsquemas() => _esquemas.Setup(e => e.GetSchemeAsync(It.IsAny<string>())).ReturnsAsync((AuthenticationScheme?)null);

    private DefaultHttpContext Contexto(bool conectado = true)
    {
        var servicos = new ServiceCollection()
            .AddSingleton(_esquemas.Object)
            .AddSingleton(_autenticacao.Object)
            .AddSingleton(new RegistroConexaoGitHub(_log))
            .BuildServiceProvider();

        List<ClaimsIdentity> identidades = [new([new Claim("matricula", "123"), new Claim("nome", "Ana Souza")], "Teste")];
        if (conectado)
        {
            identidades.Add(new ClaimsIdentity(
                [new Claim(ConexaoGitHubService.ClaimId, "7"), new Claim(ConexaoGitHubService.ClaimLogin, "ana-m"), new Claim("matricula", "123")],
                ConexaoGitHubService.TipoDeAutenticacao));
        }

        return new DefaultHttpContext { RequestServices = servicos, User = new ClaimsPrincipal(identidades) };
    }

    private static string Destino(IResult resultado) => resultado.Should().BeOfType<RedirectHttpResult>().Which.Url;

    // ---------- Conectar ----------

    [Fact]
    public void Conectar_exige_CopilotProprio_CanSelect_e_desconectar_so_usuario_autenticado()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization().AddRouting();
        var app = builder.Build();

        app.UseCopilotGitHubConfiguration();

        var rotas = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();
        var conectar = rotas.Single(r => r.RoutePattern.RawText == "copilot/github/conectar");
        conectar.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).Should().Contain("CopilotProprio.CanSelect");
        var desconectar = rotas.Single(r => r.RoutePattern.RawText == "copilot/github/desconectar");
        desconectar.Metadata.GetOrderedMetadata<IAuthorizeData>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://exemplo.com")]
    [InlineData("//exemplo.com")]
    [InlineData("/copilot")]
    [InlineData("\\\\exemplo.com")]
    [InlineData("javascript://x")]
    [InlineData(null)]
    public async Task Conectar_com_retorno_externo_volta_a_copilot(string? retorno)
    {
        var resultado = await CopilotGitHubConfiguration.ConectarAsync(Contexto(conectado: false), retorno);

        var desafio = resultado.Should().BeOfType<ChallengeHttpResult>().Which;
        desafio.AuthenticationSchemes.Should().Equal(CopilotGitHubConfiguration.EsquemaOAuth);
        desafio.Properties!.RedirectUri.Should().Be("/copilot");
    }

    [Fact]
    public async Task Conectar_guarda_o_retorno_local_e_a_matricula_no_desafio()
    {
        var resultado = await CopilotGitHubConfiguration.ConectarAsync(Contexto(conectado: false), "copilot/pessoas/ana-m?de=2026-10-01");

        var propriedades = resultado.Should().BeOfType<ChallengeHttpResult>().Which.Properties!;
        propriedades.RedirectUri.Should().Be("/copilot/pessoas/ana-m?de=2026-10-01");
        propriedades.Items[ValidacaoRetornoGitHub.PropriedadeMatricula].Should().Be("123");
    }

    [Fact]
    public async Task Conectar_sem_a_OAuth_App_configurada_volta_com_nao_configurado()
    {
        SemEsquemas();

        var resultado = await CopilotGitHubConfiguration.ConectarAsync(Contexto(conectado: false), "copilot");

        Destino(resultado).Should().Be("/copilot?conexao=nao-configurado");
    }

    // ---------- Desconectar ----------

    [Fact]
    public async Task Desconectar_apaga_o_cookie_registra_e_volta_ao_retorno_local()
    {
        var contexto = Contexto();

        var resultado = await CopilotGitHubConfiguration.DesconectarAsync(contexto, null, "copilot");

        Destino(resultado).Should().Be("/copilot");
        _autenticacao.Verify(a => a.SignOutAsync(contexto, ConexaoGitHubService.TipoDeAutenticacao, It.IsAny<AuthenticationProperties?>()), Times.Once);
        var evento = _log.Eventos.Should().ContainSingle().Which;
        evento.Propriedades["Operacao"].Should().Be("Desconexao");
        evento.Propriedades["Motivo"].Should().Be("usuario");
        evento.Propriedades["GitHubLogin"].Should().Be("ana-m");
    }

    [Theory]
    [InlineData("https://exemplo.com")]
    [InlineData("//exemplo.com")]
    [InlineData(null)]
    public async Task Desconectar_com_retorno_externo_vai_para_desconectada(string? retorno)
    {
        Destino(await CopilotGitHubConfiguration.DesconectarAsync(Contexto(), null, retorno)).Should().Be("/copilot?conexao=desconectada");
    }

    [Fact]
    public async Task Desconectar_por_nao_membro_vai_sempre_para_nao_membro_desconectada()
    {
        var resultado = await CopilotGitHubConfiguration.DesconectarAsync(Contexto(), "nao-membro", "copilot/pessoas/ana-m");

        Destino(resultado).Should().Be("/copilot?conexao=nao-membro-desconectada");
        _log.Eventos.Single().Propriedades["Motivo"].Should().Be("nao-membro");
    }

    [Fact]
    public async Task Desconectar_sem_conexao_nao_registra_e_sem_esquema_nao_apaga_cookie()
    {
        SemEsquemas();

        var resultado = await CopilotGitHubConfiguration.DesconectarAsync(Contexto(conectado: false), null, null);

        Destino(resultado).Should().Be("/copilot?conexao=desconectada");
        _log.Eventos.Should().BeEmpty();
        _autenticacao.VerifyNoOtherCalls();
    }

    // ---------- Esquemas ----------

    [Theory]
    [InlineData("", "https://localhost:7271/copilot/github/retorno")]
    [InlineData("/siat_dsv", "https://localhost:7271/siat_dsv/copilot/github/retorno")]
    public async Task Desafio_manda_ao_GitHub_o_retorno_sob_a_raiz_do_site_sem_escopo_e_com_PKCE(string pathBase, string retorno)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Copilot:Organizacao"] = "tcmrj",
            ["Copilot:OAuth:ClientId"] = "Ov23cliente",
            ["Copilot:OAuth:ClientSecret"] = CopilotOptionsTests.Segredo,
        });
        builder.Services.AddAuthentication();
        builder.AddCopilotGitHubConfiguration();
        await using var app = builder.Build();
        await using var escopo = app.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = escopo.ServiceProvider };
        contexto.Request.Scheme = "https";
        contexto.Request.Host = new HostString("localhost", 7271);
        contexto.Request.PathBase = pathBase;

        await contexto.ChallengeAsync(CopilotGitHubConfiguration.EsquemaOAuth, new AuthenticationProperties { RedirectUri = "/copilot" });

        var destino = new Uri(contexto.Response.Headers.Location.ToString());
        destino.GetLeftPart(UriPartial.Path).Should().Be("https://github.com/login/oauth/authorize");
        var parametros = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(destino.Query);
        parametros["client_id"].ToString().Should().Be("Ov23cliente");
        parametros["redirect_uri"].ToString().Should().Be(retorno);
        parametros["scope"].ToString().Should().BeEmpty();
        parametros["code_challenge_method"].ToString().Should().Be("S256");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Esquemas_so_existem_com_a_OAuth_App_completa(bool completa)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        var configuracao = new Dictionary<string, string?> { ["Copilot:Organizacao"] = "tcmrj" };
        if (completa)
        {
            configuracao["Copilot:OAuth:ClientId"] = "Iv1.cliente";
            configuracao["Copilot:OAuth:ClientSecret"] = CopilotOptionsTests.Segredo;
        }

        builder.Configuration.AddInMemoryCollection(configuracao);
        builder.Services.AddAuthentication();
        builder.AddCopilotGitHubConfiguration();
        await using var app = builder.Build();

        var esquemas = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        (await esquemas.GetSchemeAsync(CopilotGitHubConfiguration.EsquemaOAuth) is not null).Should().Be(completa);
        (await esquemas.GetSchemeAsync(ConexaoGitHubService.TipoDeAutenticacao) is not null).Should().Be(completa);
        (await esquemas.GetDefaultChallengeSchemeAsync())?.Name.Should().NotBe(CopilotGitHubConfiguration.EsquemaOAuth);
    }
}
