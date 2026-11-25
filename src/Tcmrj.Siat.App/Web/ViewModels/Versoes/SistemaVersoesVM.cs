using System.Globalization;
using System.Text;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.App.Web.ViewModels.Versoes;

/// <summary>Uma linha da tabela de Versões (data-model.md).</summary>
/// <param name="NomeNormalizado">Nome sem acento e em minúsculas, procurado pela busca (só o nome, como na tela de exemplo).</param>
/// <param name="Celulas">Seis células: DSV-1, DSV-2, HML-1, HML-2, PRD-1, PRD-2.</param>
public sealed record SistemaVersoesVM(
    int Id,
    string Nome,
    string NomeNormalizado,
    bool TemDivergencia,
    bool TemErro,
    IReadOnlyList<CelulaVersaoVM> Celulas)
{
    public bool SemPendencias => !TemDivergencia && !TemErro;

    /// <summary>Células com endereço consultado, para o detalhe da linha.</summary>
    public IEnumerable<CelulaVersaoVM> Consultadas => Celulas.Where(c => c.Caminho is not null);

    public static SistemaVersoesVM De(SistemaVersionado sistema)
    {
        ArgumentNullException.ThrowIfNull(sistema);
        return new SistemaVersoesVM(
            sistema.Id,
            sistema.Nome,
            Normalizar(sistema.Nome),
            sistema.TemDivergencia,
            sistema.TemErro,
            sistema.Celulas.Select(c => CelulaVersaoVM.De(c, sistema.DivergeEm(c.Ambiente))).ToArray());
    }

    /// <summary>Sem acento e em minúsculas (a busca do <c>FiltroLocal</c> também ignora acentos e maiúsculas).</summary>
    public static string Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder(decomposto.Length);
        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(caractere);
            }
        }

        return semAcento.ToString().Normalize(NormalizationForm.FormC).ToLower(CultureInfo.GetCultureInfo("pt-BR"));
    }
}
