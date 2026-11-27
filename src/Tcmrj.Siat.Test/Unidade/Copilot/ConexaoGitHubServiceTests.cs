using System.Security.Claims;
using AwesomeAssertions;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.Test.Unidade.Copilot;

/// <summary>Leitura da conexão a partir da identidade <c>GitHubConexao</c> do usuário do circuito (research.md R3).</summary>
public sealed class ConexaoGitHubServiceTests
{
    private static Task<ConexaoGitHub?> Obter(ClaimsPrincipal usuario)
    {
        var usuarioAtual = new Mock<IUsuarioAtualService>(MockBehavior.Strict);
        usuarioAtual.Setup(u => u.ObterAsync()).ReturnsAsync(usuario);
        return new ConexaoGitHubService(usuarioAtual.Object).ObterAsync();
    }

    private static ClaimsPrincipal ComConexao(string id, string login, string tipo = ConexaoGitHubService.TipoDeAutenticacao)
    {
        var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim("matricula", "123")], "Teste"));
        usuario.AddIdentity(new ClaimsIdentity([new Claim("github_id", id), new Claim("github_login", login)], tipo));
        return usuario;
    }

    [Fact]
    public async Task Devolve_a_conexao_da_identidade()
    {
        (await Obter(ComConexao("7", "ana-m"))).Should().Be(new ConexaoGitHub(7, "ana-m"));
    }

    [Theory]
    [InlineData("abc", "ana-m")]
    [InlineData("-7", "ana-m")]
    [InlineData("7", " ")]
    public async Task Id_que_nao_e_numero_ou_login_vazio_e_sem_conexao(string id, string login)
    {
        (await Obter(ComConexao(id, login))).Should().BeNull();
    }

    [Fact]
    public async Task Sem_a_identidade_e_sem_conexao()
    {
        (await Obter(new ClaimsPrincipal(new ClaimsIdentity([new Claim("github_id", "7"), new Claim("github_login", "ana")], "Teste"))))
            .Should().BeNull();
        (await Obter(ComConexao("7", "ana", tipo: "Outro"))).Should().BeNull();
    }
}
