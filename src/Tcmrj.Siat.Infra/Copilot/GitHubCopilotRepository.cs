using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Configurations;

namespace Tcmrj.Siat.Infra.Copilot;

/// <summary>
/// Leituras na API REST do GitHub com o token de leitura de billing (specs/005-copilot-meu-consumo,
/// contracts/github-api.md; research.md R1, R9). Só <c>GET</c> (Princípio II).
/// <list type="bullet">
/// <item>Opções lidas e validadas a cada chamada; inválidas: <see cref="Erros.Copilot.NaoConfigurado"/>, sem chamada.</item>
/// <item>Cabeçalhos do gestao-copilot em cada requisição (<c>User-Agent: SIAT</c>); o <see cref="HttpClient"/> não segue
/// redirecionamentos e tem o tempo limite configurado.</item>
/// <item>Paginação: licenças até <c>total_seats</c> (página vazia antes do total é resposta inesperada), budgets até
/// <c>has_next_page = false</c>, times e membros até uma página com menos de 100; no máximo
/// <see cref="MaximoDePaginas"/> páginas.</item>
/// <item>Erros pela tabela do contrato; o corpo das respostas de erro nunca é lido, mostrado nem registrado, e o token
/// nunca vai para mensagem nem log.</item>
/// </list>
/// </summary>
public sealed partial class GitHubCopilotRepository(
    HttpClient http,
    IOptionsMonitor<CopilotOptions> opcoes,
    TimeProvider relogio,
    ILogger<GitHubCopilotRepository> logger) : IGitHubCopilotRepository
{
    public const string VersaoDaApi = "2026-03-10";

    public const string AgenteDoUsuario = "SIAT";

    public const int ItensPorPagina = 100;

    public const int MaximoDePaginas = 1000;

    private static readonly MediaTypeWithQualityHeaderValue TipoAceito = new("application/vnd.github+json");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
    };

    public string Organizacao => opcoes.CurrentValue.OrganizacaoNormalizada;

    public async Task<Result<Associacao, Erro>> ObterAssociacaoAsync(string login, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);

        var lido = await LerAsync<AssociacaoJson>(
            org => $"orgs/{org}/memberships/{Uri.EscapeDataString(login)}",
            "associação",
            naoEncontrado: org => Erros.Copilot.NaoMembro(login, org),
            cancellationToken);
        if (lido.IsFailure)
        {
            return lido.Error;
        }

        var json = lido.Value;
        return json.State is { } estado && json.User?.Id is { } id
            ? new Associacao(estado, json.Role ?? string.Empty, id)
            : Inesperada("associação");
    }

    public async Task<Result<PerfilGitHub, Erro>> ObterPerfilAsync(long id, CancellationToken cancellationToken)
    {
        var lido = await LerAsync<UsuarioJson>(
            _ => $"user/{id.ToString(CultureInfo.InvariantCulture)}", "perfil", naoEncontrado: null, cancellationToken);
        if (lido.IsFailure)
        {
            return lido.Error;
        }

        var json = lido.Value;
        return json.Id is { } lidoId && !string.IsNullOrWhiteSpace(json.Login)
            ? new PerfilGitHub(lidoId, json.Login, json.Name, json.AvatarUrl)
            : Inesperada("perfil");
    }

    public async Task<Result<IReadOnlyList<Licenca>, Erro>> ListarLicencasAsync(CancellationToken cancellationToken)
    {
        var licencas = new List<Licenca>();
        for (var pagina = 1; pagina <= MaximoDePaginas; pagina++)
        {
            var numero = pagina;
            var lido = await LerAsync<LicencasJson>(
                org => $"orgs/{org}/copilot/billing/seats?per_page={ItensPorPagina}&page={numero.ToString(CultureInfo.InvariantCulture)}",
                "licenças",
                naoEncontrado: null,
                cancellationToken);
            if (lido.IsFailure)
            {
                return lido.Error;
            }

            if (lido.Value.TotalSeats is not { } total || lido.Value.Seats is not { } assentos)
            {
                return Inesperada("licenças");
            }

            foreach (var assento in assentos)
            {
                if (assento?.Assignee?.Id is not { } id)
                {
                    // Licença sem conta (por exemplo, de um time sem membro): não é de ninguém.
                    continue;
                }

                licencas.Add(new Licenca(id, assento.Assignee.Login ?? string.Empty));
            }

            var recebidos = (pagina - 1) * ItensPorPagina + assentos.Count;
            if (recebidos >= total)
            {
                return licencas;
            }

            if (assentos.Count == 0)
            {
                // A página acabou antes do total: a lista estaria incompleta.
                PaginacaoIncompleta(logger, "licenças");
                return Erros.Copilot.RespostaInesperada;
            }
        }

        PaginacaoIncompleta(logger, "licenças");
        return Erros.Copilot.RespostaInesperada;
    }

    public async Task<Result<IReadOnlyList<Budget>, Erro>> ListarBudgetsAsync(CancellationToken cancellationToken)
    {
        var budgets = new List<Budget>();
        for (var pagina = 1; pagina <= MaximoDePaginas; pagina++)
        {
            var numero = pagina;
            var lido = await LerAsync<BudgetsJson>(
                org => $"organizations/{org}/settings/billing/budgets?per_page={ItensPorPagina}&page={numero.ToString(CultureInfo.InvariantCulture)}",
                "budgets",
                naoEncontrado: null,
                cancellationToken);
            if (lido.IsFailure)
            {
                return lido.Error;
            }

            if (lido.Value.Budgets is not { } lista)
            {
                return Inesperada("budgets");
            }

            foreach (var json in lista)
            {
                if (json?.Id is not { } id || json.BudgetScope is not { } escopo || json.BudgetAmount is not { } valor || valor < 0)
                {
                    return Inesperada("budgets");
                }

                var skus = json.BudgetProductSkus ?? (json.BudgetProductSku is { } sku ? [sku] : []);
                if (Budget.Normalizar(id, escopo, valor, skus, json.User, json.BudgetEntityName) is { } budget)
                {
                    budgets.Add(budget);
                }
            }

            if (lido.Value.HasNextPage != true)
            {
                return budgets;
            }
        }

        PaginacaoIncompleta(logger, "budgets");
        return Erros.Copilot.RespostaInesperada;
    }

    public async Task<Result<IReadOnlyList<TimeDaOrganizacao>, Erro>> ListarTimesAsync(CancellationToken cancellationToken)
    {
        var lido = await ListarPaginasAsync<TimeJson>(
            (org, pagina) => $"orgs/{org}/teams?per_page={ItensPorPagina}&page={pagina}", "times", cancellationToken);
        if (lido.IsFailure)
        {
            return lido.Error;
        }

        var times = new List<TimeDaOrganizacao>(lido.Value.Count);
        foreach (var json in lido.Value)
        {
            if (json?.Id is not { } id || string.IsNullOrEmpty(json.Slug))
            {
                return Inesperada("times");
            }

            times.Add(new TimeDaOrganizacao(id, json.Name ?? json.Slug, json.Slug));
        }

        return times;
    }

    public async Task<Result<IReadOnlyList<MembroDoTime>, Erro>> ListarMembrosDoTimeAsync(string slug, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var lido = await ListarPaginasAsync<UsuarioJson>(
            (org, pagina) => $"orgs/{org}/teams/{Uri.EscapeDataString(slug)}/members?per_page={ItensPorPagina}&page={pagina}",
            "membros do time",
            cancellationToken);
        if (lido.IsFailure)
        {
            return lido.Error;
        }

        var membros = new List<MembroDoTime>(lido.Value.Count);
        foreach (var json in lido.Value)
        {
            if (string.IsNullOrEmpty(json?.Login))
            {
                return Inesperada("membros do time");
            }

            membros.Add(new MembroDoTime(json.Login, json.Inherited));
        }

        return membros;
    }

    public async Task<Result<IReadOnlyList<ItemDeConsumo>, Erro>> ObterRelatorioAsync(
        string login, int ano, int mes, int? dia, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);

        var parametros = $"year={Numero(ano)}&month={Numero(mes)}"
            + (dia is { } d ? $"&day={Numero(d)}" : string.Empty)
            + $"&user={Uri.EscapeDataString(login)}";
        var lido = await LerAsync<RelatorioJson>(
            org => $"organizations/{org}/settings/billing/ai_credit/usage?{parametros}",
            "relatório de consumo",
            naoEncontrado: null,
            cancellationToken);
        if (lido.IsFailure)
        {
            return lido.Error;
        }

        if (lido.Value.UsageItems is not { } itens)
        {
            return Inesperada("relatório de consumo");
        }

        var lista = new List<ItemDeConsumo>(itens.Count);
        foreach (var item in itens)
        {
            if (item?.UnitType is not { } unidade
                || item.GrossQuantity is not { } quantidade
                || item.GrossAmount is not { } bruto
                || item.NetAmount is not { } liquido)
            {
                return Inesperada("relatório de consumo");
            }

            lista.Add(new ItemDeConsumo(item.Model, unidade, quantidade, bruto, liquido));
        }

        return lista;
    }

    // Lista paginada que termina numa página com menos de ItensPorPagina itens (times e membros).
    private async Task<Result<IReadOnlyList<T?>, Erro>> ListarPaginasAsync<T>(
        Func<string, string, string> caminho, string operacao, CancellationToken cancellationToken)
    {
        var todos = new List<T?>();
        for (var pagina = 1; pagina <= MaximoDePaginas; pagina++)
        {
            var numero = pagina.ToString(CultureInfo.InvariantCulture);
            var lido = await LerAsync<List<T?>>(org => caminho(org, numero), operacao, naoEncontrado: null, cancellationToken);
            if (lido.IsFailure)
            {
                return lido.Error;
            }

            todos.AddRange(lido.Value);
            if (lido.Value.Count < ItensPorPagina)
            {
                return todos;
            }
        }

        PaginacaoIncompleta(logger, operacao);
        return Erros.Copilot.RespostaInesperada;
    }

    // Uma requisição GET: opções, cabeçalhos, mapeamento de erros e leitura do JSON.
    private async Task<Result<T, Erro>> LerAsync<T>(
        Func<string, string> caminho,
        string operacao,
        Func<string, Erro>? naoEncontrado,
        CancellationToken cancellationToken)
        where T : class
    {
        var configuracao = opcoes.CurrentValue;
        var motivos = configuracao.Validar();
        if (motivos.Count > 0)
        {
            ConfiguracaoInvalida(logger, string.Join(" ", motivos));
            return Erros.Copilot.NaoConfigurado;
        }

        var organizacao = configuracao.OrganizacaoNormalizada;
        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, caminho(Uri.EscapeDataString(organizacao)));
            pedido.Headers.Accept.Add(TipoAceito);
            pedido.Headers.Add("X-GitHub-Api-Version", VersaoDaApi);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracao.GitHub.Token);
            pedido.Headers.UserAgent.Add(new ProductInfoHeaderValue(AgenteDoUsuario, null));

            using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                return ErroDaResposta(resposta, operacao, organizacao, naoEncontrado);
            }

            await using var corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            var valor = await JsonSerializer.DeserializeAsync<T>(corpo, Json, cancellationToken);
            return valor is null ? Inesperada(operacao) : valor;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Erros.Copilot.Cancelada;
        }
        catch (OperationCanceledException)
        {
            // Sem cancelamento pedido, o cancelamento é o tempo limite do HttpClient.
            TempoEsgotado(logger, operacao, configuracao.GitHub.TempoLimiteSegundos);
            return Erros.Copilot.GitHubIndisponivel;
        }
        catch (HttpRequestException ex)
        {
            FalhaDeRede(logger, operacao, ex.HttpRequestError.ToString());
            return Erros.Copilot.GitHubIndisponivel;
        }
        catch (JsonException)
        {
            return Inesperada(operacao);
        }
    }

    private Erro ErroDaResposta(HttpResponseMessage resposta, string operacao, string organizacao, Func<string, Erro>? naoEncontrado)
    {
        var codigo = (int)resposta.StatusCode;
        RespostaComErro(logger, operacao, codigo);

        var limitado = resposta.StatusCode == HttpStatusCode.TooManyRequests
            || (resposta.StatusCode == HttpStatusCode.Forbidden
                && (Cabecalho(resposta, "x-ratelimit-remaining") == "0" || resposta.Headers.Contains("retry-after")));
        if (limitado)
        {
            var agora = relogio.GetUtcNow();
            return Erros.Copilot.LimiteDeRequisicoes(TenteEm(resposta, agora), agora);
        }

        return resposta.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Erros.Copilot.TokenRecusado,
            HttpStatusCode.Forbidden => Erros.Copilot.AcessoRecusadoPeloGitHub,
            HttpStatusCode.NotFound => naoEncontrado?.Invoke(organizacao) ?? Erros.Copilot.NaoEncontrado,
            _ when codigo >= 500 => Erros.Copilot.GitHubIndisponivel,
            _ => Erros.Copilot.RespostaInesperada,
        };
    }

    // retry-after em segundos ou, sem ele, x-ratelimit-reset em segundos desde a época (UTC).
    private static DateTimeOffset? TenteEm(HttpResponseMessage resposta, DateTimeOffset agora)
    {
        if (long.TryParse(Cabecalho(resposta, "retry-after"), NumberStyles.None, CultureInfo.InvariantCulture, out var segundos))
        {
            return agora.AddSeconds(segundos);
        }

        return long.TryParse(Cabecalho(resposta, "x-ratelimit-reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var epoca)
            ? DateTimeOffset.FromUnixTimeSeconds(epoca)
            : null;
    }

    private static string? Cabecalho(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues(nome, out var valores) ? valores.FirstOrDefault()?.Trim() : null;

    private Erro Inesperada(string operacao)
    {
        FormatoInesperado(logger, operacao);
        return Erros.Copilot.RespostaInesperada;
    }

    private static string Numero(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Copilot sem configuração válida: {Motivos}")]
    private static partial void ConfiguracaoInvalida(ILogger logger, string motivos);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O GitHub respondeu {Codigo} à leitura de {Operacao} do Copilot.")]
    private static partial void RespostaComErro(ILogger logger, string operacao, int codigo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O GitHub devolveu {Operacao} do Copilot fora do formato esperado.")]
    private static partial void FormatoInesperado(ILogger logger, string operacao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A paginação de {Operacao} do Copilot terminou antes do total.")]
    private static partial void PaginacaoIncompleta(ILogger logger, string operacao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O GitHub não respondeu em {Segundos} s à leitura de {Operacao} do Copilot.")]
    private static partial void TempoEsgotado(ILogger logger, string operacao, int segundos);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha de rede na leitura de {Operacao} do Copilot: {Motivo}")]
    private static partial void FalhaDeRede(ILogger logger, string operacao, string motivo);

    // ---------- Formato das respostas (só os campos usados; contracts/github-api.md) ----------

    private sealed class AssociacaoJson
    {
        public string? State { get; set; }

        public string? Role { get; set; }

        public UsuarioJson? User { get; set; }
    }

    private sealed class UsuarioJson
    {
        public long? Id { get; set; }

        public string? Login { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        public bool? Inherited { get; set; }
    }

    private sealed class LicencasJson
    {
        [JsonPropertyName("total_seats")]
        public int? TotalSeats { get; set; }

        public List<AssentoJson?>? Seats { get; set; }
    }

    private sealed class AssentoJson
    {
        public UsuarioJson? Assignee { get; set; }
    }

    private sealed class BudgetsJson
    {
        public List<BudgetJson?>? Budgets { get; set; }

        [JsonPropertyName("has_next_page")]
        public bool? HasNextPage { get; set; }
    }

    private sealed class BudgetJson
    {
        public string? Id { get; set; }

        [JsonPropertyName("budget_scope")]
        public string? BudgetScope { get; set; }

        [JsonPropertyName("budget_amount")]
        public decimal? BudgetAmount { get; set; }

        [JsonPropertyName("budget_product_sku")]
        public string? BudgetProductSku { get; set; }

        [JsonPropertyName("budget_product_skus")]
        public List<string>? BudgetProductSkus { get; set; }

        public string? User { get; set; }

        [JsonPropertyName("budget_entity_name")]
        public string? BudgetEntityName { get; set; }
    }

    private sealed class TimeJson
    {
        public long? Id { get; set; }

        public string? Name { get; set; }

        public string? Slug { get; set; }
    }

    private sealed class RelatorioJson
    {
        public List<ItemJson?>? UsageItems { get; set; }
    }

    private sealed class ItemJson
    {
        public string? Model { get; set; }

        public string? UnitType { get; set; }

        public decimal? GrossQuantity { get; set; }

        public decimal? GrossAmount { get; set; }

        public decimal? NetAmount { get; set; }
    }
}
