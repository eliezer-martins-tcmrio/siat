using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Leitura do AUDITORIADB (e da lista de aplicações do CSLIB_UserManager) de um servidor da lista fechada.</summary>
public interface IAuditLogRepository
{
    /// <summary>Falha de acesso ao CSLIB_UserManager devolve erro sem afetar a consulta de logs.</summary>
    Task<Result<IReadOnlyList<Aplicacao>, Erro>> ListarAplicacoesAsync(Servidor servidor, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<RegistroAuditoria>, Erro>> PesquisarAsync(
        Servidor servidor, FiltroAuditLog filtro, CancellationToken cancellationToken);
}
