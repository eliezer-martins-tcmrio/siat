using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>
/// Lê um arquivo de versão (caminho UNC ou endereço <c>http</c>/<c>https</c> já aceito pelo
/// <see cref="Models.Versoes.ClassificadorDeEndereco"/>), até 64 KB (contracts/origem-enderecos.md).
/// </summary>
public interface ILeitorDeArquivoDeVersao
{
    /// <returns>O texto lido ou a mensagem de erro da célula, em português.</returns>
    /// <exception cref="OperationCanceledException">O token foi cancelado (o tempo limite é aplicado por quem chama).</exception>
    Task<Result<string, string>> LerAsync(string endereco, CancellationToken cancellationToken);
}
