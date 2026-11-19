namespace Tcmrj.Siat.Domain.Models;

/// <summary>
/// Usuário do UserManager (<c>CSLIB_UserManager.dbo.Users</c>) cujo <c>Login</c> é o login de rede (do Windows, sem
/// o domínio).
/// </summary>
/// <param name="Matricula"><c>Users.Id</c>, que o SIA também grava como matrícula.</param>
/// <param name="Nome">Nome do usuário no UserManager.</param>
/// <param name="Email">E-mail do usuário no UserManager.</param>
/// <param name="Habilitado">O usuário está habilitado no UserManager.</param>
public sealed record UsuarioDeRede(int Matricula, string Nome, string? Email, bool Habilitado);
