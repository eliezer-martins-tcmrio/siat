using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Cache do catálogo (research.md R4, data-model.md): validade, releitura forçada, uma leitura por vez, leitura
/// anterior preservada em falha e cancelamento com interrupção na origem quando ninguém mais espera.
/// </summary>
public sealed class CatalogoSistemasServiceTests : IDisposable
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero));
    private readonly RepositorioFalso _repositorio;
    private readonly ServiceProvider _provider;
    private readonly CatalogoSistemasService _servico;

    public CatalogoSistemasServiceTests()
    {
        _repositorio = new RepositorioFalso(_relogio);
        _provider = new ServiceCollection()
            .AddSingleton<ICatalogoSistemasRepository>(_repositorio)
            .BuildServiceProvider();

        var opcoes = new Mock<IOptionsMonitor<CacheDoCatalogoOptions>>();
        opcoes.Setup(o => o.CurrentValue).Returns(new CacheDoCatalogoOptions { MinutosDeCache = 10 });
        _servico = new CatalogoSistemasService(
            _provider.GetRequiredService<IServiceScopeFactory>(), opcoes.Object, _relogio, NullLogger<CatalogoSistemasService>.Instance);
    }

    public void Dispose() => _provider.Dispose();

    private static CancellationToken Teste => TestContext.Current.CancellationToken;

    private Task<Result<CatalogoObtido, Erro>> Obter(bool atualizar = false) => _servico.ObterAsync(atualizar, Teste);

    private Task<Result<CatalogoObtido, Erro>> ObterCom(CancellationToken cancellationToken, bool atualizar = false) =>
        _servico.ObterAsync(atualizar, cancellationToken);

    private static CatalogoObtido Sucesso(Result<CatalogoObtido, Erro> resultado)
    {
        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        return resultado.Value;
    }

    [Fact]
    public async Task O_primeiro_pedido_le_a_origem()
    {
        var obtido = Sucesso(await Obter());

        _repositorio.Leituras.Should().Be(1);
        obtido.Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 1");
        obtido.Falha.Should().BeNull();
    }

    [Fact]
    public async Task Dentro_da_validade_nao_le_de_novo()
    {
        await Obter();
        _relogio.Advance(TimeSpan.FromMinutes(9));

        var obtido = Sucesso(await Obter());

        _repositorio.Leituras.Should().Be(1);
        obtido.Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 1");
    }

    [Fact]
    public async Task Depois_da_validade_le_de_novo()
    {
        await Obter();
        _relogio.Advance(TimeSpan.FromMinutes(10));

        var obtido = Sucesso(await Obter());

        _repositorio.Leituras.Should().Be(2);
        obtido.Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 2");
    }

    [Fact]
    public async Task Falha_sem_leitura_anterior_devolve_o_erro()
    {
        _repositorio.Proximo = Erros.Catalogo.AcessoRecusado;

        var resultado = await Obter();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoAcessoRecusado);
    }

    [Fact]
    public async Task Atualizar_le_de_novo_mesmo_dentro_da_validade()
    {
        await Obter();

        var obtido = Sucesso(await Obter(atualizar: true));

        _repositorio.Leituras.Should().Be(2);
        obtido.Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 2");
    }

    [Fact]
    public async Task Pedidos_simultaneos_fazem_uma_unica_leitura()
    {
        var liberar = _repositorio.Prender();

        var primeiro = Obter(atualizar: true);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        var segundo = Obter(atualizar: true);
        liberar.SetResult();

        var (a, b) = (Sucesso(await primeiro.WaitAsync(Espera, Teste)), Sucesso(await segundo.WaitAsync(Espera, Teste)));

        _repositorio.Leituras.Should().Be(1);
        b.Catalogo.Should().BeSameAs(a.Catalogo);
    }

    [Fact]
    public async Task Falha_com_leitura_anterior_devolve_a_anterior_com_a_falha()
    {
        var anterior = Sucesso(await Obter()).Catalogo;
        _relogio.Advance(TimeSpan.FromMinutes(2));
        _repositorio.Proximo = Erros.Catalogo.TempoEsgotado(10);

        var obtido = Sucesso(await Obter(atualizar: true));

        obtido.Catalogo.Should().BeSameAs(anterior);
        obtido.Falha.Should().NotBeNull();
        obtido.Falha!.Erro.Code.Should().Be(Erros.Catalogo.CodigoTempoEsgotado);
        obtido.Falha.Em.Should().Be(_relogio.GetUtcNow());
    }

    [Fact]
    public async Task Depois_de_uma_falha_o_pedido_seguinte_tenta_de_novo_mesmo_dentro_da_validade()
    {
        await Obter();
        _repositorio.Proximo = Erros.Catalogo.Indisponivel("fora do ar");
        await Obter(atualizar: true);

        var obtido = Sucesso(await Obter());

        _repositorio.Leituras.Should().Be(3);
        obtido.Falha.Should().BeNull("o sucesso depois da falha limpa a falha");
        obtido.Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 3");
    }

    [Fact]
    public async Task Cancelar_um_de_dois_pedidos_nao_cancela_a_leitura_do_outro()
    {
        var liberar = _repositorio.Prender();
        using var cancelamento = new CancellationTokenSource();

        var quemCancela = ObterCom(cancelamento.Token, atualizar: true);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        var quemEspera = Obter(atualizar: true);
        await cancelamento.CancelAsync();

        var cancelado = await quemCancela.WaitAsync(Espera, Teste);
        cancelado.IsFailure.Should().BeTrue();
        cancelado.Error.Code.Should().Be(Erros.Catalogo.CodigoCancelada);
        _repositorio.TokenRecebido.IsCancellationRequested.Should().BeFalse();

        liberar.SetResult();
        Sucesso(await quemEspera.WaitAsync(Espera, Teste)).Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 1");
        _repositorio.Leituras.Should().Be(1);
    }

    [Fact]
    public async Task Quando_o_unico_pedido_cancela_a_leitura_e_interrompida_na_origem()
    {
        _repositorio.Prender();
        using var cancelamento = new CancellationTokenSource();

        var pedido = ObterCom(cancelamento.Token);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        await cancelamento.CancelAsync();

        var resultado = await pedido.WaitAsync(Espera, Teste);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoCancelada);
        _repositorio.TokenRecebido.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Quando_o_ultimo_que_restava_cancela_a_leitura_e_interrompida()
    {
        _repositorio.Prender();
        using var primeiro = new CancellationTokenSource();
        using var segundo = new CancellationTokenSource();

        var a = ObterCom(primeiro.Token);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        var b = ObterCom(segundo.Token);

        await primeiro.CancelAsync();
        await a.WaitAsync(Espera, Teste);
        _repositorio.TokenRecebido.IsCancellationRequested.Should().BeFalse();

        await segundo.CancelAsync();
        (await b.WaitAsync(Espera, Teste)).Error.Code.Should().Be(Erros.Catalogo.CodigoCancelada);
        _repositorio.TokenRecebido.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Leitura_cancelada_nao_muda_o_estado_e_o_pedido_seguinte_le_de_novo()
    {
        var anterior = Sucesso(await Obter()).Catalogo;
        _repositorio.Prender();
        using var cancelamento = new CancellationTokenSource();

        var pedido = ObterCom(cancelamento.Token, atualizar: true);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        await cancelamento.CancelAsync();
        await pedido.WaitAsync(Espera, Teste);
        await _repositorio.Terminou.WaitAsync(Espera, Teste);
        _repositorio.Soltar();

        var dentroDaValidade = Sucesso(await Obter());
        dentroDaValidade.Catalogo.Should().BeSameAs(anterior);
        dentroDaValidade.Falha.Should().BeNull();
        _repositorio.Leituras.Should().Be(2);

        var obtido = Sucesso(await Obter(atualizar: true));
        _repositorio.Leituras.Should().Be(3);
        obtido.Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 3");
    }

    [Fact]
    public async Task Novo_pedido_depois_do_cancelamento_nao_aproveita_a_leitura_cancelada()
    {
        _repositorio.Prender();
        using var cancelamento = new CancellationTokenSource();

        var cancelado = ObterCom(cancelamento.Token);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        await cancelamento.CancelAsync();
        await cancelado.WaitAsync(Espera, Teste);

        _repositorio.Soltar();
        var obtido = Sucesso(await Obter().WaitAsync(Espera, Teste));

        _repositorio.Leituras.Should().Be(2);
        obtido.Catalogo.Sistemas.Single().Nome.Should().Be("Leitura 2");
    }

    /// <summary>Repositório falso: conta as leituras, pode prender a leitura e devolve o erro pedido uma vez.</summary>
    private sealed class RepositorioFalso(TimeProvider relogio) : ICatalogoSistemasRepository
    {
        private int _leituras;
        private TaskCompletionSource? _liberacao;
        private TaskCompletionSource _iniciou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _terminou = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Leituras => Volatile.Read(ref _leituras);

        public Erro? Proximo { get; set; }

        public CancellationToken TokenRecebido { get; private set; }

        public Task Iniciou => _iniciou.Task;

        public Task Terminou => _terminou.Task;

        /// <summary>A próxima leitura espera o <see cref="TaskCompletionSource"/> devolvido (ou o cancelamento).</summary>
        public TaskCompletionSource Prender()
        {
            _liberacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _iniciou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _liberacao;
        }

        public void Soltar() => _liberacao = null;

        public async Task<Result<CatalogoLido, Erro>> LerAsync(CancellationToken cancellationToken)
        {
            var numero = Interlocked.Increment(ref _leituras);
            TokenRecebido = cancellationToken;
            var liberacao = _liberacao;
            _iniciou.TrySetResult();
            try
            {
                if (liberacao is not null)
                {
                    try
                    {
                        await liberacao.Task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return Erros.Catalogo.Cancelada;
                    }
                }

                if (Proximo is { } erro)
                {
                    Proximo = null;
                    return erro;
                }

                return new CatalogoLido(
                    [new Sistema($"Leitura {numero}", null, new Dictionary<Domain.Enums.AmbienteDoCatalogo, IReadOnlyList<Instancia>>())],
                    0,
                    relogio.GetUtcNow(),
                    "https://git/origem");
            }
            finally
            {
                _terminou.TrySetResult();
            }
        }
    }
}
