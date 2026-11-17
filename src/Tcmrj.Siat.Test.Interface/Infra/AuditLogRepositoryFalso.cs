using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Interface.Infra;

/// <summary>
/// AuditLog em memória, no lugar do <c>AuditLogRepository</c> (nenhum banco é tocado). Filtra como o SQL do Visor:
/// <c>AppID</c>, LogID começando com o prefixo, ordem decrescente de LogID e <c>TOP (Quantidade)</c>. O atraso
/// respeita o <see cref="CancellationToken"/>: cancelado, devolve <see cref="Erros.Consultas.Cancelada"/>, como o
/// repositório real. Seguro para uso concorrente: o teste configura e o circuito consulta.
/// </summary>
public sealed class AuditLogRepositoryFalso : IAuditLogRepository
{
    private readonly Lock _trava = new();
    private readonly List<Aplicacao> _aplicacoes = [];
    private readonly List<RegistroAuditoria> _registros = [];
    private int _chamadasAplicacoes;
    private int _chamadasPesquisa;

    /// <summary>Atraso de cada pesquisa (para ver o estado "consultando" e testar "vale só a última").</summary>
    public TimeSpan AtrasoNaPesquisa { get; set; }

    /// <summary>Quando definido, a pesquisa falha com este erro.</summary>
    public Erro? ErroNaPesquisa { get; set; }

    /// <summary>Quando definido, a lista de aplicações falha com este erro (por exemplo, sem acesso ao CSLIB_UserManager).</summary>
    public Erro? ErroNasAplicacoes { get; set; }

    public int ChamadasAplicacoes => Volatile.Read(ref _chamadasAplicacoes);

    public int ChamadasPesquisa => Volatile.Read(ref _chamadasPesquisa);

    public FiltroAuditLog? UltimoFiltro { get; private set; }

    public Servidor? UltimoServidor { get; private set; }

    public AuditLogRepositoryFalso DefinirAplicacoes(params Aplicacao[] aplicacoes)
    {
        lock (_trava)
        {
            _aplicacoes.Clear();
            _aplicacoes.AddRange(aplicacoes);
        }

        return this;
    }

    public AuditLogRepositoryFalso AdicionarRegistro(RegistroAuditoria registro)
    {
        lock (_trava)
        {
            _registros.Add(registro);
        }

        return this;
    }

    /// <summary>Volta ao estado inicial: sem aplicações, sem registros, sem atraso, sem erro e contadores zerados.</summary>
    public void Limpar()
    {
        lock (_trava)
        {
            _aplicacoes.Clear();
            _registros.Clear();
            UltimoFiltro = null;
            UltimoServidor = null;
        }

        AtrasoNaPesquisa = TimeSpan.Zero;
        ErroNaPesquisa = null;
        ErroNasAplicacoes = null;
        Interlocked.Exchange(ref _chamadasAplicacoes, 0);
        Interlocked.Exchange(ref _chamadasPesquisa, 0);
    }

    public Task<Result<IReadOnlyList<Aplicacao>, Erro>> ListarAplicacoesAsync(Servidor servidor, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _chamadasAplicacoes);
        if (ErroNasAplicacoes is { } erro)
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<Aplicacao>, Erro>(erro));
        }

        lock (_trava)
        {
            // Como o SQL do Visor: ORDER BY Name.
            IReadOnlyList<Aplicacao> aplicacoes = _aplicacoes.OrderBy(a => a.Nome, StringComparer.Ordinal).ToArray();
            return Task.FromResult(Result.Success<IReadOnlyList<Aplicacao>, Erro>(aplicacoes));
        }
    }

    public async Task<Result<IReadOnlyList<RegistroAuditoria>, Erro>> PesquisarAsync(
        Servidor servidor, FiltroAuditLog filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        Interlocked.Increment(ref _chamadasPesquisa);
        lock (_trava)
        {
            UltimoFiltro = filtro;
            UltimoServidor = servidor;
        }

        try
        {
            if (AtrasoNaPesquisa > TimeSpan.Zero)
            {
                await Task.Delay(AtrasoNaPesquisa, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return Erros.Consultas.Cancelada;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Erros.Consultas.Cancelada;
        }

        if (ErroNaPesquisa is { } erro)
        {
            return erro;
        }

        lock (_trava)
        {
            IReadOnlyList<RegistroAuditoria> encontrados = _registros
                .Where(r => filtro.AppId is null || r.AppId == filtro.AppId)
                .Where(r => filtro.PrefixoLogId is null
                    || r.LogId.ToString(CultureInfo.InvariantCulture).StartsWith(filtro.PrefixoLogId, StringComparison.Ordinal))
                .OrderByDescending(r => r.LogId)
                .Take(filtro.Quantidade)
                .ToArray();
            return Result.Success<IReadOnlyList<RegistroAuditoria>, Erro>(encontrados);
        }
    }
}

/// <summary>Host de teste com o <see cref="AuditLogRepositoryFalso"/> no lugar do repositório real do AuditLog.</summary>
public sealed class AuditLogWebApplicationFactory : SiatWebApplicationFactory
{
    /// <summary>AuditLog em memória usado pela aplicação.</summary>
    public AuditLogRepositoryFalso AuditLog { get; } = new();

    protected override void ConfigurarServicosDeTeste(IServiceCollection services)
    {
        services.RemoveAll<IAuditLogRepository>();
        services.AddSingleton<IAuditLogRepository>(AuditLog);
    }
}
