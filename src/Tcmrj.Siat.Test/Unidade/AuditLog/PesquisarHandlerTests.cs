using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.AuditLog;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.Validators;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade.AuditLog;

/// <summary>Consulta do AuditLog: valida, autoriza antes de consultar, consulta e registra toda tentativa.</summary>
public class PesquisarHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 14, 32, 0, TimeSpan.Zero);

    private static readonly Servidor Hml = new()
    {
        Chave = "tcmbd4-hml",
        Nome = "TCMBD4\\HML",
        Endereco = "TCMBD4\\HML",
        Ambiente = Ambiente.Hml,
    };

    private static readonly Servidor Prd = new()
    {
        Chave = "tcmbd4-prd",
        Nome = "TCMBD4\\PRD",
        Endereco = "TCMBD4\\PRD",
        Ambiente = Ambiente.Prd,
    };

    private readonly List<string> _sequencia = [];
    private readonly List<RegistroConsulta> _registros = [];
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly AuditLogRepositoryFalso _repositorio;
    private readonly Pesquisar.Handler _handler;

    public PesquisarHandlerTests()
    {
        _repositorio = new AuditLogRepositoryFalso(_sequencia);
        Autorizar(Result.Success<Servidor, Erro>(Hml));

        var registro = new Mock<IRegistroConsultaService>();
        registro.Setup(r => r.RegistrarAsync(It.IsAny<RegistroConsulta>()))
            .Callback<RegistroConsulta>(_registros.Add)
            .Returns(Task.CompletedTask);

        var servidores = new Mock<IServidoresRepository>();
        servidores.Setup(s => s.ObterPorChave("tcmbd4-hml")).Returns(Hml);
        servidores.Setup(s => s.ObterPorChave("tcmbd4-prd")).Returns(Prd);

        _handler = new Pesquisar.Handler(
            new PesquisarAuditLogQueryValidator(),
            _autorizacao.Object,
            servidores.Object,
            _repositorio,
            registro.Object,
            new RelogioFixo(Agora));
    }

    [Fact]
    public async Task Sucesso_devolve_os_registros_e_registra_a_consulta()
    {
        _repositorio.AoPesquisar = (_, _) => Task.FromResult(Result.Success<IReadOnlyList<RegistroAuditoria>, Erro>(
        [
            new RegistroAuditoria { LogId = 436512, TipoId = 2, Tipo = "Error", AppId = 12, Mensagem = "Falhou", Excecao = "System.Exception: x" },
            new RegistroAuditoria { LogId = 436498, TipoId = 1, Tipo = "Information", AppId = 31 },
        ]));

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", "12", "4363", "100"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Itens.Select(i => i.LogId).Should().Equal(436512, 436498);
        resultado.Value.Itens[0].TemExcecao.Should().BeTrue();
        resultado.Value.Servidor.Chave.Should().Be("tcmbd4-hml");
        resultado.Value.ConsultadoEm.Should().Be(Agora);

        var registro = _registros.Should().ContainSingle().Subject;
        registro.Operacao.Should().Be(OperacaoConsulta.Consulta);
        registro.Modulo.Should().Be(Modulo.AuditLog);
        registro.ServidorChave.Should().Be("tcmbd4-hml");
        registro.ServidorNome.Should().Be("TCMBD4\\HML");
        registro.Ambiente.Should().Be(Ambiente.Hml);
        registro.Resultado.Should().Be(ResultadoRegistro.Sucesso);
        registro.Itens.Should().Be(2);
        registro.Filtros.Should().BeEquivalentTo(new { AppId = 12, PrefixoLogUid = "4363", Quantidade = 100 });
    }

    [Fact]
    public async Task Autoriza_antes_de_consultar()
    {
        await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), CancellationToken.None);

        _sequencia.Should().Equal("autorizacao", "repositorio");
    }

    [Fact]
    public async Task Acesso_negado_nao_consulta_e_registra_AcessoNegado()
    {
        Autorizar(Result.Failure<Servidor, Erro>(Erros.Consultas.AcessoNegado));

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-prd", null, "4363", null), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
        _repositorio.Pesquisas.Should().BeEmpty();

        var registro = _registros.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoRegistro.AcessoNegado);
        registro.ServidorChave.Should().Be("tcmbd4-prd");
        registro.ServidorNome.Should().Be("TCMBD4\\PRD");
        registro.Ambiente.Should().Be(Ambiente.Prd);
        registro.Itens.Should().BeNull();
        registro.Filtros.Should().BeEquivalentTo(new { AppId = (int?)null, PrefixoLogUid = "4363", Quantidade = 50 });
    }

    [Fact]
    public async Task Servidor_inexistente_nao_consulta_e_registra_Erro()
    {
        Autorizar(Result.Failure<Servidor, Erro>(Erros.Consultas.ServidorNaoEncontrado));

        var resultado = await _handler.Handle(new Pesquisar.Query("nao-existe", null, null, null), CancellationToken.None);

        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _repositorio.Pesquisas.Should().BeEmpty();
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Erro);
    }

    [Fact]
    public async Task Erro_do_repositorio_devolve_servidor_e_motivo_e_registra_Erro()
    {
        _repositorio.AoPesquisar = (_, _) => Task.FromResult(
            Result.Failure<IReadOnlyList<RegistroAuditoria>, Erro>(Erros.Consultas.ServidorIndisponivel("TCMBD4\\HML", "rede inacessível")));

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Message.Should().Contain("TCMBD4\\HML").And.Contain("rede inacessível");
        var registro = _registros.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoRegistro.Erro);
        registro.Itens.Should().BeNull();
    }

    [Fact]
    public async Task Cancelamento_devolve_Cancelada_e_registra_Cancelada()
    {
        using var cts = new CancellationTokenSource();
        _repositorio.AoPesquisar = (_, _) =>
        {
            cts.Cancel();
            return Task.FromResult(Result.Failure<IReadOnlyList<RegistroAuditoria>, Erro>(Erros.Consultas.Cancelada));
        };

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), cts.Token);

        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoCancelada);
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Cancelada);
    }

    [Fact]
    public async Task OperationCanceledException_do_repositorio_vira_Cancelada()
    {
        _repositorio.AoPesquisar = (_, _) => throw new OperationCanceledException();

        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, null, null), CancellationToken.None);

        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoCancelada);
        _registros.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoRegistro.Cancelada);
    }

    [Fact]
    public async Task Filtros_e_token_chegam_ao_repositorio()
    {
        using var cts = new CancellationTokenSource();

        await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", " 12 ", " 4fdf7b6b ", "5000"), cts.Token);

        var (servidor, filtro, token) = _repositorio.Pesquisas.Should().ContainSingle().Subject;
        servidor.Should().BeSameAs(Hml);
        filtro.AppId.Should().Be(12);
        filtro.PrefixoLogUid.Should().Be("4FDF7B6B");
        filtro.Quantidade.Should().Be(1000);
        token.Should().Be(cts.Token);
    }

    [Theory]
    [InlineData("4fdf%", null, Mensagens.LogUidInvalido)]
    [InlineData(null, "abc", Mensagens.QuantidadeForaDoPadrao)]
    public async Task Filtro_invalido_nao_autoriza_nao_consulta_e_nao_registra(string? logUid, string? quantidade, string mensagem)
    {
        var resultado = await _handler.Handle(new Pesquisar.Query("tcmbd4-hml", null, logUid, quantidade), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Message.Should().Be(mensagem);
        _sequencia.Should().BeEmpty();
        _registros.Should().BeEmpty();
    }

    [Fact]
    public async Task Sem_servidor_e_recusado()
    {
        var resultado = await _handler.Handle(new Pesquisar.Query(null, null, null, null), CancellationToken.None);

        resultado.Error.Message.Should().Be(Mensagens.ServidorObrigatorio);
        _sequencia.Should().BeEmpty();
    }

    private void Autorizar(Result<Servidor, Erro> resultado) =>
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.AuditLog, It.IsAny<string>()))
            .Callback(() => _sequencia.Add("autorizacao"))
            .ReturnsAsync(resultado);

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
