using BlazorBlueprint.Components;

namespace Tcmrj.Siat.Web.Configurations;

public static class RazorConfiguration
{
    public static WebApplicationBuilder AddRazorConfiguration(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture("pt-BR")
                .AddSupportedCultures("pt-BR")
                .AddSupportedUICultures("pt-BR");
            options.RequestCultureProviders.Clear();
        });

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddBlazorBlueprintComponents();

        // Caminho do aplicativo no IIS (<base href>); um valor malformado quebraria todas as URLs, então nem sobe.
        builder.Services.AddOptions<SiatOptions>()
            .Bind(builder.Configuration.GetSection(SiatOptions.Secao))
            .Validate(
                o => SiatOptions.BaseHrefValido(o.BlazorBaseHref),
                $"{SiatOptions.Secao}:{nameof(SiatOptions.BlazorBaseHref)} deve ser vazio ou começar com \"/\" e não terminar com \"/\" (por exemplo, /siat_dsv).")
            .ValidateOnStart();

        return builder;
    }

    public static WebApplication UseRazorConfiguration(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRequestLocalization();
        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        // CSS, JS e imagens não têm dados; ficam fora da fallback policy para não disparar o desafio Negotiate
        // em pedidos de arquivo (ícone, folha de estilo).
        app.MapStaticAssets().AllowAnonymous();

        // Modo misto: verificação de disponibilidade (balanceador, monitoramento) sem credencial do Windows.
        // Não lê nenhuma fonte de dados.
        app.MapGet("/saude", () => Results.Text("ok")).AllowAnonymous();

        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
