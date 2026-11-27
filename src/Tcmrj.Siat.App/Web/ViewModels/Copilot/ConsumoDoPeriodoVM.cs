using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.ViewModels.Copilot;

/// <summary>Linha da tabela de modelos: rótulo, consumo e participação no valor bruto do período.</summary>
/// <param name="Informado">O GitHub informou o modelo (falso: "Modelo não informado", com texto diferenciado).</param>
/// <param name="Participacao">Fração do valor bruto do período (0 a 1); zero quando o total é zero.</param>
public sealed record ModeloVM(string Rotulo, bool Informado, Consumo Consumo, decimal Participacao);

/// <summary>Consumo de um dia, com os modelos (para a visão por modelo da evolução).</summary>
public sealed record DiaVM(DateOnly Data, Consumo Consumo, IReadOnlyList<ConsumoPorModelo> Modelos);

/// <summary>
/// Consumo do período para a tela (specs/005-copilot-meu-consumo, FR-014, FR-017 e FR-018): total, um item por dia em
/// ordem e os modelos ordenados com a participação no valor bruto.
/// </summary>
public sealed record ConsumoDoPeriodoVM(
    Periodo Periodo,
    Consumo Total,
    IReadOnlyList<DiaVM> Dias,
    IReadOnlyList<ModeloVM> Modelos,
    DateTimeOffset ConsultadoEm)
{
    public static ConsumoDoPeriodoVM De(ConsumoDoPeriodo consumo)
    {
        ArgumentNullException.ThrowIfNull(consumo);

        var bruto = consumo.Total.ValorBruto;
        return new ConsumoDoPeriodoVM(
            consumo.Periodo,
            consumo.Total,
            [.. consumo.Dias.Select(d => new DiaVM(d.Data, d.Relatorio.Total, d.Relatorio.Modelos))],
            [
                .. consumo.Modelos.Select(m => new ModeloVM(
                    m.Rotulo, m.Modelo is not null, m.Consumo, bruto == 0 ? 0 : m.Consumo.ValorBruto / bruto)),
            ],
            consumo.ConsultadoEm);
    }
}
