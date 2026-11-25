using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Reqnroll;
using Tcmrj.Siat.App.Web.Features.Catalogo;
using Tcmrj.Siat.App.Web.Features.Versoes;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Test.Unidade.Versoes;

namespace Tcmrj.Siat.Test.Features.Steps;

/// <summary>
/// Partes da ficha liberadas por recurso (specs/004-ficha-sistema, FR-001, FR-002 e FR-005): chama os handlers
/// <see cref="ObterFicha"/>, <see cref="ObterVersoesDaFicha"/> e <see cref="ListarFichas"/> com autorização e serviços
/// falsos que contam as leituras.
/// </summary>
[Binding]
public sealed class PermissoesDaFichaSteps
{
    private static readonly string[] Vinculo = ["SIGARH", "SIGARH Agendador"];

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<ICatalogoSistemasService> _catalogo = new(MockBehavior.Strict);
    private readonly Mock<IVersoesPublicadasService> _versoes = new(MockBehavior.Strict);
    private int _leiturasDoCatalogo;
    private int _leiturasDasVersoes;

    private Result<FichaVM, Erro> _ficha;
    private Result<VersoesDaFichaVM, Erro> _versoesDaFicha;
    private Result<FichasVM, Erro> _fichas;
    private bool _fichaNegada;
    private bool _versoesNegadas;
    private bool _fichasNegadas;

    [Given(@"um usuário com o catálogo ""(.*)"" e as versões ""(.*)""")]
    public void DadoUmUsuario(string catalogo, string versoes)
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(catalogo == "sim");
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(versoes == "sim");
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, ObterVersoesHandlerTests.Hml.Chave))
            .ReturnsAsync(ObterVersoesHandlerTests.Hml);

        var item = new Sistema("SIGARH", null, new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>(), VinculoDeVersoes.De(Vinculo));
        var lido = new CatalogoLido([item], 0, DateTimeOffset.UnixEpoch, "https://git/sistemasDb.js");
        _catalogo.Setup(c => c.ObterAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => _leiturasDoCatalogo++)
            .ReturnsAsync(new CatalogoObtido(lido, null));
        _versoes.Setup(v => v.ObterDoSistemaAsync(It.IsAny<Servidor>(), It.IsAny<VinculoDeVersoes>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => _leiturasDasVersoes++)
            .ReturnsAsync(new VersoesObtidas(ObterVersoesHandlerTests.Lidas(ObterVersoesHandlerTests.Hml), null));
    }

    [When(@"ele pede a ficha do SIGARH, as versões da ficha e as fichas da tela de Versões")]
    public async Task QuandoElePede()
    {
        var token = TestContext.Current.CancellationToken;
        _ficha = await new ObterFicha.Handler(_autorizacao.Object, _catalogo.Object)
            .Handle(new ObterFicha.Query("SIGARH", false), token);
        _versoesDaFicha = await new ObterVersoesDaFicha.Handler(
                _autorizacao.Object, _versoes.Object, ObterVersoesHandlerTests.Opcoes(ObterVersoesHandlerTests.Hml.Chave), new FakeTimeProvider())
            .Handle(new ObterVersoesDaFicha.Query(null, Vinculo, false), token);
        _fichas = await new ListarFichas.Handler(_autorizacao.Object, _catalogo.Object)
            .Handle(new ListarFichas.Query(), token);
    }

    [Then(@"a ficha é ""(.*)""")]
    public void EntaoAFichaE(string ficha)
    {
        _fichaNegada = ficha == "negada";
        if (_fichaNegada)
        {
            _ficha.Error.Should().Be(Erros.Catalogo.AcessoNegado);
            return;
        }

        _ficha.IsSuccess.Should().BeTrue();
        if (ficha == "com vínculo")
        {
            _ficha.Value.MostraVersoes.Should().BeTrue();
            _ficha.Value.Vinculo.Should().Equal(Vinculo);
        }
        else
        {
            _ficha.Value.MostraVersoes.Should().BeFalse();
            _ficha.Value.Vinculo.Should().BeEmpty();
        }
    }

    [Then(@"as versões da ficha são ""(.*)""")]
    public void EntaoAsVersoesDaFichaSao(string versoes)
    {
        _versoesNegadas = versoes == "negadas";
        if (_versoesNegadas)
        {
            _versoesDaFicha.IsFailure.Should().BeTrue();
            _versoesDaFicha.Error.Code.Should().BeOneOf(Erros.Catalogo.CodigoAcessoNegado, Erros.Versoes.CodigoAcessoNegado);
        }
        else
        {
            _versoesDaFicha.IsSuccess.Should().BeTrue();
            _versoesDaFicha.Value.Componentes.Should().NotBeNull();
        }
    }

    [Then(@"as fichas da tela de Versões são ""(.*)""")]
    public void EntaoAsFichasSao(string fichas)
    {
        _fichasNegadas = fichas == "negadas";
        if (_fichasNegadas)
        {
            _fichas.Error.Should().Be(Erros.Catalogo.AcessoNegado);
        }
        else
        {
            _fichas.Value.PorNome.Should().ContainKey("sigarh agendador");
        }
    }

    [Then(@"nenhuma leitura é feita para o que foi negado")]
    public void EntaoNenhumaLeituraParaONegado()
    {
        var leiturasEsperadasDoCatalogo = (_fichaNegada ? 0 : 1) + (_fichasNegadas ? 0 : 1);
        _leiturasDoCatalogo.Should().Be(leiturasEsperadasDoCatalogo);
        _leiturasDasVersoes.Should().Be(_versoesNegadas ? 0 : 1);
    }
}
