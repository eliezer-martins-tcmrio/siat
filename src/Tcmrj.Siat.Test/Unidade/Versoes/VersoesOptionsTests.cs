using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Web.Extensions;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Seção <c>Versoes</c> (specs/003-versoes-publicadas/contracts/configuracao.md): faixas validadas com motivo em
/// português; sem a seção, valem os padrões e o SIAT sobe (Princípio IV).
/// </summary>
public sealed class VersoesOptionsTests
{
    private static VersoesOptions Validas() => new()
    {
        ServidorPadrao = "tcmbd-prd",
        TempoLimiteSegundos = 5,
        LeiturasSimultaneas = 32,
        MinutosDeCache = 5,
    };

    [Fact]
    public void Os_valores_do_appsettings_sao_validos()
    {
        Validas().Validar().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void TempoLimite_fora_de_1_a_60_e_invalido(int valor)
    {
        var opcoes = Validas();
        opcoes.TempoLimiteSegundos = valor;

        opcoes.Validar().Should().ContainSingle().Which.Should().Be("Versoes:TempoLimiteSegundos deve estar entre 1 e 60.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public void LeiturasSimultaneas_fora_de_1_a_128_e_invalido(int valor)
    {
        var opcoes = Validas();
        opcoes.LeiturasSimultaneas = valor;

        opcoes.Validar().Should().ContainSingle().Which.Should().Be("Versoes:LeiturasSimultaneas deve estar entre 1 e 128.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void MinutosDeCache_fora_de_1_a_60_e_invalido(int valor)
    {
        var opcoes = Validas();
        opcoes.MinutosDeCache = valor;

        opcoes.Validar().Should().ContainSingle().Which.Should().Be("Versoes:MinutosDeCache deve estar entre 1 e 60.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ServidorPadrao_vazio_e_invalido(string valor)
    {
        var opcoes = Validas();
        opcoes.ServidorPadrao = valor;

        opcoes.Validar().Should().ContainSingle().Which.Should().Contain("ServidorPadrao é obrigatório");
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(60, 128, 60)]
    public void Os_limites_das_faixas_sao_validos(int tempo, int leituras, int minutos)
    {
        var opcoes = Validas();
        opcoes.TempoLimiteSegundos = tempo;
        opcoes.LeiturasSimultaneas = leituras;
        opcoes.MinutosDeCache = minutos;

        opcoes.Validar().Should().BeEmpty();
    }

    [Fact]
    public void Sem_a_secao_Versoes_o_SIAT_sobe_com_os_padroes_e_resolve_o_servico()
    {
        using var provider = Construir([]);

        FluentActions.Invoking(() => provider.GetRequiredService<IStartupValidator>().Validate()).Should().NotThrow();
        provider.GetRequiredService<IVersoesPublicadasService>().Should().NotBeNull();
        var opcoes = provider.GetRequiredService<IOptions<VersoesOptions>>().Value;
        (opcoes.ServidorPadrao, opcoes.TempoLimiteSegundos, opcoes.LeiturasSimultaneas, opcoes.MinutosDeCache)
            .Should().Be(("tcmbd-prd", 5, 32, 5));
        var cache = provider.GetRequiredService<IOptions<CacheDeVersoesOptions>>().Value;
        (cache.ServidorPadrao, cache.TempoLimiteSegundos, cache.LeiturasSimultaneas, cache.MinutosDeCache)
            .Should().Be(("tcmbd-prd", 5, 32, 5));

        using var escopo = provider.CreateScope();
        escopo.ServiceProvider.GetRequiredService<IEnderecosDeVersaoRepository>().Should().NotBeNull();
        escopo.ServiceProvider.GetRequiredService<ILeitorDeArquivoDeVersao>().Should().NotBeNull();
        escopo.ServiceProvider.GetRequiredService<LeitorDeVersoes>().Should().NotBeNull();
    }

    [Fact]
    public void Secao_Versoes_invalida_falha_na_subida_com_o_motivo()
    {
        using var provider = Construir(new() { ["Versoes:LeiturasSimultaneas"] = "0" });

        FluentActions.Invoking(() => provider.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Contain("Versoes:LeiturasSimultaneas deve estar entre 1 e 128.");
    }

    private static ServiceProvider Construir(Dictionary<string, string?> configuracao)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuracao);

        builder.AddTcmrjSiat();
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }
}
