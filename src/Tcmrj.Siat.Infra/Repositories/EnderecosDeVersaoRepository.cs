using System.Data;
using System.Data.Common;
using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Data.SqlClient;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Conexoes;

namespace Tcmrj.Siat.Infra.Repositories;

/// <summary>
/// Leitura de <c>dbo.Apps_VersionAddresses</c> no banco <see cref="Servidor.BancoUserManager"/> de um servidor da lista
/// fechada (specs/003-versoes-publicadas/research.md R1), com o SQL embutido <c>Sql/versoes.sql</c>, sem parâmetro e
/// só <c>SELECT</c>. O <see cref="CancellationToken"/> vai para <c>OpenAsync</c>, <c>ExecuteReaderAsync</c> e
/// <c>ReadAsync</c>, e o SqlClient interrompe o comando no servidor.
/// </summary>
public sealed class EnderecosDeVersaoRepository(IFabricaDeConexao fabrica) : IEnderecosDeVersaoRepository
{
    /// <summary>Colunas de endereço por posição: (versão, sistema raiz) de cada ambiente e máquina.</summary>
    internal static readonly IReadOnlyList<(Ambiente Ambiente, Maquina Maquina, string Versao, string SistemaRaiz)> Colunas =
    [
        (Ambiente.Dsv, Maquina.Maquina1, "VersaoDSV", "SistemaRaizIniDSV"),
        (Ambiente.Hml, Maquina.Maquina1, "VersaoHML", "SistemaRaizIniHML"),
        (Ambiente.Prd, Maquina.Maquina1, "VersaoPRD", "SistemaRaizIniPRD"),
        (Ambiente.Dsv, Maquina.Maquina2, "VersaoDSV2", "SistemaRaizIniDSV2"),
        (Ambiente.Hml, Maquina.Maquina2, "VersaoHML2", "SistemaRaizIniHML2"),
        (Ambiente.Prd, Maquina.Maquina2, "VersaoPRD2", "SistemaRaizIniPRD2"),
    ];

    internal static readonly Lazy<string> Sql = new(() => LerSql("versoes.sql"));

    public async Task<Result<IReadOnlyList<EnderecoDeVersao>, Erro>> ListarAtivosAsync(Servidor servidor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servidor);

        try
        {
            await using var conexao = fabrica.Criar(servidor, servidor.BancoUserManager);
            await conexao.OpenAsync(cancellationToken);

            await using var comando = conexao.CreateCommand();
            comando.CommandText = Sql.Value;
            comando.CommandType = CommandType.Text;
            comando.CommandTimeout = fabrica.CommandTimeout;

            var itens = new List<EnderecoDeVersao>();
            await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
            {
                itens.Add(Ler(leitor));
            }

            return itens;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is OperationCanceledException or SqlException)
        {
            return Erros.Versoes.Cancelada;
        }
        catch (SqlException ex)
        {
            return Converter(ex.Number, ex.Message, servidor);
        }
        catch (InvalidOperationException ex)
        {
            // Por exemplo, tempo esgotado ao obter conexão do pool.
            return Erros.Versoes.TabelaIndisponivel(servidor.Nome, ex.Message);
        }
    }

    /// <summary>
    /// Uma linha da tabela. <c>NULL</c> e texto só com espaços valem "não configurado" (<see cref="ParDeEndereco"/>).
    /// </summary>
    internal static EnderecoDeVersao Ler(DbDataReader leitor)
    {
        ArgumentNullException.ThrowIfNull(leitor);

        var enderecos = Colunas.ToDictionary(
            c => (c.Ambiente, c.Maquina),
            c => new ParDeEndereco(Texto(leitor, c.Versao), Texto(leitor, c.SistemaRaiz)));

        return new EnderecoDeVersao(
            Convert.ToInt32(leitor["Id"], CultureInfo.InvariantCulture),
            Texto(leitor, "Sistema") ?? string.Empty,
            enderecos);
    }

    /// <summary>
    /// Erro do SQL Server para a tela (contracts/origem-enderecos.md): tempo esgotado, tabela inexistente ou sem
    /// permissão, e o resto como servidor indisponível, sempre com o nome do servidor.
    /// </summary>
    internal static Erro Converter(int numero, string mensagem, Servidor servidor)
    {
        ArgumentNullException.ThrowIfNull(servidor);

        return numero switch
        {
            -2 => Erros.Versoes.TempoEsgotado(servidor.Nome),

            // Objeto inexistente (208), sem permissão (229, 230, 916, 927) ou banco que não abre (4060, 18456).
            208 or 229 or 230 or 916 or 927 or 4060 or 18456 => Erros.Versoes.TabelaIndisponivel(servidor.Nome, Erros.Versoes.MotivoSemPermissao),

            _ => Erros.Versoes.TabelaIndisponivel(servidor.Nome, $"Não foi possível conectar a {servidor.Nome} ({mensagem})"),
        };
    }

    private static string? Texto(DbDataReader leitor, string coluna) => leitor[coluna] as string;

    private static string LerSql(string arquivo)
    {
        var assembly = typeof(EnderecosDeVersaoRepository).Assembly;
        var nome = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Sql.{arquivo}", StringComparison.Ordinal));
        using var recurso = assembly.GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"SQL embutido não encontrado: {arquivo}");
        using var leitor = new StreamReader(recurso);
        return leitor.ReadToEnd();
    }
}
