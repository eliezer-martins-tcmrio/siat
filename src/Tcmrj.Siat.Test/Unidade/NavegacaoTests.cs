using AwesomeAssertions;
using Tcmrj.Siat.App.Web;

namespace Tcmrj.Siat.Test.Unidade;

public class NavegacaoTests
{
    [Fact]
    public void Sem_parametros_devolve_somente_a_rota()
    {
        Navegacao.MailQueue().Should().Be("consultas/mailqueue");
        Navegacao.AuditLog().Should().Be("consultas/auditlog");
        Navegacao.Home.Should().BeEmpty();
    }

    [Fact]
    public void MailQueue_monta_parametros_na_ordem_e_codifica_valores()
    {
        Navegacao.MailQueue("tcmbd4-hml", 3, "boleto & cia", 100, "5F0A")
            .Should().Be("consultas/mailqueue?servidor=tcmbd4-hml&smtp=3&texto=boleto%20%26%20cia&qtd=100&sel=5F0A");
    }

    [Fact]
    public void AuditLog_omite_vazios_e_quantidade_padrao()
    {
        Navegacao.AuditLog("srv", 12, "4FDF7B6B", 50, "")
            .Should().Be("consultas/auditlog?servidor=srv&app=12&loguid=4FDF7B6B");
    }

    [Fact]
    public void AuditLog_omite_os_dias_padrao_e_mantem_os_outros()
    {
        Navegacao.AuditLog("srv", dias: 30).Should().Be("consultas/auditlog?servidor=srv");
        Navegacao.AuditLog("srv", dias: 90).Should().Be("consultas/auditlog?servidor=srv&dias=90");
    }

    [Fact]
    public void AuditLog_com_datas_leva_as_datas_e_nao_os_dias()
    {
        Navegacao.AuditLog("srv", qtd: 100, dias: 90, de: new DateOnly(2025, 1, 1), ate: new DateOnly(2025, 1, 31))
            .Should().Be("consultas/auditlog?servidor=srv&de=2025-01-01&ate=2025-01-31&qtd=100");
        Navegacao.AuditLog("srv", de: new DateOnly(2025, 1, 1)).Should().Be("consultas/auditlog?servidor=srv&de=2025-01-01");
    }

    [Fact]
    public void AuditLog_sem_AppID_vai_como_app_sem()
    {
        Navegacao.AuditLog("srv", app: 12, semApp: true).Should().Be("consultas/auditlog?servidor=srv&app=sem");
    }

    [Fact]
    public void Quantidade_diferente_do_padrao_e_mantida()
    {
        Navegacao.AuditLog(qtd: 1000).Should().Be("consultas/auditlog?qtd=1000");
    }

    [Fact]
    public void Sistemas_sem_busca_devolve_somente_a_rota()
    {
        Navegacao.Catalogo().Should().Be("sistemas/catalogo");
        Navegacao.RotaCatalogo.Should().Be("sistemas/catalogo");
    }

    [Fact]
    public void Sistemas_com_busca_codifica_o_texto()
    {
        Navegacao.Catalogo("multas hml").Should().Be("sistemas/catalogo?busca=multas%20hml");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sistemas_com_busca_vazia_fica_sem_parametro(string? busca)
    {
        Navegacao.Catalogo(busca).Should().Be("sistemas/catalogo");
    }

    [Fact]
    public void Versoes_sem_parametros_devolve_somente_a_rota()
    {
        Navegacao.Versoes().Should().Be("sistemas/versoes");
        Navegacao.RotaVersoes.Should().Be("sistemas/versoes");
    }

    [Fact]
    public void Versoes_monta_servidor_busca_e_filtro_nessa_ordem()
    {
        Navegacao.Versoes("tcmbd4-hml", "etcm", FiltroDeSituacao.ComDivergencia)
            .Should().Be("sistemas/versoes?servidor=tcmbd4-hml&busca=etcm&filtro=div");
        Navegacao.Versoes(filtro: FiltroDeSituacao.ComErro).Should().Be("sistemas/versoes?filtro=erro");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Versoes_com_busca_vazia_servidor_nulo_e_filtro_Todos_fica_sem_parametros(string? busca)
    {
        Navegacao.Versoes(null, busca, FiltroDeSituacao.Todos).Should().Be("sistemas/versoes");
    }

    [Fact]
    public void Versoes_codifica_a_busca()
    {
        Navegacao.Versoes(busca: "etcm impressão").Should().Be("sistemas/versoes?busca=etcm%20impress%C3%A3o");
    }

    [Theory]
    [InlineData("div", FiltroDeSituacao.ComDivergencia)]
    [InlineData("erro", FiltroDeSituacao.ComErro)]
    [InlineData(null, FiltroDeSituacao.Todos)]
    [InlineData("", FiltroDeSituacao.Todos)]
    [InlineData("xyz", FiltroDeSituacao.Todos)]
    [InlineData("DIV", FiltroDeSituacao.Todos)]
    [InlineData("semficha", FiltroDeSituacao.SemFicha)]
    public void LerFiltro_aceita_div_erro_e_semficha_e_o_resto_e_Todos(string? valor, FiltroDeSituacao esperado)
    {
        Navegacao.LerFiltro(valor).Should().Be(esperado);
    }

    [Fact]
    public void Versoes_com_filtro_SemFicha_grava_semficha()
    {
        Navegacao.Versoes(null, null, FiltroDeSituacao.SemFicha).Should().Be("sistemas/versoes?filtro=semficha");
    }

    [Fact]
    public void A_ficha_fica_embaixo_do_catalogo()
    {
        Navegacao.RotaFicha.Should().Be("sistemas/catalogo/ficha");
        Navegacao.RotaFicha.Should().StartWith(Navegacao.RotaCatalogo);
    }

    [Fact]
    public void Ficha_leva_o_sistema_e_o_servidor_escapados()
    {
        Navegacao.Ficha("SIGARH").Should().Be("sistemas/catalogo/ficha?sistema=SIGARH");
        Navegacao.Ficha("eTCM Impressão", "tcmbd3-dev")
            .Should().Be("sistemas/catalogo/ficha?sistema=eTCM%20Impress%C3%A3o&servidor=tcmbd3-dev");
    }

    [Fact]
    public void Ficha_escapa_os_caracteres_de_endereco()
    {
        Navegacao.Ficha("A&B/C?#%").Should().Be("sistemas/catalogo/ficha?sistema=A%26B%2FC%3F%23%25");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ficha_sem_servidor_fica_sem_o_parametro(string? servidor)
    {
        Navegacao.Ficha("SIGARH", servidor).Should().Be("sistemas/catalogo/ficha?sistema=SIGARH");
    }
}
