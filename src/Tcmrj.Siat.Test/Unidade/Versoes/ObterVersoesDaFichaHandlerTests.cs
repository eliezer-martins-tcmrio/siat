using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Features.Versoes;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Feature <see cref="ObterVersoesDaFicha"/> (specs/004-ficha-sistema/research.md R7): exige o catálogo e, como a
/// <see cref="ObterVersoes"/>, o recurso de versões e o servidor da lista fechada, antes de ler (FR-002, FR-022); monta
/// as versões do vínculo com as mesmas células e divergências da tela de Versões (SC-004).
/// </summary>
public sealed class ObterVersoesDaFichaHandlerTests
{
    private static readonly Servidor Hml = ObterVersoesHandlerTests.Hml;

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 25, 14, 0, 0, TimeSpan.Zero));
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<IVersoesPublicadasService> _servico = new(MockBehavior.Strict);
    private readonly ObterVersoesDaFicha.Handler _handler;

    public ObterVersoesDaFichaHandlerTests()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(true);
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(true);
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, Hml.Chave)).ReturnsAsync(Hml);
        _handler = new ObterVersoesDaFicha.Handler(_autorizacao.Object, _servico.Object, ObterVersoesHandlerTests.Opcoes(Hml.Chave), _relogio);
    }

    private static readonly string[] Vinculo = ["STP", "eTCM", "Inexistente"];

    private void Devolver(Result<VersoesObtidas, Erro> resultado, bool atualizar = false) =>
        _servico.Setup(s => s.ObterDoSistemaAsync(
                Hml, It.Is<VinculoDeVersoes>(v => v.Chave == "stp\netcm\ninexistente"), atualizar, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);

    private Task<Result<VersoesDaFichaVM, Erro>> Obter(string? servidor = "tcmbd4-hml", IReadOnlyList<string>? vinculo = null, bool atualizar = false) =>
        _handler.Handle(new ObterVersoesDaFicha.Query(servidor, vinculo ?? Vinculo, atualizar), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Sem_o_recurso_do_catalogo_devolve_AcessoNegado_do_catalogo_sem_chamar_o_servico()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(false);

        (await Obter()).Error.Should().Be(Erros.Catalogo.AcessoNegado);
        _servico.VerifyNoOtherCalls();
        _autorizacao.Verify(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas), Times.Never);
    }

    [Fact]
    public async Task Sem_o_recurso_de_versoes_devolve_AcessoNegado_das_versoes_sem_chamar_o_servico()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(false);

        (await Obter()).Error.Should().Be(Erros.Versoes.AcessoNegado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sem_servidor_usa_o_padrao()
    {
        Devolver(new VersoesObtidas(ObterVersoesHandlerTests.Lidas(Hml), null));

        (await Obter(servidor: null)).IsSuccess.Should().BeTrue();
        _autorizacao.Verify(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, Hml.Chave), Times.Once);
    }

    [Fact]
    public async Task Servidor_fora_da_lista_devolve_ServidorNaoEncontrado_sem_chamar_o_servico()
    {
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, "xyz")).ReturnsAsync(Erros.Consultas.ServidorNaoEncontrado);

        (await Obter("xyz")).Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Vinculo_vazio_devolve_resultado_sem_componentes_sem_chamar_o_servico()
    {
        var resultado = await Obter(vinculo: ["", "  "]);

        var versoes = resultado.Value;
        versoes.Componentes.Should().BeEmpty();
        versoes.NaoEncontrados.Should().BeEmpty();
        versoes.Falha.Should().BeNull();
        versoes.GeradoEm.Should().Be(_relogio.GetUtcNow());
        versoes.Duracao.Should().Be(TimeSpan.Zero);
        versoes.Servidor.Chave.Should().Be(Hml.Chave);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Autorizado_monta_as_versoes_do_vinculo_como_a_tela_de_Versoes()
    {
        var lidas = ObterVersoesHandlerTests.Lidas(Hml);
        var falha = new FalhaDeLeitura(Erros.Versoes.TempoEsgotado(Hml.Nome), lidas.GeradoEm.AddMinutes(3));
        Devolver(new VersoesObtidas(lidas, falha), atualizar: true);

        var versoes = (await Obter(atualizar: true)).Value;

        versoes.Servidor.Should().Be(new App.Web.ViewModels.Shared.ServidorVM(Hml.Chave, Hml.Nome, Ambiente.Hml));
        versoes.GeradoEm.Should().Be(lidas.GeradoEm);
        versoes.Duracao.Should().Be(lidas.Duracao);
        versoes.Falha.Should().Be(new FalhaDasVersoesVM(falha.Erro.Message, falha.Em));
        versoes.Componentes.Select(c => c.Nome).Should().Equal("STP", "eTCM");
        versoes.NaoEncontrados.Should().Equal("Inexistente");
        versoes.TemDivergencia.Should().BeTrue();
        versoes.TemErro.Should().BeTrue();
        versoes.SemPendencias.Should().BeFalse();

        // As mesmas células e divergências da tela de Versões (SC-004).
        var daTela = lidas.Sistemas.Single(s => s.Nome == "STP");
        versoes.Componentes[0].Registros.Single().Should().BeEquivalentTo(SistemaVersoesVM.De(daTela));
    }

    [Fact]
    public async Task Cancelamento_vira_o_erro_de_cancelamento_das_versoes()
    {
        _servico.Setup(s => s.ObterDoSistemaAsync(Hml, It.IsAny<VinculoDeVersoes>(), false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        (await Obter()).Error.Should().Be(Erros.Versoes.Cancelada);
    }

    [Fact]
    public async Task Erro_do_servico_e_repassado()
    {
        Devolver(Erros.Versoes.TabelaIndisponivel(Hml.Nome, "fora do ar"));

        (await Obter()).Error.Code.Should().Be(Erros.Versoes.CodigoTabelaIndisponivel);
    }
}
