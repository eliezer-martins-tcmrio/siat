using System.Globalization;
using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models.Copilot;

/// <summary>
/// Intervalo consultado, com as datas inclusivas em UTC (specs/005-copilot-meu-consumo, FR-023 a FR-025;
/// research.md R6; regras do <c>usage-period.ts</c> do gestao-copilot). A janela vai do dia 1 do mais antigo dos 24
/// meses disponíveis (o mês atual e os 23 anteriores) até hoje, com no máximo <see cref="MaximoDeDias"/> dias por
/// consulta.
/// </summary>
public sealed record Periodo(DateOnly De, DateOnly Ate)
{
    /// <summary>Formato das datas na URL e nos campos (<c>de</c>, <c>ate</c>).</summary>
    public const string Formato = "yyyy-MM-dd";

    public const int MaximoDeDias = 92;

    public const int MesesDisponiveis = 24;

    /// <summary>Quantos dias, contando hoje, ainda podem mudar no GitHub (cache curto; research.md R8).</summary>
    public const int DiasRecentes = 3;

    /// <summary>Do dia 1 do mês de <paramref name="hoje"/> até <paramref name="hoje"/>.</summary>
    public static Periodo Padrao(DateOnly hoje) => new(InicioDoMes(hoje), hoje);

    /// <summary>Primeiro dia aceito: dia 1 do mês 23 meses antes do mês de <paramref name="hoje"/>.</summary>
    public static DateOnly Minimo(DateOnly hoje) => InicioDoMes(hoje).AddMonths(-(MesesDisponiveis - 1));

    /// <summary>
    /// Período dos parâmetros da URL. Um parâmetro ausente (nulo) assume o valor do <see cref="Padrao"/>; presente, ele
    /// precisa ser uma data <see cref="Formato"/> válida. Recusa <c>De &gt; Ate</c>, datas fora da janela e mais de
    /// <see cref="MaximoDeDias"/> dias com <see cref="Erros.Copilot.PeriodoInvalido"/>.
    /// </summary>
    public static Result<Periodo, Erro> Validar(string? de, string? ate, DateOnly hoje)
    {
        var padrao = Padrao(hoje);
        if (!Ler(de, padrao.De, out var inicio) || !Ler(ate, padrao.Ate, out var fim))
        {
            return Erros.Copilot.PeriodoInvalido;
        }

        var periodo = new Periodo(inicio, fim);
        return periodo.ValidoEm(hoje) ? periodo : Erros.Copilot.PeriodoInvalido;
    }

    /// <summary>Datas em ordem, dentro da janela e com no máximo <see cref="MaximoDeDias"/> dias.</summary>
    public bool ValidoEm(DateOnly hoje) =>
        De <= Ate && De >= Minimo(hoje) && Ate <= hoje && QuantidadeDeDias <= MaximoDeDias;

    public int QuantidadeDeDias => Ate.DayNumber - De.DayNumber + 1;

    /// <summary>Todas as datas de <see cref="De"/> a <see cref="Ate"/>, em ordem.</summary>
    public IReadOnlyList<DateOnly> Dias =>
        De > Ate ? [] : [.. Enumerable.Range(0, QuantidadeDeDias).Select(De.AddDays)];

    /// <summary>O período chega ao mês de hoje: só então o uso do limite é consultado (research.md R6).</summary>
    public bool IncluiMesAtual(DateOnly hoje) => Ate >= InicioDoMes(hoje);

    /// <summary>É o período padrão, que fica fora da URL.</summary>
    public bool EhPadrao(DateOnly hoje) => this == Padrao(hoje);

    /// <summary>
    /// Hoje, Ontem, Últimos 7 dias, Últimos 30 dias, Mês atual (= padrão) e Mês anterior (do dia 1 ao último dia).
    /// </summary>
    public static IReadOnlyList<AtalhoDePeriodo> Atalhos(DateOnly hoje)
    {
        var inicioDoMes = InicioDoMes(hoje);
        var ontem = hoje.AddDays(-1);
        return
        [
            new("Hoje", new Periodo(hoje, hoje)),
            new("Ontem", new Periodo(ontem, ontem)),
            new("Últimos 7 dias", new Periodo(hoje.AddDays(-6), hoje)),
            new("Últimos 30 dias", new Periodo(hoje.AddDays(-29), hoje)),
            new("Mês atual", Padrao(hoje)),
            new("Mês anterior", new Periodo(inicioDoMes.AddMonths(-1), inicioDoMes.AddDays(-1))),
        ];
    }

    /// <summary>
    /// O relatório do dia ainda pode mudar no GitHub: os <see cref="DiasRecentes"/> últimos dias, contando hoje
    /// (<c>data ≥ hoje − 2</c>); define o cache do relatório (research.md R8).
    /// </summary>
    public static bool EhRecente(DateOnly data, DateOnly hoje) => data >= hoje.AddDays(-(DiasRecentes - 1));

    /// <summary>Data no <see cref="Formato"/> da URL.</summary>
    public static string Texto(DateOnly data) => data.ToString(Formato, CultureInfo.InvariantCulture);

    private static DateOnly InicioDoMes(DateOnly data) => new(data.Year, data.Month, 1);

    private static bool Ler(string? texto, DateOnly ausente, out DateOnly data)
    {
        if (texto is null)
        {
            data = ausente;
            return true;
        }

        return DateOnly.TryParseExact(texto, Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out data);
    }
}

/// <summary>Atalho do filtro de período: rótulo e intervalo.</summary>
public sealed record AtalhoDePeriodo(string Rotulo, Periodo Periodo);
