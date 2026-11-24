using Tcmrj.Siat.App.Web.Services.Interfaces;

namespace Tcmrj.Siat.App.Web.ViewModels.Sistemas;

/// <summary>Catálogo para a tela (data-model.md).</summary>
/// <param name="Sistemas">Na ordem do catálogo (nome, pt-BR).</param>
/// <param name="Ignorados">Itens do sistemasDb.js deixados de fora por estarem incompletos (FR-024).</param>
/// <param name="LidoEm">Momento da leitura (UTC).</param>
/// <param name="Origem">Link do arquivo no GitLab, para o rodapé.</param>
/// <param name="Falha">A última releitura falhou e a tela mostra esta leitura anterior (FR-023); nulo caso contrário.</param>
public sealed record CatalogoVM(
    IReadOnlyList<SistemaVM> Sistemas,
    int Ignorados,
    DateTimeOffset LidoEm,
    string Origem,
    FalhaVM? Falha)
{
    public int Total => Sistemas.Count;

    public static CatalogoVM De(CatalogoObtido obtido)
    {
        ArgumentNullException.ThrowIfNull(obtido);

        var catalogo = obtido.Catalogo;
        return new CatalogoVM(
            catalogo.Sistemas.Select(SistemaVM.De).ToArray(),
            catalogo.Ignorados,
            catalogo.LidoEm,
            catalogo.Origem,
            obtido.Falha is { } falha ? new FalhaVM(falha.Erro.Message, falha.Em) : null);
    }
}

/// <summary>Releitura que falhou: a mensagem (research.md R6) e quando.</summary>
public sealed record FalhaVM(string Mensagem, DateTimeOffset Em);
