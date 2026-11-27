namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>Escopo de um budget de AI Credits considerado pelo SIAT.</summary>
public enum EscopoDeBudget
{
    /// <summary><c>organization</c>: o global da organização; nunca sai do serviço nem entra no limite vigente.</summary>
    Organizacao,

    /// <summary><c>multi_user_customer</c>: o padrão que vale para quem não tem exceção individual.</summary>
    Padrao,

    /// <summary><c>user</c>: exceção individual de uma pessoa.</summary>
    Individual,
}

/// <summary>
/// Budget de AI Credits da organização (data-model.md). Interno ao serviço: a página recebe só o
/// <see cref="LimiteVigente"/> da própria pessoa (FR-015).
/// </summary>
/// <param name="Valor">USD, não negativo.</param>
/// <param name="Usuario">Login da pessoa, só no escopo <see cref="EscopoDeBudget.Individual"/>.</param>
public sealed record Budget(string Id, EscopoDeBudget Escopo, decimal Valor, string? Usuario)
{
    public const string SkuAiCredits = "ai_credits";

    /// <summary>
    /// Budget como o SIAT o considera, ou nulo quando ele não é de <c>ai_credits</c> ou não está num dos escopos
    /// <c>organization</c>, <c>multi_user_customer</c> e <c>user</c> (regra do <c>normalizeBudget</c> do
    /// gestao-copilot). No escopo <c>user</c>, a pessoa é o campo <c>user</c> ou, sem ele, <c>budget_entity_name</c>.
    /// </summary>
    public static Budget? Normalizar(
        string id,
        string escopo,
        decimal valor,
        IReadOnlyCollection<string> skus,
        string? usuario,
        string? nomeDaEntidade)
    {
        ArgumentNullException.ThrowIfNull(skus);

        if (!skus.Contains(SkuAiCredits, StringComparer.Ordinal))
        {
            return null;
        }

        EscopoDeBudget? tipo = escopo switch
        {
            "organization" => EscopoDeBudget.Organizacao,
            "multi_user_customer" => EscopoDeBudget.Padrao,
            "user" => EscopoDeBudget.Individual,
            _ => null,
        };

        if (tipo is not { } escopoConhecido)
        {
            return null;
        }

        var pessoa = escopoConhecido == EscopoDeBudget.Individual
            ? (string.IsNullOrEmpty(usuario) ? (string.IsNullOrEmpty(nomeDaEntidade) ? null : nomeDaEntidade) : usuario)
            : null;

        return new Budget(id, escopoConhecido, valor, pessoa);
    }
}
