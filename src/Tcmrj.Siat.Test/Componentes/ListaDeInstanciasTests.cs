using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Lista das instâncias de um ambiente, sem a célula da tabela, usada na ficha e no Catálogo
/// (specs/004-ficha-sistema/contracts/rotas-ui.md, Componente ListaDeInstancias; T021).
/// </summary>
public sealed class ListaDeInstanciasTests : BunitContext
{
    private IRenderedComponent<ListaDeInstancias> Renderizar(AmbienteDoCatalogo ambiente, params Instancia[] instancias) =>
        Render<ListaDeInstancias>(p => p
            .Add(c => c.Sistema, "SGQ")
            .Add(c => c.Ambiente, AmbienteVM.De(ambiente, instancias)));

    private static Instancia Inst(string[]? urls = null, string[]? maquinas = null, string? caminho = null) =>
        new(urls ?? [], maquinas ?? [], caminho);

    [Fact]
    public void Nao_desenha_a_celula_da_tabela()
    {
        var lista = Renderizar(AmbienteDoCatalogo.Prd, Inst(maquinas: ["A"]));

        lista.FindAll("td").Should().BeEmpty();
        lista.FindAll("div.instancia").Should().HaveCount(1);
    }

    [Fact]
    public void Sem_instancias_mostra_nao_configurado()
    {
        var lista = Renderizar(AmbienteDoCatalogo.Hml);

        var vazio = lista.Find(".vazio-cel");
        vazio.QuerySelector(".so-leitor")!.TextContent.Should().Be("não configurado");
        lista.FindAll(".instancia").Should().BeEmpty();
    }

    [Fact]
    public void Endereco_http_vira_link_em_nova_aba()
    {
        var lista = Renderizar(AmbienteDoCatalogo.Prd, Inst(urls: ["https://tcmrj.tcmnet.tcm/sgq"]));

        var link = lista.Find("a");
        link.GetAttribute("href").Should().Be("https://tcmrj.tcmnet.tcm/sgq");
        link.GetAttribute("target").Should().Be("_blank");
        link.GetAttribute("rel").Should().Be("noopener noreferrer");
        link.QuerySelector(".so-leitor")!.TextContent.Should().Contain("(abre em nova aba)");
    }

    [Fact]
    public void Endereco_que_nao_e_http_fica_como_texto()
    {
        var lista = Renderizar(AmbienteDoCatalogo.Prd, Inst(urls: ["javascript:alert(1)"]));

        lista.FindAll("a").Should().BeEmpty();
        lista.FindAll("dd").Select(d => d.TextContent).Should().Contain("javascript:alert(1)");
    }

    [Fact]
    public void Maquinas_e_caminho_tem_botao_de_copia()
    {
        var lista = Renderizar(AmbienteDoCatalogo.Prd, Inst(maquinas: ["TCM2016WEB1"], caminho: @"W:\TcmRoot\Sites\sgq"));

        lista.FindAll("button").Select(b => b.GetAttribute("aria-label")).Should().Equal(
            "Copiar máquina TCM2016WEB1 de PRD do SGQ",
            "Copiar caminho de PRD do SGQ");
    }

    [Fact]
    public void Marcacao_da_origem_aparece_codificada()
    {
        var lista = Renderizar(AmbienteDoCatalogo.Dsv, Inst(urls: ["<b>negrito</b>"], maquinas: ["<i>M</i>"], caminho: "<script>x</script>"));

        lista.FindAll("b").Should().BeEmpty();
        lista.FindAll("i").Should().BeEmpty();
        lista.FindAll("script").Should().BeEmpty();
        lista.Markup.Should().Contain("&lt;b&gt;");
    }

    [Fact]
    public void Celula_do_catalogo_envolve_a_mesma_lista()
    {
        var ambiente = AmbienteVM.De(AmbienteDoCatalogo.Prd, [Inst(urls: ["https://x"], maquinas: ["A", "B"], caminho: @"C:\y")]);

        var lista = Render<ListaDeInstancias>(p => p.Add(c => c.Sistema, "SGQ").Add(c => c.Ambiente, ambiente));
        var celula = Render<CelulaAmbiente>(p => p.Add(c => c.Sistema, "SGQ").Add(c => c.Ambiente, ambiente));

        celula.Find("td").ChildNodes.MarkupMatches(lista.Markup);
    }
}
