using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.Validators;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Features.MailQueue;

/// <summary>
/// SMTPs de <c>MailQueue.dbo.SMTPs</c> do servidor escolhido, para o filtro da MailQueue. Autoriza pelo servidor antes
/// de consultar. Lista indisponível não é erro da tela: volta vazia com aviso, e a consulta segue possível com
/// "Todos os SMTPs".
/// </summary>
public static class ListarSmtps
{
    public sealed record Query(string Servidor) : IRequest<Result<ListaSmtpsVM, Erro>>;

    public sealed class QueryHandler(
        IAutorizacaoConsultaService autorizacao,
        IMailQueueRepository repositorio) : IRequestHandler<Query, Result<ListaSmtpsVM, Erro>>
    {
        public async Task<Result<ListaSmtpsVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var autorizado = await autorizacao.AutorizarAsync(Modulo.MailQueue, request.Servidor);
            if (autorizado.IsFailure)
            {
                return autorizado.Error;
            }

            Result<IReadOnlyList<Smtp>, Erro> smtps;
            try
            {
                smtps = await repositorio.ListarSmtpsAsync(autorizado.Value, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Erros.Consultas.Cancelada;
            }

            if (smtps.IsSuccess)
            {
                return new ListaSmtpsVM(smtps.Value.Select(SmtpVM.De).ToArray());
            }

            if (smtps.Error.Code == Erros.Consultas.CodigoCancelada || cancellationToken.IsCancellationRequested)
            {
                return Erros.Consultas.Cancelada;
            }

            return new ListaSmtpsVM([], Mensagens.SmtpsIndisponiveis(smtps.Error.Message));
        }
    }
}
