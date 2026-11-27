using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Copilot;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Handler do uso do limite (specs/005-copilot-meu-consumo, T036): faixa com o limite e o bruto do mês.</summary>
public sealed class ObterUsoDoLimiteHandlerTests : HandlersCopilotBase
{
    private ObterUsoDoLimite.Handler Criar() => new(Autorizacao.Object, Conexoes.Object, Consumo.Object, Relogio);

    private void Dados(Result<LimiteVigente, Erro> limite, decimal brutoDoMes)
    {
        Consumo.Setup(c => c.ObterPessoaAsync(Conexao, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<PessoaCopilot, Erro>(Pessoa(limite)));
        Consumo.Setup(c => c.ObterMesAtualAsync(Conexao, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<RelatorioDeConsumo, Erro>(Relatorio(brutoDoMes)));
    }

    [Fact]
    public async Task Sem_a_politica_e_acesso_negado_sem_chamar_o_servico()
    {
        SemPolitica();

        (await Criar().Handle(new ObterUsoDoLimite.Query(false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoAcessoNegado);
    }

    [Fact]
    public async Task Sem_conexao_e_sem_conexao_sem_chamar_o_servico()
    {
        SemConexao();

        (await Criar().Handle(new ObterUsoDoLimite.Query(false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoSemConexao);
    }

    [Fact]
    public void A_consulta_nao_tem_propriedade_de_login() =>
        PropriedadesDeLogin(typeof(ObterUsoDoLimite.Query)).Should().BeEmpty();

    [Fact]
    public async Task Calcula_a_faixa_com_o_limite_da_pessoa_e_o_bruto_do_mes()
    {
        Dados(new LimiteVigente.Individual(50), 40);

        var uso = (await Criar().Handle(new ObterUsoDoLimite.Query(false), Ct)).Value;

        uso.Faixa.Should().Be(FaixaDeUso.Atencao);
        uso.Percentual.Should().Be("80%");
        uso.Restante.Should().Be(10);
        uso.Mes.Should().Be("2026-11");
    }

    [Fact]
    public async Task Limite_com_falha_e_indisponivel()
    {
        Dados(Result.Failure<LimiteVigente, Erro>(Erros.Copilot.GitHubIndisponivel), 40);

        (await Criar().Handle(new ObterUsoDoLimite.Query(false), Ct)).Value.Faixa.Should().Be(FaixaDeUso.Indisponivel);
    }

    [Fact]
    public async Task Erro_da_pessoa_prevalece()
    {
        Consumo.Setup(c => c.ObterPessoaAsync(Conexao, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PessoaCopilot, Erro>(Erros.Copilot.NaoMembro("ana-m", "tcmrj")));
        Consumo.Setup(c => c.ObterMesAtualAsync(Conexao, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<RelatorioDeConsumo, Erro>(Erros.Copilot.NaoMembro("ana-m", "tcmrj")));

        (await Criar().Handle(new ObterUsoDoLimite.Query(false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoNaoMembro);
    }

    [Fact]
    public async Task Cancelamento_vira_cancelada()
    {
        Consumo.Setup(c => c.ObterPessoaAsync(Conexao, false, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        Consumo.Setup(c => c.ObterMesAtualAsync(Conexao, false, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        (await Criar().Handle(new ObterUsoDoLimite.Query(false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoCancelada);
    }
}
