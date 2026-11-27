using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Faixas do uso do limite no mês atual (specs/005-copilot-meu-consumo, FR-016; data-model.md).</summary>
public sealed class UsoDoLimiteTests
{
    private const string Mes = "2026-11";

    [Fact]
    public void Oitenta_por_cento_e_atencao()
    {
        UsoDoLimite.Calcular(40, new LimiteVigente.Individual(50), Mes)
            .Should().Be(new UsoDoLimite(FaixaDeUso.Atencao, 0.8m, 10, Mes));
    }

    [Fact]
    public void Abaixo_de_oitenta_por_cento_e_normal()
    {
        UsoDoLimite.Calcular(39.99m, new LimiteVigente.Padrao(50), Mes).Faixa.Should().Be(FaixaDeUso.Normal);
    }

    [Theory]
    [InlineData(50, 0)]
    [InlineData(60, -10)]
    public void No_limite_ou_acima_e_atingido(decimal consumo, decimal restante)
    {
        var uso = UsoDoLimite.Calcular(consumo, new LimiteVigente.Padrao(50), Mes);

        uso.Faixa.Should().Be(FaixaDeUso.Atingido);
        uso.Restante.Should().Be(restante);
    }

    [Fact]
    public void Limite_zero_nao_tem_razao()
    {
        UsoDoLimite.Calcular(3, new LimiteVigente.Individual(0), Mes)
            .Should().Be(new UsoDoLimite(FaixaDeUso.LimiteZero, null, -3, Mes));
    }

    [Fact]
    public void Sem_consumo_ou_sem_limite_unico_e_indisponivel()
    {
        UsoDoLimite.Calcular(null, new LimiteVigente.Padrao(50), Mes).Faixa.Should().Be(FaixaDeUso.Indisponivel);
        UsoDoLimite.Calcular(10, new LimiteVigente.Ambiguo(), Mes).Faixa.Should().Be(FaixaDeUso.Indisponivel);
        UsoDoLimite.Calcular(10, new LimiteVigente.Nenhum(), Mes).Faixa.Should().Be(FaixaDeUso.Indisponivel);
        UsoDoLimite.Calcular(10, null, Mes).Should().Be(new UsoDoLimite(FaixaDeUso.Indisponivel, null, null, Mes));
    }
}
