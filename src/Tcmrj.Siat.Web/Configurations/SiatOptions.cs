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

    /// <summary>
    /// Como o usuário entra: <see cref="ModoDeAutenticacao.Oidc"/> (padrão) ou
    /// <see cref="ModoDeAutenticacao.UsuarioDeRede"/>. Lido na subida; trocar exige reiniciar o aplicativo.
    /// </summary>
    public ModoDeAutenticacao Autenticacao { get; set; } = ModoDeAutenticacao.Oidc;

    /// <summary>
    /// Lê o modo de autenticação já no registro dos serviços, porque os esquemas dependem dele. Usa o mesmo binder do
    /// <c>IOptions&lt;SiatOptions&gt;</c>, para que os componentes vejam o modo que decidiu os esquemas. Ausente é
    /// OIDC; valor desconhecido impede a aplicação de subir. Os dois modos valem em qualquer ambiente (constituição
    /// 2.0.0, Princípio III).
    /// </summary>
    public static ModoDeAutenticacao LerAutenticacao(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        var modo = configuracao.GetSection(Secao).Get<SiatOptions>()?.Autenticacao ?? ModoDeAutenticacao.Oidc;
        if (!Enum.IsDefined(modo))
        {
            throw new InvalidOperationException(
                $"{Secao}:{nameof(Autenticacao)} deve ser {nameof(ModoDeAutenticacao.Oidc)} ou " +
                $"{nameof(ModoDeAutenticacao.UsuarioDeRede)}.");
        }

        return modo;
    }

    /// <summary>
    /// Vazio, ou "/" seguido de segmentos com letras, números, "_", "-" e ".", sem barra no fim. Segmentos "." e ".."
    /// são recusados: o navegador os resolve e o <c>&lt;base href&gt;</c> apontaria para outro caminho.
    /// </summary>
    public static bool BaseHrefValido(string? valor) => valor is not null && (valor.Length == 0 || PadraoBaseHref().IsMatch(valor));

    [GeneratedRegex(@"^(/(?!\.\.?(/|$))[A-Za-z0-9_.\-]+)+$")]
    private static partial Regex PadraoBaseHref();
}
