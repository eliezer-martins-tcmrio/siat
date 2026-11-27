using System.Net;
using System.Text;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Infra.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Leituras na API do GitHub (specs/005-copilot-meu-consumo/contracts/github-api.md): cabeçalhos, caminhos,
/// paginação e mapeamento dos erros. Nenhuma chamada sai da máquina; o token nunca aparece nas mensagens.
/// </summary>
public sealed class GitHubCopilotRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _relogio = new(Agora);
    private readonly HandlerFalso _handler = new();
    private readonly CopilotOptions _opcoes = CopilotOptionsTests.Validas();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _handler.Dispose();

    private GitHubCopilotRepository Criar()
    {
        var monitor = new Mock<IOptionsMonitor<CopilotOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(() => _opcoes);
        return new GitHubCopilotRepository(
            new HttpClient(_handler) { BaseAddress = new Uri("https://api.github.com/") },
            monitor.Object,
            _relogio,
            NullLogger<GitHubCopilotRepository>.Instance);
    }

    private static HttpResponseMessage Json(string corpo, HttpStatusCode codigo = HttpStatusCode.OK) =>
        new(codigo) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode codigo, params (string Nome, string Valor)[] cabecalhos)
    {
        var resposta = new HttpResponseMessage(codigo) { Content = new StringContent("{\"message\":\"segredo do corpo\"}") };
        foreach (var (nome, valor) in cabecalhos)
        {
            resposta.Headers.TryAddWithoutValidation(nome, valor);
        }

        return resposta;
    }

    private static Erro Falha<T>(Result<T, Erro> resultado)
    {
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Message.Should().NotContain(CopilotOptionsTests.Token);
        resultado.Error.Message.Should().NotContain("segredo do corpo");
        return resultado.Error;
    }

    private string Caminho(int indice = 0) => _handler.Pedidos[indice].RequestUri!.PathAndQuery;

    [Fact]
    public async Task Cabecalhos_de_cada_requisicao()
    {
        _handler.Responder = _ => Json("""{"state":"active","role":"member","user":{"id":7}}""");

        await Criar().ObterAssociacaoAsync("ana", Ct);

        var pedido = _handler.Pedidos.Should().ContainSingle().Subject;
        pedido.Method.Should().Be(HttpMethod.Get);
        pedido.Headers.Accept.Select(a => a.MediaType).Should().Equal("application/vnd.github+json");
        pedido.Headers.GetValues("X-GitHub-Api-Version").Should().Equal("2026-03-10");
        pedido.Headers.Authorization!.Scheme.Should().Be("Bearer");
        pedido.Headers.Authorization.Parameter.Should().Be(CopilotOptionsTests.Token);
        pedido.Headers.UserAgent.ToString().Should().Be("SIAT");
    }

    [Fact]
    public async Task Associacao_com_login_escapado()
    {
        _handler.Responder = _ => Json("""{"state":"pending","role":"member","user":{"id":7}}""");

        var associacao = await Criar().ObterAssociacaoAsync("ana m/x", Ct);

        Caminho().Should().Be("/orgs/tcmrj/memberships/ana%20m%2Fx");
        associacao.Value.Should().Be(new Associacao("pending", "member", 7));
        associacao.Value.AtivaPara(7).Should().BeFalse();
    }

    [Fact]
    public async Task Associacao_404_e_NaoMembro_com_login_e_organizacao()
    {
        _handler.Responder = _ => Status(HttpStatusCode.NotFound);

        var erro = Falha(await Criar().ObterAssociacaoAsync("ana", Ct));

        erro.Code.Should().Be(Erros.Copilot.CodigoNaoMembro);
        erro.Message.Should().Contain("ana").And.Contain("tcmrj");
    }

    [Fact]
    public async Task Perfil_por_id()
    {
        _handler.Responder = _ => Json("""{"id":7,"login":"ana-m","name":"Ana M","avatar_url":"https://avatars.githubusercontent.com/u/7"}""");

        var perfil = await Criar().ObterPerfilAsync(7, Ct);

        Caminho().Should().Be("/user/7");
        perfil.Value.Should().Be(new PerfilGitHub(7, "ana-m", "Ana M", "https://avatars.githubusercontent.com/u/7"));
    }

    [Fact]
    public async Task Licencas_paginadas_ate_o_total()
    {
        var assentos = string.Join(",", Enumerable.Range(1, 100).Select(i => $$$"""{"assignee":{"id":{{{i}}},"login":"u{{{i}}}"}}"""));
        _handler.Responder = p => p.RequestUri!.Query.EndsWith("&page=1", StringComparison.Ordinal)
            ? Json($$"""{"total_seats":102,"seats":[{{assentos}}]}""")
            : Json("""{"total_seats":102,"seats":[{"assignee":{"id":101,"login":"u101"}},{"assignee":null}]}""");

        var licencas = await Criar().ListarLicencasAsync(Ct);

        _handler.Pedidos.Should().HaveCount(2);
        Caminho(0).Should().Be("/orgs/tcmrj/copilot/billing/seats?per_page=100&page=1");
        Caminho(1).Should().Be("/orgs/tcmrj/copilot/billing/seats?per_page=100&page=2");
        licencas.Value.Should().HaveCount(101);
        licencas.Value[^1].Should().Be(new Licenca(101, "u101"));
    }

    [Fact]
    public async Task Licencas_com_pagina_vazia_antes_do_total_sao_RespostaInesperada()
    {
        _handler.Responder = _ => Json("""{"total_seats":5,"seats":[]}""");

        Falha(await Criar().ListarLicencasAsync(Ct)).Code.Should().Be(Erros.Copilot.CodigoRespostaInesperada);
    }

    [Fact]
    public async Task Budgets_ate_has_next_page_falso_so_os_considerados()
    {
        _handler.Responder = p => p.RequestUri!.Query.EndsWith("&page=1", StringComparison.Ordinal)
            ? Json("""
                {"budgets":[
                  {"id":"a","budget_scope":"organization","budget_amount":1000,"budget_product_sku":"ai_credits"},
                  {"id":"b","budget_scope":"user","budget_amount":80,"budget_product_skus":["ai_credits"],"user":"ana"}
                ],"has_next_page":true}
                """)
            : Json("""
                {"budgets":[
                  {"id":"c","budget_scope":"multi_user_customer","budget_amount":50,"budget_product_skus":["ai_credits"]},
                  {"id":"d","budget_scope":"user","budget_amount":10,"budget_product_sku":"actions"}
                ],"has_next_page":false}
                """);

        var budgets = await Criar().ListarBudgetsAsync(Ct);

        Caminho(0).Should().Be("/organizations/tcmrj/settings/billing/budgets?per_page=100&page=1");
        _handler.Pedidos.Should().HaveCount(2);
        budgets.Value.Should().Equal(
            new Budget("a", EscopoDeBudget.Organizacao, 1000, null),
            new Budget("b", EscopoDeBudget.Individual, 80, "ana"),
            new Budget("c", EscopoDeBudget.Padrao, 50, null));
    }

    [Fact]
    public async Task Times_e_membros_ate_pagina_com_menos_de_100_e_slug_escapado()
    {
        var cheia = "[" + string.Join(",", Enumerable.Range(1, 100).Select(i => $$"""{"login":"u{{i}}"}""")) + "]";
        _handler.Responder = p => p.RequestUri!.Query.EndsWith("&page=1", StringComparison.Ordinal)
            ? Json(cheia)
            : Json("""[{"login":"ana","inherited":true},{"login":"bia","inherited":false}]""");

        var membros = await Criar().ListarMembrosDoTimeAsync("time a", Ct);

        Caminho(0).Should().Be("/orgs/tcmrj/teams/time%20a/members?per_page=100&page=1");
        _handler.Pedidos.Should().HaveCount(2);
        membros.Value.Should().HaveCount(102);
        membros.Value[0].Should().Be(new MembroDoTime("u1", null));
        membros.Value[100].Should().Be(new MembroDoTime("ana", true));
        membros.Value[101].Should().Be(new MembroDoTime("bia", false));
    }

    [Fact]
    public async Task Times_da_organizacao()
    {
        _handler.Responder = _ => Json("""[{"id":1,"name":"Infra","slug":"infra"},{"id":2,"slug":"dev"}]""");

        var times = await Criar().ListarTimesAsync(Ct);

        Caminho().Should().Be("/orgs/tcmrj/teams?per_page=100&page=1");
        times.Value.Should().Equal(new TimeDaOrganizacao(1, "Infra", "infra"), new TimeDaOrganizacao(2, "dev", "dev"));
    }

    [Fact]
    public async Task Relatorio_diario_sem_zero_a_esquerda_e_mensal_sem_dia()
    {
        _handler.Responder = _ => Json("""
            {"usageItems":[{"model":"GPT-5","unitType":"credits","grossQuantity":10,"grossAmount":0.4,"netAmount":0.3}]}
            """);
        var repositorio = Criar();

        var diario = await repositorio.ObterRelatorioAsync("ana", 2026, 11, 7, Ct);
        await repositorio.ObterRelatorioAsync("ana", 2026, 3, null, Ct);

        Caminho(0).Should().Be("/organizations/tcmrj/settings/billing/ai_credit/usage?year=2026&month=11&day=7&user=ana");
        Caminho(1).Should().Be("/organizations/tcmrj/settings/billing/ai_credit/usage?year=2026&month=3&user=ana");
        diario.Value.Should().Equal(new ItemDeConsumo("GPT-5", "credits", 10, 0.4m, 0.3m));
    }

    [Fact]
    public async Task Opcoes_invalidas_sao_NaoConfigurado_sem_chamada()
    {
        _opcoes.GitHub.Token = null;

        Falha(await Criar().ListarTimesAsync(Ct)).Code.Should().Be(Erros.Copilot.CodigoNaoConfigurado);
        _handler.Pedidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Status_401_e_TokenRecusado()
    {
        _handler.Responder = _ => Status(HttpStatusCode.Unauthorized);

        Falha(await Criar().ListarTimesAsync(Ct)).Code.Should().Be(Erros.Copilot.CodigoTokenRecusado);
    }

    [Fact]
    public async Task Status_429_com_retry_after_e_LimiteDeRequisicoes_com_os_minutos()
    {
        _handler.Responder = _ => Status(HttpStatusCode.TooManyRequests, ("retry-after", "120"));

        var erro = Falha(await Criar().ListarTimesAsync(Ct));

        erro.Code.Should().Be(Erros.Copilot.CodigoLimiteDeRequisicoes);
        erro.Message.Should().Contain("Tente de novo em 2 minutos");
    }

    [Fact]
    public async Task Status_403_sem_requisicoes_restantes_usa_o_reset()
    {
        var reset = Agora.AddMinutes(5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        _handler.Responder = _ => Status(HttpStatusCode.Forbidden, ("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", reset));

        var erro = Falha(await Criar().ListarTimesAsync(Ct));

        erro.Code.Should().Be(Erros.Copilot.CodigoLimiteDeRequisicoes);
        erro.Message.Should().Contain("Tente de novo em 5 minutos");
    }

    [Fact]
    public async Task Status_403_comum_e_AcessoRecusadoPeloGitHub()
    {
        _handler.Responder = _ => Status(HttpStatusCode.Forbidden, ("x-ratelimit-remaining", "4999"));

        Falha(await Criar().ListarTimesAsync(Ct)).Code.Should().Be(Erros.Copilot.CodigoAcessoRecusadoPeloGitHub);
    }

    [Fact]
    public async Task Status_404_fora_da_associacao_e_NaoEncontrado()
    {
        _handler.Responder = _ => Status(HttpStatusCode.NotFound);

        Falha(await Criar().ObterPerfilAsync(7, Ct)).Code.Should().Be(Erros.Copilot.CodigoNaoEncontrado);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, Erros.Copilot.CodigoGitHubIndisponivel)]
    [InlineData(HttpStatusCode.BadGateway, Erros.Copilot.CodigoGitHubIndisponivel)]
    [InlineData(HttpStatusCode.Found, Erros.Copilot.CodigoRespostaInesperada)]
    public async Task Outros_status(HttpStatusCode codigo, string erro)
    {
        _handler.Responder = _ => Status(codigo);

        Falha(await Criar().ListarTimesAsync(Ct)).Code.Should().Be(erro);
    }

    [Fact]
    public async Task Tempo_esgotado_e_falha_de_rede_sao_GitHubIndisponivel()
    {
        _handler.Responder = _ => throw new TaskCanceledException("timeout", new TimeoutException());
        Falha(await Criar().ListarTimesAsync(Ct)).Code.Should().Be(Erros.Copilot.CodigoGitHubIndisponivel);

        _handler.Responder = _ => throw new HttpRequestException("sem rede");
        Falha(await Criar().ListarTimesAsync(Ct)).Code.Should().Be(Erros.Copilot.CodigoGitHubIndisponivel);
    }

    [Theory]
    [InlineData("""{"usageItems":"x"}""")]
    [InlineData("""{"usageItems":[{"model":"GPT-5","unitType":"credits"}]}""")]
    [InlineData("""{}""")]
    [InlineData("""não é json""")]
    public async Task Json_fora_do_formato_e_RespostaInesperada(string corpo)
    {
        _handler.Responder = _ => Json(corpo);

        Falha(await Criar().ObterRelatorioAsync("ana", 2026, 11, 7, Ct)).Code.Should().Be(Erros.Copilot.CodigoRespostaInesperada);
    }

    [Fact]
    public async Task Cancelamento_do_pedido_e_Cancelada()
    {
        using var cancelamento = new CancellationTokenSource();
        _handler.ResponderAsync = async ct =>
        {
            await cancelamento.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return Status(HttpStatusCode.OK);
        };

        Falha(await Criar().ListarTimesAsync(cancelamento.Token)).Code.Should().Be(Erros.Copilot.CodigoCancelada);
    }

    [Fact]
    public async Task Nao_segue_redirecionamento()
    {
        _handler.Responder = _ =>
        {
            var resposta = Status(HttpStatusCode.MovedPermanently);
            resposta.Headers.Location = new Uri("https://exemplo.com/");
            return resposta;
        };

        Falha(await Criar().ListarTimesAsync(Ct)).Code.Should().Be(Erros.Copilot.CodigoRespostaInesperada);
        _handler.Pedidos.Should().ContainSingle();
    }

    private sealed class HandlerFalso : HttpMessageHandler
    {
        public List<HttpRequestMessage> Pedidos { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        public Func<CancellationToken, Task<HttpResponseMessage>>? ResponderAsync { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos.Add(request);
            return ResponderAsync is not null ? await ResponderAsync(cancellationToken) : Responder(request);
        }
    }
}
