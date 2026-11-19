namespace Tcmrj.Siat.Web.Configurations;

/// <summary>Como o usuário entra no SIAT (<c>SiatConfiguration:Autenticacao</c>), um modo por ambiente.</summary>
public enum ModoDeAutenticacao
{
    /// <summary>SSO do SIA por OpenID Connect, como manda o Princípio III da constituição. É o padrão.</summary>
    Oidc,

    /// <summary>
    /// PROTÓTIPO (features/prototipo_login_rede): usuário do Windows (Negotiate), ligado à matrícula pela view do RH,
    /// sem tela de login. Exige a autenticação do Windows ligada no IIS para o aplicativo e não sobe em produção
    /// enquanto a coordenação não decidir e a constituição não mudar.
    /// </summary>
    UsuarioDeRede,
}
