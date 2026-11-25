using System.Reflection;
using System.Security.Claims;
using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Web.Components.Pages;
using Tcmrj.Siat.Web.Components.Shared;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>
/// Página Início (.specs/features/pagina-home, S1): atalhos das ferramentas permitidas, agrupados e na ordem do menu,
/// sem levar o usuário a outra rota; sem nenhuma, a mensagem de falta de acesso.
/// </summary>
public sealed class HomeTests : BunitContext
{
    private readonly Mock<IAutorizacaoConsultaService> _autorizacao = new(MockBehavior.Strict);
    private readonly BunitAuthorizationContext _autenticacao;

    public HomeTests()
    {
        Services.AddSingleton(_autorizacao.Object);
        _autenticacao = AddAuthorization();
        _autenticacao.SetAuthorized("usuario.teste");
    }

    private void Permitir(params Modulo[] modulos)
    {
        foreach (var modulo in Enum.GetValues<Modulo>())
        {
            _autorizacao.Setup(a => a.PodeAcessarModuloAsync(modulo)).ReturnsAsync(modulos.Contains(modulo));
        }
    }

    private BunitNavigationManager Navegacao => Services.GetRequiredService<BunitNavigationManager>();

    private static string[] Grupos(IRenderedComponent<Home> pagina) =>
        [.. pagina.FindAll(".inicio-grupo > h2").Select(h => h.TextContent.Trim())];

    private static string[] Rotulos(IRenderedComponent<Home> pagina) =>
        [.. pagina.FindAll(".inicio-grupo .telas a strong").Select(s => s.TextContent.Trim())];

    /// <summary>Critério 1.</summary>
    [Fact]
    public void Com_ferramentas_fica_no_Inicio_sem_navegar()
    {
        Permitir(Modulo.Catalogo_Sistemas, Modulo.MailQueue, Modulo.AuditLog);

        var pagina = Render<Home>();

        Navegacao.History.Should().BeEmpty();
        Navegacao.Uri.Should().Be(Navegacao.BaseUri);
        pagina.Find("h1").TextContent.Should().Be("Início");
        pagina.Find(".cabecalho .sub").TextContent.Should().Be("Sistema Integrado de Apoio Técnico do TCMRio.");
        pagina.FindAll(".inicio-grupo .telas a").Should().HaveCount(3);
    }

    /// <summary>Critério 1, o caso forte: hoje esse usuário era levado para as Versões.</summary>
    [Fact]
    public void So_com_Versoes_fica_no_Inicio_com_um_atalho()
    {
        Permitir(Modulo.Versoes_Sistemas);

        var pagina = Render<Home>();

        Navegacao.History.Should().BeEmpty();
        Rotulos(pagina).Should().Equal("Versões");
    }

    /// <summary>Critério 1: a aba é "Início · SIAT".</summary>
    [Fact]
    public void A_aba_se_chama_Inicio_SIAT()
    {
        Permitir(Modulo.MailQueue);

        var pagina = Render<Home>();

        pagina.FindComponent<CabecalhoPagina>().Instance.Titulo.Should().Be("Início");
    }

    /// <summary>Critério 2.</summary>
    [Fact]
    public void Com_todas_agrupa_em_Sistemas_e_Consultas_na_ordem_do_menu()
    {
        Permitir(Enum.GetValues<Modulo>());

        var pagina = Render<Home>();

        Grupos(pagina).Should().Equal("Sistemas", "Consultas");
        var grupos = pagina.FindAll(".inicio-grupo");
        grupos[0].QuerySelectorAll(".telas a strong").Select(s => s.TextContent.Trim()).Should().Equal("Catálogo", "Versões");
        grupos[1].QuerySelectorAll(".telas a strong").Select(s => s.TextContent.Trim()).Should().Equal("MailQueue", "AuditLog");
    }

    /// <summary>Critério 2: grupo sem ferramenta permitida não aparece.</summary>
    [Fact]
    public void So_com_MailQueue_mostra_apenas_o_grupo_Consultas()
    {
        Permitir(Modulo.MailQueue);

        var pagina = Render<Home>();

        Grupos(pagina).Should().Equal("Consultas");
        Rotulos(pagina).Should().Equal("MailQueue");
    }

    /// <summary>Critério 2.</summary>
    [Fact]
    public void Com_Catalogo_e_AuditLog_mostra_um_atalho_em_cada_grupo()
    {
        Permitir(Modulo.Catalogo_Sistemas, Modulo.AuditLog);

        var pagina = Render<Home>();

        Grupos(pagina).Should().Equal("Sistemas", "Consultas");
        Rotulos(pagina).Should().Equal("Catálogo", "AuditLog");
    }

    /// <summary>Critério 3: o que não tem permissão não aparece de forma nenhuma.</summary>
    [Fact]
    public void Sem_AuditLog_a_pagina_nao_cita_AuditLog()
    {
        Permitir(Modulo.Catalogo_Sistemas, Modulo.Versoes_Sistemas, Modulo.MailQueue);

        var pagina = Render<Home>();

        pagina.Markup.Should().NotContain("AuditLog").And.NotContain("consultas/auditlog");
    }

