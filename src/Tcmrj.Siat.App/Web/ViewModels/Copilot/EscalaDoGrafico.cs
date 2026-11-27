namespace Tcmrj.Siat.App.Web.ViewModels.Copilot;

/// <summary>
/// Escala vertical da evolução diária: topo e marcas em passos "redondos" (1, 2, 2,5 ou 5 vezes uma potência de 10),
/// para o eixo mostrar valores legíveis e a troca de métrica mudar o que se lê, não só a altura das colunas.
/// </summary>
/// <param name="Topo">Valor do topo do gráfico (100% da altura); zero quando não há consumo.</param>
/// <param name="Marcas">Valores das linhas de grade, de zero ao topo, em ordem crescente.</param>
public sealed record EscalaDoGrafico(decimal Topo, IReadOnlyList<decimal> Marcas)
{
    // Divisões procuradas: das duas, fica a de topo mais justo ao máximo (no empate, a de menos marcas).
    private static readonly int[] DivisoesProcuradas = [4, 5];

    public static readonly EscalaDoGrafico Vazia = new(0, [0m]);

    /// <summary>Monta a escala para o maior valor; em créditos (<paramref name="inteiros"/>) o passo mínimo é 1.</summary>
    public static EscalaDoGrafico Para(decimal maximo, bool inteiros)
    {
        if (maximo <= 0)
        {
            return Vazia;
        }

        return DivisoesProcuradas
            .Select(divisoes => Montar(maximo, divisoes, inteiros))
            .OrderBy(e => e.Topo)
            .ThenBy(e => e.Marcas.Count)
            .First();
    }

    /// <summary>Altura em porcentagem (0 a 100) de um valor nesta escala.</summary>
    public decimal Altura(decimal valor) => Topo <= 0 || valor <= 0 ? 0 : Math.Clamp(valor / Topo * 100, 0, 100);

    private static EscalaDoGrafico Montar(decimal maximo, int divisoesProcuradas, bool inteiros)
    {
        var passo = PassoRedondo(maximo / divisoesProcuradas);
        if (inteiros && passo < 1)
        {
            passo = 1;
        }

        var divisoes = (int)Math.Ceiling(maximo / passo);
        var marcas = Enumerable.Range(0, divisoes + 1).Select(i => i * passo).ToList();
        return new EscalaDoGrafico(marcas[^1], marcas);
    }

    private static decimal PassoRedondo(decimal bruto)
    {
        var potencia = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)bruto)));
        var fracao = bruto / potencia;
        var fator = fracao switch
        {
            <= 1m => 1m,
            <= 2m => 2m,
            <= 2.5m => 2.5m,
            <= 5m => 5m,
            _ => 10m,
        };
        return fator * potencia;
    }
}
