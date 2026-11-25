using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Models.Catalogo;

/// <summary>Um item do catálogo (data-model.md). <see cref="Ambientes"/> tem sempre as quatro chaves.</summary>
public sealed record Sistema
{
    /// <param name="versoes">Campo <c>Versoes</c>; nulo é <see cref="VinculoDeVersoes.Nenhum"/> (specs/004-ficha-sistema).</param>
    /// <param name="posicao">Posição do item na lista do arquivo, a partir de 1.</param>
    public Sistema(
        string nome,
        string? observacao,
        IReadOnlyDictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>> ambientes,
        VinculoDeVersoes? versoes = null,
        int posicao = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentNullException.ThrowIfNull(ambientes);

        Nome = nome.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        Ambientes = Enum.GetValues<AmbienteDoCatalogo>()
            .ToDictionary(a => a, a => ambientes.TryGetValue(a, out var instancias) ? instancias : (IReadOnlyList<Instancia>)[]);
        Versoes = versoes ?? VinculoDeVersoes.Nenhum;
        Posicao = posicao;
    }

    /// <summary>Campo <c>Title</c>, sem espaços nas pontas.</summary>
    public string Nome { get; }

    /// <summary>Campo <c>OBS</c>, com as quebras de linha preservadas; nulo quando vazio.</summary>
    public string? Observacao { get; }

    /// <summary>Instâncias por ambiente; lista vazia quando o ambiente falta ou só tinha instâncias vazias.</summary>
    public IReadOnlyDictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>> Ambientes { get; }

    /// <summary>
    /// Vínculo com os registros de <c>Apps_VersionAddresses</c> (specs/004-ficha-sistema/contracts/campo-versoes.md);
    /// <see cref="VinculoDeVersoes.Nenhum"/> quando o campo falta. A tela de Catálogo e a busca o ignoram (FR-011).
    /// </summary>
    public VinculoDeVersoes Versoes { get; }

    /// <summary>
    /// Posição do item na lista do <c>sistemasDb.js</c>, a partir de 1 (a mesma do log de itens ignorados). Desempata
    /// itens com o mesmo nome (specs/004-ficha-sistema/research.md R4).
    /// </summary>
    public int Posicao { get; }
}
