using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web;
using Tcmrj.Siat.App.Web.Features.Copilot;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Test.Unidade.Copilot;
using Tcmrj.Siat.Web.Components.Shared;
using Tcmrj.Siat.Web.Services;
using Pagina = Tcmrj.Siat.Web.Components.Pages.Copilot.MeuConsumo;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Página Copilot › Meu consumo (specs/005-copilot-meu-consumo/contracts/rotas-ui.md): conexão (T027), dados
/// (T039), período (T047) e leitura, cancelamento e erros (T051).
/// </summary>
public sealed class MeuConsumoPaginaTests : BunitContext
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 13, 5, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 11, 27);
    private static readonly ConexaoGitHub Ana = new(7, "ana-m");

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new();
    private readonly Mock<IConexaoGitHubService> _conexoes = new();
    private readonly Mock<IMediator> _mediador = new();
    private readonly FakeTimeProvider _relogio = new(Agora);
    private CopilotOptions _opcoes = CopilotOptionsTests.Validas();

    private Func<Result<PessoaCopilotVM, Erro>> _pessoa = () => Pessoa();
    private Func<ObterConsumoDoPeriodo.Query, CancellationToken, Task<Result<ConsumoDoPeriodoVM, Erro>>> _consumo =
        (q, _) => Task.FromResult(Result.Success<ConsumoDoPeriodoVM, Erro>(Consumo(q)));
    private Func<Result<UsoDoLimiteVM, Erro>> _uso = () => UsoDoLimiteVM.De(UsoDoLimite.Calcular(40, new LimiteVigente.Individual(50), "2026-11"), 40);

    public MeuConsumoPaginaTests()
    {
        Services.AddSingleton(_autorizacao.Object);
        Services.AddSingleton(_conexoes.Object);
        Services.AddSingleton(_mediador.Object);
        Services.AddSingleton(new Mock<IInteracao>().Object);
        Services.AddSingleton<TimeProvider>(_relogio);
        var monitor = new Mock<IOptionsMonitor<CopilotOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(() => _opcoes);
        Services.AddSingleton(monitor.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        AddAuthorization().SetAuthorized("usuario.teste");

        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.CopilotProprio)).ReturnsAsync(true);
        _conexoes.Setup(c => c.ObterAsync()).ReturnsAsync(Ana);
        _mediador.Setup(m => m.Send(It.IsAny<ObterPessoaCopilot.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _pessoa());
        _mediador.Setup(m => m.Send(It.IsAny<ObterConsumoDoPeriodo.Query>(), It.IsAny<CancellationToken>()))
            .Returns((ObterConsumoDoPeriodo.Query q, CancellationToken ct) => _consumo(q, ct));
        _mediador.Setup(m => m.Send(It.IsAny<ObterUsoDoLimite.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _uso());
    }

    // ---------- Dados ----------

    private static PessoaCopilotVM Pessoa(
        string? nome = "Ana Souza",
        string? foto = "https://avatars.githubusercontent.com/u/7",
        bool perfil = true,
        bool licenca = true,
        IReadOnlyList<TimeVM>? times = null,
        bool semTimes = false,
        LimiteVigente? limite = null) => new(
        7, "ana-m", nome, foto, perfil, licenca,
        semTimes ? null : times ?? [new TimeVM("Dados", PessoaCopilotVM.UrlDoTime("tcmrj", "dados"), true), new TimeVM("Plataforma", PessoaCopilotVM.UrlDoTime("tcmrj", "plataforma"), false)],
        semTimes ? "O GitHub não respondeu. Tente de novo." : null,
        limite ?? new LimiteVigente.Individual(50),
        null,
        "tcmrj");

    private static ConsumoDoPeriodoVM Consumo(ObterConsumoDoPeriodo.Query q, decimal liquidoDoDia = 0.5m)
    {
        var periodo = Periodo.Validar(q.De, q.Ate, Hoje).Value;
        var dias = periodo.Dias.Select(d => new DiaDeConsumo(d, new RelatorioDeConsumo(
            new Consumo(25, 1, liquidoDoDia), [new ConsumoPorModelo("GPT-5", new Consumo(25, 1, liquidoDoDia))], Agora))).ToList();
        return ConsumoDoPeriodoVM.De(ConsumoDoPeriodo.De(periodo, dias, Agora));
    }

    private void TudoComErro(Erro erro)
    {
        _pessoa = () => erro;
        _consumo = (_, _) => Task.FromResult(Result.Failure<ConsumoDoPeriodoVM, Erro>(erro));
        _uso = () => erro;
    }

    private BunitNavigationManager Navegacao => Services.GetRequiredService<BunitNavigationManager>();

    private IRenderedComponent<Pagina> Abrir(string url, string? login = null)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Pagina>(p => p.Add(c => c.Login, login));
    }

    private IRenderedComponent<Pagina> AbrirAna(string consulta = "") => Abrir("copilot/meu-consumo/ana-m" + consulta, "ana-m");

    private void NenhumaConsulta() => _mediador.VerifyNoOtherCalls();

    // ---------- Conexão (T027) ----------

    [Fact]
    public void Sem_conexao_mostra_a_explicacao_e_o_link_de_conexao_sem_consultar()
    {
        _conexoes.Setup(c => c.ObterAsync()).ReturnsAsync((ConexaoGitHub?)null);

        var pagina = Abrir("copilot/meu-consumo");

        pagina.Find("#titulo-conectar").TextContent.Should().Be("Conecte sua conta do GitHub");
        var link = pagina.Find("#conectar-github");
        link.TextContent.Should().Contain("Conectar ao GitHub");
        link.GetAttribute("href").Should().StartWith("copilot/github/conectar?retorno=");
        link.GetAttribute("target").Should().Be("_top");
        NenhumaConsulta();
    }

    public static TheoryData<string, string> MensagensDeConexao => new()
    {
        { "nao-membro&conta=ana-m", "A conta ana-m do GitHub não é membro ativo da organização tcmrj." },
        { "negada", "A conexão com o GitHub não foi feita." },
        { "falha", "Não foi possível conectar a conta do GitHub." },
        { "outro-usuario", "A conexão foi iniciada por outro usuário do SIAT" },
        { "desconectada", "A conta do GitHub foi desconectada deste navegador." },
        { "nao-membro-desconectada", "A conta do GitHub conectada não é mais membro ativo da organização tcmrj, e a ligação foi desfeita." },
    };

    [Theory]
    [MemberData(nameof(MensagensDeConexao))]
    public void Mensagem_de_conexao_aparece_e_o_parametro_sai_do_endereco(string parametro, string mensagem)
    {
        _conexoes.Setup(c => c.ObterAsync()).ReturnsAsync((ConexaoGitHub?)null);

        var pagina = Abrir("copilot/meu-consumo?conexao=" + parametro);

        pagina.Find(".avisos").TextContent.Should().Contain(mensagem);
        Navegacao.Uri.Should().EndWith("/copilot/meu-consumo");
        Navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
    }

    [Fact]
    public void Conta_invalida_na_mensagem_de_nao_membro_nao_e_exibida()
    {
        _conexoes.Setup(c => c.ObterAsync()).ReturnsAsync((ConexaoGitHub?)null);

        var pagina = Abrir("copilot/meu-consumo?conexao=nao-membro&conta=%3Cb%3Ex%3C%2Fb%3E");

        pagina.Find(".avisos").TextContent.Should().Contain("A conta do GitHub não é membro ativo da organização tcmrj.").And.NotContain("<b>");
    }

    [Fact]
    public void Com_conexao_copilot_navega_para_a_propria_pessoa_mantendo_o_periodo()
    {
        Abrir("copilot/meu-consumo?de=2026-10-01&ate=2026-10-31");

        Navegacao.Uri.Should().EndWith("copilot/meu-consumo/ana-m?de=2026-10-01&ate=2026-10-31");
        Navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
        NenhumaConsulta();
    }

    [Fact]
    public void Desconectar_navega_com_forceLoad()
    {
        var pagina = AbrirAna();

        pagina.Find("#desconectar").Click();

        Navegacao.Uri.Should().Contain("copilot/github/desconectar");
        Navegacao.History.First().Options.ForceLoad.Should().BeTrue();
    }

    [Fact]
    public void Sem_a_politica_e_acesso_negado_com_o_recurso_sem_ler_a_conexao()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.CopilotProprio)).ReturnsAsync(false);

        var pagina = AbrirAna();

        pagina.Markup.Should().Contain("CopilotProprio.CanSelect");
        _conexoes.Verify(c => c.ObterAsync(), Times.Never);
        NenhumaConsulta();
    }

    // ---------- Dados (T039) ----------

    [Fact]
    public void Mostra_a_pessoa_licenca_times_resumo_uso_e_nota()
    {
        var pagina = AbrirAna();

        pagina.Find("h1").TextContent.Should().Contain("Ana Souza");
        pagina.Find("img.pessoa-foto").GetAttribute("src").Should().Be("https://avatars.githubusercontent.com/u/7");
        pagina.Find(".pilula.ok").TextContent.Should().Be("Licença Copilot ativa");

        var times = pagina.FindAll("a.time");
        times.Should().HaveCount(2);
        times[0].GetAttribute("href").Should().Be("https://github.com/orgs/tcmrj/teams/dados");
        times[0].GetAttribute("target").Should().Be("_blank");
        times[0].TextContent.Should().Contain("via sub-time").And.Contain("(abre em nova aba)");
        times[1].TextContent.Should().NotContain("via sub-time");

        var resumo = pagina.Find(".resumo").TextContent;
        resumo.Should().Contain("Consumo bruto").And.Contain("US$ 27.00").And.Contain("675 créditos").And.Contain("01/11/2026 a 27/11/2026");
        resumo.Should().Contain("US$ 13.50").And.Contain("Não inclui o preço das licenças");
        resumo.Should().Contain("US$ 50.00").And.Contain("Exceção individual");
        pagina.Find(".barra-uso .nota").TextContent.Should().Be("US$ 10.00 abaixo do limite no relatório · acumulado de novembro de 2026");

        pagina.FindAll(".evolucao button.dia").Should().HaveCount(27);
        pagina.Find(".tabela-modelos").TextContent.Should().Contain("GPT-5");
        pagina.Find("footer.rodape").TextContent.Should().Contain("refletem a situação atual").And.Contain("atraso").And.Contain("13:05 UTC de 27/11/2026");
    }

    [Fact]
    public void Valor_liquido_zero_com_consumo_explica_o_desconto()
    {
        _consumo = (q, _) => Task.FromResult(Result.Success<ConsumoDoPeriodoVM, Erro>(Consumo(q, liquidoDoDia: 0)));

        var pagina = AbrirAna();

        pagina.Find(".resumo").TextContent.Should().Contain("US$ 0.00")
            .And.Contain("Consumo todo descontado pelo GitHub · não inclui o preço das licenças");
        pagina.Find(".painel-nota").TextContent.Should().Be(TabelaDeModelos.NotaLiquidoZero);
    }

    [Theory]
    [InlineData("padrao", "Padrão da organização")]
    [InlineData("ambiguo", "Múltiplos budgets")]
    [InlineData("nenhum", "Sem limite configurado")]
    public void Origem_do_limite(string caso, string texto)
    {
        LimiteVigente limite = caso switch
        {
            "padrao" => new LimiteVigente.Padrao(50),
            "ambiguo" => new LimiteVigente.Ambiguo(),
            _ => new LimiteVigente.Nenhum(),
        };
        _pessoa = () => Pessoa(limite: limite);

        AbrirAna().Find(".resumo").TextContent.Should().Contain(texto);
    }

    [Fact]
    public void Sem_nome_usa_o_login_e_sem_licenca_sem_time_visivel()
    {
        _pessoa = () => Pessoa(nome: null, foto: null, licenca: false, times: []);

        var pagina = AbrirAna();

        pagina.Find("h1").TextContent.Should().Contain("ana-m");
        pagina.FindAll("img.pessoa-foto").Should().BeEmpty();
        pagina.Markup.Should().Contain("Sem licença Copilot").And.Contain("Sem time visível");
    }

    [Fact]
    public void Times_com_falha_mostram_times_indisponiveis()
    {
        _pessoa = () => Pessoa(semTimes: true);

        AbrirAna().Markup.Should().Contain("Times indisponíveis");
    }

    [Fact]
    public void Sem_licenca_nem_limite_mostra_so_a_mensagem()
    {
        TudoComErro(Erros.Copilot.SemLicencaNemLimite("tcmrj"));

        var pagina = AbrirAna();

        pagina.Markup.Should().Contain("Sua conta não tem licença Copilot nem limite individual na organização tcmrj.");
        pagina.FindAll(".resumo").Should().BeEmpty();
        pagina.FindAll(".evolucao").Should().BeEmpty();
        pagina.FindAll(".tabela-modelos").Should().BeEmpty();
    }

    [Fact]
    public void Perfil_indisponivel_mostra_o_login_sem_foto_e_o_aviso()
    {
        _pessoa = () => Pessoa(nome: null, foto: null, perfil: false);

        var pagina = AbrirAna();

        pagina.Find(".login").TextContent.Should().Be("ana-m");
        pagina.FindAll("img.pessoa-foto").Should().BeEmpty();
        pagina.Markup.Should().Contain("Perfil do GitHub indisponível.");
        pagina.FindAll(".resumo").Should().ContainSingle();
    }

    [Fact]
    public void Falha_so_do_consumo_mostra_indisponivel_e_mantem_a_pessoa_sem_zero()
    {
        _consumo = (_, _) => Task.FromResult(Result.Failure<ConsumoDoPeriodoVM, Erro>(Erros.Copilot.GitHubIndisponivel));

        var pagina = AbrirAna();

        pagina.Find(".login").TextContent.Should().Be("ana-m");
        pagina.FindAll(".resumo dd")[0].TextContent.Trim().Should().Be("Indisponível");
        pagina.Markup.Should().NotContain("US$ 0.00");
        pagina.Find(".avisos").TextContent.Should().Contain("Consumo do período indisponível");
    }

    [Fact]
    public void Nao_membro_desfaz_a_ligacao_com_forceLoad()
    {
        TudoComErro(Erros.Copilot.NaoMembro("ana-m", "tcmrj"));

        AbrirAna();

        Navegacao.Uri.Should().Contain("copilot/github/desconectar?motivo=nao-membro");
        Navegacao.History.First().Options.ForceLoad.Should().BeTrue();
    }

    [Fact]
    public void Texto_do_GitHub_nao_vira_marcacao()
    {
        _pessoa = () => Pessoa(nome: "<b>Ana</b>", times: [new TimeVM("<i>Time</i>", PessoaCopilotVM.UrlDoTime("tcmrj", "t"), false)]);

        var pagina = AbrirAna();

        pagina.FindAll("h1 b, a.time i").Should().BeEmpty();
        pagina.Markup.Should().Contain("&lt;b&gt;Ana&lt;/b&gt;");
    }

    // ---------- Período (T047) ----------

    [Fact]
    public void Periodo_anterior_consulta_esse_periodo_sem_pedir_o_uso_do_limite()
    {
        var pagina = AbrirAna("?de=2026-10-01&ate=2026-10-31");

        _mediador.Verify(m => m.Send(It.Is<ObterConsumoDoPeriodo.Query>(q => q.De == "2026-10-01" && q.Ate == "2026-10-31"), It.IsAny<CancellationToken>()));
        _mediador.Verify(m => m.Send(It.IsAny<ObterUsoDoLimite.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        pagina.Find(".resumo").TextContent.Should().Contain("Disponível em períodos que incluem o mês atual");
    }

    [Fact]
    public void Aplicar_periodo_troca_de_e_ate_no_endereco_e_o_padrao_sai_do_endereco()
    {
        var pagina = AbrirAna();

        pagina.Find("#periodo-de").Change("2026-10-01");
        pagina.Find("#periodo-ate").Change("2026-10-31");
        pagina.Find("form.filtro-periodo").Submit();

        Navegacao.Uri.Should().EndWith("copilot/meu-consumo/ana-m?de=2026-10-01&ate=2026-10-31");
        Navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
        Navegacao.History.First().Options.ForceLoad.Should().BeFalse();

        pagina.FindAll(".atalhos button").Single(b => b.TextContent == "Mês atual").Click();

        Navegacao.Uri.Should().EndWith("copilot/meu-consumo/ana-m");
    }

    [Theory]
    [InlineData("?de=2026-13-01")]
    [InlineData("?de=2026-11-20&ate=2026-11-10")]
    [InlineData("?ate=2026-11-28")]
    public void Periodo_invalido_no_endereco_mostra_a_mensagem_sem_consultar(string consulta)
    {
        var pagina = AbrirAna(consulta);

        pagina.Find(".avisos").TextContent.Should().Contain("Período inválido").And.Contain(Erros.Copilot.PeriodoInvalido.Message);
        NenhumaConsulta();
    }

    [Fact]
    public void Acionar_um_dia_da_evolucao_troca_o_periodo_para_ele()
    {
        var pagina = AbrirAna();

        pagina.FindAll(".evolucao button.dia")[9].Click();

        Navegacao.Uri.Should().EndWith("copilot/meu-consumo/ana-m?de=2026-11-10&ate=2026-11-10");
    }

    [Theory]
    [InlineData("outra-pessoa", "outra-pessoa")]
    [InlineData("%3Cscript%3E", "<script>")]
    [InlineData("a..b", "a..b")]
    public void Outro_login_navega_para_a_propria_pessoa_com_o_mesmo_periodo_sem_consultar(string noEndereco, string login)
    {
        var pagina = Abrir($"copilot/meu-consumo/{noEndereco}?de=2026-10-01&ate=2026-10-31", login);

        Navegacao.Uri.Should().EndWith("copilot/meu-consumo/ana-m?de=2026-10-01&ate=2026-10-31");
        pagina.Markup.Should().NotContain(login).And.NotContain("&lt;script");
        NenhumaConsulta();
    }

    [Fact]
    public void Login_com_outra_caixa_nao_navega()
    {
        Abrir("copilot/meu-consumo/ANA-M", "ANA-M");

        Navegacao.History.Should().ContainSingle("só a navegação de abertura do teste");
        _mediador.Verify(m => m.Send(It.IsAny<ObterPessoaCopilot.Query>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Leitura, cancelamento e erros (T051) ----------

    [Fact]
    public void Durante_a_leitura_mostra_o_andamento_e_Cancelar_volta_a_Tentar_de_novo_sem_erro()
    {
        IProgress<int>? progresso = null;
        var pendente = new TaskCompletionSource<Result<ConsumoDoPeriodoVM, Erro>>();
        _consumo = (q, ct) =>
        {
            progresso = q.Progresso;
            return pendente.Task.WaitAsync(ct);
        };

        var pagina = AbrirAna();
        progresso!.Report(3);

        pagina.WaitForAssertion(() => pagina.Find("[role=status]").TextContent.Should().Be("Lendo 3 de 27 dias"));
        _relogio.Advance(TimeSpan.FromSeconds(2));
        pagina.WaitForAssertion(() => pagina.Find("#cancelar"));

        pagina.Find("#cancelar").Click();

        pagina.WaitForAssertion(() =>
        {
            pagina.FindAll("[role=status]").Should().BeEmpty();
            pagina.Markup.Should().Contain("Leitura cancelada").And.Contain("Tentar de novo");
            pagina.Markup.Should().NotContain("Não foi possível ler");
        });
    }

    [Fact]
    public void Atualizar_agora_envia_as_tres_consultas_com_Atualizar_e_mostra_o_novo_horario()
    {
        var pagina = AbrirAna();
        _relogio.SetUtcNow(Agora.AddMinutes(20));
        _consumo = (q, _) => Task.FromResult(Result.Success<ConsumoDoPeriodoVM, Erro>(Consumo(q) with { ConsultadoEm = Agora.AddMinutes(20) }));

        pagina.Find("#atualizar").Click();

        pagina.WaitForAssertion(() => pagina.Find("footer.rodape").TextContent.Should().Contain("13:25 UTC de 27/11/2026"));
        _mediador.Verify(m => m.Send(It.Is<ObterPessoaCopilot.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        _mediador.Verify(m => m.Send(It.Is<ObterConsumoDoPeriodo.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
        _mediador.Verify(m => m.Send(It.Is<ObterUsoDoLimite.Query>(q => q.Atualizar), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Limite_de_requisicoes_mostra_a_mensagem_e_o_tempo()
    {
        TudoComErro(Erros.Copilot.LimiteDeRequisicoes(Agora.AddMinutes(5), Agora));

        var pagina = AbrirAna();

        pagina.Markup.Should().Contain("Limite de chamadas do GitHub atingido").And.Contain("Tente de novo em 5 minutos").And.Contain("Tentar de novo");
        pagina.Markup.Should().NotContain("US$ 0.00");
    }

    [Fact]
    public void Modulo_nao_configurado_mostra_so_a_mensagem()
    {
        _opcoes = new CopilotOptions();

        var pagina = AbrirAna();

        pagina.Markup.Should().Contain("O módulo Copilot não está configurado no servidor.");
        pagina.FindAll("#conectar-github, .resumo").Should().BeEmpty();
        NenhumaConsulta();
    }

    [Fact]
    public void Nao_configurado_vindo_da_consulta_mostra_a_mensagem()
    {
        TudoComErro(Erros.Copilot.NaoConfigurado);

        AbrirAna().Markup.Should().Contain("O módulo Copilot não está configurado no servidor.");
    }

    [Theory]
    [InlineData(Erros.Copilot.CodigoTokenRecusado)]
    [InlineData(Erros.Copilot.CodigoGitHubIndisponivel)]
    public void Falhas_do_GitHub_mostram_a_mensagem_com_Tentar_de_novo(string codigo)
    {
        var erro = codigo == Erros.Copilot.CodigoTokenRecusado ? Erros.Copilot.TokenRecusado : Erros.Copilot.GitHubIndisponivel;
        TudoComErro(erro);

        var pagina = AbrirAna();

        pagina.Find(".estado.erro").TextContent.Should().Contain(erro.Message).And.Contain("Tentar de novo");
        pagina.Markup.Should().NotContain("US$ 0.00");
    }
}
