using BlazorBlueprint.Components;
using Microsoft.Extensions.Options;

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

        // Páginas Account/Login e Account/Logout (só mapeadas no OIDC).
        builder.Services.AddRazorPages();

        builder.Services.AddBlazorBlueprintComponents();

        // Caminho do aplicativo no IIS (<base href>); um valor malformado quebraria todas as URLs, então nem sobe.
        builder.Services.AddOptions<SiatOptions>()
            .Bind(builder.Configuration.GetSection(SiatOptions.Secao))
            .Validate(
                o => SiatOptions.BaseHrefValido(o.BlazorBaseHref),
                $"{SiatOptions.Secao}:{nameof(SiatOptions.BlazorBaseHref)} deve ser vazio ou começar com \"/\" e não terminar com \"/\", sem segmentos \".\" ou \"..\" (por exemplo, /siat_dsv).")
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

        // CSS, JS e imagens não têm dados; ficam fora da fallback policy para não disparar o login (SIA ou desafio
        // Negotiate) em pedidos de arquivo (ícone, folha de estilo) feitos sem sessão.
        app.MapStaticAssets().AllowAnonymous();

        // Verificação de disponibilidade (balanceador, monitoramento) sem login; no usuário de rede, sem credencial do
        // Windows (modo misto). Não lê nenhuma fonte de dados.
        app.MapGet("/saude", () => Results.Text("ok")).AllowAnonymous();

        // Login e logout no SIA. No usuário de rede não há tela de login nem sessão própria para encerrar, e os
        // esquemas do OIDC nem existem.
        if (app.Services.GetRequiredService<IOptions<SiatOptions>>().Value.Autenticacao == ModoDeAutenticacao.Oidc)
        {
            app.MapRazorPages();
        }

        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
