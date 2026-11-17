using System.Diagnostics;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Test.Interface.Infra;
using static Microsoft.Playwright.Assertions;

namespace Tcmrj.Siat.Test.Interface.Fluxos;

/// <summary>
/// História 4, cenários de aceitação 1 a 5 (quickstart 14 a 16), na MailQueue em memória com repositório lento:
/// tempo decorrido e Cancelar, cancelamento com o resultado anterior, filtro local com "/", exportação baixada e
/// aberta sem o SIAT, e "Remover filtros".
/// </summary>
public sealed class CancelarFiltrarExportarTests : IClassFixture<SiatWebApplicationFactory>
{
    private static readonly DateTime Base = DateTime.Today.AddHours(9);

    private readonly SiatWebApplicationFactory _app;
    private readonly Navegador _navegador;

    public CancelarFiltrarExportarTests(SiatWebApplicationFactory app, Navegador navegador)
    {
        _app = app;
        _navegador = navegador;

        _app.MailQueue.Limpar();
        _app.Registros.Limpar();
        _app.MailQueue.DefinirSmtps(new Smtp(1, "Notificações"), new Smtp(2, "RH"));

        // 20 e-mails: 6 "Notificação" (com acento), 4 "NOTIFICACAO" (sem acento) e 10 "Relatório".
        for (var i = 0; i < 20; i++)
        {
            var assunto = i switch
            {
                < 6 => $"Notificação de prazo {i}",
                < 10 => $"NOTIFICACAO urgente {i}",
                _ => $"Relatório mensal {i}",
            };
            _app.MailQueue.AdicionarMensagem((i % 2) + 1, Mensagem($"5F0B{i:D4}-0000-4000-8000-000000000000", Base.AddMinutes(-i), assunto));
        }
    }

    private static MensagemEnfileirada Mensagem(string id, DateTime postado, string assunto) => new()
    {
        MessageId = id,
        Assunto = assunto,
        DataPostagem = postado,
        EnviarApos = postado,
        DataEnvio = postado.AddSeconds(30),
        Enviado = true,
        EhHtml = true,
        Corpo = $"<p class=\"conteudo\">Corpo de {assunto}</p><img src=\"http://externo.invalid/rastreio.png\" alt=\"\">",
        Destinatarios = "ana.souza@tcm.exemplo",
    };

