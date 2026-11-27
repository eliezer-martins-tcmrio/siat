using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models.Copilot;
using static Tcmrj.Siat.Test.Unidade.Copilot.RelatorioDeConsumoTests;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Consumo do período montado dos relatórios diários (specs/005-copilot-meu-consumo/research.md R5).</summary>
public sealed class ConsumoDoPeriodoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);

    private static DiaDeConsumo Dia(int dia, params ItemDeConsumo[] itens) =>
        new(new DateOnly(2026, 11, dia), RelatorioDeConsumo.De(itens, Agora).Value);

    [Fact]
    public void Soma_os_dias_mescla_os_modelos_e_mantem_um_item_por_dia_em_ordem()
    {
        var periodo = new Periodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3));

        var consumo = ConsumoDoPeriodo.De(
            periodo,
            [
                Dia(3, Item("gpt-5", 1, 0.04m, 0.04m)),
                Dia(1, Item("GPT-5", 2, 0.08m, 0.06m), Item("Claude", 4, 0.16m, 0.16m)),
                Dia(2),
            ],
            Agora);

        consumo.Periodo.Should().Be(periodo);
        consumo.Total.Should().Be(new Consumo(7, 0.28m, 0.26m));
        consumo.Dias.Select(d => d.Data.Day).Should().Equal(1, 2, 3);
        consumo.Modelos.Should().Equal(
            new ConsumoPorModelo("Claude", new Consumo(4, 0.16m, 0.16m)),
            new ConsumoPorModelo("GPT-5", new Consumo(3, 0.12m, 0.10m)));
        consumo.ConsultadoEm.Should().Be(Agora);
    }
}
