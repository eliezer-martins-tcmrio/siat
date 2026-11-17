using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.ViewModels.Shared;

/// <summary>Servidor exibido na tela. Não expõe o endereço: a URL e a tela só conhecem a chave.</summary>
public sealed record ServidorVM(string Chave, string Nome, Ambiente Ambiente)
{
    public string AmbienteDescricao => Ambiente.Descricao();

    public string DataAmb => Ambiente.DataAmb();

    public bool EhPrd => Ambiente == Ambiente.Prd;

    public static ServidorVM De(Servidor servidor) => new(servidor.Chave, servidor.Nome, servidor.Ambiente);
}

/// <summary>Servidores permitidos de um ambiente, na ordem da lista fechada.</summary>
public sealed record GrupoServidoresVM(Ambiente Ambiente, IReadOnlyList<ServidorVM> Servidores)
{
    public string AmbienteDescricao => Ambiente.Descricao();

    public string DataAmb => Ambiente.DataAmb();
}
