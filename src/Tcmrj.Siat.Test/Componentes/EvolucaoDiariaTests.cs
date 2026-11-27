using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Evolução diária (FR-017, research.md R11; T038).</summary>
public sealed class EvolucaoDiariaTests : BunitContext
{
    private static readonly DateOnly Dia1 = new(2026, 11, 26);
    private static readonly DateOnly Dia2 = new(2026, 11, 27);

    private static ConsumoPorModelo M(string modelo, decimal bruto) => new(modelo, new Consumo(bruto * 25, bruto, bruto / 2));

    private static DiaVM Dia(DateOnly data, params ConsumoPorModelo[] modelos) =>
        new(data, modelos.Aggregate(Consumo.Zero, (s, m) => s + m.Consumo), modelos);

    private IRenderedComponent<EvolucaoDiaria> Evolucao(IReadOnlyList<DiaVM> dias, Action<DateOnly>? aoEscolher = null) =>
        Render<EvolucaoDiaria>(p =>
        {
            p.Add(c => c.Dias, dias);
            if (aoEscolher is not null)
            {
                p.Add(c => c.AoEscolherDia, aoEscolher);
            }
        });

    [Fact]
    public void Um_botao_por_dia_com_o_aria_label_da_metrica()
    {
        var evolucao = Evolucao([Dia(Dia1), Dia(Dia2, M("GPT-5", 1.23m))]);

        var dias = evolucao.FindAll("button.dia");
        dias.Should().HaveCount(2);
        dias[1].GetAttribute("aria-label").Should().Be("27/11/2026, valor bruto US$ 1.23. Filtrar este dia.");
    }

    [Fact]
    public void Trocar_a_metrica_muda_o_aria_label()
    {
        var evolucao = Evolucao([Dia(Dia2, M("GPT-5", 2m))]);

        evolucao.FindAll(".segmentado button").Single(b => b.TextContent == "Créditos").Click();

        evolucao.Find("button.dia").GetAttribute("aria-label").Should().Be("27/11/2026, créditos 50. Filtrar este dia.");
    }

    [Fact]
    public void Visao_por_modelo_tem_legenda_dos_cinco_e_Outros_e_aria_label_das_series()
    {
        var evolucao = Evolucao(
        [
            Dia(Dia1),
            Dia(Dia2, M("GPT-5", 0.80m), M("B", 0.70m), M("C", 0.60m), M("D", 0.50m), M("E", 0.40m), M("F", 0.10m)),
        ]);

        evolucao.Find(".alternar input").Change(true);

        evolucao.FindAll(".legenda-modelos > span").Select(s => s.TextContent).Should().Equal("GPT-5", "B", "C", "D", "E", "Outros");
        var dias = evolucao.FindAll("button.dia");
        dias[1].GetAttribute("aria-label").Should().Be(
            "27/11/2026, valor bruto: GPT-5 US$ 0.80; B US$ 0.70; C US$ 0.60; D US$ 0.50; E US$ 0.40; Outros US$ 0.10. Filtrar este dia.");
        dias[0].GetAttribute("aria-label").Should().Be("26/11/2026, valor bruto: sem consumo. Filtrar este dia.");
        evolucao.FindAll(".segmentado button").Single(b => b.TextContent == "Valor líquido").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Trocar_a_metrica_muda_o_total_e_o_eixo_de_valores()
    {
        var evolucao = Evolucao([Dia(Dia1, M("GPT-5", 1m)), Dia(Dia2, M("GPT-5", 3.7m))]);

        evolucao.Find(".total").TextContent.Should().Be("US$ 4.70 no período (valor bruto)");
        evolucao.FindAll(".escala span:not(.medida)").Select(s => s.TextContent).Should()
            .Equal("US$ 0.00", "US$ 1.00", "US$ 2.00", "US$ 3.00", "US$ 4.00");

        evolucao.FindAll(".segmentado button").Single(b => b.TextContent == "Créditos").Click();

        evolucao.Find(".total").TextContent.Should().Be("118 no período (créditos)");
        evolucao.FindAll(".escala span:not(.medida)")[^1].TextContent.Should().Be("100");
    }

    [Fact]
    public void Visao_por_modelo_empilha_um_retangulo_por_serie_com_a_classe_da_cor()
    {
        var evolucao = Evolucao([Dia(Dia2, M("GPT-5", 2m), M("B", 1m))]);

        evolucao.FindAll("button.dia rect").Should().HaveCount(1);

        evolucao.Find(".alternar input").Change(true);

        evolucao.FindAll("button.dia rect").Select(r => r.GetAttribute("class")).Should().Equal("serie-1", "serie-2");
    }

    [Fact]
    public void Acionar_um_dia_dispara_o_evento_com_a_data()
    {
        DateOnly? escolhido = null;
        var evolucao = Evolucao([Dia(Dia1), Dia(Dia2)], d => escolhido = d);

        // Um <button> nativo aciona o clique também com Enter e Espaço.
        evolucao.FindAll("button.dia")[0].Click();

        escolhido.Should().Be(Dia1);
    }
}
