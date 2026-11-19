using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Web.Configurations;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// <c>SiatConfiguration:BlazorBaseHref</c> é o caminho do aplicativo no IIS (<c>/siat_dsv</c>); vazio na raiz. Um valor
/// malformado quebraria todas as URLs da aplicação, por isso ela não sobe.
/// </summary>
public sealed class SiatOptionsTests
{
    [Theory]
    [InlineData("", "/")]
    [InlineData("/siat", "/siat/")]
    [InlineData("/siat_dsv", "/siat_dsv/")]
    [InlineData("/ferramentas/siat-hml", "/ferramentas/siat-hml/")]
    public void Caminho_valido_vira_base_href_com_barra_final(string caminho, string baseHref)
    {
        SiatOptions.BaseHrefValido(caminho).Should().BeTrue();
        new SiatOptions { BlazorBaseHref = caminho }.BaseHref.Should().Be(baseHref);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/")]
    [InlineData("siat")]
    [InlineData("/siat/")]
    [InlineData("//siat")]
    [InlineData("/si at")]
    [InlineData("/siat?x=1")]
    [InlineData("https://tcmrj.tcmnet.tcm/siat")]
    public void Caminho_malformado_e_recusado(string? caminho)
        => SiatOptions.BaseHrefValido(caminho).Should().BeFalse();

    [Fact]
    public async Task Aplicacao_nao_sobe_com_caminho_malformado()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SiatConfiguration:BlazorBaseHref"] = "/siat_dsv/",
        });
        builder.AddRazorConfiguration();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();

        var iniciar = () => app.StartAsync(TestContext.Current.CancellationToken);

        (await iniciar.Should().ThrowAsync<OptionsValidationException>())
            .Which.OptionsType.Should().Be<SiatOptions>();
    }

    [Fact]
    public void Sem_a_secao_fica_na_raiz()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.Sources.Clear();
        builder.AddRazorConfiguration();
        using var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<SiatOptions>>().Value.BaseHref.Should().Be("/");
    }
}
