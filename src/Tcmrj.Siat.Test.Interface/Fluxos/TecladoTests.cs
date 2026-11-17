using Microsoft.Playwright;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Test.Interface.Infra;
using static Microsoft.Playwright.Assertions;

namespace Tcmrj.Siat.Test.Interface.Fluxos;

/// <summary>
/// FR-024: MailQueue e AuditLog usáveis só pelo teclado. Tab passa por menu, seletor de servidor, filtros, Consultar
/// e lista; "/" foca o filtro local; ↑/↓ andam entre as linhas; Enter abre o detalhe. Contraste, rótulos e foco
/// visível são conferidos à mão no roteiro de acessibilidade do quickstart (T110).
/// </summary>
public sealed class TecladoTests : IClassFixture<AuditLogWebApplicationFactory>
{
    private static readonly DateTime Base = DateTime.Today.AddHours(9);

    private readonly AuditLogWebApplicationFactory _app;
    private readonly Navegador _navegador;

    public TecladoTests(AuditLogWebApplicationFactory app, Navegador navegador)
    {
        _app = app;
        _navegador = navegador;

        _app.MailQueue.Limpar();
        _app.AuditLog.Limpar();
        _app.Registros.Limpar();
        _app.MailQueue.DefinirSmtps(new Smtp(1, "Notificações"), new Smtp(2, "RH"));
        _app.AuditLog.DefinirAplicacoes(new Aplicacao(12, "eTCM"), new Aplicacao(31, "UserManager"));

        for (var i = 0; i < 5; i++)
        {
            var postado = Base.AddMinutes(-i);
            _app.MailQueue.AdicionarMensagem((i % 2) + 1, new MensagemEnfileirada
            {
                MessageId = $"7B0A{i:D4}-0000-4000-8000-000000000000",
                Assunto = $"Aviso {i}",
                DataPostagem = postado,
                EnviarApos = postado,
                DataEnvio = postado.AddSeconds(30),
                Enviado = true,
                EhHtml = false,
                Corpo = $"Corpo do aviso {i}",
                Destinatarios = "ana.souza@tcm.exemplo",
            });
            _app.AuditLog.AdicionarRegistro(new RegistroAuditoria
            {
                LogId = 500 - i,
                TipoId = 1,
                Tipo = "Information",
                Data = postado,
                Mensagem = $"Despacho {i} distribuído",
                LogUid = "6f1c2a4e-0000-4000-8000-000000000000",
                AppId = 12,
                LogEvent = """{"Origem":"teste"}""",
            });
        }
    }

    private static ILocator Linhas(IPage pagina) => pagina.Locator(".lista-itens button.item-linha");

    private static Task TabAsync(IPage pagina) => pagina.Keyboard.PressAsync("Tab");

    /// <summary>Tab até o alvo receber o foco (no máximo <paramref name="limite"/> teclas); falha se não chegar.</summary>
    private static async Task TabAteAsync(IPage pagina, ILocator alvo, int limite = 12)
    {
        for (var i = 0; i < limite; i++)
        {
            await TabAsync(pagina);
            if (await alvo.EvaluateAsync<bool>("e => e === document.activeElement"))
            {
                return;
            }
        }

        throw new Xunit.Sdk.XunitException($"O foco não chegou em {alvo} com {limite} teclas Tab.");
    }

