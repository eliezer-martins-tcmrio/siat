using System.Globalization;

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

    public const string ParametroLogId = "logid";

    public const int QuantidadePadrao = 50;

    public static string MailQueue(string? servidor = null, int? smtp = null, string? texto = null, int? qtd = null, string? sel = null) =>
        Montar(
            RotaMailQueue,
            (ParametroServidor, servidor),
            (ParametroSmtp, Numero(smtp)),
            (ParametroTexto, texto),
            (ParametroQuantidade, Quantidade(qtd)),
            (ParametroSelecionado, sel));

    public static string AuditLog(string? servidor = null, int? app = null, string? logid = null, int? qtd = null, string? sel = null) =>
        Montar(
            RotaAuditLog,
            (ParametroServidor, servidor),
            (ParametroApp, Numero(app)),
            (ParametroLogId, logid),
            (ParametroQuantidade, Quantidade(qtd)),
            (ParametroSelecionado, sel));

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
