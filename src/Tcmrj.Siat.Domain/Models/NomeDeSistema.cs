using System.Globalization;
using System.Text;

namespace Tcmrj.Siat.Domain.Models;

/// <summary>
/// Comparação de nomes de sistema (specs/004-ficha-sistema/research.md R2): sem espaços nas pontas, sem acentos e em
/// minúsculas na cultura pt-BR. Vale para o vínculo contra <c>Apps_VersionAddresses.Sistema</c> (FR-008), para o nome
/// pedido no endereço da ficha (FR-012) e para os repetidos (FR-009, FR-020). Os espaços internos contam: o nome tem de
/// ser escrito como está na tabela.
/// </summary>
public static class NomeDeSistema
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Normalizar(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        var decomposto = nome.Trim().Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder(decomposto.Length);
        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(caractere);
            }
        }

        return semAcento.ToString().Normalize(NormalizationForm.FormC).ToLower(PtBr);
    }
}
