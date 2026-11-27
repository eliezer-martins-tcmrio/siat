using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Filtro de período (specs/005-copilot-meu-consumo, FR-023 a FR-025; T046).</summary>
public sealed class FiltroDePeriodoTests : BunitContext
{
    private static readonly DateOnly Hoje = new(2026, 11, 27);

    private readonly List<Periodo> _aplicados = [];
    private int _limpezas;

    private IRenderedComponent<FiltroDePeriodo> Filtro(Periodo? atual = null, bool desabilitado = false) =>
        Render<FiltroDePeriodo>(p => p
            .Add(c => c.Atual, atual ?? Periodo.Padrao(Hoje))
            .Add(c => c.Hoje, Hoje)
            .Add(c => c.Desabilitado, desabilitado)
            .Add(c => c.AoAplicar, (Periodo periodo) => _aplicados.Add(periodo))
            .Add(c => c.AoLimpar, () => _limpezas++));

    [Fact]
    public void Campos_com_min_e_max_da_janela_e_o_periodo_atual()
    {
        var filtro = Filtro();

        foreach (var id in new[] { "#periodo-de", "#periodo-ate" })
        {
            var campo = filtro.Find(id);
            campo.GetAttribute("min").Should().Be("2024-12-01");
            campo.GetAttribute("max").Should().Be("2026-11-27");
        }

        filtro.Find("#periodo-de").GetAttribute("value").Should().Be("2026-11-01");
        filtro.Find("#periodo-ate").GetAttribute("value").Should().Be("2026-11-27");
    }

    [Fact]
    public void Os_seis_atalhos_aplicam_o_periodo()
    {
        var filtro = Filtro();
        var atalhos = Periodo.Atalhos(Hoje);
        atalhos.Should().HaveCount(6);

        for (var i = 0; i < atalhos.Count; i++)
        {
            filtro.FindAll(".atalhos button")[i].Click();
        }

        _aplicados.Should().Equal(atalhos.Select(a => a.Periodo));
        filtro.FindAll(".atalhos button").Single(b => b.TextContent == "Mês atual").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void Aplicar_periodo_valido_dispara_o_evento()
    {
        var filtro = Filtro();
        filtro.Find("#periodo-de").Change("2026-10-01");
        filtro.Find("#periodo-ate").Change("2026-10-31");

        filtro.Find("form").Submit();

        _aplicados.Should().Equal(new Periodo(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Aplicar_periodo_invalido_mostra_a_mensagem_ligada_aos_campos_e_nao_dispara()
    {
        var filtro = Filtro();
        filtro.Find("#periodo-de").Change("2026-11-20");
        filtro.Find("#periodo-ate").Change("2026-11-10");

        filtro.Find("form").Submit();

        _aplicados.Should().BeEmpty();
        filtro.Find("#erro-periodo").TextContent.Should().Be(Erros.Copilot.PeriodoInvalido.Message);
        filtro.Find("#periodo-de").GetAttribute("aria-describedby").Should().Be("erro-periodo");
        filtro.Find("#periodo-ate").GetAttribute("aria-invalid").Should().Be("true");
    }

    [Fact]
    public void Limpar_periodo_dispara_o_padrao()
    {
        Filtro(new Periodo(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31))).Find("#limpar-periodo").Click();

        _limpezas.Should().Be(1);
    }

    [Fact]
    public void Desabilitado_durante_a_leitura()
    {
        var filtro = Filtro(desabilitado: true);

        filtro.FindAll("input, button").Should().OnlyContain(e => e.HasAttribute("disabled"));
    }
}
