namespace Tcmrj.Siat.Infra.Configurations;

/// <summary>
/// Seção <c>Copilot</c> (specs/005-copilot-meu-consumo/contracts/configuracao.md; research.md R12). Validada na
/// leitura (<see cref="Validar"/>), nunca na subida: sem ela, só a página do Copilot mostra "não configurado", os
/// esquemas OAuth nem são registrados e o resto do SIAT sobe normalmente (Princípio IV). O token de billing e o
/// segredo da OAuth App ficam no <c>appsettings</c> (constituição 3.2.0, Princípio III) e nunca aparecem em mensagem,
/// log nem exceção registrada.
/// </summary>
public sealed class CopilotOptions
{
    public const string Secao = "Copilot";

    public const int MinimoTempoLimite = 1;

    public const int MaximoTempoLimite = 60;

    public const int MinimoLeituras = 1;

    public const int MaximoLeituras = 8;

    public const int MaximoHorasRelatoriosFechados = 168;

    /// <summary>Organização do GitHub (login da organização).</summary>
    public string Organizacao { get; set; } = string.Empty;

    public GitHubCopilotOptions GitHub { get; set; } = new();

    public OAuthCopilotOptions OAuth { get; set; } = new();

    /// <summary>Relatórios diários lidos ao mesmo tempo (1 a 8; research.md R7).</summary>
    public int LeiturasSimultaneas { get; set; } = 4;

    public CacheCopilotOptions Cache { get; set; } = new();

    /// <summary>Organização sem espaços nas pontas.</summary>
    public string OrganizacaoNormalizada => Organizacao?.Trim() ?? string.Empty;

    /// <summary>A OAuth App do "Conectar ao GitHub" está completa (os esquemas OAuth só existem com ela).</summary>
    public bool OAuthCompleto =>
        OAuth is not null && !string.IsNullOrWhiteSpace(OAuth.ClientId) && !string.IsNullOrWhiteSpace(OAuth.ClientSecret);

    /// <summary>Motivos de invalidez (vazio = válida). Nenhum motivo contém valores configurados, só os nomes.</summary>
    public IReadOnlyList<string> Validar()
    {
        var motivos = new List<string>();
        var gitHub = GitHub ?? new GitHubCopilotOptions();
        var oAuth = OAuth ?? new OAuthCopilotOptions();
        var cache = Cache ?? new CacheCopilotOptions();

        if (string.IsNullOrWhiteSpace(Organizacao))
        {
            motivos.Add($"{Secao}:{nameof(Organizacao)} é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(gitHub.Token))
        {
            motivos.Add($"{Secao}:GitHub:Token é obrigatório (no appsettings).");
        }

        if (gitHub.TempoLimiteSegundos is < MinimoTempoLimite or > MaximoTempoLimite)
        {
            motivos.Add($"{Secao}:GitHub:{nameof(GitHubCopilotOptions.TempoLimiteSegundos)} deve estar entre {MinimoTempoLimite} e {MaximoTempoLimite}.");
        }

        if (string.IsNullOrWhiteSpace(oAuth.ClientId))
        {
            motivos.Add($"{Secao}:OAuth:ClientId é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(oAuth.ClientSecret))
        {
            motivos.Add($"{Secao}:OAuth:ClientSecret é obrigatório (no appsettings).");
        }

        if (LeiturasSimultaneas is < MinimoLeituras or > MaximoLeituras)
        {
            motivos.Add($"{Secao}:{nameof(LeiturasSimultaneas)} deve estar entre {MinimoLeituras} e {MaximoLeituras}.");
        }

        if (cache.MinutosLicencasETimes <= 0)
        {
            motivos.Add($"{Secao}:Cache:{nameof(CacheCopilotOptions.MinutosLicencasETimes)} deve ser maior que zero.");
        }

        if (cache.SegundosBudgets <= 0)
        {
            motivos.Add($"{Secao}:Cache:{nameof(CacheCopilotOptions.SegundosBudgets)} deve ser maior que zero.");
        }

        if (cache.HorasPerfil <= 0)
        {
            motivos.Add($"{Secao}:Cache:{nameof(CacheCopilotOptions.HorasPerfil)} deve ser maior que zero.");
        }

        if (cache.MinutosRelatoriosRecentes <= 0)
        {
            motivos.Add($"{Secao}:Cache:{nameof(CacheCopilotOptions.MinutosRelatoriosRecentes)} deve ser maior que zero.");
        }

        if (cache.HorasRelatoriosFechados is <= 0 or > MaximoHorasRelatoriosFechados)
        {
            motivos.Add($"{Secao}:Cache:{nameof(CacheCopilotOptions.HorasRelatoriosFechados)} deve estar entre 1 e {MaximoHorasRelatoriosFechados}.");
        }

        return motivos;
    }
}

/// <summary>Token de leitura de billing (fine-grained; Administration e Members só leitura) e tempo limite.</summary>
public sealed class GitHubCopilotOptions
{
    /// <summary>Token da organização, só leitura. Fica no <c>appsettings</c>; nunca em mensagem nem log.</summary>
    public string? Token { get; set; }

    /// <summary>Tempo limite de cada chamada, em segundos (1 a 60).</summary>
    public int TempoLimiteSegundos { get; set; } = 15;
}

/// <summary>OAuth App do "Conectar ao GitHub" (contracts/conexao-github.md).</summary>
public sealed class OAuthCopilotOptions
{
    public string? ClientId { get; set; }

    /// <summary>Segredo da OAuth App. Fica no <c>appsettings</c>; nunca em mensagem nem log.</summary>
    public string? ClientSecret { get; set; }
}

/// <summary>Prazos do cache em memória (research.md R8).</summary>
public sealed class CacheCopilotOptions
{
    public int MinutosLicencasETimes { get; set; } = 5;

    public int SegundosBudgets { get; set; } = 30;

    public int HorasPerfil { get; set; } = 6;

    public int MinutosRelatoriosRecentes { get; set; } = 10;

    /// <summary>Relatórios de dias que o GitHub não altera mais (1 a 168 horas).</summary>
    public int HorasRelatoriosFechados { get; set; } = 24;
}
