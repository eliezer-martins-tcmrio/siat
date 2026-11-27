using System.Globalization;
using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Barra de uso do limite (specs/005-copilot-meu-consumo, FR-016 e FR-034; T038): a faixa sempre em texto.</summary>
public sealed class BarraDeUsoTests : BunitContext
{
    private IRenderedComponent<BarraDeUso> Barra(decimal bruto, LimiteVigente? limite) =>
        Render<BarraDeUso>(p => p.Add(c => c.Uso, UsoDoLimiteVM.De(UsoDoLimite.Calcular(bruto, limite, "2026-11"), bruto)));

    [Theory]
    [InlineData(10, 50, "20%", "Dentro do limite", "normal")]
    [InlineData(40, 50, "80%", "Atenção", "atencao")]
    [InlineData(60, 50, "120%", "Limite atingido no relatório", "atingido")]
    public void Mostra_o_percentual_e_o_texto_da_faixa(decimal bruto, decimal limite, string percentual, string faixa, string classe)
    {
        var barra = Barra(bruto, new LimiteVigente.Individual(limite));

        barra.Find(".percentual").TextContent.Should().Be(percentual);
        barra.Find(".faixa").TextContent.Should().Be(faixa);
        barra.Find(".barra-uso").GetAttribute("data-faixa").Should().Be(classe);
        barra.Find(".preenchido").GetAttribute("style").Should().Be($"width: {Math.Min(int.Parse(percentual.TrimEnd('%'), CultureInfo.InvariantCulture), 100)}%");
    }

    [Fact]
    public void Nota_com_o_restante_e_o_mes()
    {
        Barra(40, new LimiteVigente.Individual(50)).Find(".nota").TextContent
            .Should().Be("US$ 10.00 abaixo do limite no relatório · acumulado de novembro de 2026");
    }

    [Fact]
    public void Limite_zero_e_indisponivel_mostram_so_o_texto()
    {
        var zero = Barra(5, new LimiteVigente.Individual(0));
        zero.Find(".faixa").TextContent.Should().Be("Limite zero");
        zero.FindAll(".percentual").Should().BeEmpty();

        var indisponivel = Barra(5, null);
        indisponivel.Find(".faixa").TextContent.Should().Be("Indisponível");
        indisponivel.FindAll(".trilho").Should().BeEmpty();
    }
}
