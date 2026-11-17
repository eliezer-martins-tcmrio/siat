using Microsoft.Playwright;

[assembly: AssemblyFixture(typeof(Tcmrj.Siat.Test.Interface.Infra.Navegador))]

namespace Tcmrj.Siat.Test.Interface.Infra;

/// <summary>
/// Navegador compartilhado pelos testes de interface (fixture do assembly; cada teste abre o seu contexto).
/// Não baixa navegadores: usa o Microsoft Edge instalado (<c>Channel = "msedge"</c>). Variáveis de ambiente:
/// <c>SIAT_PLAYWRIGHT_CHANNEL</c> troca o canal (por exemplo, <c>chrome</c>; vazio usa o Chromium do Playwright, se
/// instalado) e <c>SIAT_PLAYWRIGHT_HEADED=1</c> mostra a janela.
/// </summary>
public sealed class Navegador : IAsyncLifetime
{
    public const string CanalPadrao = "msedge";

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Tempo máximo das esperas e asserções do Playwright nos testes.</summary>
    public static float TempoLimite { get; } = 15_000;

    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Navegador não iniciado.");

    public async ValueTask InitializeAsync()
    {
        var canal = Environment.GetEnvironmentVariable("SIAT_PLAYWRIGHT_CHANNEL");
        var comJanela = Environment.GetEnvironmentVariable("SIAT_PLAYWRIGHT_HEADED") is "1" or "true";

        Assertions.SetDefaultExpectTimeout(TempoLimite);
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = canal is null ? CanalPadrao : (canal.Length == 0 ? null : canal),
            Headless = !comJanela,
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
