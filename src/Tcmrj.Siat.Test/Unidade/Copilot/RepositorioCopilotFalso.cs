using System.Collections.Concurrent;
using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Repositório do GitHub em memória: respostas configuráveis por delegate, registro das chamadas ("associacao:ana-m",
/// "perfil:7", "licencas", "budgets", "times", "membros:slug", "relatorio:ana-m:2026-11-27" ou "relatorio:ana-m:2026-11")
/// e contador de relatórios lidos ao mesmo tempo. Nenhuma rede.
/// </summary>
internal sealed class RepositorioCopilotFalso : IGitHubCopilotRepository
{
    public const string Org = "tcmrj";

    private int _emCurso;
    private int _maximoEmCurso;

    public string Organizacao => Org;

    public ConcurrentQueue<string> Chamadas { get; } = new();

    /// <summary>Maior número de relatórios lidos ao mesmo tempo.</summary>
    public int MaximoDeRelatoriosSimultaneos => Volatile.Read(ref _maximoEmCurso);

    /// <summary>Quando definido, a associação espera este sinal antes de responder (pedidos simultâneos).</summary>
    public TaskCompletionSource? PortaoDaAssociacao { get; set; }

    public Func<string, Result<Associacao, Erro>> Associacao { get; set; } =
        login => new Associacao("active", "member", IdDe(login));

    public Func<long, Result<PerfilGitHub, Erro>> Perfil { get; set; } =
        id => new PerfilGitHub(id, LoginDe(id), "Ana Souza", "https://avatars.githubusercontent.com/u/" + id);

    public Func<Result<IReadOnlyList<Licenca>, Erro>> Licencas { get; set; } =
        () => Ok<IReadOnlyList<Licenca>>([new Licenca(7, "ana-m"), new Licenca(8, "bia")]);

    public Func<Result<IReadOnlyList<Budget>, Erro>> Budgets { get; set; } =
        () => Ok<IReadOnlyList<Budget>>([new Budget("org", EscopoDeBudget.Organizacao, 5000, null), new Budget("padrao", EscopoDeBudget.Padrao, 50, null)]);

    public Func<Result<IReadOnlyList<TimeDaOrganizacao>, Erro>> Times { get; set; } =
        () => Ok<IReadOnlyList<TimeDaOrganizacao>>([]);

    public Func<string, Result<IReadOnlyList<MembroDoTime>, Erro>> Membros { get; set; } =
        _ => Ok<IReadOnlyList<MembroDoTime>>([]);

    /// <summary>Relatório (login, ano, mês, dia ou nulo para o mensal); por padrão, um item de US$ 1.00.</summary>
    public Func<string, int, int, int?, CancellationToken, Task<Result<IReadOnlyList<ItemDeConsumo>, Erro>>> Relatorio { get; set; } =
        (_, _, _, _, _) => Task.FromResult(Ok<IReadOnlyList<ItemDeConsumo>>([new ItemDeConsumo("GPT-5", "credits", 25, 1.00m, 0.50m)]));

    public static long IdDe(string login) => login.ToLowerInvariant() switch
    {
        "ana-m" or "ana-nova" => 7,
        "bia" => 8,
        _ => 99,
    };

    public static string LoginDe(long id) => id switch
    {
        7 => "ana-m",
        8 => "bia",
        _ => "outra",
    };

    public static Result<T, Erro> Ok<T>(T valor) => Result.Success<T, Erro>(valor);

    public int Quantas(string prefixo) => Chamadas.Count(c => c.StartsWith(prefixo, StringComparison.Ordinal));

    public async Task<Result<Associacao, Erro>> ObterAssociacaoAsync(string login, CancellationToken cancellationToken)
    {
        Chamadas.Enqueue("associacao:" + login);
        if (PortaoDaAssociacao is { } portao)
        {
            await portao.Task.WaitAsync(cancellationToken);
        }

        return Associacao(login);
    }

    public Task<Result<PerfilGitHub, Erro>> ObterPerfilAsync(long id, CancellationToken cancellationToken)
    {
        Chamadas.Enqueue("perfil:" + id);
        return Task.FromResult(Perfil(id));
    }

    public Task<Result<IReadOnlyList<Licenca>, Erro>> ListarLicencasAsync(CancellationToken cancellationToken)
    {
        Chamadas.Enqueue("licencas");
        return Task.FromResult(Licencas());
    }

    public Task<Result<IReadOnlyList<Budget>, Erro>> ListarBudgetsAsync(CancellationToken cancellationToken)
    {
        Chamadas.Enqueue("budgets");
        return Task.FromResult(Budgets());
    }

    public Task<Result<IReadOnlyList<TimeDaOrganizacao>, Erro>> ListarTimesAsync(CancellationToken cancellationToken)
    {
        Chamadas.Enqueue("times");
        return Task.FromResult(Times());
    }

    public Task<Result<IReadOnlyList<MembroDoTime>, Erro>> ListarMembrosDoTimeAsync(string slug, CancellationToken cancellationToken)
    {
        Chamadas.Enqueue("membros:" + slug);
        return Task.FromResult(Membros(slug));
    }

    public async Task<Result<IReadOnlyList<ItemDeConsumo>, Erro>> ObterRelatorioAsync(
        string login, int ano, int mes, int? dia, CancellationToken cancellationToken)
    {
        Chamadas.Enqueue(dia is { } d ? $"relatorio:{login}:{ano:0000}-{mes:00}-{d:00}" : $"relatorio:{login}:{ano:0000}-{mes:00}");
        var emCurso = Interlocked.Increment(ref _emCurso);
        int maximo;
        while (emCurso > (maximo = Volatile.Read(ref _maximoEmCurso)) && Interlocked.CompareExchange(ref _maximoEmCurso, emCurso, maximo) != maximo)
        {
        }

        try
        {
            return await Relatorio(login, ano, mes, dia, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _emCurso);
        }
    }
}
