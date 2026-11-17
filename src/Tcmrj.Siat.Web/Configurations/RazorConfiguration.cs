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

        // Páginas Account/Login e Account/Logout.
        builder.Services.AddRazorPages();

        builder.Services.AddBlazorBlueprintComponents();

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

        // CSS, JS e imagens não têm dados; ficam fora da fallback policy para não disparar o login do SIA
        // em pedidos de arquivo (ícone, folha de estilo) feitos sem sessão.
        app.MapStaticAssets().AllowAnonymous();

        app.MapRazorPages();
        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
