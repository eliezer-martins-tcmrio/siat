using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Copilot;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Handler do período (specs/005-copilot-meu-consumo, T036): política, conexão, período e cancelamento.</summary>
public sealed class ObterConsumoDoPeriodoHandlerTests : HandlersCopilotBase
{
    private ObterConsumoDoPeriodo.Handler Criar() => new(Autorizacao.Object, Conexoes.Object, Consumo.Object, Relogio);

    [Fact]
    public async Task Sem_a_politica_e_acesso_negado_sem_chamar_o_servico()
    {
        SemPolitica();

        (await Criar().Handle(new ObterConsumoDoPeriodo.Query(null, null, false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoAcessoNegado);
    }

    [Fact]
    public async Task Sem_conexao_e_sem_conexao_sem_chamar_o_servico()
    {
        SemConexao();

        (await Criar().Handle(new ObterConsumoDoPeriodo.Query(null, null, false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoSemConexao);
    }

    [Fact]
    public void A_consulta_nao_tem_propriedade_de_login() =>
        PropriedadesDeLogin(typeof(ObterConsumoDoPeriodo.Query)).Should().BeEmpty();

    [Theory]
    [InlineData("2026-11-10", "2026-11-01")]
    [InlineData("2026-13-01", null)]
    [InlineData(null, "2026-11-28")]
    public async Task Periodo_invalido_nao_chama_o_servico(string? de, string? ate)
    {
        (await Criar().Handle(new ObterConsumoDoPeriodo.Query(de, ate, false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoPeriodoInvalido);
    }

    [Fact]
    public async Task Periodo_padrao_vai_ao_servico_com_a_conexao_o_atualizar_e_o_progresso()
    {
        var progresso = new Progress<int>();
        var periodo = new Periodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 27));
        Consumo.Setup(c => c.ObterPeriodoAsync(Conexao, periodo, true, progresso, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<ConsumoDoPeriodo, Erro>(
                ConsumoDoPeriodo.De(periodo, [new DiaDeConsumo(new DateOnly(2026, 11, 1), Relatorio(2))], Agora)));

        var consumo = await Criar().Handle(new ObterConsumoDoPeriodo.Query(null, null, true) { Progresso = progresso }, Ct);

        consumo.Value.Total.ValorBruto.Should().Be(2);
        consumo.Value.Modelos.Single().Participacao.Should().Be(1);
    }

    [Theory]
    [InlineData(Erros.Copilot.CodigoNaoMembro)]
    [InlineData(Erros.Copilot.CodigoSemLicencaNemLimite)]
    public async Task Erros_da_pessoa_sao_repassados(string codigo)
    {
        var erro = codigo == Erros.Copilot.CodigoNaoMembro ? Erros.Copilot.NaoMembro("ana-m", "tcmrj") : Erros.Copilot.SemLicencaNemLimite("tcmrj");
        Consumo.Setup(c => c.ObterPeriodoAsync(Conexao, It.IsAny<Periodo>(), false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ConsumoDoPeriodo, Erro>(erro));

        (await Criar().Handle(new ObterConsumoDoPeriodo.Query(null, null, false), Ct)).Error.Code.Should().Be(codigo);
    }

    [Fact]
    public async Task Cancelamento_vira_cancelada()
    {
        Consumo.Setup(c => c.ObterPeriodoAsync(Conexao, It.IsAny<Periodo>(), false, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        (await Criar().Handle(new ObterConsumoDoPeriodo.Query(null, null, false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoCancelada);
    }
}
