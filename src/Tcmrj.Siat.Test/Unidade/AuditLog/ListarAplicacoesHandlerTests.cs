using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Tcmrj.Siat.App.Web.Features.AuditLog;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Test.Unidade.AuditLog;

/// <summary>Lista de aplicações do AuditLog: autoriza antes; indisponível vira lista vazia com aviso.</summary>
public class ListarAplicacoesHandlerTests
{
    private static readonly Servidor Hml = new() { Chave = "tcmbd4-hml", Nome = "TCMBD4\\HML", Ambiente = Ambiente.Hml };

    private readonly List<string> _sequencia = [];
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly AuditLogRepositoryFalso _repositorio;
    private readonly ListarAplicacoes.Handler _handler;

    public ListarAplicacoesHandlerTests()
    {
        _repositorio = new AuditLogRepositoryFalso(_sequencia);
        Autorizar(Result.Success<Servidor, Erro>(Hml));
        _handler = new ListarAplicacoes.Handler(_autorizacao.Object, _repositorio);
    }

    [Fact]
    public async Task Devolve_as_aplicacoes_na_ordem_do_banco()
    {
        _repositorio.AoListarAplicacoes = () => Task.FromResult(Result.Success<IReadOnlyList<Aplicacao>, Erro>(
            [new Aplicacao(23, "Chamados e Serviços"), new Aplicacao(12, "eTCM")]));

        var resultado = await _handler.Handle(new ListarAplicacoes.Query("tcmbd4-hml"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Disponivel.Should().BeTrue();
        resultado.Value.Aviso.Should().BeNull();
        resultado.Value.Itens.Select(a => a.Id).Should().Equal(23, 12);
        resultado.Value.NomeDe(12).Should().Be("eTCM");
        resultado.Value.NomeDe(99).Should().BeNull();
    }

    [Fact]
    public async Task Autoriza_antes_de_consultar()
    {
        await _handler.Handle(new ListarAplicacoes.Query("tcmbd4-hml"), CancellationToken.None);

        _sequencia.Should().Equal("autorizacao", "repositorio");
    }

    [Fact]
    public async Task Acesso_negado_nao_consulta()
    {
        Autorizar(Result.Failure<Servidor, Erro>(Erros.Consultas.AcessoNegado));

        var resultado = await _handler.Handle(new ListarAplicacoes.Query("tcmbd4-prd"), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
        _repositorio.ListagensDeAplicacoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Lista_indisponivel_devolve_vazia_com_aviso()
    {
        _repositorio.AoListarAplicacoes = () => Task.FromResult(Result.Failure<IReadOnlyList<Aplicacao>, Erro>(
            Erros.Consultas.BancoInacessivel("TCMBD4\\HML", "CSLIB_UserManager")));

        var resultado = await _handler.Handle(new ListarAplicacoes.Query("tcmbd4-hml"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Itens.Should().BeEmpty();
        resultado.Value.Disponivel.Should().BeFalse();
        resultado.Value.Aviso.Should().Contain("lista de aplicações").And.Contain("CSLIB_UserManager");
    }

    [Fact]
    public async Task Cancelamento_devolve_Cancelada()
    {
        using var cts = new CancellationTokenSource();
        _repositorio.AoListarAplicacoes = () =>
        {
            cts.Cancel();
            return Task.FromResult(Result.Failure<IReadOnlyList<Aplicacao>, Erro>(Erros.Consultas.Cancelada));
        };

        var resultado = await _handler.Handle(new ListarAplicacoes.Query("tcmbd4-hml"), cts.Token);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoCancelada);
    }

    private void Autorizar(Result<Servidor, Erro> resultado) =>
        _autorizacao.Setup(a => a.AutorizarAsync(Modulo.AuditLog, It.IsAny<string>()))
            .Callback(() => _sequencia.Add("autorizacao"))
            .ReturnsAsync(resultado);
}
