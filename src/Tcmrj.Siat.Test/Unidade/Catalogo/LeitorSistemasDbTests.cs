using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Infra.Catalogo;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Conversão do <c>sistemasDb.js</c> (subconjunto de JavaScript) em JSON estrito (research.md R2,
/// contracts/origem-sistemasdb.md): o resultado tem o mesmo significado que o navegador dá ao arquivo.
/// </summary>
public class LeitorSistemasDbTests
{
    internal static string CaminhoDaAmostra =>
        Path.Combine(AppContext.BaseDirectory, "Unidade", "Catalogo", "Amostras", "sistemasDb.amostra.js");

    private static string Converter(string js)
    {
        var resultado = LeitorSistemasDb.ParaJson(js);
        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        return resultado.Value;
    }

    private static JsonElement Ler(string js)
    {
        using var documento = JsonDocument.Parse(Converter(js));
        return documento.RootElement.Clone();
    }

    // Valor do campo "v" do primeiro objeto da lista.
    private static string? Texto(string js) => Ler(js)[0].GetProperty("v").GetString();

    private static Erro Falha(string js)
    {
        var resultado = LeitorSistemasDb.ParaJson(js);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoFormatoInvalido);
        return resultado.Error;
    }

    [Fact]
    public void Descarta_BOM_declaracao_e_ponto_e_virgula()
    {
        var json = Converter("\uFEFFvar data = [ { \"Title\": \"A\" } ];\n");

        json.Should().NotContain("var").And.NotContain(";").And.NotContain("\uFEFF");
        Ler("\uFEFFvar data = [ { \"Title\": \"A\" } ];")[0].GetProperty("Title").GetString().Should().Be("A");
    }

    [Fact]
    public void Aceita_virgula_sobrando_em_lista_e_objeto()
    {
        var raiz = Ler("[ { \"Title\": \"A\", \"DSV\": [ { \"URL\": \"x\", }, ], }, ];");

        raiz.GetArrayLength().Should().Be(1);
        raiz[0].GetProperty("DSV")[0].GetProperty("URL").GetString().Should().Be("x");
    }

    [Fact]
    public void Poe_aspas_em_nome_de_campo_sem_aspas_e_aceita_aspas_simples_no_nome()
    {
        var raiz = Ler("[ { \"Title\": \"A\", OBS: \"Novo site\", 'Path': 'C:' } ]");

        raiz[0].GetProperty("OBS").GetString().Should().Be("Novo site");
        raiz[0].GetProperty("Path").GetString().Should().Be("C:");
    }

    [Fact]
    public void Aceita_texto_entre_aspas_simples_com_aspas_duplas_dentro()
    {
        Texto("[ { v: 'diz \"oi\"' } ]").Should().Be("diz \"oi\"");
    }

    [Fact]
    public void Aceita_comentarios_fora_de_texto_e_nao_confunde_barras_dentro_de_texto()
    {
        var raiz = Ler("""
            var data = [ // comentário de linha
            /* comentário
               de bloco */
            { "v": "https://x//y/*z*/", /* outro */ "w": 'a//b' }, // fim
            ];
            """);

        raiz.GetArrayLength().Should().Be(1);
        raiz[0].GetProperty("v").GetString().Should().Be("https://x//y/*z*/");
        raiz[0].GetProperty("w").GetString().Should().Be("a//b");
    }

    [Theory]
    [InlineData(@"\n", "\n")]
    [InlineData(@"\r", "\r")]
    [InlineData(@"\t", "\t")]
    [InlineData(@"\b", "\b")]
    [InlineData(@"\f", "\f")]
    [InlineData(@"\v", "\v")]
    [InlineData(@"\0", "\0")]
    [InlineData(@"\x41", "A")]
    [InlineData(@"\u00e7", "ç")]
    [InlineData(@"\u{1F600}", "😀")]
    [InlineData(@"\'", "'")]
    [InlineData(@"\""", "\"")]
    [InlineData(@"\\", @"\")]
    [InlineData("a\\\nb", "ab")]
    [InlineData("a\\\r\nb", "ab")]
    [InlineData(@"\D", "D")]
    [InlineData(@"\S", "S")]
    [InlineData(@"\8", "8")]
    [InlineData(@"\101", "A")]
    public void Traduz_escapes_como_o_JavaScript(string escapeNoArquivo, string esperado)
    {
        Texto($"[ {{ \"v\": \"{escapeNoArquivo}\" }} ]").Should().Be(esperado);
    }

    [Fact]
    public void Barra_antes_de_caractere_sem_significado_e_descartada()
    {
        Texto("""[ { "v": "G:\DESENVOLVIMENTO\VBSCP\\SCP" } ]""").Should().Be(@"G:DESENVOLVIMENTOVBSCP\SCP");
        Texto("""[ { "v": "W:\\TcmRoot\\\DSV" } ]""").Should().Be(@"W:\TcmRoot\DSV");
    }

    [Fact]
    public void Undefined_e_erro_de_formato_com_linha_e_coluna()
    {
        var erro = Falha("var data = [\n{\n  \"Title\": undefined\n}];");

        erro.Message.Should().Contain("undefined").And.Contain("(linha 3, coluna 12)");
    }

    [Fact]
    public void Concatenacao_e_erro_de_formato_na_posicao_do_operador()
    {
        var erro = Falha("[ { \"Title\": \"a\" + \"b\" } ]");

        erro.Message.Should().Contain("'+'").And.Contain("(linha 1, coluna 18)");
    }

    [Fact]
    public void Texto_sem_aspas_de_fechamento_e_erro_de_formato()
    {
        Falha("[ { \"Title\": \"a } ]").Message.Should().Contain("aspas").And.Contain("(linha 1, coluna 14)");
        Falha("[ { \"Title\": \"a\n\" } ]").Message.Should().Contain("aspas").And.Contain("(linha 1, coluna 14)");
    }

    [Fact]
    public void Falta_do_colchete_final_e_erro_de_formato()
    {
        var erro = Falha("var data = [\n{ \"Title\": \"a\" },\n");

        erro.Message.Should().Contain("]").And.Contain("linha 3");
    }

    [Fact]
    public void Arquivo_sem_lista_e_erro_de_formato()
    {
        Falha("var data = {};").Message.Should().Contain("[");
    }

    [Theory]
    [InlineData("""[ { "v": "\x4" } ]""")]
    [InlineData("""[ { "v": "\u00g1" } ]""")]
    [InlineData("""[ { "v": "\u{110000}" } ]""")]
    public void Escape_hexadecimal_incompleto_e_erro_de_formato(string js)
    {
        Falha(js).Message.Should().Contain("escape");
    }

    [Fact]
    public void Aninhamento_excessivo_e_erro_de_formato()
    {
        Falha(new string('[', 200) + new string(']', 200)).Message.Should().Contain("aninhad");
    }

    [Fact]
    public void A_amostra_do_arquivo_real_converte_em_JSON_valido()
    {
        var js = File.ReadAllText(CaminhoDaAmostra);

        var json = Converter(js);

        using var documento = JsonDocument.Parse(json);
        documento.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        documento.RootElement.GetArrayLength().Should().Be(10);
    }
}
