using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tcmrj.Siat.Web.Components.Layout;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Marca do menu: sigla do TCMRio e "SIAT", o nome do sistema embaixo e, só fora de produção, a fita da publicação
/// (Development = desenvolvimento, Staging = homologação), separada da etiqueta de ambiente dos servidores.
/// </summary>
public sealed class MarcaTests : BunitContext
{
    private IRenderedComponent<Marca> RenderizarEm(string ambiente)
    {
        var hospedagem = new Mock<IWebHostEnvironment>();
        hospedagem.SetupGet(h => h.EnvironmentName).Returns(ambiente);
        Services.AddSingleton(hospedagem.Object);

        return Render<Marca>();
    }

    [Fact]
    public void Mostra_a_sigla_e_o_nome_do_sistema()
    {
        var marca = RenderizarEm("Production");

        marca.Find(".marca-sigla").TextContent.Should().Be("SIAT");
        marca.Find(".marca-nome").TextContent.Should().Be("Sistema Integrado de Apoio Técnico");
    }

    [Theory]
    [InlineData("Development", "dsv", "Desenvolvimento")]
    [InlineData("Staging", "hml", "Homologação")]
    public void Fora_de_producao_mostra_a_fita_da_publicacao_antes_do_logo(string ambiente, string chave, string nome)
    {
        var marca = RenderizarEm(ambiente);
        var fita = marca.Find(".fita-publicacao");

        fita.GetAttribute("data-publicacao").Should().Be(chave);
        fita.TextContent.Should().Be(nome);
        marca.Find(".marca").FirstElementChild!.ClassName.Should().Be("fita-publicacao");
    }

    [Fact]
    public void A_publicacao_nao_usa_a_etiqueta_de_ambiente_de_servidor()
    {
        var marca = RenderizarEm("Development");

        marca.FindAll(".amb").Should().BeEmpty();
        marca.FindAll("[data-amb]").Should().BeEmpty();
    }

    [Fact]
    public void Em_producao_nao_mostra_a_fita()
    {
        RenderizarEm("Production").FindAll(".fita-publicacao").Should().BeEmpty();
    }
}
