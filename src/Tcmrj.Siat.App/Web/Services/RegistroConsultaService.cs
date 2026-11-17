using Microsoft.Extensions.Logging;
using Tcmrj.CoreLib.DBLog;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Extensions;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Grava o registro de consulta/exportação no DBLog (R7, contracts/registro-consulta.md).
/// Nunca propaga falha de gravação.
/// </summary>
public sealed class RegistroConsultaService(
    IUsuarioAtualService usuarioAtual,
    ILogger<RegistroConsultaService> logger) : IRegistroConsultaService
{
    /// <summary>TypeID 10: "Log de Informações Gerais" em <c>audit.Log_Types</c>.</summary>
    internal const byte TipoLog = 10;

    // "OperacaoTexto" leva o texto acentuado da mensagem ("Exportação"); a propriedade "Operacao" do LogEvent
    // segue o contrato (Consulta/Exportacao) e vem do escopo. No Serilog a propriedade do modelo venceria a do escopo.
    internal const string Modelo = "{OperacaoTexto} {Modulo} em {ServidorNome} ({Ambiente}) por {Matricula}";

    public async Task RegistrarAsync(RegistroConsulta registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        try
        {
            var usuario = await usuarioAtual.ObterAsync();
            var matricula = usuario.FindFirst("matricula")?.Value ?? string.Empty;
            var nome = usuario.FindFirst("nome")?.Value ?? string.Empty;
            var ambiente = registro.Ambiente?.Descricao() ?? string.Empty;
            var servidorNome = registro.ServidorNome ?? registro.ServidorChave;
            var operacao = registro.Operacao == OperacaoConsulta.Exportacao ? "Exportação" : "Consulta";

            // O modelo da mensagem só comporta cinco propriedades; as demais vão no escopo, que o Serilog
            // acrescenta ao LogEvent ("@" no nome faz o Serilog destruturar o objeto de filtros).
            var escopo = new Dictionary<string, object?>
            {
                ["Operacao"] = registro.Operacao.ToString(),
                ["Servidor"] = registro.ServidorChave,
                ["Usuario"] = nome,
                ["@Filtros"] = registro.Filtros,
                ["Itens"] = registro.Itens,
                ["Resultado"] = registro.Resultado.ToString(),
            };

            using (logger.BeginScope(escopo))
            {
                logger.LogErrorWithLogUID(
                    TipoLog,
                    Modelo,
                    operacao,
                    registro.Modulo.ToString(),
                    servidorNome,
                    ambiente,
                    matricula);
            }
        }
#pragma warning disable CA1031 // Falha ao registrar nunca pode impedir nem interromper a consulta do usuário.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
