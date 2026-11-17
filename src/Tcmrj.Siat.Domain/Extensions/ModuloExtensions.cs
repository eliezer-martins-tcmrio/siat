using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Extensions;

public static class ModuloExtensions
{
    private const string CanSelect = "CanSelect";

    /// <summary>Nome do recurso do UserManager exigido para consultar o módulo no ambiente.</summary>
    public static string RecursoPara(this Modulo modulo, Ambiente ambiente) =>
        ambiente == Ambiente.Prd ? $"{modulo}PRD" : modulo.ToString();

    /// <summary>
    /// Política exigida para consultar o módulo no ambiente (FR-008, R4):
    /// DSV/HML → <c>"&lt;Modulo&gt;.CanSelect"</c>; PRD → <c>"&lt;Modulo&gt;PRD.CanSelect"</c>.
    /// </summary>
    public static string PoliticaPara(this Modulo modulo, Ambiente ambiente) =>
        $"{modulo.RecursoPara(ambiente)}.{CanSelect}";

    /// <summary>As duas políticas do módulo (DSV/HML e PRD), usadas no menu e na abertura da página.</summary>
    public static IReadOnlyList<string> Politicas(this Modulo modulo) =>
        [modulo.PoliticaPara(Ambiente.Dsv), modulo.PoliticaPara(Ambiente.Prd)];
}
