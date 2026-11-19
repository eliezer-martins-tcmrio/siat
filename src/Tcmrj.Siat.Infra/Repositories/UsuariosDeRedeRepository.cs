using System.Data;
using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Configurations;

namespace Tcmrj.Siat.Infra.Repositories;

/// <summary>
/// Lê o vínculo do login de rede com a matrícula (<c>Sql/usuario-de-rede.sql</c>) na connection string
/// <see cref="ConnectionString"/>, a mesma das permissões do UserManager. Roda na entrada do usuário, por isso o
/// tempo limite do comando é curto e fixo.
/// </summary>
public sealed class UsuariosDeRedeRepository(IConfiguration configuration, IOptions<ConsultasOptions> opcoes)
    : IUsuariosDeRedeRepository
{
    internal const string ConnectionString = "UserManagerConnection";

    internal const int TempoLimiteComandoEmSegundos = 10;

    // LoginRede é varchar(25). O parâmetro é maior de propósito: com o tamanho exato o SqlClient cortaria um login
    // mais longo, e o pedaço poderia coincidir com o login de outra pessoa.
    private const int TamanhoParametroLogin = 128;

    private static readonly Lazy<string> Sql = new(() => LerSql("usuario-de-rede.sql"));

    public async Task<Result<IReadOnlyList<UsuarioDeRede>, Erro>> ObterPorLoginAsync(string login, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);

        try
        {
            var conexaoBuilder = new SqlConnectionStringBuilder(configuration.GetConnectionString(ConnectionString))
            {
                ConnectTimeout = opcoes.Value.ConnectTimeout,
            };

            await using var conexao = new SqlConnection(conexaoBuilder.ConnectionString);
            await conexao.OpenAsync(cancellationToken);

            await using var comando = conexao.CreateCommand();
            comando.CommandText = Sql.Value;
            comando.CommandType = CommandType.Text;
            comando.CommandTimeout = TempoLimiteComandoEmSegundos;
            comando.Parameters.Add(new SqlParameter("@LoginRede", SqlDbType.VarChar, TamanhoParametroLogin) { Value = login });

            var itens = new List<UsuarioDeRede>(2);
            await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
            {
                itens.Add(new UsuarioDeRede(
                    Convert.ToInt32(leitor["Matricula"], CultureInfo.InvariantCulture),
                    leitor["Nome"] as string,
                    leitor["Email"] as string,
                    (bool)leitor["Habilitado"]));
            }

            return Result.Success<IReadOnlyList<UsuarioDeRede>, Erro>(itens);
        }
        catch (SqlException ex)
        {
            return Erros.UsuariosDeRede.CadastroIndisponivel(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Por exemplo, connection string ausente ou tempo esgotado ao obter conexão do pool.
            return Erros.UsuariosDeRede.CadastroIndisponivel(ex.Message);
        }
    }

    private static string LerSql(string arquivo)
    {
        var assembly = typeof(UsuariosDeRedeRepository).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Sql.{arquivo}", StringComparison.Ordinal));
        using var recurso = assembly.GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"SQL embutido não encontrado: {arquivo}");
        using var leitor = new StreamReader(recurso);
        return leitor.ReadToEnd();
    }
}
