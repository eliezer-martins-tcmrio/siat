using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Extensions;

public static class AmbienteDoCatalogoExtensions
{
    /// <summary>Nome do campo no <c>sistemasDb.js</c>: <c>DSV</c>, <c>HML</c>, <c>PRD</c> ou <c>CONT</c>.</summary>
    public static string ChaveNoArquivo(this AmbienteDoCatalogo ambiente) => ambiente.Sigla();

    /// <summary>Sigla da etiqueta: <c>DSV</c>, <c>HML</c>, <c>PRD</c> ou <c>CONT</c>.</summary>
    public static string Sigla(this AmbienteDoCatalogo ambiente) => ambiente switch
    {
        AmbienteDoCatalogo.Dsv => "DSV",
        AmbienteDoCatalogo.Hml => "HML",
        AmbienteDoCatalogo.Prd => "PRD",
        AmbienteDoCatalogo.Cont => "CONT",
        _ => throw new ArgumentOutOfRangeException(nameof(ambiente), ambiente, null),
    };

    /// <summary>Nome por extenso: Desenvolvimento, Homologação, Produção ou Contingência.</summary>
    public static string Nome(this AmbienteDoCatalogo ambiente) => ambiente switch
    {
        AmbienteDoCatalogo.Dsv => "Desenvolvimento",
        AmbienteDoCatalogo.Hml => "Homologação",
        AmbienteDoCatalogo.Prd => "Produção",
        AmbienteDoCatalogo.Cont => "Contingência",
        _ => throw new ArgumentOutOfRangeException(nameof(ambiente), ambiente, null),
    };

    /// <summary>Valor do atributo <c>data-amb</c> (cor do ambiente): <c>dsv</c>, <c>hml</c>, <c>prd</c> ou <c>cont</c>.</summary>
    public static string DataAmb(this AmbienteDoCatalogo ambiente) => ambiente switch
    {
        AmbienteDoCatalogo.Dsv => "dsv",
        AmbienteDoCatalogo.Hml => "hml",
        AmbienteDoCatalogo.Prd => "prd",
        AmbienteDoCatalogo.Cont => "cont",
        _ => throw new ArgumentOutOfRangeException(nameof(ambiente), ambiente, null),
    };
}
