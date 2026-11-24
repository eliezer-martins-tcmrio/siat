using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Features.Versoes;

/// <summary>
/// Quantidade de arquivos da leitura em andamento de um servidor, para a tela trocar "Lendo endereços em {servidor}"
/// por "Lendo {N} arquivos de versão" (specs/003-versoes-publicadas/research.md R10). Autoriza como a
/// <see cref="ObterVersoes"/>; nula enquanto a tabela é lida ou sem leitura em andamento.
/// </summary>
public static class ObterAndamento
{
    /// <param name="Servidor">Chave do servidor; nula usa <see cref="CacheDeVersoesOptions.ServidorPadrao"/>.</param>
    public sealed record Query(string? Servidor) : IRequest<Result<int?, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IVersoesPublicadasService versoes,
        IOptionsMonitor<CacheDeVersoesOptions> opcoes) : IRequestHandler<Query, Result<int?, Erro>>
    {
        public async Task<Result<int?, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var servidor = await AutorizacaoDasVersoes.AutorizarAsync(autorizacao, opcoes, request.Servidor);
            return servidor.IsSuccess
                ? Result.Success<int?, Erro>(versoes.QuantidadeEmLeitura(servidor.Value.Chave))
                : servidor.Error;
        }
    }
}
