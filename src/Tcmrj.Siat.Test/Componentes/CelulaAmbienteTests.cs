using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Célula de um ambiente na tabela de Sistemas (contracts/rotas-ui.md, Componente CelulaAmbiente).</summary>
public sealed class CelulaAmbienteTests : BunitContext
{
    private IRenderedComponent<CelulaAmbiente> Renderizar(AmbienteDoCatalogo ambiente, params Instancia[] instancias) =>
        Render<CelulaAmbiente>(p => p
            .Add(c => c.Sistema, "SGQ")
            .Add(c => c.Ambiente, AmbienteVM.De(ambiente, instancias)));

    private static Instancia Inst(string[]? urls = null, string[]? maquinas = null, string? caminho = null) =>
        new(urls ?? [], maquinas ?? [], caminho);

    [Fact]
    public void Sem_instancias_mostra_nao_configurado()
    {
        var celula = Renderizar(AmbienteDoCatalogo.Hml);

        var vazio = celula.Find("td .vazio-cel");
        vazio.QuerySelector(".so-leitor")!.TextContent.Should().Be("não configurado");
        celula.FindAll(".instancia").Should().BeEmpty();
    }

    [Fact]
    public void Endereco_http_vira_link_em_nova_aba()
    {
        var celula = Renderizar(AmbienteDoCatalogo.Prd, Inst(urls: ["https://tcmrj.tcmnet.tcm/sgq"]));

        var link = celula.Find("a");
        link.GetAttribute("href").Should().Be("https://tcmrj.tcmnet.tcm/sgq");
        link.GetAttribute("target").Should().Be("_blank");
        link.GetAttribute("rel").Should().Be("noopener noreferrer");
        link.QuerySelector(".so-leitor")!.TextContent.Should().Contain("(abre em nova aba)");
        link.TextContent.Should().StartWith("tcmrj.tcmnet.tcm/sgq");
        celula.Find("dt").TextContent.Should().Be("URL");
    }

    [Fact]
    public void Endereco_que_nao_e_http_fica_como_texto()
    {
        var celula = Renderizar(AmbienteDoCatalogo.Prd, Inst(urls: ["AgenteSIGA.EXE (diversos agendamentos)", "javascript:alert(1)"]));

        celula.FindAll("a").Should().BeEmpty();
        celula.Markup.Should().Contain("AgenteSIGA.EXE (diversos agendamentos)");
        celula.FindAll("dd").Select(d => d.TextContent).Should().Contain("javascript:alert(1)");
    }

    [Fact]
    public void Cada_maquina_tem_o_seu_botao_de_copia()
    {
        var celula = Renderizar(AmbienteDoCatalogo.Prd, Inst(maquinas: ["TCM2016WEB1", "TCM2016WEB2"]));

        celula.FindAll("dt").Select(d => d.TextContent).Should().Equal("Máquina", "Máquina");
        celula.FindAll("dd .mono").Select(m => m.TextContent).Should().Equal("TCM2016WEB1", "TCM2016WEB2");
        celula.FindAll("button").Select(b => b.GetAttribute("aria-label")).Should().Equal(
            "Copiar máquina TCM2016WEB1 de PRD do SGQ",
            "Copiar máquina TCM2016WEB2 de PRD do SGQ");
    }

    [Fact]
    public void Caminho_tem_botao_de_copia_e_quebra_depois_dos_separadores()
    {
        var celula = Renderizar(AmbienteDoCatalogo.Prd, Inst(caminho: @"W:\TcmRoot\Sites\sgq_prd.v-1/x"));

        celula.Find("button").GetAttribute("aria-label").Should().Be("Copiar caminho de PRD do SGQ");
        var valor = celula.Find("dd .mono");
        valor.TextContent.Should().Be(@"W:\TcmRoot\Sites\sgq_prd.v-1/x");
        valor.InnerHtml.Should().Be(@"W:\<wbr>TcmRoot\<wbr>Sites\<wbr>sgq_<wbr>prd.<wbr>v-<wbr>1/<wbr>x");
    }

    [Fact]
    public void Varias_instancias_viram_blocos_separados()
    {
        var celula = Renderizar(AmbienteDoCatalogo.Prd, Inst(maquinas: ["A"]), Inst(maquinas: ["B"], caminho: "C:"));

        celula.FindAll("div.instancia").Should().HaveCount(2);
        celula.FindAll("div.instancia dl").Should().HaveCount(2);
    }

    [Fact]
    public void Marcacao_da_origem_aparece_codificada()
    {
        var celula = Renderizar(AmbienteDoCatalogo.Dsv, Inst(urls: ["<b>negrito</b>"], maquinas: ["<i>M</i>"], caminho: "<script>x</script>"));

        celula.FindAll("b").Should().BeEmpty();
        celula.FindAll("i").Should().BeEmpty();
        celula.FindAll("script").Should().BeEmpty();
        celula.Markup.Should().Contain("&lt;b&gt;");
    }
}
