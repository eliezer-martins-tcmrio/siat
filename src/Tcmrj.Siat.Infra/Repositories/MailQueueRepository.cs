using System.Data;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Data.SqlClient;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Conexoes;

namespace Tcmrj.Siat.Infra.Repositories;

/// <summary>
/// Leitura do banco MailQueue (<see cref="Servidor.BancoMailQueue"/>) de um servidor da lista fechada, com o SQL
/// embutido (<c>Sql/mensagens.sql</c> e <c>Sql/smtps.sql</c>, R5) e só SELECT. Parâmetros tipados, para o plano em
/// cache ser reaproveitado. O <see cref="CancellationToken"/> vai para <c>OpenAsync</c>,
/// <c>ExecuteReaderAsync</c> e <c>ReadAsync</c>, e o SqlClient interrompe o comando no servidor (SC-004).
/// <para>
/// Erros viram <see cref="Erros.Consultas"/> com o servidor (e o banco, quando o problema é de acesso). Cancelamento
/// pelo token, venha como <see cref="OperationCanceledException"/> ou como <see cref="SqlException"/> do SqlClient,
/// vira sempre <see cref="Erros.Consultas.Cancelada"/>, como no <c>AuditLogRepository</c>.
/// </para>
/// </summary>
public sealed class MailQueueRepository(IFabricaDeConexao fabrica) : IMailQueueRepository
{
    private static readonly Lazy<string> SqlMensagens = new(() => LerSql("mensagens.sql"));

    private static readonly Lazy<string> SqlSmtps = new(() => LerSql("smtps.sql"));

    public Task<Result<IReadOnlyList<Smtp>, Erro>> ListarSmtpsAsync(Servidor servidor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);

        return Executar(
            servidor,
            SqlSmtps.Value,
            [],
            leitor => new Smtp(
                Convert.ToInt32(leitor["SmtpId"], System.Globalization.CultureInfo.InvariantCulture),
                leitor["Description"] as string ?? string.Empty),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<MensagemEnfileirada>, Erro>> PesquisarAsync(
        Servidor servidor, FiltroMailQueue filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);
        ArgumentNullException.ThrowIfNull(filtro);

        return Executar(servidor, SqlMensagens.Value, Parametros(filtro), LerMensagem, cancellationToken);
    }

    /// <summary>
    /// Parâmetros de <c>mensagens.sql</c>: <c>@Top int</c>, <c>@SmtpId int</c> e
    /// <c>@Texto nvarchar(max)</c> com o texto escapado por <see cref="PadraoLike"/> (nulo vai como <c>NULL</c>).
    /// </summary>
    internal static SqlParameter[] Parametros(FiltroMailQueue filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        return
        [
            Parametro("@Top", SqlDbType.Int, filtro.Quantidade),
            Parametro("@SmtpId", SqlDbType.Int, filtro.SmtpId),
            Parametro("@Texto", SqlDbType.NVarChar, filtro.Texto is null ? null : PadraoLike.Montar(filtro.Texto), -1),
        ];
    }

    /// <summary>
    /// Converte o erro do SQL Server no erro da tela, sempre com o nome do servidor; problemas de acesso levam também
    /// o banco.
    /// </summary>
    internal static Erro Converter(SqlException ex, Servidor servidor)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(servidor);

        return ex.Number switch
        {
            // Tempo de comando esgotado.
            -2 => Erros.Consultas.TempoEsgotado(servidor.Nome),

            // O banco não abre, o login foi recusado ou falta permissão no banco/objeto.
            4060 or 18456 or 916 or 927 or 229 or 230 => Erros.Consultas.BancoInacessivel(servidor.Nome, servidor.BancoMailQueue),

            // Rede, instância inexistente, servidor recusando conexão e demais erros.
            _ => Erros.Consultas.ServidorIndisponivel(servidor.Nome, ex.Message),
        };
    }

    private static MensagemEnfileirada LerMensagem(SqlDataReader leitor) => new()
    {
        MessageId = leitor["MessageId"] as string ?? string.Empty,
        SmtpId = leitor["SmtpId"] is DBNull ? null : Convert.ToInt32(leitor["SmtpId"], System.Globalization.CultureInfo.InvariantCulture),
        Assunto = leitor["Assunto"] as string,
        DataPostagem = (DateTime)leitor["DataPostagem"],
        EnviarApos = leitor["EnviarApos"] as DateTime?,
        DataEnvio = leitor["DataEnvio"] as DateTime?,
        Enviado = Convert.ToBoolean(leitor["Enviado"], System.Globalization.CultureInfo.InvariantCulture),
        EhHtml = leitor["EhHtml"] is not DBNull && Convert.ToBoolean(leitor["EhHtml"], System.Globalization.CultureInfo.InvariantCulture),
        Corpo = leitor["Corpo"] as string,
        Destinatarios = leitor["Destinatarios"] as string ?? MensagemEnfileirada.SemDestinatarios,
    };

    private async Task<Result<IReadOnlyList<T>, Erro>> Executar<T>(
        Servidor servidor,
        string sql,
        SqlParameter[] parametros,
        Func<SqlDataReader, T> ler,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexao = fabrica.Criar(servidor, servidor.BancoMailQueue);
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
            return Converter(ex, servidor);
        }
        catch (InvalidOperationException ex)
        {
            // Por exemplo, tempo esgotado ao obter conexão do pool.
            return Erros.Consultas.ServidorIndisponivel(servidor.Nome, ex.Message);
        }
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
        var assembly = typeof(MailQueueRepository).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Sql.{arquivo}", StringComparison.Ordinal));
        using var recurso = assembly.GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"SQL embutido não encontrado: {arquivo}");

        // Sem BOM e sem conversão: o texto vai ao servidor exatamente como está no arquivo.
        using var leitor = new StreamReader(recurso, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return leitor.ReadToEnd();
    }
}
