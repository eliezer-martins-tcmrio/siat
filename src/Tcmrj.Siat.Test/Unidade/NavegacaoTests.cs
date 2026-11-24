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
        Navegacao.Sistemas().Should().Be("catalogo/sistemas");
        Navegacao.RotaSistemas.Should().Be("catalogo/sistemas");
    }

    [Fact]
    public void Sistemas_com_busca_codifica_o_texto()
    {
        Navegacao.Sistemas("multas hml").Should().Be("catalogo/sistemas?busca=multas%20hml");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sistemas_com_busca_vazia_fica_sem_parametro(string? busca)
    {
        Navegacao.Sistemas(busca).Should().Be("catalogo/sistemas");
    }
}
