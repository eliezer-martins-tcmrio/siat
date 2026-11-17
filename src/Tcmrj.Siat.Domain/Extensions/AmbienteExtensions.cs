using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Extensions;

public static class AmbienteExtensions
{
    /// <summary>Descrição para tela: <c>DSV</c>, <c>HML</c> ou <c>PRD</c>.</summary>
    public static string Descricao(this Ambiente ambiente) => ambiente switch
    {
        Ambiente.Dsv => "DSV",
        Ambiente.Hml => "HML",
        Ambiente.Prd => "PRD",
        _ => throw new ArgumentOutOfRangeException(nameof(ambiente), ambiente, null),
    };

    /// <summary>Valor do atributo <c>data-amb</c>: <c>dsv</c>, <c>hml</c> ou <c>prd</c>.</summary>
    public static string DataAmb(this Ambiente ambiente) => ambiente switch
    {
        Ambiente.Dsv => "dsv",
        Ambiente.Hml => "hml",
        Ambiente.Prd => "prd",
        _ => throw new ArgumentOutOfRangeException(nameof(ambiente), ambiente, null),
    };
}
