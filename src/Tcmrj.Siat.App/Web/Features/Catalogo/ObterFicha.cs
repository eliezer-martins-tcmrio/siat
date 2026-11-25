using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Features.Catalogo;

/// <summary>
/// Ficha de um item do catálogo (specs/004-ficha-sistema/research.md R7). Exige <c>Catalogo_Sistemas.CanSelect</c>
/// antes de ler (FR-001) e lê o catálogo pelo mesmo serviço da tela de Catálogo (mesmo cache, FR-024). Só leva o
/// vínculo, e o aviso de vínculo inválido, a quem tem também <c>Versoes_Sistemas.CanSelect</c> (FR-002, FR-010). Sem
/// DBLog: o catálogo não é dado sensível.
/// </summary>
public static class ObterFicha
{
    /// <param name="Sistema">Nome do item, como na URL; comparado sem acentos, maiúsculas e espaços nas pontas.</param>
    /// <param name="Atualizar">Lê de novo da origem mesmo dentro da validade ("Atualizar agora").</param>
    public sealed record Query(string? Sistema, bool Atualizar) : IRequest<Result<FichaVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        ICatalogoSistemasService catalogo) : IRequestHandler<Query, Result<FichaVM, Erro>>
    {
        public async Task<Result<FichaVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!await autorizacao.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas))
            {
                return Erros.Catalogo.AcessoNegado;
            }

            var nome = request.Sistema?.Trim() ?? string.Empty;
            if (nome.Length == 0)
            {
                return Erros.Catalogo.SistemaNaoEncontrado(nome);
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

            if (obtido.IsFailure)
            {
                return obtido.Error;
            }

            var localizado = obtido.Value.Catalogo.Localizar(nome);
            if (localizado is null)
            {
                return Erros.Catalogo.SistemaNaoEncontrado(nome);
            }

            var mostraVersoes = await autorizacao.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas);
            return FichaVM.De(localizado, obtido.Value, mostraVersoes);
        }
    }
}
