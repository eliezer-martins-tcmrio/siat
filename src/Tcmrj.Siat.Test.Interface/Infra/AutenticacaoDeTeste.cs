using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Autorizacao.App.Services;
using Tcmrj.UserManager.Domain.Models;
using Tcmrj.UserManager.Domain.Models.Enums;

namespace Tcmrj.Siat.Test.Interface.Infra;

/// <summary>
/// Usuário simulado de um teste. Os recursos são nomes de política do UserManager
/// (<c>"MailQueue.CanSelect"</c>, <c>"MailQueuePRD.CanSelect"</c>...) e podem mudar durante o teste
/// (por exemplo, para retirar a permissão no meio da sessão): não há cache de permissão.
/// </summary>
public sealed class UsuarioDeTeste
{
    private readonly ConcurrentDictionary<string, byte> _recursos = new(StringComparer.Ordinal);

    public UsuarioDeTeste(params string[] recursos) => Conceder(recursos);

    /// <summary>Matrícula (claim <c>matricula</c>, que é também o nome da identidade).</summary>
    public int Matricula { get; init; } = 12345;

    /// <summary>Nome (claim <c>nome</c>); nulo omite a claim.</summary>
    public string? Nome { get; init; } = "Usuária de Teste";

    public IReadOnlyCollection<string> Recursos => _recursos.Keys.ToArray();

    public UsuarioDeTeste Conceder(params string[] recursos)
    {
        ArgumentNullException.ThrowIfNull(recursos);
        foreach (var recurso in recursos)
        {
            _recursos[recurso] = 0;
        }

        return this;
    }

    public UsuarioDeTeste Revogar(params string[] recursos)
    {
        ArgumentNullException.ThrowIfNull(recursos);
        foreach (var recurso in recursos)
        {
            _recursos.TryRemove(recurso, out _);
        }

        return this;
    }

    public bool Tem(string recurso, AccessType acesso) => _recursos.ContainsKey($"{recurso}.{acesso}");
}

/// <summary>Usuários registrados no host de teste. O navegador leva o id no cookie <see cref="AutenticacaoDeTeste.NomeCookie"/>.</summary>
public sealed class UsuariosDeTeste
{
    private readonly ConcurrentDictionary<string, UsuarioDeTeste> _usuarios = new(StringComparer.Ordinal);

    /// <summary>Registra o usuário e devolve o id que vai no cookie.</summary>
    public string Registrar(UsuarioDeTeste usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        var id = Guid.NewGuid().ToString("N");
        _usuarios[id] = usuario;
        return id;
    }

    public UsuarioDeTeste? Obter(string? id) => id is not null && _usuarios.TryGetValue(id, out var usuario) ? usuario : null;

    public UsuarioDeTeste? ObterPorMatricula(int matricula) => _usuarios.Values.FirstOrDefault(u => u.Matricula == matricula);
}

/// <summary>
/// Esquema de autenticação dos testes de interface: substitui <c>Cookies</c>/<c>oidc</c> como esquema padrão
/// (autenticação e challenge). O usuário vem do cookie <see cref="NomeCookie"/>, que o teste põe no contexto do
/// navegador (<see cref="SiatWebApplicationFactory.NovoContextoAsync"/>); o circuito Blazor recebe o usuário da
/// requisição do hub, autenticada pelo mesmo cookie. O <see cref="IUserManagerService"/> é trocado por
/// <see cref="UserManagerServiceFalso"/>, de modo que <c>UserManagerPolicyProvider</c> e <c>PermissionHandler</c>
/// reais continuam decidindo as políticas. O <c>AuthenticationStateProvider</c> de inatividade do pacote não é trocado.
/// </summary>
public static class AutenticacaoDeTeste
{
    public const string Esquema = "Teste";

    public const string NomeCookie = "siat-teste-usuario";

    /// <summary>Claim com o id do <see cref="UsuarioDeTeste"/> registrado.</summary>
    public const string ClaimId = "siat-teste-id";

    public static IServiceCollection AdicionarAutenticacaoDeTeste(this IServiceCollection services, UsuariosDeTeste usuarios)
    {
        services.AddSingleton(usuarios);
        services.AddAuthentication(o =>
            {
                o.DefaultScheme = Esquema;
                o.DefaultAuthenticateScheme = Esquema;
                o.DefaultChallengeScheme = Esquema;
                o.DefaultForbidScheme = Esquema;
                o.DefaultSignInScheme = Esquema;
                o.DefaultSignOutScheme = Esquema;
            })
            .AddScheme<AuthenticationSchemeOptions, Handler>(Esquema, _ => { });

        services.RemoveAll<IUserManagerService>();
        services.AddScoped<IUserManagerService, UserManagerServiceFalso>();
        return services;
    }

    internal sealed class Handler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        UsuariosDeTeste usuarios) : SignInAuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var id = Request.Cookies[NomeCookie];
            var usuario = usuarios.Obter(id);
            if (usuario is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimId, id!),
                new("matricula", usuario.Matricula.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("sessionId", "1"),
            };
            if (usuario.Nome is not null)
            {
                claims.Add(new Claim("nome", usuario.Nome));
            }

            var identidade = new ClaimsIdentity(claims, Esquema, "matricula", "role");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Response.WriteAsync("Sem usuario de teste (cookie siat-teste-usuario).");
        }

        protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties) => Task.CompletedTask;

        protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
        {
            Response.Cookies.Delete(NomeCookie);
            return Task.CompletedTask;
        }
    }
}

/// <summary>Permissões do UserManager vindas do <see cref="UsuarioDeTeste"/> (sem banco).</summary>
public sealed class UserManagerServiceFalso(UsuariosDeTeste usuarios) : IUserManagerService
{
    public Task<bool> UserHasPemissionAsync(ClaimsPrincipal user, string resourceKey, AccessType accessType, int? organizationUnitID)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(false);
        }

        var usuario = usuarios.Obter(user.FindFirst(AutenticacaoDeTeste.ClaimId)?.Value);
        return Task.FromResult(usuario?.Tem(resourceKey, accessType) == true);
    }

    public Task<bool> UserHasPemissionAsync(int userID, string resourceKey, AccessType accessType, int? organizationUnitID) =>
        Task.FromResult(usuarios.ObterPorMatricula(userID)?.Tem(resourceKey, accessType) == true);

    public Task<IEnumerable<User>> GetAllUsersWithResourceAccess(int resourceId, int appId, AccessType accessType, int? organizationUnitId = null) =>
        Task.FromResult(Enumerable.Empty<User>());
}
