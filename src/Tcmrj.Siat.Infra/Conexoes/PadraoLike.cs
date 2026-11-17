namespace Tcmrj.Siat.Infra.Conexoes;

public static class PadraoLike
{
    /// <summary>Escapa <c>[</c>, <c>%</c> e <c>_</c> e envolve em <c>%</c> para uso em LIKE.</summary>
    public static string Montar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var escapado = texto.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        return $"%{escapado}%";
    }
}
