using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Faixa de contexto: servidor e ambiente em toda consulta; em PRD, destacada com o aviso de registro (FR-009).</summary>
public sealed class FaixaContextoTests : BunitContext
{
    private const string AvisoDeRegistro = "consulta fica registrada em log";

    private IRenderedComponent<FaixaContexto> Renderizar(ServidorVM servidor, string? modulo = "MailQueue") =>
        Render<FaixaContexto>(p => p.Add(c => c.Servidor, servidor).Add(c => c.Modulo, modulo));

    [Fact]
    public void Em_PRD_marca_data_amb_prd_e_mostra_o_aviso_de_registro_em_log()
    {
        var faixa = Renderizar(new("tcmbd", "TCMBD", Ambiente.Prd));

        var contexto = faixa.Find(".contexto");
        contexto.GetAttribute("data-amb").Should().Be("prd");
        faixa.Find(".amb").TextContent.Should().Be("PRD");
        faixa.Find(".contexto-aviso").TextContent.Should().Contain(AvisoDeRegistro).And.Contain("seu usuário");
        faixa.Find("strong").TextContent.Should().Be("TCMBD");
        faixa.Markup.Should().Contain("MailQueue");
    }

    [Theory]
    [InlineData(Ambiente.Dsv, "dsv", "DSV", "Desenvolvimento")]
    [InlineData(Ambiente.Hml, "hml", "HML", "Homologação")]
    public void Em_DSV_e_HML_nao_mostra_o_aviso_de_registro(Ambiente ambiente, string dataAmb, string sigla, string texto)
    {
        var faixa = Renderizar(new("srv", @"TCMBD3\DEV", ambiente));

        faixa.Find(".contexto").GetAttribute("data-amb").Should().Be(dataAmb);
        faixa.Find(".amb").TextContent.Should().Be(sigla);
        faixa.Find(".contexto-aviso").TextContent.Should().Be(texto);
        faixa.Markup.Should().NotContain(AvisoDeRegistro).And.NotContain("registrada em log");
    }

    [Fact]
    public void O_destaque_de_PRD_vem_so_do_data_amb_e_a_faixa_e_uma_regiao_nomeada()
    {
        var prd = Renderizar(new("tcmbd", "TCMBD", Ambiente.Prd)).Find(".contexto");
        var hml = Renderizar(new("tcmbd4-hml", @"TCMBD4\HML", Ambiente.Hml)).Find(".contexto");

        prd.ClassList.Should().BeEquivalentTo(hml.ClassList);
        prd.GetAttribute("role").Should().Be("region");
        prd.GetAttribute("aria-label").Should().Be("Servidor consultado");
    }

    [Fact]
    public void Aviso_informado_substitui_o_texto_padrao()
    {
        var faixa = Render<FaixaContexto>(p => p
            .Add(c => c.Servidor, new ServidorVM("tcmbd", "TCMBD", Ambiente.Prd))
            .Add(c => c.Aviso, "Outro aviso"));

        faixa.Find(".contexto-aviso").TextContent.Should().Be("Outro aviso");
    }

    [Fact]
    public void Sem_modulo_nao_mostra_o_separador()
    {
        var faixa = Renderizar(new("srv", "TCMBD4", Ambiente.Hml), modulo: null);

        faixa.Markup.Should().NotContain("·");
    }
}
