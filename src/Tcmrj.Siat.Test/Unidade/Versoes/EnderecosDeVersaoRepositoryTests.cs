using System.Data;
using AwesomeAssertions;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Infra.Repositories;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// SQL embutido, mapeamento das 14 colunas de <c>Apps_VersionAddresses</c> e dos erros do SQL Server
/// (specs/003-versoes-publicadas/contracts/origem-enderecos.md). Sem SQL Server real.
/// </summary>
public sealed class EnderecosDeVersaoRepositoryTests
{
    private static readonly string[] ColunasDoContrato =
    [
        "Id", "Sistema",
        "VersaoDSV", "VersaoHML", "VersaoPRD",
        "SistemaRaizIniDSV", "SistemaRaizIniHML", "SistemaRaizIniPRD",
        "VersaoDSV2", "VersaoHML2", "VersaoPRD2",
        "SistemaRaizIniDSV2", "SistemaRaizIniHML2", "SistemaRaizIniPRD2",
    ];

    private static readonly Servidor Servidor = new() { Chave = "tcmbd3-dev", Nome = @"TCMBD3\DEV", Endereco = @"TCMBD3\DEV", Ambiente = Ambiente.Dsv };

    [Fact]
    public void O_SQL_embutido_le_so_os_ativos_com_as_14_colunas()
    {
        var sql = EnderecosDeVersaoRepository.Sql.Value;

        sql.Should().Contain("WHERE Ativo = 1").And.Contain("FROM dbo.Apps_VersionAddresses");
        foreach (var coluna in ColunasDoContrato)
        {
            sql.Should().Contain(coluna);
        }

        sql.Should().NotContainAny("INSERT", "UPDATE", "DELETE", "@");
    }

    [Fact]
    public void Cada_coluna_vai_para_o_par_certo_com_nulo_e_espacos_como_nao_configurado()
    {
        var tabela = new DataTable();
        tabela.Columns.Add("Id", typeof(int));
        foreach (var coluna in ColunasDoContrato.Skip(1))
        {
            tabela.Columns.Add(coluna, typeof(string));
        }

        var linha = tabela.NewRow();
        linha["Id"] = 3;
        linha["Sistema"] = "  eTCM Assinador ";
        foreach (var coluna in ColunasDoContrato.Skip(2))
        {
            linha[coluna] = $@"\\m\{coluna}";
        }

        linha["VersaoHML2"] = DBNull.Value;
        linha["SistemaRaizIniPRD"] = "   ";
        tabela.Rows.Add(linha);

        using var leitor = tabela.CreateDataReader();
        leitor.Read().Should().BeTrue();
        var endereco = EnderecosDeVersaoRepository.Ler(leitor);

        endereco.Id.Should().Be(3);
        endereco.Sistema.Should().Be("eTCM Assinador");
        endereco.Enderecos.Should().HaveCount(6);
        endereco.Enderecos[(Ambiente.Dsv, Maquina.Maquina1)].Should().Be(new ParDeEndereco(@"\\m\VersaoDSV", @"\\m\SistemaRaizIniDSV"));
        endereco.Enderecos[(Ambiente.Prd, Maquina.Maquina2)].Should().Be(new ParDeEndereco(@"\\m\VersaoPRD2", @"\\m\SistemaRaizIniPRD2"));
        endereco.Enderecos[(Ambiente.Hml, Maquina.Maquina2)].Versao.Should().BeNull();
        endereco.Enderecos[(Ambiente.Prd, Maquina.Maquina1)].SistemaRaiz.Should().BeNull();
        endereco.Enderecos[(Ambiente.Prd, Maquina.Maquina1)].Versao.Should().Be(@"\\m\VersaoPRD");
    }

    [Fact]
    public void Tempo_esgotado_vira_TempoEsgotado()
    {
        var erro = EnderecosDeVersaoRepository.Converter(-2, "Timeout", Servidor);

        erro.Code.Should().Be(Erros.Versoes.CodigoTempoEsgotado);
        erro.Message.Should().Contain(@"TCMBD3\DEV");
    }

    [Theory]
    [InlineData(208)]
    [InlineData(229)]
    [InlineData(230)]
    [InlineData(916)]
    [InlineData(927)]
    public void Tabela_inexistente_ou_sem_permissao_vira_TabelaIndisponivel(int numero)
    {
        var erro = EnderecosDeVersaoRepository.Converter(numero, "Invalid object name", Servidor);

        erro.Code.Should().Be(Erros.Versoes.CodigoTabelaIndisponivel);
        erro.Message.Should().Contain("tabela inexistente ou sem permissão para a conta do SIAT").And.Contain(@"TCMBD3\DEV");
    }

    [Fact]
    public void Outro_erro_vira_TabelaIndisponivel_com_o_servidor()
    {
        var erro = EnderecosDeVersaoRepository.Converter(53, "A network-related error", Servidor);

        erro.Code.Should().Be(Erros.Versoes.CodigoTabelaIndisponivel);
        erro.Message.Should().Contain(@"Não foi possível conectar a TCMBD3\DEV");
    }
}
