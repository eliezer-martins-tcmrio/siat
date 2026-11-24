using AwesomeAssertions;
using Bunit;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Features.Sistemas;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Sistemas;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Web.Components.Pages.Catalogo;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Página Catálogo › Sistemas: sem <c>Sistemas.CanSelect</c>, nada do catálogo sai (FR-003); com ele, tabela, busca
/// da URL, avisos e rodapé (contracts/rotas-ui.md).
/// </summary>
public sealed class SistemasPaginaTests : BunitContext
{
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 24, 13, 5, 0, TimeSpan.Zero);

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new();
    private readonly Mock<IMediator> _mediador = new(MockBehavior.Strict);

    public SistemasPaginaTests()
    {
        Services.AddSingleton(_autorizacao.Object);
        Services.AddSingleton(_mediador.Object);
        Services.AddSingleton(new Mock<IInteracao>().Object);
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(LidoEm));
        JSInterop.Mode = JSRuntimeMode.Loose;
        AddAuthorization().SetAuthorized("usuario.teste");
    }

    private static CatalogoVM Catalogo(int ignorados = 0, FalhaVM? falha = null)
    {
        static Sistema Sistema(string nome, string? obs = null) => new(nome, obs, new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>
        {
            [AmbienteDoCatalogo.Prd] = [new Instancia([$"https://{nome.ToLowerInvariant()}.tcm"], ["TCM2016WEB1"], @"W:\Sites\" + nome)],
        });

        return CatalogoVM.De(new CatalogoObtido(
            new CatalogoLido([Sistema("eTCM", "Portal"), Sistema("SGQ"), Sistema("STP")], ignorados, LidoEm, "https://git.tcmnet.tcm/asi/TCMRJDoc/-/blob/master/sistemasDb.js"),
            falha is null ? null : new FalhaDeLeitura(new Erro("x", falha.Mensagem), falha.Em)));
    }

    private void Devolver(Result<CatalogoVM, Erro> resultado) =>
        _mediador.Setup(m => m.Send(It.IsAny<ObterCatalogo.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    private IRenderedComponent<Sistemas> Abrir(string url = "catalogo/sistemas", bool acesso = true)
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Sistemas)).ReturnsAsync(acesso);
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Sistemas>();
    }

    [Theory]
    [InlineData("catalogo/sistemas")]
    [InlineData("catalogo/sistemas?busca=etcm")]
    public void Sem_Sistemas_CanSelect_mostra_acesso_negado_sem_chamar_a_feature(string url)
    {
        var pagina = Abrir(url, acesso: false);

        pagina.Find(".mono").TextContent.Should().Be("Sistemas.CanSelect");
        _mediador.VerifyNoOtherCalls();
        pagina.Markup.Should().NotContain("eTCM").And.NotContain("SGQ").And.NotContain("<table");
    }

    [Fact]
    public void Com_acesso_mostra_a_tabela_com_cabecalhos_contador_horario_e_rodape()
    {
        Devolver(Catalogo());

        var pagina = Abrir();

        pagina.FindAll("thead th").Select(th => th.TextContent.Trim()).Should().Equal(
            "Sistema", "DSV Desenvolvimento", "HML Homologação", "PRD Produção", "CONT Contingência", "Observação");
        pagina.FindAll("thead th[data-amb]").Select(th => th.GetAttribute("data-amb")).Should().Equal("dsv", "hml", "prd", "cont");
        pagina.FindAll("tbody tr").Should().HaveCount(3);
        pagina.FindAll("tbody td.nome").Select(td => td.TextContent).Should().Equal("eTCM", "SGQ", "STP");
        pagina.Find(".contador").TextContent.Should().Be("3 sistemas");
        pagina.Find("input#busca-sistemas").GetAttribute("placeholder").Should().Be("Sistema, máquina, URL ou caminho");
        pagina.Find(".cabecalho .status").TextContent.Should().StartWith("Catálogo lido às ");
        var fonte = pagina.Find(".rodape a");
        fonte.GetAttribute("href").Should().Be("https://git.tcmnet.tcm/asi/TCMRJDoc/-/blob/master/sistemasDb.js");
        fonte.GetAttribute("target").Should().Be("_blank");
        pagina.FindAll(".aviso").Should().BeEmpty();
    }

    [Fact]
    public void A_busca_da_URL_e_aplicada_ao_abrir()
    {
        Devolver(Catalogo());

        var pagina = Abrir("catalogo/sistemas?busca=sgq");

        pagina.Find("input#busca-sistemas").GetAttribute("value").Should().Be("sgq");
        pagina.FindAll("tbody td.nome").Select(td => td.TextContent).Should().Equal("SGQ");
        pagina.Find(".contador").TextContent.Should().Be("1 de 3 sistemas");
    }

    [Fact]
    public void Digitar_grava_a_busca_na_URL_sem_novo_historico()
    {
        Devolver(Catalogo());
        var pagina = Abrir();

        pagina.Find("input#busca-sistemas").Input("multas hml");

        var navegacao = Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>();
        navegacao.Uri.Should().EndWith("catalogo/sistemas?busca=multas%20hml");
        navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
    }

    [Fact]
    public void Sem_resultado_mostra_o_estado_vazio_e_Limpar_busca_volta_a_lista()
    {
        Devolver(Catalogo());
        var pagina = Abrir("catalogo/sistemas?busca=xyzxyz");

        pagina.FindAll("table").Should().BeEmpty();
        pagina.Find(".estado h2").TextContent.Should().Be("Nenhum sistema encontrado");

        pagina.Find("#limpar-busca").Click();

        pagina.FindAll("tbody tr").Should().HaveCount(3);
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("catalogo/sistemas");
        JSInterop.VerifyInvoke("siat.focar").Arguments.Should().Equal("busca-sistemas");
    }

    [Fact]
    public void Itens_ignorados_e_falha_da_releitura_aparecem_em_avisos()
    {
        Devolver(Catalogo(ignorados: 2, falha: new FalhaVM("O GitLab recusou o acesso ao catálogo (token inválido ou expirado).", LidoEm)));

        var pagina = Abrir();

        var avisos = pagina.FindAll(".aviso").Select(a => a.TextContent.Trim()).ToArray();
        avisos.Should().HaveCount(2);
        avisos[0].Should().StartWith("Não foi possível atualizar o catálogo: O GitLab recusou o acesso ao catálogo (token inválido ou expirado). Mostrando os dados lidos às ");
        avisos[1].Should().Be("2 itens do sistemasDb.js foram ignorados por estarem incompletos.");
    }

    [Fact]
    public void Um_item_ignorado_fica_no_singular()
    {
        Devolver(Catalogo(ignorados: 1));

        Abrir().Find(".aviso").TextContent.Trim().Should().Be("1 item do sistemasDb.js foi ignorado por estar incompleto.");
    }

    [Fact]
    public void Erro_sem_catalogo_mostra_o_estado_de_erro_e_Tentar_de_novo_forca_a_releitura()
    {
        Devolver(Erros.Catalogo.NaoConfigurado);
        var pagina = Abrir();

        pagina.Find(".estado.erro code").TextContent.Should().Be("A origem do catálogo não está configurada.");

        Devolver(Catalogo());
        pagina.FindAll(".estado.erro button").Single(b => b.TextContent == "Tentar de novo").Click();

        pagina.FindAll("tbody tr").Should().HaveCount(3);
        _mediador.Verify(m => m.Send(It.Is<ObterCatalogo.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Atualizar_agora_pede_releitura_forcada()
    {
        Devolver(Catalogo());
        var pagina = Abrir();

        pagina.Find("#atualizar").Click();

        _mediador.Verify(m => m.Send(It.Is<ObterCatalogo.Query>(q => !q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        _mediador.Verify(m => m.Send(It.Is<ObterCatalogo.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Observacao_com_marcacao_aparece_como_texto()
    {
        var catalogo = CatalogoVM.De(new CatalogoObtido(
            new CatalogoLido([new Sistema("SCP", "a<br />b", new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>())], 0, LidoEm, "https://git/x"),
            null));
        Devolver(catalogo);

        var pagina = Abrir();

        pagina.FindAll("td.obs br").Should().BeEmpty();
        pagina.Find("td.obs").TextContent.Trim().Should().Be("a<br />b");
    }
}
