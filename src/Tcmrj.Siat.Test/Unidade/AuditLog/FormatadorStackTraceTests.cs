using AwesomeAssertions;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;

namespace Tcmrj.Siat.Test.Unidade.AuditLog;

/// <summary>Stack trace do detalhe do AuditLog (R11, FR-015): quebra por linha e realce das linhas.</summary>
public class FormatadorStackTraceTests
{
    private const string Exemplo =
        "System.InvalidOperationException: O modelo de documento não foi encontrado.\r\n" +
        "   at Tcmrj.Etcm.Documentos.GeradorPdf.Gerar(Despacho despacho, CancellationToken ct) in D:\\src\\etcm\\Documentos\\GeradorPdf.cs:line 84\r\n" +
        " ---> System.IO.FileNotFoundException: Could not find file 'modelo.docx'.\r\n" +
        "   at System.IO.FileStream.ValidateFileHandle(SafeFileHandle handle)\r\n" +
        "   --- End of inner exception stack trace ---\r\n" +
        "Linha qualquer\r\n";

    private readonly FormatadorStackTrace _formatador = new();

    [Fact]
    public void Quebra_por_linha_e_preserva_o_texto_de_cada_linha()
    {
        var linhas = _formatador.Formatar(Exemplo);

        linhas.Select(l => l.Texto).Should().Equal(Exemplo.TrimEnd().Split("\r\n"));
    }

    [Fact]
    public void Aceita_quebras_LF()
    {
        var linhas = _formatador.Formatar("System.Exception: falhou\n   at A.B.C()");

        linhas.Should().HaveCount(2);
        linhas[1].Tipo.Should().Be(TipoLinhaStackTrace.Frame);
    }

    [Fact]
    public void Classifica_cada_linha()
    {
        var linhas = _formatador.Formatar(Exemplo);

        linhas.Select(l => l.Tipo).Should().Equal(
            TipoLinhaStackTrace.Mensagem,
            TipoLinhaStackTrace.Frame,
            TipoLinhaStackTrace.Mensagem,
            TipoLinhaStackTrace.Frame,
            TipoLinhaStackTrace.Separador,
            TipoLinhaStackTrace.Simples);
    }

    [Fact]
    public void Realca_o_tipo_da_excecao_e_a_mensagem()
    {
        var linha = _formatador.Formatar(Exemplo)[0];

        Trecho(linha, TipoTrechoStackTrace.TipoExcecao).Should().Be("System.InvalidOperationException:");
        Trecho(linha, TipoTrechoStackTrace.Mensagem).Should().Be("O modelo de documento não foi encontrado.");
    }

    [Fact]
    public void Realca_a_excecao_interna()
    {
        var linha = _formatador.Formatar(Exemplo)[2];

        linha.Trechos[0].Should().Be(new TrechoStackTrace(TipoTrechoStackTrace.Simples, " ---> "));
        Trecho(linha, TipoTrechoStackTrace.TipoExcecao).Should().Be("System.IO.FileNotFoundException:");
    }

    [Fact]
    public void Realca_as_partes_da_linha_at_com_arquivo_e_linha()
    {
        var linha = _formatador.Formatar(Exemplo)[1];

        Trechos(linha, TipoTrechoStackTrace.At).Should().Equal("at", "in");
        Trecho(linha, TipoTrechoStackTrace.Namespace).Should().Be("Tcmrj.Etcm.Documentos.GeradorPdf.");
        Trecho(linha, TipoTrechoStackTrace.Metodo).Should().Be("Gerar");
        Trechos(linha, TipoTrechoStackTrace.TipoParametro).Should().Equal("Despacho", "CancellationToken");
        Trechos(linha, TipoTrechoStackTrace.Parametro).Should().Equal("despacho", "ct");
        Trecho(linha, TipoTrechoStackTrace.Arquivo).Should().Be("D:\\src\\etcm\\Documentos\\GeradorPdf.cs");
        Trecho(linha, TipoTrechoStackTrace.RotuloLinha).Should().Be("line");
        Trecho(linha, TipoTrechoStackTrace.NumeroLinha).Should().Be("84");
        linha.ProprioCodigo.Should().BeTrue();
    }

    [Fact]
    public void Linha_at_sem_arquivo_nao_tem_arquivo()
    {
        var linha = _formatador.Formatar(Exemplo)[3];

        Trecho(linha, TipoTrechoStackTrace.Metodo).Should().Be("ValidateFileHandle");
        Trechos(linha, TipoTrechoStackTrace.Arquivo).Should().BeEmpty();
        linha.ProprioCodigo.Should().BeFalse();
    }

    [Fact]
    public void Virgulas_de_genericos_nao_separam_parametros()
    {
        var linha = _formatador.Formatar("   at A.B.C(Dictionary<String, Int32> mapa, Int32 n)")[0];

        Trechos(linha, TipoTrechoStackTrace.TipoParametro).Should().Equal("Dictionary<String, Int32>", "Int32");
        Trechos(linha, TipoTrechoStackTrace.Parametro).Should().Equal("mapa", "n");
        linha.Texto.Should().Be("   at A.B.C(Dictionary<String, Int32> mapa, Int32 n)");
    }

    [Fact]
    public void Reconhece_o_stack_trace_em_portugues()
    {
        var linha = _formatador.Formatar("   em Tcmrj.X.Repositorio.Obter(Int32 id) na C:\\src\\Repositorio.cs:linha 42")[0];

        linha.Tipo.Should().Be(TipoLinhaStackTrace.Frame);
        Trechos(linha, TipoTrechoStackTrace.At).Should().Equal("em", "na");
        Trecho(linha, TipoTrechoStackTrace.Arquivo).Should().Be("C:\\src\\Repositorio.cs");
        Trecho(linha, TipoTrechoStackTrace.RotuloLinha).Should().Be("linha");
        Trecho(linha, TipoTrechoStackTrace.NumeroLinha).Should().Be("42");
    }

    [Fact]
    public void Metodo_sem_namespace_e_sem_parametros()
    {
        var linha = _formatador.Formatar("at Main()")[0];

        Trechos(linha, TipoTrechoStackTrace.Namespace).Should().BeEmpty();
        Trecho(linha, TipoTrechoStackTrace.Metodo).Should().Be("Main");
        Trechos(linha, TipoTrechoStackTrace.Parametro).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public void Vazio_nao_tem_linhas(string? texto) =>
        _formatador.Formatar(texto).Should().BeEmpty();

    private static string Trecho(LinhaStackTrace linha, TipoTrechoStackTrace tipo) =>
        Trechos(linha, tipo).Should().ContainSingle().Subject;

    private static List<string> Trechos(LinhaStackTrace linha, TipoTrechoStackTrace tipo) =>
        linha.Trechos.Where(t => t.Tipo == tipo).Select(t => t.Texto).ToList();
}
