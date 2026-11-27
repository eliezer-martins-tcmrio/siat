using System.Globalization;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web;

/// <summary>
/// Rotas e parâmetros de URL das páginas (contracts/rotas-ui.md). As URLs são relativas ao <c>&lt;base href&gt;</c>
/// (sem a barra inicial): publicado num diretório virtual (<c>/siat_dsv</c>), o SIAT continua dentro dele. Os
/// <c>@page</c> mantêm a barra, porque o roteador já os resolve a partir do base href.
/// </summary>
public static class Navegacao
{
    public const string Home = "";

    public const string RotaCatalogo = "sistemas/catalogo";

    public const string RotaVersoes = "sistemas/versoes";

    /// <summary>
    /// Ficha do sistema (specs/004-ficha-sistema/research.md R3): embaixo do Catálogo, para o item Catálogo do menu
    /// (<c>NavLinkMatch.Prefix</c>) ficar marcado nela (FR-003).
    /// </summary>
    public const string RotaFicha = "sistemas/catalogo/ficha";

    public const string RotaMailQueue = "consultas/mailqueue";

    public const string RotaAuditLog = "consultas/auditlog";

    /// <summary>
    /// Copilot › Meu consumo (specs/005-copilot-meu-consumo/contracts/rotas-ui.md): explicação ou a própria pessoa, em
    /// <c>copilot/meu-consumo/{login}</c>.
    /// </summary>
    public const string RotaMeuConsumo = "copilot/meu-consumo";

    /// <summary>Endpoint HTTP que inicia o "Conectar ao GitHub" (desafio do esquema OAuth).</summary>
    public const string RotaConectarGitHub = "copilot/github/conectar";

    /// <summary>Endpoint HTTP que apaga a conexão com o GitHub.</summary>
    public const string RotaDesconectarGitHub = "copilot/github/desconectar";

    /// <summary>Retorno do OAuth do GitHub, tratado pelo manipulador do ASP.NET Core.</summary>
    public const string RotaRetornoGitHub = "copilot/github/retorno";

    /// <summary>Resultado da conexão ou desconexão, lido uma vez pela página e retirado do endereço.</summary>
    public const string ParametroConexao = "conexao";

    /// <summary>Endereço local para onde voltar depois de conectar ou desconectar.</summary>
    public const string ParametroRetorno = "retorno";

    /// <summary>Motivo da desconexão: <c>usuario</c> (o padrão) ou <c>nao-membro</c>.</summary>
    public const string ParametroMotivo = "motivo";

    /// <summary>Login da conta recusada na conexão, só para a mensagem.</summary>
    public const string ParametroConta = "conta";

    public const string ParametroBusca = "busca";

    public const string ParametroServidor = "servidor";

    /// <summary>Nome do item do catálogo na URL da ficha.</summary>
    public const string ParametroSistema = "sistema";

    public const string ParametroSmtp = "smtp";

    public const string ParametroTexto = "texto";

    public const string ParametroQuantidade = "qtd";

    public const string ParametroSelecionado = "sel";

    public const string ParametroApp = "app";

    public const string ParametroLogUid = "loguid";

    public const string ParametroDias = "dias";

    public const string ParametroDe = "de";

    public const string ParametroAte = "ate";

    /// <summary>
    /// Filtro por situação das Versões: <c>div</c>, <c>erro</c> ou <c>semficha</c>; ausente é
    /// <see cref="FiltroDeSituacao.Todos"/>.
    /// </summary>
    public const string ParametroFiltro = "filtro";

    public const string ValorFiltroDivergencia = "div";

    public const string ValorFiltroErro = "erro";

    public const string ValorFiltroSemFicha = "semficha";

    public const int QuantidadePadrao = 50;

    /// <summary>URL do catálogo de sistemas; busca vazia ou só com espaços fica fora da URL.</summary>
    public static string Catalogo(string? busca = null) =>
        Montar(RotaCatalogo, (ParametroBusca, string.IsNullOrWhiteSpace(busca) ? null : busca));

    /// <summary>
    /// URL das Versões publicadas (specs/003-versoes-publicadas/contracts/rotas-ui.md). Servidor nulo, busca vazia ou
    /// só com espaços e o filtro <see cref="FiltroDeSituacao.Todos"/> ficam fora da URL.
    /// </summary>
    public static string Versoes(string? servidor = null, string? busca = null, FiltroDeSituacao filtro = FiltroDeSituacao.Todos) =>
        Montar(
            RotaVersoes,
            (ParametroServidor, servidor),
            (ParametroBusca, string.IsNullOrWhiteSpace(busca) ? null : busca),
            (ParametroFiltro, filtro switch
            {
                FiltroDeSituacao.ComDivergencia => ValorFiltroDivergencia,
                FiltroDeSituacao.ComErro => ValorFiltroErro,
                FiltroDeSituacao.SemFicha => ValorFiltroSemFicha,
                _ => null,
            }));

    /// <summary>
    /// Filtro do parâmetro <c>filtro</c>: <c>div</c>, <c>erro</c>, <c>semficha</c> ou, para qualquer outro valor, Todos
    /// (sem aviso).
    /// </summary>
    public static FiltroDeSituacao LerFiltro(string? valor) => valor switch
    {
        ValorFiltroDivergencia => FiltroDeSituacao.ComDivergencia,
        ValorFiltroErro => FiltroDeSituacao.ComErro,
        ValorFiltroSemFicha => FiltroDeSituacao.SemFicha,
        _ => FiltroDeSituacao.Todos,
    };

    /// <summary>
    /// URL da ficha do sistema (specs/004-ficha-sistema/contracts/rotas-ui.md). O nome vai escapado com
    /// <see cref="Uri.EscapeDataString"/>; servidor nulo, vazio ou só com espaços (o padrão) fica fora da URL.
    /// </summary>
    public static string Ficha(string sistema, string? servidor = null)
    {
        ArgumentNullException.ThrowIfNull(sistema);
        return Montar(
            RotaFicha,
            (ParametroSistema, sistema),
            (ParametroServidor, string.IsNullOrWhiteSpace(servidor) ? null : servidor));
    }

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

    /// <summary>
    /// URL do Meu consumo: sem login, <c>copilot/meu-consumo</c>; com login, <c>copilot/meu-consumo/{login}</c>
    /// (escapado). O período vai em
    /// <c>de</c> e <c>ate</c>; nulos, vazios ou só com espaços ficam fora (o período padrão não vai para o endereço).
    /// </summary>
    public static string MeuConsumo(string? login = null, string? de = null, string? ate = null) =>
        Montar(
            string.IsNullOrWhiteSpace(login) ? RotaMeuConsumo : $"{RotaMeuConsumo}/{Uri.EscapeDataString(login)}",
            (ParametroDe, string.IsNullOrWhiteSpace(de) ? null : de),
            (ParametroAte, string.IsNullOrWhiteSpace(ate) ? null : ate));

    /// <summary>Endpoint de conexão com o GitHub, com o endereço local de volta escapado.</summary>
    public static string ConectarGitHub(string retorno) =>
        Montar(RotaConectarGitHub, (ParametroRetorno, retorno));

    /// <summary>Endpoint de desconexão; motivo e retorno nulos ficam fora.</summary>
    public static string DesconectarGitHub(string? motivo = null, string? retorno = null) =>
        Montar(RotaDesconectarGitHub, (ParametroMotivo, motivo), (ParametroRetorno, retorno));

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
