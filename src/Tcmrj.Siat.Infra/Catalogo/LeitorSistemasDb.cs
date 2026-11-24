using System.Globalization;
using System.Text;
using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Infra.Catalogo;

/// <summary>
/// Converte o <c>sistemasDb.js</c> (o subconjunto de JavaScript que o arquivo usa) em JSON estrito, com o mesmo
/// significado que o navegador dá ao arquivo (research.md R2, contracts/origem-sistemasdb.md):
/// <list type="bullet">
/// <item>descarta o BOM, o que vem antes do primeiro <c>[</c> (<c>var data =</c>) e o que vem depois da lista;</item>
/// <item>aceita vírgula sobrando, comentários <c>//</c> e <c>/* */</c>, nome de campo sem aspas ou entre aspas simples
/// e texto entre aspas simples;</item>
/// <item>traduz os escapes com a regra do JavaScript, inclusive <c>\c</c> sem significado, que vira <c>c</c>.</item>
/// </list>
/// Qualquer outra construção (<c>undefined</c>, expressão, concatenação, função) é
/// <see cref="Erros.Catalogo.FormatoInvalido"/> com a linha e a coluna no arquivo original. Nenhum código é executado.
/// </summary>
public static class LeitorSistemasDb
{
    /// <summary>Limite de listas e objetos aninhados; o arquivo usa três níveis.</summary>
    public const int ProfundidadeMaxima = 32;

    public static Result<string, Erro> ParaJson(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var leitor = new Leitor(texto.Length > 0 && texto[0] == '\uFEFF' ? texto[1..] : texto);
        try
        {
            return leitor.Converter();
        }
        catch (ErroDeFormato erro)
        {
            var (linha, coluna) = leitor.Posicao(erro.Indice);
            return Erros.Catalogo.FormatoInvalido(erro.Message, linha, coluna);
        }
    }

    private sealed class ErroDeFormato(string motivo, int indice) : Exception(motivo)
    {
        public int Indice { get; } = indice;
    }

    private sealed class Leitor(string texto)
    {
        private readonly StringBuilder saida = new(texto.Length);
        private int i;

        public string Converter()
        {
            i = texto.IndexOf('[', StringComparison.Ordinal);
            if (i < 0)
            {
                throw new ErroDeFormato("a lista de sistemas ('[') não foi encontrada", texto.Length);
            }

            Lista(1);

            // O que vem depois da lista (o ";" da declaração) é descartado, como o que vem antes.
            return saida.ToString();
        }

        /// <summary>Linha e coluna (a partir de 1) de um índice do texto sem o BOM.</summary>
        public (int Linha, int Coluna) Posicao(int indice)
        {
            var linha = 1;
            var inicioDaLinha = 0;
            for (var k = 0; k < Math.Min(indice, texto.Length); k++)
            {
                if (texto[k] == '\n')
                {
                    linha++;
                    inicioDaLinha = k + 1;
                }
            }

            return (linha, indice - inicioDaLinha + 1);
        }

        private bool Fim => i >= texto.Length;

        private char Atual => texto[i];

        private void Valor(int profundidade)
        {
            Espacos();
            if (Fim)
            {
                throw new ErroDeFormato("o arquivo terminou antes do fim da lista (falta ']')", i);
            }

            switch (Atual)
            {
                case '[':
                    Lista(profundidade + 1);
                    break;
                case '{':
                    Objeto(profundidade + 1);
                    break;
                case '"' or '\'':
                    Texto();
                    break;
                case '-' or (>= '0' and <= '9'):
                    Numero();
                    break;
                default:
                    if (InicioDeNome(Atual))
                    {
                        var inicio = i;
                        var nome = Nome();
                        if (nome is not ("true" or "false" or "null"))
                        {
                            throw new ErroDeFormato($"valor não aceito: {nome}", inicio);
                        }

                        saida.Append(nome);
                        break;
                    }

                    throw new ErroDeFormato($"caractere inesperado '{Atual}'", i);
            }
        }

