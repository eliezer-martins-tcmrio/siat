using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Leitura do banco MailQueue de um servidor da lista fechada (só SELECT, SQL do Visor).</summary>
public interface IMailQueueRepository
{
    Task<Result<IReadOnlyList<Smtp>, Erro>> ListarSmtpsAsync(Servidor servidor, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<MensagemEnfileirada>, Erro>> PesquisarAsync(
        Servidor servidor, FiltroMailQueue filtro, CancellationToken cancellationToken);
}
