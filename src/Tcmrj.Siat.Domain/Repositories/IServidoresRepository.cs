using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Domain.Repositories;

/// <summary>Lista fechada de servidores. A busca é sempre pela chave, nunca pelo endereço.</summary>
public interface IServidoresRepository
{
    IReadOnlyList<Servidor> Listar();

    Servidor? ObterPorChave(string chave);
}