    /// <summary>Critério 3.</summary>
    [Fact]
    public void So_com_Catalogo_a_pagina_nao_cita_as_outras_ferramentas()
    {
        Permitir(Modulo.Catalogo_Sistemas);

        var pagina = Render<Home>();

        pagina.Markup.Should().NotContain("Consultas").And.NotContain("MailQueue").And.NotContain("AuditLog")
            .And.NotContain("Versões");
    }

    /// <summary>Critérios 4 e 5: rótulo, descrição e destino de cada atalho.</summary>
    [Fact]
    public void Cada_atalho_tem_rotulo_descricao_e_rota_da_ferramenta()
    {
        Permitir(Enum.GetValues<Modulo>());

        var pagina = Render<Home>();

        pagina.FindAll(".inicio-grupo .telas a")
            .Select(a => (a.QuerySelector("strong")!.TextContent.Trim(), a.QuerySelector("span")!.TextContent,
                a.GetAttribute("href")))
            .Should().Equal(
                ("Catálogo", "Onde cada sistema roda: URL, máquina e caminho por ambiente.", "sistemas/catalogo"),
                ("Versões", "Versão de cada sistema em DSV, HML e PRD, nas duas máquinas de cada ambiente.",
                    "sistemas/versoes"),
                ("MailQueue", "E-mails enfileirados pelos sistemas, do mais recente para o mais antigo.",
                    "consultas/mailqueue"),
                ("AuditLog", "Registros de auditoria (AUDITORIADB · audit.Log), do mais recente para o mais antigo.",
                    "consultas/auditlog"));
    }

    /// <summary>Critério 5: o atalho é um link comum, que abre na mesma aba.</summary>
    [Fact]
    public void Atalho_abre_na_mesma_aba()
    {
        Permitir(Modulo.MailQueue);

        var pagina = Render<Home>();

        pagina.Find(".inicio-grupo .telas a").HasAttribute("target").Should().BeFalse();
    }

    /// <summary>Critério 6.</summary>
    [Fact]
    public void Sem_nenhum_modulo_explica_a_quem_pedir_acesso_citando_sistemas_e_consultas()
    {
        Permitir();

        var pagina = Render<Home>();

        Navegacao.History.Should().BeEmpty();
        pagina.Find("h1").TextContent.Should().Be("Início");
        pagina.FindAll(".inicio-grupo").Should().BeEmpty();
        var texto = pagina.Find(".estado").TextContent;
        texto.Should().Contain("Você não tem acesso às ferramentas do SIAT").And.Contain("Sistemas")
            .And.Contain("catálogo e versões de sistemas").And.Contain("Consultas")
            .And.Contain("MailQueue").And.Contain("AuditLog");
    }

    /// <summary>Critério 6: sem matrícula, o aviso de vínculo de rede no lugar da mensagem.</summary>
    [Fact]
    public void Sem_nenhum_modulo_e_sem_matricula_mostra_o_aviso_de_vinculo_de_rede()
    {
        _autenticacao.SetAuthorized(@"TCMNET\fulano");
        _autenticacao.SetClaims(new Claim(VinculoDeRede.TipoClaim, VinculoDeRede.NaoEncontrado));
        Permitir();

        var pagina = Render<Home>();

        pagina.Markup.Should().Contain("não está habilitado no UserManager").And.Contain(@"TCMNET\fulano");
        pagina.FindAll(".estado").Should().BeEmpty();
        pagina.FindAll(".inicio-grupo").Should().BeEmpty();
    }

    /// <summary>Critério 7: enquanto as permissões são verificadas, nem atalhos nem a mensagem de falta de acesso.</summary>
    [Fact]
    public void Durante_a_verificacao_nao_mostra_atalhos_nem_falta_de_acesso()
    {
        var pendente = new TaskCompletionSource<bool>();
        foreach (var modulo in Enum.GetValues<Modulo>())
        {
            _autorizacao.Setup(a => a.PodeAcessarModuloAsync(modulo)).Returns(pendente.Task);
        }

        var pagina = Render<Home>();

        pagina.FindAll(".estado").Should().BeEmpty();
        pagina.FindAll(".inicio-grupo").Should().BeEmpty();

        pendente.SetResult(false);

        pagina.WaitForElement(".estado");
    }

    /// <summary>Critério 8: só autenticação, e nenhum acesso a fonte de dados (nada além da verificação de permissão).</summary>
    [Fact]
    public void Exige_so_autenticacao_e_nao_le_fonte_de_dados()
    {
        var autorizacoes = typeof(Home).GetCustomAttributes<AuthorizeAttribute>().ToList();
        autorizacoes.Should().ContainSingle();
        autorizacoes[0].Policy.Should().BeNull();
        autorizacoes[0].Roles.Should().BeNull();

        typeof(Home).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(p => p.IsDefined(typeof(InjectAttribute)))
            .Select(p => p.PropertyType)
            .Should().Equal(typeof(IAutorizacaoConsultaService));
    }
}
