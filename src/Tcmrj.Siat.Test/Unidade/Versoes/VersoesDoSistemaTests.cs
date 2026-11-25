using AwesomeAssertions;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Versões de um item num servidor, a partir de um resultado completo (tela de Versões) ou parcial (leitura da ficha)
/// (specs/004-ficha-sistema/data-model.md, VersoesDoSistema; FR-008, FR-017).
/// </summary>
public sealed class VersoesDoSistemaTests
{
    private static readonly Servidor Dsv = new() { Chave = "tcmbd3-dev", Nome = @"TCMBD3\DEV", Endereco = @"TCMBD3\DEV", Ambiente = Ambiente.Dsv };

    private static LeituraDeCelula V(Ambiente a, Maquina m, string v) => LeituraDeCelula.ComVersao(a, m, v, @"\\m\s\VERSAO.TXT");

    private static LeituraDeCelula E(Ambiente a, Maquina m) => LeituraDeCelula.ComErro(a, m, "Arquivo não encontrado.", @"\\m\s\VERSAO.TXT");

    internal static VersoesLidas Lidas(params SistemaVersionado[] sistemas) =>
        new(Dsv, new DateTimeOffset(2026, 11, 25, 13, 0, 0, TimeSpan.Zero), TimeSpan.FromSeconds(1.2), sistemas, sistemas.Length);

    private static SistemaVersionado Ok(int id, string nome) =>
        new(id, nome, [V(Ambiente.Dsv, Maquina.Maquina1, "1.0"), V(Ambiente.Dsv, Maquina.Maquina2, "1.0")]);

    private static SistemaVersionado Divergente(int id, string nome) =>
        new(id, nome, [V(Ambiente.Dsv, Maquina.Maquina1, "1.0"), V(Ambiente.Dsv, Maquina.Maquina2, "1.1")]);

    private static SistemaVersionado ComErro(int id, string nome) => new(id, nome, [E(Ambiente.Prd, Maquina.Maquina1)]);

    [Fact]
    public void Componentes_na_ordem_do_vinculo_e_nao_na_da_tabela()
    {
        var versoes = VersoesDoSistema.De(Lidas(Ok(1, "SIGARH"), Ok(2, "SIGARH Agendador")), VinculoDeVersoes.De(["SIGARH Agendador", "SIGARH"]));

        versoes.Componentes.Select(c => c.Nome).Should().Equal("SIGARH Agendador", "SIGARH");
    }

    [Fact]
    public void Compara_sem_acentos_e_maiusculas_e_o_nome_fica_como_escrito_no_vinculo()
    {
        var versoes = VersoesDoSistema.De(Lidas(Ok(1, "SIGARH Agendador")), VinculoDeVersoes.De(["sigarh agendador"]));

        var componente = versoes.Componentes.Should().ContainSingle().Subject;
        componente.Nome.Should().Be("sigarh agendador");
        componente.Registros.Single().Nome.Should().Be("SIGARH Agendador");
    }

    [Fact]
    public void Duas_linhas_com_o_mesmo_nome_viram_um_componente_com_dois_registros_por_Id()
    {
        var versoes = VersoesDoSistema.De(Lidas(Ok(9, "SGQ"), Ok(3, "sgq")), VinculoDeVersoes.De(["SGQ"]));

        versoes.Componentes.Single().Registros.Select(r => r.Id).Should().Equal(3, 9);
    }

    [Fact]
    public void Nome_sem_linha_vai_para_os_nao_encontrados_na_ordem_do_vinculo()
    {
        var versoes = VersoesDoSistema.De(Lidas(Ok(1, "SIGARH")), VinculoDeVersoes.De(["SIGARH Relatórios", "SIGARH", "SIGARH Antigo"]));

        versoes.Componentes.Select(c => c.Nome).Should().Equal("SIGARH");
        versoes.NaoEncontrados.Should().Equal("SIGARH Relatórios", "SIGARH Antigo");
    }

    [Fact]
    public void Divergencia_e_erro_de_qualquer_registro_contam()
    {
        var versoes = VersoesDoSistema.De(Lidas(Divergente(1, "A"), ComErro(2, "B")), VinculoDeVersoes.De(["A", "B"]));

        versoes.TemDivergencia.Should().BeTrue();
        versoes.TemErro.Should().BeTrue();
        versoes.SemPendencias.Should().BeFalse();
    }

    [Fact]
    public void Nomes_nao_encontrados_nao_contam_como_erro_nem_tiram_o_sem_pendencias()
    {
        var versoes = VersoesDoSistema.De(Lidas(Ok(1, "A")), VinculoDeVersoes.De(["A", "Inexistente"]));

        versoes.TemErro.Should().BeFalse();
        versoes.TemDivergencia.Should().BeFalse();
        versoes.SemPendencias.Should().BeTrue();
    }

    [Fact]
    public void Sistemas_da_tabela_fora_do_vinculo_sao_ignorados_no_resultado_completo_e_no_parcial()
    {
        var completo = Lidas(Ok(1, "SIGARH"), ComErro(2, "STP"), Divergente(3, "SGQ"));
        var parcial = Lidas(Ok(1, "SIGARH"));
        var vinculo = VinculoDeVersoes.De(["SIGARH"]);

        foreach (var lidas in new[] { completo, parcial })
        {
            var versoes = VersoesDoSistema.De(lidas, vinculo);
            versoes.Componentes.Should().ContainSingle().Which.Nome.Should().Be("SIGARH");
            versoes.SemPendencias.Should().BeTrue();
        }
    }
}
