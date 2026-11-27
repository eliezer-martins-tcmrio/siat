using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Tcmrj.Siat.App.Web.Services;

namespace Tcmrj.Siat.Web.Services;

/// <summary>
/// Entrega a conexão com o GitHub ao circuito Blazor (specs/005-copilot-meu-consumo/research.md R3). O componente não
/// tem <c>HttpContext</c> nem lê cookies; o circuito conhece o usuário pelo <c>HttpContext.User</c> da conexão do
/// SignalR. Por isso, em toda requisição com usuário autenticado, este middleware (logo depois de
/// <c>UseAuthentication</c>) lê o cookie <see cref="ConexaoGitHubService.TipoDeAutenticacao"/> e, se ele for válido e
/// a matrícula dele for a do usuário autenticado, acrescenta ao usuário uma segunda identidade com o id e o login da
/// conta. Cookie de outro usuário do SIAT, vencido ou adulterado é ignorado (FR-009).
/// <para>
/// Middleware e não <c>IClaimsTransformation</c>: no modo usuário de rede o SIAT já registra uma transformação, e o
/// ASP.NET Core usa só uma; o middleware vale igual nos dois modos de autenticação. Nunca lança: sem os esquemas do
/// GitHub (seção <c>Copilot</c> incompleta) ou com qualquer falha, a requisição segue sem a conexão (Princípio IV).
/// </para>
/// </summary>
public sealed partial class ConexaoGitHubMiddleware(RequestDelegate next, ILogger<ConexaoGitHubMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await AcrescentarConexaoAsync(context);
        await next(context);
    }

    private async Task AcrescentarConexaoAsync(HttpContext context)
    {
        var usuario = context.User;
        if (usuario?.Identity?.IsAuthenticated != true
            || usuario.Identities.Any(i => string.Equals(i.AuthenticationType, ConexaoGitHubService.TipoDeAutenticacao, StringComparison.Ordinal)))
        {
            return;
        }

        var matricula = usuario.FindFirst(ConexaoGitHubService.ClaimMatricula)?.Value;
        if (string.IsNullOrEmpty(matricula))
        {
            return;
        }

        try
        {
            var esquemas = context.RequestServices.GetService<IAuthenticationSchemeProvider>();
            if (esquemas is not null && await esquemas.GetSchemeAsync(ConexaoGitHubService.TipoDeAutenticacao) is null)
            {
                return;
            }

            var resultado = await context.AuthenticateAsync(ConexaoGitHubService.TipoDeAutenticacao);
            if (!resultado.Succeeded || resultado.Principal is not { } doCookie)
            {
                return;
            }

            if (!string.Equals(doCookie.FindFirst(ConexaoGitHubService.ClaimMatricula)?.Value, matricula, StringComparison.Ordinal))
            {
                // Cookie de outro usuário do SIAT neste navegador: a conta dele não vale para este usuário.
                return;
            }

            var id = doCookie.FindFirst(ConexaoGitHubService.ClaimId)?.Value;
            var login = doCookie.FindFirst(ConexaoGitHubService.ClaimLogin)?.Value;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(login))
            {
                return;
            }

            usuario.AddIdentity(new ClaimsIdentity(
                [new Claim(ConexaoGitHubService.ClaimId, id), new Claim(ConexaoGitHubService.ClaimLogin, login)],
                ConexaoGitHubService.TipoDeAutenticacao));
        }
#pragma warning disable CA1031 // Falha ao ler o cookie de conexão não pode derrubar a requisição (Princípio IV).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            FalhaAoLerConexao(logger, ex.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível ler a conexão com o GitHub: {Tipo}.")]
    private static partial void FalhaAoLerConexao(ILogger logger, string tipo);
}
