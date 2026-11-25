using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.App.Web.ViewModels.Versoes;

/// <summary>Versões do vínculo de um item num servidor (specs/004-ficha-sistema/data-model.md, View models).</summary>
/// <param name="Servidor">De onde vieram os endereços.</param>
/// <param name="GeradoEm">Fim da leitura (UTC), da tela de Versões ou da própria ficha.</param>
/// <param name="Duracao">Duração dessa leitura.</param>
/// <param name="Falha">A última releitura falhou e a ficha mostra o resultado anterior; nulo caso contrário.</param>
/// <param name="Componentes">Um por nome do vínculo com registro, na ordem do vínculo.</param>
/// <param name="NaoEncontrados">Nomes do vínculo sem registro ativo no servidor (FR-017).</param>
public sealed record VersoesDaFichaVM(
    ServidorVM Servidor,
    DateTimeOffset GeradoEm,
    TimeSpan Duracao,
    FalhaDasVersoesVM? Falha,
    IReadOnlyList<ComponenteVM> Componentes,
    IReadOnlyList<string> NaoEncontrados,
    bool TemDivergencia,
    bool TemErro)
{
    public bool SemPendencias => !TemDivergencia && !TemErro;

    /// <summary>
    /// Monta pelo <see cref="VersoesDoSistema"/> e pelo <see cref="SistemaVersoesVM.De"/>: as mesmas células e
    /// divergências da tela de Versões (SC-004).
    /// </summary>
    public static VersoesDaFichaVM De(VersoesObtidas obtidas, VinculoDeVersoes vinculo, ServidorVM servidor)
    {
        ArgumentNullException.ThrowIfNull(obtidas);
        ArgumentNullException.ThrowIfNull(vinculo);
        ArgumentNullException.ThrowIfNull(servidor);

        var versoes = VersoesDoSistema.De(obtidas.Versoes, vinculo);
        return new VersoesDaFichaVM(
            servidor,
            obtidas.Versoes.GeradoEm,
            obtidas.Versoes.Duracao,
            obtidas.Falha is { } falha ? new FalhaDasVersoesVM(falha.Erro.Message, falha.Em) : null,
            versoes.Componentes.Select(ComponenteVM.De).ToArray(),
            versoes.NaoEncontrados,
            versoes.TemDivergencia,
            versoes.TemErro);
    }

    /// <summary>Vínculo vazio: nada a ler (data-model.md, Validações).</summary>
    public static VersoesDaFichaVM Vazio(ServidorVM servidor, DateTimeOffset agora) =>
        new(servidor, agora, TimeSpan.Zero, null, [], [], false, false);
}
