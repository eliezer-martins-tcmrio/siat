using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.App.Web.Features.Versoes;

/// <summary>
/// Quantidade de arquivos da leitura em andamento de um servidor, para a tela trocar "Lendo endereços em {servidor}"
/// por "Lendo {N} arquivos de versão" (specs/003-versoes-publicadas/research.md R10). Autoriza como a
/// <see cref="ObterVersoes"/>; nula enquanto a tabela é lida ou sem leitura em andamento. Com o vínculo, é o andamento
/// da leitura da ficha (specs/004-ficha-sistema/research.md R6).
/// </summary>
public static class ObterAndamento
{
    /// <param name="Servidor">Chave do servidor; nula usa <see cref="CacheDeVersoesOptions.ServidorPadrao"/>.</param>
    /// <param name="Vinculo">Vínculo da ficha; nulo é o andamento da tela de Versões.</param>
    public sealed record Query(string? Servidor, IReadOnlyList<string>? Vinculo = null) : IRequest<Result<int?, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IVersoesPublicadasService versoes,
        IOptionsMonitor<CacheDeVersoesOptions> opcoes) : IRequestHandler<Query, Result<int?, Erro>>
    {
        public async Task<Result<int?, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var servidor = await AutorizacaoDasVersoes.AutorizarAsync(autorizacao, opcoes, request.Servidor);
            if (servidor.IsFailure)
            {
                return servidor.Error;
            }

            var quantidade = request.Vinculo is { } vinculo
                ? versoes.QuantidadeEmLeitura(servidor.Value.Chave, VinculoDeVersoes.De(vinculo))
                : versoes.QuantidadeEmLeitura(servidor.Value.Chave);
            return Result.Success<int?, Erro>(quantidade);
        }
    }
}
