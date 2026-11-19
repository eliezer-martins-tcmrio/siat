using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Usuário do UserManager pelo login de rede, lido da <c>dbo.Users</c> do <c>CSLIB_UserManager</c>.</summary>
public interface IUsuariosDeRedeRepository
{
    /// <summary>
    /// O usuário interno (<c>Id</c> abaixo de 1.000.000) e habilitado com o login (sem o domínio), ou nulo quando não
    /// há: desabilitado e usuário externo não entram.
    /// </summary>
    Task<Result<UsuarioDeRede?, Erro>> ObterPorLoginAsync(string login, CancellationToken cancellationToken);
}
