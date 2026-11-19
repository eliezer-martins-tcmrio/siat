using System.Text.RegularExpressions;

namespace Tcmrj.Siat.Web.Configurations;

/// <summary>Configuração geral do SIAT, seção <see cref="Secao"/> do appsettings (mesmo padrão do SiCATI).</summary>
public sealed partial class SiatOptions
{
    public const string Secao = "SiatConfiguration";

    /// <summary>
    /// Caminho do aplicativo no IIS, sem a barra final: <c>/siat_dsv</c>, <c>/siat_hml</c>, <c>/siat</c>. Vazio quando o
    /// SIAT fica na raiz do site (por exemplo, <c>dotnet run</c>). Precisa ser igual ao caminho do aplicativo no IIS:
    /// é dele que o navegador resolve as rotas, os arquivos estáticos e a conexão do Blazor.
    /// </summary>
    public string BlazorBaseHref { get; set; } = string.Empty;

    /// <summary>Valor do <c>&lt;base href&gt;</c>: o caminho com a barra final (<c>/</c> na raiz).</summary>
    public string BaseHref => BlazorBaseHref + "/";

    /// <summary>Vazio, ou "/" seguido de segmentos com letras, números, "_", "-" e ".", sem barra no fim.</summary>
    public static bool BaseHrefValido(string? valor) => valor is not null && (valor.Length == 0 || PadraoBaseHref().IsMatch(valor));

    [GeneratedRegex(@"^(/[A-Za-z0-9_.\-]+)+$")]
    private static partial Regex PadraoBaseHref();
}
