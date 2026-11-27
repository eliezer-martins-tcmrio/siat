using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>
/// Leituras na API REST do GitHub com o token de leitura de billing (specs/005-copilot-meu-consumo,
/// contracts/github-api.md). Só <c>GET</c> (Princípio II). Os erros seguem <see cref="Erros.Copilot"/>; o cancelamento
/// pedido pelo chamador volta como <see cref="Erros.Copilot.Cancelada"/>, sem exceção.
/// </summary>
public interface IGitHubCopilotRepository
{
    /// <summary>Organização configurada (mensagens e links dos times).</summary>
    string Organizacao { get; }

    /// <summary><c>GET /orgs/{org}/memberships/{login}</c>; 404 é <see cref="Erros.Copilot.NaoMembro"/>.</summary>
    Task<Result<Associacao, Erro>> ObterAssociacaoAsync(string login, CancellationToken cancellationToken);

    /// <summary><c>GET /user/{id}</c>: login atual, nome e foto.</summary>
    Task<Result<PerfilGitHub, Erro>> ObterPerfilAsync(long id, CancellationToken cancellationToken);

    /// <summary>Licenças Copilot da organização, todas as páginas.</summary>
    Task<Result<IReadOnlyList<Licenca>, Erro>> ListarLicencasAsync(CancellationToken cancellationToken);

    /// <summary>Budgets de AI Credits da organização, já normalizados (<see cref="Budget.Normalizar"/>).</summary>
    Task<Result<IReadOnlyList<Budget>, Erro>> ListarBudgetsAsync(CancellationToken cancellationToken);

    /// <summary>Times da organização, todas as páginas.</summary>
    Task<Result<IReadOnlyList<TimeDaOrganizacao>, Erro>> ListarTimesAsync(CancellationToken cancellationToken);

    /// <summary>Membros de um time, todas as páginas.</summary>
    Task<Result<IReadOnlyList<MembroDoTime>, Erro>> ListarMembrosDoTimeAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Itens do relatório de consumo de AI Credits da conta: de um dia, com <paramref name="dia"/>, ou o acumulado do
    /// mês, sem ele.
    /// </summary>
    Task<Result<IReadOnlyList<ItemDeConsumo>, Erro>> ObterRelatorioAsync(
        string login, int ano, int mes, int? dia, CancellationToken cancellationToken);
}
