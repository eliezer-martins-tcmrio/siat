namespace Tcmrj.Siat.App.Web.Validators;

/// <summary>Textos do AuditLog (FR-013).</summary>
public static partial class Mensagens
{
    public const string LogUidInvalido = "Informe o início do LogUID só com dígitos hexadecimais (0-9, A-F) e hífens.";

    public const string DiasNaoNumerico = "Informe um número de dias entre 1 e 3650.";

    public const string DataInvalida = "Informe uma data válida.";

    public const string PeriodoInvertido = "A data inicial deve ser igual ou anterior à data final.";

    public const string AppIdNaoNumerico = "Informe o AppID da aplicação com um número inteiro.";

    public const string AplicacoesIndisponiveisTitulo = "Não foi possível carregar a lista de aplicações.";

    /// <summary>Aviso exibido quando a lista de aplicações não carrega e a tela passa a pedir o AppID.</summary>
    public static string AplicacoesIndisponiveis(string motivo) =>
        $"A lista de aplicações não pôde ser carregada: {Frase(motivo)} Informe o AppID no campo numérico ou deixe em branco para consultar todas as aplicações.";
}
