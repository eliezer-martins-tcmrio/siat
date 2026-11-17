using System.Globalization;
using AwesomeAssertions;
using Tcmrj.Siat.App.Web.Services;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>Filtro local (FR-020, R9): pt-BR, sem acentos nem maiúsculas, vários termos e contador "X de Y".</summary>
public sealed class FiltroLocalTests
{
    private static readonly IReadOnlyList<string> Itens =
    [
        "Notificação de prazo processual",
        "NOTIFICACAO DE PRAZO",
        "Comunicação ao jurisdicionado",
        "Relatório de auditoria",
        "Ação concluída com sucesso",
    ];

    [Fact]
    public void Usa_CompareInfo_de_pt_BR_ignorando_maiusculas_e_acentos()
    {
        FiltroLocal.Cultura.Name.Should().Be("pt-BR");
        FiltroLocal.Opcoes.Should().Be(CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
    }

    [Theory]
    [InlineData("notificacao", true)]
    [InlineData("NOTIFICAÇÃO", true)]
    [InlineData("Notificaçao", true)]
    [InlineData("prazo processual", true)]
    [InlineData("processual prazo", true)]
    [InlineData("prazo inexistente", false)]
    [InlineData("auditoria", false)]
    public void Contem_todos_os_termos_sem_diferenciar_acentos_e_maiusculas(string busca, bool esperado) =>
        FiltroLocal.Contem("Notificação de prazo processual", FiltroLocal.Termos(busca)).Should().Be(esperado);

    [Fact]
    public void Acao_sem_acento_casa_com_cedilha_e_til()
    {
        FiltroLocal.Contem("Ação concluída", FiltroLocal.Termos("acao concluida")).Should().BeTrue();
        FiltroLocal.Contem("ACAO CONCLUIDA", FiltroLocal.Termos("ação concluída")).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_termos_nao_filtra(string? busca)
    {
        FiltroLocal.Termos(busca).Should().BeEmpty();
        FiltroLocal.Filtrar(Itens, i => i, busca).Should().BeSameAs(Itens);
    }

    [Fact]
    public void Termos_sao_separados_por_qualquer_espaco()
    {
        FiltroLocal.Termos("  prazo\tprocessual  notificação ").Should().Equal("prazo", "processual", "notificação");
    }

    [Fact]
    public void Filtrar_mantem_a_ordem_dos_itens_carregados()
    {
        var visiveis = FiltroLocal.Filtrar(Itens, i => i, "notificacao prazo");

        visiveis.Should().Equal("Notificação de prazo processual", "NOTIFICACAO DE PRAZO");
    }

    [Fact]
    public void Texto_nulo_do_item_so_casa_sem_termos()
    {
        FiltroLocal.Contem(null, FiltroLocal.Termos("a")).Should().BeFalse();
        FiltroLocal.Contem(null, []).Should().BeTrue();
    }

    [Theory]
    [InlineData(3, 50, true, "e-mails", "3 de 50 e-mails")]
    [InlineData(50, 50, false, "e-mails", "50 e-mails")]
    [InlineData(0, 12, true, "logs", "0 de 12 logs")]
    [InlineData(0, 0, false, "logs", "")]
    [InlineData(0, 0, true, "logs", "")]
    public void Contador_mostra_X_de_Y(int visiveis, int total, bool filtrando, string unidade, string esperado) =>
        FiltroLocal.Contador(visiveis, total, filtrando, unidade).Should().Be(esperado);

    [Fact]
    public void Filtro_de_lista_devolve_a_mesma_instancia_enquanto_nada_muda()
    {
        var filtro = new FiltroLocal<string>();
        Func<string, string?> texto = i => i;

        var primeira = filtro.Aplicar(Itens, texto, "prazo");
        var segunda = filtro.Aplicar(Itens, texto, " prazo ");

        primeira.Should().HaveCount(2);
        segunda.Should().BeSameAs(primeira);
        filtro.Aplicar(Itens, texto, "prazo processual").Should().NotBeSameAs(primeira).And.ContainSingle();
        filtro.Aplicar(Itens, texto, string.Empty).Should().BeSameAs(Itens);
    }

    [Fact]
    public void Filtro_de_lista_refaz_quando_a_funcao_de_texto_muda()
    {
        var filtro = new FiltroLocal<int>();
        IReadOnlyList<int> numeros = [1, 2, 3];

        filtro.Aplicar(numeros, n => $"item {n}", "eTCM").Should().BeEmpty();
        filtro.Aplicar(numeros, n => n == 2 ? "item 2 eTCM" : $"item {n}", "etcm").Should().Equal(2);
    }
}
