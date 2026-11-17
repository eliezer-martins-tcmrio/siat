using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Tcmrj.Siat.App.Web.Features.Servidores;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Test.Unidade;

public class ListarPermitidosTests
{
    private static readonly Servidor[] Lista =
    [
        new() { Chave = "d1", Nome = "D1", Ambiente = Ambiente.Dsv },
        new() { Chave = "p1", Nome = "P1", Ambiente = Ambiente.Prd },
        new() { Chave = "h1", Nome = "H1", Ambiente = Ambiente.Hml },
        new() { Chave = "d2", Nome = "D2", Ambiente = Ambiente.Dsv },
        new() { Chave = "p2", Nome = "P2", Ambiente = Ambiente.Prd },
    ];

    private static (ListarPermitidos.Handler Handler, Mock<IAuthorizationService> Auth) Criar(params string[] politicasPermitidas)
    {
        var repo = new Mock<IServidoresRepository>();
        repo.Setup(r => r.Listar()).Returns(Lista);

        var usuario = new Mock<IUsuarioAtualService>();
        usuario.Setup(u => u.ObterAsync()).ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        var auth = new Mock<IAuthorizationService>();
        auth.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string politica) =>
                politicasPermitidas.Contains(politica) ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        return (new ListarPermitidos.Handler(repo.Object, auth.Object, usuario.Object), auth);
    }

    [Fact]
    public async Task Usuario_so_com_recurso_comum_nao_recebe_servidores_de_prd()
    {
        var (handler, _) = Criar("MailQueue.CanSelect");

        var grupos = await handler.Handle(new ListarPermitidos.Query(Modulo.MailQueue), CancellationToken.None);

        grupos.Select(g => g.Ambiente).Should().Equal(Ambiente.Dsv, Ambiente.Hml);
        grupos[0].Servidores.Select(s => s.Chave).Should().Equal("d1", "d2");
    }

    [Fact]
    public async Task Usuario_com_os_dois_recursos_recebe_todos_agrupados_na_ordem_dos_ambientes()
    {
        var (handler, auth) = Criar("AuditLog.CanSelect", "AuditLogPRD.CanSelect");

        var grupos = await handler.Handle(new ListarPermitidos.Query(Modulo.AuditLog), CancellationToken.None);

        grupos.Select(g => g.Ambiente).Should().Equal(Ambiente.Dsv, Ambiente.Hml, Ambiente.Prd);
        grupos[2].Servidores.Select(s => s.Chave).Should().Equal("p1", "p2");
        auth.Verify(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "AuditLogPRD.CanSelect"), Times.Once);
    }

    [Fact]
    public async Task Sem_nenhuma_permissao_devolve_lista_vazia()
    {
        var (handler, _) = Criar();

        var grupos = await handler.Handle(new ListarPermitidos.Query(Modulo.MailQueue), CancellationToken.None);

        grupos.Should().BeEmpty();
    }
}
