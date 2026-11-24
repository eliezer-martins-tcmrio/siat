using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tcmrj.Siat.Web.Components.Layout;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Menu lateral: cada item só aparece com a política do módulo; o grupo some sem nenhum (FR-003). O grupo Catálogo
/// vem antes de Consultas.
/// </summary>
public sealed class MenuTests : BunitContext
{
    private IRenderedComponent<Menu> RenderizarCom(params string[] politicas) => RenderizarEm(null, politicas);

    private IRenderedComponent<Menu> RenderizarEm(string? url, params string[] politicas)
    {
        var autorizacao = AddAuthorization();
        autorizacao.SetAuthorized("usuario.teste");
        autorizacao.SetPolicies(politicas);
        if (url is not null)
        {
            Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        }

        return Render<Menu>();
    }

    [Fact]
    public void Sem_recursos_a_secao_Consultas_nao_aparece()
    {
        var menu = RenderizarCom();

        menu.FindAll(".menu-grupo").Should().BeEmpty();
        menu.FindAll("a.menu-item").Should().BeEmpty();
        menu.Markup.Should().NotContain("Consultas");
    }

    [Fact]
    public void Com_MailQueue_CanSelect_mostra_somente_MailQueue()
    {
        var menu = RenderizarCom("MailQueue.CanSelect");

        menu.Find(".menu-grupo").GetAttribute("aria-label").Should().Be("Consultas");
        var itens = menu.FindAll("a.menu-item");
        itens.Should().ContainSingle();
        itens[0].TextContent.Trim().Should().Be("MailQueue");
        itens[0].GetAttribute("href").Should().Be("consultas/mailqueue");
        itens[0].QuerySelector("svg")!.GetAttribute("aria-hidden").Should().Be("true", itens[0].OuterHtml);
    }

    [Fact]
    public void Com_os_dois_modulos_mostra_MailQueue_e_AuditLog_nessa_ordem()
    {
        var menu = RenderizarCom("MailQueue.CanSelect", "AuditLog.CanSelect");

        menu.FindAll("a.menu-item").Select(a => a.TextContent.Trim())
            .Should().Equal("MailQueue", "AuditLog");
    }

    [Fact]
    public void Item_da_pagina_atual_fica_marcado()
    {
        var menu = RenderizarEm("consultas/auditlog?servidor=x", "MailQueue.CanSelect", "AuditLog.CanSelect");

        var auditLog = menu.FindAll("a.menu-item").Single(a => a.TextContent.Trim() == "AuditLog");
        auditLog.GetAttribute("aria-current").Should().Be("page");
        menu.FindAll("a.menu-item").Single(a => a.TextContent.Trim() == "MailQueue")
            .HasAttribute("aria-current").Should().BeFalse();
    }

    [Fact]
    public void Com_Sistemas_CanSelect_mostra_o_grupo_Catalogo_com_Sistemas()
    {
        var menu = RenderizarCom("Sistemas.CanSelect");

        var grupo = menu.Find(".menu-grupo");
        grupo.GetAttribute("aria-label").Should().Be("Catálogo");
        grupo.QuerySelector(".menu-titulo")!.TextContent.Should().Be("Catálogo");
        var itens = menu.FindAll("a.menu-item");
        itens.Should().ContainSingle();
        itens[0].TextContent.Trim().Should().Be("Sistemas");
        itens[0].GetAttribute("href").Should().Be("catalogo/sistemas");
        menu.Markup.Should().NotContain("Consultas");
    }

    [Fact]
    public void O_grupo_Catalogo_vem_antes_de_Consultas()
    {
        var menu = RenderizarCom("MailQueue.CanSelect", "Sistemas.CanSelect", "AuditLog.CanSelect");

        menu.FindAll(".menu-grupo").Select(g => g.GetAttribute("aria-label"))
            .Should().Equal("Catálogo", "Consultas");
        menu.FindAll("a.menu-item").Select(a => a.TextContent.Trim())
            .Should().Equal("Sistemas", "MailQueue", "AuditLog");
    }

    [Fact]
    public void Sem_Sistemas_CanSelect_o_grupo_Catalogo_nao_aparece()
    {
        var menu = RenderizarCom("MailQueue.CanSelect", "AuditLog.CanSelect");

        menu.FindAll(".menu-grupo").Select(g => g.GetAttribute("aria-label")).Should().Equal("Consultas");
        menu.Markup.Should().NotContain("Catálogo");
        menu.Markup.Should().NotContain("catalogo/sistemas");
    }
}
