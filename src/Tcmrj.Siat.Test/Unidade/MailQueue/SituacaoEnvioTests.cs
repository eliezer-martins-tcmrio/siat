using AwesomeAssertions;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Test.Unidade.MailQueue;

/// <summary>Situação de envio derivada para a tela (data-model, MensagemEnfileirada).</summary>
public sealed class SituacaoEnvioTests
{
    private static readonly DateTime Agora = new(2026, 11, 17, 14, 30, 0);

    private static MensagemEnfileirada Mensagem(bool enviado, DateTime? enviarApos = null, DateTime? dataEnvio = null) => new()
    {
        MessageId = "5F0A1B2C-0000-0000-0000-000000000001",
        DataPostagem = Agora.AddHours(-1),
        Enviado = enviado,
        EnviarApos = enviarApos,
        DataEnvio = dataEnvio,
    };

    [Fact]
    public void Enviado_mostra_Enviado_em_DataEnvio()
    {
        var vm = MensagemVM.De(Mensagem(enviado: true, dataEnvio: new DateTime(2026, 11, 17, 13, 45, 12)), Agora);

        vm.Situacao.Should().Be(SituacaoEnvio.Enviado);
        vm.SituacaoRotulo.Should().Be("Enviado");
        vm.SituacaoDescricao.Should().Be("Enviado em 17/11/2026 13:45");
    }

    [Fact]
    public void Enviado_mesmo_com_EnviarApos_futuro_continua_Enviado()
    {
        var vm = MensagemVM.De(Mensagem(enviado: true, enviarApos: Agora.AddDays(1), dataEnvio: Agora), Agora);

        vm.Situacao.Should().Be(SituacaoEnvio.Enviado);
    }

    [Fact]
    public void Nao_enviado_com_EnviarApos_futuro_e_Agendado()
    {
        var vm = MensagemVM.De(Mensagem(enviado: false, enviarApos: Agora.AddMinutes(10)), Agora);

        vm.Situacao.Should().Be(SituacaoEnvio.Agendado);
        vm.SituacaoRotulo.Should().Be("Agendado");
        vm.SituacaoDescricao.Should().Be("Agendado");
    }

    [Fact]
    public void Nao_enviado_com_EnviarApos_passado_esta_Na_fila()
    {
        var vm = MensagemVM.De(Mensagem(enviado: false, enviarApos: Agora.AddMinutes(-10)), Agora);

        vm.Situacao.Should().Be(SituacaoEnvio.NaFila);
        vm.SituacaoRotulo.Should().Be("Na fila");
        vm.SituacaoDescricao.Should().Be("Na fila");
    }

    [Fact]
    public void Nao_enviado_sem_EnviarApos_esta_Na_fila()
    {
        var vm = MensagemVM.De(Mensagem(enviado: false), Agora);

        vm.Situacao.Should().Be(SituacaoEnvio.NaFila);
    }

    [Fact]
    public void Destinatarios_sao_separados_e_o_primeiro_vai_na_coluna_Para()
    {
        var mensagem = Mensagem(enviado: false) with { Destinatarios = "a@tcm.exemplo; b@tcm.exemplo; c@tcm.exemplo" };

        var vm = MensagemVM.De(mensagem, Agora);

        vm.ListaDestinatarios.Should().Equal("a@tcm.exemplo", "b@tcm.exemplo", "c@tcm.exemplo");
        vm.PrimeiroDestinatario.Should().Be("a@tcm.exemplo");
        vm.DemaisDestinatarios.Should().Be(2);
    }

    [Fact]
    public void Sem_destinatarios_nao_tem_lista()
    {
        var vm = MensagemVM.De(Mensagem(enviado: false), Agora);

        vm.ListaDestinatarios.Should().BeEmpty();
        vm.PrimeiroDestinatario.Should().BeNull();
    }

    [Fact]
    public void Texto_do_filtro_local_tem_assunto_destinatarios_MessageId_e_corpo_sem_tags()
    {
        var mensagem = Mensagem(enviado: false) with
        {
            Assunto = "Boleto",
            Destinatarios = "a@tcm.exemplo",
            EhHtml = true,
            Corpo = "<style>p{color:red}</style><p>Olá <b>Ana</b></p><script>alert(1)</script>",
        };

        var vm = MensagemVM.De(mensagem, Agora);

        vm.TextoBusca.Should().Contain("Boleto").And.Contain("a@tcm.exemplo").And.Contain(mensagem.MessageId).And.Contain("Olá Ana");
        vm.TextoBusca.Should().NotContain("<").And.NotContain("color:red").And.NotContain("alert");
    }
}
