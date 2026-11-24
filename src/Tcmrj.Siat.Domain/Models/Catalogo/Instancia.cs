namespace Tcmrj.Siat.Domain.Models.Catalogo;

/// <summary>
/// Um lugar onde o sistema roda num ambiente (data-model.md). Valores já sem espaços nas pontas e sem partes
/// vazias. Invariante: pelo menos um dos três preenchido; instância toda vazia não é criada (R3).
/// </summary>
public sealed record Instancia
{
    public Instancia(IReadOnlyList<string> enderecos, IReadOnlyList<string> maquinas, string? caminho)
    {
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(maquinas);

        Caminho = string.IsNullOrWhiteSpace(caminho) ? null : caminho.Trim();
        if (enderecos.Count == 0 && maquinas.Count == 0 && Caminho is null)
        {
            throw new ArgumentException("A instância precisa de endereço, máquina ou caminho.", nameof(enderecos));
        }

        Enderecos = enderecos;
        Maquinas = maquinas;
    }

    /// <summary>Endereços do campo <c>URL</c>, divididos por <c>|</c>.</summary>
    public IReadOnlyList<string> Enderecos { get; }

    /// <summary>Máquinas do campo <c>Machine</c>, divididas por vírgula.</summary>
    public IReadOnlyList<string> Maquinas { get; }

    /// <summary>Campo <c>Path</c>, ou nulo.</summary>
    public string? Caminho { get; }
}
