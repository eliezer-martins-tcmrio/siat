using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>
/// Versões publicadas em memória, por servidor (specs/003-versoes-publicadas/research.md R5 e R10): o último resultado
/// de cada servidor vale por <c>MinutosDeCache</c>; pedidos simultâneos do mesmo servidor esperam a mesma leitura; falha
/// da tabela com resultado anterior devolve o anterior com a falha. As versões da ficha (specs/004-ficha-sistema,
/// research.md R5) ficam num segundo cache, por servidor e vínculo, que nunca mexe no da tela de Versões.
/// </summary>
public interface IVersoesPublicadasService
{
    /// <param name="servidor">Servidor já autorizado, da lista fechada.</param>
    /// <param name="atualizar">Lê de novo mesmo dentro da validade ("Atualizar agora").</param>
    /// <param name="cancellationToken">
    /// Cancelado, o pedido recebe <see cref="Erros.Versoes.Cancelada"/>; a leitura só é interrompida na origem quando
    /// ninguém mais espera por ela.
    /// </param>
    Task<Result<VersoesObtidas, Erro>> ObterAsync(Servidor servidor, bool atualizar, CancellationToken cancellationToken);

    /// <summary>
    /// Versões dos registros do vínculo de um item (specs/004-ficha-sistema/research.md R5; FR-025 a FR-027):
    /// <list type="number">
    /// <item>Sem <paramref name="atualizar"/>, procura sem ler um resultado válido: o da tela de Versões para o
    /// servidor e o da própria ficha para o servidor e o vínculo; havendo os dois, o de <c>GeradoEm</c> maior.</item>
    /// <item>Sem resultado válido, ou com <paramref name="atualizar"/>, lê os endereços ativos do servidor pelo mesmo
    /// SQL, fica só com as linhas do vínculo e lê os arquivos delas. O resultado parcial vai para o cache da ficha,
    /// com a mesma validade, leitura única e cancelamento por contagem; nunca para o da tela.</item>
    /// </list>
    /// </summary>
    /// <param name="servidor">Servidor já autorizado, da lista fechada.</param>
    /// <param name="vinculo">Vínculo do item; os sistemas fora dele que vierem do cache da tela são ignorados depois.</param>
    /// <param name="atualizar">Ignora os dois caches e relê só o vínculo ("Atualizar agora").</param>
    /// <param name="cancellationToken">
    /// Cancelado, o pedido recebe <see cref="Erros.Versoes.Cancelada"/>; a leitura só é interrompida na origem quando
    /// ninguém mais espera por ela.
    /// </param>
    Task<Result<VersoesObtidas, Erro>> ObterDoSistemaAsync(
        Servidor servidor, VinculoDeVersoes vinculo, bool atualizar, CancellationToken cancellationToken);

    /// <summary>
    /// Quantidade de arquivos da leitura em andamento do servidor; nula enquanto a tabela é lida ou sem leitura em
    /// andamento (R10).
    /// </summary>
    int? QuantidadeEmLeitura(string chaveServidor);

    /// <summary>
    /// Quantidade de arquivos da leitura da ficha em andamento, pelo servidor e pelo vínculo
    /// (specs/004-ficha-sistema/research.md R6); nula enquanto a tabela é lida ou sem leitura em andamento.
    /// </summary>
    int? QuantidadeEmLeitura(string chaveServidor, VinculoDeVersoes vinculo);
}

/// <summary>Resultado devolvido pelo serviço e, quando a última releitura falhou, a falha.</summary>
public sealed record VersoesObtidas(VersoesLidas Versoes, FalhaDeLeitura? Falha);
