using System.Security.Claims;
using System.Text;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using ExportarAuditLog = Tcmrj.Siat.App.Web.Features.AuditLog.Exportar;
using ExportarMailQueue = Tcmrj.Siat.App.Web.Features.MailQueue.Exportar;
using PesquisarAuditLog = Tcmrj.Siat.App.Web.Features.AuditLog.Pesquisar;
using PesquisarMailQueue = Tcmrj.Siat.App.Web.Features.MailQueue.Pesquisar;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// Features Exportar (T103): autorizam de novo no servidor antes de montar o arquivo; negado, não geram arquivo e
/// registram <c>Exportacao</c> com <c>AcessoNegado</c>; com sucesso, registram os itens exportados.
/// </summary>
public sealed class ExportarHandlerTests
{
    private static readonly Servidor Hml = new() { Chave = "tcmbd4-hml", Nome = @"TCMBD4\HML", Endereco = @"TCMBD4\HML", Ambiente = Ambiente.Hml };

    private static readonly Servidor Prd = new() { Chave = "tcmbd-prd", Nome = @"TCMBD\PRD", Endereco = @"TCMBD\PRD", Ambiente = Ambiente.Prd };

    private static readonly DateTime Agora = new(2026, 11, 17, 14, 32, 5);

    private readonly List<string> _chamadas = [];
    private readonly List<RegistroConsulta> _registros = [];
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<IServidoresRepository> _servidores = new();
    private readonly Mock<IRegistroConsultaService> _registro = new();
    private readonly Mock<IUsuarioAtualService> _usuario = new();
    private readonly FakeTimeProvider _relogio = new();
    private readonly ExportacaoHtmlService _exportacaoReal = new(new FormatadorStackTrace(), new FormatadorLogEvent());
    private readonly Mock<IExportacaoHtmlService> _exportacao = new(MockBehavior.Strict);

    private Result<Servidor, Erro> _autorizado = Hml;

