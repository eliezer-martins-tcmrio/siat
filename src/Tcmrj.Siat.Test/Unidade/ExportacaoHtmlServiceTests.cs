using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AwesomeAssertions;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>Arquivo exportado ([contracts/exportacao-html.md], R12): autocontido, codificado e com o cabeçalho do contrato.</summary>
public sealed class ExportacaoHtmlServiceTests
{
    private static readonly DateTime ExportadoEm = new(2026, 11, 17, 14, 32, 5);

    private static readonly DateTime Agora = new(2026, 11, 17, 15, 0, 0);

    private static readonly ServidorVM Hml = new("tcmbd4-hml", @"TCMBD4\HML", Ambiente.Hml);

    private static readonly ServidorVM Prd = new("tcmbd-prd", @"TCMBD\PRD", Ambiente.Prd);

    private const string CorpoComScript =
        "<p id=\"seguro\">Conteúdo seguro</p><script>parent.document.title='INVADIDO'</script>" +
        "<img src=\"http://externo.invalid/rastreio.png\"><a href=\"https://externo.invalid\">link</a>";

    private const string StackTrace =
        "System.InvalidOperationException: O modelo não foi encontrado.\r\n" +
        "   at Tcmrj.Etcm.Documentos.GeradorPdf.Gerar(Despacho despacho) in D:\\src\\GeradorPdf.cs:line 84\r\n";

    private readonly ExportacaoHtmlService _servico = new(new FormatadorStackTrace(), new FormatadorLogEvent());

    private static CabecalhoExportacao Cabecalho(Modulo modulo, ServidorVM servidor, string? filtroLocal = "boleto") => new(
        modulo,
        servidor,
        [new("SMTP", "3 · RH"), new("Texto", "\"<b>prazo</b>\""), new("Quantidade", "50")],
        ExportadoEm,
        "Ana Souza (matrícula 12345)",
        filtroLocal);

    private static MensagemVM Mensagem(string id, string assunto, string? corpo = "Corpo simples", bool ehHtml = false) =>
        MensagemVM.De(
            new MensagemEnfileirada
            {
                MessageId = id,
                Assunto = assunto,
                DataPostagem = Agora.AddMinutes(-5),
                EnviarApos = Agora.AddMinutes(-5),
                DataEnvio = Agora.AddMinutes(-4),
                Enviado = true,
                EhHtml = ehHtml,
                Corpo = corpo,
                Destinatarios = "ana@tcm.exemplo; bruno@tcm.exemplo",
            },
            Agora);

    private static RegistroAuditoriaVM Registro(long id, string? excecao = null, string? logEvent = """{"Usuario":"ana","Tentativa":2}""") =>
        new(id, 4, "Error", Agora.AddMinutes(-id % 10), $"Falha {id}", "6f1c2a4e", 12, excecao, logEvent);

    private static (string Html, IHtmlDocument Documento) Ler(ArquivoExportado arquivo)
    {
        var html = Encoding.UTF8.GetString(arquivo.Conteudo);
        return (html, new HtmlParser().ParseDocument(html));
    }

    private (string Html, IHtmlDocument Documento) ExportarMailQueue(params MensagemVM[] mensagens) =>
        Ler(_servico.MailQueue(Cabecalho(Modulo.MailQueue, Hml), mensagens));

    [Fact]
    public void Nome_segue_o_padrao_com_a_hora_local_do_servidor()
    {
        ExportacaoHtmlService.NomeArquivo(Modulo.MailQueue, ExportadoEm).Should().Be("mailqueue_20261117_143205.html");
        ExportacaoHtmlService.NomeArquivo(Modulo.AuditLog, ExportadoEm).Should().Be("auditlog_20261117_143205.html");

        _servico.MailQueue(Cabecalho(Modulo.MailQueue, Hml), [Mensagem("a", "x")]).Nome.Should().Be("mailqueue_20261117_143205.html");
        _servico.AuditLog(Cabecalho(Modulo.AuditLog, Hml), [Registro(1)], new Dictionary<int, string>()).Nome
            .Should().Be("auditlog_20261117_143205.html");
        ArquivoExportado.TipoConteudo.Should().Be("text/html; charset=utf-8");
    }

    [Fact]
    public void Arquivo_e_UTF8_sem_BOM_e_documento_HTML_completo()
    {
        var arquivo = _servico.MailQueue(Cabecalho(Modulo.MailQueue, Hml), [Mensagem("a", "Ação concluída")]);

        arquivo.Conteudo.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
        var (html, documento) = Ler(arquivo);
        html.Should().StartWith("<!DOCTYPE html>");
        documento.DocumentElement.GetAttribute("lang").Should().Be("pt-BR");
        documento.QuerySelector("meta[charset]")!.GetAttribute("charset").Should().Be("utf-8");
        html.Should().Contain("Ação concluída");
    }

