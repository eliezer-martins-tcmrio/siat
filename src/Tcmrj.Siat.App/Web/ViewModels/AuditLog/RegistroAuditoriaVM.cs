using System.Globalization;
using System.Text.RegularExpressions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.ViewModels.AuditLog;

/// <summary>Registro de <c>audit.Log</c> exibido na lista do AuditLog.</summary>
public sealed partial record RegistroAuditoriaVM(
    long LogId,
    byte TipoId,
    string Tipo,
    DateTime Data,
    string? Mensagem,
    string? LogUid,
    int? AppId,
    string? Excecao,
    string? LogEvent)
{
    /// <summary>A coluna Mensagem mostra até 300 caracteres, seguidos de "…".</summary>
    public const int TamanhoResumo = 300;

    public const string SemMensagem = "(sem mensagem)";

    /// <summary>Chave da linha na lista e valor de <c>sel</c> na URL.</summary>
    public string Chave => LogId.ToString(CultureInfo.InvariantCulture);

    public bool TemExcecao => !string.IsNullOrEmpty(Excecao);

    public const string SemLogUid = "(sem LogUID)";

    /// <summary>Tamanho do LogUID na lista (o primeiro bloco do GUID); o completo fica no detalhe.</summary>
    public const int TamanhoLogUidCurto = 8;

    /// <summary>Os 8 primeiros caracteres do LogUID, como na coluna da lista; "—" sem LogUID.</summary>
    public string LogUidCurto =>
        string.IsNullOrEmpty(LogUid) ? "—" : LogUid.Length <= TamanhoLogUidCurto ? LogUid : LogUid[..TamanhoLogUidCurto];

    /// <summary>Mensagem cortada em 300 caracteres com "…" (coluna Mensagem).</summary>
    public string ResumoMensagem
    {
        get
        {
            var mensagem = Mensagem ?? string.Empty;
            return mensagem.Length > TamanhoResumo ? string.Concat(mensagem.AsSpan(0, TamanhoResumo), "…") : mensagem;
        }
    }

    /// <summary>Classe CSS do nível pelo nome do tipo (<c>audit.Log_Types</c>): <c>erro</c>, <c>alerta</c> ou vazia.</summary>
    public string ClasseNivel => ClasseDoNivel(Tipo);

    public const string SemAppId = "(sem AppID)";

    /// <summary>"AppID N" ou, quando o registro não tem aplicação, "(sem AppID)".</summary>
    public string RotuloAppId => AppId is int id ? $"AppID {id.ToString(CultureInfo.InvariantCulture)}" : SemAppId;

    /// <summary>Nome da aplicação ou, sem ele, "AppID N" (ou "(sem AppID)").</summary>
    public string DescricaoAplicacao(string? nomeAplicacao) =>
        string.IsNullOrWhiteSpace(nomeAplicacao) ? RotuloAppId : nomeAplicacao;

    /// <summary>Texto procurado pelo filtro local: LogID, LogUID, mensagem, tipo, AppID e nome da aplicação.</summary>
    public string TextoBusca(string? nomeAplicacao) =>
        string.Join(' ', Chave, LogUid, Mensagem, Tipo, AppId?.ToString(CultureInfo.InvariantCulture), nomeAplicacao);

    /// <summary>Nível pelo tipo: erro, fatal, crítico, exceção → erro; aviso, alerta → alerta.</summary>
    public static string ClasseDoNivel(string? tipo)
    {
        if (string.IsNullOrEmpty(tipo))
        {
            return string.Empty;
        }

        if (NivelErro().IsMatch(tipo))
        {
            return "erro";
        }

        return NivelAlerta().IsMatch(tipo) ? "alerta" : string.Empty;
    }

    public static RegistroAuditoriaVM De(RegistroAuditoria registro)
    {
        ArgumentNullException.ThrowIfNull(registro);
        return new RegistroAuditoriaVM(
            registro.LogId,
            registro.TipoId,
            registro.Tipo,
            registro.Data,
            registro.Mensagem,
            registro.LogUid,
            registro.AppId,
            registro.Excecao,
            registro.LogEvent);
    }

    [GeneratedRegex("err|fatal|crit|exce", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NivelErro();

    [GeneratedRegex("warn|aviso|alert", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NivelAlerta();
}
