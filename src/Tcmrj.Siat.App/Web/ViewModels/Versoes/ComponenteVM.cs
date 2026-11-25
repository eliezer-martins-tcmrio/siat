using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.App.Web.ViewModels.Versoes;

/// <summary>Um nome do vínculo e os seus registros, como linhas da tela de Versões.</summary>
/// <param name="Nome">Como escrito no vínculo.</param>
/// <param name="Registros">Por <c>Id</c>, cada um com as seis células.</param>
public sealed record ComponenteVM(string Nome, IReadOnlyList<SistemaVersoesVM> Registros)
{
    public static ComponenteVM De(ComponenteDaFicha componente)
    {
        ArgumentNullException.ThrowIfNull(componente);
        return new ComponenteVM(componente.Nome, componente.Registros.Select(SistemaVersoesVM.De).ToArray());
    }
}
