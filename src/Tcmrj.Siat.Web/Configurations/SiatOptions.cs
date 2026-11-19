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
    /// OIDC; valor desconhecido e usuário de rede em produção impedem a aplicação de subir.
    /// </summary>
    public static ModoDeAutenticacao LerAutenticacao(IConfiguration configuracao, IHostEnvironment ambiente)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(ambiente);

        var chave = $"{Secao}:{nameof(Autenticacao)}";
        var modo = configuracao.GetSection(Secao).Get<SiatOptions>()?.Autenticacao ?? ModoDeAutenticacao.Oidc;
        if (!Enum.IsDefined(modo))
        {
            throw new InvalidOperationException(
                $"{chave} deve ser {nameof(ModoDeAutenticacao.Oidc)} ou {nameof(ModoDeAutenticacao.UsuarioDeRede)}.");
        }

        // Princípio III: em produção, só o SIA, até a coordenação decidir e a constituição mudar.
        if (modo == ModoDeAutenticacao.UsuarioDeRede && ambiente.IsProduction())
        {
            throw new InvalidOperationException(
                $"{chave} = {nameof(ModoDeAutenticacao.UsuarioDeRede)} não é permitido em produção: a constituição " +
                "(Princípio III) exige a entrada pelo SIA. Use Oidc.");
        }

        return modo;
    }

    /// <summary>Vazio, ou "/" seguido de segmentos com letras, números, "_", "-" e ".", sem barra no fim.</summary>
    public static bool BaseHrefValido(string? valor) => valor is not null && (valor.Length == 0 || PadraoBaseHref().IsMatch(valor));

    [GeneratedRegex(@"^(/[A-Za-z0-9_.\-]+)+$")]
    private static partial Regex PadraoBaseHref();
}
