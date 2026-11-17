namespace Tcmrj.Siat.Infra.Configurations;

public sealed class ConsultasOptions
{
    public const string Secao = "ConsultasOptions";

    /// <summary>Tempo máximo de execução de cada comando, em segundos.</summary>
    public int CommandTimeout { get; set; } = 60;

    /// <summary>Tempo máximo para abrir a conexão, em segundos.</summary>
    public int ConnectTimeout { get; set; } = 15;
}
