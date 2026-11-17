using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Features.MailQueue;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.Validators;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// Cancelamento (história 4, T099, SC-004): o <see cref="ControleConsulta{TResultado}"/> das páginas com a feature
/// Pesquisar real e um repositório que só termina quando liberado ou quando o token é cancelado (como o SqlClient).
/// Nova consulta cancela a anterior e só a última é aplicada; cancelar mantém o resultado anterior e registra Cancelada.
/// </summary>
public sealed class CancelamentoTests : IDisposable
{
    private static readonly Servidor Hml = new() { Chave = "tcmbd4-hml", Nome = @"TCMBD4\HML", Endereco = @"TCMBD4\HML", Ambiente = Ambiente.Hml };

    private static readonly Servidor Prd = new() { Chave = "tcmbd-prd", Nome = @"TCMBD\PRD", Endereco = @"TCMBD\PRD", Ambiente = Ambiente.Prd };

    private readonly List<RegistroConsulta> _registros = [];
    private readonly List<CancellationToken> _tokensNoBanco = [];
    private readonly Dictionary<string, TaskCompletionSource<IReadOnlyList<MensagemEnfileirada>>> _pendentes = new(StringComparer.Ordinal);
    private readonly FakeTimeProvider _relogio = new();
    private readonly Pesquisar.QueryHandler _handler;
    private readonly ControleConsulta<ResultadoConsultaVM<MensagemVM>> _controle;

    private Result<Servidor, Erro> _autorizado = Hml;

    public CancelamentoTests()
    {
        var autorizacao = new Mock<IAutorizacaoConsultaService>();
        autorizacao.Setup(a => a.AutorizarAsync(Modulo.MailQueue, It.IsAny<string>())).Returns(() => Task.FromResult(_autorizado));

        // O "banco": cada texto procurado fica pendente até o teste liberar; cancelado, lança como o SqlClient.
        var repositorio = new Mock<IMailQueueRepository>();
        repositorio.Setup(r => r.PesquisarAsync(It.IsAny<Servidor>(), It.IsAny<FiltroMailQueue>(), It.IsAny<CancellationToken>()))
            .Returns(async (Servidor _, FiltroMailQueue filtro, CancellationToken token) =>
            {
                _tokensNoBanco.Add(token);
                var pendente = new TaskCompletionSource<IReadOnlyList<MensagemEnfileirada>>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendentes[filtro.Texto ?? string.Empty] = pendente;
                await using var registro = token.Register(() => pendente.TrySetCanceled(token));
                return Result.Success<IReadOnlyList<MensagemEnfileirada>, Erro>(await pendente.Task);
            });

        var registroConsulta = new Mock<IRegistroConsultaService>();
        registroConsulta.Setup(r => r.RegistrarAsync(It.IsAny<RegistroConsulta>()))
            .Callback((RegistroConsulta r) => _registros.Add(r))
            .Returns(Task.CompletedTask);

        var servidores = new Mock<IServidoresRepository>();
        servidores.Setup(s => s.ObterPorChave(It.IsAny<string>())).Returns((string chave) => chave == Prd.Chave ? Prd : Hml);

        _handler = new Pesquisar.QueryHandler(
            new PesquisarMailQueueQueryValidator(),
            autorizacao.Object,
            servidores.Object,
            repositorio.Object,
            registroConsulta.Object,
            _relogio);
        _controle = new ControleConsulta<ResultadoConsultaVM<MensagemVM>>(_relogio);
    }

    private static MensagemEnfileirada Mensagem(string id) => new() { MessageId = id, Assunto = id, DataPostagem = new DateTime(2026, 11, 17, 9, 0, 0) };

    // Como a página: inicia, consulta com o token da tentativa e conclui (o cancelamento vira Erros.Consultas.Cancelada).
    private async Task<bool> ConsultarAsync(string texto)
    {
        var tentativa = _controle.Iniciar();
        Result<ResultadoConsultaVM<MensagemVM>, Erro> obtido;
        try
        {
            obtido = await _handler.Handle(new Pesquisar.Query(Hml.Chave, null, texto, "50"), tentativa.Token);
        }
        catch (OperationCanceledException)
        {
            obtido = Erros.Consultas.Cancelada;
        }

        return _controle.Concluir(tentativa, obtido);
    }

    private void Liberar(string texto, params string[] ids) => _pendentes[texto].SetResult(ids.Select(Mensagem).ToArray());

    private async Task<ResultadoConsultaVM<MensagemVM>> ResultadoInicialAsync()
    {
        var consulta = ConsultarAsync("inicial");
        Liberar("inicial", "anterior-1", "anterior-2");
        (await consulta).Should().BeTrue();
        _registros.Clear();
        return _controle.Resultado!;
    }

