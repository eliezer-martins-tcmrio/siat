namespace Tcmrj.Siat.Domain.Models;

/// <summary>Mensagem de <c>MailQueue.dbo.Messages</c> com os destinatários agregados.</summary>
public sealed record MensagemEnfileirada
{
    public const string SemDestinatarios = "(sem destinatários)";

    /// <summary><c>CONVERT(varchar(36), MessageId)</c>.</summary>
    public required string MessageId { get; init; }

    /// <summary><c>m.SmtpId</c>: SMTP pelo qual o e-mail sai. Nulo se não informado.</summary>
    public int? SmtpId { get; init; }

    public string? Assunto { get; init; }

    /// <summary>Ordem decrescente.</summary>
    public DateTime DataPostagem { get; init; }

    public DateTime? EnviarApos { get; init; }

    public DateTime? DataEnvio { get; init; }

    /// <summary><c>ISNULL(Sent, 0)</c>.</summary>
    public bool Enviado { get; init; }

    public bool EhHtml { get; init; }

    /// <summary>Exibido só no detalhe, isolado (R10). Nunca vai para o registro de consulta.</summary>
    public string? Corpo { get; init; }

    /// <summary>Destinatários separados por <c>"; "</c>, ou <see cref="SemDestinatarios"/>.</summary>
    public string Destinatarios { get; init; } = SemDestinatarios;
}