        private void Lista(int profundidade)
        {
            Aninhar(profundidade);
            i++; // [
            saida.Append('[');
            var primeiro = true;
            while (true)
            {
                Espacos();
                if (Fim)
                {
                    throw new ErroDeFormato("o arquivo terminou antes do fim da lista (falta ']')", i);
                }

                if (Atual == ']')
                {
                    i++;
                    saida.Append(']');
                    return;
                }

                if (!primeiro)
                {
                    saida.Append(',');
                }

                Valor(profundidade);
                primeiro = false;
                Separador(']');
            }
        }

        private void Objeto(int profundidade)
        {
            Aninhar(profundidade);
            var inicio = i;
            i++; // {
            saida.Append('{');
            var primeiro = true;
            while (true)
            {
                Espacos();
                if (Fim)
                {
                    throw new ErroDeFormato("o arquivo terminou antes do fim do objeto (falta '}')", inicio);
                }

                if (Atual == '}')
                {
                    i++;
                    saida.Append('}');
                    return;
                }

                if (!primeiro)
                {
                    saida.Append(',');
                }

                // Nome do campo: entre aspas (duplas ou simples) ou identificador sem aspas.
                if (Atual is '"' or '\'')
                {
                    Texto();
                }
                else if (InicioDeNome(Atual))
                {
                    Escrever(Nome());
                }
                else
                {
                    throw new ErroDeFormato($"nome de campo esperado e encontrado '{Atual}'", i);
                }

                Espacos();
                if (Fim || Atual != ':')
                {
                    throw new ErroDeFormato("':' esperado depois do nome do campo", i);
                }

                i++;
                saida.Append(':');
                Valor(profundidade);
                primeiro = false;
                Separador('}');
            }
        }

        // Depois de um valor: vírgula (que pode sobrar antes do fechamento) ou o fechamento.
        private void Separador(char fechamento)
        {
            Espacos();
            if (Fim)
            {
                return;
            }

            if (Atual == ',')
            {
                i++;
                return;
            }

            if (Atual != fechamento)
            {
                throw new ErroDeFormato($"',' ou '{fechamento}' esperado e encontrado '{Atual}'", i);
            }
        }

        private void Aninhar(int profundidade)
        {
            if (profundidade > ProfundidadeMaxima)
            {
                throw new ErroDeFormato($"listas ou objetos aninhados demais (mais de {ProfundidadeMaxima} níveis)", i);
            }
        }

        // Espaços e comentários fora de texto (não vão para a saída).
        private void Espacos()
        {
            while (!Fim)
            {
                if (char.IsWhiteSpace(Atual) || Atual == '\uFEFF')
                {
                    i++;
                }
                else if (Atual == '/' && i + 1 < texto.Length && texto[i + 1] == '/')
                {
                    while (!Fim && Atual is not ('\n' or '\r'))
                    {
                        i++;
                    }
                }
                else if (Atual == '/' && i + 1 < texto.Length && texto[i + 1] == '*')
                {
                    var fim = texto.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (fim < 0)
                    {
                        throw new ErroDeFormato("comentário '/*' sem '*/'", i);
                    }

                    i = fim + 2;
                }
                else
                {
                    return;
                }
            }
        }

        private static bool InicioDeNome(char c) => char.IsLetter(c) || c is '_' or '$';

        private string Nome()
        {
            var inicio = i;
            while (!Fim && (char.IsLetterOrDigit(Atual) || Atual is '_' or '$'))
            {
                i++;
            }

            return texto[inicio..i];
        }

        private void Numero()
        {
            var inicio = i;
            if (Atual == '-')
            {
                i++;
            }

            while (!Fim && (char.IsAsciiDigit(Atual) || Atual is '.' or 'e' or 'E' or '+' or '-'))
            {
                i++;
            }

            var numero = texto[inicio..i];
            if (!double.TryParse(numero, NumberStyles.Float, CultureInfo.InvariantCulture, out _) || !char.IsAsciiDigit(numero[^1]))
            {
                throw new ErroDeFormato($"número inválido: {numero}", inicio);
            }

            saida.Append(numero);
        }

