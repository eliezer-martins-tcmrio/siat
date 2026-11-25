namespace Tcmrj.Siat.App.Web;

/// <summary>
/// Filtro por situação da tela de Versões (data-model.md). Fica no App por ser da tela; na URL é ausente, <c>div</c>,
/// <c>erro</c> ou <c>semficha</c> (<see cref="Navegacao.Versoes"/>).
/// </summary>
public enum FiltroDeSituacao
{
    Todos,
    ComDivergencia,
    ComErro,

    /// <summary>Sistemas cujo nome não leva a nenhuma ficha (specs/004-ficha-sistema, FR-006).</summary>
    SemFicha,
}
