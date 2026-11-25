using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Leitura de uma origem lenta guardada em memória, uma entrada por chave. Extraída do <c>CatalogoSistemasService</c>
/// da 002 (specs/002-catalogo-sistemas/research.md R4) para as Versões Publicadas usarem a mesma lógica por servidor
/// (specs/003-versoes-publicadas/research.md R5), em vez de duas cópias de código concorrente sutil.
/// <list type="bullet">
/// <item>Validade: o último valor de uma chave vale enquanto <c>agora - horário do valor &lt; validade</c>; "atualizar"
/// lê de novo mesmo dentro dela.</item>
/// <item>Uma leitura por vez por chave: pedidos simultâneos da mesma chave esperam a mesma tarefa. Chaves diferentes
/// não compartilham leitura, validade nem falha.</item>
/// <item>Falha com valor anterior: devolve o anterior com a falha (erro e horário) e o pedido seguinte tenta de novo;
/// sucesso limpa a falha. Sem valor anterior, devolve o erro.</item>
/// <item>Cancelamento (Princípio VII): cada pedido entra na contagem de quem espera; cancelado, sai dela e recebe o erro
/// de cancelamento. Quando a contagem chega a zero, o token da leitura é cancelado, o que interrompe a origem. Leitura
/// cancelada não muda o estado, e o pedido seguinte começa uma leitura nova.</item>
/// </list>
/// Nada vai para disco (Princípio I). Seguro para uso concorrente: todo o estado fica sob uma única trava.
/// </summary>
/// <typeparam name="TChave">Chave da entrada (por exemplo, a chave do servidor).</typeparam>
/// <typeparam name="TValor">Valor lido.</typeparam>
public sealed class LeituraEmCache<TChave, TValor>
    where TChave : notnull
    where TValor : class
{
    private readonly Func<TChave, CancellationToken, Task<Result<TValor, Erro>>> ler;
    private readonly Func<TValor, DateTimeOffset> horarioDe;
    private readonly Func<TimeSpan> validade;
    private readonly Erro cancelada;
    private readonly Func<Exception, Erro> erroInesperado;
    private readonly TimeProvider relogio;
    private readonly ILogger logger;

    private readonly Lock trava = new();
    private readonly Dictionary<TChave, Entrada> entradas = [];

    /// <param name="ler">Lê a origem da chave; o token é cancelado quando ninguém mais espera.</param>
    /// <param name="horarioDe">Quando o valor foi lido (base da validade).</param>
    /// <param name="validade">Validade atual (lida a cada pedido, para acompanhar a configuração).</param>
    /// <param name="cancelada">Erro devolvido a quem cancela; o código identifica a leitura cancelada.</param>
    /// <param name="erroInesperado">Erro da tela para uma exceção inesperada da leitura (registrada no log).</param>
    public LeituraEmCache(
        Func<TChave, CancellationToken, Task<Result<TValor, Erro>>> ler,
        Func<TValor, DateTimeOffset> horarioDe,
        Func<TimeSpan> validade,
        Erro cancelada,
        Func<Exception, Erro> erroInesperado,
        TimeProvider relogio,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ler);
        ArgumentNullException.ThrowIfNull(horarioDe);
        ArgumentNullException.ThrowIfNull(validade);
        ArgumentNullException.ThrowIfNull(cancelada);
        ArgumentNullException.ThrowIfNull(erroInesperado);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);

        this.ler = ler;
        this.horarioDe = horarioDe;
        this.validade = validade;
        this.cancelada = cancelada;
        this.erroInesperado = erroInesperado;
        this.relogio = relogio;
        this.logger = logger;
    }

    /// <param name="chave">Entrada lida.</param>
    /// <param name="atualizar">Lê de novo mesmo dentro da validade.</param>
    /// <param name="cancellationToken">
    /// Cancelado, o pedido deixa de esperar e recebe o erro de cancelamento; a leitura só é interrompida na origem
    /// quando ninguém mais espera por ela.
    /// </param>
    /// <returns>O valor e, quando a última releitura falhou, a falha; ou o erro, sem valor anterior.</returns>
    public async Task<Result<(TValor Valor, FalhaDeLeitura? Falha), Erro>> ObterAsync(
        TChave chave, bool atualizar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chave);

        if (cancellationToken.IsCancellationRequested)
        {
            return cancelada;
        }

        Leitura leitura;
        lock (trava)
        {
            if (!entradas.TryGetValue(chave, out var entrada))
            {
                entrada = new Entrada();
                entradas[chave] = entrada;
            }

            if (!atualizar && entrada.UltimaValida is { } valida && entrada.UltimaFalha is null && DentroDaValidade(valida))
            {
                return (valida, (FalhaDeLeitura?)null);
            }

            leitura = entrada.EmAndamento ??= IniciarLeitura(chave, entrada);
            leitura.Esperando++;
        }

        try
        {
            var resultado = await leitura.Tarefa.WaitAsync(cancellationToken);
            Sair(leitura);
            return resultado;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Sair(leitura);
            return cancelada;
        }
    }

    /// <summary>
    /// Espia o último valor da chave sem iniciar leitura e sem entrar na contagem de quem espera
    /// (specs/004-ficha-sistema/research.md R5): a ficha usa o resultado da tela de Versões quando ele vale.
    /// </summary>
    /// <returns>
    /// O último valor quando está na validade e a última releitura não falhou; nulo caso contrário. Durante um
    /// "atualizar" em andamento, o valor anterior ainda na validade.
    /// </returns>
    public TValor? Valido(TChave chave)
    {
        ArgumentNullException.ThrowIfNull(chave);

        lock (trava)
        {
            return entradas.TryGetValue(chave, out var entrada)
                && entrada.UltimaValida is { } valida
                && entrada.UltimaFalha is null
                && DentroDaValidade(valida)
                    ? valida
                    : null;
        }
    }

    private bool DentroDaValidade(TValor valor) => relogio.GetUtcNow() - horarioDe(valor) < validade();

    // Sempre sob a trava.
    private Leitura IniciarLeitura(TChave chave, Entrada entrada)
    {
        var leitura = new Leitura(entrada);
        leitura.Tarefa = Task.Run(() => LerAsync(chave, leitura));
        return leitura;
    }

    // Quem esperava deixa de esperar; o último a sair de uma leitura ainda em andamento a cancela.
    private void Sair(Leitura leitura)
    {
        lock (trava)
        {
            leitura.Esperando--;
            if (leitura.Esperando > 0 || leitura.Concluida)
            {
                return;
            }

            if (ReferenceEquals(leitura.Entrada.EmAndamento, leitura))
            {
                // Um pedido que chegar agora começa uma leitura nova, em vez de esperar a cancelada.
                leitura.Entrada.EmAndamento = null;
            }

            leitura.Cancelamento.Cancel();
        }
    }

    private async Task<Result<(TValor Valor, FalhaDeLeitura? Falha), Erro>> LerAsync(TChave chave, Leitura leitura)
    {
        var token = leitura.Cancelamento.Token;
        Result<TValor, Erro> lido;
        try
        {
            lido = await ler(chave, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            lido = cancelada;
        }
#pragma warning disable CA1031 // Falha inesperada da origem vira erro da tela, não derruba o circuito.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LeituraEmCacheLog.FalhaInesperada(logger, typeof(TValor).Name, ex);
            lido = erroInesperado(ex);
        }

        lock (trava)
        {
            var entrada = leitura.Entrada;
            if (ReferenceEquals(entrada.EmAndamento, leitura))
            {
                entrada.EmAndamento = null;
            }

            leitura.Concluida = true;
            leitura.Cancelamento.Dispose();

            if (lido.IsSuccess)
            {
                entrada.UltimaValida = lido.Value;
                entrada.UltimaFalha = null;
                return (lido.Value, (FalhaDeLeitura?)null);
            }

            // Cancelada não é falha da origem: o estado fica como estava antes da leitura.
            if (token.IsCancellationRequested || lido.Error.Code == cancelada.Code)
            {
                return cancelada;
            }

            if (entrada.UltimaValida is { } anterior)
            {
                entrada.UltimaFalha = new FalhaDeLeitura(lido.Error, relogio.GetUtcNow());
                return (anterior, entrada.UltimaFalha);
            }

            return lido.Error;
        }
    }

    /// <summary>Estado de uma chave (sempre sob a trava).</summary>
    private sealed class Entrada
    {
        public TValor? UltimaValida { get; set; }

        public FalhaDeLeitura? UltimaFalha { get; set; }

        public Leitura? EmAndamento { get; set; }
    }

    private sealed class Leitura(Entrada entrada)
    {
        public Entrada Entrada { get; } = entrada;

        public Task<Result<(TValor Valor, FalhaDeLeitura? Falha), Erro>> Tarefa { get; set; } = null!;

        public CancellationTokenSource Cancelamento { get; } = new();

        public int Esperando { get; set; }

        /// <summary>A leitura terminou (sob a trava): não há mais o que cancelar.</summary>
        public bool Concluida { get; set; }
    }
}

/// <summary>Mensagens de log da <see cref="LeituraEmCache{TChave, TValor}"/> (o gerador não aceita classe genérica).</summary>
internal static partial class LeituraEmCacheLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Falha inesperada ao ler {Origem}.")]
    public static partial void FalhaInesperada(ILogger logger, string origem, Exception ex);
}
