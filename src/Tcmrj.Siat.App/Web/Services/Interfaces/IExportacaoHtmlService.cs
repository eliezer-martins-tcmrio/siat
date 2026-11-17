using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>Filtro usado na consulta, como aparece no cabeçalho do arquivo (por exemplo, "SMTP" e "3 · RH").</summary>
public sealed record FiltroExportado(string Rotulo, string Valor);

/// <summary>
/// Cabeçalho do arquivo exportado ([contracts/exportacao-html.md]): módulo, servidor e ambiente (PRD destacado),
/// filtros usados, data e hora da exportação (hora local do servidor do SIAT), quem exportou e o filtro local
/// aplicado na tela, que vem preenchido no arquivo.
/// </summary>
public sealed record CabecalhoExportacao(
    Modulo Modulo,
    ServidorVM Servidor,
    IReadOnlyList<FiltroExportado> Filtros,
    DateTime ExportadoEm,
    string ExportadoPor,
    string? FiltroLocal);

/// <summary>Arquivo pronto para o navegador baixar.</summary>
public sealed record ArquivoExportado(string Nome, byte[] Conteudo)
{
    public const string TipoConteudo = "text/html; charset=utf-8";
}

/// <summary>
/// Monta o HTML autocontido da exportação (research.md R12, FR-021): CSS e um script mínimo de filtro e expansão
/// embutidos, nenhum recurso externo, todo texto codificado. Não autoriza nem registra: quem faz isso é a feature
/// <c>Exportar</c> de cada módulo.
/// </summary>
public interface IExportacaoHtmlService
{
    /// <summary>E-mails na ordem recebida; cada corpo vai num <c>iframe sandbox=""</c> com a CSP de R10.</summary>
    ArquivoExportado MailQueue(CabecalhoExportacao cabecalho, IReadOnlyList<MensagemVM> mensagens);

    /// <summary>Registros na ordem recebida, com stack trace e LogEvent já formatados.</summary>
    /// <param name="nomesAplicacoes">Nome de cada AppID conhecido (da lista do <c>CSLIB_UserManager</c>); pode ser vazio.</param>
    ArquivoExportado AuditLog(
        CabecalhoExportacao cabecalho,
        IReadOnlyList<RegistroAuditoriaVM> registros,
        IReadOnlyDictionary<int, string> nomesAplicacoes);
}
