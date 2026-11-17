using FluentValidation;
using Tcmrj.Siat.App.Web.Features.MailQueue;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Validators;

/// <summary>
/// Filtros da consulta MailQueue (FR-010, contracts/rotas-ui.md): servidor obrigatório; texto aparado com até 500
/// caracteres; quantidade numérica (fora de 1 a 1000 é ajustada depois, não recusada). Filtro inválido não chega a
/// autorizar nem a consultar.
/// </summary>
public sealed class PesquisarMailQueueQueryValidator : AbstractValidator<Pesquisar.Query>
{
    public const string CodigoServidorObrigatorio = "filtros.servidor-obrigatorio";

    public PesquisarMailQueueQueryValidator()
    {
        RuleFor(q => q.Servidor)
            .NotEmpty()
            .WithMessage(Mensagens.ServidorObrigatorio)
            .WithErrorCode(CodigoServidorObrigatorio);

        RuleFor(q => q.Texto)
            .Must(texto => texto!.Trim().Length <= FiltroMailQueue.TamanhoMaximoTexto)
            .When(q => !string.IsNullOrWhiteSpace(q.Texto))
            .WithMessage(Mensagens.TextoMailQueueMuitoLongo)
            .WithErrorCode(Erros.Filtros.CodigoTextoMuitoLongo);

        RuleFor(q => q.Quantidade)
            .Must(quantidade => Quantidade.Interpretar(quantidade).IsSuccess)
            .WithMessage(Mensagens.QuantidadeForaDoPadrao)
            .WithErrorCode(Erros.Filtros.CodigoQuantidadeNaoNumerica);
    }
}
