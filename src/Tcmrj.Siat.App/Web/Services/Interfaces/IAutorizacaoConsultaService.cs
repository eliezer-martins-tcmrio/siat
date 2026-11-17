using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>
/// Autorização por consulta (R4): resolve o servidor pela chave (nunca pelo endereço), calcula
/// <c>Modulo.PoliticaPara(Ambiente)</c> e verifica a política com o usuário do circuito, sem cache.
/// Não grava no log: quem registra é o handler da feature.
/// </summary>
public interface IAutorizacaoConsultaService
{
    /// <returns>
    /// O servidor quando autorizado; <see cref="Erros.Consultas.ServidorNaoEncontrado"/> para chave inexistente;
    /// <see cref="Erros.Consultas.AcessoNegado"/> quando o usuário não tem a política.
    /// </returns>
    Task<Result<Servidor, Erro>> AutorizarAsync(Modulo modulo, string chaveServidor);

    /// <summary>Verdadeiro se o usuário tem ao menos uma das políticas do módulo (menu e abertura da página).</summary>
    Task<bool> PodeAcessarModuloAsync(Modulo modulo);
}
