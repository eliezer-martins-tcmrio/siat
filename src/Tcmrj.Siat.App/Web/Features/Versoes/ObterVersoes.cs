using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Features.Versoes;

/// <summary>
/// Versões publicadas de um servidor para a tela (specs/003-versoes-publicadas). Autoriza pela política do módulo
/// (<c>Versoes_Sistemas.CanSelect</c>) e resolve o servidor na lista fechada antes de qualquer leitura (FR-003). A
/// leitura não vai para o DBLog: versões e caminhos de publicação não são dados sensíveis (research.md R6).
/// </summary>
public static class ObterVersoes
{
    /// <param name="Servidor">Chave do servidor; nula usa <see cref="CacheDeVersoesOptions.ServidorPadrao"/>.</param>
    /// <param name="Atualizar">Lê de novo mesmo dentro da validade ("Atualizar agora").</param>
    public sealed record Query(string? Servidor, bool Atualizar) : IRequest<Result<VersoesVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IVersoesPublicadasService versoes,
        IOptionsMonitor<CacheDeVersoesOptions> opcoes) : IRequestHandler<Query, Result<VersoesVM, Erro>>
    {
        public async Task<Result<VersoesVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var servidor = await AutorizacaoDasVersoes.AutorizarAsync(autorizacao, opcoes, request.Servidor);
            if (servidor.IsFailure)
            {
                return servidor.Error;
            }

            Result<VersoesObtidas, Erro> obtidas;
            try
            {
                obtidas = await versoes.ObterAsync(servidor.Value, request.Atualizar, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Erros.Versoes.Cancelada;
            }

            return obtidas.IsSuccess ? VersoesVM.De(obtidas.Value, ServidorVM.De(servidor.Value)) : obtidas.Error;
        }
    }
}

/// <summary>Autorização comum a <see cref="ObterVersoes"/> e <see cref="ObterAndamento"/> (research.md R6).</summary>
internal static class AutorizacaoDasVersoes
{
    /// <returns>
    /// O servidor autorizado; <see cref="Erros.Versoes.AcessoNegado"/> sem a política (antes de olhar o servidor) ou
    /// <see cref="Erros.Consultas.ServidorNaoEncontrado"/> para chave fora da lista.
    /// </returns>
    public static async Task<Result<Servidor, Erro>> AutorizarAsync(
        IAutorizacaoConsultaService autorizacao, IOptionsMonitor<CacheDeVersoesOptions> opcoes, string? chave)
    {
        if (!await autorizacao.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas))
        {
            return Erros.Versoes.AcessoNegado;
        }

        var autorizado = await autorizacao.AutorizarAsync(Modulo.Versoes_Sistemas, chave ?? opcoes.CurrentValue.ServidorPadrao);
        if (autorizado.IsFailure && autorizado.Error.Code == Erros.Consultas.CodigoAcessoNegado)
        {
            return Erros.Versoes.AcessoNegado;
        }

        return autorizado;
    }
}
