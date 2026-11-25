using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.App.Web.Features.Versoes;

/// <summary>
/// Versões do vínculo de um item na ficha (specs/004-ficha-sistema/research.md R5 e R7). Exige
/// <c>Catalogo_Sistemas.CanSelect</c> e, pela <see cref="AutorizacaoDasVersoes"/>, <c>Versoes_Sistemas.CanSelect</c> e
/// o servidor da lista fechada, antes de qualquer leitura (FR-002, FR-022). O vínculo vem do componente da página, que
/// roda no servidor; ele só filtra dados que o usuário já pode ver na tela de Versões. Sem DBLog.
/// </summary>
public static class ObterVersoesDaFicha
{
    /// <param name="Servidor">Chave do servidor; nula usa <see cref="CacheDeVersoesOptions.ServidorPadrao"/>.</param>
    /// <param name="Vinculo">Nomes do campo <c>Versoes</c> do item (<c>FichaVM.Vinculo</c>).</param>
    /// <param name="Atualizar">Relê só o vínculo, ignorando os caches ("Atualizar agora").</param>
    public sealed record Query(string? Servidor, IReadOnlyList<string> Vinculo, bool Atualizar) : IRequest<Result<VersoesDaFichaVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IVersoesPublicadasService versoes,
        IOptionsMonitor<CacheDeVersoesOptions> opcoes,
        TimeProvider relogio) : IRequestHandler<Query, Result<VersoesDaFichaVM, Erro>>
    {
        public async Task<Result<VersoesDaFichaVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!await autorizacao.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas))
            {
                return Erros.Catalogo.AcessoNegado;
            }

            var servidor = await AutorizacaoDasVersoes.AutorizarAsync(autorizacao, opcoes, request.Servidor);
            if (servidor.IsFailure)
            {
                return servidor.Error;
            }

            var vinculo = VinculoDeVersoes.De(request.Vinculo ?? []);
            var servidorVM = ServidorVM.De(servidor.Value);
            if (vinculo.Vazio)
            {
                return VersoesDaFichaVM.Vazio(servidorVM, relogio.GetUtcNow());
            }

            Result<VersoesObtidas, Erro> obtidas;
            try
            {
                obtidas = await versoes.ObterDoSistemaAsync(servidor.Value, vinculo, request.Atualizar, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Erros.Versoes.Cancelada;
            }

            return obtidas.IsSuccess ? VersoesDaFichaVM.De(obtidas.Value, vinculo, servidorVM) : obtidas.Error;
        }
    }
}
