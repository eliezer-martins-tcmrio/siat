using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;
using Tcmrj.Autenticacao.AspNetCore.Oidc.Configurations;
using Tcmrj.Autenticacao.AspNetCore.Oidc.Sessao;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Test.Interface.Infra;
using static Microsoft.Playwright.Assertions;

namespace Tcmrj.Siat.Test.Interface.Fluxos;

/// <summary>
/// Quickstart 19 a 22 no que dá para automatizar com a autenticação de teste: logout só por POST com antiforgery e
/// logout por inatividade com relógio falso. O SIA e o cookie reais não participam: o esquema de teste substitui
/// <c>Cookies</c>/<c>oidc</c> e <see cref="SessaoWebApplicationFactory"/> põe no lugar do <c>oidc</c> um falso que
/// só apaga o usuário de teste e volta para <c>/</c>. O que aqui prova "sessão encerrada" é o POST de
/// <c>Account/Logout</c> chegar ao servidor e o usuário de teste deixar de existir (a página volta 401).
/// </summary>
public sealed class SessaoTests : IClassFixture<SessaoWebApplicationFactory>
{
    private const int MinutosSemUso = 5;

    private readonly SessaoWebApplicationFactory _app;
    private readonly Navegador _navegador;

    public SessaoTests(SessaoWebApplicationFactory app, Navegador navegador)
    {
        _app = app;
        _navegador = navegador;

        _app.MailQueue.Limpar();
        _app.Registros.Limpar();
        _app.MailQueue.DefinirSmtps(new Smtp(1, "Notificações"));
        var postado = DateTime.Today.AddHours(9);
        _app.MailQueue.AdicionarMensagem(1, new MensagemEnfileirada
        {
            MessageId = "7C0A0001-0000-4000-8000-000000000000",
            Assunto = "Aviso de sessão",
            DataPostagem = postado,
            EnviarApos = postado,
            DataEnvio = postado.AddSeconds(30),
            Enviado = true,
            EhHtml = false,
            Corpo = "Corpo",
            Destinatarios = "ana.souza@tcm.exemplo",
        });
    }

    private static UsuarioDeTeste Usuario() => new("MailQueue.CanSelect");

