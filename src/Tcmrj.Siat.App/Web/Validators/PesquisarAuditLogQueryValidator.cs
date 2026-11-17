using System.Globalization;
using FluentValidation;
using Tcmrj.Siat.App.Web.Features.AuditLog;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Validators;

/// <summary>
/// Filtros da consulta AuditLog (FR-013, contracts/rotas-ui.md): servidor obrigatório; AppID inteiro quando
/// informado; LogID só com dígitos (1 a 20); quantidade numérica (fora de 1 a 1000 é ajustada depois, não recusada).
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
            .Must(app => int.TryParse(app!.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            .When(q => !string.IsNullOrWhiteSpace(q.AppId))
            .WithMessage(Mensagens.AppIdNaoNumerico)
            .WithErrorCode(CodigoAppIdInvalido);

        RuleFor(q => q.PrefixoLogId)
            .Must(prefixo => FiltroAuditLog.PrefixoValido(prefixo!.Trim()))
            .When(q => !string.IsNullOrWhiteSpace(q.PrefixoLogId))
            .WithMessage(Mensagens.LogIdSoDigitos)
            .WithErrorCode(Erros.Filtros.CodigoLogIdInvalido);

        RuleFor(q => q.Quantidade)
            .Must(quantidade => Quantidade.Interpretar(quantidade).IsSuccess)
            .WithMessage(Mensagens.QuantidadeForaDoPadrao)
            .WithErrorCode(Erros.Filtros.CodigoQuantidadeNaoNumerica);
    }
}
