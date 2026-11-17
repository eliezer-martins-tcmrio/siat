using AwesomeAssertions;
using Microsoft.Playwright;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Test.Interface.Infra;
using static Microsoft.Playwright.Assertions;

namespace Tcmrj.Siat.Test.Interface.Fluxos;

/// <summary>
/// História 3 (quickstart 11 a 13): servidores de PRD só para quem tem o recurso de PRD do módulo; acesso direto pela
/// URL é negado sem consultar o banco e a tentativa fica registrada (SC-005, SC-006); em PRD a faixa de contexto é
/// destacada e avisa do registro em log (FR-009).
/// </summary>
public sealed class PrdTests : IClassFixture<AuditLogWebApplicationFactory>
{
    private const string Prd = SiatWebApplicationFactory.ServidorPrd;

    private readonly AuditLogWebApplicationFactory _app;
    private readonly Navegador _navegador;

    public PrdTests(AuditLogWebApplicationFactory app, Navegador navegador)
    {
        _app = app;
        _navegador = navegador;

        _app.MailQueue.Limpar();
        _app.AuditLog.Limpar();
        _app.Registros.Limpar();
        _app.MailQueue.DefinirSmtps(new Smtp(1, "Notificações"));
        _app.AuditLog.DefinirAplicacoes(new Aplicacao(12, "eTCM"));
        _app.MailQueue.AdicionarMensagem(1, new MensagemEnfileirada
        {
            MessageId = "msg-prd-1",
            Assunto = "Aviso de PRD",
            Corpo = "<p>Corpo de PRD</p>",
            Destinatarios = "ana@exemplo.invalid",
            DataPostagem = DateTime.Today.AddHours(8),
        });
        _app.AuditLog.AdicionarRegistro(new RegistroAuditoria
        {
            LogId = 500,
            TipoId = 1,
            Tipo = "Information",
            Data = DateTime.Today.AddHours(8),
            Mensagem = "Registro de PRD",
            LogUid = "6f1c2a4e-0000-4000-8000-000000000000",
            AppId = 12,
            LogEvent = """{"Origem":"prd"}""",
        });
    }

    private static UsuarioDeTeste SemPrd() => new("MailQueue.CanSelect", "AuditLog.CanSelect");

    private static UsuarioDeTeste ComPrd() =>
        new("MailQueue.CanSelect", "MailQueuePRD.CanSelect", "AuditLog.CanSelect", "AuditLogPRD.CanSelect");

    private static string Pagina(Modulo modulo) => $"/consultas/{modulo.ToString().ToLowerInvariant()}";

