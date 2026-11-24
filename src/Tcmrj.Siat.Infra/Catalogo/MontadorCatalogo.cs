using System.Globalization;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.Infra.Catalogo;

/// <summary>
/// Valida e monta os itens do catálogo a partir do JSON do <see cref="LeitorSistemasDb"/> (research.md R3):
/// <c>Title</c> obrigatório; <c>DSV</c>, <c>HML</c>, <c>PRD</c> e <c>CONT</c> listas de objetos; <c>Machine</c>,
/// <c>URL</c>, <c>Path</c> e <c>OBS</c> textos opcionais; campos desconhecidos ignorados. Item inválido fica de fora,
/// é contado em <see cref="CatalogoLido.Ignorados"/> e registrado no log (Warning) com a posição e o motivo.
/// </summary>
public static partial class MontadorCatalogo
{
    private static readonly JsonDocumentOptions Opcoes = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly StringComparer OrdemPtBr = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), CompareOptions.None);

    public static Result<CatalogoLido, Erro> Montar(string json, DateTimeOffset lidoEm, string origem, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(logger);

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json, Opcoes);
        }
        catch (JsonException ex)
        {
            return Erros.Catalogo.FormatoInvalido(
                "o conteúdo não é uma lista válida", (int)(ex.LineNumber ?? 0) + 1, (int)(ex.BytePositionInLine ?? 0) + 1);
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Array)
            {
                return Erros.Catalogo.FormatoInvalido("a raiz do arquivo não é uma lista", 1, 1);
            }

            if (raiz.GetArrayLength() == 0)
            {
                return Erros.Catalogo.FormatoInvalido("a lista de sistemas está vazia", 1, 1);
            }

            var sistemas = new List<Sistema>();
            var ignorados = 0;
            var posicao = 0;
            foreach (var item in raiz.EnumerateArray())
            {
                posicao++;
                var sistema = Sistema(item, out var motivo);
                if (sistema is null)
                {
                    ignorados++;
                    ItemIgnorado(logger, posicao, motivo!);
                    continue;
                }

                sistemas.Add(sistema);
            }

            var ordenados = sistemas.OrderBy(s => s.Nome, OrdemPtBr).ToArray();
            return new CatalogoLido(ordenados, ignorados, lidoEm, origem);
        }
    }

    private static Sistema? Sistema(JsonElement item, out string? motivo)
    {
        motivo = null;
        if (item.ValueKind != JsonValueKind.Object)
        {
            motivo = "não é um objeto";
            return null;
        }

        if (!item.TryGetProperty("Title", out var titulo) || titulo.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(titulo.GetString()))
        {
            motivo = "sem Title (texto não vazio)";
            return null;
        }

        var nome = titulo.GetString()!;
        if (!TextoOpcional(item, "OBS", out var observacao))
        {
            motivo = $"\"{nome.Trim()}\": OBS não é texto";
            return null;
        }

        var ambientes = new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>();
        foreach (var ambiente in Enum.GetValues<AmbienteDoCatalogo>())
        {
            var chave = ambiente.ChaveNoArquivo();
            if (!item.TryGetProperty(chave, out var lista) || lista.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (lista.ValueKind != JsonValueKind.Array)
            {
                motivo = $"\"{nome.Trim()}\": {chave} não é uma lista";
                return null;
            }

            var instancias = new List<Instancia>();
            foreach (var elemento in lista.EnumerateArray())
            {
                if (elemento.ValueKind != JsonValueKind.Object
                    || !TextoOpcional(elemento, "URL", out var url)
                    || !TextoOpcional(elemento, "Machine", out var maquina)
                    || !TextoOpcional(elemento, "Path", out var caminho))
                {
                    motivo = $"\"{nome.Trim()}\": instância de {chave} com Machine, URL ou Path que não é texto";
                    return null;
                }

                var enderecos = Partes(url, '|');
                var maquinas = Partes(maquina, ',');
                if (enderecos.Length > 0 || maquinas.Length > 0 || !string.IsNullOrWhiteSpace(caminho))
                {
                    instancias.Add(new Instancia(enderecos, maquinas, caminho));
                }
            }

            ambientes[ambiente] = instancias;
        }

        return new Sistema(nome, observacao, ambientes);
    }

    // Campo ausente ou nulo: verdadeiro e nulo. Texto: verdadeiro e o texto. Outro tipo: falso (item inválido).
    private static bool TextoOpcional(JsonElement objeto, string campo, out string? valor)
    {
        valor = null;
        if (!objeto.TryGetProperty(campo, out var elemento) || elemento.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (elemento.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        valor = elemento.GetString();
        return true;
    }

    private static string[] Partes(string? valor, char separador) =>
        string.IsNullOrWhiteSpace(valor)
            ? []
            : valor.Split(separador, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Item {Posicao} do sistemasDb.js ignorado: {Motivo}.")]
    private static partial void ItemIgnorado(ILogger logger, int posicao, string motivo);
}
