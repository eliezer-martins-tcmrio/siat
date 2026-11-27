using AwesomeAssertions;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Escala vertical da evolução diária (FR-017): topo e marcas em passos redondos.</summary>
public sealed class EscalaDoGraficoTests
{
    [Theory]
    [InlineData(3.7, 4.0, 1.0)]
    [InlineData(18.2, 20.0, 5.0)]
    [InlineData(0.42, 0.5, 0.1)]
    [InlineData(1234, 1250, 250)]
    [InlineData(10.3, 12.5, 2.5)]
    [InlineData(8, 8, 2)]
    public void Topo_e_passo_redondos(double maximo, double topo, double passo)
    {
        var escala = EscalaDoGrafico.Para((decimal)maximo, inteiros: false);

        escala.Topo.Should().Be((decimal)topo);
        escala.Marcas[0].Should().Be(0);
        escala.Marcas[1].Should().Be((decimal)passo);
        escala.Marcas[^1].Should().Be((decimal)topo);
    }

    [Fact]
    public void Em_creditos_o_passo_minimo_e_um()
    {
        var escala = EscalaDoGrafico.Para(2, inteiros: true);

        escala.Marcas.Should().Equal(0m, 1m, 2m);
    }

    [Fact]
    public void Sem_consumo_a_escala_e_vazia_e_toda_altura_e_zero()
    {
        var escala = EscalaDoGrafico.Para(0, inteiros: false);

        escala.Should().Be(EscalaDoGrafico.Vazia);
        escala.Altura(5).Should().Be(0);
    }

    [Fact]
    public void Altura_e_proporcional_ao_topo()
    {
        var escala = EscalaDoGrafico.Para(3.7m, inteiros: false);

        escala.Altura(1).Should().Be(25);
        escala.Altura(4).Should().Be(100);
    }
}
