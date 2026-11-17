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
/// História 1, cenários de aceitação 1 a 6 (quickstart 2 a 6), com a MailQueue em memória.
/// </summary>
public sealed class MailQueueTests : IClassFixture<SiatWebApplicationFactory>
{
    private const string CorpoComScript =
        "<p id=\"seguro\">Conteúdo seguro do e-mail</p>" +
        "<script>document.body.innerHTML = 'EXECUTOU'; parent.document.title = 'INVADIDO';</script>" +
        "<img src=\"http://externo.invalid/rastreio.png\" alt=\"rastreio\">";

    private static readonly DateTime Base = DateTime.Today.AddHours(9);

    private readonly SiatWebApplicationFactory _app;
    private readonly Navegador _navegador;

    public MailQueueTests(SiatWebApplicationFactory app, Navegador navegador)
    {
        _app = app;
        _navegador = navegador;

        _app.MailQueue.Limpar();
        _app.Registros.Limpar();
        _app.MailQueue.DefinirSmtps(new Smtp(1, "Notificações"), new Smtp(2, "Comunicações processuais"), new Smtp(3, "RH"));

        // 60 e-mails comuns, um por minuto, do mais novo (i = 0) para o mais antigo.
        for (var i = 0; i < 60; i++)
        {
            _app.MailQueue.AdicionarMensagem((i % 3) + 1, Mensagem(Id(i), Base.AddMinutes(-i), $"Aviso {i}"));
        }

        // 12 e-mails do SMTP 2 com o texto literal "[teste]_%" e um do SMTP 1, que o filtro por SMTP deve deixar de fora.
        for (var i = 0; i < 12; i++)
        {
            _app.MailQueue.AdicionarMensagem(2, Mensagem(Id(100 + i), Base.AddDays(-1).AddMinutes(-i), $"Prazo [teste]_% {i}"));
        }

        _app.MailQueue.AdicionarMensagem(1, Mensagem(Id(200), Base.AddDays(-1), "Outro SMTP [teste]_%"));

        // Parecido, mas sem o texto literal: "_" e "%" não podem funcionar como curinga.
        _app.MailQueue.AdicionarMensagem(2, Mensagem(Id(201), Base.AddDays(-1), "Prazo [teste]xy"));

        // O mais recente de todos: corpo HTML com script e imagem externa.
        _app.MailQueue.AdicionarMensagem(1, Mensagem(Id(300), Base.AddMinutes(5), "E-mail com script") with
        {
            EhHtml = true,
            Corpo = CorpoComScript,
        });
    }

    private static string Id(int i) => $"5F0A{i:D4}-0000-4000-8000-000000000000";

    private static MensagemEnfileirada Mensagem(string id, DateTime postado, string assunto) => new()
    {
        MessageId = id,
        Assunto = assunto,
        DataPostagem = postado,
        EnviarApos = postado,
        DataEnvio = postado.AddSeconds(30),
        Enviado = true,
        EhHtml = false,
        Corpo = $"Corpo de {assunto}",
        Destinatarios = "ana.souza@tcm.exemplo; carlos.lima@tcm.exemplo",
    };

    private static UsuarioDeTeste SoMailQueue(int matricula = 12345) => new("MailQueue.CanSelect") { Matricula = matricula };

    private static ILocator Linhas(IPage pagina) => pagina.Locator(".lista-itens button.item-linha");