        // Texto entre aspas duplas ou simples, com os escapes do JavaScript; sai como texto JSON.
        private void Texto()
        {
            var inicio = i;
            var aspas = Atual;
            i++;
            var valor = new StringBuilder();
            while (true)
            {
                if (Fim || Atual is '\n' or '\r' or '\u2028' or '\u2029')
                {
                    throw new ErroDeFormato("texto sem aspas de fechamento", inicio);
                }

                var c = Atual;
                i++;
                if (c == aspas)
                {
                    break;
                }

                if (c != '\\')
                {
                    valor.Append(c);
                    continue;
                }

                if (Fim)
                {
                    throw new ErroDeFormato("texto sem aspas de fechamento", inicio);
                }

                Escape(valor);
            }

            Escrever(valor.ToString());
        }

        // Escape depois da barra (i aponta para o caractere seguinte a ela).
        private void Escape(StringBuilder valor)
        {
            var barra = i - 1;
            var c = Atual;
            i++;
            switch (c)
            {
                case 'n': valor.Append('\n'); break;
                case 'r': valor.Append('\r'); break;
                case 't': valor.Append('\t'); break;
                case 'b': valor.Append('\b'); break;
                case 'f': valor.Append('\f'); break;
                case 'v': valor.Append('\v'); break;
                case 'x':
                    valor.Append((char)Hexadecimal(2, barra));
                    break;
                case 'u' when !Fim && Atual == '{':
                    i++;
                    var fim = texto.IndexOf('}', i);
                    var digitos = fim < 0 ? string.Empty : texto[i..fim];
                    if (digitos.Length is 0 or > 6
                        || !int.TryParse(digitos, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var ponto)
                        || ponto > 0x10FFFF)
                    {
                        throw new ErroDeFormato("escape \\u{...} inválido", barra);
                    }

                    i = fim + 1;
                    valor.Append(ponto <= 0xFFFF ? ((char)ponto).ToString() : char.ConvertFromUtf32(ponto));
                    break;
                case 'u':
                    valor.Append((char)Hexadecimal(4, barra));
                    break;
                case '\r':
                    // Continuação de linha: a barra e a quebra somem.
                    if (!Fim && Atual == '\n')
                    {
                        i++;
                    }

                    break;
                case '\n' or '\u2028' or '\u2029':
                    break;
                case >= '0' and <= '7':
                    // Escape octal do JavaScript fora do modo estrito (o sistemas.html não é estrito): \0, \101.
                    var maximo = c <= '3' ? 3 : 2;
                    var octal = c - '0';
                    for (var n = 1; n < maximo && !Fim && Atual is >= '0' and <= '7'; n++)
                    {
                        octal = (octal * 8) + (Atual - '0');
                        i++;
                    }

                    valor.Append((char)octal);
                    break;
                default:
                    // \', \", \\ e qualquer outro \c: a barra é descartada (\D vira D).
                    valor.Append(c);
                    break;
            }
        }

        private int Hexadecimal(int digitos, int barra)
        {
            if (i + digitos > texto.Length
                || !int.TryParse(texto.AsSpan(i, digitos), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var valor))
            {
                throw new ErroDeFormato($"escape \\{texto[barra + 1]} inválido", barra);
            }

            i += digitos;
            return valor;
        }

        private void Escrever(string valor)
        {
            saida.Append('"');
            foreach (var c in valor)
            {
                switch (c)
                {
                    case '"': saida.Append("\\\""); break;
                    case '\\': saida.Append("\\\\"); break;
                    case < ' ' or (>= '\uD800' and <= '\uDFFF'):
                        saida.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        break;
                    default: saida.Append(c); break;
                }
            }

            saida.Append('"');
        }
    }
}
