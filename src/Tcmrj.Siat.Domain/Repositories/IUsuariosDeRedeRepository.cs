using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Vínculo do login de rede com a matrícula, lido do <c>CSLIB_UserManager</c>.</summary>
public interface IUsuariosDeRedeRepository
{
    /// <summary>
    /// Linhas ativas da view do RH para o login (sem o domínio), no máximo duas: basta saber se o vínculo é único.
    /// </summary>
    Task<Result<IReadOnlyList<UsuarioDeRede>, Erro>> ObterPorLoginAsync(string login, CancellationToken cancellationToken);
}
