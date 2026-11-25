using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Moq;
using Tcmrj.Siat.App.Web.Features.Versoes;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Feature <see cref="ObterVersoes"/>: autoriza o módulo e o servidor antes de ler (FR-003, research.md R6), usa o
/// servidor padrão sem chave, monta o resumo e converte o cancelamento.
/// </summary>
public sealed class ObterVersoesHandlerTests
{
    internal static readonly Servidor Hml = new() { Chave = "tcmbd4-hml", Nome = @"TCMBD4\HML", Endereco = @"TCMBD4\HML", Ambiente = Ambiente.Hml };

    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<IVersoesPublicadasService> _servico = new(MockBehavior.Strict);
    private readonly ObterVersoes.Handler _handler;

    public ObterVersoesHandlerTests()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(true);
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, Hml.Chave)).ReturnsAsync(Hml);
        _handler = new ObterVersoes.Handler(_autorizacao.Object, _servico.Object, Opcoes("tcmbd4-hml"));
    }

    internal static IOptionsMonitor<CacheDeVersoesOptions> Opcoes(string padrao)
    {
        var opcoes = new Mock<IOptionsMonitor<CacheDeVersoesOptions>>();
        opcoes.Setup(o => o.CurrentValue).Returns(new CacheDeVersoesOptions { ServidorPadrao = padrao });
        return opcoes.Object;
    }

    private static LeituraDeCelula V(Ambiente a, Maquina m, string v) => LeituraDeCelula.ComVersao(a, m, v, @"\\m\s\VERSAO.TXT");

    internal static VersoesLidas Lidas(Servidor servidor) => new(
        servidor,
        new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.Zero),
        TimeSpan.FromSeconds(1.3),
        [
            new SistemaVersionado(1, "eTCM", [V(Ambiente.Hml, Maquina.Maquina1, "5.11.3"), V(Ambiente.Hml, Maquina.Maquina2, "5.11.2")]),
            new SistemaVersionado(2, "SGQ", [V(Ambiente.Prd, Maquina.Maquina1, "2.0.1")]),
            new SistemaVersionado(3, "STP", [V(Ambiente.Hml, Maquina.Maquina1, "6.2.0"), V(Ambiente.Hml, Maquina.Maquina2, "6.2.1"),
                LeituraDeCelula.ComErro(Ambiente.Prd, Maquina.Maquina2, "Arquivo não encontrado.", @"\\m\s\VERSAO.TXT")]),
            new SistemaVersionado(4, "SCP Multas", [LeituraDeCelula.ComErro(Ambiente.Hml, Maquina.Maquina1, "Arquivo não encontrado.", @"\\m\s\VERSAO.TXT")]),
        ],
        6);

    private void Devolver(Result<VersoesObtidas, Erro> resultado, bool atualizar = false) =>
        _servico.Setup(s => s.ObterAsync(Hml, atualizar, It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    private Task<Result<App.Web.ViewModels.Versoes.VersoesVM, Erro>> Obter(string? servidor = "tcmbd4-hml", bool atualizar = false) =>
        _handler.Handle(new ObterVersoes.Query(servidor, atualizar), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Sem_a_politica_devolve_AcessoNegado_sem_chamar_o_servico()
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)).ReturnsAsync(false);

        var resultado = await Obter();

        resultado.Error.Should().Be(Erros.Versoes.AcessoNegado);
        _servico.VerifyNoOtherCalls();
        _autorizacao.Verify(a => a.AutorizarAsync(It.IsAny<Modulo>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Sem_chave_usa_o_servidor_padrao()
    {
        Devolver(new VersoesObtidas(Lidas(Hml), null));

        (await Obter(servidor: null)).IsSuccess.Should().BeTrue();

        _autorizacao.Verify(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, "tcmbd4-hml"), Times.Once);
    }

    [Fact]
    public async Task Chave_fora_da_lista_devolve_ServidorNaoEncontrado_sem_ler()
    {
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.Versoes_Sistemas, "xyz")).ReturnsAsync(Erros.Consultas.ServidorNaoEncontrado);

        var resultado = await Obter("xyz");

        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _servico.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Autorizado_devolve_o_resumo_coerente_com_os_sistemas()
    {
        Devolver(new VersoesObtidas(Lidas(Hml), null));

        var vm = (await Obter()).Value;

        (vm.Total, vm.SemPendencias, vm.ComDivergencia, vm.ComErro).Should().Be((4, 1, 2, 2));
        vm.Servidor.Chave.Should().Be("tcmbd4-hml");
        vm.Duracao.Should().Be(TimeSpan.FromSeconds(1.3));
        vm.Falha.Should().BeNull();
        var etcm = vm.Sistemas[0];
        etcm.Celulas[2].Divergente.Should().BeTrue();
        etcm.Celulas[2].RotuloAcessivel.Should().Be("HML, máquina 1: 5.11.3, difere da outra máquina");
        vm.Sistemas.Single(s => s.Nome == "SGQ").Celulas[4].RotuloAcessivel.Should().Be("PRD, máquina 1: 2.0.1");
    }

    [Fact]
    public async Task Atualizar_e_a_falha_sao_repassados()
    {
        var em = new DateTimeOffset(2026, 11, 24, 13, 7, 0, TimeSpan.Zero);
        Devolver(new VersoesObtidas(Lidas(Hml), new FalhaDeLeitura(Erros.Versoes.TempoEsgotado("TCMBD4\\HML"), em)), atualizar: true);

        var vm = (await Obter(atualizar: true)).Value;

        vm.Falha!.Mensagem.Should().Contain("excedeu o tempo limite");
        vm.Falha.Em.Should().Be(em);
    }

    [Fact]
    public async Task Erro_do_servico_e_repassado()
    {
        Devolver(Erros.Versoes.TabelaIndisponivel(@"TCMBD4\HML", "sem permissão"));

        (await Obter()).Error.Code.Should().Be(Erros.Versoes.CodigoTabelaIndisponivel);
    }

    [Fact]
    public async Task Cancelamento_vira_Cancelada()
    {
        _servico.Setup(s => s.ObterAsync(Hml, false, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        (await Obter()).Error.Should().Be(Erros.Versoes.Cancelada);
    }
}
