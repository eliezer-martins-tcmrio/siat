using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>
/// Catálogo de sistemas em memória (research.md R4): a última leitura válida vale por <c>MinutosDeCache</c>; pedidos
/// simultâneos esperam a mesma leitura; falha com leitura anterior devolve a anterior com a falha.
/// </summary>
public interface ICatalogoSistemasService
{
    /// <param name="atualizar">Lê de novo mesmo dentro da validade ("Atualizar agora").</param>
    /// <param name="cancellationToken">
    /// Cancelado, o pedido deixa de esperar e recebe <see cref="Erros.Catalogo.Cancelada"/>; a leitura só é interrompida
    /// na origem quando ninguém mais espera por ela.
    /// </param>
    Task<Result<CatalogoObtido, Erro>> ObterAsync(bool atualizar, CancellationToken cancellationToken);
}

/// <summary>Catálogo devolvido pelo serviço e, quando a última releitura falhou, a falha.</summary>
public sealed record CatalogoObtido(CatalogoLido Catalogo, FalhaDeLeitura? Falha);

/// <summary>Erro da última releitura e quando aconteceu.</summary>
public sealed record FalhaDeLeitura(Erro Erro, DateTimeOffset Em);
