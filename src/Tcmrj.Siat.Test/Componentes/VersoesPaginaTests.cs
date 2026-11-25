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
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Web.Services;
using Pagina = Tcmrj.Siat.Web.Components.Pages.Sistemas.Versoes;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Página Sistemas › Versões publicadas (specs/003-versoes-publicadas/contracts/rotas-ui.md): acesso, tabela e detalhe
/// (História 1), resumo, filtros e servidor (História 2), leitura, andamento, cancelamento e erros (História 3).
/// </summary>
public sealed class VersoesPaginaTests : BunitContext
{
    private static readonly DateTimeOffset GeradoEm = new(2026, 11, 24, 13, 5, 0, TimeSpan.Zero);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly ServidorVM Dev = new("tcmbd3-dev", @"TCMBD3\DEV", Ambiente.Dsv);

    private static readonly ServidorVM Hml = new("tcmbd4-hml", @"TCMBD4\HML", Ambiente.Hml);

    private static readonly ServidorVM Prd = new("tcmbd-prd", @"TCMBD\PRD", Ambiente.Prd);

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new();
    private readonly Mock<IMediator> _mediador = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _relogio = new(GeradoEm);
    private IReadOnlyList<GrupoServidoresVM> _grupos =
    [
        new(Ambiente.Dsv, [Dev]),
        new(Ambiente.Hml, [Hml]),
        new(Ambiente.Prd, [Prd]),
    ];

