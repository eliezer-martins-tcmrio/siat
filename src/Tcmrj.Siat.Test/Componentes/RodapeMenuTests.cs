using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Web.Components.Layout;
using Tcmrj.Siat.Web.Configurations;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Rodapé do menu: "Sair" só existe na entrada pelo SIA (OIDC). Com o usuário de rede não há sessão própria para
/// encerrar, e Account/Logout nem é mapeado.
/// </summary>
public sealed class RodapeMenuTests : BunitContext
{
    private IRenderedComponent<RodapeMenu> RenderizarCom(ModoDeAutenticacao modo)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(Options.Create(new SiatOptions { Autenticacao = modo }));
        AddAuthorization().SetAuthorized("usuario.teste");

        return Render<RodapeMenu>();
    }

    [Fact]
    public void No_OIDC_mostra_Sair_com_POST_para_Account_Logout()
    {
        var rodape = RenderizarCom(ModoDeAutenticacao.Oidc);

        var formulario = rodape.Find("form");
        formulario.GetAttribute("action").Should().Be("Account/Logout");
        formulario.GetAttribute("method").Should().Be("post");
        formulario.QuerySelector("button")!.TextContent.Trim().Should().Be("Sair");
    }

    [Fact]
    public void Com_usuario_de_rede_nao_mostra_Sair()
    {
        var rodape = RenderizarCom(ModoDeAutenticacao.UsuarioDeRede);

        rodape.FindAll("form").Should().BeEmpty();
        rodape.Markup.Should().NotContain("Sair");
    }
}
