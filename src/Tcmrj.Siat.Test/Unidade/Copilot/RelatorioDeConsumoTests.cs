using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Relatório de um dia ou do mês (specs/005-copilot-meu-consumo/research.md R5, regras do <c>usage.ts</c> do
/// gestao-copilot): unidade, somas, mescla e ordem dos modelos.
/// </summary>
public sealed class RelatorioDeConsumoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);

    internal static ItemDeConsumo Item(string? modelo, decimal creditos, decimal bruto, decimal liquido, string unidade = "credits") =>
        new(modelo, unidade, creditos, bruto, liquido);

    [Theory]
    [InlineData("credits")]
    [InlineData("ai-credits")]
    [InlineData("AI-Credits")]
    public void Unidades_aceitas(string unidade)
    {
        RelatorioDeConsumo.De([Item("GPT-5", 1, 0.04m, 0.04m, unidade)], Agora).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Outra_unidade_e_RespostaInesperada()
    {
        var resultado = RelatorioDeConsumo.De([Item("GPT-5", 1, 1, 1), Item("GPT-5", 1, 1, 1, "minutes")], Agora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Copilot.CodigoRespostaInesperada);
    }

    [Fact]
    public void Soma_creditos_e_valores()
    {
        var relatorio = RelatorioDeConsumo.De(
            [Item("GPT-5", 10, 0.40m, 0.30m), Item("Claude", 5.5m, 0.22m, 0.20m)], Agora).Value;

        relatorio.Total.Should().Be(new Consumo(15.5m, 0.62m, 0.50m));
        relatorio.LidoEm.Should().Be(Agora);
    }

    [Fact]
    public void Modelos_com_a_mesma_chave_viram_uma_linha_com_a_primeira_grafia_sem_espacos()
    {
        var relatorio = RelatorioDeConsumo.De([Item(" GPT-5 ", 2, 0.08m, 0.08m), Item("gpt-5", 3, 0.12m, 0.10m)], Agora).Value;

        relatorio.Modelos.Should().ContainSingle()
            .Which.Should().Be(new ConsumoPorModelo("GPT-5", new Consumo(5, 0.20m, 0.18m)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Modelo_vazio_e_Modelo_nao_informado(string? modelo)
    {
        var linha = RelatorioDeConsumo.De([Item(modelo, 1, 1, 1)], Agora).Value.Modelos.Single();

        linha.Modelo.Should().BeNull();
        linha.Rotulo.Should().Be("Modelo não informado");
    }

    [Fact]
    public void Ordena_por_creditos_decrescentes_e_no_empate_pelo_nome_em_pt_BR()
    {
        var relatorio = RelatorioDeConsumo.De(
            [Item("Ômega", 2, 0, 0), Item("beta", 2, 0, 0), Item("Zeta", 9, 0, 0), Item("Alfa", 2, 0, 0)], Agora).Value;

        relatorio.Modelos.Select(m => m.Modelo).Should().Equal("Zeta", "Alfa", "beta", "Ômega");
    }

    [Fact]
    public void Lista_vazia_tem_zeros_e_nenhum_modelo()
    {
        var relatorio = RelatorioDeConsumo.De([], Agora).Value;

        relatorio.Total.Should().Be(Consumo.Zero);
        relatorio.Modelos.Should().BeEmpty();
    }
}
