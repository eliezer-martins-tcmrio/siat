using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Infra.Conexoes;
using Tcmrj.Siat.Infra.Configurations;

namespace Tcmrj.Siat.Test.Unidade;

public class FabricaDeConexaoTests
{
    [Fact]
    public void Preenche_servidor_banco_e_timeout_a_partir_da_base()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Consultas"] = "Integrated Security=true;TrustServerCertificate=true;Application Name=SIAT",
        }).Build();
        var fabrica = new FabricaDeConexao(cfg, Options.Create(new ConsultasOptions { ConnectTimeout = 7, CommandTimeout = 33 }));

        using var conexao = fabrica.Criar(new Servidor { Chave = "x", Nome = "X", Endereco = @"SRV\INST" }, "MailQueue");
        var b = new SqlConnectionStringBuilder(conexao.ConnectionString);

        b.DataSource.Should().Be(@"SRV\INST");
        b.InitialCatalog.Should().Be("MailQueue");
        b.ConnectTimeout.Should().Be(7);
        b.ApplicationName.Should().Be("SIAT");
        fabrica.CommandTimeout.Should().Be(33);
    }

    [Fact]
    public void Sem_base_usa_padrao()
    {
        var fabrica = new FabricaDeConexao(new ConfigurationBuilder().Build(), Options.Create(new ConsultasOptions()));
        using var conexao = fabrica.Criar(new Servidor { Chave = "x", Nome = "X", Endereco = "SRV" }, "B");
        var b = new SqlConnectionStringBuilder(conexao.ConnectionString);
        b.ApplicationName.Should().Be("SIAT");
        b.IntegratedSecurity.Should().BeTrue();
        b.ConnectTimeout.Should().Be(15);
    }
}
