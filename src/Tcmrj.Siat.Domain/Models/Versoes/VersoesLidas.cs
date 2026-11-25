namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>Resultado de uma leitura geral de um servidor; é o valor guardado no cache (data-model.md).</summary>
/// <param name="Servidor">De onde vieram os endereços.</param>
/// <param name="GeradoEm">Fim da leitura (UTC); base da validade do cache.</param>
/// <param name="Duracao">Da consulta à tabela até a última célula.</param>
/// <param name="Sistemas">Por nome na cultura pt-BR, depois por <c>Id</c> (research.md R8).</param>
/// <param name="QuantidadeDeEnderecos">Endereços distintos lidos.</param>
public sealed record VersoesLidas(
    Servidor Servidor,
    DateTimeOffset GeradoEm,
    TimeSpan Duracao,
    IReadOnlyList<SistemaVersionado> Sistemas,
    int QuantidadeDeEnderecos);
