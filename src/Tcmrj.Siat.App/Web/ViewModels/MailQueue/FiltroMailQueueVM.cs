using System.Globalization;
using RegraQuantidade = Tcmrj.Siat.Domain.Models.Quantidade;

namespace Tcmrj.Siat.App.Web.ViewModels.MailQueue;

/// <summary>
/// Filtros do formulário da MailQueue, como digitados ou vindos da URL (<c>servidor</c>, <c>smtp</c>, <c>texto</c>,
/// <c>qtd</c>). Ficam em texto para que valores inválidos cheguem à validação e voltem com a mensagem do campo.
/// </summary>
public sealed class FiltroMailQueueVM
{
    /// <summary>Chave do servidor na lista fechada.</summary>
    public string? Servidor { get; set; }

    /// <summary>SmtpId escolhido; vazio = todos os SMTPs.</summary>
    public string? Smtp { get; set; }

    /// <summary>Procurado literalmente em assunto, corpo, destinatários e MessageId; vazio = sem filtro.</summary>
    public string? Texto { get; set; }

    /// <summary>1 a 1000; vazio = 50.</summary>
    public string? Quantidade { get; set; } = RegraQuantidade.Padrao.ToString(CultureInfo.InvariantCulture);

    public bool TemFiltros => SmtpNumerico is not null || !string.IsNullOrWhiteSpace(Texto);

    /// <summary>SmtpId numérico, quando informado e válido; senão nulo (todos os SMTPs).</summary>
    public int? SmtpNumerico =>
        int.TryParse(Smtp?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>Texto aparado; nulo quando vazio.</summary>
    public string? TextoAparado => string.IsNullOrWhiteSpace(Texto) ? null : Texto.Trim();

    /// <summary>Quantidade já ajustada, para a URL; nula quando vazia ou não numérica.</summary>
    public int? QuantidadeNumerica
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Quantidade))
            {
                return null;
            }

            var interpretada = RegraQuantidade.Interpretar(Quantidade);
            return interpretada.IsSuccess ? interpretada.Value : null;
        }
    }

    public FiltroMailQueueVM Copiar() => new()
    {
        Servidor = Servidor,
        Smtp = Smtp,
        Texto = Texto,
        Quantidade = Quantidade,
    };
}
