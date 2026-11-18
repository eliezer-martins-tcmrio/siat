using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>Autoriza cada consulta pela política do módulo (a mesma em todos os ambientes), sem cache (R4).</summary>
public sealed class AutorizacaoConsultaService(
    IServidoresRepository servidores,
    IAuthorizationService autorizacao,
    IUsuarioAtualService usuarioAtual) : IAutorizacaoConsultaService
{
    public async Task<Result<Servidor, Erro>> AutorizarAsync(Modulo modulo, string chaveServidor)
    {
        var servidor = servidores.ObterPorChave(chaveServidor);
        if (servidor is null)
        {
            return Result.Failure<Servidor, Erro>(Erros.Consultas.ServidorNaoEncontrado);
        }

        var usuario = await usuarioAtual.ObterAsync();
        var resultado = await autorizacao.AuthorizeAsync(usuario, modulo.Politica());

        return resultado.Succeeded
            ? Result.Success<Servidor, Erro>(servidor)
            : Result.Failure<Servidor, Erro>(Erros.Consultas.AcessoNegado);
    }

    public async Task<bool> PodeAcessarModuloAsync(Modulo modulo)
    {
        var usuario = await usuarioAtual.ObterAsync();
        return (await autorizacao.AuthorizeAsync(usuario, modulo.Politica())).Succeeded;
    }
}
