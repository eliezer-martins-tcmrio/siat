using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Configurations;

namespace Tcmrj.Siat.Infra.Repositories;

/// <summary>
/// Lista fechada de servidores sobre <see cref="IOptionsMonitor{TOptions}"/>. Lista inválida é rejeitada por inteiro:
/// o erro é registrado uma vez por versão das opções e a lista fica vazia (a aplicação não cai).
/// </summary>
public sealed partial class ServidoresRepository(IOptionsMonitor<ServidoresOptions> opcoes, ILogger<ServidoresRepository> logger)
    : IServidoresRepository
{
    private sealed record Cache(ServidoresOptions? Fonte, IReadOnlyList<Servidor> Lista);

    private Cache? _cache;

    public IReadOnlyList<Servidor> Listar()
    {
        ServidoresOptions atual;
        try
        {
            atual = opcoes.CurrentValue;
        }
        catch (InvalidOperationException ex)
        {
            // Valor que o binder não converte (por exemplo, "Ambiente": "PROD"): recusa a lista sem derrubar a aplicação.
            if (_cache is not { Fonte: null })
            {
                LogListaInvalida(logger, ex.Message);
                _cache = new Cache(null, []);
            }

            return [];
        }

        var cache = _cache;
        if (cache is not null && ReferenceEquals(cache.Fonte, atual))
        {
            return cache.Lista;
        }

        var erros = ServidoresOptionsValidator.Validar(atual.Servidores);
        IReadOnlyList<Servidor> lista;
        if (erros.Count > 0)
        {
            LogListaInvalida(logger, string.Join(" | ", erros));
            lista = [];
        }
        else
        {
            lista = atual.Servidores.Select(s => s.ParaServidor()).ToArray();
        }

        _cache = new Cache(atual, lista);
        return lista;
    }

    public Servidor? ObterPorChave(string chave) =>
        Listar().FirstOrDefault(s => string.Equals(s.Chave, chave, StringComparison.Ordinal));

    [LoggerMessage(Level = LogLevel.Error, Message = "Lista de servidores inválida; usando lista vazia. Erros: {Erros}")]
    private static partial void LogListaInvalida(ILogger logger, string erros);
}
