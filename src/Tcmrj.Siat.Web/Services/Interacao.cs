using Tcmrj.Autenticacao.AspNetCore.Oidc.Sessao;

namespace Tcmrj.Siat.Web.Services;

/// <summary>
/// Renova o prazo de inatividade (<c>TcmrjSessao:MinutosSemUsoParaLogout</c>) nas ações do usuário que não
/// mudam de página: consultar, filtrar, abrir um item e exportar. A navegação já é tratada no <c>MainLayout</c>.
/// </summary>
public interface IInteracao
{
    /// <summary>Registra uma interação do usuário no circuito atual.</summary>
    void Interagir();
}

/// <summary>Scoped (um por circuito): encapsula <see cref="ITcmrjInatividade.Interagir"/> do pacote.</summary>
internal sealed class Interacao(ITcmrjInatividade inatividade) : IInteracao
{
    public void Interagir() => inatividade.Interagir();
}