    private static string Caminho(string consulta = "") =>
        $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}{consulta}";

    private static ILocator Linhas(IPage pagina) => pagina.Locator(".lista-itens button.item-linha");

    private static ILocator Contador(IPage pagina) => pagina.Locator(".painel .barra .contador");

    private async Task<IPage> AbrirComResultadoAsync(string consulta = "")
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, new UsuarioDeTeste("MailQueue.CanSelect"), Caminho(consulta));
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "MailQueue", Level = 1 })).ToBeVisibleAsync();
        return pagina;
    }

    private static Task ConsultarAsync(IPage pagina) =>
        pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true }).ClickAsync();

    private static async Task EsperarAsync(Func<bool> condicao, TimeSpan limite)
    {
        var relogio = Stopwatch.StartNew();
        while (!condicao())
        {
            if (relogio.Elapsed > limite)
            {
                throw new TimeoutException($"Condição não atendida em {limite.TotalSeconds} s.");
            }

            await Task.Delay(25);
        }
    }

    [Fact]
    public async Task Cenario1e2_tempo_e_Cancelar_depois_de_1_s_e_cancelar_mantem_o_resultado_anterior()
    {
        var pagina = await AbrirComResultadoAsync();
        await Expect(Linhas(pagina)).ToHaveCountAsync(20);
        _app.Registros.Limpar();

        _app.MailQueue.AtrasoNaPesquisa = TimeSpan.FromSeconds(30);
        await pagina.Locator("#texto").FillAsync("relatorio");
        var inicio = Stopwatch.StartNew();
        await ConsultarAsync(pagina);

        // Cenário 1: consultando; tempo decorrido e Cancelar só depois do primeiro segundo.
        await Expect(pagina.GetByRole(AriaRole.Status).Filter(new() { HasText = "Consultando" })).ToBeVisibleAsync();
        var cancelar = pagina.GetByRole(AriaRole.Button, new() { Name = "Cancelar", Exact = true });
        await Expect(cancelar).ToBeVisibleAsync();
        inicio.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900), "Cancelar só aparece depois de 1 s");
        await Expect(pagina.Locator("[data-decorrido]")).ToHaveTextAsync(new Regex(@"^\d+,\d s$"));
        await Expect(pagina).ToHaveURLAsync(new Regex("texto=relatorio"));

        // Cenário 2: cancelar interrompe no banco (token cancelado: o repositório devolve Cancelada) em até 2 s.
        var cancelamento = Stopwatch.StartNew();
        await cancelar.ClickAsync();
        await EsperarAsync(() => _app.Registros.Registros.Any(r => r.Resultado == ResultadoRegistro.Cancelada), TimeSpan.FromSeconds(2));
        cancelamento.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));

        await Expect(cancelar).ToHaveCountAsync(0);
        await Expect(Linhas(pagina)).ToHaveCountAsync(20);
        await Expect(pagina.GetByRole(AriaRole.Status).Filter(new() { HasText = "Consulta cancelada após" }))
            .ToContainTextAsync("mostrando o resultado anterior");
        await Expect(pagina).ToHaveURLAsync(new Regex($@"/consultas/mailqueue\?servidor={SiatWebApplicationFactory.ServidorHml}$"));

        var registro = _app.Registros.Registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Consulta);
        registro.Resultado.Should().Be(ResultadoRegistro.Cancelada);
        _app.MailQueue.ChamadasPesquisa.Should().Be(2);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Nova_consulta_cancela_a_anterior_e_so_a_ultima_e_aplicada()
    {
        var pagina = await AbrirComResultadoAsync();
        await Expect(Linhas(pagina)).ToHaveCountAsync(20);
        _app.Registros.Limpar();

        _app.MailQueue.AtrasoNaPesquisa = TimeSpan.FromSeconds(30);
        await pagina.Locator("#texto").FillAsync("urgente");
        await ConsultarAsync(pagina);
        await Expect(pagina.GetByRole(AriaRole.Status).Filter(new() { HasText = "Consultando" })).ToBeVisibleAsync();
        await EsperarAsync(() => _app.MailQueue.ChamadasPesquisa == 2, TimeSpan.FromSeconds(5));

        _app.MailQueue.AtrasoNaPesquisa = TimeSpan.Zero;
        await pagina.Locator("#texto").FillAsync("mensal");
        await ConsultarAsync(pagina);

        await Expect(Linhas(pagina)).ToHaveCountAsync(10);
        await EsperarAsync(() => _app.Registros.Registros.Count == 2, TimeSpan.FromSeconds(5));
        _app.Registros.Registros.Select(r => r.Resultado).Should().BeEquivalentTo([ResultadoRegistro.Cancelada, ResultadoRegistro.Sucesso]);
        await Expect(pagina).ToHaveURLAsync(new Regex("texto=mensal$"));
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario3_barra_foca_o_filtro_que_filtra_so_os_itens_carregados_sem_acentos_com_X_de_Y()
    {
        var pagina = await AbrirComResultadoAsync();
        await Expect(Linhas(pagina)).ToHaveCountAsync(20);
        await Expect(Contador(pagina)).ToHaveTextAsync("20 e-mails");
        var chamadas = _app.MailQueue.ChamadasPesquisa;

        await pagina.Locator("h1").ClickAsync();
        await pagina.Keyboard.PressAsync("/");
        var filtro = pagina.Locator("#filtro-local");
        await Expect(filtro).ToBeFocusedAsync();

        await pagina.Keyboard.TypeAsync("notificacao");
        await Expect(Contador(pagina)).ToHaveTextAsync("10 de 20 e-mails");
        await Expect(Linhas(pagina)).ToHaveCountAsync(10);

        await filtro.FillAsync("NOTIFICAÇÃO prazo");
        await Expect(Contador(pagina)).ToHaveTextAsync("6 de 20 e-mails");

        // Teclado: ↓ do filtro vai à primeira linha, ↓ à segunda, ↑ volta; Enter abre o detalhe.
        await filtro.PressAsync("ArrowDown");
        await Expect(Linhas(pagina).First).ToBeFocusedAsync();
        await pagina.Keyboard.PressAsync("ArrowDown");
        await Expect(Linhas(pagina).Nth(1)).ToBeFocusedAsync();
        await pagina.Keyboard.PressAsync("ArrowUp");
        await Expect(Linhas(pagina).First).ToBeFocusedAsync();
        await pagina.Keyboard.PressAsync("Enter");
        await Expect(Linhas(pagina).First).ToHaveAttributeAsync("aria-expanded", "true");

        // "/" dentro do campo de texto é digitado normalmente; Esc limpa o filtro.
        await filtro.FocusAsync();
        await pagina.Keyboard.PressAsync("Escape");
        await Expect(filtro).ToHaveValueAsync(string.Empty);
        await Expect(Contador(pagina)).ToHaveTextAsync("20 e-mails");
        await Expect(Linhas(pagina)).ToHaveCountAsync(20);

        _app.MailQueue.ChamadasPesquisa.Should().Be(chamadas, "o filtro local não consulta o banco");
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario4_exportar_baixa_HTML_autocontido_que_abre_sem_o_SIAT()
    {
        var pagina = await AbrirComResultadoAsync();
        await Expect(Linhas(pagina)).ToHaveCountAsync(20);
        await pagina.Locator("#filtro-local").FillAsync("notificacao");
        await Expect(Contador(pagina)).ToHaveTextAsync("10 de 20 e-mails");
        _app.Registros.Limpar();
        var chamadas = _app.MailQueue.ChamadasPesquisa;

        var download = await pagina.RunAndWaitForDownloadAsync(
            () => pagina.GetByRole(AriaRole.Button, new() { Name = "Exportar HTML" }).ClickAsync());

        download.SuggestedFilename.Should().MatchRegex(@"^mailqueue_\d{8}_\d{6}\.html$");
        var pasta = Directory.CreateTempSubdirectory("siat-exportacao-");
        try
        {
            var arquivo = Path.Combine(pasta.FullName, download.SuggestedFilename);
            await download.SaveAsAsync(arquivo);
            var html = await File.ReadAllTextAsync(arquivo, TestContext.Current.CancellationToken);
            html.Should().StartWith("<!DOCTYPE html>");
            html.Should().NotContain("<link").And.NotContain("<script src", "CSS e script vão embutidos");

            var registro = _app.Registros.Registros.Should().ContainSingle().Subject;
            registro.Operacao.Should().Be(OperacaoConsulta.Exportacao);
            registro.Modulo.Should().Be(Modulo.MailQueue);
            registro.Resultado.Should().Be(ResultadoRegistro.Sucesso);
            registro.Itens.Should().Be(20);
            _app.MailQueue.ChamadasPesquisa.Should().Be(chamadas, "exporta os itens já carregados");

            // Abre o arquivo num contexto novo, sem o cookie do SIAT, e confere que nada vai para a rede.
            await using var contexto = await _navegador.Browser.NewContextAsync();
            var offline = await contexto.NewPageAsync();
            var requisicoes = new List<string>();
            offline.Request += (_, r) =>
            {
                if (!r.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && !r.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) &&
                    !r.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    requisicoes.Add(r.Url);
                }
            };
            await offline.GotoAsync(new Uri(arquivo).AbsoluteUri);

            (await offline.EvaluateAsync<int>("document.querySelectorAll('[src], [href]').length")).Should().Be(0, "nenhum src ou href no arquivo");
            await Expect(offline.Locator("#cabecalho")).ToContainTextAsync("MailQueue");
            await Expect(offline.Locator("#cabecalho")).ToContainTextAsync(SiatWebApplicationFactory.NomeServidorHml);
            await Expect(offline.Locator("#cabecalho")).ToContainTextAsync("Usuária de Teste (matrícula 12345)");
            await Expect(offline.Locator(".contexto[data-amb='hml']")).ToBeVisibleAsync();

            // O filtro vem preenchido e funciona sem o SIAT.
            var filtro = offline.Locator("#filtro");
            await Expect(filtro).ToHaveValueAsync("notificacao");
            await Expect(offline.Locator("#contador")).ToHaveTextAsync("10 de 20 e-mails");
            await Expect(offline.Locator("details.item:visible")).ToHaveCountAsync(10);
            await filtro.FillAsync("relatorio");
            await Expect(offline.Locator("#contador")).ToHaveTextAsync("10 de 20 e-mails");
            await filtro.PressAsync("Escape");
            await Expect(offline.Locator("#contador")).ToHaveTextAsync("20 e-mails");

            // Detalhe expansível com o corpo isolado em iframe sandbox.
            var primeiro = offline.Locator("details.item").First;
            await primeiro.Locator("summary").ClickAsync();
            await Expect(primeiro).ToHaveAttributeAsync("open", string.Empty);
            var iframe = primeiro.Locator("iframe");
            await Expect(iframe).ToHaveAttributeAsync("sandbox", string.Empty);
            await Expect(primeiro.FrameLocator("iframe").Locator("p.conteudo")).ToHaveTextAsync("Corpo de Notificação de prazo 0");

            await offline.Locator("#abrir-todos").ClickAsync();
            await Expect(offline.Locator("details.item[open]")).ToHaveCountAsync(20);

            requisicoes.Should().BeEmpty("o arquivo não carrega nada de fora nem chama o SIAT");
        }
        finally
        {
            pasta.Delete(recursive: true);
            await pagina.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task Cenario5_lista_vazia_com_filtros_oferece_Remover_filtros()
    {
        var pagina = await AbrirComResultadoAsync("&smtp=2&texto=inexistente");

        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Nenhum e-mail encontrado" })).ToBeVisibleAsync();
        await Expect(pagina.GetByRole(AriaRole.Button, new() { Name = "Exportar HTML" })).ToBeDisabledAsync();
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Remover filtros" }).ClickAsync();

        await Expect(Linhas(pagina)).ToHaveCountAsync(20);
        await Expect(pagina).ToHaveURLAsync(new Regex($@"/consultas/mailqueue\?servidor={SiatWebApplicationFactory.ServidorHml}$"));
        _app.MailQueue.UltimoFiltro!.Texto.Should().BeNull();
        _app.MailQueue.UltimoFiltro.SmtpId.Should().BeNull();

        // Sem filtros, a lista vazia não oferece "Remover filtros".
        _app.MailQueue.Limpar();
        await ConsultarAsync(pagina);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Nenhum e-mail encontrado" })).ToBeVisibleAsync();
        await Expect(pagina.GetByRole(AriaRole.Button, new() { Name = "Remover filtros" })).ToHaveCountAsync(0);
        await pagina.Context.CloseAsync();
    }
}

/// <summary>História 4 no AuditLog: Cancelar e exportação com stack trace e LogEvent já formatados.</summary>
public sealed class CancelarExportarAuditLogTests : IClassFixture<AuditLogWebApplicationFactory>
{
    private const string StackTrace =
        "System.InvalidOperationException: O modelo de documento não foi encontrado.\r\n" +
        "   at Tcmrj.Etcm.Documentos.GeradorPdf.Gerar(Despacho despacho) in D:\\src\\etcm\\GeradorPdf.cs:line 84\r\n";

    private readonly AuditLogWebApplicationFactory _app;
    private readonly Navegador _navegador;

    public CancelarExportarAuditLogTests(AuditLogWebApplicationFactory app, Navegador navegador)
    {
        _app = app;
        _navegador = navegador;

        _app.AuditLog.Limpar();
        _app.Registros.Limpar();
        _app.AuditLog.DefinirAplicacoes(new Aplicacao(12, "eTCM"));
        var baseData = DateTime.Today.AddHours(9);
        for (var i = 0; i < 5; i++)
        {
            _app.AuditLog.AdicionarRegistro(new RegistroAuditoria
            {
                LogId = 436500 - i,
                TipoId = 4,
                Tipo = "Error",
                Data = baseData.AddMinutes(-i),
                Mensagem = $"Falha ao gerar o despacho {i}",
                AppId = 12,
                Excecao = i == 0 ? StackTrace : null,
                LogEvent = """{"Usuario":"ana.souza","DespachoId":18834}""",
            });
        }
    }

    private async Task<IPage> AbrirAsync()
    {
        var pagina = await _app.AbrirAsync(
            _navegador.Browser,
            new UsuarioDeTeste("AuditLog.CanSelect"),
            $"/consultas/auditlog?servidor={SiatWebApplicationFactory.ServidorHml}");
        await Expect(pagina.Locator(".lista-itens button.item-linha")).ToHaveCountAsync(5);
        return pagina;
    }

    [Fact]
    public async Task Cancelar_mantem_o_resultado_anterior_e_registra_Cancelada()
    {
        var pagina = await AbrirAsync();
        _app.Registros.Limpar();
        _app.AuditLog.AtrasoNaPesquisa = TimeSpan.FromSeconds(30);

        await pagina.Locator("#logid").FillAsync("4363");
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true }).ClickAsync();
        var cancelar = pagina.GetByRole(AriaRole.Button, new() { Name = "Cancelar", Exact = true });
        await cancelar.ClickAsync();

        await Expect(cancelar).ToHaveCountAsync(0);
        await Expect(pagina.Locator(".lista-itens button.item-linha")).ToHaveCountAsync(5);
        await Expect(pagina.GetByText("5 logs", new() { Exact = true })).ToBeVisibleAsync();
        var relogio = Stopwatch.StartNew();
        while (_app.Registros.Registros.Count == 0 && relogio.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        _app.Registros.Registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Cancelada);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Exportar_gera_auditlog_com_stack_trace_e_LogEvent_formatados()
    {
        var pagina = await AbrirAsync();
        _app.Registros.Limpar();

        var download = await pagina.RunAndWaitForDownloadAsync(
            () => pagina.GetByRole(AriaRole.Button, new() { Name = "Exportar HTML" }).ClickAsync());

        download.SuggestedFilename.Should().MatchRegex(@"^auditlog_\d{8}_\d{6}\.html$");
        var pasta = Directory.CreateTempSubdirectory("siat-exportacao-");
        try
        {
            var arquivo = Path.Combine(pasta.FullName, download.SuggestedFilename);
            await download.SaveAsAsync(arquivo);

            await using var contexto = await _navegador.Browser.NewContextAsync();
            var offline = await contexto.NewPageAsync();
            await offline.GotoAsync(new Uri(arquivo).AbsoluteUri);

            await Expect(offline.Locator("#contador")).ToHaveTextAsync("5 logs");
            var primeiro = offline.Locator("details.item").First;
            await Expect(primeiro.Locator("summary")).ToContainTextAsync("eTCM");
            await primeiro.Locator("summary").ClickAsync();
            await Expect(primeiro.Locator("pre.codigo.escuro .st-tipo")).ToContainTextAsync("System.InvalidOperationException");
            await Expect(primeiro.Locator("pre.codigo:not(.escuro) .json-chave").First).ToHaveTextAsync("\"Usuario\"");

            var registro = _app.Registros.Registros.Should().ContainSingle().Subject;
            registro.Operacao.Should().Be(OperacaoConsulta.Exportacao);
            registro.Modulo.Should().Be(Modulo.AuditLog);
            registro.Itens.Should().Be(5);
        }
        finally
        {
            pasta.Delete(recursive: true);
            await pagina.Context.CloseAsync();
        }
    }
}
