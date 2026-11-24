using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Leitura do catálogo de sistemas na origem (<c>sistemasDb.js</c> no GitLab, só leitura).</summary>
public interface ICatalogoSistemasRepository
{
    /// <returns>
    /// O catálogo lido, ou um <see cref="Erros.Catalogo"/>; cancelamento pelo <paramref name="cancellationToken"/> volta
    /// como <see cref="Erros.Catalogo.Cancelada"/>, sem exceção.
    /// </returns>
    Task<Result<CatalogoLido, Erro>> LerAsync(CancellationToken cancellationToken);
}
