using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Conexão com o GitHub entregue ao circuito (specs/005-copilot-meu-consumo/research.md R3): o cookie só vale para o
/// usuário que conectou, e nenhuma falha derruba a requisição.
/// </summary>
public sealed class ConexaoGitHubMiddlewareTests
{
    private const string Esquema = ConexaoGitHubService.TipoDeAutenticacao;

    private readonly Mock<IAuthenticationService> _autenticacao = new(MockBehavior.Strict);
    private bool _proximoChamado;

    private static ClaimsPrincipal Usuario(string? matricula = "123") =>
        new(new ClaimsIdentity(
            matricula is null ? [new Claim(ClaimTypes.Name, "fulano")] : [new Claim(ClaimTypes.Name, "fulano"), new Claim("matricula", matricula)],
            "Teste"));

    private static ClaimsPrincipal Cookie(string matricula = "123", string? id = "7", string? login = "ana-m")
    {
        var claims = new List<Claim> { new("matricula", matricula) };
        if (id is not null)
        {
            claims.Add(new Claim("github_id", id));
        }

        if (login is not null)
        {
            claims.Add(new Claim("github_login", login));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, Esquema));
    }

    private void Responder(AuthenticateResult resultado) =>
        _autenticacao.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), Esquema)).ReturnsAsync(resultado);

    private static AuthenticateResult Valido(ClaimsPrincipal principal) =>
        AuthenticateResult.Success(new AuthenticationTicket(principal, Esquema));

    private async Task<HttpContext> Executar(ClaimsPrincipal usuario, bool comEsquema = true)
    {
        var servicos = new ServiceCollection();
        servicos.AddSingleton(_autenticacao.Object);
        var esquemas = new Mock<IAuthenticationSchemeProvider>();
        esquemas.Setup(e => e.GetSchemeAsync(Esquema)).ReturnsAsync(
            comEsquema ? new AuthenticationScheme(Esquema, null, typeof(IAuthenticationHandler)) : null);
        servicos.AddSingleton(esquemas.Object);

        var contexto = new DefaultHttpContext { User = usuario, RequestServices = servicos.BuildServiceProvider() };
        var middleware = new ConexaoGitHubMiddleware(
            _ =>
            {
                _proximoChamado = true;
                return Task.CompletedTask;
            },
            NullLogger<ConexaoGitHubMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);
        return contexto;
    }

    [Fact]
    public async Task Cookie_da_mesma_matricula_acrescenta_a_identidade_depois_da_original()
    {
        Responder(Valido(Cookie()));

        var contexto = await Executar(Usuario());

        contexto.User.Identities.Should().HaveCount(2);
        contexto.User.Identities.First().AuthenticationType.Should().Be("Teste");
        var conexao = contexto.User.Identities.Last();
        conexao.AuthenticationType.Should().Be(Esquema);
        conexao.FindFirst("github_id")!.Value.Should().Be("7");
        conexao.FindFirst("github_login")!.Value.Should().Be("ana-m");
        conexao.FindFirst("matricula").Should().BeNull();
        contexto.User.FindFirst("matricula")!.Value.Should().Be("123");
        _proximoChamado.Should().BeTrue();
    }

    [Fact]
    public async Task Cookie_de_outra_matricula_e_ignorado()
    {
        Responder(Valido(Cookie(matricula: "456")));

        var contexto = await Executar(Usuario());

        contexto.User.Identities.Should().ContainSingle();
        _proximoChamado.Should().BeTrue();
    }

    [Fact]
    public async Task Cookie_ausente_ou_vencido_nao_acrescenta_nada()
    {
        Responder(AuthenticateResult.NoResult());

        (await Executar(Usuario())).User.Identities.Should().ContainSingle();
    }

    [Fact]
    public async Task Cookie_sem_id_ou_login_nao_acrescenta_nada()
    {
        Responder(Valido(Cookie(id: null)));

        (await Executar(Usuario())).User.Identities.Should().ContainSingle();
    }

    [Fact]
    public async Task Falha_da_autenticacao_nao_lanca()
    {
        _autenticacao.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), Esquema))
            .ThrowsAsync(new InvalidOperationException("chave de proteção indisponível"));

        var contexto = await Executar(Usuario());

        contexto.User.Identities.Should().ContainSingle();
        _proximoChamado.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_anonimo_nao_le_o_cookie()
    {
        var contexto = await Executar(new ClaimsPrincipal(new ClaimsIdentity()));

        _autenticacao.Verify(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()), Times.Never);
        contexto.User.Identities.Should().ContainSingle();
        _proximoChamado.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_sem_matricula_nao_le_o_cookie()
    {
        await Executar(Usuario(matricula: null));

        _autenticacao.Verify(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Sem_o_esquema_registrado_nao_le_o_cookie()
    {
        var contexto = await Executar(Usuario(), comEsquema: false);

        _autenticacao.Verify(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()), Times.Never);
        contexto.User.Identities.Should().ContainSingle();
    }
}
