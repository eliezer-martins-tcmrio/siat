using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade.AuditLog;

/// <summary>Repositório do AuditLog em memória: devolve o que o teste configurar e anota as chamadas.</summary>
internal sealed class AuditLogRepositoryFalso(List<string>? sequencia = null) : IAuditLogRepository
{
    public List<(Servidor Servidor, FiltroAuditLog Filtro, CancellationToken Token)> Pesquisas { get; } = [];

    public List<Servidor> ListagensDeAplicacoes { get; } = [];

    public Func<FiltroAuditLog, CancellationToken, Task<Result<IReadOnlyList<RegistroAuditoria>, Erro>>> AoPesquisar { get; set; } =
        (_, _) => Task.FromResult(Result.Success<IReadOnlyList<RegistroAuditoria>, Erro>([]));

    public Func<Task<Result<IReadOnlyList<Aplicacao>, Erro>>> AoListarAplicacoes { get; set; } =
        () => Task.FromResult(Result.Success<IReadOnlyList<Aplicacao>, Erro>([]));

    public Task<Result<IReadOnlyList<Aplicacao>, Erro>> ListarAplicacoesAsync(Servidor servidor, CancellationToken cancellationToken)
    {
        sequencia?.Add("repositorio");
        ListagensDeAplicacoes.Add(servidor);
        return AoListarAplicacoes();
    }

    public Task<Result<IReadOnlyList<RegistroAuditoria>, Erro>> PesquisarAsync(
        Servidor servidor, FiltroAuditLog filtro, CancellationToken cancellationToken)
    {
        sequencia?.Add("repositorio");
        Pesquisas.Add((servidor, filtro, cancellationToken));
        return AoPesquisar(filtro, cancellationToken);
    }
}
