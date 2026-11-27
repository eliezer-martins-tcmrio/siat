using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Tabela de modelos (FR-018, research.md R11; T038).</summary>
public sealed class TabelaDeModelosTests : BunitContext
{
    [Fact]
    public void Cabecalhos_valores_participacao_e_modelo_nao_informado()
    {
        var tabela = Render<TabelaDeModelos>(p => p.Add(c => c.Modelos, new ModeloVM[]
        {
            new("GPT-5", true, new Consumo(1234.6m, 1234.56m, 617.28m), 0.75m),
            new("Modelo não informado", false, new Consumo(10, 411.52m, 205.76m), 0.25m),
        }));

        tabela.FindAll("thead th").Select(t => t.TextContent).Should()
            .Equal("Modelo", "Créditos consumidos", "Valor bruto", "Valor líquido", "Participação");
        var primeira = tabela.FindAll("tbody tr")[0];
        primeira.QuerySelectorAll("td")[0].TextContent.Should().Be("1.235");
        primeira.QuerySelectorAll("td")[1].TextContent.Should().Be("US$ 1,234.56");
        tabela.FindAll(".participacao > span:first-child").Select(s => s.TextContent).Should().Equal("75,0%", "25,0%");
        tabela.Find("th.sem-modelo").TextContent.Should().Be("Modelo não informado");
    }

    [Fact]
    public void Sem_modelos_mostra_sem_consumo_reportado()
    {
        var tabela = Render<TabelaDeModelos>(p => p.Add(c => c.Modelos, Array.Empty<ModeloVM>()));

        tabela.Markup.Should().Contain("Sem consumo reportado neste período");
        tabela.FindAll("tbody tr").Should().BeEmpty();
    }
}
