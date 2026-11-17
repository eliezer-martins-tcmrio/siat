using System.Globalization;
using RegraQuantidade = Tcmrj.Siat.Domain.Models.Quantidade;

namespace Tcmrj.Siat.App.Web.ViewModels.AuditLog;

/// <summary>
/// Filtros do formulário do AuditLog, como digitados ou vindos da URL (<c>servidor</c>, <c>app</c>, <c>logid</c>,
/// <c>qtd</c>). Ficam em texto para que valores inválidos cheguem à validação e voltem com a mensagem do campo.
/// </summary>
public sealed class FiltroAuditLogVM
{
    /// <summary>Chave do servidor na lista fechada.</summary>
    public string? Servidor { get; set; }

    /// <summary>AppID escolhido no seletor ou digitado no campo numérico; vazio = todas as aplicações.</summary>
    public string? AppId { get; set; }

    /// <summary>Início do LogID (só dígitos, até 20); vazio = sem filtro.</summary>
    public string? LogId { get; set; }

    /// <summary>1 a 1000; vazio = 50.</summary>
    public string? Quantidade { get; set; } = RegraQuantidade.Padrao.ToString(CultureInfo.InvariantCulture);

    public bool TemFiltros => !string.IsNullOrWhiteSpace(AppId) || !string.IsNullOrWhiteSpace(LogId);

    /// <summary>AppID numérico, quando informado e válido.</summary>
    public int? AppIdNumerico =>
        int.TryParse(AppId?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

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

    public FiltroAuditLogVM Copiar() => new()
    {
        Servidor = Servidor,
        AppId = AppId,
        LogId = LogId,
        Quantidade = Quantidade,
    };
}
