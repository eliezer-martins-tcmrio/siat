namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>
/// Situação de uma célula (data-model.md). "Divergente" não é situação da célula: é calculada no sistema
/// (<see cref="SistemaVersionado.DivergeEm"/>).
/// </summary>
public enum SituacaoDaCelula
{
    Versao,
    Erro,
    NaoConfigurado,
}
