using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Infra.Repositories;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>PROTÓTIPO (features/prototipo_login_rede): falha do cadastro vira erro, nunca exceção na entrada do usuário.</summary>
public sealed class UsuariosDeRedeRepositoryTests
{
    [Fact]
    public async Task Sem_connection_string_devolve_cadastro_indisponivel()
    {
        var repositorio = new UsuariosDeRedeRepository(
            new ConfigurationBuilder().Build(),
            Options.Create(new ConsultasOptions()));

        var resultado = await repositorio.ObterPorLoginAsync("fulano", TestContext.Current.CancellationToken);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.UsuariosDeRede.CodigoCadastroIndisponivel);
    }

    [Fact]
    public void SQL_embutido_le_da_dbo_Users_so_usuario_interno_e_habilitado_pelo_login()
    {
        var assembly = typeof(UsuariosDeRedeRepository).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Sql.usuario-de-rede.sql", StringComparison.Ordinal));
        using var leitor = new StreamReader(assembly.GetManifestResourceStream(nome)!);

        var sql = leitor.ReadToEnd();

        // Desabilitado não entra, e o login só é único entre os habilitados (índice filtrado). Externos têm Id a partir
        // de 1.000.001.
        sql.Should().Contain("FROM dbo.Users").And.Contain("u.Login = @Login")
            .And.Contain("u.Enabled = 1").And.Contain("u.Id < 1000000")
            .And.NotContain("vwSIRHU_Integracao");
    }
}
