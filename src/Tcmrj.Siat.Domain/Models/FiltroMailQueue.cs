using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models;

/// <summary>Filtros da consulta MailQueue (FR-010, FR-016).</summary>
public sealed record FiltroMailQueue
{
    public const int TamanhoMaximoTexto = 500;

    private FiltroMailQueue(int? smtpId, string? texto, int quantidade)
    {
        SmtpId = smtpId;
        Texto = texto;
        Quantidade = quantidade;
    }

    /// <summary>Nulo = todos os SMTPs.</summary>
    public int? SmtpId { get; }

    /// <summary>Aparado; nulo = sem filtro; até 500 caracteres. Vai ao SQL como padrão LIKE escapado.</summary>
    public string? Texto { get; }

    /// <summary>1 a 1000.</summary>
    public int Quantidade { get; }

    public bool TemFiltros => SmtpId is not null || Texto is not null;

    public static FiltroMailQueue Padrao { get; } = new(null, null, Models.Quantidade.Padrao);

    public static Result<FiltroMailQueue, Erro> Create(int? smtpId, string? texto, int quantidade = Models.Quantidade.Padrao)
    {
        var aparado = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (aparado is not null && aparado.Length > TamanhoMaximoTexto)
        {
            return Erros.Filtros.TextoMuitoLongo(TamanhoMaximoTexto);
        }

        return new FiltroMailQueue(smtpId, aparado, Models.Quantidade.Ajustar(quantidade));
    }
}
