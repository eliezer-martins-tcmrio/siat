using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Tcmrj.Siat.Infra.Conexoes;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// SQL do AuditLog igual, byte a byte e com LF, ao do Visor (R5): <c>logs.sql</c> = <c>SqlLogs</c> (com o
/// <c>OPTION (RECOMPILE)</c> do Visor) e <c>apps.sql</c> = consulta de aplicações de <c>/api/auditlog/apps</c>.
/// A referência é a cópia de <c>Tcmrj.Visor/Consultas.cs</c> em <c>Unidade/Referencias/Visor/Consultas.cs.txt</c>.
/// </summary>
public partial class SqlIgualAoVisorTests
{
    private const string ReferenciaVisorAuditLog = "Unidade/Referencias/Visor/Consultas.cs.txt";

    [Fact]
    public void Logs_sql_e_igual_ao_SqlLogs_do_Visor()
    {
        var esperado = RawStringDoVisorAuditLog("SqlLogs");

        esperado.Should().EndWith("OPTION (RECOMPILE)");
        BytesDoSqlAuditLog("logs.sql").Should().Equal(Encoding.UTF8.GetBytes(esperado));
    }

    [Fact]
    public void Apps_sql_e_igual_a_consulta_de_aplicacoes_do_Visor()
    {
        var visor = LerReferenciaVisorAuditLog();
        var consulta = Regex.Match(visor, @"""(SELECT Id, Name FROM [^""]*)""");

        consulta.Success.Should().BeTrue("o Visor lista as aplicações em /api/auditlog/apps");
        consulta.Groups[1].Value.Should().Be("SELECT Id, Name FROM CSLIB_UserManager.dbo.Apps ORDER BY Name");
        BytesDoSqlAuditLog("apps.sql").Should().Equal(Encoding.UTF8.GetBytes(consulta.Groups[1].Value));
    }

    [Theory]
    [InlineData("logs.sql")]
    [InlineData("apps.sql")]
    public void Sql_do_AuditLog_usa_LF_sem_BOM_e_sem_quebra_final(string arquivo)
    {
        var bytes = BytesDoSqlAuditLog(arquivo);

        bytes.Should().NotContain((byte)'\r');
        bytes.Take(3).Should().NotEqual(Encoding.UTF8.Preamble.ToArray());
        bytes[^1].Should().NotBe((byte)'\n');
    }

    private static byte[] BytesDoSqlAuditLog(string arquivo)
    {
        var assembly = typeof(PadraoLike).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Sql.{arquivo}", StringComparison.Ordinal));
        using var recurso = assembly.GetManifestResourceStream(nome)!;
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }

    /// <summary>
    /// Conteúdo de uma raw string literal (<c>"""</c>) do Visor, com a regra do C#: a indentação da linha de
    /// fechamento sai de cada linha, e as quebras entre as linhas são LF (as do arquivo do Visor).
    /// </summary>
    private static string RawStringDoVisorAuditLog(string constante)
    {
        var linhas = LerReferenciaVisorAuditLog().Split('\n');
        var inicio = Array.FindIndex(linhas, l => l.Contains($"string {constante} = \"\"\"", StringComparison.Ordinal));
        inicio.Should().BeGreaterThanOrEqualTo(0, $"o Visor declara {constante}");

        var fim = Array.FindIndex(linhas, inicio + 1, l => l.Trim() == "\"\"\";");
        var indentacao = linhas[fim][..linhas[fim].IndexOf('"', StringComparison.Ordinal)];

        var conteudo = linhas[(inicio + 1)..fim].Select(l =>
        {
            if (l.StartsWith(indentacao, StringComparison.Ordinal))
            {
                return l[indentacao.Length..];
            }

            l.Should().BeNullOrWhiteSpace("toda linha da raw string tem a indentação do fechamento");
            return string.Empty;
        });

        return string.Join('\n', conteudo);
    }

    /// <summary>Lê a cópia do Visor com LF (o Git pode tê-la convertido para CRLF no checkout).</summary>
    private static string LerReferenciaVisorAuditLog()
    {
        var caminho = LocalizarReferenciaVisorAuditLog();
        return File.ReadAllText(caminho, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string LocalizarReferenciaVisorAuditLog()
    {
        var naSaida = Path.Combine(AppContext.BaseDirectory, ReferenciaVisorAuditLog);
        if (File.Exists(naSaida))
        {
            return naSaida;
        }

        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var candidato = Path.Combine(pasta.FullName, "Tcmrj.Siat.Test", ReferenciaVisorAuditLog);
            if (File.Exists(candidato))
            {
                return candidato;
            }

            candidato = Path.Combine(pasta.FullName, ReferenciaVisorAuditLog);
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new FileNotFoundException($"Cópia de referência do Visor não encontrada: {ReferenciaVisorAuditLog}");
    }
}
