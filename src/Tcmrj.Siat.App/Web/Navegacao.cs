using System.Globalization;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web;

/// <summary>Rotas e parâmetros de URL das páginas (contracts/rotas-ui.md).</summary>
public static class Navegacao
{
    public const string Home = "/";

    public const string RotaMailQueue = "/consultas/mailqueue";

    public const string RotaAuditLog = "/consultas/auditlog";

    public const string ParametroServidor = "servidor";

    public const string ParametroSmtp = "smtp";

    public const string ParametroTexto = "texto";

    public const string ParametroQuantidade = "qtd";

    public const string ParametroSelecionado = "sel";

    public const string ParametroApp = "app";

    public const string ParametroLogUid = "loguid";

    public const string ParametroDias = "dias";

    public const string ParametroDe = "de";

    public const string ParametroAte = "ate";

    public const int QuantidadePadrao = 50;

    public static string MailQueue(string? servidor = null, int? smtp = null, string? texto = null, int? qtd = null, string? sel = null) =>
        Montar(
            RotaMailQueue,
            (ParametroServidor, servidor),
            (ParametroSmtp, Numero(smtp)),
            (ParametroTexto, texto),
            (ParametroQuantidade, Quantidade(qtd)),
            (ParametroSelecionado, sel));

    /// <summary>
    /// URL do AuditLog. <paramref name="dias"/> no padrão (30) fica fora da URL; com <paramref name="de"/> ou
    /// <paramref name="ate"/>, o período é pelas datas e os dias não entram.
    /// </summary>
    public static string AuditLog(
        string? servidor = null,
        int? app = null,
        string? loguid = null,
        int? qtd = null,
        string? sel = null,
        int? dias = null,
        DateOnly? de = null,
        DateOnly? ate = null,
        bool semApp = false) =>
        Montar(
            RotaAuditLog,
            (ParametroServidor, servidor),
            (ParametroApp, semApp ? FiltroAuditLog.ValorSemAppId : Numero(app)),
            (ParametroLogUid, loguid),
            (ParametroDias, de is null && ate is null && dias is not null and not FiltroAuditLog.DiasPadrao ? Numero(dias) : null),
            (ParametroDe, Data(de)),
            (ParametroAte, Data(ate)),
            (ParametroQuantidade, Quantidade(qtd)),
            (ParametroSelecionado, sel));

    private static string? Data(DateOnly? data) => data?.ToString(FiltroAuditLog.FormatoData, CultureInfo.InvariantCulture);

    private static string? Numero(int? valor) => valor?.ToString(CultureInfo.InvariantCulture);

    private static string? Quantidade(int? qtd) => qtd is null or QuantidadePadrao ? null : Numero(qtd);

    private static string Montar(string rota, params (string Nome, string? Valor)[] parametros)
    {
        var pares = parametros
            .Where(p => !string.IsNullOrEmpty(p.Valor))
            .Select(p => $"{p.Nome}={Uri.EscapeDataString(p.Valor!)}")
            .ToList();

        return pares.Count == 0 ? rota : $"{rota}?{string.Join('&', pares)}";
    }
}