    public ExportarHandlerTests()
    {
        _relogio.SetLocalTimeZone(TimeZoneInfo.Utc);
        _relogio.SetUtcNow(new DateTimeOffset(Agora, TimeSpan.Zero));

        _autorizacao.Setup(a => a.AutorizarAsync(It.IsAny<Modulo>(), It.IsAny<string>()))
            .Returns(() =>
            {
                _chamadas.Add("autorizar");
                return Task.FromResult(_autorizado);
            });

        _servidores.Setup(s => s.ObterPorChave(Hml.Chave)).Returns(Hml);
        _servidores.Setup(s => s.ObterPorChave(Prd.Chave)).Returns(Prd);

        _registro.Setup(r => r.RegistrarAsync(It.IsAny<RegistroConsulta>()))
            .Callback((RegistroConsulta r) =>
            {
                _chamadas.Add("registrar");
                _registros.Add(r);
            })
            .Returns(Task.CompletedTask);

        _usuario.Setup(u => u.ObterAsync()).ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("nome", "Ana Souza"), new Claim("matricula", "12345")], "teste")));

        _exportacao.Setup(e => e.MailQueue(It.IsAny<CabecalhoExportacao>(), It.IsAny<IReadOnlyList<MensagemVM>>()))
            .Returns((CabecalhoExportacao c, IReadOnlyList<MensagemVM> m) =>
            {
                _chamadas.Add("montar");
                return _exportacaoReal.MailQueue(c, m);
            });
        _exportacao.Setup(e => e.AuditLog(It.IsAny<CabecalhoExportacao>(), It.IsAny<IReadOnlyList<RegistroAuditoriaVM>>(), It.IsAny<IReadOnlyDictionary<int, string>>()))
            .Returns((CabecalhoExportacao c, IReadOnlyList<RegistroAuditoriaVM> r, IReadOnlyDictionary<int, string> n) =>
            {
                _chamadas.Add("montar");
                return _exportacaoReal.AuditLog(c, r, n);
            });
    }

    private ExportarMailQueue.Handler HandlerMailQueue(IExportacaoHtmlService? exportacao = null) =>
        new(_autorizacao.Object, _servidores.Object, exportacao ?? _exportacao.Object, _registro.Object, _usuario.Object, _relogio);

    private ExportarAuditLog.Handler HandlerAuditLog(IExportacaoHtmlService? exportacao = null) =>
        new(_autorizacao.Object, _servidores.Object, exportacao ?? _exportacao.Object, _registro.Object, _usuario.Object, _relogio);

    private static MensagemVM[] Mensagens(int quantidade) =>
        Enumerable.Range(0, quantidade)
            .Select(i => MensagemVM.De(new MensagemEnfileirada { MessageId = $"m{i}", Assunto = $"Assunto {i}", DataPostagem = Agora }, Agora))
            .ToArray();

    private static RegistroAuditoriaVM[] Registros(int quantidade) =>
        Enumerable.Range(0, quantidade)
            .Select(i => new RegistroAuditoriaVM(1000 + i, 1, "Information", Agora, $"Mensagem {i}", null, 12, null, null))
            .ToArray();

    private static ExportarMailQueue.Query ConsultaMailQueue(string servidor = "tcmbd4-hml", int itens = 3) =>
        new(servidor, 3, "3 · RH", " boleto ", 50, "prazo", Mensagens(itens));

    [Fact]
    public async Task MailQueue_autoriza_antes_de_montar_o_arquivo_e_registra_depois()
    {
        var obtido = await HandlerMailQueue().Handle(ConsultaMailQueue(), CancellationToken.None);

        obtido.IsSuccess.Should().BeTrue();
        _chamadas.Should().Equal("autorizar", "montar", "registrar");
        _autorizacao.Verify(a => a.AutorizarAsync(Modulo.MailQueue, "tcmbd4-hml"), Times.Once);
    }

    [Fact]
    public async Task MailQueue_com_sucesso_gera_o_arquivo_com_nome_no_padrao_e_registra_Exportacao_com_os_itens()
    {
        var obtido = await HandlerMailQueue().Handle(ConsultaMailQueue(itens: 7), CancellationToken.None);

        obtido.Value.Nome.Should().Be("mailqueue_20261117_143205.html");
        var html = Encoding.UTF8.GetString(obtido.Value.Conteudo);
        html.Should().Contain("Ana Souza (matrícula 12345)").And.Contain("17/11/2026 14:32:05").And.Contain("3 · RH");
        html.Should().Contain("value=\"prazo\"", "o filtro local vem preenchido");

        var registro = _registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Exportacao);
        registro.Modulo.Should().Be(Modulo.MailQueue);
        registro.ServidorChave.Should().Be(Hml.Chave);
        registro.ServidorNome.Should().Be(Hml.Nome);
        registro.Ambiente.Should().Be(Ambiente.Hml);
        registro.Itens.Should().Be(7);
        registro.Resultado.Should().Be(ResultadoRegistro.Sucesso);
        registro.Filtros.Should().Be(new PesquisarMailQueue.FiltrosRegistrados(3, "boleto", 50));
    }

    [Fact]
    public async Task MailQueue_negada_nao_gera_arquivo_e_registra_Exportacao_AcessoNegado()
    {
        _autorizado = Erros.Consultas.AcessoNegado;
        var semMontar = new Mock<IExportacaoHtmlService>(MockBehavior.Strict);

        var obtido = await HandlerMailQueue(semMontar.Object).Handle(ConsultaMailQueue(Prd.Chave), CancellationToken.None);

        obtido.IsFailure.Should().BeTrue();
        obtido.Error.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
        semMontar.VerifyNoOtherCalls();
        _usuario.Verify(u => u.ObterAsync(), Times.Never);

        var registro = _registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Exportacao);
        registro.Resultado.Should().Be(ResultadoRegistro.AcessoNegado);
        registro.ServidorNome.Should().Be(Prd.Nome, "o registro da negação leva o servidor e o ambiente");
        registro.Ambiente.Should().Be(Ambiente.Prd);
        registro.Itens.Should().BeNull();
    }

    [Fact]
    public async Task MailQueue_servidor_fora_da_lista_nao_gera_arquivo_e_registra_Erro()
    {
        _autorizado = Erros.Consultas.ServidorNaoEncontrado;

        var obtido = await HandlerMailQueue().Handle(ConsultaMailQueue("inexistente"), CancellationToken.None);

        obtido.Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _chamadas.Should().NotContain("montar");
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Erro);
    }

    [Fact]
    public async Task Sem_servidor_nao_autoriza_nem_registra()
    {
        var obtido = await HandlerMailQueue().Handle(ConsultaMailQueue(" "), CancellationToken.None);

        obtido.Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _chamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditLog_com_sucesso_gera_o_arquivo_e_registra_Exportacao_com_os_itens()
    {
        var consulta = new ExportarAuditLog.Query("tcmbd4-hml", 12, " 4363 ", 100, "falha", Registros(4), new Dictionary<int, string> { [12] = "eTCM" });

        var obtido = await HandlerAuditLog().Handle(consulta, CancellationToken.None);

        _chamadas.Should().Equal("autorizar", "montar", "registrar");
        _autorizacao.Verify(a => a.AutorizarAsync(Modulo.AuditLog, "tcmbd4-hml"), Times.Once);
        obtido.Value.Nome.Should().Be("auditlog_20261117_143205.html");
        Encoding.UTF8.GetString(obtido.Value.Conteudo).Should().Contain("eTCM (AppID 12)").And.Contain("LogUID começa com: 4363").And.Contain("Período: últimos 30 dias");

        var registro = _registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Exportacao);
        registro.Modulo.Should().Be(Modulo.AuditLog);
        registro.Itens.Should().Be(4);
        registro.Resultado.Should().Be(ResultadoRegistro.Sucesso);
        registro.Filtros.Should().Be(new PesquisarAuditLog.FiltrosRegistrados(12, "4363", 100));
    }

    [Fact]
    public async Task AuditLog_com_tipo_exporta_so_os_registros_do_tipo_e_o_mostra_nos_filtros()
    {
        var registros = Registros(3)
            .Append(new RegistroAuditoriaVM(2000, 3, "Error", Agora, "Falhou", null, 12, null, null))
            .ToArray();
        IReadOnlyList<RegistroAuditoriaVM>? montados = null;
        _exportacao.Setup(e => e.AuditLog(It.IsAny<CabecalhoExportacao>(), It.IsAny<IReadOnlyList<RegistroAuditoriaVM>>(), It.IsAny<IReadOnlyDictionary<int, string>>()))
            .Returns((CabecalhoExportacao c, IReadOnlyList<RegistroAuditoriaVM> r, IReadOnlyDictionary<int, string> n) =>
            {
                montados = r;
                return _exportacaoReal.AuditLog(c, r, n);
            });
        var consulta = new ExportarAuditLog.Query("tcmbd4-hml", null, null, 100, null, registros, Tipo: "Error");

        var obtido = await HandlerAuditLog().Handle(consulta, CancellationToken.None);

        montados.Should().ContainSingle().Which.LogId.Should().Be(2000);
        Encoding.UTF8.GetString(obtido.Value.Conteudo).Should().Contain("Tipo: Error");
        _registros.Should().ContainSingle().Which.Itens.Should().Be(1);
    }

    [Fact]
    public async Task AuditLog_negado_nao_gera_arquivo_e_registra_Exportacao_AcessoNegado()
    {
        _autorizado = Erros.Consultas.AcessoNegado;
        var semMontar = new Mock<IExportacaoHtmlService>(MockBehavior.Strict);
        var consulta = new ExportarAuditLog.Query(Prd.Chave, null, null, 50, null, Registros(2));

        var obtido = await HandlerAuditLog(semMontar.Object).Handle(consulta, CancellationToken.None);

        obtido.Error.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
        semMontar.VerifyNoOtherCalls();
        var registro = _registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Exportacao);
        registro.Modulo.Should().Be(Modulo.AuditLog);
        registro.Resultado.Should().Be(ResultadoRegistro.AcessoNegado);
        registro.Ambiente.Should().Be(Ambiente.Prd);
    }
}
