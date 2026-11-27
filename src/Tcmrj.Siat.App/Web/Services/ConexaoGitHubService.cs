using System.Globalization;
using System.Security.Claims;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Lê a conexão com o GitHub da identidade <see cref="TipoDeAutenticacao"/> do usuário do circuito
/// (specs/005-copilot-meu-consumo/research.md R3; contracts/conexao-github.md). O circuito não lê cookies: quem
/// valida o cookie e confere a matrícula é o middleware, na requisição que abre o circuito.
/// </summary>
public sealed class ConexaoGitHubService(IUsuarioAtualService usuarioAtual) : IConexaoGitHubService
{
    /// <summary>Esquema do cookie de conexão e tipo de autenticação da segunda identidade.</summary>
    public const string TipoDeAutenticacao = "GitHubConexao";

    /// <summary>Identidade numérica da conta do GitHub.</summary>
    public const string ClaimId = "github_id";

    /// <summary>Login da conta do GitHub na hora da conexão.</summary>
    public const string ClaimLogin = "github_login";

    /// <summary>Matrícula do usuário do SIAT que conectou (a mesma claim do UserManager).</summary>
    public const string ClaimMatricula = "matricula";

    public async Task<ConexaoGitHub?> ObterAsync()
    {
        var usuario = await usuarioAtual.ObterAsync();
        return De(usuario);
    }

    /// <summary>
    /// A conexão da identidade <see cref="TipoDeAutenticacao"/>, quando ela existe e tem <see cref="ClaimId"/>
    /// numérico e <see cref="ClaimLogin"/>; senão nulo.
    /// </summary>
    public static ConexaoGitHub? De(ClaimsPrincipal? usuario)
    {
        var identidade = usuario?.Identities.FirstOrDefault(i =>
            string.Equals(i.AuthenticationType, TipoDeAutenticacao, StringComparison.Ordinal));
        if (identidade is null)
        {
            return null;
        }

        var id = identidade.FindFirst(ClaimId)?.Value;
        var login = identidade.FindFirst(ClaimLogin)?.Value;
        return long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var gitHubId) && !string.IsNullOrWhiteSpace(login)
            ? new ConexaoGitHub(gitHubId, login)
            : null;
    }
}
