namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>
/// Limite que vale hoje para a pessoa (FR-015; esclarecimento de 2026-11-27; regra do <c>effectiveLimit</c> do
/// gestao-copilot): as exceções individuais dela e, sem nenhuma, o padrão da organização. Mais de um budget no nível
/// aplicado é <see cref="Ambiguo"/>; nenhum, <see cref="Nenhum"/>. O global nunca entra.
/// </summary>
public abstract record LimiteVigente
{
    private LimiteVigente()
    {
    }

    /// <summary>Valor do limite; nulo em <see cref="Ambiguo"/> e <see cref="Nenhum"/>.</summary>
    public decimal? Valor => this switch
    {
        Individual individual => individual.Quantia,
        Padrao padrao => padrao.Quantia,
        _ => null,
    };

    public static LimiteVigente Calcular(IEnumerable<Budget> budgets, string login)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(login);

        var lista = budgets as IReadOnlyCollection<Budget> ?? [.. budgets];
        var individuais = lista
            .Where(b => b.Escopo == EscopoDeBudget.Individual && string.Equals(b.Usuario, login, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var aplicados = individuais.Length > 0 ? individuais : [.. lista.Where(b => b.Escopo == EscopoDeBudget.Padrao)];

        return aplicados.Length switch
        {
            0 => new Nenhum(),
            > 1 => new Ambiguo(),
            _ when individuais.Length > 0 => new Individual(aplicados[0].Valor),
            _ => new Padrao(aplicados[0].Valor),
        };
    }

    /// <summary>Uma exceção individual da pessoa.</summary>
    public sealed record Individual(decimal Quantia) : LimiteVigente;

    /// <summary>Nenhuma exceção e um budget padrão da organização.</summary>
    public sealed record Padrao(decimal Quantia) : LimiteVigente;

    /// <summary>Mais de um budget no nível aplicado ("Múltiplos budgets").</summary>
    public sealed record Ambiguo : LimiteVigente;

    /// <summary>Nenhuma exceção e nenhum padrão ("Sem limite configurado").</summary>
    public sealed record Nenhum : LimiteVigente;
}
