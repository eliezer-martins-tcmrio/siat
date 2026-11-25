using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Regras do script para o conteúdo do arquivo de versão e o marcador do sistema raiz
/// (specs/003-versoes-publicadas/research.md R2).
/// </summary>
public sealed class InterpretadorDeVersaoTests
{
    [Theory]
    [InlineData("1.4.2", "1.4.2")]
    [InlineData("1.4.2\r\n", "1.4.2")]
    [InlineData("  1.4.2  ", "1.4.2")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\r\n", "")]
    [InlineData(null, "")]
    [InlineData("versão nova", "versão nova")]
    [InlineData("[SCP]\r\nNOME=Sistema de Controle\r\nVERSAO=5.11.3\r\nBANCO=TCM", "5.11.3")]
    [InlineData("[SCP]\nNOME=Sistema de Controle\nversao=5.11.3", "5.11.3")]
    [InlineData("[SCP]\nVERSAO=5.11.3\nVERSAO=6.0.0", "5.11.3")]
    [InlineData("texto com mais de doze caracteres", "")]
    [InlineData("[SCP]\nVERSAO=5.11\nNOME=x", "")]
    [InlineData("1.4\n2.0.0.0.0.0", "")]
    public void Interpreta_o_conteudo(string? texto, string esperado)
    {
        InterpretadorDeVersao.Interpretar(texto).Should().Be(esperado);
    }

    [Fact]
    public void As_linhas_sao_unidas_por_espaco_antes_da_contagem_de_12_caracteres()
    {
        // "1.4 2.0" tem 7 caracteres: vale o texto unido.
        InterpretadorDeVersao.Interpretar("1.4\n2.0").Should().Be("1.4 2.0");
    }

    [Theory]
    [InlineData(@"\\unity\x\v.{0}\A\VERSAO.TXT", "5.11.3", @"\\unity\x\v.5.11.3\A\VERSAO.TXT")]
    [InlineData(@"\\unity\x\v.{0}\A\VERSAO.TXT", "", @"\\unity\x\v.\A\VERSAO.TXT")]
    [InlineData(@"\\unity\x\v.{0}\A\VERSAO.TXT", null, @"\\unity\x\v.\A\VERSAO.TXT")]
    [InlineData(@"\\unity\x\VERSAO.TXT", "5.11.3", @"\\unity\x\VERSAO.TXT")]
    [InlineData(@"\\unity\x\{1}\{0}\VERSAO.TXT", "5.11.3", @"\\unity\x\{1}\5.11.3\VERSAO.TXT")]
    [InlineData(@"\\unity\x\{\}\v.{0}", "1.0.0", @"\\unity\x\{\}\v.1.0.0")]
    public void MontarEndereco_troca_o_marcador_sem_lancar_excecao(string endereco, string? raiz, string esperado)
    {
        InterpretadorDeVersao.MontarEndereco(endereco, raiz).Should().Be(esperado);
    }
}