    [Fact]
    public async Task Nova_consulta_cancela_a_anterior_no_banco_e_so_a_ultima_e_aplicada()
    {
        var primeira = ConsultarAsync("primeira");
        var segunda = ConsultarAsync("segunda");

        _tokensNoBanco.Should().HaveCount(2);
        _tokensNoBanco[0].IsCancellationRequested.Should().BeTrue("a nova consulta interrompe a anterior no banco");
        _tokensNoBanco[1].IsCancellationRequested.Should().BeFalse();

        (await primeira).Should().BeFalse("a consulta substituída não mexe na tela");
        _controle.Estado.Should().Be(EstadoConsulta.Consultando);

        Liberar("segunda", "m-2");
        (await segunda).Should().BeTrue();

        _controle.Estado.Should().Be(EstadoConsulta.ComResultado);
        _controle.Resultado!.Itens.Select(m => m.MessageId).Should().Equal("m-2");
        _registros.Select(r => r.Resultado).Should().Equal(ResultadoRegistro.Cancelada, ResultadoRegistro.Sucesso);
    }

    [Fact]
    public async Task Resultado_que_chega_depois_de_substituido_e_descartado()
    {
        var primeira = ConsultarAsync("primeira");
        var segunda = ConsultarAsync("segunda");
        Liberar("segunda", "m-2");
        await segunda;

        // Mesmo que o banco devolva a primeira depois (cancelamento tardio), ela não é aplicada.
        _pendentes["primeira"].TrySetResult([Mensagem("m-1")]);
        (await primeira).Should().BeFalse();

        _controle.Resultado!.Itens.Select(m => m.MessageId).Should().Equal("m-2");
    }

    [Fact]
    public async Task Cancelar_interrompe_no_banco_mantem_o_resultado_anterior_e_registra_Cancelada()
    {
        var anterior = await ResultadoInicialAsync();
        _relogio.Advance(TimeSpan.FromMinutes(1));

        var lenta = ConsultarAsync("lenta");
        _controle.Estado.Should().Be(EstadoConsulta.Consultando);
        _controle.InicioAtual.Should().NotBeNull();
        _relogio.Advance(TimeSpan.FromMilliseconds(1300));

        _controle.Cancelar();

        _tokensNoBanco[^1].IsCancellationRequested.Should().BeTrue("o token que chega ao repositório (SqlClient) é cancelado");
        (await lenta).Should().BeTrue();
        _controle.Estado.Should().Be(EstadoConsulta.ComResultado);
        _controle.Cancelada.Should().BeTrue();
        _controle.Erro.Should().BeNull();
        _controle.Resultado.Should().BeSameAs(anterior);
        _controle.Duracao.Should().Be(TimeSpan.FromMilliseconds(1300));

        var registro = _registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Consulta);
        registro.Resultado.Should().Be(ResultadoRegistro.Cancelada);
        registro.Itens.Should().BeNull();
    }

    [Fact]
    public async Task Cancelar_sem_resultado_anterior_volta_a_ociosa()
    {
        var lenta = ConsultarAsync("lenta");

        _controle.Cancelar();

        (await lenta).Should().BeTrue();
        _controle.Estado.Should().Be(EstadoConsulta.Ociosa);
        _controle.Resultado.Should().BeNull();
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Cancelada);
    }

    [Fact]
    public async Task Erro_do_servidor_mantem_o_resultado_anterior_e_acesso_negado_o_descarta()
    {
        var anterior = await ResultadoInicialAsync();

        var tentativa = _controle.Iniciar();
        _controle.Concluir(tentativa, Erros.Consultas.TempoEsgotado(Hml.Nome)).Should().BeTrue();
        _controle.Estado.Should().Be(EstadoConsulta.ComErro);
        _controle.Resultado.Should().BeSameAs(anterior);

        _autorizado = Erros.Consultas.AcessoNegado;
        (await ConsultarAsync("negada")).Should().BeTrue();
        _controle.Estado.Should().Be(EstadoConsulta.ComErro);
        _controle.Erro!.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
        _controle.Resultado.Should().BeNull("nenhum dado anterior fica visível depois de uma negação");
    }

    [Fact]
    public async Task Fechar_a_pagina_cancela_a_consulta_e_ignora_o_que_chegar_depois()
    {
        await ResultadoInicialAsync();
        var lenta = ConsultarAsync("lenta");

        _controle.Dispose();

        _tokensNoBanco[^1].IsCancellationRequested.Should().BeTrue();
        (await lenta).Should().BeFalse();
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Cancelada);
        var iniciar = () => _controle.Iniciar();
        iniciar.Should().Throw<ObjectDisposedException>();
    }

    public void Dispose() => _controle.Dispose();

    [Fact]
    public void Cancelar_sem_consulta_em_andamento_nao_faz_nada()
    {
        _controle.Cancelar();

        _controle.Estado.Should().Be(EstadoConsulta.Ociosa);
        _controle.EmAndamento.Should().BeFalse();
    }
}
