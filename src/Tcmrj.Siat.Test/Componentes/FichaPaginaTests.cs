using System.Globalization;
using AwesomeAssertions;
using Bunit;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Features.Catalogo;
using Tcmrj.Siat.App.Web.Features.Servidores;
using Tcmrj.Siat.App.Web.Features.Versoes;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Catalogo;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Web.Services;
using Pagina = Tcmrj.Siat.Web.Components.Pages.Sistemas.Ficha;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Página Sistemas › Catálogo › {sistema} (specs/004-ficha-sistema/contracts/rotas-ui.md, Tela Sistemas/Ficha.razor):
/// catálogo e versões do vínculo (História 1), servidor (História 2), avisos e não encontrado (História 3), leitura,
/// andamento e falhas (História 4) e acesso por recurso (História 5).
/// </summary>
public sealed class FichaPaginaTests : BunitContext
{
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 24, 13, 5, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset GeradoEm = new(2026, 11, 24, 13, 7, 0, TimeSpan.Zero);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private const string Origem = "https://git.tcmnet.tcm/asi/TCMRJDoc/-/blob/master/sistemasDb.js";

    private static readonly ServidorVM Dev = new("tcmbd3-dev", @"TCMBD3\DEV", Ambiente.Dsv);

    private static readonly ServidorVM Hml = new("tcmbd4-hml", @"TCMBD4\HML", Ambiente.Hml);

    private static readonly ServidorVM Prd = new("tcmbd-prd", @"TCMBD\PRD", Ambiente.Prd);

    private static readonly string[] VinculoPadrao = ["SIGARH", "SIGARH Agendador"];

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new();
    private readonly Mock<IMediator> _mediador = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _relogio = new(GeradoEm);
    private IReadOnlyList<GrupoServidoresVM> _grupos =
    [
        new(Ambiente.Dsv, [Dev]),
        new(Ambiente.Hml, [Hml]),
        new(Ambiente.Prd, [Prd]),
    ];

