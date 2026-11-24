using System.Buffers;

namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>Forma de um endereço da tabela (research.md R3).</summary>
public enum TipoDeEndereco
{
    /// <summary>Caminho de rede absoluto (<c>\\servidor\compartilhamento\...</c>).</summary>
    Unc,

    /// <summary>URI absoluta <c>http</c> ou <c>https</c>.</summary>
    Http,

    /// <summary>Qualquer outra forma: a célula mostra "Endereço não suportado" sem tentar ler.</summary>
    NaoSuportado,
}

/// <summary>
/// Aceita só caminho UNC absoluto e URI <c>http</c>/<c>https</c> (research.md R3). A tabela é editável por quem
/// administra o UserManager de cada ambiente: sem a restrição, um endereço como <c>C:\inetpub\...\appsettings.json</c>
/// faria o servidor do SIAT ler um arquivo local dele mesmo. Fica no Domain, puro, para o <c>LeitorDeVersoes</c> do App
/// classificar sem depender do Infra.
/// </summary>
public static class ClassificadorDeEndereco
{
    public const string MensagemNaoSuportado = "Endereço não suportado.";

    // Caracteres que não podem estar num caminho do Windows (além dos de controle).
    private static readonly SearchValues<char> Invalidos = SearchValues.Create("\"<>|*?");

    public static TipoDeEndereco Classificar(string? endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco))
        {
            return TipoDeEndereco.NaoSuportado;
        }

        if (endereco.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return EhUncValido(endereco) ? TipoDeEndereco.Unc : TipoDeEndereco.NaoSuportado;
        }

        return Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host)
                ? TipoDeEndereco.Http
                : TipoDeEndereco.NaoSuportado;
    }

    // \\servidor\compartilhamento[\...]: sem os prefixos de dispositivo (\\?\ e \\.\), sem caractere inválido nem de
    // controle, com servidor e compartilhamento.
    private static bool EhUncValido(string endereco)
    {
        if (endereco.StartsWith(@"\\?\", StringComparison.Ordinal) || endereco.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return false;
        }

        if (endereco.AsSpan().ContainsAny(Invalidos) || endereco.Any(char.IsControl))
        {
            return false;
        }

        var partes = endereco[2..].Split('\\');
        return partes.Length >= 2
            && partes[0].Length > 0
            && partes[1].Length > 0
            && !partes.Any(p => p is "." or "..")
            && Path.IsPathFullyQualified(endereco);
    }
}
