using System.Collections.Concurrent;
using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Lê os arquivos de versão de todos os endereços de um servidor e monta os sistemas
/// (specs/003-versoes-publicadas/research.md R4):
/// <list type="number">
/// <item>Classifica cada endereço (<see cref="ClassificadorDeEndereco"/>): forma não suportada vira erro da célula sem
/// leitura (R3).</item>
/// <item>Lê primeiro os <c>.INI</c> de sistema raiz e depois os demais, cada endereço distinto uma vez por leitura geral,
/// sem diferenciar maiúsculas, como o Windows (FR-009).</item>
/// <item>Até <see cref="CacheDeVersoesOptions.LeiturasSimultaneas"/> leituras em paralelo, cada uma com
/// <see cref="CacheDeVersoesOptions.TempoLimiteSegundos"/>: a espera é solta com <c>WaitAsync</c>, porque a abertura de
/// um arquivo de rede não responde ao token (plan.md, Acompanhamento de complexidade).</item>
/// <item>A falha de um arquivo fica só na célula (Princípio IV).</item>
/// </list>
/// Chama o andamento uma vez, antes da primeira leitura, com a quantidade de endereços distintos (R10).
/// </summary>
public sealed partial class LeitorDeVersoes(
    ILeitorDeArquivoDeVersao leitor,
    IOptionsMonitor<CacheDeVersoesOptions> opcoes,
    TimeProvider relogio,
    ILogger<LeitorDeVersoes> logger)
{
    private static readonly StringComparer Ordem = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: false);

    /// <param name="servidor">De onde vieram os endereços.</param>
    /// <param name="enderecos">Linhas ativas da tabela.</param>
    /// <param name="inicio">Início da leitura geral (<see cref="TimeProvider.GetTimestamp"/>), antes da consulta à tabela.</param>
    /// <param name="andamento">Recebe a quantidade de endereços distintos a ler.</param>
    /// <param name="cancellationToken">Cancela todas as leituras (e solta as esperas das aberturas em curso).</param>
    /// <exception cref="OperationCanceledException">A leitura geral foi cancelada.</exception>
    public async Task<VersoesLidas> LerAsync(
        Servidor servidor,
        IReadOnlyList<EnderecoDeVersao> enderecos,
        long inicio,
        Action<int>? andamento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        ArgumentNullException.ThrowIfNull(enderecos);

        var configuracao = opcoes.CurrentValue;
        var limite = TimeSpan.FromSeconds(configuracao.TempoLimiteSegundos);
        var paralelo = new ParallelOptions
        {
            MaxDegreeOfParallelism = configuracao.LeiturasSimultaneas,
            CancellationToken = cancellationToken,
        };

        var celulas = enderecos
            .SelectMany(e => EnderecoDeVersao.Posicoes.Select(p => new Pendente(e, p.Ambiente, p.Maquina, e.Enderecos[p])))
            .ToArray();

        var raizes = Distintos(celulas
            .Where(c => c.Par.Versao is not null && c.Par.SistemaRaiz is not null && Aceito(c.Par.SistemaRaiz))
            .Select(c => c.Par.SistemaRaiz!));
        var modelos = Distintos(celulas
            .Where(c => c.Par.Versao is not null && Aceito(c.Par.Versao))
            .Select(c => c.Par.Versao!));
        andamento?.Invoke(raizes.Count + modelos.Count);

        var lidos = new ConcurrentDictionary<string, Result<string, string>>(StringComparer.OrdinalIgnoreCase);
        await LerTodosAsync(raizes, lidos, limite, paralelo);

        // Com os raízes lidos, o endereço de cada célula fica conhecido ({0} trocado pela versão do raiz).
        foreach (var celula in celulas)
        {
            celula.Montar(lidos);
        }

        var finais = Distintos(celulas.Where(c => c.Final is not null).Select(c => c.Final!));
        await LerTodosAsync(finais, lidos, limite, paralelo);

        var sistemas = enderecos
            .Select(e => new SistemaVersionado(e.Id, e.Sistema, celulas.Where(c => ReferenceEquals(c.Origem, e)).Select(c => c.Concluir(lidos)).ToArray()))
            .OrderBy(s => s.Nome, Ordem)
            .ThenBy(s => s.Id)
            .ToArray();

        var duracao = relogio.GetElapsedTime(inicio);
        var erros = sistemas.Sum(s => s.Celulas.Count(c => c.Situacao == SituacaoDaCelula.Erro));
        LeituraConcluida(logger, servidor.Nome, lidos.Count, erros, duracao.TotalSeconds);

        return new VersoesLidas(servidor, relogio.GetUtcNow(), duracao, sistemas, lidos.Count);
    }

    private static bool Aceito(string endereco) => ClassificadorDeEndereco.Classificar(endereco) != TipoDeEndereco.NaoSuportado;

    private static List<string> Distintos(IEnumerable<string> enderecos) =>
        enderecos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private async Task LerTodosAsync(
        IReadOnlyList<string> enderecos,
        ConcurrentDictionary<string, Result<string, string>> lidos,
        TimeSpan limite,
        ParallelOptions paralelo)
    {
        var faltam = enderecos.Where(e => !lidos.ContainsKey(e)).ToArray();
        if (faltam.Length == 0)
        {
            return;
        }

        await Parallel.ForEachAsync(faltam, paralelo, async (endereco, token) =>
            lidos[endereco] = await LerComLimiteAsync(endereco, limite, token));
    }

    private async Task<Result<string, string>> LerComLimiteAsync(string endereco, TimeSpan limite, CancellationToken geral)
    {
        if (!Aceito(endereco))
        {
            return Result.Failure<string, string>(ClassificadorDeEndereco.MensagemNaoSuportado);
        }

        using var tempo = new CancellationTokenSource(limite, relogio);
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(geral, tempo.Token);
        try
        {
            // Task.Run: a abertura síncrona do arquivo de rede não prende a vaga; WaitAsync solta a espera no limite.
            return await Task.Run(() => leitor.LerAsync(endereco, ligado.Token), CancellationToken.None).WaitAsync(ligado.Token);
        }
        catch (OperationCanceledException) when (!geral.IsCancellationRequested)
        {
            return Result.Failure<string, string>($"Tempo limite de {limite.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s esgotado ao ler o arquivo.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            FalhaInesperada(logger, endereco, ex);
            return Result.Failure<string, string>($"Falha ao ler: {SemPontoFinal(ex.Message)}.");
        }
    }

    private static string SemPontoFinal(string mensagem) => mensagem.TrimEnd().TrimEnd('.');

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Versões lidas de {Servidor}: {Enderecos} endereços distintos, {Erros} células com erro, em {Segundos:0.0} s.")]
    private static partial void LeituraConcluida(ILogger logger, string servidor, int enderecos, int erros, double segundos);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha inesperada ao ler o arquivo de versão {Endereco}.")]
    private static partial void FalhaInesperada(ILogger logger, string endereco, Exception ex);

    /// <summary>Uma célula durante a leitura: pendente, lendo o raiz, lendo o endereço e concluída (data-model.md).</summary>
    private sealed class Pendente(EnderecoDeVersao origem, Ambiente ambiente, Maquina maquina, ParDeEndereco par)
    {
        private string? erroDoRaiz;

        public EnderecoDeVersao Origem { get; } = origem;

        public ParDeEndereco Par { get; } = par;

        /// <summary>Endereço a ler, já montado; nulo quando não há o que ler.</summary>
        public string? Final { get; private set; }

        public void Montar(ConcurrentDictionary<string, Result<string, string>> lidos)
        {
            if (Par.Versao is not { } modelo || !Aceito(modelo))
            {
                return;
            }

            if (Par.SistemaRaiz is not { } raiz)
            {
                Final = modelo;
                return;
            }

            var lidoDoRaiz = Aceito(raiz) ? lidos[raiz] : Result.Failure<string, string>(ClassificadorDeEndereco.MensagemNaoSuportado);
            if (lidoDoRaiz.IsFailure)
            {
                erroDoRaiz = $"Versão do sistema raiz não obtida: {SemPontoFinal(lidoDoRaiz.Error)}.";
                return;
            }

            Final = InterpretadorDeVersao.MontarEndereco(modelo, InterpretadorDeVersao.Interpretar(lidoDoRaiz.Value));
        }

        public LeituraDeCelula Concluir(ConcurrentDictionary<string, Result<string, string>> lidos)
        {
            if (Par.Versao is not { } modelo)
            {
                return LeituraDeCelula.NaoConfigurada(ambiente, maquina);
            }

            if (!Aceito(modelo))
            {
                return LeituraDeCelula.ComErro(ambiente, maquina, ClassificadorDeEndereco.MensagemNaoSuportado, modelo, Par.SistemaRaiz);
            }

            if (erroDoRaiz is not null || Final is null)
            {
                return LeituraDeCelula.ComErro(ambiente, maquina, erroDoRaiz ?? ClassificadorDeEndereco.MensagemNaoSuportado, modelo, Par.SistemaRaiz);
            }

            var lido = lidos[Final];
            if (lido.IsFailure)
            {
                return LeituraDeCelula.ComErro(ambiente, maquina, lido.Error, Final, Par.SistemaRaiz);
            }

            var versao = InterpretadorDeVersao.Interpretar(lido.Value);
            return versao.Length == 0
                ? LeituraDeCelula.NaoConfigurada(ambiente, maquina, Final, Par.SistemaRaiz)
                : LeituraDeCelula.ComVersao(ambiente, maquina, versao, Final, Par.SistemaRaiz);
        }
    }
}
