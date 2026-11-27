using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Período consultado (specs/005-copilot-meu-consumo, FR-023 a FR-025; data-model.md, research.md R6): janela de 24
/// meses, no máximo 92 dias, atalhos e dias recentes.
/// </summary>
public sealed class PeriodoTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 27);

    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    [Fact]
    public void Padrao_vai_do_dia_1_do_mes_ate_hoje()
    {
        Periodo.Padrao(Hoje).Should().Be(new Periodo(D(2026, 11, 1), Hoje));
    }

    [Theory]
    [InlineData("2026-11-01", "2026-11-27", 2026, 11, 1, 2026, 11, 27)]
    [InlineData("2026-11-10", "2026-11-10", 2026, 11, 10, 2026, 11, 10)]
    [InlineData("2024-12-01", "2025-03-02", 2024, 12, 1, 2025, 3, 2)]
    public void Validar_aceita_periodos_na_janela(string de, string ate, int a1, int m1, int d1, int a2, int m2, int d2)
    {
        var resultado = Periodo.Validar(de, ate, Hoje);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(new Periodo(D(a1, m1, d1), D(a2, m2, d2)));
    }

    [Fact]
    public void Validar_sem_parametros_e_o_padrao()
    {
        Periodo.Validar(null, null, Hoje).Value.Should().Be(Periodo.Padrao(Hoje));
    }

    [Fact]
    public void O_primeiro_dia_da_janela_com_92_dias_e_aceito_e_93_dias_nao()
    {
        new Periodo(D(2024, 12, 1), D(2025, 3, 2)).QuantidadeDeDias.Should().Be(92);
        Periodo.Validar("2024-12-01", "2025-03-03", Hoje).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("2026-13-01", "2026-11-27")]
    [InlineData("27/11/2026", "2026-11-27")]
    [InlineData("", "2026-11-27")]
    [InlineData("2026-11-01", "")]
    [InlineData("2026-11-20", "2026-11-10")]
    [InlineData("2026-11-01", "2026-11-28")]
    [InlineData("2024-11-30", "2024-12-31")]
    [InlineData("2026-08-01", "2026-11-01")]
    public void Validar_recusa_com_PeriodoInvalido(string de, string ate)
    {
        var resultado = Periodo.Validar(de, ate, Hoje);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Copilot.CodigoPeriodoInvalido);
        resultado.Error.Message.Should().Contain("92 dias");
    }

    [Fact]
    public void Dias_lista_todas_as_datas_em_ordem()
    {
        new Periodo(D(2026, 10, 30), D(2026, 11, 2)).Dias.Should().Equal(
            D(2026, 10, 30), D(2026, 10, 31), D(2026, 11, 1), D(2026, 11, 2));
    }

    [Fact]
    public void IncluiMesAtual_quando_o_fim_chega_ao_mes_de_hoje()
    {
        new Periodo(D(2026, 10, 1), D(2026, 11, 1)).IncluiMesAtual(Hoje).Should().BeTrue();
        new Periodo(D(2026, 10, 1), D(2026, 10, 31)).IncluiMesAtual(Hoje).Should().BeFalse();
    }

    [Fact]
    public void Atalhos_na_ordem_com_os_periodos()
    {
        Periodo.Atalhos(Hoje).Should().Equal(
            new AtalhoDePeriodo("Hoje", new Periodo(Hoje, Hoje)),
            new AtalhoDePeriodo("Ontem", new Periodo(D(2026, 11, 26), D(2026, 11, 26))),
            new AtalhoDePeriodo("Últimos 7 dias", new Periodo(D(2026, 11, 21), Hoje)),
            new AtalhoDePeriodo("Últimos 30 dias", new Periodo(D(2026, 10, 29), Hoje)),
            new AtalhoDePeriodo("Mês atual", Periodo.Padrao(Hoje)),
            new AtalhoDePeriodo("Mês anterior", new Periodo(D(2026, 10, 1), D(2026, 10, 31))));
    }

    [Fact]
    public void Mes_anterior_termina_no_ultimo_dia_de_fevereiro()
    {
        Periodo.Atalhos(D(2026, 3, 15)).Single(a => a.Rotulo == "Mês anterior").Periodo
            .Should().Be(new Periodo(D(2026, 2, 1), D(2026, 2, 28)));
    }

    [Theory]
    [InlineData(25, true)]
    [InlineData(26, true)]
    [InlineData(27, true)]
    [InlineData(24, false)]
    public void EhRecente_nos_tres_ultimos_dias(int dia, bool recente)
    {
        Periodo.EhRecente(D(2026, 11, dia), Hoje).Should().Be(recente);
    }

    [Fact]
    public void EhPadrao_e_Texto()
    {
        Periodo.Padrao(Hoje).EhPadrao(Hoje).Should().BeTrue();
        new Periodo(D(2026, 11, 2), Hoje).EhPadrao(Hoje).Should().BeFalse();
        Periodo.Texto(D(2026, 1, 5)).Should().Be("2026-01-05");
    }
}
