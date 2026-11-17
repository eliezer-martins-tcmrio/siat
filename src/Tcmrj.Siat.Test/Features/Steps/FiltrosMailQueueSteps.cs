using AwesomeAssertions;
using Reqnroll;
using Tcmrj.Siat.App.Web.Features.MailQueue;
using Tcmrj.Siat.App.Web.Validators;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Test.Features.Steps;

/// <summary>Passos de <c>FiltrosMailQueue.feature</c>: validação da query e montagem do <see cref="FiltroMailQueue"/>.</summary>
[Binding]
[Scope(Feature = "Filtros da MailQueue")]
public sealed class FiltrosMailQueueSteps
{
    private string? _servidor = "tcmbd4-hml";
    private int? _smtpId;
    private string? _texto;
    private string? _quantidade;
    private FiltroMailQueue? _filtro;
    private string? _mensagemRecusa;

    [Given(@"o texto de busca ""(.*)""")]
    public void DadoOTextoDeBusca(string texto) => _texto = texto;

    [Given(@"um texto de busca com (\d+) caracteres")]
    public void DadoUmTextoComCaracteres(int tamanho) => _texto = new string('a', tamanho);

    [Given(@"nenhum SMTP escolhido")]
    public void DadoNenhumSmtp() => _smtpId = null;

    [Given(@"o SMTP (\d+)")]
    public void DadoOSmtp(int smtpId) => _smtpId = smtpId;

    [Given(@"a quantidade ""(.*)""")]
    public void DadoAQuantidade(string quantidade) => _quantidade = quantidade;

    [Given(@"nenhum servidor escolhido")]
    public void DadoNenhumServidor() => _servidor = null;

    [When(@"monto o filtro da MailQueue")]
    public void QuandoMontoOFiltro()
    {
        var query = new Pesquisar.Query(_servidor, _smtpId, _texto, _quantidade);
        var validacao = new PesquisarMailQueueQueryValidator().Validate(query);
        if (!validacao.IsValid)
        {
            _mensagemRecusa = validacao.Errors[0].ErrorMessage;
            return;
        }

        var filtro = Pesquisar.MontarFiltro(query);
        if (filtro.IsFailure)
        {
            _mensagemRecusa = filtro.Error.Message;
            return;
        }

        _filtro = filtro.Value;
    }

    [Then(@"o filtro é aceito")]
    public void EntaoOFiltroEAceito()
    {
        _mensagemRecusa.Should().BeNull();
        _filtro.Should().NotBeNull();
    }

    [Then(@"o filtro é recusado com a mensagem ""(.*)""")]
    public void EntaoOFiltroERecusado(string mensagem)
    {
        _filtro.Should().BeNull();
        _mensagemRecusa.Should().Be(mensagem);
    }

    [Then(@"o texto do filtro é ""(.*)""")]
    public void EntaoOTextoDoFiltroE(string texto) => _filtro!.Texto.Should().Be(texto);

    [Then(@"o filtro não tem texto")]
    public void EntaoOFiltroNaoTemTexto() => _filtro!.Texto.Should().BeNull();

    [Then(@"o filtro vale para todos os SMTPs")]
    public void EntaoTodosOsSmtps()
    {
        _filtro!.SmtpId.Should().BeNull();
        _filtro.TemFiltros.Should().BeFalse();
    }

    [Then(@"o SMTP do filtro é (\d+)")]
    public void EntaoOSmtpDoFiltroE(int smtpId) => _filtro!.SmtpId.Should().Be(smtpId);

    [Then(@"a quantidade do filtro é (\d+)")]
    public void EntaoAQuantidadeDoFiltroE(int quantidade) => _filtro!.Quantidade.Should().Be(quantidade);
}
