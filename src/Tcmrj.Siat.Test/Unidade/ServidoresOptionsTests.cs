using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Infra.Repositories;

namespace Tcmrj.Siat.Test.Unidade;

public class ServidoresOptionsTests
{
    private static ServidorConfigurado S(string chave = "a-dev", string nome = "A", string endereco = "A\\DEV") =>
        new() { Chave = chave, Nome = nome, Endereco = endereco, Ambiente = Ambiente.Dsv };

    private static (ServidoresRepository Repo, Mock<ILogger<ServidoresRepository>> Log) Criar(params ServidorConfigurado[] itens)
    {
        var monitor = new Mock<IOptionsMonitor<ServidoresOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(new ServidoresOptions { Servidores = [.. itens] });
        var log = new Mock<ILogger<ServidoresRepository>>();
        log.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return (new ServidoresRepository(monitor.Object, log.Object), log);
    }

    private static void VerificarErroLogado(Mock<ILogger<ServidoresRepository>> log, Times vezes) =>
        log.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), vezes);

    [Fact]
    public void Lista_valida_carrega_e_busca_por_chave_ordinal()
    {
        var (repo, log) = Criar(S("a-dev"), S("b-hml", "B", "B\\HML"));

        repo.Listar().Should().HaveCount(2);
        repo.ObterPorChave("b-hml")!.Nome.Should().Be("B");
        repo.ObterPorChave("B-HML").Should().BeNull();
        VerificarErroLogado(log, Times.Never());
    }

    [Fact]
    public void Chave_duplicada_rejeita_a_lista_inteira_e_loga_uma_vez()
    {
        var (repo, log) = Criar(S("a-dev"), S("a-dev"), S("b-hml"));

        repo.Listar().Should().BeEmpty();
        repo.Listar().Should().BeEmpty();
        repo.ObterPorChave("b-hml").Should().BeNull();
        VerificarErroLogado(log, Times.Once());
    }

    [Theory]
    [InlineData("", "A", "E")]
    [InlineData("a-dev", "", "E")]
    [InlineData("a-dev", "A", "")]
    [InlineData("A_dev", "A", "E")]
    [InlineData("a dev", "A", "E")]
    public void Campo_ausente_ou_chave_fora_do_padrao_rejeita_a_lista(string chave, string nome, string endereco)
    {
        var (repo, log) = Criar(S("ok-1"), S(chave, nome, endereco));

        repo.Listar().Should().BeEmpty();
        VerificarErroLogado(log, Times.Once());
    }

    [Fact]
    public void Limites_de_tamanho_e_chave_longa()
    {
        ServidoresOptionsValidator.Validar([S(nome: new string('n', 61))]).Should().NotBeEmpty();
        ServidoresOptionsValidator.Validar([S(endereco: new string('e', 129))]).Should().NotBeEmpty();
        ServidoresOptionsValidator.Validar([S(chave: new string('a', 41))]).Should().NotBeEmpty();
        ServidoresOptionsValidator.Validar([S(nome: new string('n', 60), endereco: new string('e', 128), chave: new string('a', 40))])
            .Should().BeEmpty();
    }

    [Fact]
    public void Ambiente_ausente_rejeita_a_lista_em_vez_de_virar_DSV()
    {
        var semAmbiente = new ServidorConfigurado { Chave = "c-prd", Nome = "C", Endereco = @"C\PRD" };
        var (repo, log) = Criar(S("ok-1"), semAmbiente);

        repo.Listar().Should().BeEmpty();
        VerificarErroLogado(log, Times.Once());
    }

    [Fact]
    public void Ambiente_desconhecido_no_json_rejeita_a_lista_sem_derrubar()
    {
        const string json = """
            { "Servidores": [ { "Chave": "a-prd", "Nome": "A", "Endereco": "A", "Ambiente": "PROD" } ] }
            """;
        var cfg = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServidores(cfg);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IServidoresRepository>().Listar().Should().BeEmpty();
    }

    [Fact]
    public void Strings_de_ambiente_ligam_ao_enum_via_json_e_raiz()
    {
        const string json = """
            { "Servidores": [
              { "Chave": "a-dsv", "Nome": "A", "Endereco": "A", "Ambiente": "DSV" },
              { "Chave": "b-hml", "Nome": "B", "Endereco": "B", "Ambiente": "HML" },
              { "Chave": "c-prd", "Nome": "C", "Endereco": "C", "Ambiente": "PRD" } ] }
            """;
        var cfg = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServidores(cfg);
        using var provider = services.BuildServiceProvider();
        var lista = provider.GetRequiredService<IServidoresRepository>().Listar();

        lista.Select(s => s.Ambiente).Should().Equal(Ambiente.Dsv, Ambiente.Hml, Ambiente.Prd);
        lista[0].BancoMailQueue.Should().Be("MailQueue");
        lista[0].BancoAuditoria.Should().Be("AUDITORIADB");
    }

    [Fact]
    public void Ambiente_em_memoria_tambem_liga()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Servidores:0:Chave"] = "a-dsv",
            ["Servidores:0:Nome"] = "A",
            ["Servidores:0:Endereco"] = "A",
            ["Servidores:0:Ambiente"] = "HML",
        }).Build();

        cfg.Get<ServidoresOptions>()!.Servidores.Single().Ambiente.Should().Be(Ambiente.Hml);
    }
}
