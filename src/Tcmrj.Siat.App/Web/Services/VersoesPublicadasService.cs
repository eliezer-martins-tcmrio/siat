using System.Collections.Concurrent;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Singleton com as versões em memória, uma entrada por servidor (specs/003-versoes-publicadas/research.md R5): validade,
/// "Atualizar agora", leitura única, resultado anterior preservado e cancelamento por contagem de quem espera vêm da
/// <see cref="LeituraEmCache{TChave, TValor}"/>. Cada leitura abre um escopo, lista os endereços pelo repositório e lê
/// os arquivos pelo <see cref="LeitorDeVersoes"/>. Só a falha da tabela é falha da leitura; erro de arquivo fica na
/// célula. Enquanto um servidor é lido, guarda a quantidade de arquivos daquela leitura (R10).
/// </summary>
public sealed partial class VersoesPublicadasService : IVersoesPublicadasService
{
    private readonly IServiceScopeFactory escopos;
    private readonly TimeProvider relogio;
    private readonly ILogger<VersoesPublicadasService> logger;
    private readonly LeituraEmCache<string, VersoesLidas> cache;
    private readonly ConcurrentDictionary<string, Servidor> servidores = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Andamento> andamentos = new(StringComparer.Ordinal);

    public VersoesPublicadasService(
        IServiceScopeFactory escopos,
        IOptionsMonitor<CacheDeVersoesOptions> opcoes,
        TimeProvider relogio,
        ILogger<VersoesPublicadasService> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        this.escopos = escopos;
        this.relogio = relogio;
        this.logger = logger;
        cache = new LeituraEmCache<string, VersoesLidas>(
            LerAsync,
            versoes => versoes.GeradoEm,
            () => TimeSpan.FromMinutes(opcoes.CurrentValue.MinutosDeCache),
            Erros.Versoes.Cancelada,
            ex => Erros.Versoes.TabelaIndisponivel("o servidor", ex.Message),
            relogio,
            logger);
    }

    public async Task<Result<VersoesObtidas, Erro>> ObterAsync(Servidor servidor, bool atualizar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);

        // A leitura usa o servidor mais recente da lista para a chave (o servidores.json recarrega sozinho).
        servidores[servidor.Chave] = servidor;
        var obtido = await cache.ObterAsync(servidor.Chave, atualizar, cancellationToken);
        return obtido.IsSuccess ? new VersoesObtidas(obtido.Value.Valor, obtido.Value.Falha) : obtido.Error;
    }

    public int? QuantidadeEmLeitura(string chaveServidor) =>
        andamentos.TryGetValue(chaveServidor, out var andamento) ? andamento.Quantidade : null;

    private async Task<Result<VersoesLidas, Erro>> LerAsync(string chave, CancellationToken cancellationToken)
    {
        var servidor = servidores[chave];
        var inicio = relogio.GetTimestamp();
        Andamento? meu = null;
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var repositorio = escopo.ServiceProvider.GetRequiredService<IEnderecosDeVersaoRepository>();
            var leitor = escopo.ServiceProvider.GetRequiredService<LeitorDeVersoes>();

            var enderecos = await repositorio.ListarAtivosAsync(servidor, cancellationToken);
            if (enderecos.IsFailure)
            {
                return enderecos.Error;
            }

            return await leitor.LerAsync(
                servidor,
                enderecos.Value,
                inicio,
                quantidade =>
                {
                    meu = new Andamento(quantidade);
                    andamentos[chave] = meu;
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            FalhaInesperada(logger, servidor.Nome, ex);
            return Erros.Versoes.TabelaIndisponivel(servidor.Nome, ex.Message);
        }
        finally
        {
            // Só remove o andamento desta leitura: uma leitura nova do mesmo servidor pode já ter começado.
            if (meu is not null)
            {
                andamentos.TryRemove(new KeyValuePair<string, Andamento>(chave, meu));
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha inesperada ao ler as versões de {Servidor}.")]
    private static partial void FalhaInesperada(ILogger logger, string servidor, Exception ex);

    /// <summary>Andamento de uma leitura (instância própria, para remover só o desta leitura).</summary>
    private sealed class Andamento(int quantidade)
    {
        public int Quantidade { get; } = quantidade;
    }
}
