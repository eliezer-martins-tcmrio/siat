using System.Security.Claims;

namespace Tcmrj.Siat.Web.Services;

/// <summary>
/// Situação do vínculo do usuário de rede com a matrícula, gravada pela <see cref="ClaimsDoUsuarioDeRede"/> quando não
/// há vínculo, para a página de acesso negado explicar o motivo.
/// </summary>
public static class VinculoDeRede
{
    public const string TipoClaim = "siat_vinculo_rede";

    public const string DominioInvalido = "dominio-invalido";

    /// <summary>Sem usuário interno e habilitado no UserManager com o login de rede.</summary>
    public const string NaoEncontrado = "nao-encontrado";

    public const string Indisponivel = "indisponivel";

    /// <summary>Situação gravada no usuário, ou nulo quando ele tem vínculo (ou não passou pela transformação).</summary>
    public static string? Situacao(ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return usuario.FindFirst(TipoClaim)?.Value;
    }
}