    private static string Caminho => $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}";

    private async Task<IPage> AbrirMailQueueAsync()
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, Usuario(), Caminho);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "MailQueue", Level = 1 })).ToBeVisibleAsync();
        await Expect(pagina.Locator(".lista-itens button.item-linha")).ToHaveCountAsync(1);
        return pagina;
    }

    private static Task<IRequest> EsperarLogoutAsync(IPage pagina, float? tempoLimite = null) =>
        pagina.WaitForRequestAsync(
            r => r.Method == "POST" && r.Url.EndsWith("/Account/Logout", StringComparison.Ordinal),
            new() { Timeout = tempoLimite });

    [Fact]
    public async Task Cenario19_Sair_e_um_POST_com_antiforgery_e_encerra_a_sessao_e_o_POST_sem_o_token_e_recusado()
    {
        var pagina = await AbrirMailQueueAsync();

        var formulario = pagina.Locator("form[action$='Account/Logout']").Filter(new() { HasText = "Sair" });
        await Expect(formulario).ToHaveCountAsync(1);
        await Expect(formulario).ToHaveAttributeAsync("method", "post");
        var token = formulario.Locator("input[name='__RequestVerificationToken']");
        await Expect(token).ToHaveCountAsync(1);
        (await token.GetAttributeAsync("value")).Should().NotBeNullOrWhiteSpace();

        // Sem o token (outro site forjando o POST), a requisição é recusada e a sessão continua.
        var recusada = await pagina.Context.APIRequest.PostAsync("/Account/Logout", new() { MaxRedirects = 0 });
        recusada.Status.Should().Be(400);
        await pagina.ReloadAsync();
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "MailQueue", Level = 1 })).ToBeVisibleAsync();

        // Com o token, o clique em Sair encerra a sessão: o usuário de teste some e a página pede login (401 no teste).
        var logout = EsperarLogoutAsync(pagina);
        await formulario.GetByRole(AriaRole.Button, new() { Name = "Sair" }).ClickAsync();
        await logout;
        await Expect(pagina).ToHaveURLAsync(new Regex(@"^http://127\.0\.0\.1:\d+/$"));
        var depois = await pagina.Context.APIRequest.GetAsync(Caminho, new() { MaxRedirects = 0 });
        depois.Status.Should().Be(401);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario20_GET_em_Account_Logout_volta_para_a_pagina_inicial_sem_encerrar_a_sessao()
    {
        var contexto = await _app.NovoContextoAsync(_navegador.Browser, Usuario());

        var resposta = await contexto.APIRequest.GetAsync("/Account/Logout", new() { MaxRedirects = 0 });
        resposta.Status.Should().Be(302);
        new Uri(_app.Endereco, resposta.Headers["location"]).AbsolutePath.Should().Be("/");

        var pagina = await contexto.NewPageAsync();
        await pagina.GotoAsync("/Account/Logout");
        await Expect(pagina).ToHaveURLAsync(new Regex(@"^http://127\.0\.0\.1:\d+/"));
        // Um link ou imagem de outro site não desloga ninguém: o usuário segue autenticado.
        await Expect(pagina.GetByRole(AriaRole.Link, new() { Name = "MailQueue" })).ToBeVisibleAsync();
        await contexto.CloseAsync();
    }

    [Fact]
    public async Task Cenario21_consultas_seguidas_mantem_a_sessao()
    {
        var pagina = await AbrirMailQueueAsync();
        var logouts = 0;
        pagina.Request += (_, r) =>
        {
            if (r.Method == "POST" && r.Url.EndsWith("/Account/Logout", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref logouts);
            }
        };
        var consultar = pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true });

        // Cada consulta renova o prazo: 3 x 4 minutos de relógio, mas nunca 5 minutos sem interação.
        for (var i = 0; i < 3; i++)
        {
            _app.Tempo.Advance(TimeSpan.FromMinutes(MinutosSemUso - 1));
            var chamadas = _app.MailQueue.ChamadasPesquisa;
            await consultar.ClickAsync();
            await EsperarAsync(() => _app.MailQueue.ChamadasPesquisa > chamadas);
            await EsperarRevalidacoesAsync();
        }

        Volatile.Read(ref logouts).Should().Be(0);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "MailQueue", Level = 1 })).ToBeVisibleAsync();
        await Expect(pagina.Locator(".lista-itens button.item-linha")).ToHaveCountAsync(1);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario22_tela_parada_alem_do_limite_encerra_a_sessao()
    {
        var pagina = await AbrirMailQueueAsync();

        // Nenhuma interação além do limite: a revalidação torna o circuito anônimo e a página envia o POST do logout.
        var logout = EsperarLogoutAsync(pagina);
        _app.Tempo.Advance(TimeSpan.FromMinutes(MinutosSemUso + 1));
        await logout;

        await Expect(pagina).ToHaveURLAsync(new Regex(@"^http://127\.0\.0\.1:\d+/$"));
        var depois = await pagina.Context.APIRequest.GetAsync(Caminho, new() { MaxRedirects = 0 });
        depois.Status.Should().Be(401);
        await pagina.Context.CloseAsync();
    }

    /// <summary>Tempo real para o circuito revalidar algumas vezes (o intervalo de teste é de 200 ms).</summary>
    private static Task EsperarRevalidacoesAsync() => Task.Delay(SessaoWebApplicationFactory.IntervaloDeRevalidacao * 5);

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        var limite = DateTime.UtcNow.AddSeconds(Navegador.TempoLimite / 1000);
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new Xunit.Sdk.XunitException("A condição não foi atendida a tempo.");
            }

            await Task.Delay(50);
        }
    }
}

