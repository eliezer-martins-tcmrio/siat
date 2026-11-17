using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Tcmrj.Siat.App.Web.Services.Interfaces;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>Usuário do circuito Blazor, lido do <see cref="AuthenticationStateProvider"/> a cada chamada (sem cache).</summary>
public sealed class UsuarioAtualService(AuthenticationStateProvider authenticationStateProvider) : IUsuarioAtualService
{
    public async Task<ClaimsPrincipal> ObterAsync()
    {
        var estado = await authenticationStateProvider.GetAuthenticationStateAsync();
        return estado.User;
    }
}
