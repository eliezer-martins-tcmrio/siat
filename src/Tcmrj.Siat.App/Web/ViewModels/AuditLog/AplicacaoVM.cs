using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.ViewModels.AuditLog;

/// <summary>Aplicação do <c>CSLIB_UserManager</c> do servidor escolhido, para o filtro do AuditLog.</summary>
public sealed record AplicacaoVM(int Id, string Nome)
{
    /// <summary>Texto da opção no seletor: "Nome (Id)".</summary>
    public string Rotulo => $"{Nome} ({Id})";

    public static AplicacaoVM De(Aplicacao aplicacao)
    {
        ArgumentNullException.ThrowIfNull(aplicacao);
        return new AplicacaoVM(aplicacao.Id, aplicacao.Nome);
    }
}

/// <summary>
/// Lista de aplicações do servidor. Indisponível (sem acesso ao <c>CSLIB_UserManager</c>): vazia e com
/// <see cref="Aviso"/>; a tela troca o seletor por um campo numérico (história 2, cenário 2).
/// </summary>
public sealed record ListaAplicacoesVM(IReadOnlyList<AplicacaoVM> Itens, string? Aviso = null)
{
    public static ListaAplicacoesVM Vazia { get; } = new([]);

    public bool Disponivel => Aviso is null;

    /// <summary>Nome da aplicação, ou nulo quando ela não está na lista.</summary>
    public string? NomeDe(int? appId) => appId is null ? null : Itens.FirstOrDefault(a => a.Id == appId)?.Nome;
}
