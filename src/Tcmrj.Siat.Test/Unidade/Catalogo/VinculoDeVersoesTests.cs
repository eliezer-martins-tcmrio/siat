using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Vínculo de um item do catálogo com os registros de <c>Apps_VersionAddresses</c> (specs/004-ficha-sistema/data-model.md,
/// VinculoDeVersoes; FR-009, FR-010).
/// </summary>
public sealed class VinculoDeVersoesTests
{
    [Fact]
    public void Nomes_sem_espacos_nas_pontas_sem_vazios_sem_repetidos_na_ordem_do_campo()
    {
        var vinculo = VinculoDeVersoes.De(["SIGARH", " ", null, "sigarh", "SIGARH Agendador "]);

        vinculo.Nomes.Should().Equal("SIGARH", "SIGARH Agendador");
        vinculo.Invalido.Should().BeFalse();
        vinculo.Vazio.Should().BeFalse();
    }

    [Fact]
    public void Fica_a_primeira_grafia_de_um_nome_repetido()
    {
        VinculoDeVersoes.De(["eTCM Impressão", "ETCM IMPRESSAO"]).Nomes.Should().Equal("eTCM Impressão");
    }

    [Fact]
    public void Sem_nome_que_sobre_o_vinculo_e_vazio_e_valido()
    {
        var vinculo = VinculoDeVersoes.De(["", "  ", null]);

        vinculo.Vazio.Should().BeTrue();
        vinculo.Invalido.Should().BeFalse();
        VinculoDeVersoes.De([]).Vazio.Should().BeTrue();
    }

    [Fact]
    public void Chave_sao_os_nomes_normalizados_na_ordem_unidos_por_quebra_de_linha()
    {
        VinculoDeVersoes.De(["SIGARH", "SIGARH Agendador"]).Chave.Should().Be("sigarh\nsigarh agendador");
        VinculoDeVersoes.De(["SIGARH Agendador", "SIGARH"]).Chave.Should().Be("sigarh agendador\nsigarh");
        VinculoDeVersoes.Nenhum.Chave.Should().BeEmpty();
    }

    [Fact]
    public void Contem_compara_pelo_nome_normalizado()
    {
        var vinculo = VinculoDeVersoes.De(["SIGARH", "SIGARH Agendador"]);

        vinculo.Contem("  sigarh agendador").Should().BeTrue();
        vinculo.Contem("SIGARH Relatórios").Should().BeFalse();
    }

    [Fact]
    public void Nenhum_e_valido_e_vazio()
    {
        VinculoDeVersoes.Nenhum.Nomes.Should().BeEmpty();
        VinculoDeVersoes.Nenhum.Invalido.Should().BeFalse();
        VinculoDeVersoes.Nenhum.Vazio.Should().BeTrue();
    }

    [Fact]
    public void ForaDoFormato_e_invalido_e_sem_nomes()
    {
        VinculoDeVersoes.ForaDoFormato.Invalido.Should().BeTrue();
        VinculoDeVersoes.ForaDoFormato.Nomes.Should().BeEmpty();
        VinculoDeVersoes.ForaDoFormato.Contem("SIGARH").Should().BeFalse();
    }
}
