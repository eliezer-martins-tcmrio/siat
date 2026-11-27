namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>Relatório de um dia do período.</summary>
public sealed record DiaDeConsumo(DateOnly Data, RelatorioDeConsumo Relatorio);

/// <summary>
/// Consumo de um período, somado dia a dia (regra do <c>periodUsage</c> do gestao-copilot; research.md R5). Só existe
/// com todos os dias lidos: um dia sem resposta faz o período inteiro ser erro, nunca um total parcial (FR-020).
/// </summary>
/// <param name="Dias">Um por dia, em ordem de data (base da evolução diária).</param>
/// <param name="Modelos">Mescla dos modelos dos dias pela mesma chave, na mesma ordem do relatório.</param>
/// <param name="ConsultadoEm">Horário mostrado na página.</param>
public sealed record ConsumoDoPeriodo(
    Periodo Periodo,
    Consumo Total,
    IReadOnlyList<DiaDeConsumo> Dias,
    IReadOnlyList<ConsumoPorModelo> Modelos,
    DateTimeOffset ConsultadoEm)
{
    public static ConsumoDoPeriodo De(Periodo periodo, IReadOnlyList<DiaDeConsumo> dias, DateTimeOffset consultadoEm)
    {
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(dias);

        var emOrdem = dias.OrderBy(d => d.Data).ToArray();
        var total = emOrdem.Aggregate(Consumo.Zero, (soma, dia) => soma + dia.Relatorio.Total);
        var modelos = ConsumoPorModelo.Mesclar(emOrdem.SelectMany(d => d.Relatorio.Modelos));
        return new ConsumoDoPeriodo(periodo, total, emOrdem, modelos, consultadoEm);
    }
}
