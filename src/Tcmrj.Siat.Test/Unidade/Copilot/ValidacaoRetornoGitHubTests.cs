using System.Net;
using System.Security.Claims;
using System.Text;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Retorno do "Conectar ao GitHub" (specs/005-copilot-meu-consumo/contracts/conexao-github.md, Fluxo): conta lida
/// com o token do usuário, matrícula de quem pediu, associação com o token de leitura, cookie de 30 dias e registros.
/// </summary>
public sealed class ValidacaoRetornoGitHubTests : IDisposable
{
    private const string TokenDoUsuario = "gho_token-do-usuario-de-teste";

    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _relogio = new(Agora);
    private readonly Mock<IGitHubCopilotRepository> _repositorio = new(MockBehavior.Strict);
    private readonly LoggerGravador<RegistroConexaoGitHub> _log = new();
    private readonly HandlerDoUsuario _handler = new();

    public ValidacaoRetornoGitHubTests()
    {
        _repositorio.Setup(r => r.Organizacao).Returns("tcmrj");
        _handler.Corpo = """{"id":7,"login":"ana-m"}""";
    }

    public void Dispose() => _handler.Dispose();

    private ValidacaoRetornoGitHub Criar() =>
        new(_repositorio.Object, new RegistroConexaoGitHub(_log), _relogio, NullLogger<ValidacaoRetornoGitHub>.Instance);

    private static ClaimsPrincipal Usuario(string matricula = "123") =>
        new(new ClaimsIdentity([new Claim("matricula", matricula), new Claim("nome", "Ana Souza")], "Teste"));

    private static AuthenticationProperties Propriedades(string? matricula = "123")
    {
        var propriedades = new AuthenticationProperties { RedirectUri = "/copilot" };
        propriedades.Items[ValidacaoRetornoGitHub.PropriedadeMatricula] = matricula;
        return propriedades;
    }

