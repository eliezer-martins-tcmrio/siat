using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Test.Unidade;

public class RegistroConsultaServiceBasicoTests
{
    private static readonly RegistroConsulta Registro = new(
        OperacaoConsulta.Consulta, Modulo.MailQueue, "tcmbd4-hml", "TCMBD4\\HML", Ambiente.Hml,
        new { SmtpId = 3 }, 10, ResultadoRegistro.Sucesso);

    [Fact]
    public async Task Falha_do_logger_nao_se_propaga()
    {
        var logger = new Mock<ILogger<RegistroConsultaService>>();
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Throws(new InvalidOperationException("banco de log fora do ar"));
        var usuario = new Mock<IUsuarioAtualService>();
        usuario.Setup(u => u.ObterAsync()).ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        var servico = new RegistroConsultaService(usuario.Object, logger.Object);

        var acao = () => servico.RegistrarAsync(Registro);

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Falha_ao_obter_o_usuario_nao_se_propaga()
    {
        var usuario = new Mock<IUsuarioAtualService>();
        usuario.Setup(u => u.ObterAsync()).ThrowsAsync(new InvalidOperationException());

        var servico = new RegistroConsultaService(usuario.Object, Mock.Of<ILogger<RegistroConsultaService>>());

        var acao = () => servico.RegistrarAsync(Registro);

        await acao.Should().NotThrowAsync();
    }
}
