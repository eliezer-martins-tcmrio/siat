using AwesomeAssertions;
using Tcmrj.Siat.App.Web;

namespace Tcmrj.Siat.Test.Unidade;

public class NavegacaoTests
{
    [Fact]
    public void Sem_parametros_devolve_somente_a_rota()
    {
        Navegacao.MailQueue().Should().Be("/consultas/mailqueue");
        Navegacao.AuditLog().Should().Be("/consultas/auditlog");
        Navegacao.Home.Should().Be("/");
    }

    [Fact]
    public void MailQueue_monta_parametros_na_ordem_e_codifica_valores()
    {
        Navegacao.MailQueue("tcmbd4-hml", 3, "boleto & cia", 100, "5F0A")
            .Should().Be("/consultas/mailqueue?servidor=tcmbd4-hml&smtp=3&texto=boleto%20%26%20cia&qtd=100&sel=5F0A");
    }

    [Fact]
    public void AuditLog_omite_vazios_e_quantidade_padrao()
    {
        Navegacao.AuditLog("srv", 12, "4363", 50, "")
            .Should().Be("/consultas/auditlog?servidor=srv&app=12&logid=4363");
    }

    [Fact]
    public void Quantidade_diferente_do_padrao_e_mantida()
    {
        Navegacao.AuditLog(qtd: 1000).Should().Be("/consultas/auditlog?qtd=1000");
    }
}
