using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tcmrj.Autenticacao.AspNetCore.Oidc;
using Tcmrj.Autenticacao.AspNetCore.Oidc.Autorizacao;
using Tcmrj.Autenticacao.AspNetCore.Oidc.Sessao;
using Tcmrj.Autorizacao.App.Services;
using Tcmrj.CoreLib.Domain.Interfaces;
using Tcmrj.Siat.Web.Configurations;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// Valida o grafo real de autorização do SIAT (sem substitutos), com validação de escopos ativa, nos dois modos de
/// entrada (<c>SiatConfiguration:Autenticacao</c>): OIDC no SIA (padrão) e usuário de rede (protótipo).
/// </summary>
public class AutorizacaoDependencyInjectionTests
{
    private const string UsuarioDeRede = nameof(ModoDeAutenticacao.UsuarioDeRede);

    [Theory]
    [InlineData(null)]
    [InlineData(UsuarioDeRede)]
    public void CadeiaDeAutorizacaoResolveDentroDoEscopoDoUsuario(string? modo)
    {
        using var provider = CriarProvider(modo);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<IAuthorizationService>().Should().NotBeNull();
        services.GetServices<IAuthorizationHandler>().Should().Contain(h => h.GetType().Name == "PermissionHandler");
        services.GetRequiredService<IUserManagerService>().Should().BeOfType<TcmrjBlazorUserManagerService>();
        services.GetRequiredService<AuthenticationStateProvider>()
            .Should().BeSameAs(services.GetRequiredService<ITcmrjInatividade>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(UsuarioDeRede)]
    public void CadeiaDeAutorizacaoNaoResolveNoProvedorRaiz(string? modo)
    {
        using var provider = CriarProvider(modo);

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
        provider.GetRequiredService<IOptions<UserManagerServiceOptions>>().Value.ApplicationId.Should().Be(93);
    }

    [Fact]
    public async Task Sem_a_chave_entra_pelo_SIA_sem_a_transformacao_do_usuario_de_rede()
    {
        using var provider = CriarProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var esquemas = services.GetRequiredService<IAuthenticationSchemeProvider>();
        (await esquemas.GetDefaultAuthenticateSchemeAsync())!.Name.Should().Be(TcmrjOidcDefaults.CookieScheme);
        (await esquemas.GetDefaultChallengeSchemeAsync())!.Name.Should().Be(TcmrjOidcDefaults.OidcScheme);
        (await esquemas.GetSchemeAsync(NegotiateDefaults.AuthenticationScheme)).Should().BeNull();
        services.GetRequiredService<IClaimsTransformation>().Should().NotBeOfType<ClaimsDoUsuarioDeRede>();
    }

    [Fact]
    public void Sem_a_chave_o_dominio_do_usuario_de_rede_nao_e_exigido()
    {
        using var provider = CriarBuilder("93", dominio: null).Services.BuildServiceProvider();

        FluentActions.Invoking(() => provider.GetRequiredService<IStartupValidator>().Validate()).Should().NotThrow();
    }

    [Fact]
    public async Task Esquema_padrao_e_Negotiate_e_a_transformacao_e_a_do_usuario_de_rede()
    {
        using var provider = CriarProvider(UsuarioDeRede);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var esquemas = services.GetRequiredService<IAuthenticationSchemeProvider>();
        (await esquemas.GetDefaultAuthenticateSchemeAsync())!.Name.Should().Be(NegotiateDefaults.AuthenticationScheme);
        (await esquemas.GetDefaultChallengeSchemeAsync())!.Name.Should().Be(NegotiateDefaults.AuthenticationScheme);
        (await esquemas.GetSchemeAsync(TcmrjOidcDefaults.OidcScheme)).Should().BeNull();
        services.GetRequiredService<IClaimsTransformation>().Should().BeOfType<ClaimsDoUsuarioDeRede>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task AplicacaoNaoSobeSemDominioDoUsuarioDeRede(string? dominio)
    {
        var builder = CriarBuilder("93", UsuarioDeRede, dominio);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();

        var iniciar = () => app.StartAsync(TestContext.Current.CancellationToken);

        (await iniciar.Should().ThrowAsync<OptionsValidationException>())
            .Which.OptionsType.Should().Be<UsuarioDeRedeOptions>();
    }

    [Fact]
    public void Usuario_de_rede_nao_sobe_em_producao()
        => FluentActions.Invoking(() => CriarBuilder("93", UsuarioDeRede, ambiente: Environments.Production))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*não é permitido em produção*");

    private static ServiceProvider CriarProvider(string? modo = null)
        => CriarBuilder("93", modo).Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private static WebApplicationBuilder CriarBuilder(
        string? applicationId,
        string? modo = null,
        string? dominio = "TCMNET",
        string ambiente = "Development")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = ambiente });

        // Começa de uma configuração limpa: o teste não depende do appsettings do projeto Web.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UserManagerConnection"] = "Server=localhost;Database=CSLIB_UserManager;Integrated Security=true;TrustServerCertificate=true",
            ["SiatConfiguration:Autenticacao"] = modo,
            ["TcmrjOidc:Authority"] = "https://localhost:44382",
            ["TcmrjOidc:ClientId"] = "SIAT-dev",
            ["UsuarioDeRede:Dominio"] = dominio,
            ["UserManagerService:ApplicationId"] = applicationId,
        });
        builder.AddAuthenticationConfiguration();

        return builder;
    }
}
