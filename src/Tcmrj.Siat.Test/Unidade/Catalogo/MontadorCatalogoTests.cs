using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Catalogo;
using Tcmrj.Siat.Infra.Catalogo;

namespace Tcmrj.Siat.Test.Unidade.Catalogo;

/// <summary>Validação e montagem dos itens do catálogo (research.md R3, data-model.md).</summary>
public class MontadorCatalogoTests
{
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 24, 13, 0, 0, TimeSpan.Zero);

    private const string Origem = "https://git.tcmnet.tcm/asi/TCMRJDoc/-/blob/master/sistemasDb.js";

    private static CatalogoLido Montar(string js, ILogger? logger = null)
    {
        var resultado = Resultado(js, logger);
        resultado.IsSuccess.Should().BeTrue(resultado.IsFailure ? resultado.Error.Message : string.Empty);
        return resultado.Value;
    }

    private static Result<CatalogoLido, Erro> Resultado(string js, ILogger? logger = null)
    {
        var json = LeitorSistemasDb.ParaJson(js);
        json.IsSuccess.Should().BeTrue();
        return MontadorCatalogo.Montar(json.Value, LidoEm, Origem, logger ?? NullLogger.Instance);
    }

    private static Sistema Unico(string js) => Montar(js).Sistemas.Should().ContainSingle().Subject;

    [Fact]
    public void Nome_sem_espacos_nas_pontas_e_dados_da_leitura()
    {
        var catalogo = Montar("""[ { "Title": "  SCP Api " } ]""");

        catalogo.Sistemas.Single().Nome.Should().Be("SCP Api");
        catalogo.LidoEm.Should().Be(LidoEm);
        catalogo.Origem.Should().Be(Origem);
        catalogo.Ignorados.Should().Be(0);
    }

    [Fact]
    public void URL_dividida_por_barra_vertical_e_Machine_por_virgula_sem_partes_vazias()
    {
        var sistema = Unico("""
            [ { "Title": "RabbitMQ", "PRD": [ { "URL": " https://a | |https://b|", "Machine": "TCM2016WEB1, TCM2016WEB2,, ", "Path": "  C:\\x  " } ] } ]
            """);

        var instancia = sistema.Ambientes[AmbienteDoCatalogo.Prd].Should().ContainSingle().Subject;
        instancia.Enderecos.Should().Equal("https://a", "https://b");
        instancia.Maquinas.Should().Equal("TCM2016WEB1", "TCM2016WEB2");
        instancia.Caminho.Should().Be(@"C:\x");
    }

    [Fact]
    public void Instancia_com_os_tres_campos_vazios_e_descartada()
    {
        var sistema = Unico("""
            [ { "Title": "WebNews", "HML": [ { "Machine": "", "URL": " ", "Path": "" } ], "PRD": [ { "Machine": "M1" }, {} ] } ]
            """);

        sistema.Ambientes[AmbienteDoCatalogo.Hml].Should().BeEmpty();
        sistema.Ambientes[AmbienteDoCatalogo.Prd].Should().ContainSingle().Which.Maquinas.Should().Equal("M1");
    }

    [Fact]
    public void Ambiente_ausente_vira_lista_vazia_e_os_quatro_estao_sempre_presentes()
    {
        var sistema = Unico("""[ { "Title": "Contratos", "OBS": "Site temporariamente fora do ar" } ]""");

        sistema.Ambientes.Keys.Should().BeEquivalentTo(Enum.GetValues<AmbienteDoCatalogo>());
        sistema.Ambientes.Values.Should().AllSatisfy(l => l.Should().BeEmpty());
    }

    [Fact]
    public void Ambiente_nulo_vira_lista_vazia()
    {
        Unico("""[ { "Title": "A", "DSV": null } ]""").Ambientes[AmbienteDoCatalogo.Dsv].Should().BeEmpty();
    }

    [Fact]
    public void OBS_vazia_vira_nulo_e_quebras_de_linha_sao_preservadas()
    {
        Unico("""[ { "Title": "A", "OBS": "  " } ]""").Observacao.Should().BeNull();
        Unico("""[ { "Title": "A", "OBS": " linha 1\nlinha 2 " } ]""").Observacao.Should().Be("linha 1\nlinha 2");
        Unico("""[ { "Title": "A", "OBS": "a<br />b" } ]""").Observacao.Should().Be("a<br />b");
    }

    [Theory]
    [InlineData("""{ "OBS": "sem título" }""")]
    [InlineData("""{ "Title": "   " }""")]
    [InlineData("""{ "Title": 10 }""")]
    [InlineData("""{ "Title": "B", "PRD": { "URL": "x" } }""")]
    [InlineData("""{ "Title": "B", "PRD": [ "x" ] }""")]
    [InlineData("""{ "Title": "B", "PRD": [ { "URL": 1 } ] }""")]
    [InlineData("""{ "Title": "B", "OBS": [] }""")]
    [InlineData("\"texto\"")]
    public void Item_invalido_e_ignorado_e_contado(string item)
    {
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var catalogo = Montar($$"""[ { "Title": "A" }, {{item}} ]""", logger.Object);

        catalogo.Sistemas.Select(s => s.Nome).Should().Equal("A");
        catalogo.Ignorados.Should().Be(1);
        logger.Verify(
            l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, _) => $"{v}".Contains("Item 2 ", StringComparison.Ordinal)),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Campo_desconhecido_e_ignorado()
    {
        var sistema = Unico("""[ { "Title": "A", "Outro": 1, "PRD": [ { "Machine": "M", "Porta": 80 } ] } ]""");

        sistema.Ambientes[AmbienteDoCatalogo.Prd].Should().ContainSingle();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[ ]")]
    public void Lista_vazia_e_formato_invalido(string js)
    {
        var resultado = Resultado(js);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoFormatoInvalido);
    }

    [Fact]
    public void Raiz_que_nao_e_lista_e_formato_invalido()
    {
        var resultado = MontadorCatalogo.Montar("""{ "Title": "A" }""", LidoEm, Origem, NullLogger.Instance);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be(Erros.Catalogo.CodigoFormatoInvalido);
    }

    [Fact]
    public void Ordena_pelo_nome_na_cultura_pt_BR()
    {
        var catalogo = Montar("""
            [ { "Title": "Exame" }, { "Title": "etc" }, { "Title": "Ética" }, { "Title": "Abc" }, { "Title": "ábaco" } ]
            """);

        catalogo.Sistemas.Select(s => s.Nome).Should().Equal("ábaco", "Abc", "etc", "Ética", "Exame");
    }

    [Fact]
    public void A_amostra_produz_os_sistemas_esperados()
    {
        var catalogo = Montar(File.ReadAllText(LeitorSistemasDbTests.CaminhoDaAmostra));

        catalogo.Ignorados.Should().Be(0);
        catalogo.Sistemas.Select(s => s.Nome).Should().Equal(
            "Contratos",
            "Portal do Servidor - INTRANET",
            "RabbitMQ - Gerenciamento",
            "SCP - Sistema de Controle de Processos",
            "SCP (Visualização de Processos)",
            "SCP Api",
            "SGQ",
            "SIGA-RH - AgenteCalculo",
            "SIGA-RH - AgenteSIGA",
            "WebNews - Intranet");

        Sistema Por(string nome) => catalogo.Sistemas.Single(s => s.Nome == nome);

        Por("Portal do Servidor - INTRANET").Observacao.Should().Be("Novo site feito para o DGP");
        Por("SCP - Sistema de Controle de Processos").Ambientes[AmbienteDoCatalogo.Dsv].Single().Caminho
            .Should().Be(@"G:DESENVOLVIMENTOVBSCP\SCP DESENVOLVIMENTO.EXE");
        Por("SCP - Sistema de Controle de Processos").Ambientes[AmbienteDoCatalogo.Dsv].Single().Enderecos.Should().Equal("N/A");
        Por("SIGA-RH - AgenteCalculo").Ambientes[AmbienteDoCatalogo.Dsv].Single().Caminho
            .Should().Be(@"C:\TcmRootSIGARH_Agentes\AgenteCalculo\DSV\");
        Por("SCP Api").Ambientes[AmbienteDoCatalogo.Dsv].Single().Caminho.Should().Be(@"W:\TcmRoot\DSV\Sites\scp_api_dsv");
        Por("WebNews - Intranet").Ambientes[AmbienteDoCatalogo.Hml].Should().BeEmpty();
        Por("WebNews - Intranet").Ambientes[AmbienteDoCatalogo.Prd].Single().Maquinas.Should().Equal("TCM2016WEB1", "TCM2016WEB2");
        Por("RabbitMQ - Gerenciamento").Ambientes[AmbienteDoCatalogo.Prd].Single().Enderecos.Should().HaveCount(4);
        Por("Contratos").Ambientes.Values.Should().AllSatisfy(l => l.Should().BeEmpty());
        Por("SCP (Visualização de Processos)").Observacao.Should().Contain("<br />");
        Por("SCP - Sistema de Controle de Processos").Ambientes[AmbienteDoCatalogo.Hml].Single().Caminho
            .Should().Be(@"F:\HOMOLOGACAO\VBSCP\SCP HOMOLOGAÇÃO.EXE");
    }
}
