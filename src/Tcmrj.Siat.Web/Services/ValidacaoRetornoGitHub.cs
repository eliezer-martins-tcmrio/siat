using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Tcmrj.Siat.App.Web;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Web.Services;

/// <summary>
/// Validação do retorno do "Conectar ao GitHub" (specs/005-copilot-meu-consumo/contracts/conexao-github.md, Fluxo),
/// separada dos eventos do <c>AddOAuth</c> para teste:
/// <list type="number">
/// <item><c>GET /user</c> com o token do usuário → <c>id</c> e <c>login</c> (o token é descartado em seguida).</item>
/// <item>Matrícula do usuário atual igual à guardada no desafio, senão <c>outro-usuario</c>.</item>
/// <item>Associação com o token de leitura: <c>active</c> e o mesmo <c>id</c>, senão <c>nao-membro</c>.</item>
/// <item>Identidade com <c>github_id</c>, <c>github_login</c> e <c>matricula</c>, persistente por 30 dias, sem
/// renovação; registro "Conexao".</item>
/// </list>
/// Qualquer recusa vira <see cref="RecusaDeConexaoGitHubException"/>, que o evento de falha remota trata com
/// <see cref="TratarFalha"/>: registro "Recusa" e volta a <c>copilot?conexao={motivo}</c>, sem cookie.
/// </summary>
public sealed partial class ValidacaoRetornoGitHub(
    IGitHubCopilotRepository repositorio,
    RegistroConexaoGitHub registro,
    TimeProvider relogio,
    ILogger<ValidacaoRetornoGitHub> logger)
{
    /// <summary>Propriedade do desafio (protegida no <c>state</c>) com a matrícula de quem pediu a conexão.</summary>
    public const string PropriedadeMatricula = "siat.matricula";

    /// <summary>Validade do cookie de conexão.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromDays(30);

    public const string MotivoNegada = "negada";

    public const string MotivoFalha = "falha";

    public const string MotivoNaoMembro = "nao-membro";

    public const string MotivoOutroUsuario = "outro-usuario";

    /// <summary>Endereço do perfil da conta autenticada (token do usuário).</summary>
    public static readonly Uri EnderecoDoUsuario = new("https://api.github.com/user");

    /// <summary>
    /// Valida a conta do retorno e devolve a identidade da conexão; ajusta <paramref name="propriedades"/> para o
    /// cookie persistente de 30 dias.
    /// </summary>
    /// <param name="backchannel">Cliente HTTP do manipulador OAuth (o mesmo que trocou o código pelo token).</param>
    /// <param name="tokenDoUsuario">Token do usuário, usado só no <c>GET /user</c>.</param>
    /// <param name="usuarioAtual">Usuário do SIAT na requisição de retorno (esquema padrão).</param>
    /// <exception cref="RecusaDeConexaoGitHubException">Quando a conexão não pode ser criada, com o motivo.</exception>
    public async Task<ClaimsIdentity> ValidarAsync(
        HttpClient backchannel,
        string? tokenDoUsuario,
        ClaimsPrincipal? usuarioAtual,
        AuthenticationProperties propriedades,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backchannel);
        ArgumentNullException.ThrowIfNull(propriedades);

        var conta = await LerContaAsync(backchannel, tokenDoUsuario, cancellationToken)
            ?? throw new RecusaDeConexaoGitHubException(MotivoFalha);

        var matriculaGuardada = propriedades.Items.TryGetValue(PropriedadeMatricula, out var guardada) ? guardada : null;
        var matriculaAtual = usuarioAtual?.FindFirst(ConexaoGitHubService.ClaimMatricula)?.Value;
        if (string.IsNullOrEmpty(matriculaGuardada) || !string.Equals(matriculaGuardada, matriculaAtual, StringComparison.Ordinal))
        {
            throw new RecusaDeConexaoGitHubException(MotivoOutroUsuario, conta.Login, conta.Id);
        }

        var associacao = await repositorio.ObterAssociacaoAsync(conta.Login, cancellationToken);
        if (associacao.IsFailure)
        {
            throw new RecusaDeConexaoGitHubException(
                associacao.Error.Code == Erros.Copilot.CodigoNaoMembro ? MotivoNaoMembro : MotivoFalha, conta.Login, conta.Id);
        }

        if (!associacao.Value.AtivaPara(conta.Id))
        {
            throw new RecusaDeConexaoGitHubException(MotivoNaoMembro, conta.Login, conta.Id);
        }

        propriedades.IsPersistent = true;
        propriedades.IssuedUtc = relogio.GetUtcNow();
        propriedades.ExpiresUtc = relogio.GetUtcNow().Add(Validade);
        propriedades.AllowRefresh = false;

        registro.Registrar(
            OperacaoConexaoGitHub.Conexao, matriculaGuardada, Nome(usuarioAtual), conta.Login, conta.Id, repositorio.Organizacao, motivo: null);

        return new ClaimsIdentity(
            [
                new Claim(ConexaoGitHubService.ClaimId, conta.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ConexaoGitHubService.ClaimLogin, conta.Login),
                new Claim(ConexaoGitHubService.ClaimMatricula, matriculaGuardada),
            ],
            ConexaoGitHubService.TipoDeAutenticacao);
    }

    /// <summary>
    /// Falha do retorno (recusa da validação, acesso negado no GitHub, <c>state</c> ou correlação inválidos): registra
    /// "Recusa" com o motivo e devolve o endereço local de volta, <c>copilot?conexao={motivo}</c>.
    /// </summary>
    public string TratarFalha(Exception? falha, AuthenticationProperties? propriedades, ClaimsPrincipal? usuarioAtual)
    {
        var recusa = falha as RecusaDeConexaoGitHubException;
        var motivo = recusa?.Motivo ?? MotivoFalha;
        if (recusa is null && falha is not null)
        {
            // Só o tipo: a mensagem de falhas do OAuth pode citar o state ou a resposta do GitHub.
            FalhaNoRetorno(logger, falha.GetType().Name);
        }

        Recusar(motivo, propriedades, usuarioAtual, recusa?.Login, recusa?.Id);

        // Não membro: a página cita a conta na mensagem (research.md R9); o login vai como texto, validado lá.
        return motivo == MotivoNaoMembro && !string.IsNullOrEmpty(recusa?.Login)
            ? $"{EnderecoDeVolta(motivo)}&{Navegacao.ParametroConta}={Uri.EscapeDataString(recusa.Login)}"
            : EnderecoDeVolta(motivo);
    }

    /// <summary>Acesso negado ou cancelado na tela do GitHub (<c>error=access_denied</c>).</summary>
    public string TratarAcessoNegado(AuthenticationProperties? propriedades, ClaimsPrincipal? usuarioAtual)
    {
        Recusar(MotivoNegada, propriedades, usuarioAtual, login: null, id: null);
        return EnderecoDeVolta(MotivoNegada);
    }

    /// <summary>Endereço local (relativo ao <c>&lt;base href&gt;</c>) da página com a mensagem do motivo.</summary>
    public static string EnderecoDeVolta(string motivo) => $"{Navegacao.RotaCopilot}?{Navegacao.ParametroConexao}={Uri.EscapeDataString(motivo)}";

    private void Recusar(string motivo, AuthenticationProperties? propriedades, ClaimsPrincipal? usuarioAtual, string? login, long? id)
    {
        var matricula = usuarioAtual?.FindFirst(ConexaoGitHubService.ClaimMatricula)?.Value
            ?? (propriedades?.Items.TryGetValue(PropriedadeMatricula, out var guardada) == true ? guardada : null);
        registro.Registrar(OperacaoConexaoGitHub.Recusa, matricula, Nome(usuarioAtual), login, id, repositorio.Organizacao, motivo);
    }

    private static string? Nome(ClaimsPrincipal? usuario) => usuario?.FindFirst("nome")?.Value;

    // GET /user com o token do usuário; sem resposta válida, nulo (motivo "falha").
    private async Task<ContaDoRetorno?> LerContaAsync(HttpClient backchannel, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, EnderecoDoUsuario);
            pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            pedido.Headers.Add("X-GitHub-Api-Version", Infra.Copilot.GitHubCopilotRepository.VersaoDaApi);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            pedido.Headers.UserAgent.Add(new ProductInfoHeaderValue(Infra.Copilot.GitHubCopilotRepository.AgenteDoUsuario, null));

            using var resposta = await backchannel.SendAsync(pedido, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                ContaNaoLida(logger, (int)resposta.StatusCode);
                return null;
            }

            await using var corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
            var raiz = json.RootElement;
            return raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("id", out var id) && id.TryGetInt64(out var numero)
                && raiz.TryGetProperty("login", out var login) && login.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(login.GetString())
                    ? new ContaDoRetorno(numero, login.GetString()!)
                    : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException)
        {
            FalhaNoRetorno(logger, ex.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "O GitHub respondeu {Codigo} ao GET /user no retorno da conexão.")]
    private static partial void ContaNaoLida(ILogger logger, int codigo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha no retorno da conexão com o GitHub: {Tipo}.")]
    private static partial void FalhaNoRetorno(ILogger logger, string tipo);

    private sealed record ContaDoRetorno(long Id, string Login);
}

/// <summary>Conexão recusada no retorno do GitHub, com o motivo da tela e, quando lida, a conta.</summary>
public sealed class RecusaDeConexaoGitHubException : Exception
{
    public RecusaDeConexaoGitHubException()
        : this(ValidacaoRetornoGitHub.MotivoFalha)
    {
    }

    public RecusaDeConexaoGitHubException(string motivo)
        : this(motivo, null, null)
    {
    }

    public RecusaDeConexaoGitHubException(string motivo, Exception inner)
        : base($"Conexão com o GitHub recusada: {motivo}.", inner)
    {
        Motivo = motivo;
    }

    public RecusaDeConexaoGitHubException(string motivo, string? login, long? id)
        : base($"Conexão com o GitHub recusada: {motivo}.")
    {
        Motivo = motivo;
        Login = login;
        Id = id;
    }

    public string Motivo { get; } = ValidacaoRetornoGitHub.MotivoFalha;

    public string? Login { get; }

    public long? Id { get; }
}
