using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Tcmrj.Siat.Infra.Conexoes;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// O SQL embutido no Infra é idêntico, byte a byte e com LF, ao do Visor (R5): o plano em cache no servidor depende
/// do texto exato. A referência é a cópia de <c>Tcmrj.Visor/Consultas.cs</c> em
/// <c>Unidade/Referencias/Visor/Consultas.cs.txt</c> (não compilada; <c>-text</c> no <c>.gitattributes</c>).
/// Classe parcial: o AuditLog fica em <c>SqlIgualAoVisorTests.AuditLog.cs</c>.
/// </summary>
public partial class SqlIgualAoVisorTests
{
    private const string ReferenciaVisor = "Unidade/Referencias/Visor/Consultas.cs.txt";

    private const string ConsultasDoVisorOriginal = @"C:\Projetos\visor\src\Tcmrj.Visor\Consultas.cs";

    [Fact]
    public void Mensagens_sql_e_igual_ao_SqlMensagens_do_Visor()
    {
        var esperado = RawStringDoVisor("SqlMensagens");

        esperado.Should().StartWith("SELECT TOP (@Top)").And.EndWith("ORDER BY m.DatePosted DESC");
        BytesDoSqlEmbutido("mensagens.sql").Should().Equal(Encoding.UTF8.GetBytes(esperado));
    }

    [Fact]
    public void Smtps_sql_e_igual_a_consulta_de_SMTPs_do_Visor()
    {
        var consulta = Regex.Match(LerReferenciaVisor(), @"""(SELECT SmtpId, Description FROM [^""]*)""");

        consulta.Success.Should().BeTrue("o Visor lista os SMTPs em /api/mailqueue/smtps");
        consulta.Groups[1].Value.Should().Be("SELECT SmtpId, Description FROM dbo.SMTPs ORDER BY SmtpId");
        BytesDoSqlEmbutido("smtps.sql").Should().Equal(Encoding.UTF8.GetBytes(consulta.Groups[1].Value));
    }

    [Theory]
    [InlineData("mensagens.sql")]
    [InlineData("smtps.sql")]
    public void Sql_da_MailQueue_usa_LF_sem_BOM_e_sem_quebra_final(string arquivo)
    {
        var bytes = BytesDoSqlEmbutido(arquivo);

        bytes.Should().NotContain((byte)'\r');
        bytes.Take(3).Should().NotEqual(Encoding.UTF8.Preamble.ToArray());
        bytes[^1].Should().NotBe((byte)'\n');
    }

    [Fact]
    public void Copia_de_referencia_e_igual_ao_Consultas_cs_do_Visor_quando_ele_esta_na_maquina()
    {
        Assert.SkipUnless(File.Exists(ConsultasDoVisorOriginal), $"Repositório do Visor ausente ({ConsultasDoVisorOriginal}).");

        var original = File.ReadAllText(ConsultasDoVisorOriginal, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);

        LerReferenciaVisor().Should().Be(original, "a cópia em Referencias/Visor deve acompanhar o Visor");
    }

    private static byte[] BytesDoSqlEmbutido(string arquivo)
    {
        var assembly = typeof(PadraoLike).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Sql.{arquivo}", StringComparison.Ordinal));
        using var recurso = assembly.GetManifestResourceStream(nome)!;
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }

    /// <summary>
    /// Conteúdo de uma raw string literal (<c>"""</c>) do Visor pela regra do C#: sem a primeira e a última quebra,
    /// com a indentação da linha de fechamento retirada de cada linha e as linhas unidas por LF.
    /// </summary>
    private static string RawStringDoVisor(string constante)
    {
        var linhas = LerReferenciaVisor().Split('\n');
        var inicio = Array.FindIndex(linhas, l => l.Contains($"string {constante} = \"\"\"", StringComparison.Ordinal));
        inicio.Should().BeGreaterThanOrEqualTo(0, $"o Visor declara {constante}");

        var fim = Array.FindIndex(linhas, inicio + 1, l => l.Trim() == "\"\"\";");
        fim.Should().BeGreaterThan(inicio, $"a raw string {constante} é fechada");
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

    /// <summary>Lê a cópia do Visor com LF (protege contra uma conversão para CRLF no checkout).</summary>
    private static string LerReferenciaVisor() =>
        File.ReadAllText(LocalizarReferenciaVisor(), Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string LocalizarReferenciaVisor()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            foreach (var candidato in new[]
                     {
                         Path.Combine(pasta.FullName, ReferenciaVisor),
                         Path.Combine(pasta.FullName, "Tcmrj.Siat.Test", ReferenciaVisor),
                         Path.Combine(pasta.FullName, "src", "Tcmrj.Siat.Test", ReferenciaVisor),
                     })
            {
                if (File.Exists(candidato))
                {
                    return candidato;
                }
            }
        }

        throw new FileNotFoundException($"Cópia de referência do Visor não encontrada: {ReferenciaVisor}");
    }
}
