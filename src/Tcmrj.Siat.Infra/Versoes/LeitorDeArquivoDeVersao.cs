using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Tcmrj.Siat.Domain.Models.Versoes;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.Infra.Versoes;

/// <summary>
/// Lê um arquivo de versão (specs/003-versoes-publicadas/research.md R3 e contracts/origem-enderecos.md):
/// <list type="bullet">
/// <item>Caminho UNC: <see cref="FileStream"/> só de leitura, compartilhado para leitura, escrita e exclusão, para não
/// travar uma publicação em andamento.</item>
/// <item><c>http</c>/<c>https</c>: <c>GET</c> com o <see cref="HttpClient"/> tipado, sem credenciais, com até 3
/// redirecionamentos (<c>VersoesConfiguration</c>); sucesso só em 2xx.</item>
/// <item>No máximo <see cref="TamanhoMaximo"/> bytes, decodificados com detecção de BOM e UTF-8 como padrão.</item>
/// </list>
/// Qualquer outra forma de endereço é recusada sem leitura. O tempo limite é de quem chama: o cancelamento chega como
/// <see cref="OperationCanceledException"/>. A interpretação do texto é do <see cref="InterpretadorDeVersao"/>.
/// </summary>
public sealed partial class LeitorDeArquivoDeVersao(HttpClient http, ILogger<LeitorDeArquivoDeVersao> logger) : ILeitorDeArquivoDeVersao
{
    /// <summary>Até 64 KB lidos por arquivo ou resposta; o resto é ignorado.</summary>
    public const int TamanhoMaximo = 64 * 1024;

    // ERROR_BAD_NETPATH (53) e ERROR_BAD_NET_NAME (67): máquina ou compartilhamento inexistente.
    private const int CaminhoDeRedeInexistente = 53;
    private const int CompartilhamentoInexistente = 67;

    public async Task<Result<string, string>> LerAsync(string endereco, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endereco);

        try
        {
            return ClassificadorDeEndereco.Classificar(endereco) switch
            {
                TipoDeEndereco.Unc => await LerArquivoAsync(endereco, cancellationToken),
                TipoDeEndereco.Http => await LerHttpAsync(new Uri(endereco, UriKind.Absolute), cancellationToken),
                _ => Falha(ClassificadorDeEndereco.MensagemNaoSuportado),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            FalhaInesperada(logger, endereco, ex);
            return Falha($"Falha ao ler: {SemPontoFinal(ex.Message)}.");
        }
    }

    /// <summary>Lê um caminho já aceito pelo <see cref="ClassificadorDeEndereco"/> (separado para teste sem rede).</summary>
    public static async Task<Result<string, string>> LerArquivoAsync(string caminho, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // A abertura de um arquivo de rede é síncrona e não aceita o token (plan.md, Acompanhamento de complexidade):
            // quem chama solta a espera com WaitAsync. A leitura do conteúdo é interrompida de fato.
            await using var fluxo = new FileStream(caminho, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous,
            });
            return Lido(await LerAteOLimiteAsync(fluxo, cancellationToken));
        }
        catch (FileNotFoundException)
        {
            return Falha("Arquivo não encontrado.");
        }
        catch (DirectoryNotFoundException)
        {
            return Falha("Caminho não encontrado.");
        }
        catch (UnauthorizedAccessException)
        {
            return Falha("Acesso negado à conta do SIAT.");
        }
        catch (IOException ex) when (CodigoWin32(ex) is CaminhoDeRedeInexistente or CompartilhamentoInexistente)
        {
            return Falha("Caminho não encontrado.");
        }
    }

    private async Task<Result<string, string>> LerHttpAsync(Uri endereco, CancellationToken cancellationToken)
    {
        try
        {
            using var resposta = await http.GetAsync(endereco, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                return Falha($"O servidor respondeu {(int)resposta.StatusCode}.");
            }

            await using var corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            return Lido(await LerAteOLimiteAsync(corpo, cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            return Falha($"Não foi possível acessar o endereço: {SemPontoFinal(ex.Message)}.");
        }
    }

    private static async Task<ArraySegment<byte>> LerAteOLimiteAsync(Stream fluxo, CancellationToken cancellationToken)
    {
        var buffer = new byte[TamanhoMaximo];
        var total = 0;
        while (total < buffer.Length)
        {
            var lidos = await fluxo.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (lidos == 0)
            {
                break;
            }

            total += lidos;
        }

        return new ArraySegment<byte>(buffer, 0, total);
    }

    private static Result<string, string> Lido(ArraySegment<byte> bytes) => Result.Success<string, string>(Decodificar(bytes));

    private static Result<string, string> Falha(string mensagem) => Result.Failure<string, string>(mensagem);

    private static string Decodificar(ArraySegment<byte> bytes)
    {
        using var memoria = new MemoryStream(bytes.Array!, bytes.Offset, bytes.Count, writable: false);
        using var leitor = new StreamReader(memoria, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return leitor.ReadToEnd();
    }

    private static int CodigoWin32(IOException ex) => ex.HResult & 0xFFFF;

    private static string SemPontoFinal(string mensagem) => mensagem.TrimEnd().TrimEnd('.');

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha inesperada ao ler o arquivo de versão {Endereco}.")]
    private static partial void FalhaInesperada(ILogger logger, string endereco, Exception ex);
}
