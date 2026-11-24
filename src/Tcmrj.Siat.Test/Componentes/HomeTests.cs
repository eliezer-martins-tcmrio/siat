using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Web.Components.Pages;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Página inicial (contracts/rotas-ui.md): vai para Sistemas, depois para a primeira Consulta permitida.</summary>
public sealed class HomeTests : BunitContext
{
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new();

    public HomeTests()
    {
        Services.AddSingleton(_autorizacao.Object);
        var autenticacao = AddAuthorization();
        autenticacao.SetAuthorized("usuario.teste");
    }

    private void Permitir(params Modulo[] modulos)
    {
        foreach (var modulo in Enum.GetValues<Modulo>())
        {
            _autorizacao.Setup(a => a.PodeAcessarModuloAsync(modulo)).ReturnsAsync(modulos.Contains(modulo));
        }
    }

    private BunitNavigationManager Navegacao => Services.GetRequiredService<BunitNavigationManager>();

    [Fact]
    public void Com_Sistemas_vai_para_o_catalogo_substituindo_o_historico()
    {
        Permitir(Modulo.Sistemas, Modulo.MailQueue, Modulo.AuditLog);

        Render<Home>();

        Navegacao.Uri.Should().EndWith("/catalogo/sistemas");
        Navegacao.History.First().Options.ReplaceHistoryEntry.Should().BeTrue();
    }

    [Fact]
    public void So_com_MailQueue_vai_para_a_MailQueue()
    {
        Permitir(Modulo.MailQueue);

        Render<Home>();

        Navegacao.Uri.Should().EndWith("/consultas/mailqueue");
    }

    [Fact]
    public void Sem_nenhum_modulo_explica_a_quem_pedir_acesso_citando_o_catalogo_e_as_consultas()
    {
        Permitir();

        var pagina = Render<Home>();

        Navegacao.History.Should().BeEmpty();
        var texto = pagina.Find(".estado").TextContent;
        texto.Should().Contain("Catalogo").And.Contain("catálogo de sistemas").And.Contain("Consultas")
            .And.Contain("MailQueue").And.Contain("AuditLog");
    }
}