    [Fact]
    public void Nenhum_src_ou_href_e_nenhum_recurso_externo()
    {
        var (_, documento) = ExportarMailQueue(Mensagem("a", "Com script", CorpoComScript, ehHtml: true), Mensagem("b", "Texto"));

        documento.All.Where(e => e.HasAttribute("src") || e.HasAttribute("href")).Should().BeEmpty();
        documento.QuerySelectorAll("link, img, object, embed, base, form").Should().BeEmpty();
        documento.Scripts.Should().ContainSingle("só o script mínimo embutido");
        documento.Scripts[0].HasAttribute("src").Should().BeFalse();

        // Fora do srcdoc dos corpos, nenhum atributo aponta para fora.
        documento.All.SelectMany(e => e.Attributes)
            .Where(a => a.Name != "srcdoc")
            .Should().NotContain(a => a.Value.Contains("http", StringComparison.OrdinalIgnoreCase));

        var csp = documento.QuerySelector("meta[http-equiv='Content-Security-Policy']")!.GetAttribute("content");
        csp.Should().Be(ExportacaoHtmlService.CspArquivo);
        csp.Should().Contain("connect-src 'none'").And.Contain("img-src data:").And.Contain("form-action 'none'");
    }

    [Fact]
    public void Cabecalho_tem_modulo_servidor_ambiente_filtros_data_e_quem_exportou()
    {
        var (_, documento) = ExportarMailQueue(Mensagem("a", "x"), Mensagem("b", "y"));

        var cabecalho = Campos(documento);
        cabecalho["Módulo"].Should().Be("MailQueue");
        cabecalho["Servidor"].Should().Be(@"TCMBD4\HML");
        cabecalho["Ambiente"].Should().Be("HML");
        cabecalho["Filtros"].Should().Be("SMTP: 3 · RH · Texto: \"<b>prazo</b>\" · Quantidade: 50");
        cabecalho["Exportado em"].Should().Be("17/11/2026 14:32:05");
        cabecalho["Exportado por"].Should().Be("Ana Souza (matrícula 12345)");
        cabecalho["Itens"].Should().StartWith("2 e-mails");

        documento.QuerySelector("#cabecalho b").Should().BeNull("o filtro é texto, não marcação");
        documento.QuerySelector(".contexto")!.GetAttribute("data-amb").Should().Be("hml");
        documento.Title.Should().Contain("MailQueue").And.Contain(@"TCMBD4\HML");
    }

    [Fact]
    public void PRD_fica_destacado_na_faixa_de_contexto()
    {
        var (html, documento) = Ler(_servico.MailQueue(Cabecalho(Modulo.MailQueue, Prd), [Mensagem("a", "x")]));

        var faixa = documento.QuerySelector(".contexto")!;
        faixa.GetAttribute("data-amb").Should().Be("prd");
        faixa.QuerySelector(".amb")!.TextContent.Should().Be("PRD");
        faixa.TextContent.Should().Contain("Produção");
        html.Should().Contain(".contexto[data-amb=\"prd\"]{background:", "PRD tem fundo sólido, como na tela");
    }

    [Fact]
    public void Filtro_local_vem_preenchido_e_o_script_filtra_e_expande()
    {
        var (html, documento) = ExportarMailQueue(Mensagem("a", "x"));

        var filtro = (IHtmlInputElement)documento.QuerySelector("#filtro")!;
        filtro.Value.Should().Be("boleto");
        filtro.Type.Should().Be("search");
        documento.QuerySelector("#contador")!.GetAttribute("aria-live").Should().Be("polite");
        documento.QuerySelector("#abrir-todos").Should().NotBeNull();
        documento.QuerySelector("#fechar-todos").Should().NotBeNull();
        documento.Body!.GetAttribute("data-unidade").Should().Be("e-mails");
        html.Should().Contain("normalize('NFD')").And.Contain("' de '");
    }

    [Fact]
    public void Filtro_local_vazio_fica_vazio()
    {
        var (_, documento) = Ler(_servico.MailQueue(Cabecalho(Modulo.MailQueue, Hml, filtroLocal: null), [Mensagem("a", "x")]));

        ((IHtmlInputElement)documento.QuerySelector("#filtro")!).Value.Should().BeEmpty();
    }

    [Fact]
    public void MailQueue_itens_na_ordem_recebida_com_detalhe_expansivel()
    {
        var (_, documento) = ExportarMailQueue(Mensagem("c", "Terceiro"), Mensagem("a", "Primeiro"), Mensagem("b", "Segundo"));

        var itens = documento.QuerySelectorAll("#lista > details.item");
        itens.Select(i => i.GetAttribute("data-chave")).Should().Equal("c", "a", "b");
        itens.Should().AllSatisfy(i => i.QuerySelector("summary").Should().NotBeNull());
        itens[0].GetAttribute("data-busca").Should().Contain("Terceiro").And.Contain("ana@tcm.exemplo");
    }

