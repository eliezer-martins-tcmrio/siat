using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>
/// Versões publicadas em memória, por servidor (specs/003-versoes-publicadas/research.md R5 e R10): o último resultado
/// de cada servidor vale por <c>MinutosDeCache</c>; pedidos simultâneos do mesmo servidor esperam a mesma leitura; falha
/// da tabela com resultado anterior devolve o anterior com a falha.
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
    /// Quantidade de arquivos da leitura em andamento do servidor; nula enquanto a tabela é lida ou sem leitura em
    /// andamento (R10).
    /// </summary>
    int? QuantidadeEmLeitura(string chaveServidor);
}

/// <summary>Resultado devolvido pelo serviço e, quando a última releitura falhou, a falha.</summary>
public sealed record VersoesObtidas(VersoesLidas Versoes, FalhaDeLeitura? Falha);
