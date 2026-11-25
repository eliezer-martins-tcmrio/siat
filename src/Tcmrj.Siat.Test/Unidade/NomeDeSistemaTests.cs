using System.Globalization;
using AwesomeAssertions;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// Comparação de nomes de sistema (specs/004-ficha-sistema/research.md R2): sem espaços nas pontas, sem acentos e em
/// minúsculas pela pt-BR, com os espaços internos preservados (FR-008, FR-009, FR-012).
/// </summary>
public sealed class NomeDeSistemaTests
{
    [Theory]
    [InlineData("  eTCM Impressão ", "etcm impressao")]
    [InlineData("SIGARH", "sigarh")]
    [InlineData("SIGARH  Agendador", "sigarh  agendador")]
    [InlineData("Ação", "acao")]
    [InlineData("", "")]
    public void Normaliza_o_nome(string nome, string esperado)
    {
        NomeDeSistema.Normalizar(nome).Should().Be(esperado);
    }

    [Fact]
    public void Nome_nulo_lanca()
    {
        var acao = () => NomeDeSistema.Normalizar(null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Minusculas_sempre_pela_pt_BR_qualquer_que_seja_a_cultura_atual()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            NomeDeSistema.Normalizar("SIGARH ITBI").Should().Be("sigarh itbi");
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Theory]
    [InlineData("eTCM Impressão")]
    [InlineData("SCP - Multas")]
    [InlineData("Ação")]
    public void A_busca_da_tela_de_Versoes_usa_a_mesma_normalizacao(string nome)
    {
        SistemaVersoesVM.Normalizar(nome).Should().Be(NomeDeSistema.Normalizar(nome));
    }
}
