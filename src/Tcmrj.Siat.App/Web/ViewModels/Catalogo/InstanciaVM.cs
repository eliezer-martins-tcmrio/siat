using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.App.Web.ViewModels.Catalogo;

/// <summary>Um lugar onde o sistema roda num ambiente.</summary>
public sealed record InstanciaVM(IReadOnlyList<EnderecoVM> Enderecos, IReadOnlyList<string> Maquinas, string? Caminho)
{
    public static InstanciaVM De(Instancia instancia)
    {
        ArgumentNullException.ThrowIfNull(instancia);
        return new InstanciaVM(instancia.Enderecos.Select(EnderecoVM.De).ToArray(), instancia.Maquinas, instancia.Caminho);
    }
}

/// <summary>
/// Endereço do campo <c>URL</c>. Só vira link o que começa por <c>http://</c> ou <c>https://</c>, sem diferenciar
/// maiúsculas (FR-009); o resto (<c>N/A</c>, nome de executável, <c>javascript:</c>) é texto.
/// </summary>
/// <param name="Texto">Como veio da origem (também o <c>href</c> quando é link).</param>
/// <param name="Exibicao">Sem o esquema quando é link; igual ao texto quando não é.</param>
public sealed record EnderecoVM(string Texto, string Exibicao, bool EhLink)
{
    private const string Http = "http://";

    private const string Https = "https://";

    public static EnderecoVM De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        if (texto.StartsWith(Https, StringComparison.OrdinalIgnoreCase))
        {
            return new EnderecoVM(texto, texto[Https.Length..], true);
        }

        if (texto.StartsWith(Http, StringComparison.OrdinalIgnoreCase))
        {
            return new EnderecoVM(texto, texto[Http.Length..], true);
        }

        return new EnderecoVM(texto, texto, false);
    }
}
