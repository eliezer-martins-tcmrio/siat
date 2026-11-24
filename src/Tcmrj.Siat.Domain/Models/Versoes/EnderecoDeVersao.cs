using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>
/// Endereços de uma célula: o arquivo de versão e, quando houver, o <c>.INI</c> do sistema raiz cuja versão preenche o
/// <c>{0}</c> do endereço (research.md R2). <c>NULL</c> ou só espaços viram <see langword="null"/>; <see cref="Versao"/>
/// nulo é "não configurado", qualquer que seja o <see cref="SistemaRaiz"/>.
/// </summary>
public sealed record ParDeEndereco
{
    public static readonly ParDeEndereco Vazio = new(null, null);

    public ParDeEndereco(string? versao, string? sistemaRaiz)
    {
        Versao = Normalizar(versao);
        SistemaRaiz = Normalizar(sistemaRaiz);
    }

    public string? Versao { get; }

    public string? SistemaRaiz { get; }

    private static string? Normalizar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>
/// Uma linha ativa de <c>CSLIB_UserManager.dbo.Apps_VersionAddresses</c> (contracts/origem-enderecos.md). O
/// <see cref="Id"/> identifica a linha, porque nomes podem se repetir.
/// </summary>
public sealed class EnderecoDeVersao
{
    /// <summary>As seis células, na ordem da tela: DSV-1, DSV-2, HML-1, HML-2, PRD-1, PRD-2.</summary>
    public static IReadOnlyList<(Ambiente Ambiente, Maquina Maquina)> Posicoes { get; } =
    [
        (Ambiente.Dsv, Maquina.Maquina1), (Ambiente.Dsv, Maquina.Maquina2),
        (Ambiente.Hml, Maquina.Maquina1), (Ambiente.Hml, Maquina.Maquina2),
        (Ambiente.Prd, Maquina.Maquina1), (Ambiente.Prd, Maquina.Maquina2),
    ];

    /// <param name="enderecos">Posições ausentes ficam com <see cref="ParDeEndereco.Vazio"/>.</param>
    public EnderecoDeVersao(int id, string sistema, IReadOnlyDictionary<(Ambiente, Maquina), ParDeEndereco>? enderecos = null)
    {
        ArgumentNullException.ThrowIfNull(sistema);

        Id = id;
        Sistema = sistema.Trim();
        Enderecos = Posicoes.ToDictionary(
            p => p,
            p => enderecos is not null && enderecos.TryGetValue(p, out var par) && par is not null ? par : ParDeEndereco.Vazio);
    }

    public int Id { get; }

    /// <summary>Nome exibido, sem espaços nas pontas.</summary>
    public string Sistema { get; }

    /// <summary>Sempre as seis entradas de <see cref="Posicoes"/>.</summary>
    public IReadOnlyDictionary<(Ambiente Ambiente, Maquina Maquina), ParDeEndereco> Enderecos { get; }
}
