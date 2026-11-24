using System.Net;
using System.Text;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Infra.Repositories;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Leitura do <c>sistemasDb.js</c> pela API de arquivos do GitLab (contracts/origem-sistemasdb.md) e mapeamento dos
/// erros (research.md R6). O token nunca aparece nas mensagens.
/// </summary>
public sealed class CatalogoSistemasRepositoryTests : IDisposable
{
    private const string Token = "glpat-segredo-de-teste";

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero));
    private readonly HandlerFalso _handler = new();
    private CatalogoSistemasOptions _opcoes = CatalogoSistemasOptionsTests.Validas();

    private CatalogoSistemasRepository Criar()
    {
        var monitor = new Mock<IOptionsMonitor<CatalogoSistemasOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(() => _opcoes);
        return new CatalogoSistemasRepository(
            new HttpClient(_handler), monitor.Object, _relogio, NullLogger<CatalogoSistemasRepository>.Instance);
    }

    public void Dispose() => _handler.Dispose();

    private Task<Result<CatalogoLido, Erro>> Ler() => Criar().LerAsync(TestContext.Current.CancellationToken);

    private static HttpResponseMessage Resposta(HttpStatusCode codigo, string? corpo = null) => new(codigo)
    {
        Content = new ByteArrayContent(corpo is null ? [] : Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(corpo)).ToArray()),
    };

    private Task<Erro> Falha() => FalhaCom(TestContext.Current.CancellationToken);

    private async Task<Erro> FalhaCom(CancellationToken cancellationToken)
    {
        var resultado = await Criar().LerAsync(cancellationToken);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Message.Should().NotContain(Token);
        return resultado.Error;
    }

    [Fact]
    public async Task Pede_o_arquivo_pela_API_com_o_token_e_monta_o_catalogo()
    {
        _handler.Responder = _ => Resposta(HttpStatusCode.OK, File.ReadAllText(LeitorSistemasDbTests.CaminhoDaAmostra));

        var resultado = await Ler();

        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        var pedido = _handler.Pedidos.Should().ContainSingle().Subject;
        pedido.Method.Should().Be(HttpMethod.Get);
        pedido.RequestUri!.AbsoluteUri.Should().Be(
            "https://git.tcmnet.tcm/api/v4/projects/asi%2FTCMRJDoc/repository/files/sistemasDb.js/raw?ref=master");
        pedido.Headers.GetValues("PRIVATE-TOKEN").Should().Equal(Token);

        resultado.Value.Sistemas.Should().HaveCount(10);
        resultado.Value.LidoEm.Should().Be(_relogio.GetUtcNow());
        resultado.Value.Origem.Should().Be("https://git.tcmnet.tcm/asi/TCMRJDoc/-/blob/master/sistemasDb.js");
    }

    [Fact]
    public async Task Sem_token_e_NaoConfigurado_sem_chamada_HTTP()
    {
        _opcoes.GitLab.Token = null;

        (await Falha()).Code.Should().Be(Erros.Catalogo.CodigoNaoConfigurado);
        _handler.Pedidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Opcoes_invalidas_sao_NaoConfigurado_sem_chamada_HTTP()
    {
        _opcoes.GitLab.BaseUrl = "git.tcmnet.tcm";

        (await Falha()).Code.Should().Be(Erros.Catalogo.CodigoNaoConfigurado);
        _handler.Pedidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Status_401_e_AcessoRecusado()
    {
        _handler.Responder = _ => Resposta(HttpStatusCode.Unauthorized, "{\"message\":\"401 Unauthorized\"}");

        (await Falha()).Code.Should().Be(Erros.Catalogo.CodigoAcessoRecusado);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Status_403_e_404_sao_ArquivoNaoEncontrado(HttpStatusCode codigo)
    {
        _handler.Responder = _ => Resposta(codigo);

        (await Falha()).Code.Should().Be(Erros.Catalogo.CodigoArquivoNaoEncontrado);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Found)]
    public async Task Outros_status_sao_Indisponivel(HttpStatusCode codigo)
    {
        _handler.Responder = _ => Resposta(codigo);

        var erro = await Falha();

        erro.Code.Should().Be(Erros.Catalogo.CodigoIndisponivel);
        erro.Message.Should().Contain(((int)codigo).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Falha_de_rede_e_Indisponivel()
    {
        _handler.Responder = _ => throw new HttpRequestException("Nenhuma conexão pôde ser feita");

        var erro = await Falha();

        erro.Code.Should().Be(Erros.Catalogo.CodigoIndisponivel);
        erro.Message.Should().Contain("Nenhuma conexão");
    }

    [Fact]
    public async Task Tempo_limite_sem_cancelamento_pedido_e_TempoEsgotado_com_os_segundos()
    {
        _opcoes.TempoLimiteSegundos = 7;
        _handler.Responder = _ => throw new TaskCanceledException("timeout", new TimeoutException());

        var erro = await Falha();

        erro.Code.Should().Be(Erros.Catalogo.CodigoTempoEsgotado);
        erro.Message.Should().Contain("7 s");
    }

    [Fact]
    public async Task Cancelamento_pedido_pelo_chamador_e_Cancelada_sem_excecao()
    {
        using var cancelamento = new CancellationTokenSource();
        _handler.ResponderAsync = async ct =>
        {
            await cancelamento.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return Resposta(HttpStatusCode.OK);
        };

        var erro = await FalhaCom(cancelamento.Token);

        erro.Code.Should().Be(Erros.Catalogo.CodigoCancelada);
    }

    [Fact]
    public async Task Arquivo_malformado_e_FormatoInvalido_com_linha_e_coluna()
    {
        _handler.Responder = _ => Resposta(HttpStatusCode.OK, "var data = [\n{ \"Title\": undefined }\n];");

        var erro = await Falha();

        erro.Code.Should().Be(Erros.Catalogo.CodigoFormatoInvalido);
        erro.Message.Should().Contain("linha 2, coluna 12");
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
