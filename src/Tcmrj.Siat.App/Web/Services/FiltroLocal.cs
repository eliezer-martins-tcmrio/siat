using System.Globalization;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Filtro local dos itens já carregados (FR-020, research.md R9): sem diferenciar acentos e maiúsculas
/// (<see cref="CompareInfo.IndexOf(string, string, CompareOptions)"/> em pt-BR com <see cref="CompareOptions.IgnoreCase"/> e
/// <see cref="CompareOptions.IgnoreNonSpace"/>), com vários termos separados por espaço (todos precisam aparecer).
/// Nunca consulta o banco.
/// </summary>
public static class FiltroLocal
{
    public const CompareOptions Opcoes = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static CultureInfo Cultura { get; } = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Termos do texto digitado, separados por espaços; vazio quando não há filtro.</summary>
    public static IReadOnlyList<string> Termos(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Verdadeiro se todos os <paramref name="termos"/> aparecem em <paramref name="texto"/> (nenhum termo: verdadeiro).</summary>
    public static bool Contem(string? texto, IReadOnlyList<string> termos)
    {
        ArgumentNullException.ThrowIfNull(termos);
        if (termos.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(texto))
        {
            return false;
        }

        var comparador = Cultura.CompareInfo;
        foreach (var termo in termos)
        {
            if (comparador.IndexOf(texto, termo, Opcoes) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Itens cujo texto contém todos os termos de <paramref name="texto"/>, na mesma ordem.</summary>
    public static IReadOnlyList<T> Filtrar<T>(IReadOnlyList<T> itens, Func<T, string?> textoDe, string? texto)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(textoDe);

        var termos = Termos(texto);
        return termos.Count == 0 ? itens : itens.Where(i => Contem(textoDe(i), termos)).ToArray();
    }

    /// <summary>
    /// Contador da barra: "Y {unidade}" sem filtro e "X de Y {unidade}" com filtro; vazio quando nada foi carregado.
    /// </summary>
    public static string Contador(int visiveis, int total, bool filtrando, string unidade)
    {
        if (total <= 0)
        {
            return string.Empty;
        }

        var unidadeDoTotal = Singular(total, unidade);
        var totalTexto = total.ToString(Cultura);
        return filtrando
            ? $"{visiveis.ToString(Cultura)} de {totalTexto} {unidadeDoTotal}"
            : $"{totalTexto} {unidadeDoTotal}";
    }

    /// <summary>"1 e-mail", "2 e-mails": quantidade e unidade no número certo (a unidade vem no plural).</summary>
    public static string Quantidade(int quantidade, string unidade) =>
        $"{quantidade.ToString(Cultura)} {Singular(quantidade, unidade)}";

    private static string Singular(int quantidade, string unidade) =>
        quantidade != 1 ? unidade
        : unidade.EndsWith("ns", StringComparison.Ordinal) ? string.Concat(unidade.AsSpan(0, unidade.Length - 2), "m")
        : unidade.EndsWith('s') ? unidade[..^1]
        : unidade;
}

/// <summary>
/// Filtro local de uma lista: guarda o último resultado e devolve a mesma instância enquanto os itens, o texto e a
/// função de texto não mudam, para a lista expansível manter a linha aberta entre renderizações.
/// </summary>
public sealed class FiltroLocal<T>
{
    private IReadOnlyList<T>? fonte;
    private Func<T, string?>? textoUsado;
    private string? termoUsado;
    private IReadOnlyList<T> visiveis = [];

    /// <summary>Itens que passam pelo filtro <paramref name="texto"/>, na ordem de <paramref name="itens"/>.</summary>
    public IReadOnlyList<T> Aplicar(IReadOnlyList<T> itens, Func<T, string?> textoDe, string? texto)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(textoDe);

        var termo = texto?.Trim() ?? string.Empty;
        if (termo.Length == 0)
        {
            return itens;
        }

        if (ReferenceEquals(fonte, itens) && ReferenceEquals(textoUsado, textoDe) && string.Equals(termoUsado, termo, StringComparison.Ordinal))
        {
            return visiveis;
        }

        visiveis = FiltroLocal.Filtrar(itens, textoDe, termo);
        fonte = itens;
        textoUsado = textoDe;
        termoUsado = termo;
        return visiveis;
    }
}
