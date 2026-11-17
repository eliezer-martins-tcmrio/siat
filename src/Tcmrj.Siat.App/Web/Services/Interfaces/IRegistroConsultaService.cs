using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.App.Web.Services.Interfaces;

public enum OperacaoConsulta
{
    Consulta,
    Exportacao,
}

public enum ResultadoRegistro
{
    Sucesso,
    Cancelada,
    Erro,
    AcessoNegado,
}

/// <summary>
/// Dados de uma tentativa de consulta ou exportação ([contracts/registro-consulta.md]).
/// <paramref name="Filtros"/> é um objeto anônimo ou record com os campos do filtro usado;
/// nunca inclui corpo de e-mail nem conteúdo de log.
/// </summary>
public sealed record RegistroConsulta(
    OperacaoConsulta Operacao,
    Modulo Modulo,
    string ServidorChave,
    string? ServidorNome,
    Ambiente? Ambiente,
    object Filtros,
    int? Itens,
    ResultadoRegistro Resultado);

/// <summary>
/// Grava o registro de consulta no DBLog (R7) com <c>LogErrorWithLogUID(10, ...)</c>, acrescentando matrícula
/// e nome do usuário atual. Falha ao gravar nunca chega ao usuário.
/// </summary>
public interface IRegistroConsultaService
{
    Task RegistrarAsync(RegistroConsulta registro);
}
