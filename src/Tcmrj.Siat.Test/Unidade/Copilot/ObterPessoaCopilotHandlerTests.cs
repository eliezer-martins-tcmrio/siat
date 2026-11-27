using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Copilot;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Handler da pessoa (specs/005-copilot-meu-consumo, T036): política, conexão e conta só da conexão.</summary>
public sealed class ObterPessoaCopilotHandlerTests : HandlersCopilotBase
{
    private ObterPessoaCopilot.Handler Criar() => new(Autorizacao.Object, Conexoes.Object, Consumo.Object, Opcoes());

    [Fact]
    public async Task Sem_a_politica_e_acesso_negado_sem_chamar_o_servico()
    {
        SemPolitica();

        var resultado = await Criar().Handle(new ObterPessoaCopilot.Query(false), Ct);

        resultado.Error.Code.Should().Be(Erros.Copilot.CodigoAcessoNegado);
        Conexoes.Verify(c => c.ObterAsync(), Times.Never);
    }

    [Fact]
    public async Task Sem_conexao_e_sem_conexao_sem_chamar_o_servico()
    {
        SemConexao();

        (await Criar().Handle(new ObterPessoaCopilot.Query(false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoSemConexao);
    }

    [Fact]
    public void A_consulta_nao_tem_propriedade_de_login() =>
        PropriedadesDeLogin(typeof(ObterPessoaCopilot.Query)).Should().BeEmpty();

    [Fact]
    public async Task O_servico_recebe_a_conta_da_conexao_e_o_atualizar()
    {
        Consumo.Setup(c => c.ObterPessoaAsync(Conexao, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<PessoaCopilot, Erro>(Pessoa()));

        var pessoa = await Criar().Handle(new ObterPessoaCopilot.Query(true), Ct);

        pessoa.Value.Login.Should().Be("ana-m");
        pessoa.Value.Organizacao.Should().Be("tcmrj");
    }

    [Theory]
    [InlineData(Erros.Copilot.CodigoNaoMembro)]
    [InlineData(Erros.Copilot.CodigoSemLicencaNemLimite)]
    public async Task Nao_membro_e_sem_licenca_nem_limite_sao_repassados(string codigo)
    {
        var erro = codigo == Erros.Copilot.CodigoNaoMembro ? Erros.Copilot.NaoMembro("ana-m", "tcmrj") : Erros.Copilot.SemLicencaNemLimite("tcmrj");
        Consumo.Setup(c => c.ObterPessoaAsync(Conexao, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PessoaCopilot, Erro>(erro));

        (await Criar().Handle(new ObterPessoaCopilot.Query(false), Ct)).Error.Code.Should().Be(codigo);
    }

    [Fact]
    public async Task Cancelamento_vira_cancelada()
    {
        Consumo.Setup(c => c.ObterPessoaAsync(Conexao, false, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        (await Criar().Handle(new ObterPessoaCopilot.Query(false), Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoCancelada);
    }
}
