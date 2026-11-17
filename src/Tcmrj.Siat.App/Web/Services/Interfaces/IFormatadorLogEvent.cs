namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>Tipo de um trecho do LogEvent; a página escolhe a classe CSS (<c>json-*</c>) por ele.</summary>
public enum TipoTrechoJson
{
    /// <summary>Pontuação, espaços e quebras de linha (ou o texto inteiro, quando não é JSON).</summary>
    Simples,

    /// <summary>Nome de propriedade, com as aspas.</summary>
    Chave,

    /// <summary>Valor texto, com as aspas.</summary>
    Texto,

    Numero,

    /// <summary><c>true</c>, <c>false</c> e <c>null</c>.</summary>
    Literal,
}

/// <summary>Trecho do LogEvent formatado.</summary>
public sealed record TrechoJson(TipoTrechoJson Tipo, string Texto);

/// <summary>
/// LogEvent pronto para exibir: <see cref="Texto"/> é o JSON indentado (ou o original, quando
/// <see cref="EhJson"/> é falso) e os <see cref="Trechos"/>, juntos, formam exatamente o <see cref="Texto"/>.
/// </summary>
public sealed record LogEventFormatado(string Texto, bool EhJson, IReadOnlyList<TrechoJson> Trechos);

/// <summary>
/// Formata o LogEvent do AuditLog (R11, FR-015): JSON indentado com realce por tipo de token; conteúdo que não é
/// JSON volta como veio, sem exceção. Devolve um modelo estruturado; quem exibe monta os elementos.
/// </summary>
public interface IFormatadorLogEvent
{
    LogEventFormatado Formatar(string? texto);
}
