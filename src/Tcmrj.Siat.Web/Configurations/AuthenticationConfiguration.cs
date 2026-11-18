using Microsoft.AspNetCore.Authorization;
using Tcmrj.Autenticacao.AspNetCore.Oidc.IoC;
using Tcmrj.Autorizacao.App.Services;

namespace Tcmrj.Siat.Web.Configurations;

public static class AuthenticationConfiguration
{
    public static WebApplicationBuilder AddAuthenticationConfiguration(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // OIDC contra o SIA (esquemas "Cookies"/"oidc"). ClientId e ClientSecret vêm da seção TcmrjOidc do appsettings.
        builder.Services.AddTcmrjOidc(builder.Configuration);

        // Permissões do UserManager (connection string padrão UserManagerConnection), ITcmrjContext por circuito
        // e logout por inatividade (seção TcmrjSessao).
        builder.Services.AddTcmrjAutorizacaoBlazor(builder.Configuration);

        // O pacote já faz o bind de UserManagerService, mas não valida o ApplicationId: sem ele a aplicação não sobe.
        builder.Services.AddOptions<UserManagerServiceOptions>()
            .Validate(
                o => o.ApplicationId > 0,
                $"{UserManagerServiceOptions.UserManagerService}:{nameof(UserManagerServiceOptions.ApplicationId)} ausente ou inválido.")
            .ValidateOnStart();

        // Toda rota exige usuário autenticado; só Account/Login é anônima ([AllowAnonymous]).
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }
}
