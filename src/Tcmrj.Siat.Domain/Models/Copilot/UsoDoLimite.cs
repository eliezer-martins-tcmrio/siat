namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>Faixa de uso do limite no mês atual (FR-016), sempre mostrada em texto, não só em cor.</summary>
public enum FaixaDeUso
{
    /// <summary>Menos de 80%.</summary>
    Normal,

    /// <summary>80% ou mais.</summary>
    Atencao,

    /// <summary>100% ou mais.</summary>
    Atingido,

    /// <summary>O limite vigente é zero.</summary>
    LimiteZero,

    /// <summary>Sem consumo do mês, sem limite, ou limite ambíguo ou ausente.</summary>
    Indisponivel,
}

/// <summary>
/// Uso do limite no acumulado do mês atual (regra do <c>usageState</c> do gestao-copilot; research.md R5).
/// </summary>
/// <param name="Razao">Consumo bruto do mês ÷ limite; nulo em <see cref="FaixaDeUso.LimiteZero"/> e
/// <see cref="FaixaDeUso.Indisponivel"/>.</param>
/// <param name="Restante">Limite − consumo bruto do mês (negativo quando passou); nulo quando indisponível.</param>
/// <param name="Mes">Mês do acumulado (<c>yyyy-MM</c>).</param>
public sealed record UsoDoLimite(FaixaDeUso Faixa, decimal? Razao, decimal? Restante, string Mes)
{
    public const decimal LimiarDeAtencao = 0.8m;

    public static UsoDoLimite Calcular(decimal? consumoBrutoDoMes, LimiteVigente? limite, string mes)
    {
        if (consumoBrutoDoMes is not { } consumo || limite?.Valor is not { } valor)
        {
            return new UsoDoLimite(FaixaDeUso.Indisponivel, null, null, mes);
        }

        if (valor == 0)
        {
            return new UsoDoLimite(FaixaDeUso.LimiteZero, null, valor - consumo, mes);
        }

        var razao = consumo / valor;
        var faixa = razao >= 1 ? FaixaDeUso.Atingido : razao >= LimiarDeAtencao ? FaixaDeUso.Atencao : FaixaDeUso.Normal;
        return new UsoDoLimite(faixa, razao, valor - consumo, mes);
    }
}
