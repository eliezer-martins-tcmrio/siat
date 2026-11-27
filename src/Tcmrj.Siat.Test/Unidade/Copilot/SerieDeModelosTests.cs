using AwesomeAssertions;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Séries da visão por modelo (specs/005-copilot-meu-consumo/research.md R5, T037).</summary>
public sealed class SerieDeModelosTests
{
    private static readonly DateOnly Dia1 = new(2026, 11, 1);
    private static readonly DateOnly Dia2 = new(2026, 11, 2);

    private static ConsumoPorModelo M(string? modelo, decimal bruto) => new(modelo, new Consumo(bruto * 25, bruto, bruto / 2));

    private static DiaVM Dia(DateOnly data, params ConsumoPorModelo[] modelos) =>
        new(data, modelos.Aggregate(Consumo.Zero, (s, m) => s + m.Consumo), modelos);

    [Fact]
    public void Sete_modelos_viram_cinco_series_e_Outros_com_a_soma_dos_dois_restantes()
    {
        var dias = new[]
        {
            Dia(Dia1, M("A", 7), M("B", 6), M("C", 5), M("D", 4), M("E", 3), M("F", 2), M("G", 1)),
        };

        var series = SerieDeModelos.Montar(dias, MetricaDeConsumo.ValorBruto);

        series.Select(s => s.Rotulo).Should().Equal("A", "B", "C", "D", "E", "Outros");
        series[^1].Classe.Should().Be(SerieDeModelos.ClasseOutros);
        series[^1].Valores.Should().Equal(3m);
        series[0].Classe.Should().Be("serie-1");
    }

    [Fact]
    public void Quatro_modelos_viram_quatro_series_sem_Outros()
    {
        var series = SerieDeModelos.Montar([Dia(Dia1, M("A", 4), M("B", 3), M("C", 2), M("D", 1))], MetricaDeConsumo.ValorBruto);

        series.Select(s => s.Rotulo).Should().Equal("A", "B", "C", "D");
    }

    [Fact]
    public void Empate_e_decidido_pelo_nome_em_pt_BR()
    {
        var series = SerieDeModelos.Montar([Dia(Dia1, M("Zeta", 1), M("Ômega", 1), M("Alfa", 1))], MetricaDeConsumo.ValorBruto);

        series.Select(s => s.Rotulo).Should().Equal("Alfa", "Ômega", "Zeta");
    }

    [Fact]
    public void Modelo_nulo_tem_o_rotulo_Modelo_nao_informado()
    {
        var series = SerieDeModelos.Montar([Dia(Dia1, M(null, 1))], MetricaDeConsumo.ValorBruto);

        series.Single().Rotulo.Should().Be("Modelo não informado");
    }

    [Fact]
    public void Um_valor_por_dia_com_zero_no_dia_sem_o_modelo_e_na_metrica_escolhida()
    {
        var dias = new[] { Dia(Dia1, M("A", 2)), Dia(Dia2, M("B", 1)) };

        var series = SerieDeModelos.Montar(dias, MetricaDeConsumo.Creditos);

        series.Should().HaveCount(2);
        series[0].Rotulo.Should().Be("A");
        series[0].Valores.Should().Equal(50m, 0m);
        series[1].Valores.Should().Equal(0m, 25m);
    }
}
