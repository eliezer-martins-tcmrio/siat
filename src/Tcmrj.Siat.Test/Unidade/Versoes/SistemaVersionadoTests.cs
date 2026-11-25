using AwesomeAssertions;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>Divergência, erro e situação de um sistema (data-model.md, SistemaVersionado).</summary>
public sealed class SistemaVersionadoTests
{
    private const string Caminho = @"\\maq\Sites\x\VERSAO.TXT";

    internal static LeituraDeCelula V(Ambiente ambiente, Maquina maquina, string versao) =>
        LeituraDeCelula.ComVersao(ambiente, maquina, versao, Caminho);

    internal static LeituraDeCelula E(Ambiente ambiente, Maquina maquina) =>
        LeituraDeCelula.ComErro(ambiente, maquina, "Arquivo não encontrado.", Caminho);

    private static SistemaVersionado Sistema(params LeituraDeCelula[] celulas) => new(1, "SGQ", celulas);

    [Fact]
    public void Diverge_com_as_duas_celulas_com_versao_e_versoes_diferentes()
    {
        var sistema = Sistema(V(Ambiente.Hml, Maquina.Maquina1, "5.11.3"), V(Ambiente.Hml, Maquina.Maquina2, "5.11.2"));

        sistema.DivergeEm(Ambiente.Hml).Should().BeTrue();
        sistema.DivergeEm(Ambiente.Prd).Should().BeFalse();
        sistema.TemDivergencia.Should().BeTrue();
        sistema.TemErro.Should().BeFalse();
        sistema.SemPendencias.Should().BeFalse();
    }

    [Fact]
    public void Comparacao_e_ordinal()
    {
        Sistema(V(Ambiente.Dsv, Maquina.Maquina1, "1.0.0-A"), V(Ambiente.Dsv, Maquina.Maquina2, "1.0.0-a"))
            .DivergeEm(Ambiente.Dsv).Should().BeTrue();
    }

    [Fact]
    public void Versoes_iguais_nao_divergem()
    {
        var sistema = Sistema(V(Ambiente.Prd, Maquina.Maquina1, "4.6.5"), V(Ambiente.Prd, Maquina.Maquina2, "4.6.5"));

        sistema.TemDivergencia.Should().BeFalse();
        sistema.SemPendencias.Should().BeTrue();
    }

    [Fact]
    public void Uma_celula_com_erro_nao_diverge_mas_o_sistema_tem_erro()
    {
        var sistema = Sistema(V(Ambiente.Prd, Maquina.Maquina1, "6.1.8"), E(Ambiente.Prd, Maquina.Maquina2));

        sistema.DivergeEm(Ambiente.Prd).Should().BeFalse();
        sistema.TemErro.Should().BeTrue();
        sistema.SemPendencias.Should().BeFalse();
    }

    [Fact]
    public void Nao_configurado_ou_uma_maquina_so_nao_diverge()
    {
        var sistema = Sistema(
            V(Ambiente.Dsv, Maquina.Maquina1, "3.1.0"),
            V(Ambiente.Hml, Maquina.Maquina1, "3.0.7"),
            LeituraDeCelula.NaoConfigurada(Ambiente.Hml, Maquina.Maquina2, Caminho));

        sistema.TemDivergencia.Should().BeFalse();
        sistema.SemPendencias.Should().BeTrue();
    }

    [Fact]
    public void Divergencia_em_HML_e_erro_em_PRD_contam_nas_duas_situacoes()
    {
        var sistema = Sistema(
            V(Ambiente.Hml, Maquina.Maquina1, "5.11.3"), V(Ambiente.Hml, Maquina.Maquina2, "5.11.2"),
            E(Ambiente.Prd, Maquina.Maquina1));

        sistema.TemDivergencia.Should().BeTrue();
        sistema.TemErro.Should().BeTrue();
        sistema.SemPendencias.Should().BeFalse();
    }

    [Fact]
    public void Seis_celulas_nao_configuradas_e_sem_pendencias_e_ficam_na_ordem_da_tela()
    {
        var sistema = Sistema(V(Ambiente.Prd, Maquina.Maquina2, "1.0.0"), V(Ambiente.Dsv, Maquina.Maquina1, "1.0.0"));

        sistema.Celulas.Select(c => (c.Ambiente, c.Maquina)).Should().Equal(EnderecoDeVersao.Posicoes);
        Sistema().SemPendencias.Should().BeTrue();
        Sistema().Celulas.Should().OnlyContain(c => c.Situacao == SituacaoDaCelula.NaoConfigurado);
    }
}
