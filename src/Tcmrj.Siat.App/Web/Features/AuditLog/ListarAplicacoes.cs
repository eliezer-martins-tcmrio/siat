using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.Validators;
using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Features.AuditLog;

/// <summary>
/// Aplicações do <c>CSLIB_UserManager</c> do servidor escolhido, para o filtro do AuditLog. Autoriza pelo servidor
/// antes de consultar. Lista indisponível não é erro da tela: volta vazia com aviso, e a tela pede o AppID num
/// campo numérico (história 2, cenário 2), sem afetar a consulta de logs.
/// </summary>
public static class ListarAplicacoes
{
    public sealed record Query(string Servidor) : IRequest<Result<ListaAplicacoesVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IAuditLogRepository repositorio) : IRequestHandler<Query, Result<ListaAplicacoesVM, Erro>>
    {
        public async Task<Result<ListaAplicacoesVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var autorizado = await autorizacao.AutorizarAsync(Modulo.AuditLog, request.Servidor);
            if (autorizado.IsFailure)
            {
                return autorizado.Error;
            }

            Result<IReadOnlyList<Aplicacao>, Erro> aplicacoes;
            try
            {
                aplicacoes = await repositorio.ListarAplicacoesAsync(autorizado.Value, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Erros.Consultas.Cancelada;
            }

            if (aplicacoes.IsSuccess)
            {
                return new ListaAplicacoesVM(aplicacoes.Value.Select(AplicacaoVM.De).ToArray());
            }

            if (aplicacoes.Error.Code == Erros.Consultas.CodigoCancelada || cancellationToken.IsCancellationRequested)
            {
                return Erros.Consultas.Cancelada;
            }

            return new ListaAplicacoesVM([], Mensagens.AplicacoesIndisponiveis(aplicacoes.Error.Message));
        }
    }
}
