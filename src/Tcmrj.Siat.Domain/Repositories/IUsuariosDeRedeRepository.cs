using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Usuário do UserManager pelo login de rede, lido da <c>dbo.Users</c> do <c>CSLIB_UserManager</c>.</summary>
public interface IUsuariosDeRedeRepository
{
    /// <summary>
    /// O usuário com o login (sem o domínio): o habilitado, que é único; sem ele, um desabilitado; nulo quando não há
    /// nenhum.
    /// </summary>
    Task<Result<UsuarioDeRede?, Erro>> ObterPorLoginAsync(string login, CancellationToken cancellationToken);
}
