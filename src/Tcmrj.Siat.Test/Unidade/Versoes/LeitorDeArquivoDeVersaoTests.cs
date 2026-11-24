using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tcmrj.Siat.Infra.Versoes;

namespace Tcmrj.Siat.Test.Unidade.Versoes;

/// <summary>
/// Leitura de um arquivo de versão (specs/003-versoes-publicadas/contracts/origem-enderecos.md): mensagens da célula,
/// limite de 64 KB, arquivo aberto por outro processo e cancelamento. O arquivo é testado numa pasta temporária local
/// (o caminho UNC é classificado à parte); o HTTP, com um handler falso. Nada vai à rede.
/// </summary>
public sealed class LeitorDeArquivoDeVersaoTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("siat-versoes-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private static CancellationToken Teste => TestContext.Current.CancellationToken;

    private string Arquivo(string nome, string conteudo)
    {
        var caminho = Path.Combine(_pasta, nome);
        File.WriteAllText(caminho, conteudo, Encoding.UTF8);
        return caminho;
    }

    [Fact]
    public async Task Arquivo_existente_devolve_o_texto()
    {
        var lido = await LeitorDeArquivoDeVersao.LerArquivoAsync(Arquivo("VERSAO.TXT", "1.4.2\r\n"), Teste);

        lido.IsSuccess.Should().BeTrue();
        lido.Value.Should().Be("1.4.2\r\n", "o BOM do UTF-8 não entra no texto");
    }

    [Fact]
    public async Task Arquivo_inexistente_e_Arquivo_nao_encontrado()
    {
        var lido = await LeitorDeArquivoDeVersao.LerArquivoAsync(Path.Combine(_pasta, "NAO.TXT"), Teste);

        lido.Error.Should().Be("Arquivo não encontrado.");
    }

    [Fact]
    public async Task Pasta_inexistente_e_Caminho_nao_encontrado()
    {
        var lido = await LeitorDeArquivoDeVersao.LerArquivoAsync(Path.Combine(_pasta, "nao", "VERSAO.TXT"), Teste);

        lido.Error.Should().Be("Caminho não encontrado.");
    }

    [Fact]
    public async Task Arquivo_maior_que_64_KB_e_lido_ate_o_limite()
    {
        var caminho = Arquivo("GRANDE.INI", new string('x', 100_000));

        var lido = await LeitorDeArquivoDeVersao.LerArquivoAsync(caminho, Teste);

        lido.Value.Length.Should().Be(LeitorDeArquivoDeVersao.TamanhoMaximo - 3, "os 3 bytes do BOM contam no limite");
    }

    [Fact]
    public async Task Arquivo_aberto_para_escrita_por_outro_processo_ainda_e_lido()
    {
        var caminho = Arquivo("VERSAO.TXT", "2.0.3");
        await using var escrita = new FileStream(caminho, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        var lido = await LeitorDeArquivoDeVersao.LerArquivoAsync(caminho, Teste);

        lido.Value.Should().Be("2.0.3");
    }

    [Fact]
    public async Task Token_cancelado_lanca_OperationCanceledException()
    {
        var caminho = Arquivo("VERSAO.TXT", "2.0.3");
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        var ler = () => LeitorDeArquivoDeVersao.LerArquivoAsync(caminho, cancelamento.Token);

        await ler.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Endereco_nao_suportado_nao_e_lido()
    {
        var caminho = Arquivo("appsettings.json", "segredo");
        var handler = new HandlerFalso(_ => throw new InvalidOperationException("não deveria chamar"));

        var lido = await Leitor(handler).LerAsync(caminho, Teste);

        lido.Error.Should().Be("Endereço não suportado.");
        handler.Requisicoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Http_200_devolve_o_corpo_sem_autenticacao()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("5.11.3\n") });

        var lido = await Leitor(handler).LerAsync("https://etcm.tcm.rj.gov.br/versao.txt", Teste);

        lido.Value.Should().Be("5.11.3\n");
        var requisicao = handler.Requisicoes.Single();
        requisicao.Method.Should().Be(HttpMethod.Get);
        requisicao.Headers.Authorization.Should().BeNull();
        requisicao.Headers.Contains("Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Http_404_e_O_servidor_respondeu_404()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var lido = await Leitor(handler).LerAsync("https://x/versao.txt", Teste);

        lido.Error.Should().Be("O servidor respondeu 404.");
    }

    [Fact]
    public async Task Falha_de_rede_e_Nao_foi_possivel_acessar_o_endereco()
    {
        var handler = new HandlerFalso(_ => throw new HttpRequestException("Nenhuma conexão pôde ser feita."));

        var lido = await Leitor(handler).LerAsync("http://x/versao.txt", Teste);

        lido.Error.Should().Be("Não foi possível acessar o endereço: Nenhuma conexão pôde ser feita.");
    }

    [Fact]
    public async Task Corpo_maior_que_64_KB_e_truncado()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('9', 200_000)) });

        var lido = await Leitor(handler).LerAsync("https://x/versao.txt", Teste);

        lido.Value.Length.Should().Be(LeitorDeArquivoDeVersao.TamanhoMaximo);
    }

    private static LeitorDeArquivoDeVersao Leitor(HandlerFalso handler) =>
        new(new HttpClient(handler), NullLogger<LeitorDeArquivoDeVersao>.Instance);

    /// <summary>Handler HTTP falso: guarda as requisições e responde pela função.</summary>
    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requisicoes { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requisicoes.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
