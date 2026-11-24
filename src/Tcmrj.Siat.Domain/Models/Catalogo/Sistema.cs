using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Models.Catalogo;

/// <summary>Um item do catálogo (data-model.md). <see cref="Ambientes"/> tem sempre as quatro chaves.</summary>
public sealed record Sistema
{
    public Sistema(string nome, string? observacao, IReadOnlyDictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>> ambientes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentNullException.ThrowIfNull(ambientes);

        Nome = nome.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        Ambientes = Enum.GetValues<AmbienteDoCatalogo>()
            .ToDictionary(a => a, a => ambientes.TryGetValue(a, out var instancias) ? instancias : (IReadOnlyList<Instancia>)[]);
    }

    /// <summary>Campo <c>Title</c>, sem espaços nas pontas.</summary>
    public string Nome { get; }

    /// <summary>Campo <c>OBS</c>, com as quebras de linha preservadas; nulo quando vazio.</summary>
    public string? Observacao { get; }

    /// <summary>Instâncias por ambiente; lista vazia quando o ambiente falta ou só tinha instâncias vazias.</summary>
    public IReadOnlyDictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>> Ambientes { get; }
}
