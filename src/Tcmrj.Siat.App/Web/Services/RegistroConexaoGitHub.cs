using Microsoft.Extensions.Logging;
using Tcmrj.CoreLib.DBLog;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>Operações registradas da conexão com o GitHub (specs/005-copilot-meu-consumo/contracts/conexao-github.md).</summary>
public enum OperacaoConexaoGitHub
{
    /// <summary>Ligação criada.</summary>
    Conexao,

    /// <summary>Fluxo terminou sem ligação.</summary>
    Recusa,

    /// <summary>Cookie apagado (pelo botão ou forçado, na carga de quem deixou de ser membro).</summary>
    Desconexao,
}

/// <summary>
/// Registra conexão, recusa e desconexão no DBLog com TypeID 10, como o <c>RegistroConsultaService</c>
/// (specs/005-copilot-meu-consumo/research.md R10). Chamado pelo Web na requisição HTTP do OAuth, fora do circuito: a
/// matrícula vem de quem chama, não do <c>IUsuarioAtualService</c>. Fica no App porque é onde a extensão do DBLog com
/// TypeID está disponível (no Web, a do pacote AspNetCore tem o mesmo nome). Nunca registra token, código de autorização nem <c>state</c>, e nunca
/// propaga falha de gravação.
/// </summary>
public sealed class RegistroConexaoGitHub(ILogger<RegistroConexaoGitHub> logger)
{
    /// <summary>TypeID 10: "Log de Informações Gerais" em <c>audit.Log_Types</c>.</summary>
    internal const byte TipoLog = 10;

    internal const string Modelo = "{Operacao} da conta {GitHubLogin} do GitHub por {Matricula}";

    /// <summary>Login no texto quando a conta ainda não é conhecida (recusa antes de ler a conta).</summary>
    internal const string LoginDesconhecido = "(desconhecida)";

    /// <param name="operacao">O que aconteceu.</param>
    /// <param name="matricula">Matrícula do usuário do SIAT.</param>
    /// <param name="nome">Nome do usuário do SIAT, quando conhecido.</param>
    /// <param name="gitHubLogin">Login da conta do GitHub, quando conhecido.</param>
    /// <param name="gitHubId">Identidade numérica da conta, quando conhecida.</param>
    /// <param name="organizacao">Organização configurada.</param>
    /// <param name="motivo">Motivo da recusa (<c>negada</c>, <c>falha</c>, <c>nao-membro</c>, <c>outro-usuario</c>) ou da
    /// desconexão (<c>usuario</c>, <c>nao-membro</c>).</param>
    public void Registrar(
        OperacaoConexaoGitHub operacao,
        string? matricula,
        string? nome,
        string? gitHubLogin,
        long? gitHubId,
        string? organizacao,
        string? motivo)
    {
        try
        {
            var escopo = new Dictionary<string, object?>
            {
                ["Usuario"] = nome ?? string.Empty,
                ["GitHubId"] = gitHubId,
                ["Organizacao"] = organizacao ?? string.Empty,
                ["Motivo"] = motivo,
                ["Resultado"] = operacao == OperacaoConexaoGitHub.Recusa ? "Recusada" : "Sucesso",
            };

            using (logger.BeginScope(escopo))
            {
                logger.LogErrorWithLogUID(
                    TipoLog,
                    Modelo,
                    operacao.ToString(),
                    string.IsNullOrWhiteSpace(gitHubLogin) ? LoginDesconhecido : gitHubLogin,
                    matricula ?? string.Empty);
            }
        }
#pragma warning disable CA1031 // Falha ao gravar o registro não pode desfazer a conexão nem derrubar a requisição.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