    [Fact]
    public void Corpo_do_email_vai_em_iframe_sandbox_com_a_CSP_de_R10()
    {
        var (_, documento) = ExportarMailQueue(Mensagem("a", "Com script", CorpoComScript, ehHtml: true));

        var iframe = documento.QuerySelector("iframe")!;
        iframe.GetAttribute("sandbox").Should().BeEmpty();
        iframe.HasAttribute("sandbox").Should().BeTrue();
        var srcdoc = iframe.GetAttribute("srcdoc")!;
        srcdoc.Should().StartWith(CorpoEmail.MetaCsp);
        srcdoc.Should().Be(CorpoEmail.MontarSrcdoc(CorpoComScript, ehHtml: true), "o mesmo documento do corpo na tela");
        ExportacaoHtmlService.MetaCspCorpo.Should().Be(CorpoEmail.MetaCsp);

        // O script e a imagem do corpo ficam só dentro do srcdoc, nunca no documento.
        documento.QuerySelector("#seguro").Should().BeNull();
        documento.Scripts.Should().ContainSingle();
    }

    [Fact]
    public void Corpo_de_texto_simples_vai_escapado_em_pre()
    {
        var (_, documento) = ExportarMailQueue(Mensagem("a", "Texto", "Linha 1\n<b>não é negrito</b> & fim", ehHtml: false));

        var srcdoc = documento.QuerySelector("iframe")!.GetAttribute("srcdoc")!;
        srcdoc.Should().StartWith(CorpoEmail.MetaCsp);
        var corpo = new HtmlParser().ParseDocument(srcdoc);
        corpo.QuerySelector("pre")!.TextContent.Should().Be("Linha 1\n<b>não é negrito</b> & fim");
        corpo.QuerySelector("b").Should().BeNull();
    }

    [Fact]
    public void Texto_do_banco_e_codificado_e_nao_vira_marcacao()
    {
        var (_, documento) = ExportarMailQueue(Mensagem("a\"><script>x()</script>", "<img src=x onerror=alert(1)> Assunto"));

        documento.Scripts.Should().ContainSingle();
        documento.QuerySelectorAll("img").Should().BeEmpty();
        var item = documento.QuerySelector("details.item")!;
        item.GetAttribute("data-chave").Should().Be("a\"><script>x()</script>");
        item.QuerySelector("summary")!.TextContent.Should().Contain("<img src=x onerror=alert(1)> Assunto");
    }

    [Fact]
    public void AuditLog_ja_vem_formatado()
    {
        var invalido = Registro(2, logEvent: "não é JSON {");
        var arquivo = _servico.AuditLog(
            Cabecalho(Modulo.AuditLog, Hml),
            [Registro(1, StackTrace), invalido, Registro(3, logEvent: null)],
            new Dictionary<int, string> { [12] = "eTCM" });
        var (_, documento) = Ler(arquivo);

        var itens = documento.QuerySelectorAll("#lista > details.item");
        itens.Select(i => i.GetAttribute("data-chave")).Should().Equal("1", "2", "3");
        documento.Body!.GetAttribute("data-unidade").Should().Be("logs");

        // Stack trace: bloco escuro, uma linha por quebra, tipo da exceção e frame realçados.
        var excecao = itens[0].QuerySelector("pre.codigo.escuro")!;
        excecao.QuerySelector(".st-tipo")!.TextContent.Should().StartWith("System.InvalidOperationException");
        excecao.QuerySelector(".st-metodo")!.TextContent.Should().Be("Gerar");
        excecao.QuerySelector(".st-arquivo").Should().NotBeNull();
        excecao.TextContent.Split('\n').Should().HaveCount(2);

        // LogEvent: JSON indentado e realçado; inválido aparece como veio; sem LogEvent, o aviso.
        var json = itens[0].QuerySelector("pre.codigo:not(.escuro)")!;
        json.TextContent.Should().Be("{\n  \"Usuario\": \"ana\",\n  \"Tentativa\": 2\n}");
        json.QuerySelectorAll(".json-chave").Select(c => c.TextContent).Should().Equal("\"Usuario\"", "\"Tentativa\"");
        json.QuerySelector(".json-numero")!.TextContent.Should().Be("2");
        itens[1].QuerySelector("pre.codigo")!.TextContent.Should().Be("não é JSON {");
        itens[2].QuerySelector("pre.codigo").Should().BeNull();
        itens[2].TextContent.Should().Contain("(sem LogEvent)");

        // Aplicação pelo nome conhecido, e o texto do filtro como na tela.
        itens[0].QuerySelector("summary")!.TextContent.Should().Contain("eTCM").And.Contain("exceção");
        itens[0].GetAttribute("data-busca").Should().Be(Registro(1).TextoBusca("eTCM"));
        documento.QuerySelectorAll("iframe").Should().BeEmpty();
    }

    [Fact]
    public void Autor_descreve_nome_e_matricula()
    {
        var usuario = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new("nome", "Ana Souza"), new("matricula", "12345")], "teste"));

        ExportacaoHtmlService.Autor(usuario).Should().Be("Ana Souza (matrícula 12345)");
        ExportacaoHtmlService.Autor(new System.Security.Claims.ClaimsPrincipal()).Should().Be("matrícula não informada");
    }

    private static Dictionary<string, string> Campos(IDocument documento)
    {
        var termos = documento.QuerySelectorAll("#cabecalho dt");
        return termos.ToDictionary(t => t.TextContent, t => t.NextElementSibling!.TextContent.Trim(), StringComparer.Ordinal);
    }
}
