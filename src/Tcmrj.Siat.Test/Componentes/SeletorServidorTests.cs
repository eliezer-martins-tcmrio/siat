using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Seletor de servidor: lista fechada agrupada por ambiente, sem campo livre (FR-007, FR-008).</summary>
public sealed class SeletorServidorTests : BunitContext
{
    private static readonly ServidorVM Dev = new("tcmbd3-dev", @"TCMBD3\DEV", Ambiente.Dsv);
    private static readonly ServidorVM Hml = new("tcmbd4-hml", @"TCMBD4\HML", Ambiente.Hml);
    private static readonly ServidorVM Hml2 = new("tcmbd5-hml", @"TCMBD5\HML", Ambiente.Hml);
    private static readonly ServidorVM Prd = new("tcmbd", "TCMBD", Ambiente.Prd);

    private IRenderedComponent<SeletorServidor> Renderizar(IReadOnlyList<GrupoServidoresVM> grupos, string? valor = null) =>
        Render<SeletorServidor>(p => p
            .Add(c => c.Grupos, grupos)
            .Add(c => c.Valor, valor));

    [Fact]
    public void Agrupa_os_servidores_por_ambiente()
    {
        var seletor = Renderizar(
        [
            new(Ambiente.Dsv, [Dev]),
            new(Ambiente.Hml, [Hml, Hml2]),
            new(Ambiente.Prd, [Prd]),
        ]);

        var grupos = seletor.FindAll("select optgroup");
        grupos.Select(g => g.GetAttribute("label")).Should().Equal("DSV", "HML", "PRD");
        grupos[1].QuerySelectorAll("option").Select(o => o.GetAttribute("value"))
            .Should().Equal("tcmbd4-hml", "tcmbd5-hml");
        grupos[2].QuerySelectorAll("option").Select(o => o.TextContent).Should().Equal("TCMBD");
    }

    [Fact]
    public void Nao_mostra_servidores_de_PRD_que_nao_vieram_na_entrada()
    {
        var seletor = Renderizar([new(Ambiente.Dsv, [Dev]), new(Ambiente.Hml, [Hml])]);

        seletor.FindAll("optgroup").Select(g => g.GetAttribute("label")).Should().NotContain("PRD");
        seletor.FindAll("option").Select(o => o.GetAttribute("value"))
            .Where(v => !string.IsNullOrEmpty(v))
            .Should().Equal("tcmbd3-dev", "tcmbd4-hml");
        seletor.Markup.Should().NotContain("TCMBD<");
    }

    [Fact]
    public void Nao_tem_campo_de_texto_livre()
    {
        var seletor = Renderizar([new(Ambiente.Dsv, [Dev]), new(Ambiente.Prd, [Prd])], valor: "tcmbd");

        seletor.FindAll("input").Should().BeEmpty();
        seletor.FindAll("textarea").Should().BeEmpty();
        seletor.FindAll("[contenteditable]").Should().BeEmpty();
        seletor.FindAll("select").Should().ContainSingle();
    }

    [Fact]
    public void Lista_vazia_avisa_que_nao_ha_servidores()
    {
        var seletor = Renderizar([]);

        seletor.Markup.Should().Contain("Não há servidores configurados.");
        seletor.FindAll("select").Should().BeEmpty();
    }

    [Fact]
    public void Servidor_escolhido_marca_o_ambiente_no_controle()
    {
        var seletor = Renderizar([new(Ambiente.Hml, [Hml]), new(Ambiente.Prd, [Prd])], valor: "tcmbd");

        seletor.Find(".seletor-servidor").GetAttribute("data-amb").Should().Be("prd");
        seletor.Find(".seletor-servidor .amb").TextContent.Should().Be("PRD");
        seletor.Find("option[selected]").GetAttribute("value").Should().Be("tcmbd");
    }

    [Fact]
    public void Chave_que_nao_esta_na_lista_nao_e_selecionada()
    {
        var seletor = Renderizar([new(Ambiente.Hml, [Hml])], valor: "tcmbd");

        seletor.Find(".seletor-servidor").HasAttribute("data-amb").Should().BeFalse();
        seletor.Find("option[selected]").GetAttribute("value").Should().BeEmpty();
    }

    [Fact]
    public void Trocar_o_servidor_avisa_a_nova_chave()
    {
        string? recebido = null;
        var seletor = Render<SeletorServidor>(p => p
            .Add(c => c.Grupos, [new GrupoServidoresVM(Ambiente.Dsv, [Dev]), new GrupoServidoresVM(Ambiente.Hml, [Hml])])
            .Add(c => c.Valor, "tcmbd3-dev")
            .Add(c => c.ValorChanged, (string? v) => recebido = v));

        seletor.Find("select").Change("tcmbd4-hml");

        recebido.Should().Be("tcmbd4-hml");
    }
}
