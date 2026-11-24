using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Versoes;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Endereços de versão ativos de <c>CSLIB_UserManager.dbo.Apps_VersionAddresses</c> (contracts/origem-enderecos.md).</summary>
public interface IEnderecosDeVersaoRepository
{
    /// <returns>
    /// As linhas com <c>Ativo = 1</c>, sem ordem definida, ou <see cref="Erros.Versoes.TabelaIndisponivel"/>,
    /// <see cref="Erros.Versoes.TempoEsgotado"/> ou <see cref="Erros.Versoes.Cancelada"/>.
    /// </returns>
    Task<Result<IReadOnlyList<EnderecoDeVersao>, Erro>> ListarAtivosAsync(Servidor servidor, CancellationToken cancellationToken);
}
