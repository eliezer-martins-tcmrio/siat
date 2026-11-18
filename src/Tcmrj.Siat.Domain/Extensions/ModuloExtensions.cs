using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Extensions;

public static class ModuloExtensions
{
    private const string CanSelect = "CanSelect";

    /// <summary>Nome do recurso do UserManager exigido para consultar o módulo (o mesmo em DSV, HML e PRD).</summary>
    public static string Recurso(this Modulo modulo) => modulo.ToString();

    /// <summary>
    /// Política exigida para consultar o módulo em qualquer ambiente (FR-008, R4): <c>"&lt;Modulo&gt;.CanSelect"</c>.
    /// Usada no menu, na abertura da página, na lista de servidores e em cada consulta.
    /// </summary>
    public static string Politica(this Modulo modulo) => $"{modulo.Recurso()}.{CanSelect}";
}
