using System.Collections.Concurrent;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Singleton com as versões em memória, uma entrada por servidor (specs/003-versoes-publicadas/research.md R5): validade,
/// "Atualizar agora", leitura única, resultado anterior preservado e cancelamento por contagem de quem espera vêm da
/// <see cref="LeituraEmCache{TChave, TValor}"/>. Cada leitura abre um escopo, lista os endereços pelo repositório e lê
/// os arquivos pelo <see cref="LeitorDeVersoes"/>. Só a falha da tabela é falha da leitura; erro de arquivo fica na
/// célula. Enquanto um servidor é lido, guarda a quantidade de arquivos daquela leitura (R10).
/// <para>
/// As versões da ficha (specs/004-ficha-sistema/research.md R5 e R6) ficam num segundo cache, por servidor e vínculo
/// (<see cref="ChaveDaFicha"/>), com a mesma validade e o mesmo relógio: a leitura usa o mesmo SQL e o mesmo leitor, só
/// com as linhas do vínculo. O resultado parcial nunca entra no cache da tela de Versões (FR-027).
/// </para>
/// </summary>
public sealed partial class VersoesPublicadasService : IVersoesPublicadasService
{
    private readonly IServiceScopeFactory escopos;
    private readonly TimeProvider relogio;
    private readonly ILogger<VersoesPublicadasService> logger;
    private readonly LeituraEmCache<string, VersoesLidas> cache;
    private readonly LeituraEmCache<ChaveDaFicha, VersoesLidas> cacheDaFicha;
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
        cacheDaFicha = new LeituraEmCache<ChaveDaFicha, VersoesLidas>(
            LerDaFichaAsync,
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

    public async Task<Result<VersoesObtidas, Erro>> ObterDoSistemaAsync(
        Servidor servidor, VinculoDeVersoes vinculo, bool atualizar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        ArgumentNullException.ThrowIfNull(vinculo);

        servidores[servidor.Chave] = servidor;
        var chave = ChaveDaFicha.De(servidor, vinculo);

        if (!atualizar)
        {
            // Quem chega à ficha pela tela de Versões já tem o resultado do servidor (FR-025): nenhuma leitura.
            var daTela = cache.Valido(servidor.Chave);
            var daFicha = cacheDaFicha.Valido(chave);
            var maisRecente = (daTela, daFicha) switch
            {
                ({ } tela, { } ficha) => tela.GeradoEm >= ficha.GeradoEm ? tela : ficha,
                ({ } tela, null) => tela,
                (null, { } ficha) => ficha,
                _ => null,
            };

            if (maisRecente is not null)
            {
                return new VersoesObtidas(maisRecente, null);
            }
        }

        var obtido = await cacheDaFicha.ObterAsync(chave, atualizar, cancellationToken);
        return obtido.IsSuccess ? new VersoesObtidas(obtido.Value.Valor, obtido.Value.Falha) : obtido.Error;
    }

    public int? QuantidadeEmLeitura(string chaveServidor) =>
        andamentos.TryGetValue(chaveServidor, out var andamento) ? andamento.Quantidade : null;

    public int? QuantidadeEmLeitura(string chaveServidor, VinculoDeVersoes vinculo) =>
        QuantidadeEmLeitura(ChaveDaFicha.De(chaveServidor, vinculo).ChaveDeAndamento);

    private Task<Result<VersoesLidas, Erro>> LerAsync(string chave, CancellationToken cancellationToken) =>
        LerAsync(servidores[chave], chave, enderecos => enderecos, cancellationToken);

    // Só as linhas cujo nome normalizado está no vínculo (research.md R5): a comparação é a de R2, qualquer que seja
    // o collation da coluna. Nenhuma linha do vínculo não é erro: o resultado vem sem sistemas.
    private Task<Result<VersoesLidas, Erro>> LerDaFichaAsync(ChaveDaFicha chave, CancellationToken cancellationToken)
    {
        var nomes = chave.Nomes();
        return LerAsync(
            servidores[chave.Servidor],
            chave.ChaveDeAndamento,
            enderecos => enderecos.Where(e => nomes.Contains(NomeDeSistema.Normalizar(e.Sistema))).ToArray(),
            cancellationToken);
    }

    private async Task<Result<VersoesLidas, Erro>> LerAsync(
        Servidor servidor,
        string chaveDeAndamento,
        Func<IReadOnlyList<EnderecoDeVersao>, IReadOnlyList<EnderecoDeVersao>> filtrar,
        CancellationToken cancellationToken)
    {
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
                filtrar(enderecos.Value),
                inicio,
                quantidade =>
                {
                    meu = new Andamento(quantidade);
                    andamentos[chaveDeAndamento] = meu;
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
            // Só remove o andamento desta leitura: uma leitura nova da mesma chave pode já ter começado.
            if (meu is not null)
            {
                andamentos.TryRemove(new KeyValuePair<string, Andamento>(chaveDeAndamento, meu));
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
