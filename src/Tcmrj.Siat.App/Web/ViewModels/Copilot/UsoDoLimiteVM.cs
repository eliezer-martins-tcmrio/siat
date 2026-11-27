using System.Globalization;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.ViewModels.Copilot;

/// <summary>
/// Uso do limite no mês atual para a barra (specs/005-copilot-meu-consumo, FR-016 e FR-034): a faixa sempre em texto,
/// nunca só na cor, e a nota com o restante e o mês.
/// </summary>
public sealed record UsoDoLimiteVM(FaixaDeUso Faixa, decimal? Razao, decimal? Restante, decimal ConsumoDoMes, string Mes)
{
    /// <summary>Texto da faixa: "Atenção", "Limite atingido no relatório", "Limite zero", "Indisponível" ou vazio (normal).</summary>
    public string RotuloDaFaixa => Faixa switch
    {
        FaixaDeUso.Atencao => "Atenção",
        FaixaDeUso.Atingido => "Limite atingido no relatório",
        FaixaDeUso.LimiteZero => "Limite zero",
        FaixaDeUso.Indisponivel => "Indisponível",
        _ => "Dentro do limite",
    };

    /// <summary>"80%", ou nulo sem razão (limite zero ou indisponível).</summary>
    public string? Percentual => Razao is { } razao ? FormatoCopilot.Percentual(razao) : null;

    /// <summary>Largura da barra preenchida (0 a 100).</summary>
    public decimal Preenchimento => Faixa switch
    {
        FaixaDeUso.LimiteZero => 100,
        _ when Razao is { } razao => Math.Clamp(Math.Floor(razao * 100), 0, 100),
        _ => 0,
    };

    /// <summary>"US$ 10.00 abaixo do limite no relatório · acumulado de novembro de 2026".</summary>
    public string? Nota
    {
        get
        {
            if (Restante is not { } restante)
            {
                return null;
            }

            var lado = restante >= 0 ? "abaixo do" : "acima do";
            return $"{FormatoCopilot.Usd(Math.Abs(restante))} {lado} limite no relatório · acumulado de {MesPorExtenso}";
        }
    }

    /// <summary>"novembro de 2026", a partir de <see cref="Mes"/> (<c>yyyy-MM</c>).</summary>
    public string MesPorExtenso =>
        DateOnly.TryParseExact(Mes + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)
            ? FormatoCopilot.Mes(inicio.Year, inicio.Month)
            : Mes;

    public static UsoDoLimiteVM De(UsoDoLimite uso, decimal consumoDoMes)
    {
        ArgumentNullException.ThrowIfNull(uso);
        return new UsoDoLimiteVM(uso.Faixa, uso.Razao, uso.Restante, consumoDoMes, uso.Mes);
    }
}
