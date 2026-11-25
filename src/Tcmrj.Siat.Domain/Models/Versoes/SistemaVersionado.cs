using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>
/// Um sistema com as seis células lidas (data-model.md). Um sistema pode ter divergência e erro ao mesmo tempo e conta
/// nas duas situações.
/// </summary>
public sealed class SistemaVersionado
{
    public SistemaVersionado(int id, string nome, IReadOnlyList<LeituraDeCelula> celulas)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(celulas);

        // Sempre na ordem DSV-1, DSV-2, HML-1, HML-2, PRD-1, PRD-2, qualquer que seja a ordem recebida.
        var ordenadas = EnderecoDeVersao.Posicoes
            .Select(p => celulas.FirstOrDefault(c => c.Ambiente == p.Ambiente && c.Maquina == p.Maquina)
                ?? LeituraDeCelula.NaoConfigurada(p.Ambiente, p.Maquina))
            .ToArray();

        Id = id;
        Nome = nome;
        Celulas = ordenadas;
    }

    public int Id { get; }

    public string Nome { get; }

    /// <summary>Seis células: DSV-1, DSV-2, HML-1, HML-2, PRD-1, PRD-2.</summary>
    public IReadOnlyList<LeituraDeCelula> Celulas { get; }

    public LeituraDeCelula Celula(Ambiente ambiente, Maquina maquina) =>
        Celulas.First(c => c.Ambiente == ambiente && c.Maquina == maquina);

    /// <summary>
    /// As duas células do ambiente têm versão e as versões são diferentes (comparação ordinal), como o <c>Divergem</c>
    /// do script (research.md R2). Erro, "não configurado" ou uma máquina só nunca divergem.
    /// </summary>
    public bool DivergeEm(Ambiente ambiente)
    {
        var primeira = Celula(ambiente, Maquina.Maquina1);
        var segunda = Celula(ambiente, Maquina.Maquina2);
        return primeira.Situacao == SituacaoDaCelula.Versao
            && segunda.Situacao == SituacaoDaCelula.Versao
            && !string.Equals(primeira.Versao, segunda.Versao, StringComparison.Ordinal);
    }

    public bool TemDivergencia => Enum.GetValues<Ambiente>().Any(DivergeEm);

    public bool TemErro => Celulas.Any(c => c.Situacao == SituacaoDaCelula.Erro);

    /// <summary>Sem divergência nem erro (inclui o sistema com tudo "não configurado").</summary>
    public bool SemPendencias => !TemDivergencia && !TemErro;
}
