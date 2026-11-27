using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>
/// Dados do Copilot da conta conectada, com cache em memória por tipo de dado (specs/005-copilot-meu-consumo,
/// research.md R4, R5, R7 e R8). Só recebe a conexão: não existe caminho que leia dados de outra conta.
/// </summary>
public interface IConsumoCopilotService
{
    /// <summary>
    /// A pessoa da carga (associação sempre relida, perfil, licença, limite e times), compartilhada por 10 s entre os
    /// pedidos da mesma carga. <see cref="Erros.Copilot.NaoMembro"/> e <see cref="Erros.Copilot.SemLicencaNemLimite"/>
    /// são respostas, não falhas.
    /// </summary>
    /// <param name="atualizar">"Atualizar agora": relê a pessoa da carga, o perfil, as licenças, os budgets e os times.</param>
    Task<Result<PessoaCopilot, Erro>> ObterPessoaAsync(ConexaoGitHub conexao, bool atualizar, CancellationToken cancellationToken);

    /// <summary>
    /// Consumo do período, um relatório por dia, depois de confirmada a pessoa da carga. A primeira falha interrompe as
    /// demais leituras e vira o erro do período: nunca há total parcial (FR-020).
    /// </summary>
    /// <param name="atualizar">
    /// Relê a pessoa da carga (compartilhada com os outros pedidos do mesmo "Atualizar agora") e os dias recentes; dias
    /// fechados não são relidos (FR-030).
    /// </param>
    /// <param name="progresso">Recebe a quantidade de dias concluídos (1 a N).</param>
    Task<Result<ConsumoDoPeriodo, Erro>> ObterPeriodoAsync(
        ConexaoGitHub conexao, Periodo periodo, bool atualizar, IProgress<int>? progresso, CancellationToken cancellationToken);

    /// <summary>Acumulado do mês atual (relatório mensal), depois de confirmada a pessoa da carga.</summary>
    Task<Result<RelatorioDeConsumo, Erro>> ObterMesAtualAsync(ConexaoGitHub conexao, bool atualizar, CancellationToken cancellationToken);
}
