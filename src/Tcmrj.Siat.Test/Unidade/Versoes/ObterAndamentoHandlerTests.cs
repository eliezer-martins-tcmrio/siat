using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Versoes;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

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

    private Task<Result<int?, Erro>> Obter(string? servidor, IReadOnlyList<string>? vinculo = null) =>
        _handler.Handle(new ObterAndamento.Query(servidor, vinculo), TestContext.Current.CancellationToken);

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

    // ---------- Andamento da ficha (specs/004-ficha-sistema/research.md R6; T046) ----------

    [Fact]
    public async Task Com_vinculo_devolve_o_andamento_da_leitura_da_ficha()
    {
        var chave = VinculoDeVersoes.De(["SIGARH", "SIGARH Agendador"]).Chave;
        _servico.Setup(s => s.QuantidadeEmLeitura("tcmbd4-hml", It.Is<VinculoDeVersoes>(v => v.Chave == chave)))
            .Returns(4);

        var resultado = await Obter("tcmbd4-hml", ["SIGARH", "SIGARH Agendador"]);

        resultado.Value.Should().Be(4);
        _servico.Verify(s => s.QuantidadeEmLeitura(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Com_vinculo_e_sem_a_politica_devolve_AcessoNegado_sem_consultar_o_servico()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(false);

        (await Obter("tcmbd4-hml", ["SIGARH"])).Error.Should().Be(Erros.Versoes.AcessoNegado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Com_vinculo_e_servidor_fora_da_lista_devolve_ServidorNaoEncontrado()
    {
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, "xyz")).ReturnsAsync(Erros.Consultas.ServidorNaoEncontrado);

        (await Obter("xyz", ["SIGARH"])).Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _servico.VerifyNoOtherCalls();
    }
}
