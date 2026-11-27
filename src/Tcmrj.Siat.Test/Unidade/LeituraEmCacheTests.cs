using System.Collections.Concurrent;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// <see cref="LeituraEmCache{TChave, TValor}"/> (specs/003-versoes-publicadas/research.md R5; estados em data-model.md):
/// validade, releitura forçada, uma leitura por vez, valor anterior preservado em falha, cancelamento com interrupção
/// na origem só quando ninguém mais espera e chaves independentes.
/// </summary>
public sealed class LeituraEmCacheTests
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);

    private static readonly Erro Cancelada = new("teste.cancelada", "Cancelada.");

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero));
    private readonly OrigemFalsa _origem;
    private readonly LeituraEmCache<string, Lido> _cache;

    public LeituraEmCacheTests()
    {
        _origem = new OrigemFalsa(_relogio);
        _cache = new LeituraEmCache<string, Lido>(
            _origem.LerAsync,
            lido => lido.Em,
            () => TimeSpan.FromMinutes(5),
            Cancelada,
            ex => new Erro("teste.inesperado", ex.Message),
            _relogio,
            NullLogger.Instance);
    }

    private static CancellationToken Teste => TestContext.Current.CancellationToken;

    private Task<Result<(Lido Valor, FalhaDeLeitura? Falha), Erro>> Obter(string chave = "a", bool atualizar = false) =>
        _cache.ObterAsync(chave, atualizar, Teste);

    private Task<Result<(Lido Valor, FalhaDeLeitura? Falha), Erro>> ObterCom(CancellationToken token, string chave = "a", bool atualizar = false) =>
        _cache.ObterAsync(chave, atualizar, token);

    private static (Lido Valor, FalhaDeLeitura? Falha) Sucesso(Result<(Lido Valor, FalhaDeLeitura? Falha), Erro> resultado)
    {
        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        return resultado.Value;
    }

    [Fact]
    public async Task O_primeiro_pedido_le_a_origem()
    {
        var (valor, falha) = Sucesso(await Obter());

        _origem.Leituras("a").Should().Be(1);
        valor.Numero.Should().Be(1);
        falha.Should().BeNull();
    }

    [Fact]
    public async Task Dentro_da_validade_nao_le_de_novo()
    {
        await Obter();
        _relogio.Advance(TimeSpan.FromMinutes(4));

        Sucesso(await Obter()).Valor.Numero.Should().Be(1);
        _origem.Leituras("a").Should().Be(1);
    }

    [Fact]
    public async Task Depois_da_validade_le_de_novo()
    {
        await Obter();
        _relogio.Advance(TimeSpan.FromMinutes(5));

        Sucesso(await Obter()).Valor.Numero.Should().Be(2);
    }

    [Fact]
    public async Task Atualizar_le_de_novo_mesmo_dentro_da_validade()
    {
        await Obter();

        Sucesso(await Obter(atualizar: true)).Valor.Numero.Should().Be(2);
    }

    [Fact]
    public async Task Pedidos_simultaneos_fazem_uma_unica_leitura()
    {
        var liberar = _origem.Prender();

        var primeiro = Obter(atualizar: true);
        await _origem.Iniciou.WaitAsync(Espera, Teste);
        var segundo = Obter(atualizar: true);
        liberar.SetResult();

        var a = Sucesso(await primeiro.WaitAsync(Espera, Teste));
        var b = Sucesso(await segundo.WaitAsync(Espera, Teste));
        _origem.Leituras("a").Should().Be(1);
        b.Valor.Should().BeSameAs(a.Valor);
    }

    [Fact]
    public async Task Falha_sem_valor_anterior_devolve_o_erro()
    {
        _origem.Proximo = new Erro("teste.fora", "Fora do ar.");

        var resultado = await Obter();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be("teste.fora");
    }

    [Fact]
    public async Task Falha_com_valor_anterior_devolve_o_anterior_com_a_falha_e_o_seguinte_tenta_de_novo()
    {
        var anterior = Sucesso(await Obter()).Valor;
        _relogio.Advance(TimeSpan.FromMinutes(1));
        _origem.Proximo = new Erro("teste.fora", "Fora do ar.");

        var comFalha = Sucesso(await Obter(atualizar: true));
        comFalha.Valor.Should().BeSameAs(anterior);
        comFalha.Falha!.Erro.Code.Should().Be("teste.fora");
        comFalha.Falha.Em.Should().Be(_relogio.GetUtcNow());

        var depois = Sucesso(await Obter());
        _origem.Leituras("a").Should().Be(3);
        depois.Falha.Should().BeNull("o sucesso limpa a falha");
        depois.Valor.Numero.Should().Be(3);
    }

    [Fact]
    public async Task Excecao_inesperada_vira_o_erro_informado()
    {
        _origem.Lancar = new InvalidOperationException("quebrou");

        var resultado = await Obter();

        resultado.Error.Code.Should().Be("teste.inesperado");
        resultado.Error.Message.Should().Be("quebrou");
    }

    [Fact]
    public async Task Pedido_ja_cancelado_recebe_o_erro_de_cancelamento_sem_ler()
    {
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        (await ObterCom(cancelamento.Token)).Error.Should().BeSameAs(Cancelada);
        _origem.Leituras("a").Should().Be(0);
    }

    [Fact]
    public async Task Quando_o_unico_pedido_cancela_a_leitura_e_interrompida_na_origem()
    {
        _origem.Prender();
        using var cancelamento = new CancellationTokenSource();

        var pedido = ObterCom(cancelamento.Token);
        await _origem.Iniciou.WaitAsync(Espera, Teste);
        await cancelamento.CancelAsync();

        (await pedido.WaitAsync(Espera, Teste)).Error.Code.Should().Be(Cancelada.Code);
        _origem.TokenRecebido.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelar_um_de_dois_pedidos_nao_cancela_a_leitura_do_outro()
    {
        var liberar = _origem.Prender();
        using var cancelamento = new CancellationTokenSource();

        var quemCancela = ObterCom(cancelamento.Token, atualizar: true);
        await _origem.Iniciou.WaitAsync(Espera, Teste);
        var quemEspera = Obter(atualizar: true);
        await cancelamento.CancelAsync();

        (await quemCancela.WaitAsync(Espera, Teste)).Error.Code.Should().Be(Cancelada.Code);
        _origem.TokenRecebido.IsCancellationRequested.Should().BeFalse();

        liberar.SetResult();
        Sucesso(await quemEspera.WaitAsync(Espera, Teste)).Valor.Numero.Should().Be(1);
        _origem.Leituras("a").Should().Be(1);
    }

    [Fact]
    public async Task Leitura_cancelada_nao_muda_o_estado_e_o_pedido_seguinte_le_de_novo()
    {
        var anterior = Sucesso(await Obter()).Valor;
        _origem.Prender();
        using var cancelamento = new CancellationTokenSource();

        var pedido = ObterCom(cancelamento.Token, atualizar: true);
        await _origem.Iniciou.WaitAsync(Espera, Teste);
        await cancelamento.CancelAsync();
        await pedido.WaitAsync(Espera, Teste);
        await _origem.Terminou.WaitAsync(Espera, Teste);
        _origem.Soltar();

        var dentroDaValidade = Sucesso(await Obter());
        dentroDaValidade.Valor.Should().BeSameAs(anterior);
        dentroDaValidade.Falha.Should().BeNull();

        Sucesso(await Obter(atualizar: true)).Valor.Numero.Should().Be(3);
    }

    [Fact]
    public async Task Chaves_diferentes_nao_compartilham_leitura_validade_nem_falha()
    {
        var liberar = _origem.Prender();
        var a = Obter("a");
        await _origem.Iniciou.WaitAsync(Espera, Teste);
        _origem.Soltar();

        // A leitura de "a" está presa; "b" lê sozinha, sem esperar por ela.
        var b = Sucesso(await Obter("b").WaitAsync(Espera, Teste));
        b.Valor.Chave.Should().Be("b");
        liberar.SetResult();
        Sucesso(await a.WaitAsync(Espera, Teste)).Valor.Chave.Should().Be("a");

        // Falha de "b" não aparece em "a".
        _origem.Proximo = new Erro("teste.fora", "Fora do ar.");
        Sucesso(await Obter("b", atualizar: true)).Falha.Should().NotBeNull();
        Sucesso(await Obter("a")).Falha.Should().BeNull();
        _origem.Leituras("a").Should().Be(1, "\"a\" continua na validade");

        // Validade separada: "b" lida de novo agora vale mais que "a".
        _relogio.Advance(TimeSpan.FromMinutes(4));
        await Obter("b", atualizar: true);
        _relogio.Advance(TimeSpan.FromMinutes(2));
        await Obter("a");
        await Obter("b");
        _origem.Leituras("a").Should().Be(2);
        _origem.Leituras("b").Should().Be(3);
    }

    // Valido(chave): espia o cache sem ler (specs/004-ficha-sistema/research.md R5).

    [Fact]
    public void Valido_antes_da_primeira_leitura_e_nulo()
    {
        _cache.Valido("a").Should().BeNull();
        _origem.Leituras("a").Should().Be(0);
    }

    [Fact]
    public async Task Valido_devolve_o_valor_dentro_da_validade_sem_ler()
    {
        var lido = Sucesso(await Obter()).Valor;
        _relogio.Advance(TimeSpan.FromMinutes(4));

        _cache.Valido("a").Should().BeSameAs(lido);
        _origem.Leituras("a").Should().Be(1);
    }

    [Fact]
    public async Task Valido_depois_da_validade_e_nulo_e_nao_le()
    {
        await Obter();
        _relogio.Advance(TimeSpan.FromMinutes(5));

        _cache.Valido("a").Should().BeNull();
        _origem.Leituras("a").Should().Be(1);
    }

    [Fact]
    public async Task Valido_e_nulo_quando_a_ultima_releitura_falhou()
    {
        await Obter();
        _origem.Proximo = new Erro("teste.fora", "Fora do ar.");
        Sucesso(await Obter(atualizar: true)).Falha.Should().NotBeNull();

        _cache.Valido("a").Should().BeNull();
    }

    [Fact]
    public async Task Valido_de_outra_chave_e_nulo()
    {
        await Obter("a");

        _cache.Valido("b").Should().BeNull();
    }

    [Fact]
    public async Task Valido_nao_entra_na_contagem_de_quem_espera()
    {
        _origem.Prender();
        using var cancelamento = new CancellationTokenSource();

        var pedido = ObterCom(cancelamento.Token);
        await _origem.Iniciou.WaitAsync(Espera, Teste);
        _cache.Valido("a").Should().BeNull();
        await cancelamento.CancelAsync();

        (await pedido.WaitAsync(Espera, Teste)).Error.Code.Should().Be(Cancelada.Code);
        _origem.TokenRecebido.IsCancellationRequested.Should().BeTrue("Valido não segura a leitura");
    }

    [Fact]
    public async Task Valido_durante_um_atualizar_devolve_o_anterior_ainda_na_validade()
    {
        var anterior = Sucesso(await Obter()).Valor;
        var liberar = _origem.Prender();

        var atualizando = Obter(atualizar: true);
        await _origem.Iniciou.WaitAsync(Espera, Teste);

        _cache.Valido("a").Should().BeSameAs(anterior);
        liberar.SetResult();
        Sucesso(await atualizando.WaitAsync(Espera, Teste)).Valor.Numero.Should().Be(2);
    }

    // ---------- Limpeza (specs/005-copilot-meu-consumo/research.md R8) ----------

    [Fact]
    public async Task Leitura_nova_tira_as_entradas_vencidas_ha_mais_que_a_validade()
    {
        await Obter("a");
        await Obter("b");
        _cache.Quantidade.Should().Be(2);
        _relogio.Advance(TimeSpan.FromMinutes(10));

        await Obter("c");

        _cache.Quantidade.Should().Be(1);
        _cache.Valido("a").Should().BeNull();
        Sucesso(await Obter("a")).Valor.Numero.Should().Be(2);
    }

    [Fact]
    public async Task Entrada_vencida_ha_menos_que_a_validade_continua()
    {
        await Obter("a");
        _relogio.Advance(TimeSpan.FromMinutes(9));

        await Obter("c");

        _cache.Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task Entrada_ainda_valida_continua()
    {
        await Obter("a");
        _relogio.Advance(TimeSpan.FromMinutes(4));

        await Obter("c");

        _cache.Quantidade.Should().Be(2);
        Sucesso(await Obter("a")).Valor.Numero.Should().Be(1);
    }

    [Fact]
    public async Task Entrada_com_leitura_em_curso_continua()
    {
        await Obter("a");
        _relogio.Advance(TimeSpan.FromMinutes(10));
        var liberacao = _origem.Prender();
        var relendo = Obter("a");
        await _origem.Iniciou.WaitAsync(Espera, Teste);
        _origem.Soltar();

        await Obter("c");

        _cache.Quantidade.Should().Be(2);
        liberacao.SetResult();
        Sucesso(await relendo.WaitAsync(Espera, Teste)).Valor.Numero.Should().Be(2);
    }

    /// <summary>Valor lido: a chave, o número da leitura daquela chave e quando.</summary>
    private sealed record Lido(string Chave, int Numero, DateTimeOffset Em);

    /// <summary>Origem falsa: conta as leituras por chave, pode prender a próxima e devolve o erro pedido uma vez.</summary>
    private sealed class OrigemFalsa(TimeProvider relogio)
    {
        private readonly ConcurrentDictionary<string, int> _leituras = new();
        private TaskCompletionSource? _liberacao;
        private TaskCompletionSource _iniciou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _terminou = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Erro? Proximo { get; set; }

        public Exception? Lancar { get; set; }

        public CancellationToken TokenRecebido { get; private set; }

        public Task Iniciou => _iniciou.Task;

        public Task Terminou => _terminou.Task;

        public int Leituras(string chave) => _leituras.GetValueOrDefault(chave);

        public TaskCompletionSource Prender()
        {
            _liberacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _iniciou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _liberacao;
        }

        public void Soltar() => _liberacao = null;

        public async Task<Result<Lido, Erro>> LerAsync(string chave, CancellationToken cancellationToken)
        {
            var numero = _leituras.AddOrUpdate(chave, 1, (_, n) => n + 1);
            TokenRecebido = cancellationToken;
            var liberacao = _liberacao;
            _iniciou.TrySetResult();
            try
            {
                if (liberacao is not null)
                {
                    await liberacao.Task.WaitAsync(cancellationToken);
                }

                if (Lancar is { } excecao)
                {
                    Lancar = null;
                    throw excecao;
                }

                if (Proximo is { } erro)
                {
                    Proximo = null;
                    return erro;
                }

                return new Lido(chave, numero, relogio.GetUtcNow());
            }
            finally
            {
                _terminou.TrySetResult();
            }
        }
    }
}
