using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.Sistemas;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Sistemas;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>Feature do catálogo: autoriza antes de ler (FR-003) e monta os view models (data-model.md).</summary>
public class ObterCatalogoHandlerTests
{
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 24, 13, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<ICatalogoSistemasService> _servico = new(MockBehavior.Strict);
    private readonly ObterCatalogo.Handler _handler;

    public ObterCatalogoHandlerTests()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Sistemas)).ReturnsAsync(true);
        _handler = new ObterCatalogo.Handler(_autorizacao.Object, _servico.Object);
    }

    private static Instancia Inst(string? urls = null, string? maquinas = null, string? caminho = null) => new(
        urls?.Split('|') ?? [],
        maquinas?.Split(", ") ?? [],
        caminho);

    private static Sistema Sgq() => new(
        "SGQ",
        "Sistema da qualidade",
        new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>
        {
            [AmbienteDoCatalogo.Prd] =
            [
                Inst("https://tcmrj.tcmnet.tcm/sgq|HTTPS://Outro/SGQ", "TCM2016WEB1, TCM2016WEB2", @"W:\TcmRoot\Sites\sgq"),
                Inst(maquinas: "TCMWEBC", caminho: @"C:\Reserva"),
            ],
            [AmbienteDoCatalogo.Dsv] = [Inst("N/A|AgenteSIGA.EXE (diversos agendamentos)|javascript:alert(1)|\\\\servidor\\pasta|http://dsv/sgq")],
        });

    private void Devolver(Result<CatalogoObtido, Erro> resultado, bool atualizar = false) =>
        _servico.Setup(s => s.ObterAsync(atualizar, It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    private async Task<CatalogoVM> Obter(bool atualizar = false)
    {
        var resultado = await _handler.Handle(new ObterCatalogo.Query(atualizar), CancellationToken.None);
        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        return resultado.Value;
    }

    [Fact]
    public async Task Sem_a_politica_devolve_AcessoNegado_sem_chamar_o_servico()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Sistemas)).ReturnsAsync(false);

        var resultado = await _handler.Handle(new ObterCatalogo.Query(false), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoAcessoNegado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Com_a_politica_devolve_o_catalogo_com_total_horario_e_origem()
    {
        Devolver(new CatalogoObtido(new CatalogoLido([Sgq()], 0, LidoEm, "https://git/origem"), null));

        var catalogo = await Obter();

        catalogo.Total.Should().Be(1);
        catalogo.LidoEm.Should().Be(LidoEm);
        catalogo.Origem.Should().Be("https://git/origem");
        catalogo.Ignorados.Should().Be(0);
        catalogo.Falha.Should().BeNull();
        catalogo.Sistemas.Single().Nome.Should().Be("SGQ");
    }

    [Fact]
    public async Task Erro_do_servico_chega_a_tela()
    {
        Devolver(Erros.Catalogo.NaoConfigurado);

        var resultado = await _handler.Handle(new ObterCatalogo.Query(false), CancellationToken.None);

        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoNaoConfigurado);
    }

    [Fact]
    public async Task Ambientes_sempre_com_as_quatro_entradas_na_ordem()
    {
        Devolver(new CatalogoObtido(new CatalogoLido([Sgq()], 0, LidoEm, "o"), null));

        var sistema = (await Obter()).Sistemas.Single();

        sistema.Ambientes.Select(a => a.Sigla).Should().Equal("DSV", "HML", "PRD", "CONT");
        sistema.Ambientes.Select(a => a.Nome).Should().Equal("Desenvolvimento", "Homologação", "Produção", "Contingência");
        sistema.Ambientes.Select(a => a.DataAmb).Should().Equal("dsv", "hml", "prd", "cont");
        sistema.Ambientes[1].Instancias.Should().BeEmpty();
        sistema.Ambientes[2].Instancias.Should().HaveCount(2);
    }

    [Fact]
    public async Task So_http_e_https_viram_link_e_a_exibicao_nao_tem_o_esquema()
    {
        Devolver(new CatalogoObtido(new CatalogoLido([Sgq()], 0, LidoEm, "o"), null));

        var sistema = (await Obter()).Sistemas.Single();

        var prd = sistema.Ambientes[2].Instancias[0].Enderecos;
        prd.Select(e => e.EhLink).Should().Equal(true, true);
        prd.Select(e => e.Exibicao).Should().Equal("tcmrj.tcmnet.tcm/sgq", "Outro/SGQ");
        prd.Select(e => e.Texto).Should().Equal("https://tcmrj.tcmnet.tcm/sgq", "HTTPS://Outro/SGQ");

        var dsv = sistema.Ambientes[0].Instancias[0].Enderecos;
        dsv.Select(e => e.EhLink).Should().Equal(false, false, false, false, true);
        dsv[0].Exibicao.Should().Be("N/A");
        dsv[2].Exibicao.Should().Be("javascript:alert(1)");
    }

    [Fact]
    public async Task Texto_de_busca_tem_nome_observacao_enderecos_maquinas_e_caminhos()
    {
        Devolver(new CatalogoObtido(new CatalogoLido([Sgq()], 0, LidoEm, "o"), null));

        var texto = (await Obter()).Sistemas.Single().TextoDeBusca;

        texto.Should().Contain("SGQ").And.Contain("Sistema da qualidade")
            .And.Contain("https://tcmrj.tcmnet.tcm/sgq").And.Contain("HTTPS://Outro/SGQ")
            .And.Contain("TCM2016WEB1").And.Contain("TCM2016WEB2").And.Contain("TCMWEBC")
            .And.Contain(@"W:\TcmRoot\Sites\sgq").And.Contain(@"C:\Reserva")
            .And.Contain("AgenteSIGA.EXE");
    }

    [Fact]
    public async Task Atualizar_chega_ao_servico_como_releitura_forcada()
    {
        Devolver(new CatalogoObtido(new CatalogoLido([Sgq()], 0, LidoEm, "o"), null), atualizar: true);

        await Obter(atualizar: true);

        _servico.Verify(s => s.ObterAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ignorados_e_falha_chegam_ao_view_model()
    {
        var falhaEm = LidoEm.AddMinutes(3);
        Devolver(new CatalogoObtido(
            new CatalogoLido([Sgq()], 2, LidoEm, "o"),
            new FalhaDeLeitura(Erros.Catalogo.AcessoRecusado, falhaEm)));

        var catalogo = await Obter();

        catalogo.Ignorados.Should().Be(2);
        catalogo.Falha.Should().NotBeNull();
        catalogo.Falha!.Mensagem.Should().Be(Erros.Catalogo.AcessoRecusado.Message);
        catalogo.Falha.Em.Should().Be(falhaEm);
    }

    [Fact]
    public async Task Cancelamento_vira_Cancelada()
    {
        _servico.Setup(s => s.ObterAsync(false, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        var resultado = await _handler.Handle(new ObterCatalogo.Query(false), CancellationToken.None);

        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoCancelada);
    }
}
