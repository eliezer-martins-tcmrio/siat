using System.Data;
using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Data.SqlClient;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Conexoes;

namespace Tcmrj.Siat.Infra.Repositories;

/// <summary>
/// Leitura do <c>AUDITORIADB</c> (banco <see cref="Servidor.BancoAuditoria"/>) de um servidor da lista fechada, com o
/// SQL embutido (R5): <c>Sql/apps.sql</c> e <c>Sql/logs.sql</c> (filtro pelo início do
/// LogUID e período por dias ou por datas, convertido numa faixa de LogID). As aplicações vêm de
/// <c>CSLIB_UserManager.dbo.Apps</c> pelo nome de três partes, na mesma conexão do <c>AUDITORIADB</c>.
/// O <see cref="CancellationToken"/> vai para <c>OpenAsync</c>, <c>ExecuteReaderAsync</c> e <c>ReadAsync</c>, e o
/// SqlClient interrompe o comando no servidor (SC-004).
/// </summary>
public sealed class AuditLogRepository(IFabricaDeConexao fabrica) : IAuditLogRepository
{
    internal const string BancoAplicacoes = "CSLIB_UserManager";

    private static readonly Lazy<string> SqlLogs = new(() => LerSql("logs.sql"));

    private static readonly Lazy<string> SqlAplicacoes = new(() => LerSql("apps.sql"));

    public Task<Result<IReadOnlyList<Aplicacao>, Erro>> ListarAplicacoesAsync(Servidor servidor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);

        // Falha aqui (em geral, sem acesso ao CSLIB_UserManager) só afeta a lista: a feature a transforma em aviso.
        return Executar(
            servidor,
            BancoAplicacoes,
            SqlAplicacoes.Value,
            [],
            leitor => new Aplicacao(
                Convert.ToInt32(leitor.GetValue(0), CultureInfo.InvariantCulture),
                leitor.IsDBNull(1) ? string.Empty : leitor.GetString(1)),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<RegistroAuditoria>, Erro>> PesquisarAsync(
        Servidor servidor, FiltroAuditLog filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        ArgumentNullException.ThrowIfNull(filtro);

        // Tipos e tamanhos fixos: o plano em cache depende deles. O período vira faixa de LogID no banco (logs.sql).
        SqlParameter[] parametros =
        [
            Parametro("@Dias", SqlDbType.Int, filtro.Dias),
            Parametro("@De", SqlDbType.DateTime, filtro.De?.ToDateTime(TimeOnly.MinValue)),
            // A data final é inclusiva: o SQL usa "antes do dia seguinte".
            Parametro("@Ate", SqlDbType.DateTime, filtro.Ate?.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            Parametro("@Top", SqlDbType.Int, filtro.Quantidade),
            Parametro("@AppID", SqlDbType.Int, filtro.AppId),
            Parametro("@SemAppID", SqlDbType.Bit, filtro.SemAppId),
            Parametro("@LogUID", SqlDbType.VarChar, filtro.PrefixoLogUid, 36),
        ];

        return Executar(servidor, servidor.BancoAuditoria, SqlLogs.Value, parametros, LerRegistro, cancellationToken);
    }

    private static RegistroAuditoria LerRegistro(SqlDataReader leitor) => new()
    {
        LogId = Convert.ToInt64(leitor["LogID"], CultureInfo.InvariantCulture),
        TipoId = Convert.ToByte(leitor["TypeID"], CultureInfo.InvariantCulture),
        Tipo = Texto(leitor, "Type") ?? RegistroAuditoria.TipoNaoCadastrado,
        Data = (DateTime)leitor["Date"],
        Mensagem = Texto(leitor, "Message"),
        LogUid = Texto(leitor, "LogUID"),
        // audit.Log.AppID aceita NULL (registros gravados sem aplicação).
        AppId = leitor["AppID"] is DBNull ? null : Convert.ToInt32(leitor["AppID"], CultureInfo.InvariantCulture),
        Excecao = Texto(leitor, "Exception"),
        LogEvent = Texto(leitor, "LogEvent"),
    };

    private static string? Texto(SqlDataReader leitor, string coluna) =>
        leitor[coluna] is string valor ? valor : null;

    private async Task<Result<IReadOnlyList<T>, Erro>> Executar<T>(
        Servidor servidor,
        string banco,
        string sql,
        SqlParameter[] parametros,
        Func<SqlDataReader, T> ler,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexao = fabrica.Criar(servidor, servidor.BancoAuditoria);
            await conexao.OpenAsync(cancellationToken);

            await using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            comando.CommandType = CommandType.Text;
            comando.CommandTimeout = fabrica.CommandTimeout;
            comando.Parameters.AddRange(parametros);

            var itens = new List<T>();
            await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
            {
                itens.Add(ler(leitor));
            }

            return Result.Success<IReadOnlyList<T>, Erro>(itens);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is OperationCanceledException or SqlException)
        {
            return Erros.Consultas.Cancelada;
        }
        catch (SqlException ex)
        {
            return Converter(ex, servidor, banco);
        }
        catch (InvalidOperationException ex)
        {
            // Por exemplo, tempo esgotado ao obter conexão do pool.
            return Erros.Consultas.ServidorIndisponivel(servidor.Nome, ex.Message);
        }
    }

    /// <summary>
    /// Converte o erro do SQL Server no erro da tela, sempre com o servidor e, quando o problema é de acesso, com o
    /// banco: o da conexão (<see cref="Servidor.BancoAuditoria"/>) quando ele não abre, senão o banco consultado.
    /// </summary>
    internal static Erro Converter(SqlException ex, Servidor servidor, string banco)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(servidor);

        return ex.Number switch
        {
            // Tempo de comando esgotado.
            -2 => Erros.Consultas.TempoEsgotado(servidor.Nome),

            // O banco da conexão não abre, ou o login foi recusado.
            4060 or 18456 => Erros.Consultas.BancoInacessivel(servidor.Nome, servidor.BancoAuditoria),

            // Sem permissão (916, 927, 229, 230) ou objeto inexistente (208, por exemplo CSLIB_UserManager.dbo.Apps).
            916 or 927 or 229 or 230 or 208 => Erros.Consultas.BancoInacessivel(servidor.Nome, banco),

            _ => Erros.Consultas.ServidorIndisponivel(servidor.Nome, ex.Message),
        };
    }

    private static SqlParameter Parametro(string nome, SqlDbType tipo, object? valor, int tamanho = 0)
    {
        var parametro = new SqlParameter(nome, tipo) { Value = valor ?? DBNull.Value };
        if (tamanho != 0)
        {
            parametro.Size = tamanho;
        }

        return parametro;
    }

    private static string LerSql(string arquivo)
    {
        var assembly = typeof(AuditLogRepository).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Sql.{arquivo}", StringComparison.Ordinal));
        using var recurso = assembly.GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"SQL embutido não encontrado: {arquivo}");
        using var leitor = new StreamReader(recurso);
        return leitor.ReadToEnd();
    }
}
