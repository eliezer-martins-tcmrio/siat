using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models;

/// <summary>Filtros da consulta AuditLog (FR-013).</summary>
public sealed partial record FiltroAuditLog
{
    private FiltroAuditLog(int? appId, string? prefixoLogId, int quantidade)
    {
        AppId = appId;
        PrefixoLogId = prefixoLogId;
        Quantidade = quantidade;
    }

    /// <summary>Nulo = todas as aplicações.</summary>
    public int? AppId { get; }

    /// <summary>Prefixo do LogID: só dígitos, 1 a 20; nulo = sem filtro.</summary>
    public string? PrefixoLogId { get; }

    /// <summary>1 a 1000.</summary>
    public int Quantidade { get; }

    public bool TemFiltros => AppId is not null || PrefixoLogId is not null;

    public static FiltroAuditLog Padrao { get; } = new(null, null, Models.Quantidade.Padrao);

    public static bool PrefixoValido(string prefixo) => SoDigitos().IsMatch(prefixo);

    public static Result<FiltroAuditLog, Erro> Create(int? appId, string? prefixoLogId, int quantidade = Models.Quantidade.Padrao)
    {
        var prefixo = string.IsNullOrWhiteSpace(prefixoLogId) ? null : prefixoLogId.Trim();
        if (prefixo is not null && !PrefixoValido(prefixo))
        {
            return Erros.Filtros.LogIdInvalido;
        }

        return new FiltroAuditLog(appId, prefixo, Models.Quantidade.Ajustar(quantidade));
    }

    [GeneratedRegex(@"^\d{1,20}$")]
    private static partial Regex SoDigitos();
}
