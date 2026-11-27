namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Paralelismo e prazos do cache do Copilot, lidos da mesma seção <c>Copilot</c> das opções de leitura do Infra (o App
/// não depende do Infra; specs/005-copilot-meu-consumo/contracts/configuracao.md, research.md R8). As faixas são
/// validadas na leitura pelo repositório (fora delas, <c>NaoConfigurado</c> antes de qualquer chamada).
/// </summary>
public sealed class CacheDoCopilotOptions
{
    public const string Secao = "Copilot";

    /// <summary>Organização do GitHub (links dos times).</summary>
    public string Organizacao { get; set; } = string.Empty;

    /// <summary>Relatórios diários lidos ao mesmo tempo (1 a 8).</summary>
    public int LeiturasSimultaneas { get; set; } = 4;

    public PrazosDoCopilotOptions Cache { get; set; } = new();
}

/// <summary>Prazos de cada tipo de dado em memória (research.md R8).</summary>
public sealed class PrazosDoCopilotOptions
{
    public int MinutosLicencasETimes { get; set; } = 5;

    public int SegundosBudgets { get; set; } = 30;

    public int HorasPerfil { get; set; } = 6;

    public int MinutosRelatoriosRecentes { get; set; } = 10;

    public int HorasRelatoriosFechados { get; set; } = 24;
}
