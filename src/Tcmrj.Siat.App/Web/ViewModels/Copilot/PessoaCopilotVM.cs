using System.Globalization;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.ViewModels.Copilot;

/// <summary>Time da pessoa, com o link para a página do time no GitHub.</summary>
/// <param name="Herdado">Membro por um sub-time ("via sub-time"); nulo quando o GitHub não informa.</param>
public sealed record TimeVM(string Nome, string Url, bool? Herdado);

/// <summary>
/// Cabeçalho e limite da pessoa conectada (specs/005-copilot-meu-consumo, FR-013 e FR-015; data-model.md). Não leva
/// budgets, o budget global nem dados de outra conta.
/// </summary>
public sealed record PessoaCopilotVM(
    long GitHubId,
    string Login,
    string? Nome,
    string? FotoUrl,
    bool PerfilDisponivel,
    bool TemLicenca,
    IReadOnlyList<TimeVM>? Times,
    string? ErroDosTimes,
    LimiteVigente? Limite,
    string? ErroDoLimite,
    string Organizacao)
{
    public const string TextoComLicenca = "Licença Copilot ativa";

    public const string TextoSemLicenca = "Sem licença Copilot";

    public const string TextoPerfilIndisponivel = "Perfil do GitHub indisponível.";

    /// <summary>Título da página: o nome público ou, sem ele, o login.</summary>
    public string Titulo => Nome ?? Login;

    public string TextoDaLicenca => TemLicenca ? TextoComLicenca : TextoSemLicenca;

    /// <summary>Valor do limite vigente, quando há um só.</summary>
    public decimal? ValorDoLimite => Limite?.Valor;

    /// <summary>Origem do limite: "Exceção individual", "Padrão da organização", "Múltiplos budgets" ou "Sem limite configurado".</summary>
    public string? OrigemDoLimite => Limite switch
    {
        LimiteVigente.Individual => "Exceção individual",
        LimiteVigente.Padrao => "Padrão da organização",
        LimiteVigente.Ambiguo => "Múltiplos budgets",
        LimiteVigente.Nenhum => "Sem limite configurado",
        _ => null,
    };

    public static PessoaCopilotVM De(PessoaCopilot pessoa, string organizacao)
    {
        ArgumentNullException.ThrowIfNull(pessoa);
        var org = organizacao?.Trim() ?? string.Empty;

        return new PessoaCopilotVM(
            pessoa.GitHubId,
            pessoa.Login,
            pessoa.Nome,
            pessoa.FotoUrl,
            pessoa.PerfilDisponivel,
            pessoa.TemLicenca,
            pessoa.Times.IsSuccess ? [.. pessoa.Times.Value.Select(t => new TimeVM(t.Nome, UrlDoTime(org, t.Slug), t.Herdado))] : null,
            pessoa.Times.IsFailure ? pessoa.Times.Error.Message : null,
            pessoa.Limite.IsSuccess ? pessoa.Limite.Value : null,
            pessoa.Limite.IsFailure ? pessoa.Limite.Error.Message : null,
            org);
    }

    /// <summary><c>https://github.com/orgs/{org}/teams/{slug}</c>, com os dois valores escapados.</summary>
    public static string UrlDoTime(string organizacao, string slug) =>
        string.Create(CultureInfo.InvariantCulture, $"https://github.com/orgs/{Uri.EscapeDataString(organizacao)}/teams/{Uri.EscapeDataString(slug)}");
}
