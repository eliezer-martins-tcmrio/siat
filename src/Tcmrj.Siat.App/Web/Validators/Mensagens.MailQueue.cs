namespace Tcmrj.Siat.App.Web.Validators;

/// <summary>Textos da MailQueue (FR-010).</summary>
public static partial class Mensagens
{
    public const string TextoMailQueueMuitoLongo = "O texto de busca deve ter no máximo 500 caracteres.";

    /// <summary>Parâmetro <c>smtp</c> da URL que não é número.</summary>
    public const string SmtpInvalido = "Escolha o SMTP na lista ou use \"Todos os SMTPs\".";

    public const string SmtpsIndisponiveisTitulo = "Não foi possível carregar a lista de SMTPs.";

    /// <summary>Aviso exibido quando a lista de SMTPs não carrega; a consulta continua possível com todos os SMTPs.</summary>
    public static string SmtpsIndisponiveis(string motivo) =>
        $"A lista de SMTPs não pôde ser carregada: {Frase(motivo)} A consulta continua possível com todos os SMTPs.";
}
