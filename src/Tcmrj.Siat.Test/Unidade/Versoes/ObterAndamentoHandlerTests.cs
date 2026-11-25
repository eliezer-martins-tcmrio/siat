using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Versoes;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>Feature <see cref="ObterAndamento"/> (research.md R10): autoriza como a <see cref="ObterVersoes"/>.</summary>
public sealed class ObterAndamentoHandlerTests
{
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<IVersoesPublicadasService> _servico = new(MockBehavior.Strict);
    private readonly ObterAndamento.Handler _handler;

    public ObterAndamentoHandlerTests()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(true);
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, "tcmbd4-hml")).ReturnsAsync(ObterVersoesHandlerTests.Hml);
        _handler = new ObterAndamento.Handler(_autorizacao.Object, _servico.Object, ObterVersoesHandlerTests.Opcoes("tcmbd4-hml"));
    }

    private Task<Result<int?, Erro>> Obter(string? servidor) =>
        _handler.Handle(new ObterAndamento.Query(servidor), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Sem_a_politica_devolve_AcessoNegado_sem_consultar_o_servico()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(false);

        (await Obter("tcmbd4-hml")).Error.Should().Be(Erros.Versoes.AcessoNegado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Chave_inexistente_devolve_ServidorNaoEncontrado()
    {
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, "xyz")).ReturnsAsync(Erros.Consultas.ServidorNaoEncontrado);

        (await Obter("xyz")).Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _servico.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(42)]
    public async Task Autorizado_devolve_a_quantidade_do_servico_e_sem_chave_usa_o_padrao(int? quantidade)
    {
        _servico.Setup(s => s.QuantidadeEmLeitura("tcmbd4-hml")).Returns(quantidade);

        var resultado = await Obter(null);

        resultado.Value.Should().Be(quantidade);
    }
}
