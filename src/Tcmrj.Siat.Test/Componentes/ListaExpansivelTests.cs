using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Lista expansível: detalhe sob demanda e linha aberta pelo parâmetro <c>sel</c>.</summary>
public sealed class ListaExpansivelTests : BunitContext
{
    public sealed record Item(string Id, string Texto);

    private static readonly IReadOnlyList<Item> Itens = [new("a", "Primeiro"), new("b", "Segundo"), new("c", "Terceiro")];

    private IRenderedComponent<ListaExpansivel<Item>> Renderizar(
        string? selecionado = null,
        Action<string?>? aoMudarSelecionado = null,
        Action<Item>? aoAbrir = null) =>
        Render<ListaExpansivel<Item>>(p =>
        {
            p.Add(c => c.Itens, Itens)
             .Add(c => c.Chave, i => i.Id)
             .Add(c => c.Linha, i => $"<span class=\"cel-texto\">{i.Texto}</span>")
             .Add(c => c.Detalhe, i => $"<p class=\"detalhe-teste\">Detalhe de {i.Texto}</p>")
             .Add(c => c.Selecionado, selecionado);
            if (aoMudarSelecionado is not null)
            {
                p.Add(c => c.SelecionadoChanged, aoMudarSelecionado);
            }

            if (aoAbrir is not null)
            {
                p.Add(c => c.AoAbrir, aoAbrir);
            }
        });

    [Fact]
    public void Detalhe_so_e_renderizado_depois_de_abrir_a_linha()
    {
        var lista = Renderizar();

        lista.FindAll(".item-linha").Should().HaveCount(3);
        lista.FindAll(".item-detalhe").Should().BeEmpty();
        lista.Markup.Should().NotContain("Detalhe de");

        lista.FindAll(".item-linha")[1].Click();

        lista.Find(".item-detalhe").TextContent.Should().Contain("Detalhe de Segundo");
        lista.FindAll(".item-detalhe").Should().ContainSingle();
        lista.FindAll(".item-linha")[1].GetAttribute("aria-expanded").Should().Be("true");
        lista.FindAll(".item-linha")[0].GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public void Sel_de_item_fora_do_resultado_e_ignorado()
    {
        var lista = Renderizar(selecionado: "zzz");

        lista.FindAll(".item-detalhe").Should().BeEmpty();
        lista.FindAll(".item-linha").Select(b => b.GetAttribute("aria-expanded")).Should().AllBe("false");
    }

    [Fact]
    public void Sel_existente_abre_a_linha()
    {
        var lista = Renderizar(selecionado: "c");

        lista.Find(".item-detalhe").TextContent.Should().Contain("Detalhe de Terceiro");
        lista.Find("[data-linha='c']").GetAttribute("aria-expanded").Should().Be("true");
    }

    [Fact]
    public void Abrir_e_fechar_avisam_o_selecionado_e_AoAbrir_so_ao_abrir()
    {
        var selecionados = new List<string?>();
        var abertos = new List<Item>();
        var lista = Renderizar(aoMudarSelecionado: selecionados.Add, aoAbrir: abertos.Add);

        lista.Find("[data-linha='a']").Click();
        lista.Find("[data-linha='a']").Click();

        selecionados.Should().Equal("a", null);
        abertos.Should().ContainSingle().Which.Id.Should().Be("a");
        lista.FindAll(".item-detalhe").Should().BeEmpty();
    }

    [Fact]
    public void Abrir_outra_linha_fecha_a_anterior()
    {
        var lista = Renderizar(selecionado: "a");

        lista.Find("[data-linha='b']").Click();

        lista.FindAll(".item-detalhe").Should().ContainSingle()
            .Which.TextContent.Should().Contain("Detalhe de Segundo");
        lista.Find("[data-linha='a']").GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public void Linhas_tem_data_linha_com_a_chave()
    {
        var lista = Renderizar();

        lista.FindAll("button.item-linha").Select(b => b.GetAttribute("data-linha")).Should().Equal("a", "b", "c");
    }
}
