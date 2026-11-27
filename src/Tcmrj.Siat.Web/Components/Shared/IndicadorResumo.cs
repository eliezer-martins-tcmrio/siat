using Microsoft.AspNetCore.Components;

namespace Tcmrj.Siat.Web.Components.Shared;

/// <summary>Um indicador do <see cref="ResumoNumerico"/>.</summary>
/// <param name="Rotulo">Rótulo (<c>dt</c>).</param>
/// <param name="Valor">Valor em texto; com <paramref name="Indisponivel"/>, o texto no lugar de "Indisponível".</param>
/// <param name="Detalhe">Linha menor sob o valor.</param>
/// <param name="Indisponivel">A parte falhou ou não se aplica: mostra o texto de indisponível, nunca zero.</param>
/// <param name="Conteudo">Conteúdo no lugar do valor (por exemplo, a <see cref="BarraDeUso"/>).</param>
public sealed record IndicadorResumo(
    string Rotulo,
    string? Valor = null,
    string? Detalhe = null,
    bool Indisponivel = false,
    RenderFragment? Conteudo = null);
