using System.Reflection;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Dependências falsas comuns aos handlers do Copilot (specs/005-copilot-meu-consumo, T036).</summary>
public abstract class HandlersCopilotBase
{
    protected static readonly DateTimeOffset Agora = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);

    protected static readonly ConexaoGitHub Conexao = new(7, "ana-m");

    protected Mock<IAutorizacaoConsultaService> Autorizacao { get; } = new();

    protected Mock<IConexaoGitHubService> Conexoes { get; } = new();

    protected Mock<IConsumoCopilotService> Consumo { get; } = new(MockBehavior.Strict);

    protected FakeTimeProvider Relogio { get; } = new(Agora);

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected HandlersCopilotBase()
    {
        Autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.CopilotProprio)).ReturnsAsync(true);
        Conexoes.Setup(c => c.ObterAsync()).ReturnsAsync(Conexao);
    }

    protected static IOptionsMonitor<CacheDoCopilotOptions> Opcoes()
    {
        var monitor = new Mock<IOptionsMonitor<CacheDoCopilotOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new CacheDoCopilotOptions { Organizacao = "tcmrj" });
        return monitor.Object;
    }

    protected void SemPolitica() =>
        Autorizacao.Setup(a => a.PodeAcessarModuloAsync(Modulo.CopilotProprio)).ReturnsAsync(false);

    protected void SemConexao() => Conexoes.Setup(c => c.ObterAsync()).ReturnsAsync((ConexaoGitHub?)null);

    protected static PessoaCopilot Pessoa(Result<LimiteVigente, Erro>? limite = null) => new(
        7, "ana-m", true, "Ana Souza", null, true,
        Result.Success<IReadOnlyList<TimeDaPessoa>, Erro>([]),
        limite ?? Result.Success<LimiteVigente, Erro>(new LimiteVigente.Padrao(50)));

    protected static RelatorioDeConsumo Relatorio(decimal bruto) =>
        new(new Consumo(bruto * 25, bruto, bruto / 2), [new ConsumoPorModelo("GPT-5", new Consumo(bruto * 25, bruto, bruto / 2))], Agora);

    /// <summary>Nenhuma propriedade de login na consulta: a conta vem só da conexão (FR-029).</summary>
    protected static IEnumerable<string> PropriedadesDeLogin(Type query) =>
        query.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => n.Contains("login", StringComparison.OrdinalIgnoreCase)
                || n.Contains("conta", StringComparison.OrdinalIgnoreCase)
                || n.Contains("usuario", StringComparison.OrdinalIgnoreCase)
                || n.Contains("github", StringComparison.OrdinalIgnoreCase));
}