    private void Associacao(Result<Associacao, Erro> resultado) =>
        _repositorio.Setup(r => r.ObterAssociacaoAsync("ana-m", It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    private Task<ClaimsIdentity> Validar(AuthenticationProperties? propriedades = null, ClaimsPrincipal? usuario = null) =>
        Criar().ValidarAsync(new HttpClient(_handler), TokenDoUsuario, usuario ?? Usuario(), propriedades ?? Propriedades(), TestContext.Current.CancellationToken);

    private async Task<RecusaDeConexaoGitHubException> Recusa(AuthenticationProperties? propriedades = null, ClaimsPrincipal? usuario = null)
    {
        var validar = () => Validar(propriedades, usuario);
        return (await validar.Should().ThrowAsync<RecusaDeConexaoGitHubException>()).Which;
    }

    [Fact]
    public async Task Membro_ativo_com_o_mesmo_id_e_a_mesma_matricula_cria_a_identidade_de_30_dias()
    {
        Associacao(new Associacao("active", "member", 7));
        var propriedades = Propriedades();

        var identidade = await Validar(propriedades);

        identidade.AuthenticationType.Should().Be("GitHubConexao");
        identidade.FindFirst("github_id")!.Value.Should().Be("7");
        identidade.FindFirst("github_login")!.Value.Should().Be("ana-m");
        identidade.FindFirst("matricula")!.Value.Should().Be("123");
        identidade.Claims.Should().NotContain(c => c.Value.Contains(TokenDoUsuario, StringComparison.Ordinal));
        propriedades.IsPersistent.Should().BeTrue();
        propriedades.ExpiresUtc.Should().Be(Agora.AddDays(30));
        propriedades.AllowRefresh.Should().BeFalse();
        propriedades.Items.Values.Should().NotContain(TokenDoUsuario);
        propriedades.GetTokens().Should().BeEmpty();

        var evento = _log.Eventos.Should().ContainSingle().Which;
        evento.Propriedades["Operacao"].Should().Be("Conexao");
        evento.Propriedades["GitHubId"].Should().Be(7L);

        var pedido = _handler.Pedidos.Should().ContainSingle().Subject;
        pedido.RequestUri.Should().Be(new Uri("https://api.github.com/user"));
        pedido.Headers.Authorization!.Parameter.Should().Be(TokenDoUsuario);
    }

    [Fact]
    public async Task Associacao_pendente_e_nao_membro()
    {
        Associacao(new Associacao("pending", "member", 7));

        var recusa = await Recusa();

        recusa.Motivo.Should().Be("nao-membro");
        recusa.Login.Should().Be("ana-m");
    }

    [Fact]
    public async Task Associacao_404_e_nao_membro()
    {
        Associacao(Erros.Copilot.NaoMembro("ana-m", "tcmrj"));

        (await Recusa()).Motivo.Should().Be("nao-membro");
    }

    [Fact]
    public async Task Id_diferente_na_associacao_e_nao_membro()
    {
        Associacao(new Associacao("active", "member", 8));

        (await Recusa()).Motivo.Should().Be("nao-membro");
    }

    [Fact]
    public async Task Matricula_diferente_da_guardada_e_outro_usuario()
    {
        (await Recusa(usuario: Usuario("456"))).Motivo.Should().Be("outro-usuario");
        (await Recusa(propriedades: Propriedades(null))).Motivo.Should().Be("outro-usuario");
    }

    [Fact]
    public async Task Falha_do_GitHub_na_associacao_e_falha()
    {
        Associacao(Erros.Copilot.GitHubIndisponivel);

        (await Recusa()).Motivo.Should().Be("falha");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.OK, """{"login":"ana-m"}""")]
    [InlineData(HttpStatusCode.OK, "não é json")]
    public async Task Conta_sem_resposta_valida_e_falha(HttpStatusCode codigo, string corpo)
    {
        _handler.Codigo = codigo;
        _handler.Corpo = corpo;

        (await Recusa()).Motivo.Should().Be("falha");
    }

    [Fact]
    public void Falha_redireciona_com_o_motivo_registra_recusa_e_cita_a_conta_no_nao_membro()
    {
        var volta = Criar().TratarFalha(new RecusaDeConexaoGitHubException("nao-membro", "ana-m", 7), Propriedades(), Usuario());

        volta.Should().Be("copilot/meu-consumo?conexao=nao-membro&conta=ana-m");
        var evento = _log.Eventos.Should().ContainSingle().Which;
        evento.Propriedades["Operacao"].Should().Be("Recusa");
        evento.Propriedades["Motivo"].Should().Be("nao-membro");
    }

    [Fact]
    public void State_invalido_ou_correlacao_e_falha()
    {
        var volta = Criar().TratarFalha(new AuthenticationFailureException("Correlation failed."), Propriedades(), usuarioAtual: null);

        volta.Should().Be("copilot/meu-consumo?conexao=falha");
        var evento = _log.Eventos.Single();
        evento.Propriedades["Motivo"].Should().Be("falha");
        evento.Propriedades["Matricula"].Should().Be("123");
        evento.Propriedades.Values.OfType<string>().Should().NotContain(v => v.Contains("Correlation", StringComparison.Ordinal));
    }

    [Fact]
    public void Acesso_negado_no_GitHub_e_negada()
    {
        Criar().TratarAcessoNegado(Propriedades(), Usuario()).Should().Be("copilot/meu-consumo?conexao=negada");
        _log.Eventos.Single().Propriedades["Motivo"].Should().Be("negada");
    }

    private sealed class HandlerDoUsuario : HttpMessageHandler
    {
        public List<HttpRequestMessage> Pedidos { get; } = [];

        public HttpStatusCode Codigo { get; set; } = HttpStatusCode.OK;

        public string Corpo { get; set; } = "{}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos.Add(request);
            return Task.FromResult(new HttpResponseMessage(Codigo) { Content = new StringContent(Corpo, Encoding.UTF8, "application/json") });
        }
    }
}