    public VersoesPaginaTests()
    {
        Services.AddSingleton(_autorizacao.Object);
        Services.AddSingleton(_mediador.Object);
        Services.AddSingleton(new Mock<IInteracao>().Object);
        Services.AddSingleton<TimeProvider>(_relogio);
        var opcoes = new Mock<IOptionsMonitor<CacheDeVersoesOptions>>();
        opcoes.Setup(o => o.CurrentValue).Returns(new CacheDeVersoesOptions { ServidorPadrao = Prd.Chave, MinutosDeCache = 5 });
        Services.AddSingleton(opcoes.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        AddAuthorization().SetAuthorized("usuario.teste");

        _mediador.Setup(m => m.Send(It.IsAny<ListarPermitidos.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _grupos);
    }

    private static string Caminho(Ambiente ambiente, Maquina maquina) =>
        $@"\\APP{ambiente.Descricao()}0{maquina.Numero()}\Sites\VERSAO.TXT";

    private static LeituraDeCelula V(Ambiente ambiente, Maquina maquina, string versao, string? raiz = null) =>
        LeituraDeCelula.ComVersao(ambiente, maquina, versao, Caminho(ambiente, maquina), raiz);

    // eTCM: divergência em HML (e raiz na HML-1); eTCM Impressão e SGQ sem pendências; STP com erro na PRD-2.
    private static readonly SistemaVersionado[] Sistemas =
    [
        new(1, "eTCM", [V(Ambiente.Dsv, Maquina.Maquina1, "5.12.0"), V(Ambiente.Hml, Maquina.Maquina1, "5.11.3", @"\\APPHML01\Sites\Raiz\VERSAO.TXT"), V(Ambiente.Hml, Maquina.Maquina2, "5.11.2")]),
        new(2, "eTCM Impressão", [V(Ambiente.Prd, Maquina.Maquina1, "1.0.0")]),
        new(3, "SGQ", [V(Ambiente.Dsv, Maquina.Maquina1, "2.0.3")]),
        new(4, "STP", [V(Ambiente.Prd, Maquina.Maquina1, "6.1.8"),
            LeituraDeCelula.ComErro(Ambiente.Prd, Maquina.Maquina2, "Arquivo não encontrado.", Caminho(Ambiente.Prd, Maquina.Maquina2))]),
    ];

    private static VersoesVM Versoes(ServidorVM? servidor = null, IReadOnlyList<SistemaVersionado>? sistemas = null, FalhaDeLeitura? falha = null)
    {
        servidor ??= Prd;
        var dominio = new Servidor { Chave = servidor.Chave, Nome = servidor.Nome, Endereco = servidor.Nome, Ambiente = servidor.Ambiente };
        var lidas = new VersoesLidas(dominio, GeradoEm, TimeSpan.FromSeconds(1.3), sistemas ?? Sistemas, 8);
        return VersoesVM.De(new VersoesObtidas(lidas, falha), servidor);
    }

    private void Devolver(Result<VersoesVM, Erro> resultado) =>
        _mediador.Setup(m => m.Send(It.IsAny<ObterVersoes.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    // Leitura que só termina quando o teste quiser (ou quando o token for cancelado).
    private TaskCompletionSource<Result<VersoesVM, Erro>> Pendurar()
    {
        var fonte = new TaskCompletionSource<Result<VersoesVM, Erro>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediador.Setup(m => m.Send(It.IsAny<ObterVersoes.Query>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<Result<VersoesVM, Erro>> _, CancellationToken token) => fonte.Task.WaitAsync(token));
        return fonte;
    }

    private IRenderedComponent<Pagina> Abrir(string url = "sistemas/versoes", bool acesso = true)
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(acesso);
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Pagina>();
    }

    private Bunit.TestDoubles.BunitNavigationManager Navegacao => Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>();

    private static string[] Nomes(IRenderedComponent<Pagina> pagina) =>
        pagina.FindAll("tbody tr.linha-expansivel td.nome").Select(td => td.TextContent.Trim()).ToArray();

    // ---------- História 4: acesso ----------

    [Theory]
    [InlineData("sistemas/versoes")]
    [InlineData("sistemas/versoes?servidor=tcmbd4-hml&busca=etcm&filtro=erro")]
    public void Sem_Versoes_Sistemas_CanSelect_mostra_acesso_negado_sem_chamar_feature(string url)
    {
        var pagina = Abrir(url, acesso: false);

        pagina.Find(".mono").TextContent.Should().Be("Versoes_Sistemas.CanSelect");
        _mediador.VerifyNoOtherCalls();
        pagina.Markup.Should().NotContain("eTCM").And.NotContain("VERSAO.TXT").And.NotContain("Arquivo não encontrado").And.NotContain("<table");
    }

    // ---------- História 1: tabela e detalhe ----------

    [Fact]
    public void Com_acesso_le_o_servidor_padrao_e_mostra_a_tabela_status_e_rodape()
    {
        Devolver(Versoes());

        var pagina = Abrir();

        _mediador.Verify(m => m.Send(It.Is<ObterVersoes.Query>(q => q.Servidor == Prd.Chave && !q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        var linhas = pagina.FindAll("thead tr");
        linhas.Should().HaveCount(2);
        var ambientes = linhas[0].QuerySelectorAll("th[colspan='2']");
        ambientes.Select(th => th.TextContent.Trim()).Should().Equal("DSV", "HML", "PRD");
        linhas[0].QuerySelector("th[rowspan='2']")!.TextContent.Should().Be("Sistema");
        linhas[1].QuerySelectorAll("th").Select(th => th.TextContent).Should().Equal(
            "Máquina 1", "Máquina 2", "Máquina 1", "Máquina 2", "Máquina 1", "Máquina 2");

        Nomes(pagina).Should().Equal("eTCM", "eTCM Impressão", "SGQ", "STP");
        pagina.FindAll("tbody tr.linha-expansivel").Should().AllSatisfy(tr => tr.QuerySelectorAll("td").Length.Should().Be(7));

        var expansor = pagina.Find("tbody tr.linha-expansivel .expansor");
        expansor.GetAttribute("aria-expanded").Should().Be("false");
        expansor.GetAttribute("aria-label").Should().Be("Caminhos consultados de eTCM");
        pagina.FindAll(".linha-detalhe").Should().BeEmpty();

        var hora = GeradoEm.ToLocalTime().ToString("HH:mm", PtBr);
        pagina.Find(".cabecalho .status").TextContent.Should().Be($"Gerado às {hora} em 1,3 s");
        var rodape = pagina.Find("footer.rodape").TextContent;
        rodape.Should().Contain(@"TCMBD\PRD").And.Contain("CSLIB_UserManager.dbo.Apps_VersionAddresses").And.Contain("cache por 5 minutos");
        pagina.Find(".contexto").GetAttribute("data-amb").Should().Be("prd");
        pagina.Find(".contexto").TextContent.Should().NotContain("registrada em log");
    }

    [Fact]
    public void Abrir_a_linha_mostra_os_caminhos_consultados_com_copia_e_o_sistema_raiz()
    {
        Devolver(Versoes());
        var pagina = Abrir();

        pagina.Find("tbody tr.linha-expansivel .expansor").Click();

        pagina.Find("tbody tr.linha-expansivel .expansor").GetAttribute("aria-expanded").Should().Be("true");
        var detalhe = pagina.Find("tr.linha-detalhe td[colspan='7'] .caminhos");
        detalhe.QuerySelectorAll("[data-amb] .amb").Select(a => a.TextContent).Should().Equal("DSV", "HML", "HML");
        detalhe.QuerySelectorAll(".suave").Select(s => s.TextContent).Should().Contain("Máquina 1").And.Contain("Máquina 2");
        detalhe.TextContent.Should().Contain(Caminho(Ambiente.Hml, Maquina.Maquina2)).And.NotContain(Caminho(Ambiente.Prd, Maquina.Maquina1));
        detalhe.QuerySelector(".caminho-raiz .mono")!.TextContent.Should().Be(@"\\APPHML01\Sites\Raiz\VERSAO.TXT");
        detalhe.QuerySelectorAll("button.copiar").Select(b => b.GetAttribute("aria-label")).Should().Contain(
            "Copiar caminho de HML, máquina 2, do eTCM");

        pagina.Find("tbody tr.linha-expansivel .expansor").Click();
        pagina.FindAll(".linha-detalhe").Should().BeEmpty();
    }

    [Fact]
    public void O_detalhe_mostra_a_mensagem_de_erro()
    {
        Devolver(Versoes());
        var pagina = Abrir();

        pagina.FindAll("tbody tr.linha-expansivel .expansor").Single(b => b.GetAttribute("aria-label") == "Caminhos consultados de STP").Click();

        pagina.Find(".linha-detalhe .erro-msg").TextContent.Should().Be("Arquivo não encontrado.");
    }

    [Fact]
    public void Caminho_e_mensagem_com_marcacao_aparecem_como_texto()
    {
        var sistema = new SistemaVersionado(9, "X", [LeituraDeCelula.ComErro(Ambiente.Dsv, Maquina.Maquina1, "<b>falha</b>", @"\\m\<b>x</b>")]);
        Devolver(Versoes(sistemas: [sistema]));
        var pagina = Abrir();

        pagina.Find(".expansor").Click();

        pagina.FindAll(".linha-detalhe b").Should().BeEmpty();
        pagina.Find(".linha-detalhe .caminho .mono").TextContent.Should().Be(@"\\m\<b>x</b>");
        pagina.Find(".linha-detalhe .erro-msg").TextContent.Should().Be("<b>falha</b>");
    }

    // ---------- História 2: resumo, filtros e servidor ----------

    [Fact]
    public void Resumo_e_filtros_mostram_as_quantidades()
    {
        Devolver(Versoes());
        var pagina = Abrir();

        var resumo = pagina.FindAll("dl.resumo > div")
            .Select(d => (d.QuerySelector("dt")!.TextContent.Trim(), d.QuerySelector("dd")!.TextContent.Trim()))
            .ToArray();
        resumo.Should().Equal(("Sistemas", "4"), ("Sem pendências", "2"), ("Com divergência", "1"), ("Com erro", "1"));

        var grupo = pagina.Find(".segmentado[role='group']");
        grupo.GetAttribute("aria-label").Should().Be("Filtrar por situação");
        grupo.QuerySelectorAll("button").Select(b => (b.TextContent, b.GetAttribute("aria-pressed"))).Should().Equal(
            ("Todos4", "true"), ("Com divergência1", "false"), ("Com erro1", "false"));
        pagina.Find(".contador").TextContent.Should().Be("4 sistemas");
    }

    [Fact]
    public void Escolher_Com_divergencia_filtra_e_grava_na_URL_sem_novo_historico()
    {
        Devolver(Versoes());
        var pagina = Abrir();

        pagina.FindAll(".segmentado button").Single(b => b.TextContent.StartsWith("Com divergência", StringComparison.Ordinal)).Click();

        Nomes(pagina).Should().Equal("eTCM");
        pagina.FindAll(".segmentado button").Single(b => b.TextContent.StartsWith("Com divergência", StringComparison.Ordinal))
            .GetAttribute("aria-pressed").Should().Be("true");
        Navegacao.Uri.Should().EndWith("sistemas/versoes?filtro=div");
        Navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
    }

    [Theory]
    [InlineData("etcm", new[] { "eTCM", "eTCM Impressão" })]
    [InlineData("ETCM", new[] { "eTCM", "eTCM Impressão" })]
    [InlineData("impressao", new[] { "eTCM Impressão" })]
    public void A_busca_ignora_acentos_e_maiusculas(string termo, string[] esperados)
    {
        Devolver(Versoes());
        var pagina = Abrir();

        pagina.Find("input#busca-versoes").Input(termo);

        Nomes(pagina).Should().Equal(esperados);
        Navegacao.Uri.Should().EndWith($"sistemas/versoes?busca={termo}");
    }

    [Fact]
    public void Busca_e_filtro_da_URL_sao_restaurados_e_combinados()
    {
        Devolver(Versoes());

        var pagina = Abrir("sistemas/versoes?busca=etcm&filtro=div");

        pagina.Find("input#busca-versoes").GetAttribute("value").Should().Be("etcm");
        Nomes(pagina).Should().Equal("eTCM");
    }

    [Fact]
    public void Sem_resultado_mostra_o_estado_vazio_e_Ver_todos_limpa_busca_e_filtro()
    {
        Devolver(Versoes());
        var pagina = Abrir("sistemas/versoes?busca=etcm&filtro=erro");

        pagina.FindAll("table").Should().BeEmpty();
        pagina.Find(".estado h2").TextContent.Should().Be("Nenhum sistema neste filtro");
        pagina.Find(".estado p").TextContent.Should().Be("Não há nenhum sistema com erro com \"etcm\" no nome.");

        pagina.Find("#ver-todos").Click();

        Nomes(pagina).Should().HaveCount(4);
        Navegacao.Uri.Should().EndWith("sistemas/versoes");
        JSInterop.VerifyInvoke("siat.focar").Arguments.Should().Equal("busca-versoes");
    }

    [Fact]
    public void Filtro_desconhecido_na_URL_vira_Todos()
    {
        Devolver(Versoes());

        var pagina = Abrir("sistemas/versoes?filtro=xyz");

        Nomes(pagina).Should().HaveCount(4);
        pagina.Find(".segmentado button[aria-pressed='true']").TextContent.Should().StartWith("Todos");
    }

    [Fact]
    public void O_seletor_traz_os_servidores_permitidos_com_o_padrao_selecionado()
    {
        Devolver(Versoes());

        var pagina = Abrir();

        _mediador.Verify(m => m.Send(It.Is<ListarPermitidos.Query>(q => q.Modulo == Modulo.Versoes_Sistemas), It.IsAny<CancellationToken>()), Times.Once);
        var seletor = pagina.Find("select#servidor");
        seletor.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).Should().Equal("tcmbd3-dev", "tcmbd4-hml", "tcmbd-prd");
        seletor.QuerySelector("option[selected]")!.GetAttribute("value").Should().Be("tcmbd-prd");
    }

    [Fact]
    public void Trocar_de_servidor_le_o_novo_e_grava_na_URL()
    {
        Devolver(Versoes());
        var pagina = Abrir();
        Devolver(Versoes(Hml));

        pagina.Find("select#servidor").Change("tcmbd4-hml");

        _mediador.Verify(m => m.Send(It.Is<ObterVersoes.Query>(q => q.Servidor == "tcmbd4-hml" && !q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        Navegacao.Uri.Should().EndWith("sistemas/versoes?servidor=tcmbd4-hml");
        pagina.Find(".contexto").GetAttribute("data-amb").Should().Be("hml");
        pagina.Find("footer.rodape").TextContent.Should().Contain(@"TCMBD4\HML");
    }

    [Fact]
    public void Servidor_da_URL_le_esse_servidor()
    {
        Devolver(Versoes(Dev));

        Abrir("sistemas/versoes?servidor=tcmbd3-dev");

        _mediador.Verify(m => m.Send(It.Is<ObterVersoes.Query>(q => q.Servidor == "tcmbd3-dev"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Servidor_fora_da_lista_avisa_e_le_o_padrao()
    {
        Devolver(Versoes());

        var pagina = Abrir("sistemas/versoes?servidor=xyz");

        pagina.Find(".aviso").TextContent.Trim().Should().Be(@"O servidor xyz não está na lista; mostrando TCMBD\PRD.");
        _mediador.Verify(m => m.Send(It.Is<ObterVersoes.Query>(q => q.Servidor == Prd.Chave), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Padrao_fora_da_lista_pede_para_escolher_o_servidor_sem_ler()
    {
        _grupos = [new(Ambiente.Dsv, [Dev]), new(Ambiente.Hml, [Hml])];

        var pagina = Abrir();

        pagina.Find(".estado h2").TextContent.Should().Be("Escolha o servidor dos endereços");
        _mediador.Verify(m => m.Send(It.IsAny<ObterVersoes.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        pagina.Find("#atualizar").HasAttribute("disabled").Should().BeTrue();
    }

    // ---------- História 3: leitura, andamento, cancelamento e erros ----------

    [Fact]
    public void Atualizar_agora_pede_a_releitura_forcada()
    {
        Devolver(Versoes());
        var pagina = Abrir();

        pagina.Find("#atualizar").Click();

        _mediador.Verify(m => m.Send(It.Is<ObterVersoes.Query>(q => !q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        _mediador.Verify(m => m.Send(It.Is<ObterVersoes.Query>(q => q.Atualizar && q.Servidor == Prd.Chave), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Durante_a_leitura_mostra_os_enderecos_e_depois_a_quantidade_de_arquivos()
    {
        var fonte = Pendurar();
        _mediador.Setup(m => m.Send(It.Is<ObterAndamento.Query>(q => q.Servidor == Prd.Chave), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<int?, Erro>(26));

        var pagina = Abrir();

        pagina.Find(".painel .barra .status").TextContent.Should().Be(@"Lendo endereços em TCMBD\PRD");

        _relogio.Advance(TimeSpan.FromSeconds(1));

        pagina.WaitForAssertion(() => pagina.Find(".painel .barra .status").TextContent.Should().Be("Lendo 26 arquivos de versão"));
        pagina.Find("#cancelar").Should().NotBeNull();

        fonte.SetResult(Versoes());

        pagina.WaitForAssertion(() => Nomes(pagina).Should().HaveCount(4));
    }

    [Fact]
    public void Cancelar_sem_resultado_anterior_mostra_Tentar_de_novo()
    {
        Pendurar();
        _mediador.Setup(m => m.Send(It.IsAny<ObterAndamento.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success<int?, Erro>(null));
        var pagina = Abrir();
        _relogio.Advance(TimeSpan.FromSeconds(1));

        pagina.WaitForElement("#cancelar").Click();

        pagina.WaitForAssertion(() => pagina.Find(".estado.erro h2").TextContent.Trim().Should().Be("Leitura das versões cancelada"));
        pagina.FindAll(".estado.erro button").Select(b => b.TextContent).Should().Contain("Tentar de novo");
    }

    [Fact]
    public void Cancelar_com_resultado_anterior_volta_a_ele()
    {
        Devolver(Versoes());
        var pagina = Abrir();
        Pendurar();
        _mediador.Setup(m => m.Send(It.IsAny<ObterAndamento.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success<int?, Erro>(null));

        pagina.Find("#atualizar").Click();
        _relogio.Advance(TimeSpan.FromSeconds(1));
        pagina.WaitForElement("#cancelar").Click();

        pagina.WaitForAssertion(() => Nomes(pagina).Should().HaveCount(4));
        pagina.FindAll(".estado.erro").Should().BeEmpty();
    }

    [Fact]
    public void Erro_sem_resultado_mostra_o_estado_de_erro_e_Tentar_de_novo_forca_a_releitura()
    {
        Devolver(Erros.Versoes.TabelaIndisponivel(@"TCMBD\PRD", Erros.Versoes.MotivoSemPermissao));
        var pagina = Abrir();

        pagina.Find(".estado.erro h2").TextContent.Trim().Should().Be(@"Não foi possível ler as versões em TCMBD\PRD");
        pagina.Find(".estado.erro code").TextContent.Should().Contain("sem permissão para a conta do SIAT");

        Devolver(Versoes());
        pagina.FindAll(".estado.erro button").Single(b => b.TextContent == "Tentar de novo").Click();

        Nomes(pagina).Should().HaveCount(4);
        _mediador.Verify(m => m.Send(It.Is<ObterVersoes.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Falha_da_releitura_aparece_num_aviso_com_o_horario_dos_dados()
    {
        Devolver(Versoes(falha: new FalhaDeLeitura(Erros.Versoes.TempoEsgotado(@"TCMBD\PRD"), GeradoEm.AddMinutes(7))));

        var pagina = Abrir();

        var hora = GeradoEm.ToLocalTime().ToString("HH:mm", PtBr);
        pagina.Find(".aviso").TextContent.Trim().Should().Be(
            $@"Não foi possível atualizar as versões: A leitura dos endereços de versão em TCMBD\PRD excedeu o tempo limite. Dados gerados às {hora}.");
        Nomes(pagina).Should().HaveCount(4);
    }

    [Fact]
    public void Sem_sistemas_ativos_mostra_o_estado_vazio_do_servidor()
    {
        Devolver(Versoes(sistemas: []));

        var pagina = Abrir();

        pagina.Find(".estado h2").TextContent.Should().Be(@"Nenhum sistema cadastrado em TCMBD\PRD");
        pagina.Find(".estado p").TextContent.Should().Contain("Apps_VersionAddresses");
        pagina.FindAll("dl.resumo").Should().BeEmpty();
    }

    [Fact]
    public void Acesso_retirado_durante_o_uso_mostra_acesso_negado()
    {
        Devolver(Erros.Versoes.AcessoNegado);

        var pagina = Abrir();

        pagina.Find(".mono").TextContent.Should().Be("Versoes_Sistemas.CanSelect");
    }

    // ---------- Fichas (specs/004-ficha-sistema, História 2 e História 5; T037, T053) ----------

    // eTCM e eTCM Impressão vão para a ficha do eTCM, SGQ para a dele; STP fica sem ficha.
    private static readonly FichasVM Mapa = new(new Dictionary<string, string>
    {
        ["etcm"] = "eTCM",
        ["etcm impressao"] = "eTCM",
        ["sgq"] = "SGQ",
    });

    private void ComCatalogo(Result<FichasVM, Erro> fichas)
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(true);
        _mediador.Setup(m => m.Send(It.IsAny<ListarFichas.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(fichas);
    }

    private TaskCompletionSource<Result<FichasVM, Erro>> ComCatalogoPendente()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)).ReturnsAsync(true);
        var fonte = new TaskCompletionSource<Result<FichasVM, Erro>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediador.Setup(m => m.Send(It.IsAny<ListarFichas.Query>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<Result<FichasVM, Erro>> _, CancellationToken token) => fonte.Task.WaitAsync(token));
        return fonte;
    }

    // Nome como se vê, sem o "Ficha do " só para leitor de tela.
    private static string[] NomesVisiveis(IRenderedComponent<Pagina> pagina) =>
        pagina.FindAll("tbody tr.linha-expansivel td.nome").Select(td =>
        {
            var copia = (AngleSharp.Dom.IElement)td.Clone();
            foreach (var oculto in copia.QuerySelectorAll(".so-leitor").ToArray())
            {
                oculto.Remove();
            }

            return copia.TextContent.Trim();
        }).ToArray();

    private static string[] Filtros(IRenderedComponent<Pagina> pagina) =>
        pagina.FindAll(".segmentado button").Select(b => b.GetAttribute("data-filtro")!).ToArray();

    [Fact]
    public void Com_o_catalogo_a_tabela_nao_espera_as_fichas_e_os_links_aparecem_quando_chegam()
    {
        Devolver(Versoes());
        var fonte = ComCatalogoPendente();

        var pagina = Abrir();

        _mediador.Verify(m => m.Send(It.IsAny<ListarFichas.Query>(), It.IsAny<CancellationToken>()), Times.Once);
        Nomes(pagina).Should().Equal("eTCM", "eTCM Impressão", "SGQ", "STP");
        pagina.FindAll("td.nome a").Should().BeEmpty();
        Filtros(pagina).Should().NotContain("SemFicha");

        fonte.SetResult(Mapa);

        pagina.WaitForAssertion(() => pagina.FindAll("td.nome a").Should().HaveCount(3));
    }

    [Fact]
    public void Nome_no_mapa_vira_link_para_a_ficha_e_fora_do_mapa_fica_texto()
    {
        Devolver(Versoes());
        ComCatalogo(Mapa);

        var pagina = Abrir();

        var links = pagina.FindAll("td.nome a");
        links.Select(a => a.GetAttribute("href")).Should().Equal(
            "sistemas/catalogo/ficha?sistema=eTCM", "sistemas/catalogo/ficha?sistema=eTCM", "sistemas/catalogo/ficha?sistema=SGQ");
        links[1].QuerySelector(".so-leitor")!.TextContent.Should().Be("Ficha do ");
        links[1].TextContent.Should().Be("Ficha do eTCM Impressão");
        NomesVisiveis(pagina).Should().Equal("eTCM", "eTCM Impressão", "SGQ", "STP");
        pagina.FindAll("tbody tr.linha-expansivel")[3].QuerySelectorAll("td.nome a").Should().BeEmpty();
    }

    [Fact]
    public void O_link_leva_o_servidor_escolhido_quando_nao_e_o_padrao()
    {
        Devolver(Versoes(Dev));
        ComCatalogo(Mapa);

        var pagina = Abrir("sistemas/versoes?servidor=tcmbd3-dev");

        pagina.Find("td.nome a").GetAttribute("href").Should().Be("sistemas/catalogo/ficha?sistema=eTCM&servidor=tcmbd3-dev");
    }

    [Fact]
    public void Clicar_no_link_nao_abre_a_linha()
    {
        Devolver(Versoes());
        ComCatalogo(Mapa);
        var pagina = Abrir();

        // O clique no link não sobe até a linha (no navegador, a página navega para a ficha sem abrir a linha).
        var link = pagina.Find("td.nome a");
        link.Attributes.Select(a => a.Name).Should().Contain(n => n.EndsWith("onclick:stoppropagation", StringComparison.OrdinalIgnoreCase));
        pagina.FindAll(".linha-detalhe").Should().BeEmpty();
    }

    [Fact]
    public void Sem_ficha_vem_depois_de_Com_erro_com_a_quantidade_e_filtra()
    {
        Devolver(Versoes());
        ComCatalogo(Mapa);
        var pagina = Abrir();

        Filtros(pagina).Should().Equal("Todos", "ComDivergencia", "ComErro", "SemFicha");
        var semFicha = pagina.Find("button[data-filtro='SemFicha']");
        semFicha.TextContent.Should().Be("Sem ficha1");
        semFicha.GetAttribute("aria-pressed").Should().Be("false");

        semFicha.Click();

        Nomes(pagina).Should().Equal("STP");
        pagina.Find("button[data-filtro='SemFicha']").GetAttribute("aria-pressed").Should().Be("true");
        Navegacao.Uri.Should().EndWith("sistemas/versoes?filtro=semficha");
        Navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
    }

    [Fact]
    public void Sem_ficha_combina_com_a_busca_e_tem_estados_vazios_proprios()
    {
        Devolver(Versoes());
        ComCatalogo(Mapa);

        var pagina = Abrir("sistemas/versoes?busca=etcm&filtro=semficha");

        pagina.Find(".estado p").TextContent.Should().Be("Não há nenhum sistema sem ficha com \"etcm\" no nome.");
    }

    [Fact]
    public void Sem_ficha_sem_nenhum_sistema_fora_do_mapa()
    {
        Devolver(Versoes());
        ComCatalogo(new FichasVM(new Dictionary<string, string>
        {
            ["etcm"] = "eTCM", ["etcm impressao"] = "eTCM", ["sgq"] = "SGQ", ["stp"] = "STP",
        }));

        var pagina = Abrir("sistemas/versoes?filtro=semficha");

        pagina.Find(".estado p").TextContent.Should().Be("Não há nenhum sistema sem ficha nesta leitura.");
    }

    [Fact]
    public void Filtro_sem_ficha_da_URL_espera_o_mapa_chegar()
    {
        Devolver(Versoes());
        var fonte = ComCatalogoPendente();

        var pagina = Abrir("sistemas/versoes?filtro=semficha");

        Nomes(pagina).Should().HaveCount(4);
        pagina.Find("button[data-filtro='Todos']").GetAttribute("aria-pressed").Should().Be("true");

        fonte.SetResult(Mapa);

        pagina.WaitForAssertion(() => Nomes(pagina).Should().Equal("STP"));
        pagina.Find("button[data-filtro='SemFicha']").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void Falha_das_fichas_avisa_e_deixa_os_nomes_sem_link_e_sem_o_filtro()
    {
        Devolver(Versoes());
        ComCatalogo(new Erro("catalogo.x", "O GitLab não respondeu."));

        var pagina = Abrir("sistemas/versoes?filtro=semficha");

        pagina.FindAll(".aviso").Select(a => a.TextContent.Trim()).Should().Contain("As fichas dos sistemas estão indisponíveis: O GitLab não respondeu.");
        pagina.FindAll("td.nome a").Should().BeEmpty();
        Filtros(pagina).Should().NotContain("SemFicha");
        Nomes(pagina).Should().HaveCount(4);
        pagina.Find("button[data-filtro='Todos']").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void So_com_as_versoes_nao_pede_as_fichas_e_fica_em_Todos_sem_aviso()
    {
        Devolver(Versoes());

        var pagina = Abrir("sistemas/versoes?filtro=semficha");

        _mediador.Verify(m => m.Send(It.IsAny<ListarFichas.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        pagina.FindAll("td.nome a").Should().BeEmpty();
        Filtros(pagina).Should().NotContain("SemFicha");
        Nomes(pagina).Should().HaveCount(4);
        pagina.Find("button[data-filtro='Todos']").GetAttribute("aria-pressed").Should().Be("true");
        pagina.FindAll(".aviso").Should().BeEmpty();
    }
}
