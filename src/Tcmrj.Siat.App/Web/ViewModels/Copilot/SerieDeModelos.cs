using System.Globalization;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.ViewModels.Copilot;

/// <summary>Uma série da visão por modelo: rótulo, classe de cor e um valor por dia do período.</summary>
public sealed record Serie(string Rotulo, string Classe, IReadOnlyList<decimal> Valores);

/// <summary>
/// Séries da visão por modelo da evolução diária (specs/005-copilot-meu-consumo/research.md R5, <c>model-series.ts</c>
/// do gestao-copilot): os <see cref="MaximoDeSeries"/> modelos de maior total na métrica e, quando sobram, "Outros"
/// com a soma dos demais. Empate pelo nome em pt-BR.
/// </summary>
public static class SerieDeModelos
{
    public const int MaximoDeSeries = 5;

    public const string RotuloOutros = "Outros";

    public const string ClasseOutros = "serie-outros";

    private static readonly CompareInfo ComparacaoPtBr = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    public static IReadOnlyList<Serie> Montar(IReadOnlyList<DiaVM> dias, MetricaDeConsumo metrica)
    {
        ArgumentNullException.ThrowIfNull(dias);

        // Total por modelo na métrica, com a grafia da primeira ocorrência.
        var totais = new Dictionary<string, (string Rotulo, decimal Total)>(StringComparer.Ordinal);
        foreach (var modelo in dias.SelectMany(d => d.Modelos))
        {
            var valor = metrica.De(modelo.Consumo);
            totais[modelo.Chave] = totais.TryGetValue(modelo.Chave, out var anterior)
                ? (anterior.Rotulo, anterior.Total + valor)
                : (modelo.Rotulo, valor);
        }

        var ordenadas = totais
            .OrderByDescending(t => t.Value.Total)
            .ThenBy(t => t.Value.Rotulo, Comparer<string>.Create((a, b) => ComparacaoPtBr.Compare(a, b, CompareOptions.None)))
            .ToList();
        var principais = ordenadas.Take(MaximoDeSeries).ToList();
        var outros = ordenadas.Skip(MaximoDeSeries).Select(t => t.Key).ToHashSet(StringComparer.Ordinal);

        var series = principais
            .Select((t, i) => new Serie(
                t.Value.Rotulo,
                $"serie-{(i + 1).ToString(CultureInfo.InvariantCulture)}",
                [.. dias.Select(d => d.Modelos.Where(m => m.Chave == t.Key).Sum(m => metrica.De(m.Consumo)))]))
            .ToList();

        if (outros.Count > 0)
        {
            series.Add(new Serie(
                RotuloOutros,
                ClasseOutros,
                [.. dias.Select(d => d.Modelos.Where(m => outros.Contains(m.Chave)).Sum(m => metrica.De(m.Consumo)))]));
        }

        return series;
    }
}
