namespace Tcmrj.Siat.Domain.Enums;

/// <summary>Módulos do SIAT. O nome é também o recurso do UserManager, o mesmo em todos os ambientes.</summary>
public enum Modulo
{
    MailQueue,
    AuditLog,
    Catalogo_Sistemas,
    Versoes_Sistemas,

    /// <summary>
    /// Consumo próprio do Copilot (Gestão › Copilot › Meu consumo; PRD RG-02, specs/005-copilot-meu-consumo). Libera
    /// só os dados da conta do GitHub que a própria pessoa conectou; o painel e os limites (CP-01, CP-02) terão
    /// recursos próprios.
    /// </summary>
    CopilotProprio,
}