    private async Task<IPage> AbrirAsync(UsuarioDeTeste usuario, Modulo modulo, string consulta = "")
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, usuario, Pagina(modulo) + consulta);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = modulo.ToString(), Level = 1 })).ToBeVisibleAsync();
        return pagina;
    }

    public static TheoryData<Modulo> Modulos => new() { Modulo.MailQueue, Modulo.AuditLog };

    private int ChamadasAoBanco(Modulo modulo) => modulo == Modulo.MailQueue
        ? _app.MailQueue.ChamadasPesquisa + _app.MailQueue.ChamadasSmtps
        : _app.AuditLog.ChamadasPesquisa + _app.AuditLog.ChamadasAplicacoes;

    [Theory]
    [MemberData(nameof(Modulos))]
    public async Task Seletor_nao_oferece_PRD_a_quem_nao_tem_o_recurso_de_PRD(Modulo modulo)
    {
        var pagina = await AbrirAsync(SemPrd(), modulo);

        var seletor = pagina.Locator("#servidor");
        (await seletor.Locator("optgroup").EvaluateAllAsync<string[]>("gs => gs.map(g => g.label)")).Should().Equal("DSV", "HML");
        await Expect(seletor.Locator($"option[value='{Prd}']")).ToHaveCountAsync(0);
        (await seletor.Locator("option").AllTextContentsAsync()).Should().NotContain(SiatWebApplicationFactory.NomeServidorPrd);
        await pagina.Context.CloseAsync();
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public async Task URL_com_servidor_de_PRD_sem_o_recurso_mostra_acesso_negado_sem_dados_e_sem_chamar_o_banco(Modulo modulo)
    {
        var pagina = await AbrirAsync(SemPrd(), modulo, $"?servidor={Prd}");

        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Acesso negado" })).ToBeVisibleAsync();
        await Expect(pagina.Locator(".estado.erro")).ToContainTextAsync("MailQueuePRD.CanSelect".Replace("MailQueue", modulo.ToString()));
        await Expect(pagina.Locator(".lista-itens .item-linha")).ToHaveCountAsync(0);
        await Expect(pagina.Locator(".contexto[data-amb='prd']")).ToHaveCountAsync(0);
        (await pagina.ContentAsync()).Should().NotContain("Aviso de PRD").And.NotContain("Registro de PRD");
        ChamadasAoBanco(modulo).Should().Be(0);

        // A tentativa que passou da validação fica registrada, inclusive a negada (SC-005, SC-006).
        var registro = _app.Registros.Registros.Should().ContainSingle().Which;
        registro.Modulo.Should().Be(modulo);
        registro.Resultado.Should().Be(ResultadoRegistro.AcessoNegado);
        registro.ServidorChave.Should().Be(Prd);
        registro.Ambiente.Should().Be(Ambiente.Prd);
        registro.ServidorNome.Should().Be(SiatWebApplicationFactory.NomeServidorPrd);
        registro.Itens.Should().BeNull();
        await pagina.Context.CloseAsync();
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public async Task Usuario_com_o_recurso_de_PRD_ve_o_grupo_de_PRD_e_a_faixa_com_o_aviso_de_registro(Modulo modulo)
    {
        var pagina = await AbrirAsync(ComPrd(), modulo);

        var seletor = pagina.Locator("#servidor");
        (await seletor.Locator("optgroup").EvaluateAllAsync<string[]>("gs => gs.map(g => g.label)")).Should().Equal("DSV", "HML", "PRD");
        await Expect(seletor.Locator("optgroup[data-amb='prd']")).ToHaveCountAsync(1);

        await seletor.SelectOptionAsync(Prd);

        var faixa = pagina.Locator(".contexto[data-amb='prd']");
        await Expect(faixa).ToBeVisibleAsync();
        await Expect(faixa).ToContainTextAsync(SiatWebApplicationFactory.NomeServidorPrd);
        await Expect(faixa.Locator(".contexto-aviso")).ToContainTextAsync("consulta fica registrada em log");
        await Expect(pagina.Locator(".seletor-servidor[data-amb='prd']")).ToHaveCountAsync(1);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Acesso negado" })).ToHaveCountAsync(0);

        // Fundo sólido de PRD (DESIGN.md): diferente do fundo suave de DSV e HML.
        var fundoPrd = await faixa.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");
        await seletor.SelectOptionAsync(SiatWebApplicationFactory.ServidorHml);
        var fundoHml = await pagina.Locator(".contexto[data-amb='hml']").EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");
        fundoPrd.Should().NotBe(fundoHml);

        _app.Registros.Registros.Should().Contain(r =>
            r.Modulo == modulo && r.ServidorChave == Prd && r.Ambiente == Ambiente.Prd && r.Resultado == ResultadoRegistro.Sucesso);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Faixa_de_DSV_e_HML_nao_tem_o_aviso_de_registro()
    {
        var pagina = await AbrirAsync(ComPrd(), Modulo.MailQueue, $"?servidor={SiatWebApplicationFactory.ServidorHml}");

        var faixa = pagina.Locator(".contexto[data-amb='hml']");
        await Expect(faixa).ToBeVisibleAsync();
        await Expect(faixa).Not.ToContainTextAsync("registrada em log");
        await pagina.Context.CloseAsync();
    }
}
