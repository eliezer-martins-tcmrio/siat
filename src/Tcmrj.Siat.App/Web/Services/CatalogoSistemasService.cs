using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Singleton com o catálogo em memória (research.md R4; estados em data-model.md). Nada vai para disco (Princípio I).
/// <list type="bullet">
/// <item>Validade: a última leitura válida vale enquanto <c>agora - LidoEm &lt; MinutosDeCache</c>; "Atualizar agora"
/// lê de novo mesmo dentro dela.</item>
/// <item>Uma leitura por vez: pedidos simultâneos esperam a mesma tarefa de leitura.</item>
/// <item>Falha com leitura anterior: devolve a anterior com a falha (erro e horário) e o pedido seguinte tenta de novo;
/// sucesso limpa a falha. Sem leitura anterior, devolve o erro.</item>
/// <item>Cancelamento (Princípio VII): cada pedido entra na contagem de quem espera; cancelado, sai dela e recebe
/// <see cref="Erros.Catalogo.Cancelada"/>. Quando a contagem chega a zero, a leitura é cancelada, o que interrompe a
/// requisição ao GitLab. Leitura cancelada não muda o estado, e o pedido seguinte começa uma leitura nova.</item>
/// </list>
/// O repositório (scoped, com <see cref="HttpClient"/> tipado) é resolvido num escopo próprio de cada leitura.
/// </summary>
public sealed partial class CatalogoSistemasService(
    IServiceScopeFactory escopos,
    IOptionsMonitor<CacheDoCatalogoOptions> opcoes,
    TimeProvider relogio,
    ILogger<CatalogoSistemasService> logger) : ICatalogoSistemasService
{
    private readonly Lock trava = new();
    private CatalogoLido? ultimaValida;
    private FalhaDeLeitura? ultimaFalha;
    private Leitura? leituraEmAndamento;

    public async Task<Result<CatalogoObtido, Erro>> ObterAsync(bool atualizar, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Erros.Catalogo.Cancelada;
        }

        Leitura leitura;
        lock (trava)
        {
            if (!atualizar && ultimaValida is { } valida && ultimaFalha is null && DentroDaValidade(valida))
            {
                return new CatalogoObtido(valida, null);
            }

            leitura = leituraEmAndamento ??= IniciarLeitura();
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
            return Erros.Catalogo.Cancelada;
        }
    }

    private bool DentroDaValidade(CatalogoLido catalogo) =>
        relogio.GetUtcNow() - catalogo.LidoEm < TimeSpan.FromMinutes(opcoes.CurrentValue.MinutosDeCache);

    // Sempre sob a trava.
    private Leitura IniciarLeitura()
    {
        var leitura = new Leitura();
        leitura.Tarefa = Task.Run(() => LerAsync(leitura));
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

            if (ReferenceEquals(leituraEmAndamento, leitura))
            {
                // Um pedido que chegar agora começa uma leitura nova, em vez de esperar a cancelada.
                leituraEmAndamento = null;
            }

            leitura.Cancelamento.Cancel();
        }
    }

    private async Task<Result<CatalogoObtido, Erro>> LerAsync(Leitura leitura)
    {
        var token = leitura.Cancelamento.Token;
        Result<CatalogoLido, Erro> lido;
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var repositorio = escopo.ServiceProvider.GetRequiredService<ICatalogoSistemasRepository>();
            lido = await repositorio.LerAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            lido = Erros.Catalogo.Cancelada;
        }
#pragma warning disable CA1031 // Falha inesperada do repositório vira erro da tela, não derruba o circuito.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            FalhaInesperada(logger, ex);
            lido = Erros.Catalogo.Indisponivel(ex.Message);
        }

        lock (trava)
        {
            if (ReferenceEquals(leituraEmAndamento, leitura))
            {
                leituraEmAndamento = null;
            }

            leitura.Concluida = true;
            leitura.Cancelamento.Dispose();

            if (lido.IsSuccess)
            {
                ultimaValida = lido.Value;
                ultimaFalha = null;
                return new CatalogoObtido(lido.Value, null);
            }

            // Cancelada não é falha da origem: o estado fica como estava antes da leitura.
            if (token.IsCancellationRequested || lido.Error.Code == Erros.Catalogo.CodigoCancelada)
            {
                return Erros.Catalogo.Cancelada;
            }

            if (ultimaValida is { } anterior)
            {
                ultimaFalha = new FalhaDeLeitura(lido.Error, relogio.GetUtcNow());
                return new CatalogoObtido(anterior, ultimaFalha);
            }

            return lido.Error;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha inesperada ao ler o catálogo de sistemas.")]
    private static partial void FalhaInesperada(ILogger logger, Exception ex);

    private sealed class Leitura
    {
        public Task<Result<CatalogoObtido, Erro>> Tarefa { get; set; } = null!;

        public CancellationTokenSource Cancelamento { get; } = new();

        public int Esperando { get; set; }

        /// <summary>A leitura terminou (sob a trava): não há mais o que cancelar.</summary>
        public bool Concluida { get; set; }
    }
}
