using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.App.Web.ViewModels.Catalogo;

/// <summary>Ficha de um item do catálogo para a tela (specs/004-ficha-sistema/data-model.md, View models).</summary>
/// <param name="Nome">Nome do item, como no catálogo.</param>
/// <param name="Observacao">Campo <c>OBS</c>, exibido como texto (FR-021).</param>
/// <param name="Ambientes">Sempre os quatro, na ordem DSV, HML, PRD e CONT.</param>
/// <param name="Repetidos">Outros itens com o mesmo nome (FR-020).</param>
/// <param name="Vinculo">Nomes do campo <c>Versoes</c>; vazio sem <c>Versoes_Sistemas.CanSelect</c>.</param>
/// <param name="VinculoInvalido">O campo não é lista de nomes (FR-010); falso sem o recurso de versões.</param>
/// <param name="MostraVersoes">O usuário tem <c>Versoes_Sistemas.CanSelect</c> (FR-002).</param>
/// <param name="LidoEm">Momento da leitura do catálogo (UTC).</param>
/// <param name="Origem">Link do <c>sistemasDb.js</c> no GitLab.</param>
/// <param name="Falha">A última releitura do catálogo falhou e a ficha vem da leitura anterior; nulo caso contrário.</param>
public sealed record FichaVM(
    string Nome,
    string? Observacao,
    IReadOnlyList<AmbienteVM> Ambientes,
    int Repetidos,
    IReadOnlyList<string> Vinculo,
    bool VinculoInvalido,
    bool MostraVersoes,
    DateTimeOffset LidoEm,
    string Origem,
    FalhaVM? Falha)
{
    /// <summary>Sem o recurso de versões, a ficha não leva o vínculo nem os avisos dele (FR-002, FR-010).</summary>
    public static FichaVM De(ItemLocalizado localizado, CatalogoObtido obtido, bool mostraVersoes)
    {
        ArgumentNullException.ThrowIfNull(localizado);
        ArgumentNullException.ThrowIfNull(obtido);

        var item = localizado.Item;
        return new FichaVM(
            item.Nome,
            item.Observacao,
            Enum.GetValues<AmbienteDoCatalogo>().Select(a => AmbienteVM.De(a, item.Ambientes[a])).ToArray(),
            localizado.Repetidos,
            mostraVersoes ? item.Versoes.Nomes : [],
            mostraVersoes && item.Versoes.Invalido,
            mostraVersoes,
            obtido.Catalogo.LidoEm,
            obtido.Catalogo.Origem,
            obtido.Falha is { } falha ? new FalhaVM(falha.Erro.Message, falha.Em) : null);
    }
}
