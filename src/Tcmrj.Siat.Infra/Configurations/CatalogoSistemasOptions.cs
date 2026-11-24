namespace Tcmrj.Siat.Infra.Configurations;

/// <summary>
/// Seção <c>CatalogoSistemas</c> (contracts/configuracao.md). Validada na leitura (<see cref="Validar"/>), nunca na
/// subida: configuração ausente ou inválida faz só a tela de Sistemas mostrar <c>NaoConfigurado</c> (Princípio IV).
/// </summary>
public sealed class CatalogoSistemasOptions
{
    public const string Secao = "CatalogoSistemas";

    public const int Minimo = 1;

    public const int Maximo = 60;

    public GitLabOptions GitLab { get; set; } = new();

    /// <summary>Tempo limite da leitura no GitLab, em segundos (1 a 60).</summary>
    public int TempoLimiteSegundos { get; set; } = 10;

    /// <summary>Validade do catálogo em memória, em minutos (1 a 60).</summary>
    public int MinutosDeCache { get; set; } = 10;

    /// <summary>Motivos de invalidez (vazio = válida). Nenhum motivo contém valores configurados, só os nomes.</summary>
    public IReadOnlyList<string> Validar()
    {
        var motivos = new List<string>();
        var gitLab = GitLab ?? new GitLabOptions();

        if (!Uri.TryCreate(gitLab.BaseUrl, UriKind.Absolute, out var baseUrl)
            || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            motivos.Add($"{Secao}:GitLab:BaseUrl deve ser um endereço absoluto http ou https.");
        }

        if (string.IsNullOrWhiteSpace(gitLab.Projeto))
        {
            motivos.Add($"{Secao}:GitLab:Projeto é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(gitLab.Arquivo))
        {
            motivos.Add($"{Secao}:GitLab:Arquivo é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(gitLab.Ref))
        {
            motivos.Add($"{Secao}:GitLab:Ref é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(gitLab.Token))
        {
            motivos.Add($"{Secao}:GitLab:Token é obrigatório (no appsettings).");
        }

        if (TempoLimiteSegundos is < Minimo or > Maximo)
        {
            motivos.Add($"{Secao}:{nameof(TempoLimiteSegundos)} deve estar entre {Minimo} e {Maximo}.");
        }

        if (MinutosDeCache is < Minimo or > Maximo)
        {
            motivos.Add($"{Secao}:{nameof(MinutosDeCache)} deve estar entre {Minimo} e {Maximo}.");
        }

        return motivos;
    }
}

/// <summary>Onde está o <c>sistemasDb.js</c> no GitLab e o token de projeto (só leitura) para lê-lo.</summary>
public sealed class GitLabOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Caminho do projeto, por exemplo <c>asi/TCMRJDoc</c>.</summary>
    public string Projeto { get; set; } = string.Empty;

    public string Arquivo { get; set; } = string.Empty;

    /// <summary>Branch, tag ou commit.</summary>
    public string Ref { get; set; } = string.Empty;

    /// <summary>Token de acesso do projeto (Reporter, <c>read_repository</c>). Fica no <c>appsettings</c> (constituição, Princípio III); nunca em mensagem nem log.</summary>
    public string? Token { get; set; }

    /// <summary>API de arquivos: <c>{BaseUrl}/api/v4/projects/{Projeto}/repository/files/{Arquivo}/raw?ref={Ref}</c>.</summary>
    public Uri UrlDaApi() => new(
        $"{Base()}/api/v4/projects/{Uri.EscapeDataString(Projeto.Trim())}/repository/files/{Uri.EscapeDataString(Arquivo.Trim())}/raw?ref={Uri.EscapeDataString(Ref.Trim())}");

    /// <summary>O arquivo na interface do GitLab, para o rodapé (FR-022): <c>{BaseUrl}/{Projeto}/-/blob/{Ref}/{Arquivo}</c>.</summary>
    public string UrlDaOrigem() => $"{Base()}/{Projeto.Trim().Trim('/')}/-/blob/{Ref.Trim()}/{Arquivo.Trim()}";

    private string Base() => BaseUrl.Trim().TrimEnd('/');
}
