using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>
/// Um relatório de consumo do GitHub (um dia ou o acumulado de um mês), normalizado pelas regras do
/// <c>normalizeUsageReport</c> do gestao-copilot (specs/005-copilot-meu-consumo/research.md R5).
/// </summary>
/// <param name="Total">Soma de todos os itens.</param>
/// <param name="Modelos">Um por modelo, por créditos decrescentes e, no empate, pelo nome em pt-BR.</param>
/// <param name="LidoEm">Horário da leitura (base do cache).</param>
public sealed record RelatorioDeConsumo(Consumo Total, IReadOnlyList<ConsumoPorModelo> Modelos, DateTimeOffset LidoEm)
{
    private static readonly string[] Unidades = ["credits", "ai-credits"];

    /// <summary>
    /// Normaliza os itens. <c>unitType</c> aceito: <c>credits</c> ou <c>ai-credits</c>, sem diferenciar maiúsculas
    /// (a documentação usa o primeiro; os relatórios reais, o segundo); outra unidade não é somada: o relatório inteiro
    /// vira <see cref="Erros.Copilot.RespostaInesperada"/>.
    /// </summary>
    public static Result<RelatorioDeConsumo, Erro> De(IReadOnlyList<ItemDeConsumo> itens, DateTimeOffset lidoEm)
    {
        ArgumentNullException.ThrowIfNull(itens);

        if (itens.Any(i => !Unidades.Contains(i.Unidade, StringComparer.OrdinalIgnoreCase)))
        {
            return Erros.Copilot.RespostaInesperada;
        }

        var total = itens.Aggregate(Consumo.Zero, (soma, item) => soma + ConsumoDe(item));
        var modelos = ConsumoPorModelo.Mesclar(
            itens.Select(i => new ConsumoPorModelo(ConsumoPorModelo.Normalizar(i.Modelo), ConsumoDe(i))));

        return new RelatorioDeConsumo(total, modelos, lidoEm);
    }

    private static Consumo ConsumoDe(ItemDeConsumo item) => new(item.Quantidade, item.ValorBruto, item.ValorLiquido);
}
