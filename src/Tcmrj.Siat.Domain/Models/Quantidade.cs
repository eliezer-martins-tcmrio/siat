using System.Globalization;
using System.Numerics;
using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models;

/// <summary>
/// Regra da quantidade de itens (FR-010, FR-013): 1 a 1000, padrão 50. Não numérica é recusada;
/// numérica fora do limite é ajustada ao limite mais próximo (<c>Math.Clamp</c>).
/// </summary>
public static class Quantidade
{
    public const int Minimo = 1;

    public const int Maximo = 1000;

    public const int Padrao = 50;

    public static int Ajustar(int valor) => Math.Clamp(valor, Minimo, Maximo);

    /// <summary>Interpreta o valor vindo da URL ou do formulário. Vazio vale o padrão.</summary>
    public static Result<int, Erro> Interpretar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Padrao;
        }

        // BigInteger: números grandes demais para int continuam numéricos e vão para o limite.
        if (BigInteger.TryParse(valor.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numero))
        {
            return (int)BigInteger.Clamp(numero, Minimo, Maximo);
        }

        return Erros.Filtros.QuantidadeNaoNumerica;
    }
}
