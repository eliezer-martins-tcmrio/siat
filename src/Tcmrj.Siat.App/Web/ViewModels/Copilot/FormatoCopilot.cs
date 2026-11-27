using System.Globalization;

namespace Tcmrj.Siat.App.Web.ViewModels.Copilot;

/// <summary>
/// Formatos da tela do Copilot (specs/005-copilot-meu-consumo/research.md R11): USD com <c>en-US</c> e prefixo
/// "US$" (como o <c>money</c> do gestao-copilot), créditos com separador de milhar pt-BR, datas <c>dd/MM/yyyy</c>.
/// </summary>
public static class FormatoCopilot
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"US$ 1,234.56"; negativo, "-US$ 10.00".</summary>
    public static string Usd(decimal valor) =>
        (valor < 0 ? "-" : string.Empty) + "US$ " + Math.Abs(valor).ToString("N2", EnUs);

    /// <summary>"1.235" (sem casas decimais).</summary>
    public static string Creditos(decimal valor) => Math.Round(valor, MidpointRounding.AwayFromZero).ToString("N0", PtBr);

    /// <summary>"27/11/2026".</summary>
    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>"novembro de 2026".</summary>
    public static string Mes(int ano, int mes) => new DateOnly(ano, mes, 1).ToString("MMMM 'de' yyyy", PtBr);

    /// <summary>"80%" (arredondado para baixo, como a barra).</summary>
    public static string Percentual(decimal razao) =>
        Math.Floor(razao * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Valor de uma métrica: USD nas de valor, créditos na de créditos.</summary>
    public static string Valor(MetricaDeConsumo metrica, decimal valor) =>
        metrica == MetricaDeConsumo.Creditos ? Creditos(valor) : Usd(valor);
}

/// <summary>Métrica da evolução diária e das séries por modelo.</summary>
public enum MetricaDeConsumo
{
    ValorBruto,

    ValorLiquido,

    Creditos,
}

/// <summary>Rótulos das métricas (em minúsculas, para as frases de acessibilidade).</summary>
public static class MetricaDeConsumoExtensions
{
    public static string Rotulo(this MetricaDeConsumo metrica) => metrica switch
    {
        MetricaDeConsumo.ValorLiquido => "valor líquido",
        MetricaDeConsumo.Creditos => "créditos",
        _ => "valor bruto",
    };

    public static decimal De(this MetricaDeConsumo metrica, Domain.Models.Copilot.Consumo consumo)
    {
        ArgumentNullException.ThrowIfNull(consumo);
        return metrica switch
        {
            MetricaDeConsumo.ValorLiquido => consumo.ValorLiquido,
            MetricaDeConsumo.Creditos => consumo.Creditos,
            _ => consumo.ValorBruto,
        };
    }
}
