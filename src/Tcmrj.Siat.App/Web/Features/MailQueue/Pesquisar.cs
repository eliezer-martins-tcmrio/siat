using CSharpFunctionalExtensions;
using FluentValidation;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Features.MailQueue;

/// <summary>
/// Consulta da MailQueue (história 1): valida os filtros, autoriza pelo servidor (R4) antes de tocar no banco,
/// consulta e registra a tentativa (contracts/registro-consulta.md), inclusive negada, cancelada ou com erro.
/// </summary>
public static class Pesquisar
{
    /// <summary>
    /// Filtros como vieram da tela ou da URL. <paramref name="Quantidade"/> é texto para que a validação devolva a
    /// mensagem do campo quando não for numérica.
    /// </summary>
    public sealed record Query(string? Servidor, int? SmtpId, string? Texto, string? Quantidade)
        : IRequest<Result<ResultadoConsultaVM<MensagemVM>, Erro>>;

    /// <summary>Filtros gravados no registro da consulta (propriedade <c>Filtros</c> do LogEvent): o texto como digitado (aparado).</summary>
    public sealed record FiltrosRegistrados(int? SmtpId, string? Texto, int Quantidade);

    public sealed class QueryHandler(
        IValidator<Query> validador,
        IAutorizacaoConsultaService autorizacao,
        IServidoresRepository servidores,
        IMailQueueRepository repositorio,
        IRegistroConsultaService registro,
        TimeProvider relogio) : IRequestHandler<Query, Result<ResultadoConsultaVM<MensagemVM>, Erro>>
    {
        public async Task<Result<ResultadoConsultaVM<MensagemVM>, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Filtro inválido não é tentativa de consulta: não autoriza, não consulta e não registra.
            var validacao = await validador.ValidateAsync(request, cancellationToken);
            if (!validacao.IsValid)
            {
                var falha = validacao.Errors[0];
                return new Erro(falha.ErrorCode, falha.ErrorMessage);
            }

            var filtro = MontarFiltro(request);
            if (filtro.IsFailure)
            {
                return filtro.Error;
            }

            var chave = request.Servidor!.Trim();
            var filtros = new FiltrosRegistrados(filtro.Value.SmtpId, filtro.Value.Texto, filtro.Value.Quantidade);

            // Autoriza antes de abrir qualquer conexão: cobre a URL compartilhada para PRD e a permissão retirada.
            var autorizado = await autorizacao.AutorizarAsync(Modulo.MailQueue, chave);
            if (autorizado.IsFailure)
            {
                var resultadoNegado = autorizado.Error.Code == Erros.Consultas.CodigoAcessoNegado
                    ? ResultadoRegistro.AcessoNegado
                    : ResultadoRegistro.Erro;
                // Negado: o servidor existe na lista e o registro leva o nome e o ambiente (PRD negado é o caso a auditar).
                await Registrar(chave, servidores.ObterPorChave(chave), filtros, null, resultadoNegado);
                return autorizado.Error;
            }

            var servidor = autorizado.Value;
            Result<IReadOnlyList<MensagemEnfileirada>, Erro> consulta;
            try
            {
                consulta = await repositorio.PesquisarAsync(servidor, filtro.Value, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                consulta = Erros.Consultas.Cancelada;
            }

            if (consulta.IsFailure)
            {
                var cancelada = consulta.Error.Code == Erros.Consultas.CodigoCancelada || cancellationToken.IsCancellationRequested;
                await Registrar(chave, servidor, filtros, null, cancelada ? ResultadoRegistro.Cancelada : ResultadoRegistro.Erro);
                return cancelada ? Erros.Consultas.Cancelada : consulta.Error;
            }

            var agora = relogio.GetLocalNow().DateTime;
            var itens = consulta.Value.Select(m => MensagemVM.De(m, agora)).ToArray();
            await Registrar(chave, servidor, filtros, itens.Length, ResultadoRegistro.Sucesso);

            return new ResultadoConsultaVM<MensagemVM>(ServidorVM.De(servidor), itens, relogio.GetUtcNow());
        }

        private Task Registrar(string chave, Servidor? servidor, FiltrosRegistrados filtros, int? itens, ResultadoRegistro resultado) =>
            registro.RegistrarAsync(new RegistroConsulta(
                OperacaoConsulta.Consulta,
                Modulo.MailQueue,
                chave,
                servidor?.Nome,
                servidor?.Ambiente,
                filtros,
                itens,
                resultado));
    }

    /// <summary>Converte os filtros já validados no objeto de valor do domínio (texto aparado, quantidade ajustada a 1..1000).</summary>
    public static Result<FiltroMailQueue, Erro> MontarFiltro(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var quantidade = Quantidade.Interpretar(query.Quantidade);
        if (quantidade.IsFailure)
        {
            return quantidade.Error;
        }

        return FiltroMailQueue.Create(query.SmtpId, query.Texto, quantidade.Value);
    }
}
