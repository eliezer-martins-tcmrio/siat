using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Infra.Configurations;

namespace Tcmrj.Siat.Infra.Conexoes;

public sealed class FabricaDeConexao(IConfiguration configuration, IOptions<ConsultasOptions> opcoes) : IFabricaDeConexao
{
    public const string BasePadrao = "Integrated Security=true;TrustServerCertificate=true;Application Name=SIAT";

    public int CommandTimeout => opcoes.Value.CommandTimeout;

    public SqlConnection Criar(Servidor servidor, string banco)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        var baseCs = configuration.GetConnectionString("Consultas");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(baseCs) ? BasePadrao : baseCs)
        {
            DataSource = servidor.Endereco,
            InitialCatalog = banco,
            ConnectTimeout = opcoes.Value.ConnectTimeout,
        };
        return new SqlConnection(builder.ConnectionString);
    }
}
