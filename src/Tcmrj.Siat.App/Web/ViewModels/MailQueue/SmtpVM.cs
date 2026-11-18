using System.Globalization;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.ViewModels.MailQueue;

/// <summary>SMTP de <c>MailQueue.dbo.SMTPs</c> do servidor escolhido, para o filtro da MailQueue.</summary>
public sealed record SmtpVM(int SmtpId, string Descricao)
{
    /// <summary>Texto da opção no seletor: "Id · Descrição" (como em <c>design/mailqueue.html</c>).</summary>
    public string Rotulo => string.IsNullOrWhiteSpace(Descricao)
        ? SmtpId.ToString(CultureInfo.InvariantCulture)
        : $"{SmtpId.ToString(CultureInfo.InvariantCulture)} · {Descricao}";

    public static SmtpVM De(Smtp smtp)
    {
        ArgumentNullException.ThrowIfNull(smtp);
        return new SmtpVM(smtp.SmtpId, smtp.Descricao);
    }
}

/// <summary>
/// Lista de SMTPs do servidor. Indisponível (sem acesso ao banco MailQueue, servidor fora do ar): vazia e com
/// <see cref="Aviso"/>; a tela continua com "Todos os SMTPs" e a consulta segue possível.
/// </summary>
public sealed record ListaSmtpsVM(IReadOnlyList<SmtpVM> Itens, string? Aviso = null)
{
    public static ListaSmtpsVM Vazia { get; } = new([]);

    public bool Disponivel => Aviso is null;

    /// <summary>Rótulo do SMTP, ou "SMTP N" quando ele não está na lista.</summary>
    public string RotuloDe(int smtpId) =>
        Itens.FirstOrDefault(s => s.SmtpId == smtpId)?.Rotulo ?? $"SMTP {smtpId.ToString(CultureInfo.InvariantCulture)}";
}
