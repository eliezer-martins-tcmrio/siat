using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Web.Extensions;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Configuração do catálogo (contracts/configuracao.md): validada na leitura, nunca na subida (Princípio IV, FR-025).
/// Os motivos de invalidez nunca contêm o token.
/// </summary>
public class CatalogoSistemasOptionsTests
{
    private const string Token = "glpat-segredo-de-teste";

    internal static CatalogoSistemasOptions Validas() => new()
    {
        GitLab = new GitLabOptions
        {
            BaseUrl = "https://git.tcmnet.tcm",
            Projeto = "asi/TCMRJDoc",
            Arquivo = "sistemasDb.js",
            Ref = "master",
            Token = Token,
        },
        TempoLimiteSegundos = 10,
        MinutosDeCache = 10,
    };

    [Fact]
    public void Valores_padrao_com_token_sao_validos()
    {
        Validas().Validar().Should().BeEmpty();
    }

    [Theory]
    [InlineData("git.tcmnet.tcm")]
    [InlineData("/api")]
    [InlineData("ftp://git.tcmnet.tcm")]
    [InlineData("file:///c:/temp")]
    [InlineData("")]
    public void BaseUrl_nao_absoluta_ou_fora_de_http_e_invalida(string baseUrl)
    {
        var opcoes = Validas();
        opcoes.GitLab.BaseUrl = baseUrl;

        var motivos = opcoes.Validar();

        motivos.Should().ContainSingle().Which.Should().Contain("BaseUrl");
        motivos.Should().NotContain(m => m.Contains(Token, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(nameof(GitLabOptions.Projeto))]
    [InlineData(nameof(GitLabOptions.Arquivo))]
    [InlineData(nameof(GitLabOptions.Ref))]
    public void Projeto_arquivo_ou_ref_vazios_sao_invalidos(string campo)
    {
        var opcoes = Validas();
        typeof(GitLabOptions).GetProperty(campo)!.SetValue(opcoes.GitLab, "  ");

        opcoes.Validar().Should().ContainSingle().Which.Should().Contain(campo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(-5)]
    public void TempoLimite_fora_de_1_a_60_e_invalido(int segundos)
    {
        var opcoes = Validas();
        opcoes.TempoLimiteSegundos = segundos;

        opcoes.Validar().Should().ContainSingle().Which.Should().Contain(nameof(CatalogoSistemasOptions.TempoLimiteSegundos));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void MinutosDeCache_fora_de_1_a_60_e_invalido(int minutos)
    {
        var opcoes = Validas();
        opcoes.MinutosDeCache = minutos;

        opcoes.Validar().Should().ContainSingle().Which.Should().Contain(nameof(CatalogoSistemasOptions.MinutosDeCache));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void Limites_de_1_e_60_sao_validos(int valor)
    {
        var opcoes = Validas();
        opcoes.TempoLimiteSegundos = valor;
        opcoes.MinutosDeCache = valor;

        opcoes.Validar().Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Token_ausente_e_invalido(string? token)
    {
        var opcoes = Validas();
        opcoes.GitLab.Token = token;

        opcoes.Validar().Should().ContainSingle().Which.Should().Contain("Token");
    }

    [Fact]
    public void Varios_problemas_aparecem_juntos_e_nenhum_motivo_contem_o_token()
    {
        var opcoes = new CatalogoSistemasOptions
        {
            GitLab = new GitLabOptions { BaseUrl = Token, Projeto = Token, Token = Token },
            TempoLimiteSegundos = 0,
            MinutosDeCache = 0,
        };

        var motivos = opcoes.Validar();

        motivos.Should().HaveCount(5);
        motivos.Should().NotContain(m => m.Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public void Urls_da_api_e_da_interface_do_GitLab()
    {
        var opcoes = Validas();

        opcoes.GitLab.UrlDaApi().Should().Be(
            new Uri("https://git.tcmnet.tcm/api/v4/projects/asi%2FTCMRJDoc/repository/files/sistemasDb.js/raw?ref=master"));
        opcoes.GitLab.UrlDaOrigem().Should().Be("https://git.tcmnet.tcm/asi/TCMRJDoc/-/blob/master/sistemasDb.js");
    }

    [Fact]
    public void Barra_no_fim_da_BaseUrl_nao_duplica_a_barra()
    {
        var opcoes = Validas();
        opcoes.GitLab.BaseUrl = "https://git.tcmnet.tcm/";

        opcoes.GitLab.UrlDaApi().AbsoluteUri.Should().StartWith("https://git.tcmnet.tcm/api/v4/");
        opcoes.GitLab.UrlDaOrigem().Should().StartWith("https://git.tcmnet.tcm/asi/");
    }

    [Fact]
    public void Sem_a_secao_CatalogoSistemas_o_SIAT_sobe_e_resolve_o_catalogo()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>());

        builder.AddTcmrjSiat();
        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });

        provider.GetRequiredService<ICatalogoSistemasService>().Should().NotBeNull();
        using var escopo = provider.CreateScope();
        escopo.ServiceProvider.GetRequiredService<ICatalogoSistemasRepository>().Should().NotBeNull();
    }
}
