using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Budgets considerados e limite vigente da pessoa (specs/005-copilot-meu-consumo, FR-015; data-model.md): exceção
/// individual, padrão da organização, ambíguo ou nenhum; o global nunca é o limite.
/// </summary>
public sealed class LimiteVigenteTests
{
    private static readonly string[] AiCredits = [Budget.SkuAiCredits];

    private static Budget B(string escopo, decimal valor, string? usuario = null, string[]? skus = null, string? entidade = null) =>
        Budget.Normalizar(Guid.NewGuid().ToString(), escopo, valor, skus ?? AiCredits, usuario, entidade)!;

    [Theory]
    [InlineData("organization", EscopoDeBudget.Organizacao)]
    [InlineData("multi_user_customer", EscopoDeBudget.Padrao)]
    [InlineData("user", EscopoDeBudget.Individual)]
    public void Normalizar_aceita_os_tres_escopos_de_ai_credits(string escopo, EscopoDeBudget esperado)
    {
        B(escopo, 10, "ana").Escopo.Should().Be(esperado);
    }

    [Fact]
    public void Normalizar_ignora_outros_skus_e_escopos()
    {
        Budget.Normalizar("1", "user", 10, ["copilot_premium_request"], "ana", null).Should().BeNull();
        Budget.Normalizar("1", "enterprise", 10, AiCredits, null, null).Should().BeNull();
        Budget.Normalizar("1", "repository", 10, AiCredits, null, null).Should().BeNull();
    }

    [Fact]
    public void Normalizar_individual_usa_o_usuario_ou_o_nome_da_entidade()
    {
        B("user", 10, "ana").Usuario.Should().Be("ana");
        B("user", 10, null, entidade: "bia").Usuario.Should().Be("bia");
        B("multi_user_customer", 10, "ana").Usuario.Should().BeNull();
    }

    [Fact]
    public void Excecao_individual_com_login_em_outra_caixa()
    {
        LimiteVigente.Calcular([B("organization", 1000), B("multi_user_customer", 50), B("user", 80, "Ana-M")], "ana-m")
            .Should().Be(new LimiteVigente.Individual(80));
    }

    [Fact]
    public void Sem_excecao_vale_o_padrao()
    {
        var limite = LimiteVigente.Calcular([B("organization", 1000), B("multi_user_customer", 50), B("user", 80, "bia")], "ana");

        limite.Should().Be(new LimiteVigente.Padrao(50));
        limite.Valor.Should().Be(50);
    }

    [Fact]
    public void Duas_excecoes_sao_ambiguas()
    {
        LimiteVigente.Calcular([B("user", 80, "ana"), B("user", 90, "ANA"), B("multi_user_customer", 50)], "ana")
            .Should().BeOfType<LimiteVigente.Ambiguo>().Which.Valor.Should().BeNull();
    }

    [Fact]
    public void Dois_padroes_sem_excecao_sao_ambiguos()
    {
        LimiteVigente.Calcular([B("multi_user_customer", 50), B("multi_user_customer", 60)], "ana")
            .Should().BeOfType<LimiteVigente.Ambiguo>();
    }

    [Fact]
    public void So_o_global_e_nenhum()
    {
        LimiteVigente.Calcular([B("organization", 1000)], "ana").Should().BeOfType<LimiteVigente.Nenhum>()
            .Which.Valor.Should().BeNull();
        LimiteVigente.Calcular([], "ana").Should().BeOfType<LimiteVigente.Nenhum>();
    }
}
