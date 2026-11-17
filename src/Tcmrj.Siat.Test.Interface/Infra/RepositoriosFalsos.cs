using CSharpFunctionalExtensions;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Interface.Infra;

/// <summary>
/// MailQueue em memória, no lugar do <c>MailQueueRepository</c> (nenhum banco é tocado). Filtra como o SQL do Visor:
/// SMTP, texto literal (sem diferenciar maiúsculas) em assunto, corpo, destinatários e MessageId, ordem decrescente
/// de <see cref="MensagemEnfileirada.DataPostagem"/> e <c>TOP (Quantidade)</c>. O atraso respeita o
/// <see cref="CancellationToken"/>: cancelado, devolve <see cref="Erros.Consultas.Cancelada"/>, como o repositório real.
/// Seguro para uso concorrente: o teste configura e o circuito consulta.
/// </summary>
public sealed class MailQueueRepositoryFalso : IMailQueueRepository
{
    private readonly Lock _trava = new();
    private readonly List<Smtp> _smtps = [];
    private readonly List<(int SmtpId, MensagemEnfileirada Mensagem)> _mensagens = [];
    private int _chamadasSmtps;
    private int _chamadasPesquisa;

    /// <summary>Atraso de cada pesquisa (para ver o estado "consultando" e testar "vale só a última").</summary>
    public TimeSpan AtrasoNaPesquisa { get; set; }

    public TimeSpan AtrasoNosSmtps { get; set; }

    /// <summary>Quando definido, a pesquisa falha com este erro.</summary>
    public Erro? ErroNaPesquisa { get; set; }

    /// <summary>Quando definido, a lista de SMTPs falha com este erro.</summary>
    public Erro? ErroNosSmtps { get; set; }

    public int ChamadasSmtps => Volatile.Read(ref _chamadasSmtps);

    public int ChamadasPesquisa => Volatile.Read(ref _chamadasPesquisa);

    public FiltroMailQueue? UltimoFiltro { get; private set; }

    public Servidor? UltimoServidor { get; private set; }

    public MailQueueRepositoryFalso DefinirSmtps(params Smtp[] smtps)
    {
        lock (_trava)
        {
            _smtps.Clear();
            _smtps.AddRange(smtps);
        }

        return this;
    }

    public MailQueueRepositoryFalso AdicionarMensagem(int smtpId, MensagemEnfileirada mensagem)
    {
        lock (_trava)
        {
            _mensagens.Add((smtpId, mensagem));
        }

        return this;
    }

    /// <summary>Volta ao estado inicial: sem SMTPs, sem mensagens, sem atraso, sem erro e contadores zerados.</summary>
    public void Limpar()
    {
        lock (_trava)
        {
            _smtps.Clear();
            _mensagens.Clear();
            UltimoFiltro = null;
            UltimoServidor = null;
        }

        AtrasoNaPesquisa = TimeSpan.Zero;
        AtrasoNosSmtps = TimeSpan.Zero;
        ErroNaPesquisa = null;
        ErroNosSmtps = null;
        Interlocked.Exchange(ref _chamadasSmtps, 0);
        Interlocked.Exchange(ref _chamadasPesquisa, 0);
    }

    public async Task<Result<IReadOnlyList<Smtp>, Erro>> ListarSmtpsAsync(Servidor servidor, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _chamadasSmtps);
        if (!await EsperarAsync(AtrasoNosSmtps, cancellationToken))
        {
            return Erros.Consultas.Cancelada;
        }

        if (ErroNosSmtps is { } erro)
        {
            return erro;
        }

        lock (_trava)
        {
            return Result.Success<IReadOnlyList<Smtp>, Erro>(_smtps.ToArray());
        }
    }

    public async Task<Result<IReadOnlyList<MensagemEnfileirada>, Erro>> PesquisarAsync(
        Servidor servidor, FiltroMailQueue filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        Interlocked.Increment(ref _chamadasPesquisa);
        lock (_trava)
        {
            UltimoFiltro = filtro;
            UltimoServidor = servidor;
        }

        if (!await EsperarAsync(AtrasoNaPesquisa, cancellationToken))
        {
            return Erros.Consultas.Cancelada;
        }

        if (ErroNaPesquisa is { } erro)
        {
            return erro;
        }

        lock (_trava)
        {
            IReadOnlyList<MensagemEnfileirada> encontradas = _mensagens
                .Where(m => filtro.SmtpId is null || m.SmtpId == filtro.SmtpId)
                .Where(m => filtro.Texto is null || Contem(m.Mensagem, filtro.Texto))
                .Select(m => m.Mensagem)
                .OrderByDescending(m => m.DataPostagem)
                .Take(filtro.Quantidade)
                .ToArray();
            return Result.Success<IReadOnlyList<MensagemEnfileirada>, Erro>(encontradas);
        }
    }

    /// <summary>Espera o atraso; falso se o token for cancelado antes (ou já estiver cancelado).</summary>
    private static async Task<bool> EsperarAsync(TimeSpan atraso, CancellationToken cancellationToken)
    {
        try
        {
            if (atraso > TimeSpan.Zero)
            {
                await Task.Delay(atraso, cancellationToken);
            }

            return !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static bool Contem(MensagemEnfileirada m, string texto) =>
        new[] { m.Assunto, m.Corpo, m.Destinatarios, m.MessageId }
            .Any(campo => campo?.Contains(texto, StringComparison.OrdinalIgnoreCase) == true);
}

/// <summary>Registro de consultas em memória, no lugar do DBLog (nada é gravado em AUDITORIADB).</summary>
public sealed class RegistroConsultaFalso : IRegistroConsultaService
{
    private readonly Lock _trava = new();
    private readonly List<RegistroConsulta> _registros = [];

    public IReadOnlyList<RegistroConsulta> Registros
    {
        get
        {
            lock (_trava)
            {
                return _registros.ToArray();
            }
        }
    }

    public Task RegistrarAsync(RegistroConsulta registro)
    {
        lock (_trava)
        {
            _registros.Add(registro);
        }

        return Task.CompletedTask;
    }

    public void Limpar()
    {
        lock (_trava)
        {
            _registros.Clear();
        }
    }
}
