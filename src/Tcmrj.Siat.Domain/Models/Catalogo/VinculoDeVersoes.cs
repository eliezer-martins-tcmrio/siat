namespace Tcmrj.Siat.Domain.Models.Catalogo;

/// <summary>
/// Campo <c>Versoes</c> de um item do <c>sistemasDb.js</c>: os nomes de <c>Apps_VersionAddresses.Sistema</c> que
/// correspondem ao item (specs/004-ficha-sistema/data-model.md e contracts/campo-versoes.md). O SIAT não deduz vínculo
/// por semelhança de nomes (FR-007): sem o campo, não há versões na ficha.
/// </summary>
public sealed class VinculoDeVersoes
{
    private readonly string[] normalizados;

    private VinculoDeVersoes(IReadOnlyList<string> nomes, bool invalido)
    {
        Nomes = nomes;
        Invalido = invalido;
        normalizados = nomes.Select(NomeDeSistema.Normalizar).ToArray();
    }

    /// <summary>Campo ausente ou <c>null</c>: sem vínculo (FR-018).</summary>
    public static VinculoDeVersoes Nenhum { get; } = new([], invalido: false);

    /// <summary>Campo que não é lista de textos (FR-010): o item continua no catálogo, sem vínculo e com aviso.</summary>
    public static VinculoDeVersoes ForaDoFormato { get; } = new([], invalido: true);

    /// <summary>
    /// Como escritos no catálogo, sem espaços nas pontas, sem vazios e sem repetidos (pela forma normalizada, fica a
    /// primeira grafia), na ordem do campo (FR-009).
    /// </summary>
    public IReadOnlyList<string> Nomes { get; }

    /// <summary>O campo existe e não é lista de textos; então <see cref="Nomes"/> é vazio.</summary>
    public bool Invalido { get; }

    public bool Vazio => Nomes.Count == 0;

    /// <summary>
    /// Nomes normalizados, na ordem, unidos por quebra de linha: parte da chave do cache das versões da ficha
    /// (research.md R5). Mudar o vínculo no catálogo muda a chave.
    /// </summary>
    public string Chave => string.Join('\n', normalizados);

    /// <param name="nomes">Os textos da lista do campo; nulos e vazios são descartados.</param>
    public static VinculoDeVersoes De(IEnumerable<string?> nomes)
    {
        ArgumentNullException.ThrowIfNull(nomes);

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var lista = new List<string>();
        foreach (var nome in nomes)
        {
            if (string.IsNullOrWhiteSpace(nome))
            {
                continue;
            }

            var limpo = nome.Trim();
            if (vistos.Add(NomeDeSistema.Normalizar(limpo)))
            {
                lista.Add(limpo);
            }
        }

        return new VinculoDeVersoes(lista, invalido: false);
    }

    /// <summary>Algum nome do vínculo é igual a <paramref name="nome"/> pela comparação de research.md R2.</summary>
    public bool Contem(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        var procurado = NomeDeSistema.Normalizar(nome);
        return normalizados.Contains(procurado, StringComparer.Ordinal);
    }
}
