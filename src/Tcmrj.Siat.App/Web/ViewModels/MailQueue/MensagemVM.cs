using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.ViewModels.MailQueue;

/// <summary>Situação de envio derivada para a tela (data-model, MensagemEnfileirada).</summary>
public enum SituacaoEnvio
{
    Enviado,
    Agendado,
    NaFila,
}

/// <summary>E-mail da MailQueue exibido na lista.</summary>
public sealed partial record MensagemVM
{
    public const string SemAssunto = "(sem assunto)";

    public const string NaoEnviado = "não enviado";

    private const string FormatoData = "dd/MM/yyyy HH:mm";

    private const string FormatoDataCompleta = "dd/MM/yyyy HH:mm:ss";

    /// <summary>Chave da linha na lista e valor de <c>sel</c> na URL.</summary>
    public required string MessageId { get; init; }

    /// <summary>SMTP do e-mail, exibido ao lado da situação; nulo quando o banco não informa.</summary>
    public int? SmtpId { get; init; }

    public string? Assunto { get; init; }

    public DateTime DataPostagem { get; init; }

    public DateTime? EnviarApos { get; init; }

    public DateTime? DataEnvio { get; init; }

    public bool Enviado { get; init; }

    public bool EhHtml { get; init; }

    /// <summary>Exibido só no detalhe, isolado (<c>CorpoEmail</c>).</summary>
    public string? Corpo { get; init; }

    /// <summary>Destinatários como vieram do SQL (<c>"a; b"</c> ou <c>(sem destinatários)</c>).</summary>
    public string Destinatarios { get; init; } = MensagemEnfileirada.SemDestinatarios;

    /// <summary>Destinatários separados; vazia quando não há nenhum.</summary>
    public IReadOnlyList<string> ListaDestinatarios { get; init; } = [];

    public SituacaoEnvio Situacao { get; init; }

    /// <summary>Texto do filtro local: assunto, destinatários, MessageId e corpo sem tags.</summary>
    public string TextoBusca { get; init; } = string.Empty;

    /// <summary>"Postado em" relativo ao momento da consulta: "hoje 14:32:10", "ontem 09:15" ou "15/11/2026 08:00".</summary>
    public string PostadoEmRelativo { get; init; } = string.Empty;

    public bool TemAssunto => !string.IsNullOrWhiteSpace(Assunto);

    /// <summary>Primeiro destinatário (coluna Para), ou nulo sem destinatários.</summary>
    public string? PrimeiroDestinatario => ListaDestinatarios.Count > 0 ? ListaDestinatarios[0] : null;

    /// <summary>Quantos destinatários além do primeiro ("+N" na coluna Para).</summary>
    public int DemaisDestinatarios => Math.Max(ListaDestinatarios.Count - 1, 0);

    /// <summary>Texto da pílula da coluna Situação.</summary>
    public string SituacaoRotulo => Situacao switch
    {
        SituacaoEnvio.Enviado => "Enviado",
        SituacaoEnvio.Agendado => "Agendado",
        _ => "Na fila",
    };

    /// <summary>Classe da pílula (<c>siat.css</c>): ok, alerta ou info.</summary>
    public string SituacaoClasse => Situacao switch
    {
        SituacaoEnvio.Enviado => "ok",
        SituacaoEnvio.Agendado => "alerta",
        _ => "info",
    };

    /// <summary>Situação no detalhe: "Enviado em {DataEnvio}", "Agendado" ou "Na fila".</summary>
    public string SituacaoDescricao => Situacao == SituacaoEnvio.Enviado && DataEnvio is { } envio
        ? $"Enviado em {Formatar(envio)}"
        : SituacaoRotulo;

    /// <summary>SmtpId em texto, ou nulo.</summary>
    public string? SmtpTexto => SmtpId?.ToString(CultureInfo.InvariantCulture);

    public string PostadoEm => Formatar(DataPostagem);

    /// <summary>Data completa, com segundos (<c>title</c> da coluna Postado).</summary>
    public string PostadoEmCompleto => DataPostagem.ToString(FormatoDataCompleta, CultureInfo.InvariantCulture);

    public string? EnviarAposTexto => EnviarApos is { } data ? Formatar(data) : null;

    public string? DataEnvioTexto => DataEnvio is { } data ? Formatar(data) : null;

    /// <param name="agora">Hora local do servidor do SIAT no momento da consulta (para "Agendado" e "hoje"/"ontem").</param>
    public static MensagemVM De(MensagemEnfileirada mensagem, DateTime agora)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        var destinatarios = string.Equals(mensagem.Destinatarios, MensagemEnfileirada.SemDestinatarios, StringComparison.Ordinal)
            ? []
            : mensagem.Destinatarios.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var situacao = mensagem.Enviado
            ? SituacaoEnvio.Enviado
            : mensagem.EnviarApos > agora ? SituacaoEnvio.Agendado : SituacaoEnvio.NaFila;

        return new MensagemVM
        {
            MessageId = mensagem.MessageId,
            SmtpId = mensagem.SmtpId,
            Assunto = mensagem.Assunto,
            DataPostagem = mensagem.DataPostagem,
            EnviarApos = mensagem.EnviarApos,
            DataEnvio = mensagem.DataEnvio,
            Enviado = mensagem.Enviado,
            EhHtml = mensagem.EhHtml,
            Corpo = mensagem.Corpo,
            Destinatarios = mensagem.Destinatarios,
            ListaDestinatarios = destinatarios,
            Situacao = situacao,
            TextoBusca = string.Join(' ', mensagem.Assunto, mensagem.Destinatarios, mensagem.MessageId, TextoPlano(mensagem.Corpo, mensagem.EhHtml)),
            PostadoEmRelativo = Relativo(mensagem.DataPostagem, agora),
        };
    }

    /// <summary>Só o texto visível do corpo, para o filtro local não casar com tags, estilos ou scripts.</summary>
    public static string TextoPlano(string? corpo, bool ehHtml)
    {
        if (string.IsNullOrEmpty(corpo))
        {
            return string.Empty;
        }

        var texto = corpo;
        if (ehHtml)
        {
            texto = BlocosOcultos().Replace(texto, " ");
            texto = Tags().Replace(texto, " ");
            texto = WebUtility.HtmlDecode(texto);
        }

        return Espacos().Replace(texto, " ").Trim();
    }

    /// <summary>Data da lista: hoje com segundos, ontem, ou a data.</summary>
    private static string Relativo(DateTime data, DateTime agora)
    {
        var dias = (agora.Date - data.Date).Days;
        return dias switch
        {
            0 => $"hoje {data.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}",
            1 => $"ontem {data.ToString("HH:mm", CultureInfo.InvariantCulture)}",
            _ => Formatar(data),
        };
    }

    private static string Formatar(DateTime data) => data.ToString(FormatoData, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"<(head|style|script)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlocosOcultos();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();
}
