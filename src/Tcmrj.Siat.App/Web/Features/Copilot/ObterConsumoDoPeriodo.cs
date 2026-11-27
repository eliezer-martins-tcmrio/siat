using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.Features.Copilot;

/// <summary>
/// Consumo da conta conectada no período da URL (specs/005-copilot-meu-consumo, FR-014, FR-017, FR-018, FR-023 a
/// FR-027): autoriza, lê a conexão, valida o período (UTC) e só então lê os relatórios diários. Sem login na consulta.
/// </summary>
public static class ObterConsumoDoPeriodo
{
    /// <param name="De">Data inicial da URL (<c>yyyy-MM-dd</c>); nula, o dia 1 do mês atual.</param>
    /// <param name="Ate">Data final da URL; nula, hoje.</param>
    /// <param name="Atualizar">"Atualizar agora": relê a pessoa da carga e os dias recentes; dias fechados não.</param>
    public sealed record Query(string? De, string? Ate, bool Atualizar) : IRequest<Result<ConsumoDoPeriodoVM, Erro>>
    {
        /// <summary>Recebe a quantidade de dias lidos (1 a N), para o andamento.</summary>
        public IProgress<int>? Progresso { get; init; }
    }

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IConexaoGitHubService conexoes,
        IConsumoCopilotService consumo,
        TimeProvider relogio) : IRequestHandler<Query, Result<ConsumoDoPeriodoVM, Erro>>
    {
        public async Task<Result<ConsumoDoPeriodoVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!await autorizacao.PodeAcessarModuloAsync(Modulo.CopilotProprio))
            {
                return Erros.Copilot.AcessoNegado;
            }

            if (await conexoes.ObterAsync() is not { } conexao)
            {
                return Erros.Copilot.SemConexao;
            }

            var hoje = DateOnly.FromDateTime(relogio.GetUtcNow().UtcDateTime);
            var periodo = Periodo.Validar(request.De, request.Ate, hoje);
            if (periodo.IsFailure)
            {
                return periodo.Error;
            }

            try
            {
                var lido = await consumo.ObterPeriodoAsync(conexao, periodo.Value, request.Atualizar, request.Progresso, cancellationToken);
                return lido.IsSuccess ? ConsumoDoPeriodoVM.De(lido.Value) : lido.Error;
            }
            catch (OperationCanceledException)
            {
                return Erros.Copilot.Cancelada;
            }
        }
    }
}
