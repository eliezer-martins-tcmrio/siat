using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Resumo numérico (research.md R11; T038): indisponível nunca vira zero.</summary>
public sealed class ResumoNumericoTests : BunitContext
{
    [Fact]
    public void Mostra_rotulos_valores_detalhes_e_indisponivel()
    {
        var resumo = Render<ResumoNumerico>(p => p
            .Add(c => c.Rotulo, "Resumo do período")
            .Add(c => c.Itens, new IndicadorResumo[]
            {
                new("Consumo bruto", "US$ 12.00", "300 créditos"),
                new("Valor líquido", Indisponivel: true),
                new("Uso do limite", "—", "Disponível em períodos que incluem o mês atual", Indisponivel: true),
            }));

        resumo.Find("dl").GetAttribute("aria-label").Should().Be("Resumo do período");
        resumo.FindAll("dt").Select(d => d.TextContent).Should().Equal("Consumo bruto", "Valor líquido", "Uso do limite");
        var valores = resumo.FindAll("dd");
        valores[0].TextContent.Should().Contain("US$ 12.00").And.Contain("300 créditos");
        valores[1].TextContent.Trim().Should().Be("Indisponível");
        valores[1].TextContent.Should().NotContain("0.00");
        valores[2].TextContent.Should().Contain("—").And.Contain("Disponível em períodos que incluem o mês atual");
    }
}
