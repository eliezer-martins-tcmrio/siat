using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>
/// Resultado de uma máquina num ambiente (data-model.md). Criada só pelas fábricas, que garantem as regras:
/// <see cref="Versao"/> só em <see cref="SituacaoDaCelula.Versao"/>, <see cref="Mensagem"/> só em
/// <see cref="SituacaoDaCelula.Erro"/> e <see cref="Caminho"/> nulo só em "não configurado" sem endereço.
/// </summary>
public sealed record LeituraDeCelula
{
    private LeituraDeCelula(
        Ambiente ambiente,
        Maquina maquina,
        SituacaoDaCelula situacao,
        string? versao,
        string? caminho,
        string? caminhoDoRaiz,
        string? mensagem)
    {
        Ambiente = ambiente;
        Maquina = maquina;
        Situacao = situacao;
        Versao = versao;
        Caminho = caminho;
        CaminhoDoRaiz = caminhoDoRaiz;
        Mensagem = mensagem;
    }

    public Ambiente Ambiente { get; }

    public Maquina Maquina { get; }

    public SituacaoDaCelula Situacao { get; }

    /// <summary>Versão já interpretada (research.md R2), sem espaços nas pontas.</summary>
    public string? Versao { get; }

    /// <summary>Endereço efetivamente consultado, com o <c>{0}</c> já trocado pela versão do sistema raiz.</summary>
    public string? Caminho { get; }

    /// <summary>Endereço do <c>.INI</c> do sistema raiz, quando houver.</summary>
    public string? CaminhoDoRaiz { get; }

    /// <summary>Motivo do erro, em português (research.md R8).</summary>
    public string? Mensagem { get; }

    public static LeituraDeCelula ComVersao(Ambiente ambiente, Maquina maquina, string versao, string caminho, string? caminhoDoRaiz = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        return new(ambiente, maquina, SituacaoDaCelula.Versao, versao.Trim(), caminho, caminhoDoRaiz, null);
    }

    public static LeituraDeCelula ComErro(Ambiente ambiente, Maquina maquina, string mensagem, string caminho, string? caminhoDoRaiz = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensagem);
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        return new(ambiente, maquina, SituacaoDaCelula.Erro, null, caminho, caminhoDoRaiz, mensagem);
    }

    /// <summary>
    /// Sem endereço (<paramref name="caminho"/> nulo) ou com conteúdo vazio ou sem <c>VERSAO=</c> (com o caminho lido,
    /// esclarecimento de 2026-11-24).
    /// </summary>
    public static LeituraDeCelula NaoConfigurada(Ambiente ambiente, Maquina maquina, string? caminho = null, string? caminhoDoRaiz = null) =>
        new(ambiente, maquina, SituacaoDaCelula.NaoConfigurado, null, caminho, caminhoDoRaiz, null);
}
