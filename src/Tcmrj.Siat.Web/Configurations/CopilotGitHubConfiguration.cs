using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.App.Web;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Web.Configurations;

/// <summary>
/// "Conectar ao GitHub" (specs/005-copilot-meu-consumo/contracts/conexao-github.md; research.md R2): esquemas
/// <see cref="EsquemaOAuth"/> e <see cref="ConexaoGitHubService.TipoDeAutenticacao"/> e os endpoints de conexão e
/// desconexão. Os esquemas só existem com <c>Copilot:OAuth</c> completo; nenhum deles é o padrão, e a autenticação do
/// SIAT (OIDC ou usuário de rede) não muda. Sem eles, o resto do SIAT sobe normalmente (Princípio IV).
/// </summary>
public static class CopilotGitHubConfiguration
{
    /// <summary>Esquema OAuth da OAuth App do SIAT no GitHub.</summary>
    public const string EsquemaOAuth = "GitHubOAuth";

    /// <summary>Nome do cookie da conexão.</summary>
    public const string NomeDoCookie = ".Siat.GitHub";

    /// <summary>Caminho do retorno do OAuth (relativo ao <c>&lt;base href&gt;</c>).</summary>
    public const string CaminhoDoRetorno = "/" + Navegacao.RotaRetornoGitHub;

    /// <summary>Motivo da desconexão pelo botão.</summary>
    public const string MotivoUsuario = "usuario";

    /// <summary>Valores de <c>conexao</c> que a página mostra depois de desconectar.</summary>
    public const string ConexaoDesconectada = "desconectada";

    public const string ConexaoNaoMembroDesconectada = "nao-membro-desconectada";

    /// <summary>Valor de <c>conexao</c> quando a OAuth App não está configurada.</summary>
    public const string ConexaoNaoConfigurada = "nao-configurado";

    public static WebApplicationBuilder AddCopilotGitHubConfiguration(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<RegistroConexaoGitHub>();
        builder.Services.AddScoped<ValidacaoRetornoGitHub>();

        var opcoes = builder.Configuration.GetSection(CopilotOptions.Secao).Get<CopilotOptions>() ?? new CopilotOptions();
        if (!opcoes.OAuthCompleto)
        {
            return builder;
        }

        var baseHref = builder.Configuration.GetSection(SiatOptions.Secao).Get<SiatOptions>()?.BlazorBaseHref;
        builder.Services.AddAuthentication()
            .AddCookie(ConexaoGitHubService.TipoDeAutenticacao, o =>
            {
                o.Cookie.Name = NomeDoCookie;
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.Path = string.IsNullOrEmpty(baseHref) ? "/" : baseHref;
                o.ExpireTimeSpan = ValidacaoRetornoGitHub.Validade;
                o.SlidingExpiration = false;

                // O cookie de conexão nunca desafia: sem ele, a página mostra a explicação.
                o.Events.OnRedirectToLogin = c => Responder(c.Response, StatusCodes.Status401Unauthorized);
                o.Events.OnRedirectToAccessDenied = c => Responder(c.Response, StatusCodes.Status403Forbidden);
            })
            .AddOAuth(EsquemaOAuth, o =>
            {
                o.ClientId = opcoes.OAuth.ClientId!;
                o.ClientSecret = opcoes.OAuth.ClientSecret!;
                o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                o.TokenEndpoint = "https://github.com/login/oauth/access_token";
                o.UserInformationEndpoint = ValidacaoRetornoGitHub.EnderecoDoUsuario.AbsoluteUri;
                o.CallbackPath = CaminhoDoRetorno;
                o.Scope.Clear(); // só a identificação da conta (FR-007)
                o.UsePkce = true;
                o.SaveTokens = false;
                o.SignInScheme = ConexaoGitHubService.TipoDeAutenticacao;
                o.Events = new OAuthEvents
                {
                    OnCreatingTicket = AoCriarTicketAsync,
                    OnRemoteFailure = AoFalharAsync,
                    OnAccessDenied = AoNegarAsync,
                };
            });

        return builder;
    }

    /// <summary>Endpoints <c>copilot/github/conectar</c> e <c>copilot/github/desconectar</c>.</summary>
    public static WebApplication UseCopilotGitHubConfiguration(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Navegacao.RotaConectarGitHub, ConectarAsync).RequireAuthorization(Modulo.CopilotProprio.Politica());

        // Exige só usuário autenticado (fallback policy): quem perdeu o recurso também pode apagar a conexão.
        app.MapGet(Navegacao.RotaDesconectarGitHub, DesconectarAsync);

