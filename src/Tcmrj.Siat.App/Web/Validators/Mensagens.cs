namespace Tcmrj.Siat.App.Web.Validators;

/// <summary>
/// Textos de validação em pt-BR. Classe parcial: cada módulo acrescenta os seus em
/// <c>Mensagens.MailQueue.cs</c> e <c>Mensagens.AuditLog.cs</c>.
/// </summary>
public static partial class Mensagens
{
    public const string ServidorObrigatorio = "Escolha o servidor.";

    public const string QuantidadeForaDoPadrao = "Informe um número entre 1 e 1000 na quantidade.";

    /// <summary>Motivo como frase: sem espaços nas pontas e terminado em ponto, para emendar a orientação seguinte.</summary>
    private static string Frase(string motivo)
    {
        var texto = motivo.Trim();
        return texto.Length == 0 || texto[^1] is '.' or '!' or '?' ? texto : texto + ".";
    }
}
