using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Features.Catalogo;

/// <summary>
/// Catálogo de sistemas para a tela. Autoriza pela política do módulo (<c>Catalogo_Sistemas.CanSelect</c>) antes de ler e,
/// sem ela, não devolve nada do catálogo (FR-003). A leitura não vai para o registro de consultas: o catálogo não é
/// dado sensível (research.md R5).
/// </summary>
public static class ObterCatalogo
{
    /// <param name="Atualizar">Lê de novo da origem mesmo dentro da validade ("Atualizar agora").</param>
    public sealed record Query(bool Atualizar) : IRequest<Result<CatalogoVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        ICatalogoSistemasService catalogo) : IRequestHandler<Query, Result<CatalogoVM, Erro>>
    {
        public async Task<Result<CatalogoVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!await autorizacao.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas))
            {
                return Erros.Catalogo.AcessoNegado;
            }

            Result<CatalogoObtido, Erro> obtido;
            try
            {
                obtido = await catalogo.ObterAsync(request.Atualizar, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Erros.Catalogo.Cancelada;
            }

            return obtido.IsSuccess ? CatalogoVM.De(obtido.Value) : obtido.Error;
        }
    }
}