/// <summary>
/// Host de teste da sessão: relógio falso (<see cref="Tempo"/>), <c>TcmrjSessao:MinutosSemUsoParaLogout</c> reduzido e
/// revalidação a cada <see cref="IntervaloDeRevalidacao"/> (o pacote só aceita minutos inteiros e usa o relógio real
/// para o intervalo; o prazo de inatividade, esse sim, vem do <see cref="TimeProvider"/>). O <c>oidc</c> real, que
/// precisaria do SIA, é trocado por um falso que apaga o usuário de teste e redireciona para o <c>RedirectUri</c>.
/// </summary>
public sealed class SessaoWebApplicationFactory : SiatWebApplicationFactory
{
    public static readonly TimeSpan IntervaloDeRevalidacao = TimeSpan.FromMilliseconds(200);

    /// <summary>Relógio do host: só anda quando o teste manda.</summary>
    public FakeTimeProvider Tempo { get; } = new(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TcmrjSessao:MinutosSemUsoParaLogout"] = "5",
        }));
    }

    protected override void ConfigurarServicosDeTeste(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Tempo);

        // Nenhuma chamada ao SIA: o handler de "oidc" e de "Cookies" passa a ser o falso.
        services.RemoveAll<IAuthenticationHandlerProvider>();
        services.AddScoped<IAuthenticationHandlerProvider>(sp => new ProvedorDeHandlers(
            new AuthenticationHandlerProvider(sp.GetRequiredService<IAuthenticationSchemeProvider>())));

        // Mesmo registro do pacote, com o intervalo de revalidação curto.
        services.RemoveAll<TcmrjInatividadeAuthenticationStateProvider>();
        services.RemoveAll<AuthenticationStateProvider>();
        services.RemoveAll<ITcmrjInatividade>();
        services.AddScoped<TcmrjInatividadeAuthenticationStateProvider>(sp =>
            new InatividadeRapida(sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<IOptions<TcmrjSessaoOptions>>(), sp.GetRequiredService<TimeProvider>()));
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TcmrjInatividadeAuthenticationStateProvider>());
        services.AddScoped<ITcmrjInatividade>(sp => sp.GetRequiredService<TcmrjInatividadeAuthenticationStateProvider>());
    }

    private sealed class InatividadeRapida(ILoggerFactory loggerFactory, IOptions<TcmrjSessaoOptions> options, TimeProvider timeProvider)
        : TcmrjInatividadeAuthenticationStateProvider(loggerFactory, options, timeProvider)
    {
        protected override TimeSpan RevalidationInterval => IntervaloDeRevalidacao;
    }

    /// <summary>Entrega o <see cref="OidcFalso"/> para os esquemas do SIA e deixa os demais com o provedor real.</summary>
    private sealed class ProvedorDeHandlers(IAuthenticationHandlerProvider real) : IAuthenticationHandlerProvider
    {
        public async Task<IAuthenticationHandler?> GetHandlerAsync(HttpContext context, string authenticationScheme)
        {
            if (authenticationScheme is "oidc" or "Cookies")
            {
                var handler = new OidcFalso();
                await handler.InitializeAsync(new AuthenticationScheme(authenticationScheme, null, typeof(OidcFalso)), context);
                return handler;
            }

            return await real.GetHandlerAsync(context, authenticationScheme);
        }
    }

    /// <summary>
    /// Faz de conta do logout no SIA: apaga o cookie do usuário de teste e, no <c>oidc</c>, vai para o
    /// <c>RedirectUri</c> (como o <c>signout-callback-oidc</c> faria). Não autentica ninguém.
    /// </summary>
    private sealed class OidcFalso : IAuthenticationSignOutHandler
    {
        private HttpContext _contexto = default!;
        private AuthenticationScheme _esquema = default!;

        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
        {
            _esquema = scheme;
            _contexto = context;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(AuthenticationProperties? properties)
        {
            if (_esquema.Name == "oidc")
            {
                _contexto.Response.Cookies.Delete(AutenticacaoDeTeste.NomeCookie);
                _contexto.Response.Redirect(properties?.RedirectUri ?? "/");
            }

            return Task.CompletedTask;
        }
    }
}
