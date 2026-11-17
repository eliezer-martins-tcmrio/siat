using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Test.Interface.Infra;
using static Microsoft.Playwright.Assertions;

namespace Tcmrj.Siat.Test.Interface.Fluxos;

/// <summary>
/// História 2, cenários de aceitação 1 a 4 (quickstart 7 a 10), com o AuditLog em memória.
/// </summary>
public sealed class AuditLogTests : IClassFixture<AuditLogWebApplicationFactory>
{
    private const long LogComExcecao = 436700;
    private const long LogComLogEventInvalido = 436701;
    private const string LogEventInvalido = "não é JSON {";

    private const string StackTrace =
        "System.InvalidOperationException: O modelo de documento não foi encontrado.\r\n" +
        "   at Tcmrj.Etcm.Documentos.GeradorPdf.Gerar(Despacho despacho, CancellationToken ct) in D:\\src\\etcm\\GeradorPdf.cs:line 84\r\n" +
        " ---> System.IO.FileNotFoundException: Could not find file 'modelo.docx'.\r\n" +
        "   at System.IO.FileStream.ValidateFileHandle(SafeFileHandle handle)\r\n" +
        "   --- End of inner exception stack trace ---\r\n";

    private static readonly DateTime Base = DateTime.Today.AddHours(9);

    private readonly AuditLogWebApplicationFactory _app;
    private readonly Navegador _navegador;

    public AuditLogTests(AuditLogWebApplicationFactory app, Navegador navegador)
    {
        _app = app;
        _navegador = navegador;

        _app.AuditLog.Limpar();
        _app.Registros.Limpar();
        _app.AuditLog.DefinirAplicacoes(new Aplicacao(12, "eTCM"), new Aplicacao(31, "UserManager"), new Aplicacao(23, "Chamados e Serviços"));

        // 60 registros do eTCM (436500 a 436441) e 20 do UserManager (436600 a 436581).
        for (var i = 0; i < 60; i++)
        {
            _app.AuditLog.AdicionarRegistro(Registro(436500 - i, 12, Base.AddMinutes(-i), $"Despacho {i} distribuído"));
        }

        for (var i = 0; i < 20; i++)
        {
            _app.AuditLog.AdicionarRegistro(Registro(436600 - i, 31, Base.AddMinutes(-i), $"Login {i}"));
        }

        // Começam com 4363: 436300 a 436309 e 43631. Não começa: 14363.
        for (var i = 0; i < 10; i++)
        {
            _app.AuditLog.AdicionarRegistro(Registro(436300 + i, 23, Base.AddDays(-2).AddMinutes(i), $"Chamado {i}"));
        }

        _app.AuditLog.AdicionarRegistro(Registro(43631, 23, Base.AddDays(-30), "Chamado antigo"));
        _app.AuditLog.AdicionarRegistro(Registro(14363, 23, Base.AddDays(-60), "Chamado muito antigo"));

        // Os dois mais recentes: um com exceção e LogEvent JSON; outro com LogEvent que não é JSON.
        _app.AuditLog.AdicionarRegistro(Registro(LogComExcecao, 12, Base.AddMinutes(5), "Falha ao gerar o PDF do despacho") with
        {
            TipoId = 4,
            Tipo = "Error",
            Excecao = StackTrace,
            LogEvent = """{"RequestId":"0HN7QK2:00000A3","Usuario":"ana.souza","DespachoId":18834,"Reprocessar":true,"Detalhe":null}""",
        });
        _app.AuditLog.AdicionarRegistro(Registro(LogComLogEventInvalido, 31, Base.AddMinutes(6), "Aviso com LogEvent inválido") with
        {
            TipoId = 3,
            Tipo = "Warning",
            LogEvent = LogEventInvalido,
        });
    }

    private static RegistroAuditoria Registro(long logId, int appId, DateTime data, string mensagem) => new()
    {
        LogId = logId,
        TipoId = 1,
        Tipo = "Information",
        Data = data,
        Mensagem = mensagem,
        LogUid = "6f1c2a4e-0000-4000-8000-000000000000",
        AppId = appId,
        LogEvent = """{"Origem":"teste"}""",
    };

    private static UsuarioDeTeste SoAuditLog(int matricula = 12345) => new("AuditLog.CanSelect") { Matricula = matricula };

    private static ILocator Linhas(IPage pagina) => pagina.Locator(".lista-itens button.item-linha");

    private static string Caminho(string consulta = "") =>
        $"/consultas/auditlog?servidor={SiatWebApplicationFactory.ServidorHml}{consulta}";

