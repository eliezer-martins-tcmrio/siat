using System.Globalization;
using FluentValidation;
using Tcmrj.Siat.App.Web.Features.AuditLog;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Validators;

/// <summary>
/// Filtros da consulta AuditLog (FR-013, contracts/rotas-ui.md): servidor obrigatório; AppID inteiro quando
/// informado; início do LogUID só com dígitos hexadecimais e hífens (1 a 36); quantidade numérica (fora de 1 a 1000 é ajustada depois, não recusada);
/// dias numéricos (fora de 1 a 3650 são ajustados); datas no formato yyyy-MM-dd, a inicial até a final.
/// Filtro inválido não chega a autorizar nem a consultar.
/// </summary>
public sealed class PesquisarAuditLogQueryValidator : AbstractValidator<Pesquisar.Query>
{
    public const string CodigoServidorObrigatorio = "filtros.servidor-obrigatorio";

    public const string CodigoAppIdInvalido = "filtros.appid-invalido";

    public PesquisarAuditLogQueryValidator()
    {
        RuleFor(q => q.Servidor)
            .NotEmpty()
            .WithMessage(Mensagens.ServidorObrigatorio)
            .WithErrorCode(CodigoServidorObrigatorio);

        RuleFor(q => q.AppId)
            .Must(app => FiltroAuditLog.EhSemAppId(app)
                || int.TryParse(app!.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            .When(q => !string.IsNullOrWhiteSpace(q.AppId))
            .WithMessage(Mensagens.AppIdNaoNumerico)
            .WithErrorCode(CodigoAppIdInvalido);

        RuleFor(q => q.PrefixoLogUid)
            .Must(prefixo => FiltroAuditLog.PrefixoValido(prefixo!.Trim()))
            .When(q => !string.IsNullOrWhiteSpace(q.PrefixoLogUid))
            .WithMessage(Mensagens.LogUidInvalido)
            .WithErrorCode(Erros.Filtros.CodigoLogUidInvalido);

        RuleFor(q => q.Dias)
            .Must(dias => FiltroAuditLog.InterpretarDias(dias).IsSuccess)
            .WithMessage(Mensagens.DiasNaoNumerico)
            .WithErrorCode(Erros.Filtros.CodigoDiasNaoNumerico);

        RuleFor(q => q.De)
            .Must(data => FiltroAuditLog.InterpretarData(data).IsSuccess)
            .WithMessage(Mensagens.DataInvalida)
            .WithErrorCode(Erros.Filtros.CodigoDataInvalida);

        RuleFor(q => q.Ate)
            .Must(data => FiltroAuditLog.InterpretarData(data).IsSuccess)
            .WithMessage(Mensagens.DataInvalida)
            .WithErrorCode(Erros.Filtros.CodigoDataInvalida);

        // Período invertido aparece no campo "Até".
        RuleFor(q => q.Ate)
            .Must((q, ate) => !Invertido(q.De, ate))
            .WithMessage(Mensagens.PeriodoInvertido)
            .WithErrorCode(Erros.Filtros.CodigoPeriodoInvertido);

        RuleFor(q => q.Quantidade)
            .Must(quantidade => Quantidade.Interpretar(quantidade).IsSuccess)
            .WithMessage(Mensagens.QuantidadeForaDoPadrao)
            .WithErrorCode(Erros.Filtros.CodigoQuantidadeNaoNumerica);
    }

    private static bool Invertido(string? de, string? ate) =>
        FiltroAuditLog.InterpretarData(de) is { IsSuccess: true, Value: { } inicio }
        && FiltroAuditLog.InterpretarData(ate) is { IsSuccess: true, Value: { } fim }
        && inicio > fim;
}
