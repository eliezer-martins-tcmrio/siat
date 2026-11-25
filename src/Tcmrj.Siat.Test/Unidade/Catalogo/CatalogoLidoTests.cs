using AwesomeAssertions;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>
/// Localizar o item da ficha e montar o mapa de fichas da tela de Versões (specs/004-ficha-sistema/research.md R4;
/// FR-012, FR-020, SC-005).
/// </summary>
public sealed class CatalogoLidoTests
{
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 25, 13, 0, 0, TimeSpan.Zero);

    internal static Sistema Item(string nome, int posicao, params string[] versoes) =>
        new(nome, null, new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>(),
            versoes.Length == 0 ? null : VinculoDeVersoes.De(versoes), posicao);

    internal static Sistema ItemInvalido(string nome, int posicao) =>
        new(nome, null, new Dictionary<AmbienteDoCatalogo, IReadOnlyList<Instancia>>(), VinculoDeVersoes.ForaDoFormato, posicao);

    /// <param name="itens">Já na ordem do catálogo (alfabética).</param>
    internal static CatalogoLido Catalogo(params Sistema[] itens) => new(itens, 0, LidoEm, "https://git/sistemasDb.js");

    [Fact]
    public void Localizar_compara_pelo_nome_normalizado()
    {
        var sigarh = Item("SIGARH", 2);
        var catalogo = Catalogo(Item("SCP", 1), sigarh);

        var localizado = catalogo.Localizar("  sigarh ");

        localizado.Should().NotBeNull();
        localizado!.Item.Should().BeSameAs(sigarh);
        localizado.Repetidos.Should().Be(0);
    }

    [Theory]
    [InlineData("Inexistente")]
    [InlineData("")]
    [InlineData("   ")]
    public void Localizar_nome_que_nao_esta_no_catalogo_devolve_nulo(string nome)
    {
        Catalogo(Item("SIGARH", 1)).Localizar(nome).Should().BeNull();
    }

    [Fact]
    public void Localizar_nome_repetido_devolve_o_primeiro_no_arquivo_e_a_quantidade_dos_outros()
    {
        var depois = Item("SIGARH", 30);
        var antes = Item("sigarh", 7);
        var catalogo = Catalogo(depois, antes);

        var localizado = catalogo.Localizar("SIGARH")!;

        localizado.Item.Should().BeSameAs(antes);
        localizado.Repetidos.Should().Be(1);
    }

    [Fact]
    public void Mapa_de_fichas_vai_do_nome_normalizado_da_tabela_ao_nome_do_item()
    {
        var catalogo = Catalogo(Item("SIGARH", 1, "SIGARH", "SIGARH Agendador"), Item("SGQ", 2, "SGQ"));

        catalogo.FichasPorVersao().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["sigarh"] = "SIGARH",
            ["sigarh agendador"] = "SIGARH",
            ["sgq"] = "SGQ",
        });
    }

    [Fact]
    public void Nome_no_vinculo_de_dois_itens_fica_com_o_primeiro_em_ordem_alfabetica()
    {
        var catalogo = Catalogo(Item("Agendador", 9, "SIGARH Agendador"), Item("SIGARH", 1, "SIGARH", "SIGARH Agendador"));

        var mapa = catalogo.FichasPorVersao();

        mapa["sigarh agendador"].Should().Be("Agendador");
        mapa["sigarh"].Should().Be("SIGARH");
    }

    [Fact]
    public void Vinculo_do_item_repetido_que_nao_e_o_primeiro_do_seu_nome_nao_entra_no_mapa()
    {
        var catalogo = Catalogo(
            Item("SIGARH", 30, "SIGARH Relatórios", "SGQ"),
            Item("SIGARH", 7, "SIGARH"),
            Item("SGQ", 40, "SGQ"));

        var mapa = catalogo.FichasPorVersao();

        mapa.Should().ContainKey("sigarh").WhoseValue.Should().Be("SIGARH");
        mapa.Should().NotContainKey("sigarh relatorios");
        mapa["sgq"].Should().Be("SGQ", "o nome continua no mapa pelo outro item");
    }

    [Fact]
    public void Vinculo_invalido_e_item_sem_vinculo_nao_entram_no_mapa()
    {
        var catalogo = Catalogo(ItemInvalido("SCP", 1), Item("SGQ", 2));

        catalogo.FichasPorVersao().Should().BeEmpty();
    }

    // ---------- Invariantes de SC-005 (T043, regressão da Base) ----------

    /// <summary>
    /// Todo link da tela de Versões leva a uma ficha cujo vínculo contém aquele nome, e todo nome válido do vínculo de um
    /// item que é o primeiro do seu nome tem link (SC-005).
    /// </summary>
    private static void ConferirInvariantes(CatalogoLido catalogo)
    {
        var mapa = catalogo.FichasPorVersao();

        foreach (var (nomeDaTabela, nomeDoItem) in mapa)
        {
            var destino = catalogo.Localizar(nomeDoItem);
            destino.Should().NotBeNull($"o link de {nomeDaTabela} leva a {nomeDoItem}");
            destino!.Item.Versoes.Contem(nomeDaTabela).Should().BeTrue($"a ficha de {nomeDoItem} tem {nomeDaTabela} no vínculo");
        }

        var primeiros = catalogo.Sistemas.Where(s => ReferenceEquals(catalogo.Localizar(s.Nome)?.Item, s));
        foreach (var item in primeiros.Where(s => !s.Versoes.Invalido))
        {
            foreach (var nome in item.Versoes.Nomes)
            {
                mapa.Should().ContainKey(NomeDeSistema.Normalizar(nome), $"{nome} está no vínculo de {item.Nome}");
            }
        }
    }

    [Fact]
    public void Invariantes_valem_no_catalogo_da_amostra()
    {
        var catalogo = MontadorCatalogoTests.Montar(File.ReadAllText(LeitorSistemasDbTests.CaminhoDaAmostra));

        catalogo.FichasPorVersao().Should().NotBeEmpty();
        ConferirInvariantes(catalogo);
    }

    [Fact]
    public void Invariantes_valem_em_catalogos_gerados()
    {
        var aleatorio = new Random(4);
        string[] nomes = ["SIGARH", "sigarh", "SGQ", "Gestão", "Gestao", "SCP", "SCP Api", "STP"];

        for (var rodada = 0; rodada < 200; rodada++)
        {
            var itens = Enumerable.Range(0, aleatorio.Next(1, 8))
                .Select(posicao =>
                {
                    var nome = nomes[aleatorio.Next(nomes.Length)];
                    return aleatorio.Next(6) == 0
                        ? ItemInvalido(nome, posicao)
                        : Item(nome, posicao, Enumerable.Range(0, aleatorio.Next(0, 4)).Select(_ => nomes[aleatorio.Next(nomes.Length)]).ToArray());
                })
                .OrderBy(s => s.Nome, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true))
                .ToArray();

            ConferirInvariantes(Catalogo(itens));
        }
    }
}
