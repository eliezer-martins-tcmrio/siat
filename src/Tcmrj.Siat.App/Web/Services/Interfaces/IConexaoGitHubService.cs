using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>
/// Conta do GitHub conectada pelo usuário do circuito (specs/005-copilot-meu-consumo/research.md R3), lida a cada
/// chamada da segunda identidade que o <c>ConexaoGitHubMiddleware</c> acrescenta ao usuário quando o cookie de
/// conexão é válido e é do mesmo usuário do SIAT.
/// </summary>
public interface IConexaoGitHubService
{
    /// <returns>A conexão, ou nulo quando não há conta conectada para este usuário neste navegador.</returns>
    Task<ConexaoGitHub?> ObterAsync();
}
