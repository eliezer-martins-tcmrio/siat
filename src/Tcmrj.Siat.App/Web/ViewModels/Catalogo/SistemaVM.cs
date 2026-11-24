using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.App.Web.ViewModels.Catalogo;

/// <summary>Uma linha da tabela de Sistemas.</summary>
/// <param name="Ambientes">Sempre as quatro entradas, na ordem DSV, HML, PRD e CONT.</param>
/// <param name="TextoDeBusca">Nome, observação, endereços, máquinas e caminhos de todas as instâncias (filtro local).</param>
public sealed record SistemaVM(string Nome, string? Observacao, IReadOnlyList<AmbienteVM> Ambientes, string TextoDeBusca)
{
    public static SistemaVM De(Sistema sistema)
    {
        ArgumentNullException.ThrowIfNull(sistema);

        var ambientes = Enum.GetValues<AmbienteDoCatalogo>()
            .Select(a => AmbienteVM.De(a, sistema.Ambientes[a]))
            .ToArray();

        var partes = new List<string> { sistema.Nome };
        if (sistema.Observacao is not null)
        {
            partes.Add(sistema.Observacao);
        }

        foreach (var instancia in ambientes.SelectMany(a => a.Instancias))
        {
            partes.AddRange(instancia.Enderecos.Select(e => e.Texto));
            partes.AddRange(instancia.Maquinas);
            if (instancia.Caminho is not null)
            {
                partes.Add(instancia.Caminho);
            }
        }

        return new SistemaVM(sistema.Nome, sistema.Observacao, ambientes, string.Join(' ', partes));
    }
}

/// <summary>Uma célula de ambiente: etiqueta (sigla, nome e cor) e as instâncias.</summary>
public sealed record AmbienteVM(AmbienteDoCatalogo Ambiente, IReadOnlyList<InstanciaVM> Instancias)
{
    public string Sigla => Ambiente.Sigla();

    public string Nome => Ambiente.Nome();

    /// <summary>Valor do atributo <c>data-amb</c>.</summary>
    public string DataAmb => Ambiente.DataAmb();

    public static AmbienteVM De(AmbienteDoCatalogo ambiente, IReadOnlyList<Instancia> instancias)
    {
        ArgumentNullException.ThrowIfNull(instancias);
        return new AmbienteVM(ambiente, instancias.Select(InstanciaVM.De).ToArray());
    }
}
