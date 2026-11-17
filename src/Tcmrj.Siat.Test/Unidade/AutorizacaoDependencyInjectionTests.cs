using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tcmrj.Autenticacao.AspNetCore.Oidc.Autorizacao;
using Tcmrj.Autenticacao.AspNetCore.Oidc.Sessao;
using Tcmrj.Autorizacao.App.Services;
using Tcmrj.CoreLib.Domain.Interfaces;
using Tcmrj.Siat.Web.Configurations;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>Valida o grafo real de autorização do SIAT (sem substitutos), com validação de escopos ativa.</summary>
public class AutorizacaoDependencyInjectionTests
{
    [Fact]
    public void CadeiaDeAutorizacaoResolveDentroDoEscopoDoUsuario()
    {
        using var provider = CriarProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<IAuthorizationService>().Should().NotBeNull();
        services.GetServices<IAuthorizationHandler>().Should().Contain(h => h.GetType().Name == "PermissionHandler");
        services.GetRequiredService<IUserManagerService>().Should().BeOfType<TcmrjBlazorUserManagerService>();
        services.GetRequiredService<AuthenticationStateProvider>()
            .Should().BeSameAs(services.GetRequiredService<ITcmrjInatividade>());
    }

    [Fact]
    public void CadeiaDeAutorizacaoNaoResolveNoProvedorRaiz()
    {
        using var provider = CriarProvider();

        FluentActions.Invoking(() => provider.GetRequiredService<IAuthorizationService>()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => provider.GetRequiredService<IUserManagerService>()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => provider.GetRequiredService<ITcmrjContext>()).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData(null)]
    public async Task AplicacaoNaoSobeSemApplicationIdValido(string? applicationId)
    {
        var builder = CriarBuilder(applicationId);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();

        var iniciar = () => app.StartAsync(TestContext.Current.CancellationToken);

        (await iniciar.Should().ThrowAsync<OptionsValidationException>())
            .Which.OptionsType.Should().Be<UserManagerServiceOptions>();
    }

    [Fact]
    public void ApplicationIdValidoPassaNaValidacaoDeInicializacao()
    {
        using var provider = CriarProvider();

        FluentActions.Invoking(() => provider.GetRequiredService<IStartupValidator>().Validate()).Should().NotThrow();
        provider.GetRequiredService<IOptions<UserManagerServiceOptions>>().Value.ApplicationId.Should().Be(178);
    }

    private static ServiceProvider CriarProvider()
        => CriarBuilder("178").Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private static WebApplicationBuilder CriarBuilder(string? applicationId)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });

        // Começa de uma configuração limpa: o teste não depende do appsettings do projeto Web.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UserManagerConnection"] = "Server=localhost;Database=CSLIB_UserManager;Integrated Security=true;TrustServerCertificate=true",
            ["TcmrjOidc:Authority"] = "https://localhost:44382",
            ["TcmrjOidc:ClientId"] = "SIAT-dev",
            ["UserManagerService:ApplicationId"] = applicationId,
        });
        builder.AddAuthenticationConfiguration();

        return builder;
    }
}
