using AwesomeAssertions;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Endereços aceitos: só UNC absoluto e <c>http</c>/<c>https</c>; nunca caminho local do servidor do SIAT
/// (specs/003-versoes-publicadas/research.md R3).
/// </summary>
public sealed class ClassificadorDeEnderecoTests
{
    [Theory]
    [InlineData(@"\\tcm2016web3\TcmRoot\DSV\Sites\sgq_dsv\VERSAO.TXT")]
    [InlineData(@"\\unity\producao\prod\producao\VBSCP\SCP.INI")]
    [InlineData(@"\\unity\producao\prod\producao\VBSCP\v.{0}\Assinador\VERSAO.TXT")]
    public void Aceita_caminho_UNC(string endereco)
    {
        ClassificadorDeEndereco.Classificar(endereco).Should().Be(TipoDeEndereco.Unc);
    }

    [Theory]
    [InlineData("https://etcm.tcm.rj.gov.br/versao.txt")]
    [InlineData("http://x/versao.txt")]
    public void Aceita_http_e_https(string endereco)
    {
        ClassificadorDeEndereco.Classificar(endereco).Should().Be(TipoDeEndereco.Http);
    }

    [Theory]
    [InlineData(@"C:\inetpub\appsettings.json")]
    [InlineData(@"G:\x\VERSAO.TXT")]
    [InlineData("VERSAO.TXT")]
    [InlineData(@"..\x")]
    [InlineData("file://servidor/x")]
    [InlineData("ftp://x")]
    [InlineData(@"\\?\C:\x")]
    [InlineData(@"\\.\pipe\x")]
    [InlineData(@"\\servidor\x|y\VERSAO.TXT")]
    [InlineData("\\\\servidor\\x\u0001\\VERSAO.TXT")]
    [InlineData(@"\\servidor")]
    [InlineData(@"\\servidor\share\..\..\x")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Recusa_as_outras_formas(string? endereco)
    {
        ClassificadorDeEndereco.Classificar(endereco).Should().Be(TipoDeEndereco.NaoSuportado);
    }
}