    private async Task<IPage> AbrirAsync(UsuarioDeTeste usuario, string caminho, string titulo)
    {
        var pagina = await _app.AbrirAsync(_navegador.Browser, usuario, caminho);
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = titulo, Level = 1 })).ToBeVisibleAsync();
        await Expect(Linhas(pagina)).ToHaveCountAsync(5);
        return pagina;
    }

    [Fact]
    public async Task MailQueue_so_pelo_teclado_do_menu_ate_o_detalhe()
    {
        var pagina = await AbrirAsync(new UsuarioDeTeste("MailQueue.CanSelect"), $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}", "MailQueue");

        // Menu, depois o conteúdo: servidor, SMTP, busca, quantidade e Consultar, uma tecla Tab para cada.
        await TabAteAsync(pagina, pagina.GetByRole(AriaRole.Link, new() { Name = "MailQueue" }));
        await TabAteAsync(pagina, pagina.GetByLabel("Servidor", new() { Exact = true }));
        await TabAsync(pagina);
        await Expect(pagina.GetByLabel("SMTP", new() { Exact = true })).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(pagina.GetByLabel("Buscar no banco")).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(pagina.GetByLabel("Quantidade")).ToBeFocusedAsync();
        await TabAsync(pagina);
        var consultar = pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true });
        await Expect(consultar).ToBeFocusedAsync();

        // Consultar de novo só pelo teclado (Enter no botão).
        var chamadas = _app.MailQueue.ChamadasPesquisa;
        await pagina.Keyboard.PressAsync("Enter");
        await EsperarAsync(() => _app.MailQueue.ChamadasPesquisa > chamadas);
        await Expect(Linhas(pagina)).ToHaveCountAsync(5);

        // Depois de Consultar: filtro local, Exportar HTML e a primeira linha da lista.
        await consultar.FocusAsync();
        await TabAsync(pagina);
        await Expect(pagina.Locator("[data-busca]")).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(pagina.GetByRole(AriaRole.Button, new() { Name = "Exportar HTML" })).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(Linhas(pagina).First).ToBeFocusedAsync();

        await UsarListaAsync(pagina);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task AuditLog_so_pelo_teclado_do_menu_ate_o_detalhe()
    {
        var pagina = await AbrirAsync(new UsuarioDeTeste("AuditLog.CanSelect"), $"/consultas/auditlog?servidor={SiatWebApplicationFactory.ServidorHml}", "AuditLog");

        await TabAteAsync(pagina, pagina.GetByRole(AriaRole.Link, new() { Name = "AuditLog" }));
        await TabAteAsync(pagina, pagina.GetByLabel("Servidor", new() { Exact = true }));
        await TabAsync(pagina);
        await Expect(pagina.Locator("#app")).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(pagina.Locator("#logid")).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(pagina.Locator("#qtd")).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(pagina.GetByRole(AriaRole.Button, new() { Name = "Consultar", Exact = true })).ToBeFocusedAsync();

        await TabAsync(pagina);
        await Expect(pagina.Locator("[data-busca]")).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(pagina.GetByRole(AriaRole.Button, new() { Name = "Exportar HTML" })).ToBeFocusedAsync();
        await TabAsync(pagina);
        await Expect(Linhas(pagina).First).ToBeFocusedAsync();

        await UsarListaAsync(pagina);
        await pagina.Context.CloseAsync();
    }

    [Fact]
    public async Task Barra_dentro_de_um_campo_de_texto_e_so_um_caractere()
    {
        var pagina = await AbrirAsync(new UsuarioDeTeste("MailQueue.CanSelect"), $"/consultas/mailqueue?servidor={SiatWebApplicationFactory.ServidorHml}", "MailQueue");

        var busca = pagina.GetByLabel("Buscar no banco");
        await busca.FocusAsync();
        await pagina.Keyboard.TypeAsync("a/b");
        await Expect(busca).ToHaveValueAsync("a/b");
        await Expect(busca).ToBeFocusedAsync();
        await pagina.Context.CloseAsync();
    }

    /// <summary>Com o foco na primeira linha: ↑/↓, Enter abre e fecha o detalhe e "/" leva ao filtro.</summary>
    private static async Task UsarListaAsync(IPage pagina)
    {
        var linhas = Linhas(pagina);
        await Expect(linhas.First).ToBeFocusedAsync();
        await Expect(linhas.First).ToHaveAttributeAsync("aria-expanded", "false");

        await pagina.Keyboard.PressAsync("ArrowDown");
        await Expect(linhas.Nth(1)).ToBeFocusedAsync();
        await pagina.Keyboard.PressAsync("ArrowDown");
        await Expect(linhas.Nth(2)).ToBeFocusedAsync();
        await pagina.Keyboard.PressAsync("ArrowUp");
        await Expect(linhas.Nth(1)).ToBeFocusedAsync();

        // Na primeira linha a seta para cima não sai da lista.
        await pagina.Keyboard.PressAsync("ArrowUp");
        await Expect(linhas.First).ToBeFocusedAsync();
        await pagina.Keyboard.PressAsync("ArrowUp");
        await Expect(linhas.First).ToBeFocusedAsync();

        await pagina.Keyboard.PressAsync("Enter");
        await Expect(linhas.First).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(pagina.Locator(".item-detalhe")).ToHaveCountAsync(1);
        await Expect(linhas.First).ToBeFocusedAsync();

        // Foco na linha e "/" vai para o filtro local; ↓ no filtro volta para a primeira linha.
        await pagina.Keyboard.PressAsync("/");
        await Expect(pagina.Locator("[data-busca]")).ToBeFocusedAsync();
        await pagina.Keyboard.PressAsync("ArrowDown");
        await Expect(linhas.First).ToBeFocusedAsync();

        await pagina.Keyboard.PressAsync("Enter");
        await Expect(linhas.First).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(pagina.Locator(".item-detalhe")).ToHaveCountAsync(0);
    }

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        var limite = DateTime.UtcNow.AddSeconds(Navegador.TempoLimite / 1000);
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new Xunit.Sdk.XunitException("A condição não foi atendida a tempo.");
            }

            await Task.Delay(50);
        }
    }
}
