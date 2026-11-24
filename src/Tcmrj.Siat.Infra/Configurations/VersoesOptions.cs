namespace Tcmrj.Siat.Infra.Configurations;

/// <summary>
/// Seção <c>Versoes</c> (specs/003-versoes-publicadas/contracts/configuracao.md). Validada na subida, com motivos em
/// português; sem a seção, valem os padrões e o SIAT sobe normalmente (Princípio IV). <see cref="ServidorPadrao"/> fora
/// da lista de servidores não impede a subida: a tela pede para escolher o servidor.
/// </summary>
public sealed class VersoesOptions
{
    public const string Secao = "Versoes";

    public const string ServidorPadraoDaPublicacao = "tcmbd-prd";

    /// <summary>Chave do <c>servidores.json</c> lida quando a URL não traz o servidor.</summary>
    public string ServidorPadrao { get; set; } = ServidorPadraoDaPublicacao;

    /// <summary>Tempo limite por arquivo ou endereço, em segundos (1 a 60).</summary>
    public int TempoLimiteSegundos { get; set; } = 5;

    /// <summary>Leituras de arquivo em paralelo (1 a 128).</summary>
    public int LeiturasSimultaneas { get; set; } = 32;

    /// <summary>Validade do resultado de cada servidor, em minutos (1 a 60).</summary>
    public int MinutosDeCache { get; set; } = 5;

    /// <summary>Motivos de invalidez (vazio = válida).</summary>
    public IReadOnlyList<string> Validar()
    {
        var motivos = new List<string>();

        if (string.IsNullOrWhiteSpace(ServidorPadrao))
        {
            motivos.Add($"{Secao}:{nameof(ServidorPadrao)} é obrigatório (chave do servidores.json).");
        }

        Faixa(motivos, nameof(TempoLimiteSegundos), TempoLimiteSegundos, 1, 60);
        Faixa(motivos, nameof(LeiturasSimultaneas), LeiturasSimultaneas, 1, 128);
        Faixa(motivos, nameof(MinutosDeCache), MinutosDeCache, 1, 60);
        return motivos;
    }

    private static void Faixa(List<string> motivos, string nome, int valor, int minimo, int maximo)
    {
        if (valor < minimo || valor > maximo)
        {
            motivos.Add($"{Secao}:{nome} deve estar entre {minimo} e {maximo}.");
        }
    }
}
