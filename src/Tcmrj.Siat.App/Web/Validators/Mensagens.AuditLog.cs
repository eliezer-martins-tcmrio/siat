namespace Tcmrj.Siat.App.Web.Validators;

/// <summary>Textos do AuditLog (FR-013).</summary>
public static partial class Mensagens
{
    public const string LogIdSoDigitos = "Informe apenas dígitos no LogID.";

    public const string AppIdNaoNumerico = "Informe o AppID da aplicação com um número inteiro.";

    public const string AplicacoesIndisponiveisTitulo = "Não foi possível carregar a lista de aplicações.";

    /// <summary>Aviso exibido quando a lista de aplicações não carrega e a tela passa a pedir o AppID.</summary>
    public static string AplicacoesIndisponiveis(string motivo) =>
        $"A lista de aplicações não pôde ser carregada: {motivo} Informe o AppID no campo numérico ou deixe em branco para todas as aplicações.";
}
