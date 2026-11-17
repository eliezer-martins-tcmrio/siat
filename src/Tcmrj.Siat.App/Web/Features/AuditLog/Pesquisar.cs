using System.Globalization;
using CSharpFunctionalExtensions;
using FluentValidation;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Features.AuditLog;

/// <summary>
/// Consulta do AuditLog (história 2): valida os filtros, autoriza pelo servidor (R4) antes de tocar no banco,
/// consulta e registra a tentativa (contracts/registro-consulta.md), inclusive negada, cancelada ou com erro.
/// </summary>
public static class Pesquisar
{
    /// <summary>
    /// Filtros como vieram da tela ou da URL. <paramref name="AppId"/>, <paramref name="PrefixoLogId"/> e
    /// <paramref name="Quantidade"/> são texto para que a validação devolva a mensagem do campo.
    /// </summary>
    public sealed record Query(string? Servidor, string? AppId, string? PrefixoLogId, string? Quantidade)
        : IRequest<Result<ResultadoConsultaVM<RegistroAuditoriaVM>, Erro>>;

    /// <summary>Filtros gravados no registro da consulta (propriedade <c>Filtros</c> do LogEvent).</summary>
    public sealed record FiltrosRegistrados(int? AppId, string? PrefixoLogId, int Quantidade);

    public sealed class Handler(
        IValidator<Query> validador,
        IAutorizacaoConsultaService autorizacao,
        IServidoresRepository servidores,
        IAuditLogRepository repositorio,
        IRegistroConsultaService registro,
        TimeProvider relogio) : IRequestHandler<Query, Result<ResultadoConsultaVM<RegistroAuditoriaVM>, Erro>>
    {
        public async Task<Result<ResultadoConsultaVM<RegistroAuditoriaVM>, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Filtro inválido não é tentativa de consulta: não autoriza, não consulta e não registra.
            var validacao = await validador.ValidateAsync(request, cancellationToken);
            if (!validacao.IsValid)
            {
                var falha = validacao.Errors[0];
                return new Erro(falha.ErrorCode, falha.ErrorMessage);
            }

            var filtro = ParaFiltro(request);
            if (filtro.IsFailure)
            {
                return filtro.Error;
            }

            var chave = request.Servidor!.Trim();
            var filtros = new FiltrosRegistrados(filtro.Value.AppId, filtro.Value.PrefixoLogId, filtro.Value.Quantidade);

            var autorizado = await autorizacao.AutorizarAsync(Modulo.AuditLog, chave);
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
            Result<IReadOnlyList<RegistroAuditoria>, Erro> consulta;
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

            var itens = consulta.Value.Select(RegistroAuditoriaVM.De).ToArray();
            await Registrar(chave, servidor, filtros, itens.Length, ResultadoRegistro.Sucesso);

            return new ResultadoConsultaVM<RegistroAuditoriaVM>(ServidorVM.De(servidor), itens, relogio.GetUtcNow());
        }

        private Task Registrar(string chave, Servidor? servidor, FiltrosRegistrados filtros, int? itens, ResultadoRegistro resultado) =>
            registro.RegistrarAsync(new RegistroConsulta(
                OperacaoConsulta.Consulta,
                Modulo.AuditLog,
                chave,
                servidor?.Nome,
                servidor?.Ambiente,
                filtros,
                itens,
                resultado));
    }

    /// <summary>Converte os filtros já validados no objeto de valor do domínio (quantidade ajustada a 1..1000).</summary>
    public static Result<FiltroAuditLog, Erro> ParaFiltro(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var quantidade = Quantidade.Interpretar(query.Quantidade);
        if (quantidade.IsFailure)
        {
            return quantidade.Error;
        }

        int? appId = null;
        if (!string.IsNullOrWhiteSpace(query.AppId))
        {
            if (!int.TryParse(query.AppId.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id))
            {
                return new Erro(Validators.PesquisarAuditLogQueryValidator.CodigoAppIdInvalido, Validators.Mensagens.AppIdNaoNumerico);
            }

            appId = id;
        }

        return FiltroAuditLog.Create(appId, query.PrefixoLogId, quantidade.Value);
    }
}
