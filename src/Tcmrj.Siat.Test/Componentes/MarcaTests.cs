using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tcmrj.Siat.Web.Components.Layout;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Marca do menu: sigla do TCMRio e "SIAT", o nome do sistema embaixo e, só fora de produção, o ambiente da
/// publicação (Development = DSV, Staging = HML), como no SiCATI.
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
    public void Fora_de_producao_mostra_o_ambiente(string ambiente, string sigla, string nome)
    {
        var etiqueta = RenderizarEm(ambiente).Find(".marca-ambiente");

        etiqueta.GetAttribute("data-amb").Should().Be(sigla);
        etiqueta.TextContent.Should().Be(nome);
    }

    [Fact]
    public void Em_producao_nao_mostra_ambiente()
    {
        RenderizarEm("Production").FindAll(".marca-ambiente").Should().BeEmpty();
    }
}
