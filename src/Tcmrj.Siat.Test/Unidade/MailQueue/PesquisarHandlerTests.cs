using System.Data;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.MailQueue;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.Validators;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Repositories;

namespace Tcmrj.Siat.Test.Unidade.MailQueue;

/// <summary>Feature Pesquisar da MailQueue com repositório falso: autorização, registro da consulta e erros.</summary>
public sealed class PesquisarHandlerTests
{
    private static readonly Servidor Hml = new() { Chave = "tcmbd4-hml", Nome = @"TCMBD4\HML", Endereco = @"TCMBD4\HML", Ambiente = Ambiente.Hml };

    private static readonly Servidor Prd = new() { Chave = "tcmbd-prd", Nome = @"TCMBD\PRD", Endereco = @"TCMBD\PRD", Ambiente = Ambiente.Prd };

    private readonly List<string> _chamadas = [];
    private readonly List<RegistroConsulta> _registros = [];
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<IMailQueueRepository> _repositorio = new(MockBehavior.Strict);
    private readonly Pesquisar.QueryHandler _handler;

    private Result<Servidor, Erro> _autorizado = Hml;
    private FiltroMailQueue? _filtroRecebido;
    private Func<CancellationToken, Task<Result<IReadOnlyList<MensagemEnfileirada>, Erro>>> _resposta =
        _ => Task.FromResult(Result.Success<IReadOnlyList<MensagemEnfileirada>, Erro>([]));

