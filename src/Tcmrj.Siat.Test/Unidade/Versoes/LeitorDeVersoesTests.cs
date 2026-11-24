using System.Collections.Concurrent;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Orquestração da leitura dos arquivos de versão (specs/003-versoes-publicadas/research.md R4 e R10): células na
/// ordem, deduplicação, sistema raiz antes, tempo limite por arquivo, limite de paralelismo, cancelamento, andamento e
/// ordem pt-BR. O leitor de arquivo é falso; o tempo, um <see cref="FakeTimeProvider"/>.
/// </summary>
public sealed class LeitorDeVersoesTests
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);

    private const string Raiz = @"\\unity\Corp2\VBSCP\SCP.INI";
    private const string Modelo = @"\\unity\Corp2\VBSCP\v.{0}\Assinador\VERSAO.TXT";
    private const string Montado = @"\\unity\Corp2\VBSCP\v.5.11.3\Assinador\VERSAO.TXT";

    private static readonly Servidor Servidor = new() { Chave = "tcmbd3-dev", Nome = @"TCMBD3\DEV", Endereco = @"TCMBD3\DEV", Ambiente = Ambiente.Dsv };

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero));
    private readonly LeitorFalso _arquivos = new();
    private readonly CacheDeVersoesOptions _opcoes = new() { TempoLimiteSegundos = 5, LeiturasSimultaneas = 32 };
    private readonly LeitorDeVersoes _leitor;

    public LeitorDeVersoesTests()
    {
        var opcoes = new Mock<IOptionsMonitor<CacheDeVersoesOptions>>();
        opcoes.Setup(o => o.CurrentValue).Returns(_opcoes);
        _leitor = new LeitorDeVersoes(_arquivos, opcoes.Object, _relogio, NullLogger<LeitorDeVersoes>.Instance);
    }

    private static CancellationToken Teste => TestContext.Current.CancellationToken;

    private static string Arq(string nome) => $@"\\tcm2016web3\TcmRoot\DSV\Sites\{nome}\VERSAO.TXT";

    private static EnderecoDeVersao Linha(int id, string sistema, params (Ambiente, Maquina, string?, string?)[] celulas) =>
        new(id, sistema, celulas.ToDictionary(c => (c.Item1, c.Item2), c => new ParDeEndereco(c.Item3, c.Item4)));

    private Task<VersoesLidas> Ler(params EnderecoDeVersao[] linhas) =>
        _leitor.LerAsync(Servidor, linhas, _relogio.GetTimestamp(), null, Teste);

    [Fact]
    public async Task Monta_as_seis_celulas_na_ordem_e_endereco_nulo_nao_e_lido()
    {
        _arquivos.Responder(Arq("a1"), "1.0.0");
        _arquivos.Responder(Arq("a2"), "1.0.1");

        var lidas = await Ler(Linha(1, "SGQ",
            (Ambiente.Prd, Maquina.Maquina2, Arq("a2"), null),
            (Ambiente.Dsv, Maquina.Maquina1, Arq("a1"), null)));

        var celulas = lidas.Sistemas.Single().Celulas;
        celulas.Select(c => (c.Ambiente, c.Maquina)).Should().Equal(EnderecoDeVersao.Posicoes);
        celulas[0].Versao.Should().Be("1.0.0");
        celulas[5].Versao.Should().Be("1.0.1");
        celulas.Skip(1).Take(4).Should().OnlyContain(c => c.Situacao == SituacaoDaCelula.NaoConfigurado && c.Caminho == null);
        _arquivos.Lidos.Should().BeEquivalentTo([Arq("a1"), Arq("a2")]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto longo sem a chave da versão")]
    public async Task Conteudo_vazio_ou_sem_VERSAO_e_nao_configurado_com_o_caminho(string conteudo)
    {
        _arquivos.Responder(Arq("a"), conteudo);

        var celula = (await Ler(Linha(1, "SGQ", (Ambiente.Dsv, Maquina.Maquina1, Arq("a"), null)))).Sistemas.Single().Celulas[0];

        celula.Situacao.Should().Be(SituacaoDaCelula.NaoConfigurado);
        celula.Caminho.Should().Be(Arq("a"));
    }

    [Fact]
    public async Task Endereco_repetido_mesmo_com_maiusculas_diferentes_e_lido_uma_vez()
    {
        _arquivos.Responder(Arq("comum"), "2.0.0");

        var lidas = await Ler(
            Linha(1, "A", (Ambiente.Dsv, Maquina.Maquina1, Arq("comum"), null), (Ambiente.Hml, Maquina.Maquina1, Arq("comum"), null)),
            Linha(2, "B", (Ambiente.Prd, Maquina.Maquina1, Arq("comum").ToUpperInvariant(), null)));

        _arquivos.Lidos.Should().ContainSingle();
        lidas.QuantidadeDeEnderecos.Should().Be(1);
        lidas.Sistemas.SelectMany(s => s.Celulas).Count(c => c.Versao == "2.0.0").Should().Be(3);
    }

    [Fact]
    public async Task O_sistema_raiz_e_lido_antes_e_uma_vez_e_o_endereco_e_montado_com_a_versao_dele()
    {
        _arquivos.Responder(Raiz, "[SCP]\r\nNOME=Sistema de Controle\r\nVERSAO=5.11.3\r\n");
        _arquivos.Responder(Montado, "1.2.0");

        var lidas = await Ler(
            Linha(3, "eTCM Assinador", (Ambiente.Dsv, Maquina.Maquina1, Modelo, Raiz), (Ambiente.Dsv, Maquina.Maquina2, Modelo, Raiz)),
            Linha(4, "eTCM Impressão", (Ambiente.Dsv, Maquina.Maquina1, Arq("impressao"), Raiz)));

        _arquivos.Lidos[0].Should().Be(Raiz);
        _arquivos.Lidos.Count(e => e == Raiz).Should().Be(1);
        var assinador = lidas.Sistemas.Single(s => s.Id == 3).Celulas[0];
        assinador.Versao.Should().Be("1.2.0");
        assinador.Caminho.Should().Be(Montado);
        assinador.CaminhoDoRaiz.Should().Be(Raiz);
    }

    [Fact]
    public async Task Raiz_com_erro_vira_erro_da_celula_sem_ler_o_endereco()
    {
        _arquivos.Responder(Raiz, Result.Failure<string, string>("Arquivo não encontrado."));

        var celula = (await Ler(Linha(3, "eTCM Assinador", (Ambiente.Dsv, Maquina.Maquina1, Modelo, Raiz)))).Sistemas.Single().Celulas[0];

        celula.Situacao.Should().Be(SituacaoDaCelula.Erro);
        celula.Mensagem.Should().Be("Versão do sistema raiz não obtida: Arquivo não encontrado.");
        celula.CaminhoDoRaiz.Should().Be(Raiz);
        celula.Caminho.Should().Be(Modelo);
        _arquivos.Lidos.Should().Equal(Raiz);
    }

    [Fact]
    public async Task Endereco_nao_suportado_vira_erro_sem_chamar_o_leitor()
    {
        var celula = (await Ler(Linha(1, "Local", (Ambiente.Dsv, Maquina.Maquina1, @"C:\inetpub\appsettings.json", null)))).Sistemas.Single().Celulas[0];

        celula.Situacao.Should().Be(SituacaoDaCelula.Erro);
        celula.Mensagem.Should().Be("Endereço não suportado.");
        celula.Caminho.Should().Be(@"C:\inetpub\appsettings.json");
        _arquivos.Lidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Leitor_que_nunca_responde_esgota_o_tempo_so_naquela_celula()
    {
        // Uma leitura por vez: a célula que responde termina antes de o relógio avançar.
        _opcoes.LeiturasSimultaneas = 1;
        _arquivos.Travar(Arq("fora"));
        _arquivos.Responder(Arq("ok"), "6.2.0");

        var tarefa = Ler(Linha(1, "STP", (Ambiente.Prd, Maquina.Maquina1, Arq("ok"), null), (Ambiente.Prd, Maquina.Maquina2, Arq("fora"), null)));
        await _arquivos.ChegouNoTravado.WaitAsync(Espera, Teste);
        tarefa.IsCompleted.Should().BeFalse();
        _relogio.Advance(TimeSpan.FromSeconds(5));
        var celulas = (await tarefa.WaitAsync(Espera, Teste)).Sistemas.Single().Celulas;

        celulas[4].Versao.Should().Be("6.2.0");
        celulas[5].Situacao.Should().Be(SituacaoDaCelula.Erro);
        celulas[5].Mensagem.Should().Be("Tempo limite de 5 s esgotado ao ler o arquivo.");
    }

    [Fact]
    public async Task Nunca_le_mais_que_LeiturasSimultaneas_ao_mesmo_tempo()
    {
        _opcoes.LeiturasSimultaneas = 2;
        var portao = _arquivos.Represar();
        var linhas = Enumerable.Range(1, 8).Select(i => Linha(i, $"S{i}", (Ambiente.Dsv, Maquina.Maquina1, Arq($"s{i}"), null))).ToArray();

        var tarefa = Ler(linhas);
        await _arquivos.Represados(2).WaitAsync(Espera, Teste);
        _arquivos.Maximo.Should().Be(2);
        portao.SetResult();
        await tarefa.WaitAsync(Espera, Teste);

        _arquivos.Maximo.Should().Be(2);
        _arquivos.Lidos.Should().HaveCount(8);
    }

    [Fact]
    public async Task Cancelar_a_leitura_geral_cancela_o_token_do_leitor()
    {
        _arquivos.EsperarCancelamento = true;
        using var cancelamento = new CancellationTokenSource();

        var tarefa = _leitor.LerAsync(Servidor, [Linha(1, "A", (Ambiente.Dsv, Maquina.Maquina1, Arq("a"), null))], _relogio.GetTimestamp(), null, cancelamento.Token);
        await _arquivos.Represados(1).WaitAsync(Espera, Teste);
        await cancelamento.CancelAsync();

        await tarefa.Awaiting(t => t.WaitAsync(Espera, Teste)).Should().ThrowAsync<OperationCanceledException>();
        _arquivos.Tokens.Single().IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task O_andamento_e_informado_uma_vez_antes_da_primeira_leitura()
    {
        var chamadas = new List<(int Quantidade, int LidosAntes)>();

        await _leitor.LerAsync(
            Servidor,
            [
                Linha(1, "A", (Ambiente.Dsv, Maquina.Maquina1, Arq("a"), null), (Ambiente.Dsv, Maquina.Maquina2, Arq("a"), null)),
                Linha(2, "B", (Ambiente.Dsv, Maquina.Maquina1, Modelo, Raiz), (Ambiente.Hml, Maquina.Maquina1, Arq("b"), null)),
            ],
            _relogio.GetTimestamp(),
            n => chamadas.Add((n, _arquivos.Lidos.Length)),
            Teste);

        chamadas.Should().Equal((4, 0));
    }

    [Fact]
    public async Task Resultado_tem_os_enderecos_distintos_a_duracao_e_a_ordem_pt_BR()
    {
        var inicio = _relogio.GetTimestamp();
        _relogio.Advance(TimeSpan.FromSeconds(2));

        var lidas = await _leitor.LerAsync(
            Servidor,
            [
                Linha(1, "INTERnet", (Ambiente.Dsv, Maquina.Maquina1, Arq("i"), null)),
                Linha(9, "eTCM Impressão", (Ambiente.Dsv, Maquina.Maquina1, Arq("e9"), null)),
                Linha(5, "eTCM Impressão", (Ambiente.Dsv, Maquina.Maquina1, Arq("e5"), null)),
                Linha(2, "Ágil", (Ambiente.Dsv, Maquina.Maquina1, Arq("i"), null)),
            ],
            inicio,
            null,
            Teste);

        lidas.Sistemas.Select(s => (s.Nome, s.Id)).Should().Equal(("Ágil", 2), ("eTCM Impressão", 5), ("eTCM Impressão", 9), ("INTERnet", 1));
        lidas.QuantidadeDeEnderecos.Should().Be(3);
        lidas.Duracao.Should().Be(TimeSpan.FromSeconds(2));
        lidas.GeradoEm.Should().Be(_relogio.GetUtcNow());
        lidas.Servidor.Should().BeSameAs(Servidor);
    }

    [Fact]
    public async Task Excecao_inesperada_do_leitor_fica_so_na_celula()
    {
        _arquivos.Lancar(Arq("quebra"), new InvalidOperationException("Falhou de um jeito novo."));

        var celula = (await Ler(Linha(1, "A", (Ambiente.Dsv, Maquina.Maquina1, Arq("quebra"), null)))).Sistemas.Single().Celulas[0];

        celula.Mensagem.Should().Be("Falha ao ler: Falhou de um jeito novo.");
    }

    /// <summary>
    /// Leitor de arquivo falso: responde pelo endereço (sem diferenciar maiúsculas), pode travar um endereço para sempre
    /// (ignorando o token, como a abertura SMB), represar as leituras num portão ou esperar o cancelamento.
    /// </summary>
    private sealed class LeitorFalso : ILeitorDeArquivoDeVersao
    {
        private readonly ConcurrentDictionary<string, Result<string, string>> _respostas = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Exception> _excecoes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _travados = new(StringComparer.OrdinalIgnoreCase);
        private readonly TaskCompletionSource _chegouNoTravado = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string> _lidos = new();
        private readonly ConcurrentQueue<CancellationToken> _tokens = new();
        private TaskCompletionSource? _portao;
        private int _atuais;
        private int _maximo;
        private int _represados;

        public string[] Lidos => _lidos.ToArray();

        public CancellationToken[] Tokens => _tokens.ToArray();

        public Task ChegouNoTravado => _chegouNoTravado.Task;

        public int Maximo => Volatile.Read(ref _maximo);

        public bool EsperarCancelamento { get; set; }

        public void Responder(string endereco, string conteudo) => _respostas[endereco] = Result.Success<string, string>(conteudo);

        public void Responder(string endereco, Result<string, string> resultado) => _respostas[endereco] = resultado;

        public void Lancar(string endereco, Exception excecao) => _excecoes[endereco] = excecao;

        public void Travar(string endereco) => _travados.Add(endereco);

        public TaskCompletionSource Represar() => _portao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completa quando ao menos <paramref name="quantidade"/> leituras estão esperando.</summary>
        public async Task Represados(int quantidade)
        {
            while (Volatile.Read(ref _represados) < quantidade)
            {
                await Task.Yield();
            }
        }

        public async Task<Result<string, string>> LerAsync(string endereco, CancellationToken cancellationToken)
        {
            _lidos.Enqueue(endereco);
            _tokens.Enqueue(cancellationToken);

            if (_travados.Contains(endereco))
            {
                _chegouNoTravado.TrySetResult();
                await new TaskCompletionSource().Task;
            }

            var atuais = Interlocked.Increment(ref _atuais);
            InterlockedMaximo(atuais);
            try
            {
                if (EsperarCancelamento)
                {
                    Interlocked.Increment(ref _represados);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                if (_portao is { } portao)
                {
                    Interlocked.Increment(ref _represados);
                    await portao.Task;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _atuais);
            }

            if (_excecoes.TryGetValue(endereco, out var excecao))
            {
                throw excecao;
            }

            return _respostas.TryGetValue(endereco, out var resposta) ? resposta : Result.Success<string, string>("1.0.0");
        }

        private void InterlockedMaximo(int valor)
        {
            int atual;
            do
            {
                atual = Volatile.Read(ref _maximo);
                if (valor <= atual)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _maximo, valor, atual) != atual);
        }
    }
}
