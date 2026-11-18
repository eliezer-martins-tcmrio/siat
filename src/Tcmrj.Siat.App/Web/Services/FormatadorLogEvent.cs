using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tcmrj.Siat.App.Web.Services.Interfaces;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// LogEvent indentado (R11, FR-015): <c>JsonDocument.Parse</c> + <c>JsonSerializer</c> com indentação de dois espaços e
/// LF, separado em trechos por tipo de token. Conteúdo que não
/// é JSON volta como veio, sem exceção.
/// </summary>
public sealed partial class FormatadorLogEvent : IFormatadorLogEvent
{
    // Acentos e caracteres como < e & ficam como estão: o texto é exibido pelo Razor (que escapa) e copiado
    // como texto, nunca inserido como HTML.
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public LogEventFormatado Formatar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return new LogEventFormatado(string.Empty, false, []);
        }

        string indentado;
        try
        {
            using var documento = JsonDocument.Parse(texto);
            indentado = JsonSerializer.Serialize(documento.RootElement, Opcoes);
        }
        catch (JsonException)
        {
            return Original(texto);
        }

        try
        {
            return new LogEventFormatado(indentado, true, Realcar(indentado));
        }
        catch (RegexMatchTimeoutException)
        {
            return new LogEventFormatado(indentado, true, [new(TipoTrechoJson.Simples, indentado)]);
        }
    }

    private static LogEventFormatado Original(string texto) =>
        new(texto, false, [new(TipoTrechoJson.Simples, texto)]);

    // Realce por tipo de token: o texto é JSON válido gerado pelo serializador, então a busca da
    // esquerda para a direita nunca confunde um número dentro de um texto com um número.
    private static List<TrechoJson> Realcar(string json)
    {
        var trechos = new List<TrechoJson>();
        var ultimo = 0;
        foreach (Match token in Token().Matches(json))
        {
            Adicionar(trechos, TipoTrechoJson.Simples, json[ultimo..token.Index]);
            if (token.Groups["texto"].Success)
            {
                var ehChave = token.Groups["dois"].Success;
                Adicionar(trechos, ehChave ? TipoTrechoJson.Chave : TipoTrechoJson.Texto, token.Groups["texto"].Value);
                if (ehChave)
                {
                    Adicionar(trechos, TipoTrechoJson.Simples, token.Groups["dois"].Value);
                }
            }
            else if (token.Groups["literal"].Success)
            {
                Adicionar(trechos, TipoTrechoJson.Literal, token.Value);
            }
            else
            {
                Adicionar(trechos, TipoTrechoJson.Numero, token.Value);
            }

            ultimo = token.Index + token.Length;
        }

        Adicionar(trechos, TipoTrechoJson.Simples, json[ultimo..]);
        return trechos;
    }

    private static void Adicionar(List<TrechoJson> trechos, TipoTrechoJson tipo, string texto)
    {
        if (texto.Length > 0)
        {
            trechos.Add(new TrechoJson(tipo, texto));
        }
    }

    [GeneratedRegex(
        @"(?<texto>""(?:\\.|[^""\\])*"")(?<dois>\s*:)?|\b(?<literal>true|false|null)\b|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token();
}
