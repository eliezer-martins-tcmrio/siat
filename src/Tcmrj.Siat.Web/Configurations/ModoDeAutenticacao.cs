namespace Tcmrj.Siat.Web.Configurations;

/// <summary>Como o usuário entra no SIAT (<c>SiatConfiguration:Autenticacao</c>), um modo por ambiente.</summary>
public enum ModoDeAutenticacao
{
    /// <summary>SSO do SIA por OpenID Connect, como manda o Princípio III da constituição. É o padrão.</summary>
    Oidc,

    /// <summary>
    /// PROTÓTIPO (features/prototipo_login_rede): usuário do Windows (Negotiate), ligado ao usuário do UserManager com
    /// o mesmo login, sem tela de login. Exige a autenticação do Windows ligada no IIS para o aplicativo. Vale em qualquer
    /// ambiente, inclusive produção, enquanto a coordenação avalia (constituição 2.0.0, Princípio III).
    /// </summary>
    UsuarioDeRede,
}