    public PesquisarHandlerTests()
    {
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.MailQueue, It.IsAny<string>()))
            .Returns(() =>
            {
                _chamadas.Add("autorizar");
                return Task.FromResult(_autorizado);
            });

        _repositorio.Setup(r => r.PesquisarAsync(It.IsAny<Servidor>(), It.IsAny<FiltroMailQueue>(), It.IsAny<CancellationToken>()))
            .Returns((Servidor _, FiltroMailQueue filtro, CancellationToken token) =>
            {
                _chamadas.Add("consultar");
                _filtroRecebido = filtro;
                return _resposta(token);
            });

        var registro = new Mock<IRegistroConsultaService>();
        registro.Setup(r => r.RegistrarAsync(It.IsAny<RegistroConsulta>()))
            .Callback((RegistroConsulta r) =>
            {
                _chamadas.Add("registrar");
                _registros.Add(r);
            })
            .Returns(Task.CompletedTask);

        var servidores = new Mock<IServidoresRepository>();
        servidores.Setup(s => s.ObterPorChave(Hml.Chave)).Returns(Hml);
        servidores.Setup(s => s.ObterPorChave(Prd.Chave)).Returns(Prd);

        _handler = new Pesquisar.QueryHandler(
            new PesquisarMailQueueQueryValidator(),
            _autorizacao.Object,
            servidores.Object,
            _repositorio.Object,
            registro.Object,
            TimeProvider.System);
    }

    private static MensagemEnfileirada Mensagem(string id, int minutosAtras = 0) => new()
    {
        MessageId = id,
        Assunto = $"Assunto {id}",
        DataPostagem = DateTime.Now.AddMinutes(-minutosAtras),
        Enviado = true,
        DataEnvio = DateTime.Now.AddMinutes(-minutosAtras),
        Destinatarios = "a@tcm.exemplo; b@tcm.exemplo",
    };

    [Fact]
    public async Task Autoriza_antes_de_consultar()
    {
        await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), CancellationToken.None);

        _chamadas.Should().StartWith(["autorizar", "consultar"]);
    }

    [Fact]
    public async Task Acesso_negado_nao_consulta_e_registra_AcessoNegado()
    {
        _autorizado = Erros.Consultas.AcessoNegado;

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd-prd", 3, "boleto", "10"), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
        _chamadas.Should().NotContain("consultar");
        var registro = _registros.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoRegistro.AcessoNegado);
        registro.ServidorChave.Should().Be("tcmbd-prd");
        registro.ServidorNome.Should().Be(@"TCMBD\PRD");
        registro.Ambiente.Should().Be(Ambiente.Prd);
        registro.Itens.Should().BeNull();
    }

    [Fact]
    public async Task Servidor_fora_da_lista_nao_consulta_e_registra_Erro()
    {
        _autorizado = Erros.Consultas.ServidorNaoEncontrado;

        var resultado = await _handler.Handle(new Pesquisar.Query("nao-existe", null, null, null), CancellationToken.None);

        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _chamadas.Should().NotContain("consultar");
        var registro = _registros.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoRegistro.Erro);
        registro.ServidorNome.Should().BeNull();
    }

    [Fact]
    public async Task Sucesso_registra_Consulta_com_Itens_e_Filtros()
    {
        _resposta = _ => Task.FromResult(Result.Success<IReadOnlyList<MensagemEnfileirada>, Erro>([Mensagem("1"), Mensagem("2", 5)]));

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", 3, "  boleto ", "100"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Itens.Select(m => m.MessageId).Should().Equal("1", "2");
        resultado.Value.Servidor.Chave.Should().Be("tcmbd4-hml");
        var registro = _registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Consulta);
        registro.Modulo.Should().Be(Modulo.MailQueue);
        registro.Resultado.Should().Be(ResultadoRegistro.Sucesso);
        registro.Itens.Should().Be(2);
        registro.ServidorChave.Should().Be("tcmbd4-hml");
        registro.ServidorNome.Should().Be(@"TCMBD4\HML");
        registro.Ambiente.Should().Be(Ambiente.Hml);
        registro.Filtros.Should().BeEquivalentTo(new { SmtpId = (int?)3, Texto = "boleto", Quantidade = 100 });
        _chamadas.Should().Equal("autorizar", "consultar", "registrar");
    }

    [Fact]
    public async Task Filtro_chega_ao_repositorio_aparado_e_com_a_quantidade_ajustada()
    {
        await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, "  [teste]_%  ", "5000"), CancellationToken.None);

        _filtroRecebido.Should().NotBeNull();
        _filtroRecebido.Texto.Should().Be("[teste]_%");
        _filtroRecebido.SmtpId.Should().BeNull();
        _filtroRecebido.Quantidade.Should().Be(1000);
    }

    [Fact]
    public async Task Erro_do_repositorio_volta_com_servidor_e_motivo_e_registra_Erro()
    {
        _resposta = _ => Task.FromResult(Result.Failure<IReadOnlyList<MensagemEnfileirada>, Erro>(
            Erros.Consultas.ServidorIndisponivel(@"TCMBD4\HML", "a rede não respondeu")));

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoServidorIndisponivel);
        resultado.Error.Message.Should().Contain(@"TCMBD4\HML").And.Contain("a rede não respondeu");
        var registro = _registros.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoRegistro.Erro);
        registro.Itens.Should().BeNull();
    }

    [Fact]
    public async Task Cancelamento_registra_Cancelada_e_devolve_Erro_Cancelada()
    {
        using var cancelamento = new CancellationTokenSource();
        _resposta = async token =>
        {
            await cancelamento.CancelAsync();
            token.ThrowIfCancellationRequested();
            return Result.Success<IReadOnlyList<MensagemEnfileirada>, Erro>([]);
        };

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), cancelamento.Token);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoCancelada);
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Cancelada);
    }

    [Fact]
    public async Task Cancelada_pelo_repositorio_registra_Cancelada()
    {
        _resposta = _ => Task.FromResult(Result.Failure<IReadOnlyList<MensagemEnfileirada>, Erro>(Erros.Consultas.Cancelada));

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), CancellationToken.None);

        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoCancelada);
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Cancelada);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("tcmbd4-hml", "abc", null)]
    [InlineData("tcmbd4-hml", "10", 501)]
    public async Task Query_invalida_nao_autoriza_nao_consulta_e_nao_registra(string? servidor, string? quantidade, int? tamanhoTexto)
    {
        var texto = tamanhoTexto is null ? null : new string('x', tamanhoTexto.Value);

        var resultado = await _handler.Handle(new Pesquisar.Query(servidor, null, texto, quantidade), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        _chamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Quantidade_nao_numerica_devolve_o_erro_do_campo()
    {
        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, "dez"), CancellationToken.None);

        resultado.Error.Code.Should().Be(Erros.Filtros.CodigoQuantidadeNaoNumerica);
        resultado.Error.Message.Should().Be(Mensagens.QuantidadeForaDoPadrao);
    }

    [Fact]
    public void Parametros_do_SQL_sao_tipados_e_o_texto_vai_com_PadraoLike()
    {
        var filtro = FiltroMailQueue.Create(3, "[teste]_%", 10).Value;

        var parametros = MailQueueRepository.Parametros(filtro).ToDictionary(p => p.ParameterName);

        parametros.Keys.Should().BeEquivalentTo("@Top", "@SmtpId", "@Texto");
        parametros["@Top"].SqlDbType.Should().Be(SqlDbType.Int);
        parametros["@Top"].Value.Should().Be(10);
        parametros["@SmtpId"].SqlDbType.Should().Be(SqlDbType.Int);
        parametros["@SmtpId"].Value.Should().Be(3);
        parametros["@Texto"].SqlDbType.Should().Be(SqlDbType.NVarChar);
        parametros["@Texto"].Size.Should().Be(-1);
        parametros["@Texto"].Value.Should().Be("%[[]teste][_][%]%");
    }

    [Fact]
    public void Parametros_sem_filtros_vao_como_DBNull()
    {
        var parametros = MailQueueRepository.Parametros(FiltroMailQueue.Padrao).ToDictionary(p => p.ParameterName);

        parametros["@Top"].Value.Should().Be(50);
        parametros["@SmtpId"].Value.Should().Be(DBNull.Value);
        parametros["@Texto"].Value.Should().Be(DBNull.Value);
    }
}