        return app;
    }

    /// <summary>
    /// Desafio do <see cref="EsquemaOAuth"/> com a matrícula de quem pediu e o retorno local guardados nas propriedades
    /// (protegidas no <c>state</c>). Sem a OAuth App configurada, volta à página com a mensagem de não configurado.
    /// </summary>
    internal static async Task<IResult> ConectarAsync(HttpContext contexto, string? retorno)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var esquemas = contexto.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        if (await esquemas.GetSchemeAsync(EsquemaOAuth) is null)
        {
            return Results.LocalRedirect(Local(contexto.Request, ValidacaoRetornoGitHub.EnderecoDeVolta(ConexaoNaoConfigurada)));
        }

        var propriedades = new AuthenticationProperties
        {
            RedirectUri = Local(contexto.Request, RetornoLocal(retorno) ?? Navegacao.RotaMeuConsumo),
        };
        propriedades.Items[ValidacaoRetornoGitHub.PropriedadeMatricula] = contexto.User.FindFirst(ConexaoGitHubService.ClaimMatricula)?.Value;
        return Results.Challenge(propriedades, [EsquemaOAuth]);
    }

    /// <summary>
    /// Apaga o cookie de conexão, registra "Desconexao" (motivo <c>usuario</c> ou <c>nao-membro</c>) e volta com
    /// navegação completa: com <c>nao-membro</c>, sempre a <c>copilot/meu-consumo?conexao=nao-membro-desconectada</c>; senão, a
    /// <paramref name="retorno"/> local ou a <c>copilot/meu-consumo?conexao=desconectada</c>.
    /// </summary>
    internal static async Task<IResult> DesconectarAsync(HttpContext contexto, string? motivo, string? retorno)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var naoMembro = string.Equals(motivo, ValidacaoRetornoGitHub.MotivoNaoMembro, StringComparison.Ordinal);
        var conexao = ConexaoGitHubService.De(contexto.User);
        var servicos = contexto.RequestServices;

        if (await servicos.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync(ConexaoGitHubService.TipoDeAutenticacao) is not null)
        {
            await contexto.SignOutAsync(ConexaoGitHubService.TipoDeAutenticacao);
        }

        if (conexao is not null)
        {
            servicos.GetRequiredService<RegistroConexaoGitHub>().Registrar(
                OperacaoConexaoGitHub.Desconexao,
                contexto.User.FindFirst(ConexaoGitHubService.ClaimMatricula)?.Value,
                contexto.User.FindFirst("nome")?.Value,
                conexao.Login,
                conexao.GitHubId,
                servicos.GetService<IOptionsMonitor<CopilotOptions>>()?.CurrentValue.OrganizacaoNormalizada,
                naoMembro ? ValidacaoRetornoGitHub.MotivoNaoMembro : MotivoUsuario);
        }

        var destino = naoMembro
            ? ValidacaoRetornoGitHub.EnderecoDeVolta(ConexaoNaoMembroDesconectada)
            : RetornoLocal(retorno) ?? ValidacaoRetornoGitHub.EnderecoDeVolta(ConexaoDesconectada);
        return Results.LocalRedirect(Local(contexto.Request, destino));
    }

    /// <summary>
    /// Endereço de volta aceito: caminho relativo ao <c>&lt;base href&gt;</c> (como <c>copilot/meu-consumo/ana?de=...</c>),
    /// sem esquema, sem <c>/</c> ou <c>\</c> no início e sem caracteres de controle; qualquer outro valor é nulo.
    /// </summary>
    internal static string? RetornoLocal(string? retorno)
    {
        if (string.IsNullOrWhiteSpace(retorno)
            || retorno[0] is '/' or '\\'
            || retorno.Contains('\\', StringComparison.Ordinal)
            || retorno.Contains("://", StringComparison.Ordinal)
            || retorno.Any(char.IsControl)
            || !Uri.TryCreate(retorno, UriKind.Relative, out _))
        {
            return null;
        }

        return retorno.Trim();
    }

    private static string Local(HttpRequest pedido, string caminho) => $"{pedido.PathBase}/{caminho}";

    private static Task Responder(HttpResponse resposta, int codigo)
    {
        resposta.StatusCode = codigo;
        return Task.CompletedTask;
    }

    private static async Task<ClaimsPrincipal?> UsuarioDoSiatAsync(HttpContext contexto)
    {
        // O retorno é tratado antes de o usuário do esquema padrão ser lido: lê-se aqui (OIDC ou usuário de rede).
        var resultado = await contexto.AuthenticateAsync();
        return resultado.Succeeded ? resultado.Principal : null;
    }

    private static async Task AoCriarTicketAsync(OAuthCreatingTicketContext contexto)
    {
        var validacao = contexto.HttpContext.RequestServices.GetRequiredService<ValidacaoRetornoGitHub>();
        var identidade = await validacao.ValidarAsync(
            contexto.Backchannel,
            contexto.AccessToken,
            await UsuarioDoSiatAsync(contexto.HttpContext),
            contexto.Properties,
            contexto.HttpContext.RequestAborted);

        // Só a identidade validada vira cookie; o token do usuário não fica nas claims nem nas propriedades.
        contexto.Principal = new ClaimsPrincipal(identidade);
    }

    private static async Task AoFalharAsync(RemoteFailureContext contexto)
    {
        var validacao = contexto.HttpContext.RequestServices.GetRequiredService<ValidacaoRetornoGitHub>();
        var volta = validacao.TratarFalha(contexto.Failure, contexto.Properties, await UsuarioDoSiatAsync(contexto.HttpContext));
        contexto.Response.Redirect(Local(contexto.Request, volta));
        contexto.HandleResponse();
    }

    private static async Task AoNegarAsync(AccessDeniedContext contexto)
    {
        var validacao = contexto.HttpContext.RequestServices.GetRequiredService<ValidacaoRetornoGitHub>();
        var volta = validacao.TratarAcessoNegado(contexto.Properties, await UsuarioDoSiatAsync(contexto.HttpContext));
        contexto.Response.Redirect(Local(contexto.Request, volta));
        contexto.HandleResponse();
    }
}
