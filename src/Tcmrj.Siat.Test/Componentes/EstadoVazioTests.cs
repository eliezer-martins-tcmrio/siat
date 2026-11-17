using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Estado vazio (história 4, cenário 5): "Remover filtros" só com filtros preenchidos.</summary>
public sealed class EstadoVazioTests : BunitContext
{
    private IRenderedComponent<EstadoVazio> Renderizar(bool temFiltros, Action? aoRemover = null, string? acao = null) =>
        Render<EstadoVazio>(p =>
        {
            p.Add(c => c.Titulo, "Nenhum e-mail encontrado")
             .Add(c => c.Mensagem, "Todos os SMTPs, contendo \"boleto\", em TCMBD4\\HML.")
             .Add(c => c.TemFiltros, temFiltros);
            if (aoRemover is not null)
            {
                p.Add(c => c.AoRemoverFiltros, aoRemover);
            }

            if (acao is not null)
            {
                p.Add(c => c.Acoes, $"<button type=\"button\" class=\"propria\">{acao}</button>");
            }
        });

    [Fact]
    public void Sem_filtros_nao_oferece_Remover_filtros()
    {
        var vazio = Renderizar(temFiltros: false, aoRemover: () => { });

        vazio.Find("h2").TextContent.Should().Be("Nenhum e-mail encontrado");
        vazio.FindAll("button").Should().BeEmpty();
        vazio.FindAll(".acoes").Should().BeEmpty();
    }

    [Fact]
    public void Com_filtros_oferece_Remover_filtros_com_um_clique()
    {
        var removeu = 0;
        var vazio = Renderizar(temFiltros: true, aoRemover: () => removeu++);

        var botao = vazio.Find("button[data-remover-filtros]");
        botao.TextContent.Should().Be("Remover filtros");
        botao.Click();

        removeu.Should().Be(1);
    }

    [Fact]
    public void Sem_acao_de_remover_nao_mostra_o_botao()
    {
        Renderizar(temFiltros: true).FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void Acoes_do_modulo_vem_antes_de_Remover_filtros()
    {
        var vazio = Renderizar(temFiltros: true, aoRemover: () => { }, acao: "Todas as aplicações");

        vazio.FindAll(".acoes button").Select(b => b.TextContent).Should().Equal("Todas as aplicações", "Remover filtros");
    }
}
