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
    public void SQL_embutido_le_a_dbo_Users_pelo_login_com_o_habilitado_primeiro()
    {
        var assembly = typeof(UsuariosDeRedeRepository).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Sql.usuario-de-rede.sql", StringComparison.Ordinal));
        using var leitor = new StreamReader(assembly.GetManifestResourceStream(nome)!);

        var sql = leitor.ReadToEnd();

        // O login é único só entre os habilitados (índice filtrado): sem a ordem, um desabilitado com o mesmo login
        // poderia esconder o habilitado.
        sql.Should().Contain("FROM dbo.Users").And.Contain("u.Login = @Login")
            .And.Contain("TOP (1)").And.Contain("ORDER BY u.Enabled DESC")
            .And.NotContain("vwSIRHU_Integracao");
    }
}
