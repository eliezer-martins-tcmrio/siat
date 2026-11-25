using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Catalogo;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Feature <see cref="ObterFicha"/> (specs/004-ficha-sistema/research.md R7): autoriza o catálogo antes de ler (FR-001),
/// localiza o item pelo nome (FR-012, FR-019, FR-020) e só leva o vínculo a quem tem também o recurso de versões
/// (FR-002, FR-010).
/// </summary>
public sealed class ObterFichaHandlerTests
{
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 25, 13, 0, 0, TimeSpan.Zero);

    private const string Origem = "https://git/asi/TCMRJDoc/-/blob/master/sistemasDb.js";

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<ICatalogoSistemasService> _servico = new(MockBehavior.Strict);
    private readonly ObterFicha.Handler _handler;

    public ObterFichaHandlerTests()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(true);
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(true);
        _handler = new ObterFicha.Handler(_autorizacao.Object, _servico.Object);
    }

    private static Sistema Sigarh(int posicao = 1, VinculoDeVersoes? versoes = null) => new(
        "SIGARH",
        "Observação",
        new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>
        {
            [AmbienteDoCatalogo.Prd] = [new Instancia(["https://tcmrj/sigarh"], ["TCM2016WEB1"], @"W:\Sites\sigarh")],
        },
        versoes ?? VinculoDeVersoes.De(["SIGARH", "SIGARH Agendador"]),
        posicao);

    private static CatalogoLido Catalogo(params Sistema[] itens) => new(itens, 0, LidoEm, Origem);

    private void Devolver(Result<CatalogoObtido, Erro> resultado, bool atualizar = false) =>
        _servico.Setup(s => s.ObterAsync(atualizar, It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    private Task<Result<FichaVM, Erro>> Obter(string? sistema = "SIGARH", bool atualizar = false) =>
        _handler.Handle(new ObterFicha.Query(sistema, atualizar), TestContext.Current.CancellationToken);

    private async Task<FichaVM> Ficha(string? sistema = "SIGARH", bool atualizar = false)
    {
        var resultado = await Obter(sistema, atualizar);
        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        return resultado.Value;
    }

    [Fact]
    public async Task Sem_o_recurso_do_catalogo_devolve_AcessoNegado_sem_ler()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(false);

        (await Obter()).Error.Should().Be(Erros.Catalogo.AcessoNegado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task So_com_o_catalogo_a_ficha_nao_leva_o_vinculo()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(false);
        Devolver(new CatalogoObtido(Catalogo(Sigarh()), null));

        var ficha = await Ficha();

        ficha.MostraVersoes.Should().BeFalse();
        ficha.Vinculo.Should().BeEmpty();
        ficha.VinculoInvalido.Should().BeFalse();
    }

    [Fact]
    public async Task So_com_o_catalogo_nao_mostra_que_o_vinculo_e_invalido()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(false);
        Devolver(new CatalogoObtido(Catalogo(Sigarh(versoes: VinculoDeVersoes.ForaDoFormato)), null));

        (await Ficha()).VinculoInvalido.Should().BeFalse();
    }

    [Fact]
    public async Task Com_os_dois_recursos_leva_o_vinculo()
    {
        Devolver(new CatalogoObtido(Catalogo(Sigarh()), null));

        var ficha = await Ficha();

        ficha.MostraVersoes.Should().BeTrue();
        ficha.Vinculo.Should().Equal("SIGARH", "SIGARH Agendador");
        ficha.VinculoInvalido.Should().BeFalse();
    }

    [Fact]
    public async Task Com_os_dois_recursos_leva_o_vinculo_invalido()
    {
        Devolver(new CatalogoObtido(Catalogo(Sigarh(versoes: VinculoDeVersoes.ForaDoFormato)), null));

        var ficha = await Ficha();

        ficha.VinculoInvalido.Should().BeTrue();
        ficha.Vinculo.Should().BeEmpty();
    }

    [Fact]
    public async Task Acha_o_item_sem_diferenciar_maiusculas_e_espacos_nas_pontas()
    {
        Devolver(new CatalogoObtido(Catalogo(Sigarh()), null));

        (await Ficha("  sigarh ")).Nome.Should().Be("SIGARH");
    }

    [Theory]
    [InlineData("Inexistente")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Nome_que_nao_esta_no_catalogo_devolve_SistemaNaoEncontrado(string? nome)
    {
        Devolver(new CatalogoObtido(Catalogo(Sigarh()), null));

        (await Obter(nome)).Error.Code.Should().Be(Erros.Catalogo.CodigoSistemaNaoEncontrado);
    }

    [Fact]
    public async Task Leva_a_quantidade_de_repetidos()
    {
        Devolver(new CatalogoObtido(Catalogo(Sigarh(30), Sigarh(7), Sigarh(12)), null));

        (await Ficha()).Repetidos.Should().Be(2);
    }

    [Fact]
    public async Task Quatro_ambientes_na_ordem_e_os_dados_da_leitura()
    {
        var falha = new FalhaDeLeitura(Erros.Catalogo.TempoEsgotado(10), LidoEm.AddMinutes(12));
        Devolver(new CatalogoObtido(Catalogo(Sigarh()), falha));

        var ficha = await Ficha();

        ficha.Ambientes.Select(a => a.Ambiente).Should().Equal(AmbienteDoCatalogo.Dsv, AmbienteDoCatalogo.Hml, AmbienteDoCatalogo.Prd, AmbienteDoCatalogo.Cont);
        ficha.Ambientes[2].Instancias.Single().Maquinas.Should().Equal("TCM2016WEB1");
        ficha.Observacao.Should().Be("Observação");
        ficha.LidoEm.Should().Be(LidoEm);
        ficha.Origem.Should().Be(Origem);
        ficha.Falha.Should().Be(new FalhaVM(Erros.Catalogo.TempoEsgotado(10).Message, LidoEm.AddMinutes(12)));
    }

    [Fact]
    public async Task Atualizar_e_repassado_ao_servico()
    {
        Devolver(new CatalogoObtido(Catalogo(Sigarh()), null), atualizar: true);

        (await Ficha(atualizar: true)).Nome.Should().Be("SIGARH");
        _servico.Verify(s => s.ObterAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Erro_do_catalogo_e_repassado()
    {
        Devolver(Erros.Catalogo.AcessoRecusado);

        (await Obter()).Error.Code.Should().Be(Erros.Catalogo.CodigoAcessoRecusado);
    }
}
