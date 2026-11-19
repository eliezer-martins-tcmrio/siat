namespace Tcmrj.Siat.Domain.Models;

/// <summary>
/// Vínculo de um usuário de rede (login do Windows) com a matrícula: linha ativa da view do RH
/// <c>CSLIB_UserManager.dbo.vwSIRHU_Integracao</c> e, quando existe, o usuário de mesmo Id no UserManager.
/// </summary>
/// <param name="Matricula">Matrícula do RH; é também o <c>Users.Id</c> do UserManager.</param>
/// <param name="Nome">Nome do usuário no UserManager; nulo quando não há usuário com essa matrícula.</param>
/// <param name="Email">E-mail do usuário no UserManager.</param>
/// <param name="Habilitado">Existe usuário com essa matrícula no UserManager e ele está habilitado.</param>
public sealed record UsuarioDeRede(int Matricula, string? Nome, string? Email, bool Habilitado);
