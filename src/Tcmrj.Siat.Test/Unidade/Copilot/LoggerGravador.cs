using Microsoft.Extensions.Logging;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Evento recebido pelo <see cref="LoggerGravador{T}"/>: nível, mensagem e propriedades (modelo e escopos).</summary>
internal sealed record EventoGravado(LogLevel Nivel, string Mensagem, IReadOnlyDictionary<string, object?> Propriedades);

/// <summary>Logger que guarda o que recebe, como o Serilog junta as propriedades do modelo e dos escopos.</summary>
internal sealed class LoggerGravador<T> : ILogger<T>
{
    private readonly List<IReadOnlyList<KeyValuePair<string, object?>>> _escopos = [];

    public List<EventoGravado> Eventos { get; } = [];

    /// <summary>Faz o próximo <c>Log</c> lançar (para testar que a falha não propaga).</summary>
    public bool Falhar { get; set; }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        var itens = state is IEnumerable<KeyValuePair<string, object?>> pares ? pares.ToList() : [];
        _escopos.Add(itens);
        return new Fim(() => _escopos.Remove(itens));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Falhar)
        {
            throw new InvalidOperationException("Banco de log indisponível.");
        }

        var propriedades = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var par in _escopos.SelectMany(e => e))
        {
            propriedades[par.Key.TrimStart('@')] = par.Value;
        }

        if (state is IEnumerable<KeyValuePair<string, object?>> modelo)
        {
            foreach (var par in modelo.Where(p => p.Key != "{OriginalFormat}"))
            {
                propriedades[par.Key] = par.Value;
            }
        }

        Eventos.Add(new EventoGravado(logLevel, formatter(state, exception), propriedades));
    }

    private sealed class Fim(Action acao) : IDisposable
    {
        public void Dispose() => acao();
    }
}
