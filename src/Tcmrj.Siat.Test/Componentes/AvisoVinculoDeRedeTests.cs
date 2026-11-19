using System.Security.Claims;
using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.Web.Components.Pages;
using Tcmrj.Siat.Web.Components.Shared;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// PROTÓTIPO (features/prototipo_login_rede): sem vínculo com a matrícula, a tela explica o motivo em vez de pedir um
/// recurso do UserManager que não resolveria nada.
/// </summary>
public sealed class AvisoVinculoDeRedeTests : BunitContext
{
    [Theory]
    [InlineData(VinculoDeRede.DominioInvalido, "não é do domínio do TCMRio")]
    [InlineData(VinculoDeRede.NaoEncontrado, "não está habilitado no UserManager")]
    [InlineData(VinculoDeRede.Indisponivel, "Não foi possível verificar o seu acesso")]
    public void Cada_situacao_tem_titulo_proprio_em_alerta(string situacao, string trechoDoTitulo)
    {
        var aviso = Render<AvisoVinculoDeRede>(p => p
            .Add(c => c.Situacao, situacao)
            .Add(c => c.UsuarioDeRede, @"TCMNET\fulano"));

        var alerta = aviso.Find("[role=alert]");
        alerta.QuerySelector("strong")!.TextContent.Should().Contain(trechoDoTitulo);
    }

    [Fact]
    public void Mensagem_cita_o_usuario_de_rede()
    {
        var aviso = Render<AvisoVinculoDeRede>(p => p
            .Add(c => c.Situacao, VinculoDeRede.NaoEncontrado)
            .Add(c => c.UsuarioDeRede, @"TCMNET\fulano"));

        aviso.Markup.Should().Contain(@"TCMNET\fulano");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("outra-coisa")]
    public void Situacao_desconhecida_nao_mostra_nada(string? situacao)
    {
        var aviso = Render<AvisoVinculoDeRede>(p => p.Add(c => c.Situacao, situacao));

        aviso.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void Acesso_negado_sem_vinculo_mostra_o_motivo_e_nao_o_recurso()
    {
        var autorizacao = AddAuthorization();
        autorizacao.SetAuthorized(@"TCMNET\fulano");
        autorizacao.SetClaims(new Claim(VinculoDeRede.TipoClaim, VinculoDeRede.NaoEncontrado));

        var pagina = Render<AcessoNegado>(p => p.Add(c => c.Recurso, "MailQueue.CanSelect"));

        pagina.Markup.Should().Contain("não está habilitado no UserManager");
        pagina.Markup.Should().NotContain("MailQueue.CanSelect");
    }

    [Fact]
    public void Acesso_negado_com_vinculo_continua_pedindo_o_recurso()
    {
        var autorizacao = AddAuthorization();
        autorizacao.SetAuthorized(@"TCMNET\fulano");
        autorizacao.SetClaims(new Claim("matricula", "12345"));

        var pagina = Render<AcessoNegado>(p => p.Add(c => c.Recurso, "MailQueue.CanSelect"));

        pagina.Find(".mono").TextContent.Should().Be("MailQueue.CanSelect");
    }
}
