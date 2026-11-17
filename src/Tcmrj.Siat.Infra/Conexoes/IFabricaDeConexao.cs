using Microsoft.Data.SqlClient;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Infra.Conexoes;

public interface IFabricaDeConexao
{
    /// <summary>Tempo máximo de comando, em segundos.</summary>
    int CommandTimeout { get; }

    /// <summary>Cria (sem abrir) a conexão; o endereço vem sempre do <see cref="Servidor"/>, nunca do usuário.</summary>
    SqlConnection Criar(Servidor servidor, string banco);
}
