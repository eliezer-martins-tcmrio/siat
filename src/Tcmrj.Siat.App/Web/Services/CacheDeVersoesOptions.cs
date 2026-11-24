namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// O que o App precisa da seção <c>Versoes</c> (o App não depende do Infra, como em <see cref="CacheDoCatalogoOptions"/>).
/// A validação das faixas é do <c>VersoesOptions</c> do Infra, na subida.
/// </summary>
public sealed class CacheDeVersoesOptions
{
    public const string Secao = "Versoes";

    /// <summary>Chave do <c>servidores.json</c> lida quando a URL não traz o servidor.</summary>
    public string ServidorPadrao { get; set; } = "tcmbd-prd";

    /// <summary>Validade do resultado de cada servidor, em minutos.</summary>
    public int MinutosDeCache { get; set; } = 5;

    /// <summary>Tempo limite por arquivo, em segundos (usado pelo <see cref="LeitorDeVersoes"/>).</summary>
    public int TempoLimiteSegundos { get; set; } = 5;

    /// <summary>Leituras de arquivo em paralelo (usado pelo <see cref="LeitorDeVersoes"/>).</summary>
    public int LeiturasSimultaneas { get; set; } = 32;
}