    public FichaPaginaTests()
    {
        Services.AddSingleton(_autorizacao.Object);
        Services.AddSingleton(_mediador.Object);
        Services.AddSingleton(new Mock<IInteracao>().Object);
        Services.AddSingleton<TimeProvider>(_relogio);
        var versoes = new Mock<IOptionsMonitor<CacheDeVersoesOptions>>();
        versoes.Setup(o => o.CurrentValue).Returns(new CacheDeVersoesOptions { ServidorPadrao = Prd.Chave, MinutosDeCache = 5 });
        Services.AddSingleton(versoes.Object);
        var catalogo = new Mock<IOptionsMonitor<CacheDoCatalogoOptions>>();
        catalogo.Setup(o => o.CurrentValue).Returns(new CacheDoCatalogoOptions { MinutosDeCache = 10 });
        Services.AddSingleton(catalogo.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        AddAuthorization().SetAuthorized("usuario.teste");

        _mediador.Setup(m => m.Send(It.IsAny<ListarPermitidos.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _grupos);
        _mediador.Setup(m => m.Send(It.IsAny<ObterAndamento.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<int?, Erro>(null));
    }

    // ---------- Dados ----------

    private static Sistema Item(
        string nome = "SIGARH",
        string? observacao = "Folha de pagamento\nMódulo <b>principal</b>",
        VinculoDeVersoes? versoes = null) =>
        new(nome, observacao, new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>
        {
            [AmbienteDoCatalogo.Dsv] = [new Instancia(["https://sigarh-dsv.tcm"], ["TCM2016DSV1"], @"W:\Sites\sigarh_dsv")],
            [AmbienteDoCatalogo.Hml] = [new Instancia(["https://sigarh-hml.tcm"], ["TCM2016HML1"], null)],
            [AmbienteDoCatalogo.Prd] = [new Instancia(["https://sigarh.tcm"], ["TCM2016WEB1", "TCM2016WEB2"], @"W:\Sites\sigarh")],
            [AmbienteDoCatalogo.Cont] = [new Instancia([], ["TCMCONT1"], null)],
        }, versoes ?? VinculoDeVersoes.De(VinculoPadrao));

    private static FichaVM Ficha(
        Sistema? item = null,
        bool mostraVersoes = true,
        int repetidos = 0,
        FalhaDeLeitura? falha = null)
    {
        item ??= Item();
        var outros = Enumerable.Range(1, repetidos).Select(i => new Sistema(item.Nome, null, new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>(), posicao: i));
        var lido = new CatalogoLido([item, .. outros], 0, LidoEm, Origem);
        return FichaVM.De(lido.Localizar(item.Nome)!, new CatalogoObtido(lido, falha), mostraVersoes);
    }

    private static string Caminho(Ambiente ambiente, int maquina, string pasta) => $@"\\APP{ambiente}0{maquina}\Sites\{pasta}\VERSAO.TXT";

    // SIGARH: DSV igual nas duas máquinas, PRD com erro na máquina 2. SIGARH Agendador: dois registros; o primeiro com
    // divergência em DSV e caminho do raiz. HML sem nenhum caminho. "Outro" fica fora do vínculo.
    private static readonly SistemaVersionado[] Registros =
    [
        new(1, "SIGARH",
        [
            LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina1, "2.1.0", Caminho(Ambiente.Dsv, 1, "sigarh")),
            LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina2, "2.1.0", Caminho(Ambiente.Dsv, 2, "sigarh")),
            LeituraDeCelula.ComVersao(Ambiente.Prd, Maquina.Maquina1, "2.0.0", Caminho(Ambiente.Prd, 1, "sigarh")),
            LeituraDeCelula.ComErro(Ambiente.Prd, Maquina.Maquina2, "Arquivo <b>não</b> encontrado.", Caminho(Ambiente.Prd, 2, "sigarh")),
        ]),
        new(7, "SIGARH Agendador",
        [
            LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina1, "1.0.0", Caminho(Ambiente.Dsv, 1, "agendador"), @"\\APPDSV01\Sites\raiz\VERSAO.TXT"),
            LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina2, "1.0.1", Caminho(Ambiente.Dsv, 2, "agendador")),
        ]),
        new(9, "SIGARH Agendador",
        [
            LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina1, "1.0.0", Caminho(Ambiente.Dsv, 1, "agendador2")),
        ]),
        new(12, "Outro", [LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina1, "9.9.9", Caminho(Ambiente.Dsv, 1, "outro"))]),
    ];

    private static VersoesDaFichaVM Versoes(
        ServidorVM? servidor = null,
        IReadOnlyList<SistemaVersionado>? registros = null,
        IEnumerable<string>? vinculo = null,
        FalhaDeLeitura? falha = null)
    {
        servidor ??= Prd;
        var dominio = new Servidor { Chave = servidor.Chave, Nome = servidor.Nome, Endereco = servidor.Nome, Ambiente = servidor.Ambiente };
        var lidas = new VersoesLidas(dominio, GeradoEm, TimeSpan.FromSeconds(1.3), registros ?? Registros, 8);
        return VersoesDaFichaVM.De(new VersoesObtidas(lidas, falha), VinculoDeVersoes.De(vinculo ?? VinculoPadrao), servidor);
    }

    private void DevolverFicha(Result<FichaVM, Erro> resultado) =>
        _mediador.Setup(m => m.Send(It.IsAny<ObterFicha.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    private void DevolverVersoes(Result<VersoesDaFichaVM, Erro> resultado) =>
        _mediador.Setup(m => m.Send(It.IsAny<ObterVersoesDaFicha.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    // Leituras que só terminam quando o teste quiser (ou quando o token for cancelado).
    private TaskCompletionSource<Result<FichaVM, Erro>> PendurarFicha()
    {
        var fonte = new TaskCompletionSource<Result<FichaVM, Erro>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediador.Setup(m => m.Send(It.IsAny<ObterFicha.Query>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<Result<FichaVM, Erro>> _, CancellationToken token) => fonte.Task.WaitAsync(token));
        return fonte;
    }

    private TaskCompletionSource<Result<VersoesDaFichaVM, Erro>> PendurarVersoes()
    {
        var fonte = new TaskCompletionSource<Result<VersoesDaFichaVM, Erro>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediador.Setup(m => m.Send(It.IsAny<ObterVersoesDaFicha.Query>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<Result<VersoesDaFichaVM, Erro>> _, CancellationToken token) => fonte.Task.WaitAsync(token));
        return fonte;
    }

    private IRenderedComponent<Pagina> Abrir(string url = "sistemas/catalogo/ficha?sistema=SIGARH", bool catalogo = true)
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(catalogo);
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Pagina>();
    }

    private Bunit.TestDoubles.BunitNavigationManager Navegacao => Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>();

    private static string Hora(DateTimeOffset momento) => momento.ToLocalTime().ToString("HH:mm", PtBr);

    private static string[] Avisos(IRenderedComponent<Pagina> pagina) =>
        pagina.FindAll(".avisos > div").Select(a => Espacos(a.TextContent)).ToArray();

    private static string Espacos(string texto) => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static AngleSharp.Dom.IElement Secao(IRenderedComponent<Pagina> pagina, string dataAmb) =>
        pagina.Find($"section.ficha-ambiente[data-amb='{dataAmb}']");

    private void VerificarSemVersoes()
    {
        _mediador.Verify(m => m.Send(It.IsAny<ObterVersoesDaFicha.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediador.Verify(m => m.Send(It.IsAny<ListarPermitidos.Query>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- História 5: acesso ----------

    [Theory]
    [InlineData("sistemas/catalogo/ficha?sistema=SIGARH")]
    [InlineData("sistemas/catalogo/ficha?sistema=SIGARH&servidor=tcmbd3-dev")]
    public void Sem_Catalogo_Sistemas_CanSelect_mostra_acesso_negado_sem_chamar_feature(string url)
    {
        var pagina = Abrir(url, catalogo: false);

        pagina.Find(".mono").TextContent.Should().Be("Catalogo_Sistemas.CanSelect");
        _mediador.VerifyNoOtherCalls();
        pagina.Markup.Should().NotContain("SIGARH").And.NotContain("sistemasDb.js").And.NotContain("<table");
    }

    [Fact]
    public void So_com_o_catalogo_a_ficha_mostra_so_o_catalogo()
    {
        DevolverFicha(Ficha(mostraVersoes: false));

        var pagina = Abrir("sistemas/catalogo/ficha?sistema=SIGARH&servidor=tcmbd3-dev");

        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);
        pagina.FindAll("select#servidor").Should().BeEmpty();
        pagina.FindAll(".contexto").Should().BeEmpty();
        pagina.FindAll("table").Should().BeEmpty();
        pagina.FindAll(".legenda").Should().BeEmpty();
        pagina.FindAll(".avisos > div").Should().BeEmpty();
        pagina.FindAll(".pilula").Should().BeEmpty();
        pagina.Find("footer.rodape").TextContent.Should().NotContain("Apps_VersionAddresses");
        pagina.Markup.Should().NotContain("Contingência.</p>").And.NotContain("tcmbd3-dev");
        VerificarSemVersoes();
    }

    // ---------- História 1: catálogo e versões ----------

    [Theory]
    [InlineData("sistemas/catalogo/ficha?sistema=SIGARH", "SIGARH")]
    [InlineData("sistemas/catalogo/ficha?sistema=A%26B%2FC%3F%23%25", "A&B/C?#%")]
    public void Envia_ObterFicha_com_o_nome_da_URL(string url, string nome)
    {
        DevolverFicha(Ficha(mostraVersoes: false));

        Abrir(url);

        _mediador.Verify(m => m.Send(It.Is<ObterFicha.Query>(q => q.Sistema == nome && !q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Mostra_trilha_titulo_e_observacao_como_texto()
    {
        DevolverFicha(Ficha(mostraVersoes: false));

        var pagina = Abrir();

        var trilha = pagina.Find("nav[aria-label='Trilha de navegação'] ol");
        trilha.QuerySelectorAll("li").Select(li => li.TextContent.Trim()).Should().Equal("Sistemas", "Catálogo", "SIGARH");
        trilha.QuerySelector("a")!.GetAttribute("href").Should().Be("sistemas/catalogo");
        trilha.QuerySelector("[aria-current='page']")!.TextContent.Should().Be("SIGARH");
        pagina.Find("h1").TextContent.Should().Be("SIGARH");
        pagina.Find(".cabecalho .sub").TextContent.Should().Be("Onde o sistema roda e o que está publicado em cada ambiente.");
        var observacao = pagina.Find("p.observacao");
        observacao.TextContent.Should().Be("Folha de pagamento\nMódulo <b>principal</b>");
        observacao.QuerySelectorAll("b").Should().BeEmpty();
    }

    [Fact]
    public void Quatro_secoes_na_ordem_com_as_instancias_do_catalogo()
    {
        DevolverFicha(Ficha(mostraVersoes: false));

        var pagina = Abrir();

        var secoes = pagina.FindAll("section.ficha-ambiente");
        secoes.Select(s => s.GetAttribute("data-amb")).Should().Equal("dsv", "hml", "prd", "cont");
        secoes.Select(s => s.QuerySelector(".amb")!.TextContent).Should().Equal("DSV", "HML", "PRD", "CONT");
        secoes.Select(s => s.QuerySelector("h2")!.TextContent).Should().Equal("Desenvolvimento", "Homologação", "Produção", "Contingência");
        secoes[0].GetAttribute("aria-labelledby").Should().Be(secoes[0].QuerySelector("h2")!.Id);
        Secao(pagina, "prd").QuerySelectorAll("button").Select(b => b.GetAttribute("aria-label")).Should().Contain(
            "Copiar máquina TCM2016WEB2 de PRD do SIGARH");
        Secao(pagina, "dsv").QuerySelector("a")!.GetAttribute("href").Should().Be("https://sigarh-dsv.tcm");
    }

    [Fact]
    public void Com_versoes_le_o_vinculo_no_servidor_padrao_e_monta_a_tabela_por_ambiente()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());

        var pagina = Abrir();

        _mediador.Verify(m => m.Send(
            It.Is<ObterVersoesDaFicha.Query>(q => q.Servidor == Prd.Chave && q.Vinculo.SequenceEqual(VinculoPadrao) && !q.Atualizar),
            It.IsAny<CancellationToken>()), Times.Once);

        var dsv = Secao(pagina, "dsv");
        dsv.QuerySelector(".bloco-titulo[id='versoes-dsv']")!.TextContent.Should().Be("Versões em DSV");
        var tabela = dsv.QuerySelector("table.tabela-ficha")!;
        tabela.GetAttribute("aria-labelledby").Should().Be("versoes-dsv");
        tabela.QuerySelectorAll("th[scope='col']").Select(th => th.TextContent).Should().Equal("Componente", "Máquina 1", "Máquina 2");
        var linhas = tabela.QuerySelectorAll("tr.registro");
        linhas.Select(l => l.QuerySelector("td.nome")!.TextContent).Should().Equal("SIGARH", "SIGARH Agendador", "SIGARH Agendador");
        linhas[0].QuerySelectorAll(".versao.ok").Select(v => v.TextContent).Should().Equal("2.1.0", "2.1.0");
        linhas[1].QuerySelectorAll(".versao.divergente").Select(v => v.TextContent).Should().Equal("1.0.0", "1.0.1");
        linhas[2].QuerySelector(".vazio-cel").Should().NotBeNull();
        tabela.TextContent.Should().NotContain("Outro");

        var caminhos = tabela.QuerySelectorAll("tr.caminhos-linha")[1];
        caminhos.QuerySelector("td")!.GetAttribute("colspan").Should().Be("3");
        caminhos.QuerySelectorAll(".suave").Select(s => s.TextContent).Should().Contain("Máquina 1").And.Contain("Máquina 2");
        caminhos.QuerySelector(".caminho .mono")!.TextContent.Should().Be(Caminho(Ambiente.Dsv, 1, "agendador"));
        caminhos.QuerySelectorAll("button").Select(b => b.GetAttribute("aria-label")).Should().Contain("Copiar caminho de DSV, máquina 1, do SIGARH Agendador");
        caminhos.QuerySelector(".caminho-raiz .mono")!.TextContent.Should().Be(@"\\APPDSV01\Sites\raiz\VERSAO.TXT");
    }

    [Fact]
    public void Erro_de_arquivo_aparece_como_texto_e_ambiente_sem_caminho_tem_nota()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());

        var pagina = Abrir();

        var prd = Secao(pagina, "prd");
        prd.QuerySelector("tr.registro .pilula.erro").Should().NotBeNull();
        var erro = prd.QuerySelector(".erro-msg")!;
        erro.TextContent.Should().Be("Arquivo <b>não</b> encontrado.");
        erro.QuerySelectorAll("b").Should().BeEmpty();
        Secao(pagina, "hml").QuerySelector("p.nota")!.TextContent.Should().Be("Nenhum endereço de versão configurado em HML.");
        Secao(pagina, "hml").QuerySelectorAll("table").Should().BeEmpty();
        Secao(pagina, "cont").QuerySelector("p.nota")!.TextContent.Should().Be("Não há endereços de versão para Contingência.");
        Secao(pagina, "cont").QuerySelectorAll(".instancia").Should().HaveCount(1);
    }

    [Fact]
    public void Pilulas_status_legenda_e_rodape()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());

        var pagina = Abrir();

        pagina.FindAll(".situacao .pilula").Select(p => p.TextContent).Should().Equal("Com divergência", "Com erro");
        pagina.Find(".cabecalho .status").TextContent.Should().Be(
            $"Catálogo lido às {Hora(LidoEm)} · Versões geradas às {Hora(GeradoEm)} em 1,3 s");
        pagina.FindAll(".legenda > span").Should().HaveCount(4);
        var rodape = Espacos(pagina.Find("footer.rodape").TextContent);
        rodape.Should().Contain("Catálogo: sistemasDb.js (abre em nova aba) no TCMRJDoc · cache de 10 minutos.");
        rodape.Should().Contain(@"Endereços: TCMBD\PRD · CSLIB_UserManager.dbo.Apps_VersionAddresses (registros ativos) · cache de 5 minutos.");
        pagina.Find("footer.rodape a").GetAttribute("href").Should().Be(Origem);
    }

    [Fact]
    public void Sem_pendencias_mostra_so_essa_pilula()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes(
            registros: [new SistemaVersionado(1, "SIGARH", [LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina1, "2.1.0", Caminho(Ambiente.Dsv, 1, "sigarh"))])],
            vinculo: ["SIGARH"]));

        var pagina = Abrir();

        pagina.FindAll(".situacao .pilula").Select(p => p.TextContent).Should().Equal("Sem pendências");
    }

    [Fact]
    public void Enquanto_as_versoes_nao_chegam_as_instancias_ja_aparecem()
    {
        DevolverFicha(Ficha());
        var fonte = PendurarVersoes();

        var pagina = Abrir();

        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);
        pagina.Find(".bloco-versoes .barra .status").TextContent.Should().Be(@"Lendo endereços em TCMBD\PRD");
        pagina.FindAll("table").Should().BeEmpty();

        fonte.SetResult(Versoes());

        pagina.WaitForAssertion(() => pagina.FindAll("table.tabela-ficha").Should().HaveCount(2));
        pagina.FindAll(".bloco-versoes").Should().BeEmpty();
    }

    [Fact]
    public void Enquanto_a_ficha_nao_chega_mostra_o_carregamento_e_Cancelar_leva_ao_erro()
    {
        PendurarFicha();
        var pagina = Abrir();

        pagina.Find(".barra .status").TextContent.Should().Be("Lendo o catálogo no GitLab…");
        pagina.FindAll("section.ficha-ambiente").Should().BeEmpty();

        _relogio.Advance(TimeSpan.FromSeconds(1));
        pagina.WaitForElement("#cancelar").Click();

        pagina.WaitForAssertion(() => pagina.Find(".estado.erro h2").TextContent.Trim().Should().Be("Leitura do catálogo cancelada"));
        pagina.FindAll(".estado.erro button").Select(b => b.TextContent).Should().Contain("Tentar de novo");
    }

    // ---------- História 2: servidor ----------

    [Fact]
    public void O_seletor_traz_os_servidores_permitidos_com_o_padrao_e_a_faixa()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());

        var pagina = Abrir();

        _mediador.Verify(m => m.Send(It.Is<ListarPermitidos.Query>(q => q.Modulo == Modulo.Versoes_Sistemas), It.IsAny<CancellationToken>()), Times.Once);
        var seletor = pagina.Find("select#servidor");
        seletor.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).Should().Equal("tcmbd3-dev", "tcmbd4-hml", "tcmbd-prd");
        seletor.QuerySelector("option[selected]")!.GetAttribute("value").Should().Be("tcmbd-prd");
        pagina.Find("label[for='servidor']").TextContent.Should().Contain("Servidor dos endereços");
        pagina.Find(".contexto").GetAttribute("data-amb").Should().Be("prd");
        pagina.Find(".contexto").TextContent.Should().Contain(@"TCMBD\PRD").And.Contain("Ficha do sistema");
    }

    [Fact]
    public void Servidor_da_URL_le_esse_servidor()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes(Dev));

        Abrir("sistemas/catalogo/ficha?sistema=SIGARH&servidor=tcmbd3-dev");

        _mediador.Verify(m => m.Send(It.Is<ObterVersoesDaFicha.Query>(q => q.Servidor == "tcmbd3-dev"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Trocar_o_servidor_le_so_as_versoes_e_grava_na_URL()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());
        var pagina = Abrir();
        DevolverVersoes(Versoes(Hml));

        pagina.Find("select#servidor").Change("tcmbd4-hml");

        _mediador.Verify(m => m.Send(It.IsAny<ObterFicha.Query>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediador.Verify(m => m.Send(It.Is<ObterVersoesDaFicha.Query>(q => q.Servidor == "tcmbd4-hml" && !q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        Navegacao.Uri.Should().EndWith("sistemas/catalogo/ficha?sistema=SIGARH&servidor=tcmbd4-hml");
        Navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
        pagina.Find(".contexto").GetAttribute("data-amb").Should().Be("hml");

        DevolverVersoes(Versoes());
        pagina.Find("select#servidor").Change("tcmbd-prd");

        Navegacao.Uri.Should().EndWith("sistemas/catalogo/ficha?sistema=SIGARH");
        _mediador.Verify(m => m.Send(It.IsAny<ObterFicha.Query>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Servidor_fora_da_lista_avisa_e_le_o_padrao()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());

        var pagina = Abrir("sistemas/catalogo/ficha?sistema=SIGARH&servidor=xyz");

        Avisos(pagina).Should().Contain(@"O servidor xyz não está na lista; mostrando TCMBD\PRD.");
        _mediador.Verify(m => m.Send(It.Is<ObterVersoesDaFicha.Query>(q => q.Servidor == Prd.Chave), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Padrao_fora_da_lista_pede_a_escolha_sem_ler_as_versoes()
    {
        _grupos = [new(Ambiente.Dsv, [Dev])];
        DevolverFicha(Ficha());

        var pagina = Abrir();

        pagina.Find(".bloco-versoes .estado h2").TextContent.Should().Be("Escolha o servidor dos endereços");
        _mediador.Verify(m => m.Send(It.IsAny<ObterVersoesDaFicha.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);
    }

    // ---------- História 3: avisos e não encontrado ----------

    [Fact]
    public void Vinculo_invalido_avisa_e_nao_le_as_versoes()
    {
        DevolverFicha(Ficha(Item(versoes: VinculoDeVersoes.ForaDoFormato)));

        var pagina = Abrir();

        Avisos(pagina).Should().Equal("O campo Versoes deste sistema no catálogo não é uma lista de nomes; corrija no sistemasDb.js.");
        VerificarSemVersoes();
        pagina.FindAll("select#servidor").Should().BeEmpty();
    }

    [Fact]
    public void Sem_vinculo_explica_como_vincular_com_o_link_do_sistemasDb()
    {
        DevolverFicha(Ficha(Item(versoes: VinculoDeVersoes.Nenhum)));

        var pagina = Abrir();

        Avisos(pagina).Should().Equal(
            "Este sistema não tem versões vinculadas. Para ver as versões aqui, liste os nomes de Apps_VersionAddresses no campo Versoes do item, no sistemasDb.js (abre em nova aba).");
        pagina.Find(".avisos a").GetAttribute("href").Should().Be(Origem);
        pagina.FindAll("select#servidor").Should().BeEmpty();
        pagina.FindAll("table").Should().BeEmpty();
        VerificarSemVersoes();
    }

    [Fact]
    public void Nome_vinculado_sem_registro_ganha_aviso_proprio_fora_das_pilulas()
    {
        DevolverFicha(Ficha(Item(versoes: VinculoDeVersoes.De(["SIGARH", "<i>Fantasma</i>"]))));
        DevolverVersoes(Versoes(vinculo: ["SIGARH", "<i>Fantasma</i>"]));

        var pagina = Abrir();

        var aviso = pagina.FindAll(".avisos > div").Single();
        aviso.QuerySelector("strong")!.TextContent.Should().Be("Nome vinculado não encontrado");
        Espacos(aviso.TextContent).Should().Be(
            @"Nome vinculado não encontrado<i>Fantasma</i> não está entre os registros ativos de TCMBD\PRD. Confira o campo Versoes do SIGARH no sistemasDb.js.");
        aviso.QuerySelectorAll("i").Should().BeEmpty();
        pagina.FindAll(".situacao .pilula").Select(p => p.TextContent).Should().Equal("Com erro");
    }

    [Fact]
    public void Item_repetido_avisa()
    {
        DevolverFicha(Ficha(mostraVersoes: false, repetidos: 2));

        Avisos(Abrir()).Should().Equal(
            "Há 2 outro(s) item(ns) com o nome SIGARH no catálogo; esta ficha mostra o primeiro. Corrija no sistemasDb.js.");
    }

    [Fact]
    public void Sistema_nao_encontrado_mostra_o_estado_vazio_com_o_link_para_o_Catalogo()
    {
        DevolverFicha(Erros.Catalogo.SistemaNaoEncontrado("Inexistente"));

        var pagina = Abrir("sistemas/catalogo/ficha?sistema=Inexistente");

        pagina.Find(".estado h2").TextContent.Should().Be("Sistema não encontrado no catálogo");
        pagina.Find(".estado p").TextContent.Should().Be("Não há item com o nome Inexistente no catálogo. Ele pode ter sido renomeado ou removido.");
        pagina.Find("#procurar-no-catalogo").GetAttribute("href").Should().Be("sistemas/catalogo?busca=Inexistente");
        pagina.FindAll("section.ficha-ambiente").Should().BeEmpty();
        VerificarSemVersoes();
    }

    [Theory]
    [InlineData("sistemas/catalogo/ficha")]
    [InlineData("sistemas/catalogo/ficha?sistema=%20%20")]
    public void Sem_nome_na_URL_mostra_o_mesmo_estado_sem_ler(string url)
    {
        var pagina = Abrir(url);

        pagina.Find(".estado h2").TextContent.Should().Be("Sistema não encontrado no catálogo");
        _mediador.Verify(m => m.Send(It.IsAny<ObterFicha.Query>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- História 4: leitura, andamento e falhas ----------

    [Fact]
    public void Atualizar_agora_rele_o_catalogo_e_depois_as_versoes()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());
        var pagina = Abrir();

        pagina.Find("#atualizar").Click();

        _mediador.Verify(m => m.Send(It.Is<ObterFicha.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        _mediador.Verify(m => m.Send(It.Is<ObterVersoesDaFicha.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Durante_a_leitura_das_versoes_mostra_os_enderecos_e_depois_a_quantidade_de_arquivos()
    {
        DevolverFicha(Ficha());
        var fonte = PendurarVersoes();
        _mediador.Setup(m => m.Send(
                It.Is<ObterAndamento.Query>(q => q.Servidor == Prd.Chave && q.Vinculo != null && q.Vinculo.SequenceEqual(VinculoPadrao)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<int?, Erro>(4));

        var pagina = Abrir();
        _relogio.Advance(TimeSpan.FromSeconds(1));

        pagina.WaitForAssertion(() => pagina.Find(".bloco-versoes .barra .status").TextContent.Should().Be("Lendo 4 arquivos de versão"));
        pagina.Find("#cancelar").Should().NotBeNull();
        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);

        fonte.SetResult(Versoes());

        pagina.WaitForAssertion(() => pagina.FindAll("table.tabela-ficha").Should().HaveCount(2));
    }

    [Fact]
    public void Cancelar_as_versoes_sem_resultado_anterior_mostra_Tentar_de_novo_com_as_instancias()
    {
        DevolverFicha(Ficha());
        PendurarVersoes();
        var pagina = Abrir();
        _relogio.Advance(TimeSpan.FromSeconds(1));

        pagina.WaitForElement("#cancelar").Click();

        pagina.WaitForAssertion(() => pagina.Find(".bloco-versoes .estado.erro h2").TextContent.Trim().Should().Be("Leitura das versões cancelada"));
        pagina.FindAll(".bloco-versoes button").Select(b => b.TextContent).Should().Contain("Tentar de novo");
        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);
    }

    [Fact]
    public void Cancelar_as_versoes_com_resultado_anterior_volta_a_ele()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());
        var pagina = Abrir();
        PendurarVersoes();

        pagina.Find("select#servidor").Change("tcmbd-prd"); // mesmo servidor: nada muda
        pagina.Find("#atualizar").Click();
        _relogio.Advance(TimeSpan.FromSeconds(1));
        pagina.WaitForElement(".bloco-versoes #cancelar").Click();

        pagina.WaitForAssertion(() => pagina.FindAll("table.tabela-ficha").Should().HaveCount(2));
        pagina.FindAll(".estado.erro").Should().BeEmpty();
    }

    [Fact]
    public void Erro_das_versoes_sem_resultado_anterior_fica_no_bloco_de_versoes()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(new Erro("versoes.x", "O SQL Server recusou a conexão."));

        var pagina = Abrir();

        pagina.Find(".bloco-versoes .estado.erro h2").TextContent.Trim().Should().Be(@"Não foi possível ler as versões em TCMBD\PRD");
        pagina.Find(".bloco-versoes .estado.erro code").TextContent.Should().Be("O SQL Server recusou a conexão.");
        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);

        DevolverVersoes(Versoes());
        pagina.FindAll(".bloco-versoes button").Single(b => b.TextContent == "Tentar de novo").Click();

        pagina.FindAll("table.tabela-ficha").Should().HaveCount(2);
        _mediador.Verify(m => m.Send(It.Is<ObterVersoesDaFicha.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Falha_das_versoes_com_resultado_anterior_mostra_o_anterior_e_o_aviso()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes(falha: new FalhaDeLeitura(new Erro("x", "Tempo esgotado."), GeradoEm)));

        var pagina = Abrir();

        Avisos(pagina).Should().Contain($"Não foi possível atualizar as versões: Tempo esgotado. Dados gerados às {Hora(GeradoEm)}.");
        pagina.FindAll("table.tabela-ficha").Should().HaveCount(2);
    }

    [Fact]
    public void Erro_do_catalogo_sem_leitura_anterior_mostra_o_estado_de_erro()
    {
        DevolverFicha(Erros.Catalogo.NaoConfigurado);
        var pagina = Abrir();

        pagina.Find(".estado.erro code").TextContent.Should().Be("A origem do catálogo não está configurada.");
        pagina.FindAll("section.ficha-ambiente").Should().BeEmpty();

        DevolverFicha(Ficha(mostraVersoes: false));
        pagina.FindAll(".estado.erro button").Single(b => b.TextContent == "Tentar de novo").Click();

        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);
    }

    [Fact]
    public void Falha_do_catalogo_com_leitura_anterior_mostra_a_ficha_e_o_aviso()
    {
        DevolverFicha(Ficha(mostraVersoes: false, falha: new FalhaDeLeitura(new Erro("x", "O GitLab não respondeu."), LidoEm)));

        var pagina = Abrir();

        Avisos(pagina).Should().Equal($"Não foi possível atualizar o catálogo: O GitLab não respondeu. Mostrando os dados lidos às {Hora(LidoEm)}.");
        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);
    }

    [Fact]
    public void Cancelar_a_releitura_do_catalogo_volta_a_ficha_anterior()
    {
        DevolverFicha(Ficha(mostraVersoes: false));
        var pagina = Abrir();
        PendurarFicha();

        pagina.Find("#atualizar").Click();
        _relogio.Advance(TimeSpan.FromSeconds(1));
        pagina.WaitForElement("#cancelar").Click();

        pagina.WaitForAssertion(() => pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4));
        pagina.FindAll(".estado.erro").Should().BeEmpty();
    }

    [Fact]
    public void Catalogo_retirado_durante_a_sessao_mostra_o_acesso_negado()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());
        var pagina = Abrir();
        DevolverFicha(Erros.Catalogo.AcessoNegado);

        pagina.Find("#atualizar").Click();

        pagina.Find(".mono").TextContent.Should().Be("Catalogo_Sistemas.CanSelect");
        pagina.Markup.Should().NotContain("<table").And.NotContain("TCM2016WEB1");
    }

    [Fact]
    public void Catalogo_retirado_na_leitura_das_versoes_mostra_o_acesso_negado()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Erros.Catalogo.AcessoNegado);

        var pagina = Abrir();

        pagina.Find(".mono").TextContent.Should().Be("Catalogo_Sistemas.CanSelect");
        pagina.Markup.Should().NotContain("TCM2016WEB1");
    }

    [Fact]
    public void Versoes_retiradas_durante_a_sessao_tiram_as_versoes_sem_Tentar_de_novo()
    {
        DevolverFicha(Ficha());
        DevolverVersoes(Versoes());
        var pagina = Abrir();
        DevolverFicha(Ficha());
        DevolverVersoes(Erros.Versoes.AcessoNegado);

        pagina.Find("#atualizar").Click();

        pagina.Find(".bloco-versoes .estado.erro h2").TextContent.Trim().Should().Be(@"Não foi possível ler as versões em TCMBD\PRD");
        pagina.FindAll(".bloco-versoes button").Should().BeEmpty();
        pagina.FindAll("table.tabela-ficha").Should().BeEmpty();
        pagina.FindAll("section.ficha-ambiente").Should().HaveCount(4);
    }
}
