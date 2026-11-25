using Tcmrj.Siat.App.Web.ViewModels.Versoes;

namespace Tcmrj.Siat.App.Web.ViewModels.Catalogo;

/// <summary>
/// Mapa da tela de Versões para as fichas (specs/004-ficha-sistema/data-model.md, View models): nome normalizado da
/// tabela → nome do item do catálogo cujo vínculo o contém (research.md R4).
/// </summary>
/// <param name="PorNome">Chave: nome da tabela normalizado (<see cref="SistemaVersoesVM.NomeNormalizado"/>).</param>
public sealed record FichasVM(IReadOnlyDictionary<string, string> PorNome)
{
    /// <summary>Nome do item cuja ficha mostra a linha, ou nulo quando nenhum vínculo a cita.</summary>
    public string? Ficha(SistemaVersoesVM sistema)
    {
        ArgumentNullException.ThrowIfNull(sistema);
        return PorNome.GetValueOrDefault(sistema.NomeNormalizado);
    }
}
