using System.Globalization;
using Tcmrj.Siat.Domain.Models;
using RegraQuantidade = Tcmrj.Siat.Domain.Models.Quantidade;

namespace Tcmrj.Siat.App.Web.ViewModels.AuditLog;

/// <summary>
/// Filtros do formulário do AuditLog, como digitados ou vindos da URL (<c>servidor</c>, <c>app</c>, <c>loguid</c>,
/// <c>dias</c>, <c>de</c>, <c>ate</c>, <c>qtd</c>). Ficam em texto para que valores inválidos cheguem à validação e
/// voltem com a mensagem do campo.
/// </summary>
public sealed class FiltroAuditLogVM
{
    private static readonly string DiasPadrao = FiltroAuditLog.DiasPadrao.ToString(CultureInfo.InvariantCulture);

    /// <summary>Chave do servidor na lista fechada.</summary>
    public string? Servidor { get; set; }

    /// <summary>
    /// AppID escolhido no seletor ou digitado no campo numérico; vazio = todas as aplicações;
    /// <see cref="FiltroAuditLog.ValorSemAppId"/> = só os registros sem aplicação.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>Início do LogUID (dígitos hexadecimais e hífens, até 36); vazio = sem filtro.</summary>
    public string? LogUid { get; set; }

    /// <summary>Últimos N dias, 1 a 3650; vazio = 30. Ignorado com <see cref="De"/> ou <see cref="Ate"/>.</summary>
    public string? Dias { get; set; } = DiasPadrao;

    /// <summary>Data inicial (yyyy-MM-dd, inclusive); vazia = sem limite inicial.</summary>
    public string? De { get; set; }

    /// <summary>Data final (yyyy-MM-dd, inclusive); vazia = até agora.</summary>
    public string? Ate { get; set; }

    /// <summary>1 a 1000; vazio = 50.</summary>
    public string? Quantidade { get; set; } = RegraQuantidade.Padrao.ToString(CultureInfo.InvariantCulture);

    /// <summary>Período pelas datas (alguma data informada): os dias não valem.</summary>
    public bool PorData => !string.IsNullOrWhiteSpace(De) || !string.IsNullOrWhiteSpace(Ate);

    /// <summary>Filtros que podem ser removidos: aplicação, LogUID e período diferente do padrão.</summary>
    public bool TemFiltros =>
        !string.IsNullOrWhiteSpace(AppId) || !string.IsNullOrWhiteSpace(LogUid) || PorData
        || DiasNumerico is int dias && dias != FiltroAuditLog.DiasPadrao;

    /// <summary>Só os registros sem aplicação.</summary>
    public bool SemAppId => FiltroAuditLog.EhSemAppId(AppId);

    /// <summary>AppID numérico, quando informado e válido.</summary>
    public int? AppIdNumerico =>
        int.TryParse(AppId?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>Dias já ajustados, para a URL; nulos quando não numéricos.</summary>
    public int? DiasNumerico
    {
        get
        {
            var interpretados = FiltroAuditLog.InterpretarDias(Dias);
            return interpretados.IsSuccess ? interpretados.Value : null;
        }
    }

    public DateOnly? DeData => Data(De);

    public DateOnly? AteData => Data(Ate);

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

    /// <summary>Volta o período ao padrão: últimos 30 dias, sem datas.</summary>
    public void PeriodoPadrao()
    {
        Dias = DiasPadrao;
        De = null;
        Ate = null;
    }

    public FiltroAuditLogVM Copiar() => new()
    {
        Servidor = Servidor,
        AppId = AppId,
        LogUid = LogUid,
        Dias = Dias,
        De = De,
        Ate = Ate,
        Quantidade = Quantidade,
    };

    private static DateOnly? Data(string? valor)
    {
        var interpretada = FiltroAuditLog.InterpretarData(valor);
        return interpretada.IsSuccess ? interpretada.Value : null;
    }
}

/// <summary>Texto do período do AuditLog para a tela, a exportação e o estado vazio.</summary>
public static class PeriodoAuditLog
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"últimos 30 dias", "de 01/01/2025 a 31/01/2025", "desde 01/01/2025" ou "até 31/01/2025".</summary>
    public static string Descrever(int? dias, DateOnly? de, DateOnly? ate) => (de, ate) switch
    {
        ({ } inicio, { } fim) => $"de {Data(inicio)} a {Data(fim)}",
        ({ } inicio, null) => $"desde {Data(inicio)}",
        (null, { } fim) => $"até {Data(fim)}",
        _ => (dias ?? FiltroAuditLog.DiasPadrao) == 1
            ? "último dia"
            : $"últimos {(dias ?? FiltroAuditLog.DiasPadrao).ToString(CultureInfo.InvariantCulture)} dias",
    };

    private static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", PtBr);
}
