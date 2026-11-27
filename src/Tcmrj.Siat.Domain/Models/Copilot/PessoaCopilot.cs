using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>
/// A conta conectada, como membro da organização (specs/005-copilot-meu-consumo, FR-013, FR-021, FR-032;
/// data-model.md). Existe quando há licença ou budget individual; os times e o limite falham sem derrubar o resto.
/// </summary>
/// <param name="Login">Login atual, do perfil; com o perfil indisponível, o da conexão.</param>
/// <param name="PerfilDisponivel">Falso quando o perfil não respondeu: <see cref="Nome"/> e <see cref="FotoUrl"/> nulos.</param>
/// <param name="Nome">Nome público sem espaços nas pontas; vazio vira nulo.</param>
/// <param name="FotoUrl"><c>avatar_url</c> do perfil, só <c>https</c>.</param>
/// <param name="Times">Times da pessoa (pode ser vazia) ou o erro de qualquer time sem resposta.</param>
/// <param name="Limite">Limite vigente ou o erro da leitura dos budgets.</param>
public sealed record PessoaCopilot(
    long GitHubId,
    string Login,
    bool PerfilDisponivel,
    string? Nome,
    string? FotoUrl,
    bool TemLicenca,
    Result<IReadOnlyList<TimeDaPessoa>, Erro> Times,
    Result<LimiteVigente, Erro> Limite);

/// <summary>Time da organização de que a pessoa é membro.</summary>
/// <param name="Herdado"><c>true</c> quando vem de um sub-time ("via sub-time"); nulo quando a API não informa.</param>
public sealed record TimeDaPessoa(long Id, string Nome, string Slug, bool? Herdado);

/// <summary>Time da organização, como a lista de times do GitHub o devolve.</summary>
public sealed record TimeDaOrganizacao(long Id, string Nome, string Slug);

/// <summary>Membro de um time; <paramref name="Herdado"/> vem do campo <c>inherited</c>, que pode faltar.</summary>
public sealed record MembroDoTime(string Login, bool? Herdado);

/// <summary>Licença Copilot de uma conta (<c>seats[].assignee</c>).</summary>
public sealed record Licenca(long Id, string Login);

/// <summary>Associação da conta à organização (<c>GET /orgs/{org}/memberships/{login}</c>).</summary>
/// <param name="Estado">Campo <c>state</c>: <c>active</c> ou <c>pending</c>.</param>
/// <param name="Papel">Campo <c>role</c>.</param>
/// <param name="UsuarioId">Campo <c>user.id</c>.</param>
public sealed record Associacao(string Estado, string Papel, long UsuarioId)
{
    public const string EstadoAtivo = "active";

    /// <summary>Membro ativo da organização com a conta de <paramref name="gitHubId"/> (FR-007, FR-010).</summary>
    public bool AtivaPara(long gitHubId) =>
        string.Equals(Estado, EstadoAtivo, StringComparison.Ordinal) && UsuarioId == gitHubId;
}

/// <summary>Perfil público da conta (<c>GET /user/{id}</c>).</summary>
public sealed record PerfilGitHub(long Id, string Login, string? Nome, string? FotoUrl);
