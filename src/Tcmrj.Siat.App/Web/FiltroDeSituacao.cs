namespace Tcmrj.Siat.App.Web;

/// <summary>
/// Filtro por situação da tela de Versões (data-model.md). Fica no App por ser da tela; na URL é ausente, <c>div</c> ou
/// <c>erro</c> (<see cref="Navegacao.Versoes"/>).
/// </summary>
public enum FiltroDeSituacao
{
    Todos,
    ComDivergencia,
    ComErro,
}
