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
    /// Filtros como vieram da tela ou da URL, em texto para que a validação devolva a mensagem do campo.
    /// <paramref name="Dias"/> vazio vale 30; com <paramref name="De"/> ou <paramref name="Ate"/> (yyyy-MM-dd), o
    /// período é pelas datas e os dias são ignorados.
    /// </summary>
    public sealed record Query(
        string? Servidor,
        string? AppId,
        string? PrefixoLogUid,
        string? Quantidade,
        string? Dias = null,
        string? De = null,
        string? Ate = null) : IRequest<Result<ResultadoConsultaVM<RegistroAuditoriaVM>, Erro>>;

    /// <summary>
    /// Filtros gravados no registro da consulta (propriedade <c>Filtros</c> do LogEvent). <paramref name="Dias"/> é
    /// nulo quando o período é pelas datas.
    /// </summary>
    public sealed record FiltrosRegistrados(
        int? AppId,
        string? PrefixoLogUid,
        int Quantidade,
        int? Dias = FiltroAuditLog.DiasPadrao,
        DateOnly? De = null,
        DateOnly? Ate = null,
        bool SemAppId = false)
    {
        public static FiltrosRegistrados Do(FiltroAuditLog filtro)
        {
            ArgumentNullException.ThrowIfNull(filtro);
            return new(filtro.AppId, filtro.PrefixoLogUid, filtro.Quantidade, filtro.Dias, filtro.De, filtro.Ate, filtro.SemAppId);
        }
    }

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
            var filtros = FiltrosRegistrados.Do(filtro.Value);

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
        var semAppId = FiltroAuditLog.EhSemAppId(query.AppId);
        if (!semAppId && !string.IsNullOrWhiteSpace(query.AppId))
        {
            if (!int.TryParse(query.AppId.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id))
            {
                return new Erro(Validators.PesquisarAuditLogQueryValidator.CodigoAppIdInvalido, Validators.Mensagens.AppIdNaoNumerico);
            }

            appId = id;
        }

        var dias = FiltroAuditLog.InterpretarDias(query.Dias);
        if (dias.IsFailure)
        {
            return dias.Error;
        }

        var de = FiltroAuditLog.InterpretarData(query.De);
        if (de.IsFailure)
        {
            return de.Error;
        }

        var ate = FiltroAuditLog.InterpretarData(query.Ate);
        if (ate.IsFailure)
        {
            return ate.Error;
        }

        return FiltroAuditLog.Create(appId, query.PrefixoLogUid, quantidade.Value, dias.Value, de.Value, ate.Value, semAppId);
    }
}
