using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Singleton com o catálogo em memória (research.md R4; estados em data-model.md). Validade, "Atualizar agora", uma
/// leitura por vez, leitura anterior preservada em falha e cancelamento na origem quando ninguém mais espera vêm da
/// <see cref="LeituraEmCache{TChave, TValor}"/>, com uma chave única (<see cref="Chave.Unica"/>; specs/003-versoes-publicadas/research.md R5).
/// O repositório (scoped, com <see cref="HttpClient"/> tipado) é resolvido num escopo próprio de cada leitura.
/// </summary>
public sealed class CatalogoSistemasService : ICatalogoSistemasService
{
    private readonly IServiceScopeFactory escopos;
    private readonly LeituraEmCache<Chave, CatalogoLido> cache;

    public CatalogoSistemasService(
        IServiceScopeFactory escopos,
        IOptionsMonitor<CacheDoCatalogoOptions> opcoes,
        TimeProvider relogio,
        ILogger<CatalogoSistemasService> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        this.escopos = escopos;
        cache = new LeituraEmCache<Chave, CatalogoLido>(
            LerAsync,
            catalogo => catalogo.LidoEm,
            () => TimeSpan.FromMinutes(opcoes.CurrentValue.MinutosDeCache),
            Erros.Catalogo.Cancelada,
            ex => Erros.Catalogo.Indisponivel(ex.Message),
            relogio,
            logger);
    }

    public async Task<Result<CatalogoObtido, Erro>> ObterAsync(bool atualizar, CancellationToken cancellationToken)
    {
        var obtido = await cache.ObterAsync(Chave.Unica, atualizar, cancellationToken);
        return obtido.IsSuccess ? new CatalogoObtido(obtido.Value.Valor, obtido.Value.Falha) : obtido.Error;
    }

    private async Task<Result<CatalogoLido, Erro>> LerAsync(Chave chave, CancellationToken cancellationToken)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<ICatalogoSistemasRepository>();
        return await repositorio.LerAsync(cancellationToken);
    }

    /// <summary>O catálogo tem uma entrada só.</summary>
    private enum Chave
    {
        Unica,
    }
}
