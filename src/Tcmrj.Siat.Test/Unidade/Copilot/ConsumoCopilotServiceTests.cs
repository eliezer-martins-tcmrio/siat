using System.Collections.Concurrent;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>
/// Serviço do Copilot (specs/005-copilot-meu-consumo, research.md R4, R5, R7 e R8) com o repositório em memória e o
/// relógio falso: pessoa da carga, consumo do período, mês atual, cache, "Atualizar agora" e cancelamento.
/// </summary>
public sealed class ConsumoCopilotServiceTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);
    private static readonly ConexaoGitHub Ana = new(7, "ana-m");
    private static readonly ConexaoGitHub Bia = new(8, "bia");
    private static readonly Periodo DiaFechado = new(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 10));
    private static readonly Periodo DiaRecente = new(new DateOnly(2026, 11, 27), new DateOnly(2026, 11, 27));

    private readonly FakeTimeProvider _relogio = new(Agora);
    private readonly RepositorioCopilotFalso _repositorio = new();
    private readonly CacheDoCopilotOptions _opcoes = new() { Organizacao = RepositorioCopilotFalso.Org };
    private readonly ServiceProvider _provedor;

    public ConsumoCopilotServiceTests()
    {
        _provedor = new ServiceCollection()
            .AddSingleton<IGitHubCopilotRepository>(_repositorio)
            .BuildServiceProvider();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _provedor.Dispose();

    private ConsumoCopilotService Criar()
    {
        var monitor = new Mock<IOptionsMonitor<CacheDoCopilotOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(_opcoes);
        return new ConsumoCopilotService(
            _provedor.GetRequiredService<IServiceScopeFactory>(), monitor.Object, _relogio, NullLogger<ConsumoCopilotService>.Instance);
    }

    private static Result<T, Erro> Falha<T>(Erro erro) => Result.Failure<T, Erro>(erro);

    private void SemLicenca() => _repositorio.Licencas = () => RepositorioCopilotFalso.Ok<IReadOnlyList<Licenca>>([]);

    // ---------- Pessoa ----------

    [Fact]
    public async Task Pessoa_com_licenca_perfil_limite_padrao_e_sem_time()
    {
        var pessoa = await Criar().ObterPessoaAsync(Ana, false, Ct);

        pessoa.IsSuccess.Should().BeTrue();
        var p = pessoa.Value;
        p.Login.Should().Be("ana-m");
        p.Nome.Should().Be("Ana Souza");
        p.FotoUrl.Should().Be("https://avatars.githubusercontent.com/u/7");
        p.PerfilDisponivel.Should().BeTrue();
        p.TemLicenca.Should().BeTrue();
        p.Limite.Value.Should().Be(new LimiteVigente.Padrao(50));
        p.Times.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Associacao_e_relida_a_cada_carga_e_os_dados_da_organizacao_ficam_no_cache()
    {
        var servico = Criar();

        await servico.ObterPessoaAsync(Ana, false, Ct);
        await servico.ObterPessoaAsync(Ana, false, Ct);
        _relogio.Advance(TimeSpan.FromSeconds(11));
        await servico.ObterPessoaAsync(Ana, false, Ct);

        _repositorio.Quantas("associacao:").Should().Be(2, "dentro de 10 s a pessoa da carga é compartilhada; depois, a associação é relida");
        _repositorio.Quantas("licencas").Should().Be(1);
        _repositorio.Quantas("budgets").Should().Be(1);
        _repositorio.Quantas("times").Should().Be(1);
        _repositorio.Quantas("perfil:").Should().Be(1);
    }

    [Fact]
    public async Task Associacao_404_com_perfil_renomeado_consulta_o_login_novo()
    {
        _repositorio.Associacao = login => login == "ana-m"
            ? Erros.Copilot.NaoMembro(login, RepositorioCopilotFalso.Org)
            : new Associacao("active", "member", 7);
        _repositorio.Perfil = id => new PerfilGitHub(id, "ana-nova", "Ana Souza", null);

        var pessoa = await Criar().ObterPessoaAsync(Ana, false, Ct);

        pessoa.Value.Login.Should().Be("ana-nova");
        _repositorio.Chamadas.Should().ContainInOrder("associacao:ana-m", "perfil:7", "associacao:ana-nova");
    }

    [Fact]
    public async Task Associacao_404_com_o_mesmo_login_no_perfil_e_nao_membro()
    {
        _repositorio.Associacao = login => Erros.Copilot.NaoMembro(login, RepositorioCopilotFalso.Org);

        var pessoa = await Criar().ObterPessoaAsync(Ana, false, Ct);

        pessoa.Error.Code.Should().Be(Erros.Copilot.CodigoNaoMembro);
        _repositorio.Quantas("licencas").Should().Be(0);
    }

    [Theory]
    [InlineData("active", 8)]
    [InlineData("pending", 7)]
    public async Task Associacao_de_outro_id_ou_nao_ativa_e_nao_membro(string estado, long id)
    {
        _repositorio.Associacao = _ => new Associacao(estado, "member", id);

        (await Criar().ObterPessoaAsync(Ana, false, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoNaoMembro);
    }

    [Fact]
    public async Task Perfil_com_falha_depois_do_404_e_a_falha_do_perfil()
    {
        _repositorio.Associacao = login => Erros.Copilot.NaoMembro(login, RepositorioCopilotFalso.Org);
        _repositorio.Perfil = _ => Erros.Copilot.GitHubIndisponivel;

        (await Criar().ObterPessoaAsync(Ana, false, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoGitHubIndisponivel);
    }

    [Fact]
    public async Task Licenca_e_reconhecida_pelo_id_mesmo_com_outro_login()
    {
        _repositorio.Licencas = () => RepositorioCopilotFalso.Ok<IReadOnlyList<Licenca>>([new Licenca(7, "login-antigo")]);

        (await Criar().ObterPessoaAsync(Ana, false, Ct)).Value.TemLicenca.Should().BeTrue();
    }

    [Fact]
    public async Task Sem_licenca_e_sem_budget_individual_e_sem_licenca_nem_limite()
    {
        SemLicenca();

        (await Criar().ObterPessoaAsync(Ana, false, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoSemLicencaNemLimite);
    }

    [Fact]
    public async Task Sem_licenca_com_budget_individual_e_pessoa_com_excecao_individual()
    {
        SemLicenca();
        _repositorio.Budgets = () => RepositorioCopilotFalso.Ok<IReadOnlyList<Budget>>(
            [new Budget("padrao", EscopoDeBudget.Padrao, 50, null), new Budget("ana", EscopoDeBudget.Individual, 120, "Ana-M")]);

        var pessoa = (await Criar().ObterPessoaAsync(Ana, false, Ct)).Value;

        pessoa.TemLicenca.Should().BeFalse();
        pessoa.Limite.Value.Should().Be(new LimiteVigente.Individual(120));
    }

    [Fact]
    public async Task Sem_licenca_e_budgets_com_falha_e_o_erro_dos_budgets()
    {
        SemLicenca();
        _repositorio.Budgets = () => Erros.Copilot.TokenRecusado;

        (await Criar().ObterPessoaAsync(Ana, false, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoTokenRecusado);
    }

    [Fact]
    public async Task Times_da_pessoa_ordenados_em_pt_BR_com_o_herdado()
    {
        _repositorio.Times = () => RepositorioCopilotFalso.Ok<IReadOnlyList<TimeDaOrganizacao>>(
            [new TimeDaOrganizacao(1, "Zeta", "zeta"), new TimeDaOrganizacao(2, "Beta", "beta"), new TimeDaOrganizacao(3, "Ágil", "agil")]);
        _repositorio.Membros = slug => RepositorioCopilotFalso.Ok<IReadOnlyList<MembroDoTime>>(slug switch
        {
            "zeta" => [new MembroDoTime("ana-m", false)],
            "agil" => [new MembroDoTime("ANA-M", true)],
            _ => [new MembroDoTime("bia", false)],
        });

        var times = (await Criar().ObterPessoaAsync(Ana, false, Ct)).Value.Times.Value;

        times.Should().Equal(new TimeDaPessoa(3, "Ágil", "agil", true), new TimeDaPessoa(1, "Zeta", "zeta", false));
    }

    [Fact]
    public async Task Falha_em_um_time_deixa_os_times_com_erro_e_o_resto_normal()
    {
        _repositorio.Times = () => RepositorioCopilotFalso.Ok<IReadOnlyList<TimeDaOrganizacao>>(
            [new TimeDaOrganizacao(1, "Zeta", "zeta"), new TimeDaOrganizacao(2, "Beta", "beta")]);
        _repositorio.Membros = slug => slug == "beta"
            ? Erros.Copilot.GitHubIndisponivel
            : RepositorioCopilotFalso.Ok<IReadOnlyList<MembroDoTime>>([new MembroDoTime("ana-m", false)]);

        var pessoa = (await Criar().ObterPessoaAsync(Ana, false, Ct)).Value;

        pessoa.Times.IsFailure.Should().BeTrue();
        pessoa.TemLicenca.Should().BeTrue();
        pessoa.Limite.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Budgets_com_falha_e_com_licenca_deixam_o_limite_com_erro_e_o_resto_normal()
    {
        _repositorio.Budgets = () => Erros.Copilot.GitHubIndisponivel;

        var pessoa = (await Criar().ObterPessoaAsync(Ana, false, Ct)).Value;

        pessoa.Limite.Error.Code.Should().Be(Erros.Copilot.CodigoGitHubIndisponivel);
        pessoa.TemLicenca.Should().BeTrue();
        pessoa.Times.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task O_resultado_nao_leva_budgets_nem_o_global()
    {
        typeof(PessoaCopilot).GetProperties().Select(p => p.PropertyType)
            .Should().NotContain(t => t == typeof(Budget) || typeof(IEnumerable<Budget>).IsAssignableFrom(t));

        var pessoa = (await Criar().ObterPessoaAsync(Ana, false, Ct)).Value;

        pessoa.Limite.Value.Should().Be(new LimiteVigente.Padrao(50), "o budget global (5000) nunca é o limite");
    }

    [Fact]
    public async Task Perfil_com_falha_usa_o_login_da_conexao_sem_nome_nem_foto()
    {
        _repositorio.Perfil = _ => Erros.Copilot.GitHubIndisponivel;

        var pessoa = (await Criar().ObterPessoaAsync(Ana, false, Ct)).Value;

        pessoa.Login.Should().Be("ana-m");
        pessoa.Nome.Should().BeNull();
        pessoa.FotoUrl.Should().BeNull();
        pessoa.PerfilDisponivel.Should().BeFalse();
        pessoa.TemLicenca.Should().BeTrue();
    }

    // ---------- Pessoa da carga ----------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tres_pedidos_simultaneos_da_carga_fazem_uma_so_leitura_da_associacao(bool atualizar)
    {
        var servico = Criar();
        if (atualizar)
        {
            await servico.ObterPessoaAsync(Ana, false, Ct);
        }

        var antes = _repositorio.Quantas("associacao:");
        _repositorio.PortaoDaAssociacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pessoa = servico.ObterPessoaAsync(Ana, atualizar, Ct);
        var periodo = servico.ObterPeriodoAsync(Ana, DiaFechado, atualizar, null, Ct);
        var mes = servico.ObterMesAtualAsync(Ana, atualizar, Ct);
        _repositorio.PortaoDaAssociacao.SetResult();
        await Task.WhenAll(pessoa, periodo, mes);

        _repositorio.Quantas("associacao:").Should().Be(antes + 1);
        (await pessoa).IsSuccess.Should().BeTrue();
        (await periodo).IsSuccess.Should().BeTrue();
        (await mes).IsSuccess.Should().BeTrue();
    }

    public static TheoryData<string> ErrosDaPessoa => ["nao-membro", "sem-licenca", "falha"];

    [Theory]
    [MemberData(nameof(ErrosDaPessoa))]
    public async Task Erro_da_pessoa_vale_para_o_periodo_e_o_mes_sem_ler_relatorio(string caso)
    {
        string codigo;
        switch (caso)
        {
            case "nao-membro":
                _repositorio.Associacao = _ => new Associacao("active", "member", 99);
                codigo = Erros.Copilot.CodigoNaoMembro;
                break;
            case "sem-licenca":
                SemLicenca();
                codigo = Erros.Copilot.CodigoSemLicencaNemLimite;
                break;
            default:
                _repositorio.Associacao = _ => Erros.Copilot.GitHubIndisponivel;
                codigo = Erros.Copilot.CodigoGitHubIndisponivel;
                break;
        }

        var servico = Criar();

        (await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct)).Error.Code.Should().Be(codigo);
        (await servico.ObterMesAtualAsync(Ana, false, Ct)).Error.Code.Should().Be(codigo);
        _repositorio.Quantas("relatorio:").Should().Be(0);
    }

    [Fact]
    public async Task Falha_so_dos_times_ou_do_limite_nao_impede_o_consumo()
    {
        _repositorio.Times = () => Erros.Copilot.GitHubIndisponivel;
        _repositorio.Budgets = () => Erros.Copilot.GitHubIndisponivel;
        var servico = Criar();

        (await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct)).IsSuccess.Should().BeTrue();
        (await servico.ObterMesAtualAsync(Ana, false, Ct)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Com_a_pessoa_no_cache_a_carga_seguinte_com_404_e_nao_membro_sem_ler_relatorio()
    {
        var servico = Criar();
        (await servico.ObterPessoaAsync(Ana, false, Ct)).IsSuccess.Should().BeTrue();

        _relogio.Advance(TimeSpan.FromSeconds(11));
        _repositorio.Associacao = login => Erros.Copilot.NaoMembro(login, RepositorioCopilotFalso.Org);

        (await servico.ObterPessoaAsync(Ana, false, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoNaoMembro);
        (await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoNaoMembro);
        _repositorio.Quantas("relatorio:").Should().Be(0);
    }

    [Fact]
    public async Task Com_a_pessoa_no_cache_a_carga_seguinte_com_falha_da_associacao_devolve_a_falha()
    {
        var servico = Criar();
        await servico.ObterPessoaAsync(Ana, false, Ct);

        _relogio.Advance(TimeSpan.FromSeconds(11));
        _repositorio.Associacao = _ => Erros.Copilot.GitHubIndisponivel;

        (await servico.ObterPessoaAsync(Ana, false, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoGitHubIndisponivel);
    }

    // ---------- Período e mês ----------

    [Fact]
    public async Task Periodo_le_um_relatorio_por_dia_com_no_maximo_LeiturasSimultaneas()
    {
        _opcoes.LeiturasSimultaneas = 2;
        _repositorio.Relatorio = async (_, _, _, _, ct) =>
        {
            await Task.Delay(15, ct);
            return RepositorioCopilotFalso.Ok<IReadOnlyList<ItemDeConsumo>>([new ItemDeConsumo("GPT-5", "credits", 25, 1.00m, 0.50m)]);
        };
        var periodo = new Periodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10));

        var consumo = await Criar().ObterPeriodoAsync(Ana, periodo, false, null, Ct);

        consumo.Value.Dias.Should().HaveCount(10);
        consumo.Value.Total.ValorBruto.Should().Be(10.00m);
        _repositorio.Quantas("relatorio:ana-m:2026-11-").Should().Be(10);
        _repositorio.MaximoDeRelatoriosSimultaneos.Should().BeInRange(1, 2);
    }

    [Fact]
    public async Task Primeira_falha_interrompe_os_dias_restantes_e_vira_o_erro_do_periodo()
    {
        _opcoes.LeiturasSimultaneas = 1;
        _repositorio.Relatorio = (_, _, _, dia, _) => Task.FromResult(dia == 3
            ? Falha<IReadOnlyList<ItemDeConsumo>>(Erros.Copilot.GitHubIndisponivel)
            : RepositorioCopilotFalso.Ok<IReadOnlyList<ItemDeConsumo>>([]));
        var periodo = new Periodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10));

        var consumo = await Criar().ObterPeriodoAsync(Ana, periodo, false, null, Ct);

        consumo.Error.Code.Should().Be(Erros.Copilot.CodigoGitHubIndisponivel);
        _repositorio.Quantas("relatorio:").Should().BeLessThan(10);
    }

    [Fact]
    public async Task Limite_de_requisicoes_interrompe_os_dias_restantes_e_volta_com_o_tempo()
    {
        _opcoes.LeiturasSimultaneas = 1;
        _repositorio.Relatorio = (_, _, _, dia, _) => Task.FromResult(dia == 2
            ? Falha<IReadOnlyList<ItemDeConsumo>>(Erros.Copilot.LimiteDeRequisicoes(Agora.AddMinutes(5), Agora))
            : RepositorioCopilotFalso.Ok<IReadOnlyList<ItemDeConsumo>>([]));
        var periodo = new Periodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10));

        var consumo = await Criar().ObterPeriodoAsync(Ana, periodo, false, null, Ct);

        consumo.Error.Code.Should().Be(Erros.Copilot.CodigoLimiteDeRequisicoes);
        consumo.Error.Message.Should().Contain("Tente de novo em 5 minutos");
        _repositorio.Quantas("relatorio:").Should().BeLessThan(10);
    }

    [Fact]
    public async Task Progresso_recebe_de_1_a_N()
    {
        var recebidos = new ConcurrentBag<int>();
        var periodo = new Periodo(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        await Criar().ObterPeriodoAsync(Ana, periodo, false, new ProgressoImediato(recebidos.Add), Ct);

        recebidos.Order().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Mes_atual_le_o_relatorio_mensal_sem_dia()
    {
        var mes = await Criar().ObterMesAtualAsync(Ana, false, Ct);

        mes.Value.Total.ValorBruto.Should().Be(1.00m);
        _repositorio.Chamadas.Should().Contain("relatorio:ana-m:2026-11");
    }

    // ---------- Cache ----------

    [Fact]
    public async Task Segunda_chamada_dentro_da_validade_nao_le_o_relatorio_de_novo()
    {
        var servico = Criar();

        await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);
        await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);

        _repositorio.Quantas("relatorio:").Should().Be(1);
    }

    [Fact]
    public async Task Dia_recente_expira_em_10_minutos_e_o_fechado_em_24_horas()
    {
        var servico = Criar();
        await servico.ObterPeriodoAsync(Ana, DiaRecente, false, null, Ct);
        await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);

        _relogio.Advance(TimeSpan.FromMinutes(11));
        await servico.ObterPeriodoAsync(Ana, DiaRecente, false, null, Ct);
        await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);

        _repositorio.Quantas("relatorio:ana-m:2026-11-27").Should().Be(2);
        _repositorio.Quantas("relatorio:ana-m:2026-11-10").Should().Be(1);

        _relogio.Advance(TimeSpan.FromHours(24));
        await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);

        _repositorio.Quantas("relatorio:ana-m:2026-11-10").Should().Be(2);
    }

    [Fact]
    public async Task Licencas_budgets_e_times_sao_compartilhados_e_relatorios_e_perfis_separados_por_conta()
    {
        var servico = Criar();

        await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);
        await servico.ObterPeriodoAsync(Bia, DiaFechado, false, null, Ct);

        _repositorio.Quantas("licencas").Should().Be(1);
        _repositorio.Quantas("budgets").Should().Be(1);
        _repositorio.Quantas("times").Should().Be(1);
        _repositorio.Chamadas.Should().Contain(["perfil:7", "perfil:8", "relatorio:ana-m:2026-11-10", "relatorio:bia:2026-11-10"]);
    }

    // ---------- Atualizar agora ----------

    [Fact]
    public async Task Atualizar_rele_pessoa_dados_da_organizacao_dias_recentes_e_mes_mas_nao_os_fechados()
    {
        var servico = Criar();
        var periodo = new Periodo(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 27));
        await servico.ObterPeriodoAsync(Ana, periodo, false, null, Ct);
        await servico.ObterMesAtualAsync(Ana, false, Ct);
        var antes = _repositorio.Chamadas.ToArray();
        _repositorio.PortaoDaAssociacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pedidos = Task.WhenAll(
            servico.ObterPessoaAsync(Ana, true, Ct),
            servico.ObterPeriodoAsync(Ana, periodo, true, null, Ct),
            servico.ObterMesAtualAsync(Ana, true, Ct));
        _repositorio.PortaoDaAssociacao.SetResult();
        await pedidos;

        var depois = _repositorio.Chamadas.Skip(antes.Length).ToArray();
        depois.Count(c => c.StartsWith("associacao:", StringComparison.Ordinal)).Should().Be(1);
        depois.Should().Contain(["perfil:7", "licencas", "budgets", "times", "relatorio:ana-m:2026-11"]);
        depois.Should().Contain(["relatorio:ana-m:2026-11-25", "relatorio:ana-m:2026-11-26", "relatorio:ana-m:2026-11-27"]);
        depois.Should().NotContain(c => c.StartsWith("relatorio:ana-m:2026-11-2", StringComparison.Ordinal)
            && string.CompareOrdinal(c, "relatorio:ana-m:2026-11-25") < 0);
    }

    [Fact]
    public async Task Dia_recente_que_falha_ao_atualizar_devolve_o_erro_e_nao_o_valor_anterior()
    {
        var servico = Criar();
        (await servico.ObterPeriodoAsync(Ana, DiaRecente, false, null, Ct)).IsSuccess.Should().BeTrue();

        _repositorio.Relatorio = (_, _, _, _, _) => Task.FromResult(Falha<IReadOnlyList<ItemDeConsumo>>(Erros.Copilot.GitHubIndisponivel));

        (await servico.ObterPeriodoAsync(Ana, DiaRecente, true, null, Ct)).Error.Code.Should().Be(Erros.Copilot.CodigoGitHubIndisponivel);
    }

    [Fact]
    public async Task Dois_pedidos_simultaneos_do_mesmo_dia_fazem_uma_leitura()
    {
        var portao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repositorio.Relatorio = async (_, _, _, _, ct) =>
        {
            await portao.Task.WaitAsync(ct);
            return RepositorioCopilotFalso.Ok<IReadOnlyList<ItemDeConsumo>>([]);
        };
        var servico = Criar();
        await servico.ObterPessoaAsync(Ana, false, Ct);

        var primeiro = servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);
        var segundo = servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);
        await EsperarAsync(() => _repositorio.Quantas("relatorio:") == 1);
        portao.SetResult();
        await Task.WhenAll(primeiro, segundo);

        _repositorio.Quantas("relatorio:").Should().Be(1);
    }

    [Fact]
    public async Task Cancelar_o_unico_pedido_interrompe_a_leitura_e_nao_deixa_nada_no_cache()
    {
        var interrompida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bloquear = true;
        _repositorio.Relatorio = async (_, _, _, _, ct) =>
        {
            if (Volatile.Read(ref bloquear))
            {
                using var registro = ct.Register(() => interrompida.TrySetResult());
                await Task.Delay(Timeout.Infinite, ct);
            }

            return RepositorioCopilotFalso.Ok<IReadOnlyList<ItemDeConsumo>>([]);
        };
        var servico = Criar();
        await servico.ObterPessoaAsync(Ana, false, Ct);
        using var cancelamento = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var pedido = servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, cancelamento.Token);
        await EsperarAsync(() => _repositorio.Quantas("relatorio:") == 1);
        await cancelamento.CancelAsync();

        (await pedido).Error.Code.Should().Be(Erros.Copilot.CodigoCancelada);
        await interrompida.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Volatile.Write(ref bloquear, false);
        (await servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct)).IsSuccess.Should().BeTrue();
        _repositorio.Quantas("relatorio:").Should().Be(2, "a leitura cancelada não deixou valor nem falha");
    }

    [Fact]
    public async Task Cancelar_um_de_dois_pedidos_do_mesmo_dia_nao_interrompe_o_outro()
    {
        var portao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repositorio.Relatorio = async (_, _, _, _, ct) =>
        {
            await portao.Task.WaitAsync(ct);
            return RepositorioCopilotFalso.Ok<IReadOnlyList<ItemDeConsumo>>([new ItemDeConsumo("GPT-5", "credits", 25, 1.00m, 0.50m)]);
        };
        var servico = Criar();
        await servico.ObterPessoaAsync(Ana, false, Ct);
        using var cancelamento = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var cancelado = servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, cancelamento.Token);
        var mantido = servico.ObterPeriodoAsync(Ana, DiaFechado, false, null, Ct);
        await EsperarAsync(() => _repositorio.Quantas("relatorio:") == 1);
        await cancelamento.CancelAsync();
        (await cancelado).Error.Code.Should().Be(Erros.Copilot.CodigoCancelada);
        portao.SetResult();

        (await mantido).Value.Total.ValorBruto.Should().Be(1.00m);
        _repositorio.Quantas("relatorio:").Should().Be(1);
    }

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        for (var i = 0; i < 500 && !condicao(); i++)
        {
            await Task.Delay(10, Ct);
        }

        condicao().Should().BeTrue("a condição esperada não aconteceu a tempo");
    }

    /// <summary><see cref="IProgress{T}"/> que repassa na hora (o <c>Progress&lt;T&gt;</c> posta no contexto).</summary>
    private sealed class ProgressoImediato(Action<int> receber) : IProgress<int>
    {
        public void Report(int value) => receber(value);
    }
}
