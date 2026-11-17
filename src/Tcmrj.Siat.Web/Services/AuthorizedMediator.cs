using Microsoft.AspNetCore.Authorization;
using NetDevPack.SimpleMediator;

namespace Tcmrj.Siat.Web.Services;

/// <summary>
/// Mediador do modelo: resolvido no escopo do circuito, mantém a autorização no mesmo escopo dos handlers
/// e da identidade do usuário (nunca a partir do provedor raiz).
/// </summary>
internal sealed class AuthorizedMediator(IServiceProvider serviceProvider, IAuthorizationService authorizationService)
    : Mediator(serviceProvider)
{
    public IAuthorizationService AuthorizationService { get; } = authorizationService;
}
