using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Reqnroll;
using Tcmrj.Siat.App.Web.Features.AuditLog;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.Validators;
using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Test.Unidade.AuditLog;

namespace Tcmrj.Siat.Test.Features.Steps;

/// <summary>
/// Passos de <c>FiltrosAuditLog.feature</c>: a consulta passa pelo handler real (validação, autorização e
/// registro) com um repositório falso, para conferir o que chega ao banco e o que é recusado antes dele.
/// </summary>
[Binding]
[Scope(Feature = "Filtros do AuditLog")]
public sealed class FiltrosAuditLogSteps
{
    private readonly AuditLogRepositoryFalso _repositorio = new();
    private string? _servidor;
    private string? _appId;
    private string? _prefixoLogUid;
    private string? _quantidade;
    private string? _dias;
    private string? _de;
    private string? _ate;
    private Result<ResultadoConsultaVM<RegistroAuditoriaVM>, Erro> _resultado;

    [Given(@"uma consulta ao AuditLog no servidor ""(.*)""")]
    public void DadoUmaConsultaNoServidor(string servidor) => _servidor = servidor;

    [Given(@"o início do LogUID ""(.*)""")]
    public void DadoOInicioDoLogUid(string prefixo) => _prefixoLogUid = prefixo;

    [Given(@"a quantidade ""(.*)"" no AuditLog")]
    public void DadoAQuantidade(string quantidade) => _quantidade = quantidade;

    [Given(@"os dias ""(.*)"" no AuditLog")]
    public void DadoOsDias(string dias) => _dias = dias;

    [Given(@"o período de ""(.*)"" a ""(.*)"" no AuditLog")]
    public void DadoOPeriodo(string de, string ate) => (_de, _ate) = (de, ate);

    [Given(@"a aplicação ""(.*)"" no AuditLog")]
    public void DadoAAplicacao(string appId) => _appId = appId;

    [When(@"a consulta ao AuditLog é feita")]
    public async Task QuandoAConsultaEFeita()
    {
        var servidor = new Servidor { Chave = _servidor!, Nome = "TCMBD4\\HML", Ambiente = Ambiente.Hml };
        var autorizacao = new Mock<IAutorizacaoConsultaService>();
        autorizacao.Setup(a => a.AutorizarAsync(Modulo.AuditLog, _servidor!))
            .ReturnsAsync(Result.Success<Servidor, Erro>(servidor));

        var handler = new Pesquisar.Handler(
            new PesquisarAuditLogQueryValidator(),
            autorizacao.Object,
            Mock.Of<IServidoresRepository>(),
            _repositorio,
            Mock.Of<IRegistroConsultaService>(),
            TimeProvider.System);

        _resultado = await handler.Handle(
            new Pesquisar.Query(_servidor, _appId, _prefixoLogUid, _quantidade, _dias, _de, _ate), CancellationToken.None);
    }

    [Then(@"o AuditLog é consultado com o início do LogUID ""(.*)""")]
    public void EntaoConsultadoComOPrefixo(string prefixo) => FiltroEnviado().PrefixoLogUid.Should().Be(prefixo);

    [Then(@"o AuditLog é consultado sem início de LogUID")]
    public void EntaoConsultadoSemPrefixo() => FiltroEnviado().PrefixoLogUid.Should().BeNull();

    [Then(@"o AuditLog é consultado com a quantidade (\d+)")]
    public void EntaoConsultadoComAQuantidade(int quantidade) => FiltroEnviado().Quantidade.Should().Be(quantidade);

    [Then(@"o AuditLog é consultado em todas as aplicações")]
    public void EntaoConsultadoEmTodasAsAplicacoes()
    {
        var filtro = FiltroEnviado();
        filtro.AppId.Should().BeNull();
        filtro.SemAppId.Should().BeFalse();
    }

    [Then(@"o AuditLog é consultado só nos registros sem AppID")]
    public void EntaoConsultadoSemAppId()
    {
        var filtro = FiltroEnviado();
        filtro.AppId.Should().BeNull();
        filtro.SemAppId.Should().BeTrue();
    }

    [Then(@"o AuditLog é consultado nos últimos (\d+) dias")]
    public void EntaoConsultadoNosUltimosDias(int dias)
    {
        var filtro = FiltroEnviado();
        filtro.Dias.Should().Be(dias);
        filtro.De.Should().BeNull();
        filtro.Ate.Should().BeNull();
    }

    [Then(@"o AuditLog é consultado de ""(.*)"" a ""(.*)"" sem os dias")]
    public void EntaoConsultadoNoPeriodo(string de, string ate)
    {
        var filtro = FiltroEnviado();
        filtro.Dias.Should().BeNull();
        filtro.De.Should().Be(string.IsNullOrEmpty(de) ? null : DateOnly.Parse(de, System.Globalization.CultureInfo.InvariantCulture));
        filtro.Ate.Should().Be(string.IsNullOrEmpty(ate) ? null : DateOnly.Parse(ate, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Then(@"o AuditLog é consultado na aplicação (\d+)")]
    public void EntaoConsultadoNaAplicacao(int appId) => FiltroEnviado().AppId.Should().Be(appId);

    [Then(@"a consulta ao AuditLog é recusada com a mensagem ""(.*)""")]
    public void EntaoRecusadaComAMensagem(string mensagem)
    {
        _resultado.IsFailure.Should().BeTrue();
        _resultado.Error.Message.Should().Be(mensagem);
    }

    [Then(@"o banco de auditoria não é consultado")]
    public void EntaoOBancoNaoEConsultado() => _repositorio.Pesquisas.Should().BeEmpty();

    private FiltroAuditLog FiltroEnviado()
    {
        _resultado.IsSuccess.Should().BeTrue(_resultado.IsFailure ? _resultado.Error.Message : null);
        return _repositorio.Pesquisas.Should().ContainSingle().Subject.Filtro;
    }
}
