using AwesomeAssertions;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;

namespace Tcmrj.Siat.Test.Unidade.AuditLog;

/// <summary>LogEvent do detalhe do AuditLog (R11, FR-015): JSON indentado e realçado; inválido volta como veio.</summary>
public class FormatadorLogEventTests
{
    private readonly FormatadorLogEvent _formatador = new();

    [Fact]
    public void Json_valido_e_indentado_com_dois_espacos_e_LF()
    {
        var resultado = _formatador.Formatar("""{"Usuario":"ana.souza","Tentativa":2,"Lista":[1,true,null]}""");

        resultado.EhJson.Should().BeTrue();
        resultado.Texto.Should().Be(
            "{\n" +
            "  \"Usuario\": \"ana.souza\",\n" +
            "  \"Tentativa\": 2,\n" +
            "  \"Lista\": [\n" +
            "    1,\n" +
            "    true,\n" +
            "    null\n" +
            "  ]\n" +
            "}");
    }

    [Fact]
    public void Realca_por_tipo_de_token()
    {
        var resultado = _formatador.Formatar("""{"Usuario":"ana","Tentativa":2.5e3,"Mfa":false,"Extra":null}""");

        Trechos(resultado, TipoTrechoJson.Chave).Should().Equal("\"Usuario\"", "\"Tentativa\"", "\"Mfa\"", "\"Extra\"");
        Trechos(resultado, TipoTrechoJson.Texto).Should().Equal("\"ana\"");
        Trechos(resultado, TipoTrechoJson.Numero).Should().Equal("2.5e3");
        Trechos(resultado, TipoTrechoJson.Literal).Should().Equal("false", "null");
    }

    [Fact]
    public void Trechos_juntos_formam_o_texto_formatado()
    {
        var resultado = _formatador.Formatar("""{"a":{"b":[1,"x\"y",-3]},"c":"123"}""");

        string.Concat(resultado.Trechos.Select(t => t.Texto)).Should().Be(resultado.Texto);
        Trechos(resultado, TipoTrechoJson.Texto).Should().Equal("\"x\\\"y\"", "\"123\"");
        Trechos(resultado, TipoTrechoJson.Numero).Should().Equal("1", "-3");
    }

    [Fact]
    public void Mantem_acentos_sem_escapar()
    {
        var resultado = _formatador.Formatar("""{"Mensagem":"Ação concluída <ok>"}""");

        resultado.Texto.Should().Contain("\"Ação concluída <ok>\"");
    }

    [Theory]
    [InlineData("não é JSON")]
    [InlineData("{\"a\": 1")]
    [InlineData("{'a': 1}")]
    public void Invalido_devolve_o_texto_original_sem_excecao(string texto)
    {
        var resultado = _formatador.Formatar(texto);

        resultado.EhJson.Should().BeFalse();
        resultado.Texto.Should().Be(texto);
        resultado.Trechos.Should().ContainSingle().Which.Should().Be(new TrechoJson(TipoTrechoJson.Simples, texto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Vazio_nao_tem_trechos(string? texto)
    {
        var resultado = _formatador.Formatar(texto);

        resultado.EhJson.Should().BeFalse();
        resultado.Texto.Should().BeEmpty();
        resultado.Trechos.Should().BeEmpty();
    }

    private static List<string> Trechos(LogEventFormatado resultado, TipoTrechoJson tipo) =>
        resultado.Trechos.Where(t => t.Tipo == tipo).Select(t => t.Texto).ToList();
}
