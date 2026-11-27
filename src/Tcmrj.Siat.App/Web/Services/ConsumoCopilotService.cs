using System.Collections.Concurrent;
using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Singleton com os dados do Copilot em memória (specs/005-copilot-meu-consumo, research.md R4, R5, R7 e R8). Uma
/// <see cref="LeituraEmCache{TChave, TValor}"/> por tipo de dado:
/// <list type="table">
/// <item><term>Licenças, times e membros</term><description>da organização, <c>MinutosLicencasETimes</c>.</description></item>
/// <item><term>Budgets</term><description>da organização, <c>SegundosBudgets</c>.</description></item>
/// <item><term>Perfil</term><description>por conta, <c>HorasPerfil</c>.</description></item>
/// <item><term>Pessoa da carga</term><description>por conexão, 10 s fixos: só para os pedidos da mesma carga
/// compartilharem uma verificação; guarda o resultado inteiro (a pessoa, não membro ou sem licença nem limite).</description></item>
/// <item><term>Relatórios recentes</term><description>os três últimos dias e o mês atual, <c>MinutosRelatoriosRecentes</c>.</description></item>
/// <item><term>Relatórios fechados</term><description>os demais dias, <c>HorasRelatoriosFechados</c>.</description></item>
/// </list>
/// Regras:
/// <list type="bullet">
/// <item>A associação à organização é lida a cada verificação da pessoa, sem cache; 404 relê o perfil sem cache para
/// saber se a conta foi renomeada (FR-010).</item>
/// <item>Período e mês só leem relatórios depois de confirmada a pessoa da carga; com qualquer erro dela, devolvem o
/// mesmo erro sem ler relatório (FR-010, FR-021). Falhas só dos times, do limite ou do perfil não bloqueiam o
/// consumo (FR-032).</item>
/// <item>Toda resposta com falha é erro: o valor anterior que o <see cref="LeituraEmCache{TChave, TValor}"/> devolve
/// junto com a falha nunca é usado (FR-010, FR-020).</item>
/// <item>Período: no máximo <c>LeiturasSimultaneas</c> dias ao mesmo tempo; a primeira falha cancela os demais
/// (R7). Licenças, budgets e times são da organização e só saem daqui filtrados para a conta (FR-015, FR-029).</item>
/// </list>
/// O repositório (com <see cref="HttpClient"/> tipado) é resolvido num escopo próprio de cada leitura.
/// </summary>
public sealed class ConsumoCopilotService : IConsumoCopilotService
{
    /// <summary>Validade da pessoa da carga (fixa, não configurável; research.md R4).</summary>
    public static readonly TimeSpan ValidadeDaPessoaDaCarga = TimeSpan.FromSeconds(10);

    /// <summary>Membros de times lidos ao mesmo tempo (como o <c>teamsForUsers</c> do gestao-copilot).</summary>
    public const int LeiturasDeTimesSimultaneas = 4;

    private static readonly CompareInfo ComparacaoPtBr = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    private readonly IServiceScopeFactory escopos;
    private readonly IOptionsMonitor<CacheDoCopilotOptions> opcoes;
    private readonly TimeProvider relogio;

    // "Atualizar agora" pedido para a conta: a próxima verificação da pessoa relê também perfil, licenças, budgets e times.
    private readonly ConcurrentDictionary<long, bool> atualizacoesPendentes = new();

    private readonly LeituraEmCache<Unica, Lido<IReadOnlyList<Licenca>>> licencas;
    private readonly LeituraEmCache<Unica, Lido<IReadOnlyList<Budget>>> budgets;
    private readonly LeituraEmCache<Unica, Lido<IReadOnlyList<TimeComMembros>>> times;
    private readonly LeituraEmCache<long, Lido<PerfilGitHub>> perfis;
    private readonly LeituraEmCache<ConexaoGitHub, Lido<Result<PessoaCopilot, Erro>>> pessoas;
    private readonly LeituraEmCache<ChaveDeRelatorio, RelatorioDeConsumo> recentes;
    private readonly LeituraEmCache<ChaveDeRelatorio, RelatorioDeConsumo> fechados;

    public ConsumoCopilotService(
        IServiceScopeFactory escopos,
        IOptionsMonitor<CacheDoCopilotOptions> opcoes,
        TimeProvider relogio,
        ILogger<ConsumoCopilotService> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);

        this.escopos = escopos;
        this.opcoes = opcoes;
        this.relogio = relogio;

