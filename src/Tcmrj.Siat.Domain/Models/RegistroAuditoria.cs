namespace Tcmrj.Siat.Domain.Models;

/// <summary>Registro de <c>AUDITORIADB.audit.Log</c> + <c>audit.Log_Types</c>.</summary>
public sealed record RegistroAuditoria
{
    public const string TipoNaoCadastrado = "(tipo não cadastrado)";

    /// <summary>Ordem decrescente.</summary>
    public long LogId { get; init; }

    public byte TipoId { get; init; }

    public string Tipo { get; init; } = TipoNaoCadastrado;

    public DateTime Data { get; init; }

    public string? Mensagem { get; init; }

    /// <summary><c>CONVERT(varchar(36), LogUID)</c>.</summary>
    public string? LogUid { get; init; }

    /// <summary>Nulo quando o registro foi gravado sem aplicação.</summary>
    public int? AppId { get; init; }

    /// <summary>Formatada no detalhe (R11).</summary>
    public string? Excecao { get; init; }

    /// <summary>JSON indentado ou texto original (R11).</summary>
    public string? LogEvent { get; init; }
}
