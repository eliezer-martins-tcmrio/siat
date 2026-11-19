using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Tcmrj.Autenticacao.AspNetCore.Oidc.IoC;
using Tcmrj.Autorizacao.App.Services;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Repositories;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Web.Configurations;

public static class AuthenticationConfiguration
{
    public static WebApplicationBuilder AddAuthenticationConfiguration(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // PROTÓTIPO (features/prototipo_login_rede): entrada pelo usuário de rede em vez do OIDC no SIA.
        // Modo misto: o IIS aceita anônimo e Windows. A autenticação do Windows é ligada pela infra no
        // applicationHost.config, só para o aplicativo (como no SuporteASI); o projeto não tem web.config próprio,
        // porque a seção vem bloqueada no servidor e declará-la daria HTTP 500.19. A fallback policy abaixo exige
        // usuário autenticado e, sem ele, o desafio Negotiate faz o navegador enviar as credenciais do Windows. No IIS
        // o handler repassa a autenticação para o próprio IIS; no Kestrel (dotnet run) ele mesmo faz o Negotiate.
        builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

        // DOMINIO\login -> matrícula (view do RH no CSLIB_UserManager) -> claims que o pacote de autorização lê.
        builder.Services.AddOptions<UsuarioDeRedeOptions>()
            .Bind(builder.Configuration.GetSection(UsuarioDeRedeOptions.Secao))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Dominio), $"{UsuarioDeRedeOptions.Secao}:{nameof(UsuarioDeRedeOptions.Dominio)} ausente.")
            .Validate(o => o.MinutosDeCache > 0, $"{UsuarioDeRedeOptions.Secao}:{nameof(UsuarioDeRedeOptions.MinutosDeCache)} deve ser maior que zero.")
            .ValidateOnStart();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IUsuariosDeRedeRepository, UsuariosDeRedeRepository>();
        builder.Services.AddSingleton<IClaimsTransformation, ClaimsDoUsuarioDeRede>();

        // Permissões do UserManager (connection string padrão UserManagerConnection), ITcmrjContext por circuito
        // e logout por inatividade (seção TcmrjSessao). O pacote só depende da claim "matricula", não do OIDC.
        builder.Services.AddTcmrjAutorizacaoBlazor(builder.Configuration);

        // O pacote já faz o bind de UserManagerService, mas não valida o ApplicationId: sem ele a aplicação não sobe.
        builder.Services.AddOptions<UserManagerServiceOptions>()
            .Validate(
                o => o.ApplicationId > 0,
                $"{UserManagerServiceOptions.UserManagerService}:{nameof(UserManagerServiceOptions.ApplicationId)} ausente ou inválido.")
            .ValidateOnStart();

        // Toda rota exige usuário autenticado; só os arquivos estáticos e /saude são anônimos ([AllowAnonymous]).
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }
}
