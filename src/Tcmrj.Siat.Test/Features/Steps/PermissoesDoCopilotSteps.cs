using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Reqnroll;
using Tcmrj.Siat.App.Web.Features.Copilot;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Test.Unidade.Copilot;

namespace Tcmrj.Siat.Test.Features.Steps;

/// <summary>
/// Próprio consumo do Copilot por recurso e conexão (specs/005-copilot-meu-consumo, FR-001, FR-004 e FR-029): os
/// handlers reais com o <see cref="ConsumoCopilotService"/> real sobre o repositório em memória, que registra as contas
/// consultadas no GitHub.
/// </summary>
[Binding]
public sealed class PermissoesDoCopilotSteps : IDisposable
{
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly Mock<IConexaoGitHubService> _conexoes = new(MockBehavior.Strict);
    private readonly RepositorioCopilotFalso _repositorio = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 27, 13, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _provedor;

    private Result<string, Erro>[] _respostas = [];

    public PermissoesDoCopilotSteps()
    {
        _provedor = new ServiceCollection().AddSingleton<IGitHubCopilotRepository>(_repositorio).BuildServiceProvider();
    }

    public void Dispose() => _provedor.Dispose();

    [Given(@"um usuário com o recurso CopilotProprio ""(.*)"" e a conexão ""(.*)""")]
    public void DadoUmUsuario(string recurso, string conexao)
    {
        _autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.CopilotProprio)).ReturnsAsync(recurso == "sim");
        _conexoes.Setup(c => c.ObterAsync()).ReturnsAsync(conexao == "nenhuma" ? null : new ConexaoGitHub(7, "ana-m"));
        if (conexao == "ex-membro")
        {
            _repositorio.Associacao = login => Erros.Copilot.NaoMembro(login, RepositorioCopilotFalso.Org);
        }
    }

    [When(@"ele pede a pessoa, o consumo do período e o uso do limite do Copilot")]
    public async Task QuandoElePede()
    {
        var token = TestContext.Current.CancellationToken;
        var monitor = new Mock<IOptionsMonitor<CacheDoCopilotOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new CacheDoCopilotOptions { Organizacao = RepositorioCopilotFalso.Org });
        var servico = new ConsumoCopilotService(
            _provedor.GetRequiredService<IServiceScopeFactory>(), monitor.Object, _relogio, NullLogger<ConsumoCopilotService>.Instance);

        var pessoa = await new ObterPessoaCopilot.Handler(_autorizacao.Object, _conexoes.Object, servico, monitor.Object)
            .Handle(new ObterPessoaCopilot.Query(false), token);
        var periodo = await new ObterConsumoDoPeriodo.Handler(_autorizacao.Object, _conexoes.Object, servico, _relogio)
            .Handle(new ObterConsumoDoPeriodo.Query("2026-11-10", "2026-11-12", false), token);
        var uso = await new ObterUsoDoLimite.Handler(_autorizacao.Object, _conexoes.Object, servico, _relogio)
            .Handle(new ObterUsoDoLimite.Query(false), token);

        _respostas =
        [
            pessoa.Map(p => p.Login),
            periodo.Map(p => p.Total.ValorBruto.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            uso.Map(u => u.Faixa.ToString()),
        ];
    }

    [Then(@"as três consultas do Copilot respondem ""(.*)""")]
    public void EntaoAsConsultasRespondem(string resultado)
    {
        var codigo = resultado switch
        {
            "acesso negado" => Erros.Copilot.CodigoAcessoNegado,
            "sem conexão" => Erros.Copilot.CodigoSemConexao,
            "não membro" => Erros.Copilot.CodigoNaoMembro,
            _ => null,
        };

        if (codigo is null)
        {
            _respostas.Should().OnlyContain(r => r.IsSuccess);
            _respostas[0].Value.Should().Be("ana-m");
            _respostas[1].Value.Should().Be("3.00", "três dias de US$ 1.00 da própria conta");
            return;
        }

        _respostas.Should().OnlyContain(r => r.IsFailure && r.Error.Code == codigo);
    }

    [Then(@"o GitHub foi consultado só para a conta ""(.*)""")]
    public void EntaoOGitHubFoiConsultadoSoPara(string contas)
    {
        if (contas == "nenhuma")
        {
            _repositorio.Chamadas.Should().BeEmpty();
            return;
        }

        var porConta = _repositorio.Chamadas
            .Where(c => c.StartsWith("associacao:", StringComparison.Ordinal) || c.StartsWith("relatorio:", StringComparison.Ordinal))
            .Select(c => c.Split(':')[1])
            .Distinct();
        porConta.Should().Equal(contas);
        _repositorio.Chamadas.Where(c => c.StartsWith("perfil:", StringComparison.Ordinal)).Should().OnlyContain(c => c == "perfil:7");
    }
}
