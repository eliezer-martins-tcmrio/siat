using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Catalogo;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Feature <see cref="ListarFichas"/> (specs/004-ficha-sistema/research.md R4 e R7): mapa dos nomes da tabela de
/// Versões para as fichas, só com <c>Catalogo_Sistemas.CanSelect</c> (FR-005, FR-012; T036).
/// </summary>
public sealed class ListarFichasHandlerTests
{
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 25, 13, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<ICatalogoSistemasService> _servico = new(MockBehavior.Strict);
    private readonly ListarFichas.Handler _handler;

    public ListarFichasHandlerTests()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(true);
        _handler = new ListarFichas.Handler(_autorizacao.Object, _servico.Object);
    }

    private static CatalogoLido Catalogo() => new(
        [
            CatalogoLidoTests.Item("Folha", 3, "SIGARH Agendador"),
            CatalogoLidoTests.Item("SIGARH", 1, "SIGARH", "SIGARH Agendador", "Relatórios"),
            CatalogoLidoTests.Item("WebNews", 2),
        ],
        0, LidoEm, "https://git/sistemasDb.js");

    private void Devolver(Result<CatalogoObtido, Erro> resultado) =>
        _servico.Setup(s => s.ObterAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    private Task<Result<FichasVM, Erro>> Listar() =>
        _handler.Handle(new ListarFichas.Query(), TestContext.Current.CancellationToken);

    private static SistemaVersoesVM Linha(string nome) => SistemaVersoesVM.De(new SistemaVersionado(1, nome, []));

    [Fact]
    public async Task Sem_o_recurso_do_catalogo_devolve_AcessoNegado_sem_ler_o_catalogo()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(false);

        (await Listar()).Error.Should().Be(Erros.Catalogo.AcessoNegado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Autorizado_devolve_o_mapa_do_catalogo()
    {
        var catalogo = Catalogo();
        Devolver(new CatalogoObtido(catalogo, null));

        var fichas = (await Listar()).Value;

        fichas.PorNome.Should().BeEquivalentTo(catalogo.FichasPorVersao());
        fichas.PorNome.Should().ContainKey("sigarh agendador").WhoseValue.Should().Be("Folha");
    }

    [Theory]
    [InlineData("SIGARH", "SIGARH")]
    [InlineData("Relatórios", "SIGARH")]
    [InlineData("relatorios", "SIGARH")]
    [InlineData("SIGARH Agendador", "Folha")]
    [InlineData("WebNews", null)]
    [InlineData("Outro", null)]
    public async Task Ficha_de_uma_linha_vai_pelo_nome_normalizado(string nomeNaTabela, string? esperada)
    {
        Devolver(new CatalogoObtido(Catalogo(), null));

        var fichas = (await Listar()).Value;

        fichas.Ficha(Linha(nomeNaTabela)).Should().Be(esperada);
    }

    [Fact]
    public async Task Falha_sem_leitura_anterior_devolve_o_erro()
    {
        Devolver(Erros.Catalogo.NaoConfigurado);

        (await Listar()).Error.Should().Be(Erros.Catalogo.NaoConfigurado);
    }

    [Fact]
    public async Task Falha_com_leitura_anterior_devolve_o_mapa_dessa_leitura()
    {
        var catalogo = Catalogo();
        Devolver(new CatalogoObtido(catalogo, new FalhaDeLeitura(new Erro("x", "Tempo esgotado."), LidoEm)));

        (await Listar()).Value.PorNome.Should().BeEquivalentTo(catalogo.FichasPorVersao());
    }

    [Fact]
    public async Task Cancelamento_vira_o_erro_de_cancelamento_do_catalogo()
    {
        _servico.Setup(s => s.ObterAsync(false, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        (await Listar()).Error.Should().Be(Erros.Catalogo.Cancelada);
    }
}
