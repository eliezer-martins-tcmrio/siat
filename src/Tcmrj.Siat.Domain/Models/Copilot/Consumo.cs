using System.Globalization;

namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>Créditos e valores (USD) de um relatório, de um modelo ou de uma soma; <c>+</c> soma campo a campo.</summary>
public sealed record Consumo(decimal Creditos, decimal ValorBruto, decimal ValorLiquido)
{
    public static Consumo Zero { get; } = new(0, 0, 0);

    public static Consumo operator +(Consumo a, Consumo b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new(a.Creditos + b.Creditos, a.ValorBruto + b.ValorBruto, a.ValorLiquido + b.ValorLiquido);
    }

    public static Consumo Add(Consumo a, Consumo b) => a + b;
}

/// <summary>
/// Consumo de um modelo. <see cref="Modelo"/> nulo é "Modelo não informado". Dois nomes que só diferem em maiúsculas e
/// espaços nas pontas são o mesmo modelo (<see cref="Chave"/>), com a grafia da primeira ocorrência (research.md R5).
/// </summary>
public sealed record ConsumoPorModelo(string? Modelo, Consumo Consumo)
{
    /// <summary>Rótulo do modelo nulo.</summary>
    public const string NaoInformado = "Modelo não informado";

    private static readonly CompareInfo ComparacaoPtBr = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    /// <summary>Chave de comparação: o nome em minúsculas, ou um marcador para o modelo não informado.</summary>
    public string Chave => ChaveDe(Modelo);

    public string Rotulo => Modelo ?? NaoInformado;

    /// <summary>Nome sem espaços nas pontas; vazio ou só espaços vira nulo.</summary>
    public static string? Normalizar(string? modelo) => string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim();

    public static string ChaveDe(string? modelo) => modelo is null ? "\0" : modelo.ToLowerInvariant();

    /// <summary>
    /// Junta os modelos de mesma <see cref="Chave"/> (mantendo a grafia da primeira ocorrência) e ordena por créditos
    /// decrescentes e, no empate, pelo nome em pt-BR.
    /// </summary>
    public static IReadOnlyList<ConsumoPorModelo> Mesclar(IEnumerable<ConsumoPorModelo> modelos)
    {
        ArgumentNullException.ThrowIfNull(modelos);

        var porChave = new Dictionary<string, ConsumoPorModelo>(StringComparer.Ordinal);
        var ordem = new List<string>();
        foreach (var modelo in modelos)
        {
            if (porChave.TryGetValue(modelo.Chave, out var anterior))
            {
                porChave[modelo.Chave] = anterior with { Consumo = anterior.Consumo + modelo.Consumo };
            }
            else
            {
                porChave[modelo.Chave] = modelo;
                ordem.Add(modelo.Chave);
            }
        }

        return Ordenar(ordem.Select(chave => porChave[chave]));
    }

    /// <summary>Créditos decrescentes; no empate, nome do modelo em pt-BR (o não informado conta como vazio).</summary>
    public static IReadOnlyList<ConsumoPorModelo> Ordenar(IEnumerable<ConsumoPorModelo> modelos)
    {
        ArgumentNullException.ThrowIfNull(modelos);
        return
        [
            .. modelos
                .OrderByDescending(m => m.Consumo.Creditos)
                .ThenBy(m => m.Modelo ?? string.Empty, ComparadorPtBr.Instancia),
        ];
    }

    private sealed class ComparadorPtBr : IComparer<string>
    {
        public static ComparadorPtBr Instancia { get; } = new();

        public int Compare(string? x, string? y) => ComparacaoPtBr.Compare(x, y, CompareOptions.None);
    }
}

/// <summary>Item do relatório de consumo do GitHub (<c>usageItems[]</c>), como veio.</summary>
/// <param name="Modelo">Campo <c>model</c>; pode faltar.</param>
/// <param name="Unidade">Campo <c>unitType</c>: <c>credits</c> ou <c>ai-credits</c>.</param>
/// <param name="Quantidade">Campo <c>grossQuantity</c> (créditos).</param>
public sealed record ItemDeConsumo(string? Modelo, string Unidade, decimal Quantidade, decimal ValorBruto, decimal ValorLiquido);
