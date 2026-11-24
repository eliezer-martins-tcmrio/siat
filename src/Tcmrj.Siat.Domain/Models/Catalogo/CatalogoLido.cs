namespace Tcmrj.Siat.Domain.Models.Catalogo;

/// <summary>Resultado de uma leitura bem-sucedida do <c>sistemasDb.js</c> (data-model.md).</summary>
/// <param name="Sistemas">Ordenados por nome na cultura pt-BR.</param>
/// <param name="Ignorados">Itens sem <c>Title</c> válido ou com estrutura inválida (FR-024).</param>
/// <param name="LidoEm">Momento da leitura (UTC), do <see cref="TimeProvider"/>.</param>
/// <param name="Origem">URL do arquivo na interface do GitLab, para o rodapé (FR-022).</param>
public sealed record CatalogoLido(IReadOnlyList<Sistema> Sistemas, int Ignorados, DateTimeOffset LidoEm, string Origem);
