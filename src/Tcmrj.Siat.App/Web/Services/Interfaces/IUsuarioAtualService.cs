using System.Security.Claims;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>Usuário do circuito Blazor atual, lido a cada chamada (sem cache).</summary>
public interface IUsuarioAtualService
{
    Task<ClaimsPrincipal> ObterAsync();
}
