using AwesomeAssertions;
using Tcmrj.Siat.Infra.Configurations;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Validação da seção <c>Copilot</c> na leitura (specs/005-copilot-meu-consumo/contracts/configuracao.md).</summary>
public sealed class CopilotOptionsTests
{
    public const string Token = "github_pat_segredo-de-teste";

    public const string Segredo = "segredo-da-oauth-app-de-teste";

    public static CopilotOptions Validas() => new()
    {
        Organizacao = "tcmrj",
        GitHub = new GitHubCopilotOptions { Token = Token, TempoLimiteSegundos = 15 },
        OAuth = new OAuthCopilotOptions { ClientId = "Iv1.cliente", ClientSecret = Segredo },
        LeiturasSimultaneas = 4,
        Cache = new CacheCopilotOptions(),
    };

    [Fact]
    public void Valida_com_todos_os_campos()
    {
        var opcoes = Validas();

        opcoes.Validar().Should().BeEmpty();
        opcoes.OAuthCompleto.Should().BeTrue();
    }

    public static TheoryData<string, Action<CopilotOptions>> Invalidas => new()
    {
        { "Organizacao", o => o.Organizacao = " " },
        { "Token", o => o.GitHub.Token = "" },
        { "ClientId", o => o.OAuth.ClientId = null },
        { "ClientSecret", o => o.OAuth.ClientSecret = "" },
        { "TempoLimiteSegundos", o => o.GitHub.TempoLimiteSegundos = 0 },
        { "TempoLimiteSegundos", o => o.GitHub.TempoLimiteSegundos = 61 },
        { "LeiturasSimultaneas", o => o.LeiturasSimultaneas = 0 },
        { "LeiturasSimultaneas", o => o.LeiturasSimultaneas = 9 },
        { "MinutosLicencasETimes", o => o.Cache.MinutosLicencasETimes = 0 },
        { "SegundosBudgets", o => o.Cache.SegundosBudgets = -1 },
        { "HorasPerfil", o => o.Cache.HorasPerfil = 0 },
        { "MinutosRelatoriosRecentes", o => o.Cache.MinutosRelatoriosRecentes = 0 },
        { "HorasRelatoriosFechados", o => o.Cache.HorasRelatoriosFechados = 0 },
        { "HorasRelatoriosFechados", o => o.Cache.HorasRelatoriosFechados = 169 },
    };

    [Theory]
    [MemberData(nameof(Invalidas))]
    public void Invalida_com_o_campo_fora_da_regra(string campo, Action<CopilotOptions> alterar)
    {
        ArgumentNullException.ThrowIfNull(alterar);
        var opcoes = Validas();
        alterar(opcoes);

        var motivos = opcoes.Validar();

        motivos.Should().ContainSingle().Which.Should().Contain(campo);
        motivos.Should().AllSatisfy(m =>
        {
            m.Should().NotContain(Token);
            m.Should().NotContain(Segredo);
        });
    }

    [Fact]
    public void Sem_OAuth_completo_os_esquemas_nao_existem()
    {
        var opcoes = Validas();
        opcoes.OAuth.ClientSecret = null;

        opcoes.OAuthCompleto.Should().BeFalse();
    }

    [Fact]
    public void Organizacao_sem_espacos_nas_pontas()
    {
        var opcoes = Validas();
        opcoes.Organizacao = "  tcmrj ";

        opcoes.OrganizacaoNormalizada.Should().Be("tcmrj");
    }
}
