using AwesomeAssertions;
using Tcmrj.Siat.App.Web.Services;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Registro da conexão no DBLog, TypeID 10 (specs/005-copilot-meu-consumo/contracts/conexao-github.md; research.md
/// R10): modelo, propriedades do escopo, nada de token, código ou <c>state</c>, e falha que não propaga.
/// </summary>
public sealed class RegistroConexaoGitHubTests
{
    private readonly LoggerGravador<RegistroConexaoGitHub> _logger = new();

    private RegistroConexaoGitHub Criar() => new(_logger);

    [Theory]
    [InlineData(OperacaoConexaoGitHub.Conexao, null, "Sucesso")]
    [InlineData(OperacaoConexaoGitHub.Recusa, "nao-membro", "Recusada")]
    [InlineData(OperacaoConexaoGitHub.Desconexao, "usuario", "Sucesso")]
    public void Cada_operacao_gera_um_evento_TypeID_10_com_o_modelo_e_o_escopo(OperacaoConexaoGitHub operacao, string? motivo, string resultado)
    {
        Criar().Registrar(operacao, "12345", "Ana Souza", "ana-m", 7, "tcmrj", motivo);

        var evento = _logger.Eventos.Should().ContainSingle().Which;
        evento.Mensagem.Should().Be($"{operacao} da conta ana-m do GitHub por 12345");
        var p = evento.Propriedades;
        p["TypeID"].Should().Be((byte)10);
        p["Operacao"].Should().Be(operacao.ToString());
        p["GitHubLogin"].Should().Be("ana-m");
        p["Matricula"].Should().Be("12345");
        p["Usuario"].Should().Be("Ana Souza");
        p["GitHubId"].Should().Be(7L);
        p["Organizacao"].Should().Be("tcmrj");
        p["Motivo"].Should().Be(motivo);
        p["Resultado"].Should().Be(resultado);
    }

    [Fact]
    public void Recusa_sem_conta_conhecida_diz_desconhecida()
    {
        Criar().Registrar(OperacaoConexaoGitHub.Recusa, "12345", null, null, null, "tcmrj", "negada");

        _logger.Eventos.Single().Mensagem.Should().Be("Recusa da conta (desconhecida) do GitHub por 12345");
    }

    [Fact]
    public void Nenhuma_propriedade_tem_token_codigo_ou_state()
    {
        Criar().Registrar(OperacaoConexaoGitHub.Conexao, "12345", "Ana", "ana-m", 7, "tcmrj", null);

        _logger.Eventos.Single().Propriedades.Keys.Should().NotContain(k =>
            k.Contains("token", StringComparison.OrdinalIgnoreCase)
            || k.Contains("code", StringComparison.OrdinalIgnoreCase)
            || k.Contains("state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Falha_do_logger_nao_propaga()
    {
        _logger.Falhar = true;

        var registrar = () => Criar().Registrar(OperacaoConexaoGitHub.Desconexao, "12345", null, "ana-m", 7, "tcmrj", "usuario");

        registrar.Should().NotThrow();
    }
}
