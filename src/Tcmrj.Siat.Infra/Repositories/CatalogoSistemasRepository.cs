using System.Globalization;
using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Catalogo;
using Tcmrj.Siat.Infra.Configurations;

namespace Tcmrj.Siat.Infra.Repositories;

/// <summary>
/// Lê o <c>sistemasDb.js</c> pela API de arquivos do GitLab (contracts/origem-sistemasdb.md), com o token de projeto
/// no cabeçalho <c>PRIVATE-TOKEN</c>, e o converte em <see cref="CatalogoLido"/> pelo <see cref="LeitorSistemasDb"/> e
/// pelo <see cref="MontadorCatalogo"/>. Só leitura.
/// <para>
/// As opções são validadas antes de qualquer chamada (inválidas: <see cref="Erros.Catalogo.NaoConfigurado"/>). Os erros
/// seguem research.md R6: 401 é acesso recusado; 403 e 404, arquivo não encontrado; demais códigos e falhas de rede,
/// indisponível; tempo limite do <see cref="HttpClient"/>, tempo esgotado. Cancelamento pedido pelo chamador volta
/// como <see cref="Erros.Catalogo.Cancelada"/>, sem exceção, no padrão do <c>MailQueueRepository</c>. O token nunca
/// vai para mensagem nem log.
/// </para>
/// </summary>
public sealed partial class CatalogoSistemasRepository(
    HttpClient http,
    IOptionsMonitor<CatalogoSistemasOptions> opcoes,
    TimeProvider relogio,
    ILogger<CatalogoSistemasRepository> logger) : ICatalogoSistemasRepository
{
    public const string CabecalhoToken = "PRIVATE-TOKEN";

    public async Task<Result<CatalogoLido, Erro>> LerAsync(CancellationToken cancellationToken)
    {
        var configuracao = opcoes.CurrentValue;
        var motivos = configuracao.Validar();
        if (motivos.Count > 0)
        {
            ConfiguracaoInvalida(logger, string.Join(" ", motivos));
            return Erros.Catalogo.NaoConfigurado;
        }

        var gitLab = configuracao.GitLab;
        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, gitLab.UrlDaApi());
            pedido.Headers.Add(CabecalhoToken, gitLab.Token);

            using var resposta = await http.SendAsync(pedido, cancellationToken);
            if (resposta.StatusCode != HttpStatusCode.OK)
            {
                var codigo = (int)resposta.StatusCode;
                RespostaInesperada(logger, codigo);
                return resposta.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => Erros.Catalogo.AcessoRecusado,
                    HttpStatusCode.Forbidden or HttpStatusCode.NotFound => Erros.Catalogo.ArquivoNaoEncontrado,
                    _ => Erros.Catalogo.Indisponivel($"resposta HTTP {codigo.ToString(CultureInfo.InvariantCulture)}."),
                };
            }

            var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);
            var texto = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(bytes);

            var json = LeitorSistemasDb.ParaJson(texto);
            if (json.IsFailure)
            {
                FormatoInvalido(logger, json.Error.Message);
                return json.Error;
            }

            var catalogo = MontadorCatalogo.Montar(json.Value, relogio.GetUtcNow(), gitLab.UrlDaOrigem(), logger);
            if (catalogo.IsFailure)
            {
                FormatoInvalido(logger, catalogo.Error.Message);
            }

            return catalogo;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LeituraCancelada(logger);
            return Erros.Catalogo.Cancelada;
        }
        catch (OperationCanceledException)
        {
            // Sem cancelamento pedido, o cancelamento é o tempo limite do HttpClient.
            TempoEsgotado(logger, configuracao.TempoLimiteSegundos);
            return Erros.Catalogo.TempoEsgotado(configuracao.TempoLimiteSegundos);
        }
        catch (HttpRequestException ex)
        {
            FalhaDeRede(logger, ex.Message);
            return Erros.Catalogo.Indisponivel(ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catálogo de sistemas sem configuração válida: {Motivos}")]
    private static partial void ConfiguracaoInvalida(ILogger logger, string motivos);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O GitLab respondeu {Codigo} à leitura do catálogo de sistemas.")]
    private static partial void RespostaInesperada(ILogger logger, int codigo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O sistemasDb.js não pôde ser lido: {Motivo}")]
    private static partial void FormatoInvalido(ILogger logger, string motivo);

    [LoggerMessage(Level = LogLevel.Information, Message = "Leitura do catálogo de sistemas cancelada antes do fim.")]
    private static partial void LeituraCancelada(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O GitLab não respondeu em {Segundos} s à leitura do catálogo de sistemas.")]
    private static partial void TempoEsgotado(ILogger logger, int segundos);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha de rede ao ler o catálogo de sistemas: {Motivo}")]
    private static partial void FalhaDeRede(ILogger logger, string motivo);
}
