namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>
/// Conta do GitHub ligada ao usuário do SIAT (specs/005-copilot-meu-consumo, FR-006 a FR-011; data-model.md). Vem só do
/// cookie de conexão, entregue ao circuito pelo middleware quando a matrícula do cookie é a do usuário autenticado.
/// </summary>
/// <param name="GitHubId">Identidade numérica da conta; é ela que liga, não o login.</param>
/// <param name="Login">Login na hora da conexão; pode ficar velho se a conta for renomeada (research.md R4).</param>
public sealed record ConexaoGitHub(long GitHubId, string Login);
