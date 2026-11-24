using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Domain.Models;

/// <summary>Item da lista fechada de servidores (<c>servidores.json</c>).</summary>
public sealed class Servidor
{
    public const string PadraoChave = "^[a-z0-9-]{1,40}$";

    public const int TamanhoMaximoNome = 60;

    public const int TamanhoMaximoEndereco = 128;

    public const string BancoMailQueuePadrao = "MailQueue";

    public const string BancoAuditoriaPadrao = "AUDITORIADB";

    public const string BancoUserManagerPadrao = "CSLIB_UserManager";

    /// <summary>Obrigatória, única na lista, <c>^[a-z0-9-]{1,40}$</c>; é o único valor que vai na URL.</summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>Obrigatório, até 60 caracteres; exibido no seletor e na faixa de contexto.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Instância SQL Server (<c>MAQUINA\INSTANCIA</c>), até 128 caracteres; nunca vem do usuário.</summary>
    public string Endereco { get; set; } = string.Empty;

    public Ambiente Ambiente { get; set; }

    public string BancoMailQueue { get; set; } = BancoMailQueuePadrao;

    public string BancoAuditoria { get; set; } = BancoAuditoriaPadrao;

    /// <summary>Banco com <c>dbo.Apps_VersionAddresses</c>, lido pelas Versões Publicadas (research.md R1 da 003).</summary>
    public string BancoUserManager { get; set; } = BancoUserManagerPadrao;
}
