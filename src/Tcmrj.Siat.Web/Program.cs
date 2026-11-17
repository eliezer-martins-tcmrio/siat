var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<Tcmrj.Siat.Web.Components.App>().AddInteractiveServerRenderMode();

app.Run();

/// <summary>Ponto de entrada exposto para os testes de interface (WebApplicationFactory).</summary>
public partial class Program;
