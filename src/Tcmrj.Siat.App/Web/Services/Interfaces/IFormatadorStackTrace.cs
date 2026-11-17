namespace Tcmrj.Siat.App.Web.Services.Interfaces;

/// <summary>Tipo de uma linha do stack trace.</summary>
public enum TipoLinhaStackTrace
{
    /// <summary>Linha sem formato reconhecido.</summary>
    Simples,

    /// <summary>Linha da mensagem: <c>Tipo.Da.Excecao: mensagem</c> (também a interna, com <c>---&gt;</c>).</summary>
    Mensagem,

    /// <summary>Linha <c>at ...</c> (ou <c>em ...</c>).</summary>
    Frame,

    /// <summary><c>--- End of inner exception stack trace ---</c> e semelhantes.</summary>
    Separador,
}

/// <summary>Tipo de um trecho de uma linha; a página escolhe a classe CSS (<c>st-*</c>) por ele.</summary>
public enum TipoTrechoStackTrace
{
    /// <summary>Texto sem realce (indentação, pontuação, espaços).</summary>
    Simples,

    TipoExcecao,

    Mensagem,

    /// <summary>A palavra <c>at</c>/<c>em</c> e <c>in</c>/<c>na</c>.</summary>
    At,

    /// <summary>Namespace e tipo do método, com o ponto final.</summary>
    Namespace,

    Metodo,

    TipoParametro,

    Parametro,

    Arquivo,

    /// <summary>A palavra <c>line</c>/<c>linha</c>.</summary>
    RotuloLinha,

    NumeroLinha,

    Separador,
}

/// <summary>Trecho de uma linha do stack trace.</summary>
public sealed record TrechoStackTrace(TipoTrechoStackTrace Tipo, string Texto);

/// <summary>
/// Linha do stack trace já separada em trechos. Os trechos, juntos, formam exatamente a linha original.
/// <see cref="ProprioCodigo"/> indica um frame com arquivo e linha (código com símbolos, em geral o da aplicação).
/// </summary>
public sealed record LinhaStackTrace(TipoLinhaStackTrace Tipo, IReadOnlyList<TrechoStackTrace> Trechos, bool ProprioCodigo = false)
{
    public string Texto => string.Concat(Trechos.Select(t => t.Texto));
}

/// <summary>
/// Formata a exceção do AuditLog (R11): quebra por linha e marca as linhas <c>at ...</c> e a linha da mensagem.
/// Devolve um modelo estruturado; quem exibe monta os elementos (nunca HTML em texto).
/// </summary>
public interface IFormatadorStackTrace
{
    /// <summary>Linhas do stack trace (CRLF ou LF), sem as linhas em branco do fim. Nulo ou vazio: nenhuma linha.</summary>
    IReadOnlyList<LinhaStackTrace> Formatar(string? texto);
}
