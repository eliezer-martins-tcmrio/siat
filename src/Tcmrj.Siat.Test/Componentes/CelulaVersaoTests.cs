using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.App.Web.ViewModels.Versoes;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Célula de uma máquina na tabela de Versões (contracts/rotas-ui.md, <c>CelulaVersao</c>).</summary>
public sealed class CelulaVersaoTests : BunitContext
{
    private const string Caminho = @"\\APPPRD01\Sites\STP\VERSAO.TXT";

    private IRenderedComponent<CelulaVersao> Renderizar(LeituraDeCelula leitura, bool divergente = false) =>
        Render<CelulaVersao>(p => p
            .Add(c => c.Celula, CelulaVersaoVM.De(leitura, divergente))
            .Add(c => c.Divergente, divergente));

    [Fact]
    public void Versao_mostra_o_selo_ok_sem_texto_de_divergencia()
    {
        var celula = Renderizar(LeituraDeCelula.ComVersao(Ambiente.Prd, Maquina.Maquina2, "6.1.8", Caminho));

        var selo = celula.Find("td .versao");
        selo.ClassList.Should().Contain("ok").And.NotContain("divergente");
        selo.TextContent.Should().Be("6.1.8");
        celula.Markup.Should().NotContain("difere da outra máquina");
    }

    [Fact]
    public void Versao_divergente_tem_o_selo_divergente_e_o_texto_para_leitor_de_tela()
    {
        var celula = Renderizar(LeituraDeCelula.ComVersao(Ambiente.Prd, Maquina.Maquina2, "7.23.9", Caminho), divergente: true);

        celula.Find(".versao").ClassList.Should().Contain("divergente");
        celula.Find(".so-leitor").TextContent.Should().Contain("difere da outra máquina");
    }

    [Fact]
    public void Erro_mostra_a_pilula_e_o_texto_para_leitor_de_tela()
    {
        var celula = Renderizar(LeituraDeCelula.ComErro(Ambiente.Hml, Maquina.Maquina1, "Arquivo não encontrado.", Caminho));

        var pilula = celula.Find(".pilula.erro");
        pilula.QuerySelector("[aria-hidden]")!.TextContent.Should().Be("Erro");
        pilula.QuerySelector(".so-leitor")!.TextContent.Should().Be("Erro ao ler a versão");
        celula.Markup.Should().NotContain("Arquivo não encontrado");
    }

    [Fact]
    public void Sem_endereco_mostra_traco_e_nao_configurado()
    {
        var celula = Renderizar(LeituraDeCelula.NaoConfigurada(Ambiente.Dsv, Maquina.Maquina2));

        celula.Find(".vazio-cel .so-leitor").TextContent.Should().Be("não configurado");
        celula.FindAll(".versao").Should().BeEmpty();
    }

    [Fact]
    public void A_versao_sai_como_texto_e_nao_como_marcacao()
    {
        var celula = Renderizar(LeituraDeCelula.ComVersao(Ambiente.Dsv, Maquina.Maquina1, "<b>1</b>", Caminho));

        celula.FindAll("b").Should().BeEmpty();
        celula.Find(".versao").TextContent.Should().Be("<b>1</b>");
    }

    [Theory]
    [InlineData(Maquina.Maquina1, true)]
    [InlineData(Maquina.Maquina2, false)]
    public void So_a_maquina_1_tem_a_divisa(Maquina maquina, bool divisa)
    {
        var celula = Renderizar(LeituraDeCelula.NaoConfigurada(Ambiente.Hml, maquina));

        celula.Find("td").ClassList.Contains("divisa").Should().Be(divisa);
    }
}
