using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.App.Web.ViewModels.Versoes;

/// <summary>Uma célula da tabela de Versões (data-model.md; contracts/rotas-ui.md, <c>CelulaVersao</c>).</summary>
/// <param name="Divergente">A versão difere da outra máquina do mesmo ambiente.</param>
public sealed record CelulaVersaoVM(
    Ambiente Ambiente,
    Maquina Maquina,
    SituacaoDaCelula Situacao,
    bool Divergente,
    string? Versao,
    string? Caminho,
    string? CaminhoDoRaiz,
    string? Mensagem)
{
    /// <summary>"DSV", "HML" ou "PRD".</summary>
    public string AmbienteDescricao => Ambiente.Descricao();

    /// <summary>Valor do atributo <c>data-amb</c>.</summary>
    public string DataAmb => Ambiente.DataAmb();

    public int NumeroDaMaquina => Maquina.Numero();

    /// <summary>A primeira coluna do ambiente leva a borda divisória.</summary>
    public bool Divisa => Maquina == Maquina.Maquina1;

    /// <summary>Por exemplo, "PRD, máquina 2: 7.23.9, difere da outra máquina".</summary>
    public string RotuloAcessivel => $"{AmbienteDescricao}, máquina {NumeroDaMaquina}: {TextoAcessivel}";

    /// <summary>Texto da situação para o leitor de tela, sem a posição.</summary>
    public string TextoAcessivel => Situacao switch
    {
        SituacaoDaCelula.Versao when Divergente => $"{Versao}, difere da outra máquina",
        SituacaoDaCelula.Versao => Versao ?? string.Empty,
        SituacaoDaCelula.Erro => "Erro ao ler a versão",
        _ => "não configurado",
    };

    public static CelulaVersaoVM De(LeituraDeCelula celula, bool divergente)
    {
        ArgumentNullException.ThrowIfNull(celula);
        return new CelulaVersaoVM(
            celula.Ambiente,
            celula.Maquina,
            celula.Situacao,
            divergente && celula.Situacao == SituacaoDaCelula.Versao,
            celula.Versao,
            celula.Caminho,
            celula.CaminhoDoRaiz,
            celula.Mensagem);
    }
}
