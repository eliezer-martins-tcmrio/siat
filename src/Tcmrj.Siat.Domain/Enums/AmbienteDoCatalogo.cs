namespace Tcmrj.Siat.Domain.Enums;

/// <summary>
/// Ambiente de um sistema no catálogo (<c>sistemasDb.js</c>). A ordem é a ordem das colunas da tela (FR-005).
/// Separado de <see cref="Ambiente"/> porque o catálogo tem Contingência e a lista fechada de servidores não.
/// </summary>
public enum AmbienteDoCatalogo
{
    Dsv,
    Hml,
    Prd,
    Cont,
}
