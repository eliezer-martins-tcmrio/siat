using System.Text.RegularExpressions;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.Infra.Configurations;

/// <summary>
/// Lista fechada de servidores. O <c>servidores.json</c> tem o formato <c>{ "Servidores": [ ... ] }</c> e é
/// adicionado à raiz da configuração; por isso as opções são ligadas à RAIZ
/// (<c>services.Configure&lt;ServidoresOptions&gt;(configuration)</c>), e a propriedade <see cref="Servidores"/>
/// recebe o array. Veja <see cref="ServidoresConfiguration.AddServidores"/>.
/// </summary>
public sealed class ServidoresOptions
{
    public const string Secao = "Servidores";

    public List<ServidorConfigurado> Servidores { get; set; } = [];
}

/// <summary>
/// Item como vem do <c>servidores.json</c>. <see cref="Ambiente"/> é anulável de propósito: sem ele, o binder
/// deixaria o valor padrão (<c>Dsv</c>) e um servidor de PRD mal configurado ficaria acessível com a permissão
/// comum. Ambiente ausente invalida a lista.
/// </summary>
public sealed class ServidorConfigurado
{
    public string Chave { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Endereco { get; set; } = string.Empty;

    public Ambiente? Ambiente { get; set; }

    public string? BancoMailQueue { get; set; }

    public string? BancoAuditoria { get; set; }

    /// <summary>Opcional; sem ele, <see cref="Servidor.BancoUserManagerPadrao"/>.</summary>
    public string? BancoUserManager { get; set; }

    /// <summary>Converte para o modelo do domínio. Só chamar depois da validação.</summary>
    public Servidor ParaServidor() => new()
    {
        Chave = Chave,
        Nome = Nome,
        Endereco = Endereco,
        Ambiente = Ambiente ?? throw new InvalidOperationException($"Servidor '{Chave}' sem Ambiente."),
        BancoMailQueue = string.IsNullOrWhiteSpace(BancoMailQueue) ? Servidor.BancoMailQueuePadrao : BancoMailQueue,
        BancoAuditoria = string.IsNullOrWhiteSpace(BancoAuditoria) ? Servidor.BancoAuditoriaPadrao : BancoAuditoria,
        BancoUserManager = string.IsNullOrWhiteSpace(BancoUserManager) ? Servidor.BancoUserManagerPadrao : BancoUserManager,
    };
}

/// <summary>Validação pura da lista de servidores; devolve as mensagens de erro (vazia = válida).</summary>
public static partial class ServidoresOptionsValidator
{
    public static IReadOnlyList<string> Validar(IReadOnlyCollection<ServidorConfigurado>? servidores)
    {
        var erros = new List<string>();
        if (servidores is null)
        {
            return erros;
        }

        var chaves = new HashSet<string>(StringComparer.Ordinal);
        var indice = 0;
        foreach (var s in servidores)
        {
            indice++;
            var rotulo = $"Servidor #{indice}";
            if (s is null)
            {
                erros.Add($"{rotulo}: item nulo.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(s.Chave))
            {
                erros.Add($"{rotulo}: Chave é obrigatória.");
            }
            else
            {
                if (!PadraoChave().IsMatch(s.Chave))
                {
                    erros.Add($"{rotulo}: Chave '{s.Chave}' fora do padrão {Servidor.PadraoChave}.");
                }

                if (!chaves.Add(s.Chave))
                {
                    erros.Add($"{rotulo}: Chave '{s.Chave}' duplicada.");
                }
            }

            if (string.IsNullOrWhiteSpace(s.Nome))
            {
                erros.Add($"{rotulo}: Nome é obrigatório.");
            }
            else if (s.Nome.Length > Servidor.TamanhoMaximoNome)
            {
                erros.Add($"{rotulo}: Nome excede {Servidor.TamanhoMaximoNome} caracteres.");
            }

            if (string.IsNullOrWhiteSpace(s.Endereco))
            {
                erros.Add($"{rotulo}: Endereco é obrigatório.");
            }
            else if (s.Endereco.Length > Servidor.TamanhoMaximoEndereco)
            {
                erros.Add($"{rotulo}: Endereco excede {Servidor.TamanhoMaximoEndereco} caracteres.");
            }

            if (s.Ambiente is null || !Enum.IsDefined(s.Ambiente.Value))
            {
                erros.Add($"{rotulo}: Ambiente é obrigatório (DSV, HML ou PRD).");
            }
        }

        return erros;
    }

    [GeneratedRegex(Servidor.PadraoChave)]
    private static partial Regex PadraoChave();
}
