using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>
/// As versões de um item do catálogo num servidor (specs/004-ficha-sistema/data-model.md, research.md R5), a partir de
/// um <see cref="VersoesLidas"/> completo (tela de Versões) ou parcial (leitura da ficha). Os sistemas da tabela fora
/// do vínculo são ignorados.
/// </summary>
public sealed class VersoesDoSistema
{
    private VersoesDoSistema(IReadOnlyList<ComponenteDaFicha> componentes, IReadOnlyList<string> naoEncontrados)
    {
        Componentes = componentes;
        NaoEncontrados = naoEncontrados;
        var registros = componentes.SelectMany(c => c.Registros).ToArray();
        TemDivergencia = registros.Any(r => r.TemDivergencia);
        TemErro = registros.Any(r => r.TemErro);
    }

    /// <summary>Um por nome do vínculo com registro ativo, na ordem do vínculo.</summary>
    public IReadOnlyList<ComponenteDaFicha> Componentes { get; }

    /// <summary>Nomes do vínculo sem registro ativo no servidor, na ordem do vínculo (FR-017).</summary>
    public IReadOnlyList<string> NaoEncontrados { get; }

    public bool TemDivergencia { get; }

    /// <summary>Algum registro tem célula com erro; nomes não encontrados não contam (FR-017).</summary>
    public bool TemErro { get; }

    public bool SemPendencias => !TemDivergencia && !TemErro;

    public static VersoesDoSistema De(VersoesLidas versoes, VinculoDeVersoes vinculo)
    {
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(vinculo);

        var porNome = versoes.Sistemas.ToLookup(s => NomeDeSistema.Normalizar(s.Nome), StringComparer.Ordinal);
        var componentes = new List<ComponenteDaFicha>();
        var naoEncontrados = new List<string>();
        foreach (var nome in vinculo.Nomes)
        {
            var registros = porNome[NomeDeSistema.Normalizar(nome)].OrderBy(s => s.Id).ToArray();
            if (registros.Length == 0)
            {
                naoEncontrados.Add(nome);
            }
            else
            {
                componentes.Add(new ComponenteDaFicha(nome, registros));
            }
        }

        return new VersoesDoSistema(componentes, naoEncontrados);
    }
}

/// <summary>Um nome do vínculo e os seus registros na tabela.</summary>
/// <param name="Nome">Como escrito no vínculo.</param>
/// <param name="Registros">Por <c>Id</c>; mais de um quando o nome se repete na tabela.</param>
public sealed record ComponenteDaFicha(string Nome, IReadOnlyList<SistemaVersionado> Registros);
