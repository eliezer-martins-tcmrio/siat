using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Chave do cache e do andamento das versões da ficha (specs/004-ficha-sistema/data-model.md): o servidor e o vínculo.
/// Dois itens com o mesmo vínculo leriam exatamente os mesmos registros; mudar o vínculo no catálogo muda a chave, e a
/// ficha lê de novo.
/// </summary>
/// <param name="Servidor">Chave do servidor na lista fechada.</param>
/// <param name="Vinculo"><see cref="VinculoDeVersoes.Chave"/>: nomes normalizados unidos por quebra de linha.</param>
public readonly record struct ChaveDaFicha(string Servidor, string Vinculo)
{
    /// <summary>
    /// Chave no dicionário de andamento, ao lado das chaves de servidor da tela de Versões: a quebra de linha não
    /// aparece em chave de servidor, então as duas nunca se confundem.
    /// </summary>
    public string ChaveDeAndamento => $"{Servidor}\n{Vinculo}";

    public static ChaveDaFicha De(string chaveServidor, VinculoDeVersoes vinculo)
    {
        ArgumentNullException.ThrowIfNull(chaveServidor);
        ArgumentNullException.ThrowIfNull(vinculo);
        return new ChaveDaFicha(chaveServidor, vinculo.Chave);
    }

    public static ChaveDaFicha De(Servidor servidor, VinculoDeVersoes vinculo)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        return De(servidor.Chave, vinculo);
    }

    /// <summary>Nomes normalizados do vínculo, para filtrar as linhas da tabela.</summary>
    public IReadOnlySet<string> Nomes() =>
        Vinculo.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
}
