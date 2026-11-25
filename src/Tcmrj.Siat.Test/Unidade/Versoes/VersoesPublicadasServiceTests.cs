using System.Collections.Concurrent;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Serviço das versões em memória por servidor (specs/003-versoes-publicadas/research.md R5 e R10): validade e
/// leitura única por servidor, falha da tabela com resultado anterior, erro de arquivo dentro da célula, cancelamento
/// até a origem e andamento da leitura. Repositório e leitor de arquivo falsos; tempo por <see cref="FakeTimeProvider"/>.
/// </summary>
public sealed class VersoesPublicadasServiceTests : IDisposable
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);

    private static readonly Servidor Dsv = new() { Chave = "tcmbd3-dev", Nome = @"TCMBD3\DEV", Endereco = @"TCMBD3\DEV", Ambiente = Ambiente.Dsv };
    private static readonly Servidor Hml = new() { Chave = "tcmbd4-hml", Nome = @"TCMBD4\HML", Endereco = @"TCMBD4\HML", Ambiente = Ambiente.Hml };

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero));
    private readonly RepositorioFalso _repositorio = new();
    private readonly ArquivosFalsos _arquivos = new();
    private readonly ServiceProvider _provider;
    private readonly VersoesPublicadasService _servico;

    public VersoesPublicadasServiceTests()
    {
        var opcoes = new Mock<IOptionsMonitor<CacheDeVersoesOptions>>();
        opcoes.Setup(o => o.CurrentValue).Returns(new CacheDeVersoesOptions { MinutosDeCache = 5, TempoLimiteSegundos = 5, LeiturasSimultaneas = 4 });

        _provider = new ServiceCollection()
            .AddSingleton<IEnderecosDeVersaoRepository>(_repositorio)
            .AddSingleton<ILeitorDeArquivoDeVersao>(_arquivos)
            .AddSingleton(opcoes.Object)
            .AddSingleton<TimeProvider>(_relogio)
            .AddLogging()
            .AddTransient<LeitorDeVersoes>()
            .BuildServiceProvider();

        _servico = new VersoesPublicadasService(
            _provider.GetRequiredService<IServiceScopeFactory>(), opcoes.Object, _relogio, NullLogger<VersoesPublicadasService>.Instance);
    }

    public void Dispose() => _provider.Dispose();

    private static CancellationToken Teste => TestContext.Current.CancellationToken;

    private Task<Result<VersoesObtidas, Erro>> Obter(Servidor servidor, bool atualizar = false) => _servico.ObterAsync(servidor, atualizar, Teste);

    private static VersoesObtidas Sucesso(Result<VersoesObtidas, Erro> resultado)
    {
        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        return resultado.Value;
    }

    [Fact]
    public async Task Servidores_diferentes_tem_resultados_e_validades_separados()
    {
        var dsv = Sucesso(await Obter(Dsv));
        _relogio.Advance(TimeSpan.FromMinutes(3));
        var hml = Sucesso(await Obter(Hml));
        _relogio.Advance(TimeSpan.FromMinutes(3));

        dsv.Versoes.Servidor.Should().BeSameAs(Dsv);
        hml.Versoes.Servidor.Should().BeSameAs(Hml);
        await Obter(Dsv);
        await Obter(Hml);

        _repositorio.Consultas(Dsv).Should().Be(2, "DSV venceu");
        _repositorio.Consultas(Hml).Should().Be(1, "HML ainda vale");
    }

    [Fact]
    public async Task Dentro_da_validade_nao_rele_e_Atualizar_rele()
    {
        var primeira = Sucesso(await Obter(Dsv));
        _relogio.Advance(TimeSpan.FromMinutes(4));

        Sucesso(await Obter(Dsv)).Versoes.Should().BeSameAs(primeira.Versoes);
        _repositorio.Consultas(Dsv).Should().Be(1);

        Sucesso(await Obter(Dsv, atualizar: true)).Versoes.Should().NotBeSameAs(primeira.Versoes);
        _repositorio.Consultas(Dsv).Should().Be(2);
    }

    [Fact]
    public async Task Pedidos_simultaneos_do_mesmo_servidor_consultam_a_tabela_uma_vez()
    {
        var liberar = _repositorio.Prender();

        var a = Obter(Dsv);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        var b = Obter(Dsv);
        liberar.SetResult();

        Sucesso(await b.WaitAsync(Espera, Teste)).Versoes.Should().BeSameAs(Sucesso(await a.WaitAsync(Espera, Teste)).Versoes);
        _repositorio.Consultas(Dsv).Should().Be(1);
    }

    [Fact]
    public async Task Falha_da_tabela_com_resultado_anterior_devolve_o_anterior_com_a_falha()
    {
        var anterior = Sucesso(await Obter(Dsv)).Versoes;
        _repositorio.Proximo = Erros.Versoes.TempoEsgotado(Dsv.Nome);

        var obtidas = Sucesso(await Obter(Dsv, atualizar: true));

        obtidas.Versoes.Should().BeSameAs(anterior);
        obtidas.Falha!.Erro.Code.Should().Be(Erros.Versoes.CodigoTempoEsgotado);
    }

    [Fact]
    public async Task Erro_de_arquivo_fica_na_celula_e_nao_e_falha_da_leitura()
    {
        _arquivos.Erro = "Arquivo não encontrado.";

        var obtidas = Sucesso(await Obter(Dsv));

        obtidas.Falha.Should().BeNull();
        obtidas.Versoes.Sistemas.Single().Celulas[0].Mensagem.Should().Be("Arquivo não encontrado.");
    }

    [Fact]
    public async Task Cancelar_o_ultimo_que_espera_cancela_o_token_da_tabela()
    {
        _repositorio.Prender();
        using var cancelamento = new CancellationTokenSource();

        var pedido = _servico.ObterAsync(Dsv, false, cancelamento.Token);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        await cancelamento.CancelAsync();

        (await pedido.WaitAsync(Espera, Teste)).Error.Should().Be(Erros.Versoes.Cancelada);
        _repositorio.TokenRecebido.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelar_o_ultimo_que_espera_cancela_o_token_dos_arquivos_e_limpa_o_andamento()
    {
        _arquivos.Prender();
        using var cancelamento = new CancellationTokenSource();

        var pedido = _servico.ObterAsync(Dsv, false, cancelamento.Token);
        await _arquivos.Iniciou.WaitAsync(Espera, Teste);
        _servico.QuantidadeEmLeitura(Dsv.Chave).Should().Be(1);
        await cancelamento.CancelAsync();

        (await pedido.WaitAsync(Espera, Teste)).Error.Should().Be(Erros.Versoes.Cancelada);
        await _arquivos.Terminou.WaitAsync(Espera, Teste);
        _arquivos.TokenRecebido.IsCancellationRequested.Should().BeTrue();
        await EsperarAsync(() => _servico.QuantidadeEmLeitura(Dsv.Chave) is null);
    }

    [Fact]
    public async Task Andamento_e_nulo_na_tabela_vale_N_durante_a_leitura_so_daquele_servidor_e_volta_a_nulo()
    {
        _servico.QuantidadeEmLeitura(Dsv.Chave).Should().BeNull();
        var liberarTabela = _repositorio.Prender();
        var liberarArquivos = _arquivos.Prender();

        var pedido = Obter(Dsv);
        await _repositorio.Iniciou.WaitAsync(Espera, Teste);
        _servico.QuantidadeEmLeitura(Dsv.Chave).Should().BeNull("a tabela ainda está sendo lida");

        liberarTabela.SetResult();
        await _arquivos.Iniciou.WaitAsync(Espera, Teste);
        _servico.QuantidadeEmLeitura(Dsv.Chave).Should().Be(1);
        _servico.QuantidadeEmLeitura(Hml.Chave).Should().BeNull();

        liberarArquivos.SetResult();
        Sucesso(await pedido.WaitAsync(Espera, Teste));
        _servico.QuantidadeEmLeitura(Dsv.Chave).Should().BeNull();
    }

    [Fact]
    public async Task Andamento_volta_a_nulo_depois_de_uma_falha_da_tabela()
    {
        _repositorio.Proximo = Erros.Versoes.TabelaIndisponivel(Dsv.Nome, "fora do ar");

        (await Obter(Dsv)).IsFailure.Should().BeTrue();

        _servico.QuantidadeEmLeitura(Dsv.Chave).Should().BeNull();
    }

    // A limpeza do andamento acontece no fim da tarefa de leitura, logo depois de quem esperava ser solto.
    private static async Task EsperarAsync(Func<bool> condicao)
    {
        for (var tentativa = 0; !condicao(); tentativa++)
        {
            tentativa.Should().BeLessThan(1_000_000, "a condição deveria ser atendida logo");
            await Task.Yield();
        }
    }

    /// <summary>Tabela falsa: uma linha por servidor; conta as consultas, pode prender e devolver um erro uma vez.</summary>
    private sealed class RepositorioFalso : IEnderecosDeVersaoRepository
    {
        private readonly ConcurrentDictionary<string, int> _consultas = new();
        private TaskCompletionSource? _liberacao;
        private TaskCompletionSource _iniciou = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Erro? Proximo { get; set; }

        public CancellationToken TokenRecebido { get; private set; }

        public Task Iniciou => _iniciou.Task;

        public int Consultas(Servidor servidor) => _consultas.GetValueOrDefault(servidor.Chave);

        public TaskCompletionSource Prender()
        {
            _iniciou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _liberacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<Result<IReadOnlyList<EnderecoDeVersao>, Erro>> ListarAtivosAsync(Servidor servidor, CancellationToken cancellationToken)
        {
            _consultas.AddOrUpdate(servidor.Chave, 1, (_, n) => n + 1);
            TokenRecebido = cancellationToken;
            var liberacao = _liberacao;
            _liberacao = null;
            _iniciou.TrySetResult();
            if (liberacao is not null)
            {
                await liberacao.Task.WaitAsync(cancellationToken);
            }

            if (Proximo is { } erro)
            {
                Proximo = null;
                return erro;
            }

            IReadOnlyList<EnderecoDeVersao> linhas =
            [
                new EnderecoDeVersao(1, "SGQ", new Dictionary<(Ambiente, Maquina), ParDeEndereco>
                {
                    [(Ambiente.Dsv, Maquina.Maquina1)] = new(@"\\tcm2016web3\TcmRoot\DSV\Sites\sgq\VERSAO.TXT", null),
                }),
            ];
            return Result.Success<IReadOnlyList<EnderecoDeVersao>, Erro>(linhas);
        }
    }

    /// <summary>Arquivos falsos: respondem "2.0.3" ou o erro pedido; podem prender a próxima leitura.</summary>
    private sealed class ArquivosFalsos : ILeitorDeArquivoDeVersao
    {
        private TaskCompletionSource? _liberacao;
        private TaskCompletionSource _iniciou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _terminou = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? Erro { get; set; }

        public CancellationToken TokenRecebido { get; private set; }

        public Task Iniciou => _iniciou.Task;

        public Task Terminou => _terminou.Task;

        public TaskCompletionSource Prender()
        {
            _iniciou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _liberacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<Result<string, string>> LerAsync(string endereco, CancellationToken cancellationToken)
        {
            TokenRecebido = cancellationToken;
            var liberacao = _liberacao;
            _liberacao = null;
            _iniciou.TrySetResult();
            try
            {
                if (liberacao is not null)
                {
                    await liberacao.Task.WaitAsync(cancellationToken);
                }

                return Erro is { } erro ? Result.Failure<string, string>(erro) : Result.Success<string, string>("2.0.3");
            }
            finally
            {
                _terminou.TrySetResult();
            }
        }
    }
}
