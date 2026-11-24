using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Shared;

namespace Tcmrj.Siat.App.Web.ViewModels.Versoes;

/// <summary>Versões de um servidor para a tela (data-model.md, View models).</summary>
/// <param name="Servidor">De onde vieram os endereços.</param>
/// <param name="GeradoEm">Fim da leitura (UTC).</param>
/// <param name="Duracao">Duração da leitura geral.</param>
/// <param name="Sistemas">Na ordem pt-BR pelo nome.</param>
/// <param name="Falha">A última releitura falhou e a tela mostra este resultado anterior; nulo caso contrário.</param>
public sealed record VersoesVM(
    ServidorVM Servidor,
    DateTimeOffset GeradoEm,
    TimeSpan Duracao,
    IReadOnlyList<SistemaVersoesVM> Sistemas,
    FalhaDasVersoesVM? Falha)
{
    public int Total => Sistemas.Count;

    public int SemPendencias => Sistemas.Count(s => s.SemPendencias);

    public int ComDivergencia => Sistemas.Count(s => s.TemDivergencia);

    public int ComErro => Sistemas.Count(s => s.TemErro);

    public static VersoesVM De(VersoesObtidas obtidas, ServidorVM servidor)
    {
        ArgumentNullException.ThrowIfNull(obtidas);
        ArgumentNullException.ThrowIfNull(servidor);

        var versoes = obtidas.Versoes;
        return new VersoesVM(
            servidor,
            versoes.GeradoEm,
            versoes.Duracao,
            versoes.Sistemas.Select(SistemaVersoesVM.De).ToArray(),
            obtidas.Falha is { } falha ? new FalhaDasVersoesVM(falha.Erro.Message, falha.Em) : null);
    }
}

/// <summary>Releitura que falhou: a mensagem e quando.</summary>
public sealed record FalhaDasVersoesVM(string Mensagem, DateTimeOffset Em);