    private async Task<IPage> AbrirMailQueueAsync(UsuarioDeTeste usuario, string caminho = "/consultas/mailqueue")
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, usuario, caminho);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "MailQueue", Level = 1 })).ToBeVisibleAsync();
        return pagina;
    }

    [Fact]
    public async Task Cenario1_menu_mostra_Consultas_com_MailQueue_e_sem_AuditLog()
    {
        var pagina = await AbrirMailQueueAsync(SoMailQueue(), "/");

        var consultas = pagina.GetByRole(AriaRole.Group, new() { Name = "Consultas" });
        await Expect(consultas.GetByRole(AriaRole.Link, new() { Name = "MailQueue" })).ToBeVisibleAsync();
        await Expect(consultas.GetByRole(AriaRole.Link, new() { Name = "AuditLog" })).ToHaveCountAsync(0);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario2_seletor_mostra_so_DSV_e_HML_e_nao_tem_campo_livre()
    {
        var pagina = await AbrirMailQueueAsync(SoMailQueue());

        var seletor = pagina.GetByLabel("Servidor", new() { Exact = true });
        await Expect(seletor).ToHaveJSPropertyAsync("tagName", "SELECT");
        var grupos = await seletor.Locator("optgroup").EvaluateAllAsync<string[]>("gs => gs.map(g => g.label)");
        grupos.Should().Equal("DSV", "HML");
        var opcoes = await seletor.Locator("optgroup option").AllTextContentsAsync();
        opcoes.Should().Equal(SiatWebApplicationFactory.NomeServidorDsv, SiatWebApplicationFactory.NomeServidorHml);
        await Expect(seletor.Locator($"option[value='{SiatWebApplicationFactory.ServidorPrd}']")).ToHaveCountAsync(0);
        await Expect(pagina.GetByRole(AriaRole.Textbox, new() { Name = "Servidor" })).ToHaveCountAsync(0);
        await Expect(pagina.Locator("input[name*='servidor' i], input[id*='servidor' i]")).ToHaveCountAsync(0);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario3_consulta_sem_filtros_mostra_os_50_mais_recentes_de_todos_os_SMTPs()
    {
        var pagina = await AbrirMailQueueAsync(SoMailQueue());

        await pagina.GetByLabel("Servidor", new() { Exact = true }).SelectOptionAsync(SiatWebApplicationFactory.ServidorHml);

        await Expect(Linhas(pagina)).ToHaveCountAsync(50);
        await Expect(Linhas(pagina).First).ToHaveAttributeAsync("data-linha", Id(300));
        await Expect(Linhas(pagina).Nth(1)).ToHaveAttributeAsync("data-linha", Id(0));
        await Expect(pagina.GetByText("50 e-mails", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(pagina.Locator(".contexto[data-amb='hml']")).ToContainTextAsync(SiatWebApplicationFactory.NomeServidorHml);
        await Expect(pagina.GetByLabel("SMTP", new() { Exact = true }).Locator("option")).ToHaveCountAsync(4);

        var filtro = _app.MailQueue.UltimoFiltro!;
        filtro.SmtpId.Should().BeNull();
        filtro.Texto.Should().BeNull();
        filtro.Quantidade.Should().Be(50);
        _app.MailQueue.UltimoServidor!.Chave.Should().Be(SiatWebApplicationFactory.ServidorHml);
        var registro = _app.Registros.Registros.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoRegistro.Sucesso);
        registro.Itens.Should().Be(50);
        await Expect(pagina).ToHaveURLAsync(new Regex($@"/consultas/mailqueue\?servidor={SiatWebApplicationFactory.ServidorHml}$"));
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario4_filtros_por_SMTP_texto_literal_e_quantidade()
    {
        var pagina = await AbrirMailQueueAsync(SoMailQueue(), $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}");
        await Expect(Linhas(pagina)).ToHaveCountAsync(50);

        await pagina.GetByLabel("SMTP", new() { Exact = true }).SelectOptionAsync("2");
        await pagina.GetByLabel("Buscar no banco").FillAsync("[teste]_%");
        await pagina.GetByLabel("Quantidade").FillAsync("10");
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true }).ClickAsync();

        await Expect(Linhas(pagina)).ToHaveCountAsync(10);
        await Expect(pagina.GetByText("10 e-mails", new() { Exact = true })).ToBeVisibleAsync();
        var assuntos = await pagina.Locator(".lista-itens .item-linha .cel-texto").AllTextContentsAsync();
        assuntos.Should().AllSatisfy(a => a.Should().StartWith("Prazo [teste]_%"));
        var filtro = _app.MailQueue.UltimoFiltro!;
        filtro.SmtpId.Should().Be(2);
        filtro.Texto.Should().Be("[teste]_%");
        filtro.Quantidade.Should().Be(10);
        await Expect(pagina).ToHaveURLAsync(new Regex(@"smtp=2&texto=%5Bteste%5D_%25&qtd=10$"));
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario4_quantidade_fora_do_limite_e_ajustada_e_nao_numerica_e_recusada_sem_consulta()
    {
        var pagina = await AbrirMailQueueAsync(SoMailQueue(), $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}&qtd=5000");
        await Expect(Linhas(pagina)).ToHaveCountAsync(75);
        _app.MailQueue.UltimoFiltro!.Quantidade.Should().Be(1000);
        var chamadas = _app.MailQueue.ChamadasPesquisa;

        await pagina.GetByLabel("Quantidade").FillAsync("dez");
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true }).ClickAsync();

        await Expect(pagina.GetByText("Informe um número entre 1 e 1000 na quantidade.")).ToBeVisibleAsync();
        _app.MailQueue.ChamadasPesquisa.Should().Be(chamadas);
        await Expect(Linhas(pagina)).ToHaveCountAsync(75);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario5_corpo_com_script_aparece_isolado_sem_executar_nem_carregar_recurso_externo()
    {
        var externos = new List<string>();
        var pagina = await AbrirMailQueueAsync(SoMailQueue(), $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}");
        pagina.Context.Request += (_, requisicao) =>
        {
            if (requisicao.Url.Contains("externo.invalid", StringComparison.Ordinal))
            {
                externos.Add(requisicao.Url);
            }
        };
        var titulo = await pagina.TitleAsync();

        await pagina.Locator($"[data-linha='{Id(300)}']").ClickAsync();

        var iframe = pagina.Locator("iframe.corpo-email");
        await Expect(iframe).ToHaveAttributeAsync("sandbox", string.Empty);
        var corpo = pagina.FrameLocator("iframe.corpo-email");
        await Expect(corpo.Locator("#seguro")).ToHaveTextAsync("Conteúdo seguro do e-mail");
        await Expect(corpo.GetByText("EXECUTOU")).ToHaveCountAsync(0);
        (await pagina.TitleAsync()).Should().Be(titulo);
        externos.Should().BeEmpty();

        var detalhe = pagina.Locator(".item-detalhe");
        await Expect(detalhe.Locator("dl.campos")).ToContainTextAsync("E-mail com script");
        await Expect(detalhe.Locator("dl.campos")).ToContainTextAsync("ana.souza@tcm.exemplo; carlos.lima@tcm.exemplo");
        await Expect(detalhe.Locator("dl.campos")).ToContainTextAsync(Id(300));
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Cenario6_URL_copiada_refaz_a_consulta_com_a_mesma_linha_aberta_para_outra_pessoa()
    {
        var pagina = await AbrirMailQueueAsync(SoMailQueue());
        await pagina.GetByLabel("Servidor", new() { Exact = true }).SelectOptionAsync(SiatWebApplicationFactory.ServidorDsv);
        await Expect(Linhas(pagina)).ToHaveCountAsync(50);
        await pagina.GetByLabel("SMTP", new() { Exact = true }).SelectOptionAsync("2");
        await pagina.GetByLabel("Buscar no banco").FillAsync("Prazo");
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true }).ClickAsync();
        await Expect(Linhas(pagina)).ToHaveCountAsync(13);

        var escolhida = Id(103);
        await pagina.Locator($"[data-linha='{escolhida}']").ClickAsync();
        await Expect(pagina).ToHaveURLAsync(new Regex($"sel={escolhida}"));
        var url = pagina.Url;
        await pagina.Context.CloseAsync();
        _app.MailQueue.Limpar();
        _app.MailQueue.DefinirSmtps(new Smtp(2, "Comunicações processuais"));
        for (var i = 0; i < 12; i++)
        {
            _app.MailQueue.AdicionarMensagem(2, Mensagem(Id(100 + i), Base.AddDays(-1).AddMinutes(-i), $"Prazo [teste]_% {i}"));
        }

        var outra = await _app.AbrirAsync(_navegador.Browser, SoMailQueue(matricula: 54321), url);

        await Expect(outra.Locator($"[data-linha='{escolhida}']")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(outra.Locator(".item-detalhe dl.campos")).ToContainTextAsync(escolhida);
        await Expect(outra.Locator(".contexto[data-amb='dsv']")).ToContainTextAsync(SiatWebApplicationFactory.NomeServidorDsv);
        await Expect(outra.GetByLabel("SMTP", new() { Exact = true })).ToHaveValueAsync("2");
        await Expect(outra.GetByLabel("Buscar no banco")).ToHaveValueAsync("Prazo");
        var filtro = _app.MailQueue.UltimoFiltro!;
        filtro.SmtpId.Should().Be(2);
        filtro.Texto.Should().Be("Prazo");
        _app.MailQueue.UltimoServidor!.Chave.Should().Be(SiatWebApplicationFactory.ServidorDsv);
        await outra.Context.CloseAsync();
    }

    [Fact]
    public async Task Sel_de_item_que_nao_esta_no_resultado_e_ignorado()
    {
        var pagina = await AbrirMailQueueAsync(
            SoMailQueue(),
            string.Create(CultureInfo.InvariantCulture, $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}&sel=nao-existe"));

        await Expect(Linhas(pagina)).ToHaveCountAsync(50);
        await Expect(pagina.Locator(".item-detalhe")).ToHaveCountAsync(0);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Erro_do_servidor_mostra_servidor_e_motivo()
    {
        _app.MailQueue.ErroNaPesquisa = Erros.Consultas.TempoEsgotado(SiatWebApplicationFactory.NomeServidorHml);
        var pagina = await AbrirMailQueueAsync(SoMailQueue());

        await pagina.GetByLabel("Servidor", new() { Exact = true }).SelectOptionAsync(SiatWebApplicationFactory.ServidorHml);

        var erro = pagina.Locator(".estado.erro");
        await Expect(erro).ToContainTextAsync(SiatWebApplicationFactory.NomeServidorHml);
        await Expect(erro).ToContainTextAsync("excedeu o tempo limite");
        _app.Registros.Registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Erro);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Usuario_sem_recurso_da_MailQueue_ve_acesso_negado()
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, new UsuarioDeTeste("AuditLog.CanSelect"), "/consultas/mailqueue");

        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Acesso negado" })).ToBeVisibleAsync();
        _app.MailQueue.ChamadasPesquisa.Should().Be(0);
        _app.MailQueue.ChamadasSmtps.Should().Be(0);
        await pagina.Context.CloseAsync();
    }
}
