using System.Net;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using Tcmrj.Siat.Test.Interface.Infra;
using static Microsoft.Playwright.Assertions;

namespace Tcmrj.Siat.Test.Interface.Fluxos;

/// <summary>Fumaça da infraestrutura: host no Kestrel, usuário de teste no circuito e permissões pelo pipeline real.</summary>
public sealed partial class InfraTests(SiatWebApplicationFactory app, Navegador navegador) : IClassFixture<SiatWebApplicationFactory>
{
    [Fact]
    public async Task Sem_usuario_de_teste_a_aplicacao_exige_autenticacao()
    {
        using var cliente = new HttpClient { BaseAddress = app.Endereco };

        using var resposta = await cliente.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Usuario_com_MailQueue_vai_da_pagina_inicial_para_a_MailQueue_e_ve_so_esse_item_no_menu()
    {
        await using var contexto = await app.NovoContextoAsync(navegador.Browser, new UsuarioDeTeste("MailQueue.CanSelect"));
        var pagina = await contexto.NewPageAsync();

        await pagina.GotoAsync("/");

        await Expect(pagina).ToHaveURLAsync(RotaMailQueue());
        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "MailQueue", Level = 1 })).ToBeVisibleAsync();
        var menu = pagina.GetByRole(AriaRole.Navigation, new() { Name = "Navegação principal" });
        await Expect(menu.GetByRole(AriaRole.Link, new() { Name = "MailQueue" })).ToBeVisibleAsync();
        await Expect(menu.GetByRole(AriaRole.Link, new() { Name = "AuditLog" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Usuario_sem_recursos_ve_a_mensagem_de_sem_acesso_e_nenhum_item_de_Consultas()
    {
        await using var contexto = await app.NovoContextoAsync(navegador.Browser, new UsuarioDeTeste());
        var pagina = await contexto.NewPageAsync();

        await pagina.GotoAsync("/");

        await Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Você não tem acesso às ferramentas do SIAT" })).ToBeVisibleAsync();
        await Expect(pagina.GetByRole(AriaRole.Group, new() { Name = "Consultas" })).ToHaveCountAsync(0);
        await Expect(pagina.GetByText("Usuária de Teste")).ToBeVisibleAsync();
    }

    [GeneratedRegex(@"/consultas/mailqueue$")]
    private static partial Regex RotaMailQueue();
}
