using System.Text.RegularExpressions;
using Tcmrj.Siat.App.Web.Services.Interfaces;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Stack trace .NET separado em linhas e trechos (R11), com as mesmas regras do <c>formatStackTrace</c> do Visor:
/// linha <c>at</c> com namespace, método, parâmetros (tipo e nome), arquivo e linha; linha da mensagem com o tipo da
/// exceção; separador de exceção interna. Aceita também o texto em português (<c>em ... na ...:linha N</c>).
/// </summary>
public sealed partial class FormatadorStackTrace : IFormatadorStackTrace
{
    private const int TempoMaximoRegexMs = 250;

    public IReadOnlyList<LinhaStackTrace> Formatar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var linhas = texto.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd().Split('\n');
        return linhas.Select(FormatarLinha).ToArray();
    }

    private static LinhaStackTrace FormatarLinha(string linha)
    {
        try
        {
            var frame = Frame().Match(linha);
            if (frame.Success)
            {
                return FormatarFrame(frame);
            }

            if (Separador().IsMatch(linha))
            {
                return new LinhaStackTrace(TipoLinhaStackTrace.Separador, [new(TipoTrechoStackTrace.Separador, linha)]);
            }

            var mensagem = Mensagem().Match(linha);
            if (mensagem.Success)
            {
                return new LinhaStackTrace(TipoLinhaStackTrace.Mensagem, SemVazios(
                [
                    new(TipoTrechoStackTrace.Simples, mensagem.Groups["pre"].Value),
                    new(TipoTrechoStackTrace.TipoExcecao, mensagem.Groups["tipo"].Value),
                    new(TipoTrechoStackTrace.Simples, mensagem.Groups["s"].Value),
                    new(TipoTrechoStackTrace.Mensagem, mensagem.Groups["msg"].Value),
                ]));
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Linha patológica (muito longa): fica sem realce.
        }

        return new LinhaStackTrace(TipoLinhaStackTrace.Simples, [new(TipoTrechoStackTrace.Simples, linha)]);
    }

    private static LinhaStackTrace FormatarFrame(Match frame)
    {
        var trechos = new List<TrechoStackTrace>
        {
            new(TipoTrechoStackTrace.Simples, frame.Groups["ind"].Value),
            new(TipoTrechoStackTrace.At, frame.Groups["at"].Value),
            new(TipoTrechoStackTrace.Simples, frame.Groups["s1"].Value),
        };

        var metodo = frame.Groups["metodo"].Value;
        var ponto = metodo.LastIndexOf('.');
        if (ponto > 0)
        {
            trechos.Add(new(TipoTrechoStackTrace.Namespace, metodo[..(ponto + 1)]));
            trechos.Add(new(TipoTrechoStackTrace.Metodo, metodo[(ponto + 1)..]));
        }
        else
        {
            trechos.Add(new(TipoTrechoStackTrace.Metodo, metodo));
        }

        trechos.Add(new(TipoTrechoStackTrace.Simples, "("));
        AdicionarParametros(trechos, frame.Groups["params"].Value);
        trechos.Add(new(TipoTrechoStackTrace.Simples, ")"));

        var temArquivo = frame.Groups["arq"].Success;
        if (temArquivo)
        {
            trechos.Add(new(TipoTrechoStackTrace.Simples, frame.Groups["s2"].Value));
            trechos.Add(new(TipoTrechoStackTrace.At, frame.Groups["in"].Value));
            trechos.Add(new(TipoTrechoStackTrace.Simples, frame.Groups["s3"].Value));
            trechos.Add(new(TipoTrechoStackTrace.Arquivo, frame.Groups["arq"].Value));
            trechos.Add(new(TipoTrechoStackTrace.Simples, ":"));
            trechos.Add(new(TipoTrechoStackTrace.RotuloLinha, frame.Groups["rot"].Value));
            trechos.Add(new(TipoTrechoStackTrace.Simples, frame.Groups["s4"].Value));
            trechos.Add(new(TipoTrechoStackTrace.NumeroLinha, frame.Groups["num"].Value));
        }

        trechos.Add(new(TipoTrechoStackTrace.Simples, frame.Groups["fim"].Value));
        return new LinhaStackTrace(TipoLinhaStackTrace.Frame, SemVazios(trechos), temArquivo);
    }

    // Vírgulas dentro de genéricos (Task<Foo, Bar>) não separam parâmetros. Cada parâmetro é "Tipo nome".
    private static void AdicionarParametros(List<TrechoStackTrace> trechos, string parametros)
    {
        var partes = VirgulaEntreParametros().Split(parametros);
        for (var i = 0; i < partes.Length; i++)
        {
            if (i > 0)
            {
                trechos.Add(new(TipoTrechoStackTrace.Simples, ","));
            }

            var parte = partes[i];
            var nucleo = parte.Trim();
            if (nucleo.Length == 0)
            {
                trechos.Add(new(TipoTrechoStackTrace.Simples, parte));
                continue;
            }

            var inicio = parte.IndexOf(nucleo, StringComparison.Ordinal);
            trechos.Add(new(TipoTrechoStackTrace.Simples, parte[..inicio]));

            var espaco = nucleo.LastIndexOf(' ');
            if (espaco <= 0)
            {
                trechos.Add(new(TipoTrechoStackTrace.Parametro, nucleo));
            }
            else
            {
                trechos.Add(new(TipoTrechoStackTrace.TipoParametro, nucleo[..espaco]));
                trechos.Add(new(TipoTrechoStackTrace.Simples, " "));
                trechos.Add(new(TipoTrechoStackTrace.Parametro, nucleo[(espaco + 1)..]));
            }

            trechos.Add(new(TipoTrechoStackTrace.Simples, parte[(inicio + nucleo.Length)..]));
        }
    }

    private static TrechoStackTrace[] SemVazios(IEnumerable<TrechoStackTrace> trechos) =>
        trechos.Where(t => t.Texto.Length > 0).ToArray();

    [GeneratedRegex(
        @"^(?<ind>\s*)(?<at>at|em)(?<s1>\s+)(?<metodo>.+?)\((?<params>.*)\)(?:(?<s2>\s+)(?<in>in|em|na)(?<s3>\s+)(?<arq>.+?):(?<rot>line|linha)(?<s4>\s+)(?<num>\d+))?(?<fim>\s*)$",
        RegexOptions.CultureInvariant,
        TempoMaximoRegexMs)]
    private static partial Regex Frame();

    [GeneratedRegex(@"^\s*-{3,}\s*(End of|Fim d[ao])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TempoMaximoRegexMs)]
    private static partial Regex Separador();

    [GeneratedRegex(
        @"^(?<pre>\s*(?:--->\s*)?)(?<tipo>[\w.$+`]+(?:Exception|Error)[\w.$+`]*:)(?<s>\s*)(?<msg>.*)$",
        RegexOptions.CultureInvariant,
        TempoMaximoRegexMs)]
    private static partial Regex Mensagem();

    [GeneratedRegex(@",(?![^<]*>)", RegexOptions.CultureInvariant, TempoMaximoRegexMs)]
    private static partial Regex VirgulaEntreParametros();
}
