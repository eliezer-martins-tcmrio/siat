namespace Tcmrj.Siat.Domain.Models.Catalogo;

/// <summary>Resultado de uma leitura bem-sucedida do <c>sistemasDb.js</c> (data-model.md).</summary>
/// <param name="Sistemas">Ordenados por nome na cultura pt-BR.</param>
/// <param name="Ignorados">Itens sem <c>Title</c> válido ou com estrutura inválida (FR-024).</param>
/// <param name="LidoEm">Momento da leitura (UTC), do <see cref="TimeProvider"/>.</param>
/// <param name="Origem">URL do arquivo na interface do GitLab, para o rodapé (FR-022).</param>
public sealed record CatalogoLido(IReadOnlyList<Sistema> Sistemas, int Ignorados, DateTimeOffset LidoEm, string Origem)
{
    /// <summary>
    /// O item da ficha pelo nome (specs/004-ficha-sistema/research.md R4): os itens com o nome normalizado igual ao
    /// pedido, por <see cref="Sistema.Posicao"/>; a ficha é do primeiro e os demais viram o aviso de repetição
    /// (FR-012, FR-020).
    /// </summary>
    /// <returns>Nulo quando nenhum item tem o nome (também para nome vazio ou só com espaços).</returns>
    public ItemLocalizado? Localizar(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        var procurado = NomeDeSistema.Normalizar(nome);
        if (procurado.Length == 0)
        {
            return null;
        }

        var iguais = Sistemas
            .Where(s => string.Equals(NomeDeSistema.Normalizar(s.Nome), procurado, StringComparison.Ordinal))
            .OrderBy(s => s.Posicao)
            .ToArray();

        return iguais.Length == 0 ? null : new ItemLocalizado(iguais[0], iguais.Length - 1);
    }

    /// <summary>
    /// Mapa do nome normalizado da tabela de versões ao nome do item cuja ficha o mostra (research.md R4). Percorre os
    /// itens na ordem do catálogo e, para cada nome do vínculo, fica o primeiro item. Só entram itens que são o
    /// primeiro do seu nome e com vínculo válido: nenhum link de Versões leva a uma ficha cujo vínculo não tenha
    /// aquele nome (SC-005).
    /// </summary>
    public IReadOnlyDictionary<string, string> FichasPorVersao()
    {
        var primeiros = Sistemas
            .GroupBy(s => NomeDeSistema.Normalizar(s.Nome), StringComparer.Ordinal)
            .Select(g => g.MinBy(s => s.Posicao)!)
            .ToHashSet<Sistema>(ReferenceEqualityComparer.Instance);

        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in Sistemas)
        {
            if (!primeiros.Contains(item) || item.Versoes.Invalido)
            {
                continue;
            }

            foreach (var nome in item.Versoes.Nomes)
            {
                mapa.TryAdd(NomeDeSistema.Normalizar(nome), item.Nome);
            }
        }

        return mapa;
    }
}

/// <summary>Item da ficha e a quantidade de outros itens com o mesmo nome (FR-020).</summary>
public sealed record ItemLocalizado(Sistema Item, int Repetidos);
