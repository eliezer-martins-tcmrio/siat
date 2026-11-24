using System.Text.RegularExpressions;

namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>
/// Regras do <c>VersaoDeSistemasPublicados.ps1</c> para o conteúdo de um arquivo de versão (research.md R2), puras e
/// sem rede:
/// <list type="number">
/// <item>As linhas viram um texto só, unidas por espaço, como o <c>Get-Content</c> convertido em texto; as pontas
/// perdem os espaços.</item>
/// <item>Até 12 caracteres, o texto é a versão. Acima disso, vale o primeiro <c>VERSAO=x.y.z</c> (sem diferenciar
/// maiúsculas, como o <c>-match</c> do PowerShell); sem ele, a versão é vazia.</item>
/// <item>Versão vazia é "não configurado" (esclarecimento de 2026-11-24).</item>
/// </list>
/// Diferenças aceitas em relação ao script: a mesma regra vale para <c>http</c>/<c>https</c> (o script mostra a
/// resposta crua; tirar a quebra de linha evita divergência falsa) e o <c>{0}</c> é trocado por texto simples, sem
/// <c>string.Format</c>, para outras chaves no caminho não derrubarem a leitura.
/// </summary>
public static partial class InterpretadorDeVersao
{
    /// <summary>Acima deste tamanho, o texto só vale pelo <c>VERSAO=</c>.</summary>
    public const int TamanhoMaximoDoTexto = 12;

    /// <summary>Marcador do endereço preenchido com a versão do sistema raiz.</summary>
    public const string Marcador = "{0}";

    private static readonly string[] QuebrasDeLinha = ["\r\n", "\n", "\r"];

    /// <summary>Versão contida em <paramref name="texto"/>; vazio quando não há.</summary>
    public static string Interpretar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        var unido = string.Join(' ', texto.Split(QuebrasDeLinha, StringSplitOptions.None)).Trim();
        if (unido.Length <= TamanhoMaximoDoTexto)
        {
            return unido;
        }

        var achado = Versao().Match(unido);
        return achado.Success ? achado.Groups[1].Value : string.Empty;
    }

    /// <summary>
    /// Troca o <see cref="Marcador"/> de <paramref name="endereco"/> pela <paramref name="versaoRaiz"/> (vazia monta
    /// <c>v.</c>, como o script). Sem marcador, devolve o endereço como está.
    /// </summary>
    public static string MontarEndereco(string endereco, string? versaoRaiz)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        return endereco.Replace(Marcador, versaoRaiz ?? string.Empty, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"VERSAO=(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Versao();
}