    private static Task ConsultarAsync(IPage pagina) =>
        pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true }).ClickAsync();

    private async Task<IPage> AbrirAuditLogAsync(UsuarioDeTeste usuario, string caminho = "/consultas/auditlog")
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, usuario, caminho);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "AuditLog", Level = 1 })).ToBeVisibleAsync();
        return pagina;
    }

    [Fact]
    public async Task Cenario1_escolher_servidor_e_aplicacao_mostra_os_50_mais_recentes_da_aplicacao()
    {
        var pagina = await AbrirAuditLogAsync(SoAuditLog());

        await pagina.Locator("#servidor").SelectOptionAsync(SiatWebApplicationFactory.ServidorHml);

        await Expect(Linhas(pagina)).ToHaveCountAsync(50);
        await Expect(Linhas(pagina).First).ToHaveAttributeAsync("data-linha", LogComLogEventInvalido.ToString(CultureInfo.InvariantCulture));
        await Expect(pagina.Locator(".contexto[data-amb='hml']")).ToContainTextAsync(SiatWebApplicationFactory.NomeServidorHml);
        await Expect(pagina.Locator("#app option")).ToHaveCountAsync(4);
        await Expect(pagina).ToHaveURLAsync(new Regex($@"/consultas/auditlog\?servidor={SiatWebApplicationFactory.ServidorHml}$"));

        await pagina.Locator("#app").SelectOptionAsync("12");
        await ConsultarAsync(pagina);

        await Expect(pagina).ToHaveURLAsync(new Regex($@"servidor={SiatWebApplicationFactory.ServidorHml}&app=12$"));
        await Expect(pagina.GetByText("50 logs", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Linhas(pagina).First).ToHaveAttributeAsync("data-linha", LogComExcecao.ToString(CultureInfo.InvariantCulture));
        var ids = (await Linhas(pagina).EvaluateAllAsync<string[]>("ls => ls.map(l => l.dataset.linha)"))
            .Select(id => long.Parse(id, CultureInfo.InvariantCulture))
            .ToList();
        ids.Should().HaveCount(50).And.BeInDescendingOrder();
        ids.Skip(1).Should().Equal(Enumerable.Range(0, 49).Select(i => 436500L - i));
        var aplicacoes = await pagina.Locator(".lista-itens .item-linha .cel-suave.opc:not(.mono)").AllTextContentsAsync();
        aplicacoes.Should().HaveCount(50).And.AllBe("eTCM");

        var filtro = _app.AuditLog.UltimoFiltro!;
        filtro.AppId.Should().Be(12);
        filtro.PrefixoLogId.Should().BeNull();
        filtro.Quantidade.Should().Be(50);
        _app.AuditLog.UltimoServidor!.Chave.Should().Be(SiatWebApplicationFactory.ServidorHml);
        var registro = _app.Registros.Registros[^1];
        registro.Modulo.Should().Be(Tcmrj.Siat.Domain.Enums.Modulo.AuditLog);
        registro.Resultado.Should().Be(ResultadoRegistro.Sucesso);
        registro.Itens.Should().Be(50);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario2_lista_de_aplicacoes_indisponivel_troca_o_seletor_por_campo_numerico_com_aviso()
    {
        _app.AuditLog.ErroNasAplicacoes = Erros.Consultas.BancoInacessivel(SiatWebApplicationFactory.NomeServidorHml, "CSLIB_UserManager");

        var pagina = await AbrirAuditLogAsync(SoAuditLog(), Caminho());

        await Expect(pagina.GetByText("Não foi possível carregar a lista de aplicações.")).ToBeVisibleAsync();
        await Expect(pagina.Locator("#app")).ToHaveAttributeAsync("type", "number");
        await Expect(pagina.Locator("select#app")).ToHaveCountAsync(0);
        await Expect(Linhas(pagina)).ToHaveCountAsync(50);

        await pagina.Locator("#app").FillAsync("31");
        await ConsultarAsync(pagina);

        await Expect(Linhas(pagina)).ToHaveCountAsync(21);
        _app.AuditLog.UltimoFiltro!.AppId.Should().Be(31);
        await Expect(pagina.Locator(".lista-itens .item-linha .cel-suave.opc:not(.mono)").First).ToHaveTextAsync("AppID 31");
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario3_LogID_so_com_digitos_busca_pelo_inicio_e_com_outro_caractere_e_recusado_sem_consulta()
    {
        var pagina = await AbrirAuditLogAsync(SoAuditLog(), Caminho());
        await Expect(Linhas(pagina)).ToHaveCountAsync(50);

        await pagina.Locator("#logid").FillAsync("4363");
        await ConsultarAsync(pagina);

        await Expect(Linhas(pagina)).ToHaveCountAsync(11);
        var ids = await Linhas(pagina).EvaluateAllAsync<string[]>("ls => ls.map(l => l.dataset.linha)");
        ids.Should().AllSatisfy(id => id.Should().StartWith("4363"));
        ids.Should().NotContain("14363");
        _app.AuditLog.UltimoFiltro!.PrefixoLogId.Should().Be("4363");
        await Expect(pagina).ToHaveURLAsync(new Regex("logid=4363$"));
        var chamadas = _app.AuditLog.ChamadasPesquisa;

        await pagina.Locator("#logid").FillAsync("43a");
        await ConsultarAsync(pagina);

        await Expect(pagina.GetByText("Informe apenas dígitos no LogID.")).ToBeVisibleAsync();
        await Expect(pagina.Locator("#logid")).ToHaveAttributeAsync("aria-invalid", "true");
        _app.AuditLog.ChamadasPesquisa.Should().Be(chamadas);
        await Expect(Linhas(pagina)).ToHaveCountAsync(11);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario3_LogID_invalido_na_URL_nao_consulta_nem_registra()
    {
        var pagina = await AbrirAuditLogAsync(SoAuditLog(), Caminho("&logid=43a"));

        await Expect(pagina.GetByText("Informe apenas dígitos no LogID.")).ToBeVisibleAsync();
        await Expect(Linhas(pagina)).ToHaveCountAsync(0);
        _app.AuditLog.ChamadasPesquisa.Should().Be(0);
        _app.Registros.Registros.Should().BeEmpty();
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario4_detalhe_mostra_stack_trace_com_quebras_e_realce_e_LogEvent_indentado()
    {
        var sel = LogComExcecao.ToString(CultureInfo.InvariantCulture);
        var pagina = await AbrirAuditLogAsync(SoAuditLog(), Caminho($"&sel={sel}"));

        await Expect(pagina.Locator($"[data-linha='{sel}']")).ToHaveAttributeAsync("aria-expanded", "true");
        var detalhe = pagina.Locator(".item-detalhe");
        await Expect(detalhe.Locator("dl.campos")).ToContainTextAsync("Falha ao gerar o PDF do despacho");
        await Expect(detalhe.Locator("dl.campos")).ToContainTextAsync("TypeID 4");
        await Expect(detalhe.Locator("dl.campos")).ToContainTextAsync("eTCM (AppID 12)");

        var excecao = detalhe.Locator("pre.codigo.escuro");
        (await excecao.TextContentAsync()).Should().Be(StackTrace.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
        await Expect(excecao.Locator(".st-tipo").First).ToHaveTextAsync("System.InvalidOperationException:");
        await Expect(excecao.Locator(".st-metodo").First).ToHaveTextAsync("Gerar");
        await Expect(excecao.Locator(".st-arquivo")).ToHaveTextAsync("D:\\src\\etcm\\GeradorPdf.cs");
        await Expect(excecao.Locator(".st-tipo-param")).ToHaveCountAsync(3);

        var logEvent = detalhe.Locator("pre.codigo:not(.escuro)");
        (await logEvent.TextContentAsync()).Should().Be(
            "{\n" +
            "  \"RequestId\": \"0HN7QK2:00000A3\",\n" +
            "  \"Usuario\": \"ana.souza\",\n" +
            "  \"DespachoId\": 18834,\n" +
            "  \"Reprocessar\": true,\n" +
            "  \"Detalhe\": null\n" +
            "}");
        await Expect(logEvent.Locator(".json-chave")).ToHaveCountAsync(5);
        await Expect(logEvent.Locator(".json-numero")).ToHaveTextAsync("18834");
        await Expect(logEvent.Locator(".json-literal")).ToHaveTextAsync(new[] { "true", "null" });

        await pagina.Locator($"[data-linha='{LogComLogEventInvalido}']").ClickAsync();

        await Expect(pagina).ToHaveURLAsync(new Regex($"sel={LogComLogEventInvalido}"));
        await Expect(detalhe).ToHaveCountAsync(1);
        await Expect(detalhe.Locator("pre.codigo")).ToHaveTextAsync(LogEventInvalido);
        await Expect(detalhe.Locator(".json-chave")).ToHaveCountAsync(0);
        await Expect(pagina.Locator(".estado.erro")).ToHaveCountAsync(0);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Erro_do_servidor_mostra_servidor_e_motivo_e_registra_Erro()
    {
        _app.AuditLog.ErroNaPesquisa = Erros.Consultas.TempoEsgotado(SiatWebApplicationFactory.NomeServidorHml);

        var pagina = await AbrirAuditLogAsync(SoAuditLog(), Caminho());

        var erro = pagina.Locator(".estado.erro");
        await Expect(erro).ToContainTextAsync(SiatWebApplicationFactory.NomeServidorHml);
        await Expect(erro).ToContainTextAsync("excedeu o tempo limite");
        _app.Registros.Registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Erro);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Usuario_so_com_AuditLog_vai_da_inicial_para_o_AuditLog_e_o_menu_nao_mostra_MailQueue()
    {
        var pagina = await AbrirAuditLogAsync(SoAuditLog(), "/");

        await Expect(pagina).ToHaveURLAsync(new Regex("/consultas/auditlog$"));
        var consultas = pagina.GetByRole(AriaRole.Group, new() { Name = "Consultas" });
        await Expect(consultas.GetByRole(AriaRole.Link, new() { Name = "AuditLog" })).ToBeVisibleAsync();
        await Expect(consultas.GetByRole(AriaRole.Link, new() { Name = "MailQueue" })).ToHaveCountAsync(0);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Usuario_sem_recurso_do_AuditLog_ve_acesso_negado()
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, new UsuarioDeTeste("MailQueue.CanSelect"), Caminho());

        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Acesso negado" })).ToBeVisibleAsync();
        _app.AuditLog.ChamadasPesquisa.Should().Be(0);
        _app.AuditLog.ChamadasAplicacoes.Should().Be(0);
        await pagina.Context.CloseAsync();
    }
}
