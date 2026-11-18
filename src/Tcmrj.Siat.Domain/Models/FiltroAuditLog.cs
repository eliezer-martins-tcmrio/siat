using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models;

/// <summary>
/// Filtros da consulta AuditLog (FR-013). O período é sempre limitado: pelos últimos <see cref="Dias"/> dias (padrão
/// <see cref="DiasPadrao"/>) ou, quando informadas, pelas datas <see cref="De"/> e <see cref="Ate"/>. <c>audit.Log</c>
/// só tem índice em LogID, e sem o período os filtros por aplicação e por LogUID varriam a tabela inteira; o
/// <c>logs.sql</c> converte o período numa faixa de LogID.
/// </summary>
public sealed partial record FiltroAuditLog
{
    public const int DiasPadrao = 30;

    public const int DiasMinimo = 1;

    public const int DiasMaximo = 3650;

    /// <summary>Valor do filtro de aplicação (na tela e na URL) para os registros gravados sem AppID.</summary>
    public const string ValorSemAppId = "sem";

    /// <summary>Formato das datas na URL e no <c>&lt;input type="date"&gt;</c>.</summary>
    public const string FormatoData = "yyyy-MM-dd";

    private FiltroAuditLog(int? appId, bool semAppId, string? prefixoLogUid, int quantidade, int? dias, DateOnly? de, DateOnly? ate)
    {
        AppId = appId;
        SemAppId = semAppId;
        PrefixoLogUid = prefixoLogUid;
        Quantidade = quantidade;
        Dias = dias;
        De = de;
        Ate = ate;
    }

    /// <summary>Nulo = todas as aplicações (ou, com <see cref="SemAppId"/>, só os registros sem aplicação).</summary>
    public int? AppId { get; }

    /// <summary>Só os registros gravados sem aplicação (<c>audit.Log.AppID</c> nulo).</summary>
    public bool SemAppId { get; }

    /// <summary>
    /// Início do LogUID (<c>uniqueidentifier</c> como texto, em maiúsculas): dígitos hexadecimais e hífens, 1 a 36;
    /// nulo = sem filtro.
    /// </summary>
    public string? PrefixoLogUid { get; }

    /// <summary>1 a 1000.</summary>
    public int Quantidade { get; }

    /// <summary>Últimos N dias (1 a 3650); nulo quando o período é pelas datas.</summary>
    public int? Dias { get; }

    /// <summary>Primeiro dia do período (inclusive); nulo = sem limite inicial. Só com período por data.</summary>
    public DateOnly? De { get; }

    /// <summary>Último dia do período (inclusive); nulo = até agora. Só com período por data.</summary>
    public DateOnly? Ate { get; }

    public bool PorData => Dias is null;

    public bool TemFiltros => AppId is not null || SemAppId || PrefixoLogUid is not null;

    public static FiltroAuditLog Padrao { get; } = new(null, false, null, Models.Quantidade.Padrao, DiasPadrao, null, null);

    /// <summary>O valor do filtro de aplicação pede os registros sem AppID.</summary>
    public static bool EhSemAppId(string? valor) =>
        string.Equals(valor?.Trim(), ValorSemAppId, StringComparison.OrdinalIgnoreCase);

    public static bool PrefixoValido(string prefixo) => InicioDeGuid().IsMatch(prefixo);

    /// <summary>
    /// Com <paramref name="de"/> ou <paramref name="ate"/>, o período é pelas datas e <paramref name="dias"/> é
    /// ignorado. <paramref name="de"/> depois de <paramref name="ate"/> é recusado. <paramref name="semAppId"/> pede só
    /// os registros sem aplicação e não combina com <paramref name="appId"/>.
    /// </summary>
    public static Result<FiltroAuditLog, Erro> Create(
        int? appId,
        string? prefixoLogUid,
        int quantidade = Models.Quantidade.Padrao,
        int dias = DiasPadrao,
        DateOnly? de = null,
        DateOnly? ate = null,
        bool semAppId = false)
    {
        var prefixo = string.IsNullOrWhiteSpace(prefixoLogUid) ? null : prefixoLogUid.Trim().ToUpperInvariant();
        if (prefixo is not null && !PrefixoValido(prefixo))
        {
            return Erros.Filtros.LogUidInvalido;
        }

        if (de is { } inicio && ate is { } fim && inicio > fim)
        {
            return Erros.Filtros.PeriodoInvertido;
        }

        var porData = de is not null || ate is not null;
        return new FiltroAuditLog(
            semAppId ? null : appId,
            semAppId,
            prefixo,
            Models.Quantidade.Ajustar(quantidade),
            porData ? null : AjustarDias(dias),
            de,
            ate);
    }

    public static int AjustarDias(int dias) => Math.Clamp(dias, DiasMinimo, DiasMaximo);

    /// <summary>Dias vindos da URL ou do formulário. Vazio vale o padrão; numérico fora de 1 a 3650 vai ao limite.</summary>
    public static Result<int, Erro> InterpretarDias(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return DiasPadrao;
        }

        if (BigInteger.TryParse(valor.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numero))
        {
            return (int)BigInteger.Clamp(numero, DiasMinimo, DiasMaximo);
        }

        return Erros.Filtros.DiasNaoNumerico;
    }

    /// <summary>Data no formato <see cref="FormatoData"/>; vazio = sem data.</summary>
    public static Result<DateOnly?, Erro> InterpretarData(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return (DateOnly?)null;
        }

        return DateOnly.TryParseExact(valor.Trim(), FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : Erros.Filtros.DataInvalida;
    }

    // Sem curingas do LIKE: só o que pode aparecer num uniqueidentifier escrito por extenso.
    [GeneratedRegex(@"^[0-9A-Fa-f-]{1,36}$")]
    private static partial Regex InicioDeGuid();
}
