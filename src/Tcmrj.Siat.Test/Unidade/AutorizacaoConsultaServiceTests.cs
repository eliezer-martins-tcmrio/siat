using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade;

public class AutorizacaoConsultaServiceTests
{
    private readonly HashSet<string> _permitidas = [];
    private readonly Mock<IRegistroConsultaService> _registro = new(MockBehavior.Strict);
    private readonly AutorizacaoConsultaService _servico;

    public AutorizacaoConsultaServiceTests()
    {
        var repo = new Mock<IServidoresRepository>();
        repo.Setup(r => r.ObterPorChave("prd1")).Returns(new Servidor { Chave = "prd1", Nome = "P", Ambiente = Ambiente.Prd });
        repo.Setup(r => r.ObterPorChave("dsv1")).Returns(new Servidor { Chave = "dsv1", Nome = "D", Ambiente = Ambiente.Dsv });

        var usuario = new Mock<IUsuarioAtualService>();
        usuario.Setup(u => u.ObterAsync()).ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        var auth = new Mock<IAuthorizationService>();
        auth.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string politica) =>
                _permitidas.Contains(politica) ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        _servico = new AutorizacaoConsultaService(repo.Object, auth.Object, usuario.Object);
    }

    [Fact]
    public async Task Autorizado_devolve_o_servidor()
    {
        _permitidas.Add("MailQueuePRD.CanSelect");

        var resultado = await _servico.AutorizarAsync(Modulo.MailQueue, "prd1");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Chave.Should().Be("prd1");
    }

    [Fact]
    public async Task Negado_devolve_acesso_negado_sem_registrar()
    {
        _permitidas.Add("MailQueue.CanSelect");

        var resultado = await _servico.AutorizarAsync(Modulo.MailQueue, "prd1");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
        _registro.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Chave_inexistente_devolve_servidor_nao_encontrado()
    {
        var resultado = await _servico.AutorizarAsync(Modulo.AuditLog, "nao-existe");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Consultas.CodigoServidorNaoEncontrado);
        _registro.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Permissao_removida_entre_duas_chamadas_nega_a_segunda()
    {
        _permitidas.Add("AuditLog.CanSelect");

        var primeira = await _servico.AutorizarAsync(Modulo.AuditLog, "dsv1");
        _permitidas.Clear();
        var segunda = await _servico.AutorizarAsync(Modulo.AuditLog, "dsv1");

        primeira.IsSuccess.Should().BeTrue();
        segunda.IsFailure.Should().BeTrue();
        segunda.Error.Code.Should().Be(Erros.Consultas.CodigoAcessoNegado);
    }

    [Fact]
    public async Task PodeAcessarModulo_e_verdadeiro_com_qualquer_uma_das_politicas()
    {
        (await _servico.PodeAcessarModuloAsync(Modulo.MailQueue)).Should().BeFalse();

        _permitidas.Add("MailQueuePRD.CanSelect");

        (await _servico.PodeAcessarModuloAsync(Modulo.MailQueue)).Should().BeTrue();
    }
}
