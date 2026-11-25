using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Features.Catalogo;

/// <summary>
/// Mapa dos nomes da tabela de Versões para as fichas (specs/004-ficha-sistema/research.md R4 e R7). Exige
/// <c>Catalogo_Sistemas.CanSelect</c> antes de ler o catálogo (FR-005); lê pelo mesmo serviço e cache da tela de
/// Catálogo. Com a releitura falha e uma leitura anterior, o mapa é o dessa leitura.
/// </summary>
public static class ListarFichas
{
    public sealed record Query : IRequest<Result<FichasVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        ICatalogoSistemasService catalogo) : IRequestHandler<Query, Result<FichasVM, Erro>>
    {
        public async Task<Result<FichasVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!await autorizacao.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas))
            {
                return Erros.Catalogo.AcessoNegado;
            }

            Result<CatalogoObtido, Erro> obtido;
            try
            {
                obtido = await catalogo.ObterAsync(false, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Erros.Catalogo.Cancelada;
            }

            if (obtido.IsFailure)
            {
                return obtido.Error;
            }

            return new FichasVM(obtido.Value.Catalogo.FichasPorVersao());
        }
    }
}
