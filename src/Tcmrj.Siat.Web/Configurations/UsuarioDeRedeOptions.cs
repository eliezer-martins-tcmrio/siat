namespace Tcmrj.Siat.Web.Configurations;

/// <summary>Entrada pelo usuário de rede (autenticação Windows), seção <see cref="Secao"/> do appsettings.</summary>
public sealed class UsuarioDeRedeOptions
{
    public const string Secao = "UsuarioDeRede";

    /// <summary>Domínio aceito (nome NetBIOS, por exemplo <c>TCMNET</c>); login de outro domínio não é vinculado.</summary>
    public string Dominio { get; set; } = string.Empty;

    /// <summary>Quanto tempo um vínculo encontrado fica em memória. Vínculo não encontrado não fica.</summary>
    public int MinutosDeCache { get; set; } = 5;
}
