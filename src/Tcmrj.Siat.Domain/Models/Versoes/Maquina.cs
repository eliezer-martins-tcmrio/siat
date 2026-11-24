namespace Tcmrj.Siat.Domain.Models.Versoes;

/// <summary>
/// Coluna de um ambiente na tela de Versões (data-model.md): a máquina 1 vem das colunas sem sufixo da tabela e a 2,
/// das colunas com sufixo <c>2</c>. O nome segue o script e a tela de exemplo, mesmo quando o endereço é de um
/// compartilhamento ou site e não de uma máquina.
/// </summary>
#pragma warning disable CA1712 // Os nomes Maquina1 e Maquina2 são os do data-model.md e da tela ("Máquina 1", "Máquina 2").
public enum Maquina
{
    Maquina1,
    Maquina2,
}
#pragma warning restore CA1712

public static class MaquinaExtensions
{
    /// <summary>1 ou 2, para os rótulos "Máquina 1" e "Máquina 2".</summary>
    public static int Numero(this Maquina maquina) => maquina switch
    {
        Maquina.Maquina1 => 1,
        Maquina.Maquina2 => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(maquina), maquina, null),
    };
}