        licencas = Cache<Unica, Lido<IReadOnlyList<Licenca>>>(
            (_, ct) => LerDaOrganizacaoAsync(r => r.ListarLicencasAsync(ct)), l => l.Em, () => TimeSpan.FromMinutes(Prazos.MinutosLicencasETimes), logger);
        budgets = Cache<Unica, Lido<IReadOnlyList<Budget>>>(
            (_, ct) => LerDaOrganizacaoAsync(r => r.ListarBudgetsAsync(ct)), l => l.Em, () => TimeSpan.FromSeconds(Prazos.SegundosBudgets), logger);
        times = Cache<Unica, Lido<IReadOnlyList<TimeComMembros>>>(
            (_, ct) => LerTimesAsync(ct), l => l.Em, () => TimeSpan.FromMinutes(Prazos.MinutosLicencasETimes), logger);
        perfis = Cache<long, Lido<PerfilGitHub>>(
            (id, ct) => LerDaOrganizacaoAsync(r => r.ObterPerfilAsync(id, ct)), l => l.Em, () => TimeSpan.FromHours(Prazos.HorasPerfil), logger);
        pessoas = Cache<ConexaoGitHub, Lido<Result<PessoaCopilot, Erro>>>(
            LerPessoaAsync, l => l.Em, () => ValidadeDaPessoaDaCarga, logger);
        recentes = Cache<ChaveDeRelatorio, RelatorioDeConsumo>(
            LerRelatorioAsync, r => r.LidoEm, () => TimeSpan.FromMinutes(Prazos.MinutosRelatoriosRecentes), logger);
        fechados = Cache<ChaveDeRelatorio, RelatorioDeConsumo>(
            LerRelatorioAsync, r => r.LidoEm, () => TimeSpan.FromHours(Prazos.HorasRelatoriosFechados), logger);
    }

    private PrazosDoCopilotOptions Prazos => opcoes.CurrentValue.Cache ?? new PrazosDoCopilotOptions();

    private int LeiturasSimultaneas => Math.Clamp(opcoes.CurrentValue.LeiturasSimultaneas, 1, 8);

    public async Task<Result<PessoaCopilot, Erro>> ObterPessoaAsync(ConexaoGitHub conexao, bool atualizar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conexao);
        return await PessoaDaCargaAsync(conexao, atualizar, cancellationToken);
    }

    public async Task<Result<ConsumoDoPeriodo, Erro>> ObterPeriodoAsync(
        ConexaoGitHub conexao, Periodo periodo, bool atualizar, IProgress<int>? progresso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conexao);
        ArgumentNullException.ThrowIfNull(periodo);

        // A pessoa vem antes dos relatórios: sem ela confirmada, nenhum relatório é lido (R4). No "Atualizar agora", os
        // três pedidos simultâneos (pessoa, período e mês) compartilham a mesma releitura da pessoa.
        var pessoa = await PessoaDaCargaAsync(conexao, atualizar, cancellationToken);
        if (pessoa.IsFailure)
        {
            return pessoa.Error;
        }

        var login = pessoa.Value.Login;
        var hoje = Hoje();
        var dias = periodo.Dias;
        var lidos = new RelatorioDeConsumo[dias.Count];
        var trava = new Lock();
        Erro? primeiroErro = null;
        var concluidos = 0;

        using var interrupcao = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, dias.Count),
                new ParallelOptions { MaxDegreeOfParallelism = LeiturasSimultaneas, CancellationToken = interrupcao.Token },
                async (indice, token) =>
                {
                    var dia = dias[indice];
                    var chave = new ChaveDeRelatorio(conexao.GitHubId, login, dia.Year, dia.Month, dia.Day);
                    var recente = Periodo.EhRecente(dia, hoje);
                    var obtido = Valor(await (recente ? recentes : fechados).ObterAsync(chave, atualizar && recente, token));
                    if (obtido.IsFailure)
                    {
                        lock (trava)
                        {
                            primeiroErro ??= obtido.Error;
                        }

                        // A primeira falha (inclusive o limite de requisições) interrompe os dias restantes (R7).
                        await interrupcao.CancelAsync();
                        return;
                    }

                    lidos[indice] = obtido.Value;
                    progresso?.Report(Interlocked.Increment(ref concluidos));
                });
        }
        catch (OperationCanceledException)
        {
            // Interrompido pela primeira falha ou pelo pedido; tratado abaixo.
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Erros.Copilot.Cancelada;
        }

        if (primeiroErro is { } erro)
        {
            return erro;
        }

        var consultadoEm = lidos.Length == 0 ? relogio.GetUtcNow() : lidos.Min(r => r.LidoEm);
        return ConsumoDoPeriodo.De(periodo, [.. dias.Select((data, i) => new DiaDeConsumo(data, lidos[i]))], consultadoEm);
    }

    public async Task<Result<RelatorioDeConsumo, Erro>> ObterMesAtualAsync(ConexaoGitHub conexao, bool atualizar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conexao);

        var pessoa = await PessoaDaCargaAsync(conexao, atualizar, cancellationToken);
        if (pessoa.IsFailure)
        {
            return pessoa.Error;
        }

        var hoje = Hoje();
        var chave = new ChaveDeRelatorio(conexao.GitHubId, pessoa.Value.Login, hoje.Year, hoje.Month, null);
        return Valor(await recentes.ObterAsync(chave, atualizar, cancellationToken));
    }

    // ---------- Pessoa da carga ----------

    // "Atualizar" relê a pessoa da carga (associação incluída) e marca a conta para a leitura reler também perfil,
    // licenças, budgets e times; a marca é consumida pela leitura que começar (research.md R8).
    private async Task<Result<PessoaCopilot, Erro>> PessoaDaCargaAsync(ConexaoGitHub conexao, bool atualizar, CancellationToken cancellationToken)
    {
        if (atualizar)
        {
            atualizacoesPendentes[conexao.GitHubId] = true;
        }

        try
        {
            var obtido = Valor(await pessoas.ObterAsync(conexao, atualizar, cancellationToken));
            return obtido.IsFailure ? obtido.Error : obtido.Value.Valor;
        }
        finally
        {
            if (atualizar)
            {
                atualizacoesPendentes.TryRemove(conexao.GitHubId, out _);
            }
        }
    }

    // Verificação inteira da pessoa: sucesso guarda a pessoa ou a resposta "não membro"/"sem licença nem limite";
    // falha é só a falha do GitHub (que nunca traz de volta a pessoa anterior).
    private async Task<Result<Lido<Result<PessoaCopilot, Erro>>, Erro>> LerPessoaAsync(ConexaoGitHub conexao, CancellationToken cancellationToken)
    {
        var atualizar = atualizacoesPendentes.TryRemove(conexao.GitHubId, out _);
        await using var escopo = escopos.CreateAsyncScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IGitHubCopilotRepository>();
        var organizacao = repositorio.Organizacao;

        var verificada = await VerificarAssociacaoAsync(repositorio, conexao, cancellationToken);
        if (verificada.IsFailure)
        {
            return verificada.Error;
        }

        if (verificada.Value.Resposta is { } naoMembro)
        {
            return Guardar(Result.Failure<PessoaCopilot, Erro>(naoMembro));
        }

        var perfilLido = verificada.Value.Perfil;
        var perfilTarefa = perfilLido is null
            ? Valor(perfis.ObterAsync(conexao.GitHubId, atualizar, cancellationToken))
            : Task.FromResult(Result.Success<Lido<PerfilGitHub>, Erro>(new Lido<PerfilGitHub>(perfilLido, relogio.GetUtcNow())));
        var licencasTarefa = Valor(licencas.ObterAsync(Unica.Organizacao, atualizar, cancellationToken));
        var budgetsTarefa = Valor(budgets.ObterAsync(Unica.Organizacao, atualizar, cancellationToken));
        var timesTarefa = Valor(times.ObterAsync(Unica.Organizacao, atualizar, cancellationToken));
        await Task.WhenAll(perfilTarefa, licencasTarefa, budgetsTarefa, timesTarefa);

        if (cancellationToken.IsCancellationRequested)
        {
            return Erros.Copilot.Cancelada;
        }

        var perfil = perfilTarefa.Result;
        var login = perfil.IsSuccess ? perfil.Value.Valor.Login : verificada.Value.Login;

        var lidasLicencas = licencasTarefa.Result;
        if (lidasLicencas.IsFailure)
        {
            return lidasLicencas.Error;
        }

        var temLicenca = lidasLicencas.Value.Valor.Any(l => l.Id == conexao.GitHubId);
        var lidosBudgets = budgetsTarefa.Result;
        if (!temLicenca)
        {
            // Sem licença, a pessoa só existe com budget individual; sem os budgets, não se afirma que ela não existe.
            if (lidosBudgets.IsFailure)
            {
                return lidosBudgets.Error;
            }

            if (!lidosBudgets.Value.Valor.Any(b => b.Escopo == EscopoDeBudget.Individual && MesmoLogin(b.Usuario, login)))
            {
                return Guardar(Result.Failure<PessoaCopilot, Erro>(Erros.Copilot.SemLicencaNemLimite(organizacao)));
            }
        }

        var limite = lidosBudgets.IsSuccess
            ? Result.Success<LimiteVigente, Erro>(LimiteVigente.Calcular(lidosBudgets.Value.Valor, login))
            : Result.Failure<LimiteVigente, Erro>(lidosBudgets.Error);

        var lidosTimes = timesTarefa.Result;
        var timesDaPessoa = lidosTimes.IsSuccess
            ? Result.Success<IReadOnlyList<TimeDaPessoa>, Erro>(TimesDe(lidosTimes.Value.Valor, login))
            : Result.Failure<IReadOnlyList<TimeDaPessoa>, Erro>(lidosTimes.Error);

        var pessoa = perfil.IsSuccess
            ? new PessoaCopilot(conexao.GitHubId, login, true, Nome(perfil.Value.Valor.Nome), Foto(perfil.Value.Valor.FotoUrl), temLicenca, timesDaPessoa, limite)
            : new PessoaCopilot(conexao.GitHubId, login, false, null, null, temLicenca, timesDaPessoa, limite);
        return Guardar(Result.Success<PessoaCopilot, Erro>(pessoa));
    }

    // Associação sempre lida sem cache (FR-010). 404 no login guardado: relê o perfil sem cache; com login novo
    // (conta renomeada), consulta a associação com ele. Devolve a resposta "não membro" quando a conta não é membro
    // ativo com o mesmo id; a falha do perfil nesse caso é a falha da verificação (research.md R4).
    private static async Task<Result<Verificacao, Erro>> VerificarAssociacaoAsync(
        IGitHubCopilotRepository repositorio, ConexaoGitHub conexao, CancellationToken cancellationToken)
    {
        var organizacao = repositorio.Organizacao;
        var associacao = await repositorio.ObterAssociacaoAsync(conexao.Login, cancellationToken);
        if (associacao.IsSuccess)
        {
            return associacao.Value.AtivaPara(conexao.GitHubId)
                ? new Verificacao(conexao.Login, null, null)
                : new Verificacao(conexao.Login, null, Erros.Copilot.NaoMembro(conexao.Login, organizacao));
        }

        if (associacao.Error.Code != Erros.Copilot.CodigoNaoMembro)
        {
            return associacao.Error;
        }

        var perfil = await repositorio.ObterPerfilAsync(conexao.GitHubId, cancellationToken);
        if (perfil.IsFailure)
        {
            return perfil.Error;
        }

        if (perfil.Value.Id != conexao.GitHubId || MesmoLogin(perfil.Value.Login, conexao.Login))
        {
            return new Verificacao(conexao.Login, perfil.Value, Erros.Copilot.NaoMembro(conexao.Login, organizacao));
        }

        var novoLogin = perfil.Value.Login;
        var comNovoLogin = await repositorio.ObterAssociacaoAsync(novoLogin, cancellationToken);
        if (comNovoLogin.IsFailure)
        {
            return comNovoLogin.Error.Code == Erros.Copilot.CodigoNaoMembro
                ? new Verificacao(novoLogin, perfil.Value, Erros.Copilot.NaoMembro(novoLogin, organizacao))
                : comNovoLogin.Error;
        }

        return comNovoLogin.Value.AtivaPara(conexao.GitHubId)
            ? new Verificacao(novoLogin, perfil.Value, null)
            : new Verificacao(novoLogin, perfil.Value, Erros.Copilot.NaoMembro(novoLogin, organizacao));
    }

    private Lido<Result<PessoaCopilot, Erro>> Guardar(Result<PessoaCopilot, Erro> resultado) => new(resultado, relogio.GetUtcNow());

    private static IReadOnlyList<TimeDaPessoa> TimesDe(IReadOnlyList<TimeComMembros> times, string login) =>
    [
        .. times
            .Select(t => (t.Time, Membro: t.Membros.FirstOrDefault(m => MesmoLogin(m.Login, login))))
            .Where(t => t.Membro is not null)
            .Select(t => new TimeDaPessoa(t.Time.Id, t.Time.Nome, t.Time.Slug, t.Membro!.Herdado))
            .OrderBy(t => t.Nome, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: false)),
    ];

    private static string? Nome(string? nome) => string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();

    // Só https: a foto é carregada pelo navegador.
    private static string? Foto(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var endereco) && endereco.Scheme == Uri.UriSchemeHttps ? endereco.AbsoluteUri : null;

    private static bool MesmoLogin(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // ---------- Leituras da organização e relatórios ----------

    private async Task<Result<Lido<T>, Erro>> LerDaOrganizacaoAsync<T>(Func<IGitHubCopilotRepository, Task<Result<T, Erro>>> ler)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var lido = await ler(escopo.ServiceProvider.GetRequiredService<IGitHubCopilotRepository>());
        return lido.IsSuccess ? new Lido<T>(lido.Value, relogio.GetUtcNow()) : lido.Error;
    }

    // Times e membros (4 times por vez); um time sem resposta torna todos indisponíveis: uma varredura incompleta não
    // prova que a pessoa não tem time (research.md R5).
    private async Task<Result<Lido<IReadOnlyList<TimeComMembros>>, Erro>> LerTimesAsync(CancellationToken cancellationToken)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IGitHubCopilotRepository>();
        var lista = await repositorio.ListarTimesAsync(cancellationToken);
        if (lista.IsFailure)
        {
            return lista.Error;
        }

        var times = lista.Value;
        var lidos = new TimeComMembros[times.Count];
        Erro? primeiroErro = null;
        var trava = new Lock();
        using var interrupcao = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, times.Count),
                new ParallelOptions { MaxDegreeOfParallelism = LeiturasDeTimesSimultaneas, CancellationToken = interrupcao.Token },
                async (indice, token) =>
                {
                    var membros = await repositorio.ListarMembrosDoTimeAsync(times[indice].Slug, token);
                    if (membros.IsFailure)
                    {
                        lock (trava)
                        {
                            primeiroErro ??= membros.Error;
                        }

                        await interrupcao.CancelAsync();
                        return;
                    }

                    lidos[indice] = new TimeComMembros(times[indice], membros.Value);
                });
        }
        catch (OperationCanceledException)
        {
            // Interrompido pela primeira falha ou pelo pedido; tratado abaixo.
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Erros.Copilot.Cancelada;
        }

        return primeiroErro is { } erro ? erro : new Lido<IReadOnlyList<TimeComMembros>>(lidos, relogio.GetUtcNow());
    }

    private async Task<Result<RelatorioDeConsumo, Erro>> LerRelatorioAsync(ChaveDeRelatorio chave, CancellationToken cancellationToken)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IGitHubCopilotRepository>();
        var itens = await repositorio.ObterRelatorioAsync(chave.Login, chave.Ano, chave.Mes, chave.Dia, cancellationToken);
        return itens.IsSuccess ? RelatorioDeConsumo.De(itens.Value, relogio.GetUtcNow()) : itens.Error;
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(relogio.GetUtcNow().UtcDateTime);

    private LeituraEmCache<TChave, TValor> Cache<TChave, TValor>(
        Func<TChave, CancellationToken, Task<Result<TValor, Erro>>> ler,
        Func<TValor, DateTimeOffset> horarioDe,
        Func<TimeSpan> validade,
        ILogger logger)
        where TChave : notnull
        where TValor : class =>
        new(ler, horarioDe, validade, Erros.Copilot.Cancelada, _ => Erros.Copilot.GitHubIndisponivel, relogio, logger);

    // Falha com valor anterior é falha: o valor anterior nunca é mostrado (R8).
    private static Result<TValor, Erro> Valor<TValor>(Result<(TValor Valor, FalhaDeLeitura? Falha), Erro> obtido) =>
        obtido.IsFailure ? obtido.Error : obtido.Value.Falha is { } falha ? falha.Erro : obtido.Value.Valor;

    private static async Task<Result<TValor, Erro>> Valor<TValor>(Task<Result<(TValor Valor, FalhaDeLeitura? Falha), Erro>> obtido) =>
        Valor(await obtido);

    /// <summary>Valor lido e quando (base da validade).</summary>
    private sealed record Lido<T>(T Valor, DateTimeOffset Em);

    private sealed record TimeComMembros(TimeDaOrganizacao Time, IReadOnlyList<MembroDoTime> Membros);

    /// <summary>Resultado da verificação de membro: login em uso, perfil já relido (no 404) e a resposta "não membro".</summary>
    private sealed record Verificacao(string Login, PerfilGitHub? Perfil, Erro? Resposta);

    /// <summary>Relatório de uma conta: de um dia (<see cref="Dia"/>) ou o acumulado do mês (sem dia).</summary>
    private readonly record struct ChaveDeRelatorio(long Conta, string Login, int Ano, int Mes, int? Dia);

    /// <summary>Dados da organização têm uma entrada só.</summary>
    private enum Unica
    {
        Organizacao,
    }
}
