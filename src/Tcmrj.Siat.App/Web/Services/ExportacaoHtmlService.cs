using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// HTML autocontido da exportação ([contracts/exportacao-html.md], research.md R12).
/// <list type="bullet">
/// <item>Sem nenhum <c>src</c> ou <c>href</c>: CSS e script embutidos; a CSP do documento impede carregar qualquer
/// recurso ou fazer chamadas (inclusive ao SIAT).</item>
/// <item>Todo texto vindo do banco passa pelo <see cref="HtmlEncoder"/> (conteúdo e atributos).</item>
/// <item>Itens como <c>&lt;details&gt;</c> (abrem e fecham sem script, também pelo teclado), na ordem recebida.</item>
/// <item>MailQueue: corpo de cada e-mail em <c>&lt;iframe sandbox="" srcdoc&gt;</c>, com a mesma CSP de R10 no início
/// do <c>srcdoc</c> (o mesmo documento do componente <c>CorpoEmail</c>).</item>
/// <item>AuditLog: stack trace e LogEvent já formatados por <see cref="IFormatadorStackTrace"/> e
/// <see cref="IFormatadorLogEvent"/>.</item>
/// </list>
/// </summary>
public sealed class ExportacaoHtmlService(IFormatadorStackTrace formatadorStackTrace, IFormatadorLogEvent formatadorLogEvent)
    : IExportacaoHtmlService
{
    /// <summary>Política do documento do corpo do e-mail (R10); igual à de <c>CorpoEmail.MetaCsp</c>.</summary>
    public const string MetaCspCorpo =
        "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">";

    /// <summary>
    /// Política do arquivo: só o CSS e o script embutidos; nada de rede (fetch, imagens, fontes, mídia, formulários).
    /// Sem <c>default-src</c> e sem <c>frame-src</c> para não bloquear os <c>iframe srcdoc</c> dos corpos, que herdam
    /// esta política e ainda têm a sua própria (<see cref="MetaCspCorpo"/>, com <c>default-src 'none'</c>).
    /// </summary>
    public const string CspArquivo =
        "script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:; connect-src 'none'; font-src 'none'; " +
        "media-src 'none'; object-src 'none'; manifest-src 'none'; worker-src 'none'; base-uri 'none'; form-action 'none'";

    private const string FormatoData = "dd/MM/yyyy HH:mm:ss";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // Acentos ficam como estão (o arquivo é UTF-8); <, >, &, aspas e apóstrofo são sempre codificados.
    private static readonly HtmlEncoder Codificador = HtmlEncoder.Create(UnicodeRanges.All);

    private static readonly UTF8Encoding Utf8SemBom = new(encoderShouldEmitUTF8Identifier: false);

    private const string CabecalhoSrcdoc =
        MetaCspCorpo +
        "<meta charset=\"utf-8\"><base target=\"_blank\">" +
        "<style>body{margin:0;padding:12px;font-family:\"Segoe UI\",Arial,sans-serif;font-size:14px;color:#222;background:#fff}</style>";

    private const string Estilo = """
        :root{--ink:oklch(22.5% 0.005 250);--muted:oklch(47% 0.01 250);--line:oklch(90% 0.009 242.8);--soft:oklch(97.7% 0.004 236.5);
        --accent-soft:oklch(94.9% 0.016 221.1);--accent-ink:oklch(38.5% 0.072 230.9);--ok:oklch(45% 0.097 157.6);--ok-soft:oklch(95.2% 0.022 160.2);
        --warn:oklch(47% 0.11 65);--warn-soft:oklch(96.5% 0.035 84);--bad:oklch(47.3% 0.156 25.4);--bad-soft:oklch(95.2% 0.022 17.6);
        --env-dsv:oklch(45% 0.13 265);--code-bg:oklch(25% 0.012 250);--code-line:oklch(38% 0.015 250);--code-ink:oklch(84% 0.012 250)}
        *{box-sizing:border-box}[hidden]{display:none !important}
        body{margin:0 auto;max-width:1200px;padding:20px 24px 40px;font:14px/1.5 "Segoe UI",system-ui,sans-serif;color:var(--ink);background:#fff}
        h1{font-size:22px;margin:16px 0 12px}
        .mono,pre,code{font-family:"Cascadia Mono",ui-monospace,Consolas,monospace}
        .num{font-variant-numeric:tabular-nums}.suave{color:var(--muted)}
        .contexto{display:flex;flex-wrap:wrap;align-items:center;gap:6px 12px;padding:8px 14px;border:1px solid var(--amb-line);border-radius:10px;background:var(--amb-soft);color:var(--ink)}
        .contexto .aviso{margin-left:auto;color:var(--amb);font-weight:600}
        [data-amb="dsv"]{--amb:oklch(45% 0.13 265);--amb-soft:oklch(95.5% 0.022 265);--amb-line:oklch(80% 0.07 265)}
        [data-amb="hml"]{--amb:oklch(46% 0.105 68);--amb-soft:oklch(96.5% 0.04 86);--amb-line:oklch(80% 0.11 80)}
        [data-amb="prd"]{--amb:oklch(46% 0.16 27);--amb-soft:oklch(95.5% 0.025 20);--amb-line:oklch(80% 0.08 22)}
        .contexto[data-amb="prd"]{background:oklch(44% 0.155 27);border-color:oklch(44% 0.155 27);color:#fff}
        .contexto[data-amb="prd"] .aviso{color:inherit}
        .amb{display:inline-block;padding:0 6px;border:1px solid var(--amb-line);border-radius:4px;color:var(--amb);font-size:12px;font-weight:700;letter-spacing:.04em}
        .contexto[data-amb="prd"] .amb{border-color:rgb(255 255 255 / .5);background:rgb(255 255 255 / .16);color:inherit}
        dl.meta{display:grid;grid-template-columns:max-content 1fr;gap:4px 16px;margin:0 0 16px;padding:12px 14px;border:1px solid var(--line);border-radius:10px}
        dl.meta dt,dl.campos dt{color:var(--muted)}dl dd{margin:0;overflow-wrap:anywhere}
        .barra{display:flex;flex-wrap:wrap;align-items:center;gap:8px 12px;margin:0 0 10px}
        .barra input{flex:1 1 280px;max-width:520px;min-height:36px;padding:6px 10px;border:1px solid oklch(60% 0.035 244);border-radius:7px;font:inherit}
        .barra .contador{color:var(--muted);font-size:13px;font-variant-numeric:tabular-nums}
        .barra .acoes{margin-left:auto;display:flex;gap:8px}
        button{min-height:32px;padding:4px 12px;border:1px solid oklch(60% 0.035 244);border-radius:7px;background:#fff;color:var(--ink);font:inherit;font-size:13px;cursor:pointer}
        :focus-visible{outline:3px solid oklch(63.8% 0.101 222.4);outline-offset:2px}
        .lista{border:1px solid var(--line);border-radius:10px;overflow:hidden}
        details.item{border-bottom:1px solid var(--line)}details.item:last-child{border-bottom:0}
        details.item>summary{display:grid;grid-template-columns:var(--colunas);gap:12px;align-items:center;padding:8px 14px;cursor:pointer;font-size:13px;list-style:none}
        details.item>summary::-webkit-details-marker{display:none}
        details.item>summary::before{content:"\25B8";color:var(--muted)}details.item[open]>summary::before{content:"\25BE"}
        details.item[open]>summary{background:var(--soft)}
        .cel{min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.direita{text-align:right}
        .detalhe{display:grid;gap:12px;padding:12px 14px 16px 40px;border-top:1px solid var(--line)}
        dl.campos{display:grid;grid-template-columns:130px 1fr;gap:4px 14px;margin:0;font-size:13px}
        .secao{display:block;margin:0 0 4px;color:var(--muted);font-size:12px;font-weight:600;text-transform:uppercase;letter-spacing:.04em}
        .pilula{display:inline-flex;align-items:center;gap:6px;padding:1px 8px;border-radius:999px;font-size:12px;font-weight:600;white-space:nowrap}
        .pilula::before{content:"";width:6px;height:6px;border-radius:50%;background:currentColor}.pilula.sem-ponto::before{content:none}
        .pilula.ok{background:var(--ok-soft);color:var(--ok)}.pilula.alerta{background:var(--warn-soft);color:var(--warn)}
        .pilula.info{background:var(--accent-soft);color:var(--accent-ink)}.pilula.erro{background:var(--bad-soft);color:var(--bad)}
        .nivel{font-size:12px;font-weight:600;color:var(--muted)}.nivel.erro{color:var(--bad)}.nivel.alerta{color:var(--warn)}
        iframe.corpo{width:100%;height:320px;resize:vertical;border:1px solid var(--line);border-radius:7px;background:#fff}
        pre.codigo{margin:0;padding:10px 12px;border:1px solid var(--line);border-radius:7px;background:var(--soft);white-space:pre-wrap;word-break:break-word;font-size:12px;line-height:1.6}
        pre.codigo.escuro{background:var(--code-bg);border-color:var(--code-line);color:var(--code-ink)}
        .st-tipo{font-weight:700;color:oklch(96% 0.005 250)}.st-at,.st-ns,.st-param{color:oklch(62% 0.015 250)}
        .st-metodo{font-weight:600;color:oklch(93% 0.008 250)}.st-tipo-param{color:oklch(74% 0.16 150)}
        .st-arquivo{color:oklch(72% 0.13 250)}.st-linha-rotulo{color:oklch(68% 0.19 25)}
        .json-chave{color:var(--accent-ink)}.json-texto{color:var(--ok)}.json-numero{color:var(--env-dsv)}.json-literal{color:var(--warn);font-weight:600}
        .estado{padding:24px 14px;color:var(--muted);text-align:center}
        footer{margin-top:16px;color:var(--muted);font-size:12px}
        @media print{.barra{display:none}details.item>summary::before{content:none}}
        """;

    // Filtro (sem diferenciar acentos e maiúsculas, vários termos, "X de Y"), atalho "/", Esc e abrir/fechar todos.
    private const string Script = """
        (function () {
          'use strict';
          var filtro = document.getElementById('filtro');
          var contador = document.getElementById('contador');
          var vazio = document.getElementById('vazio');
          var unidade = document.body.getAttribute('data-unidade') || 'itens';
          var itens = Array.prototype.slice.call(document.querySelectorAll('#lista > details.item'));
          function normaliza(t) { return String(t || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); }
          var textos = itens.map(function (i) { return normaliza(i.getAttribute('data-busca')); });
          function filtrar() {
            var termos = normaliza(filtro.value).split(/\s+/).filter(Boolean);
            var visiveis = 0;
            itens.forEach(function (item, n) {
              var ok = termos.every(function (t) { return textos[n].indexOf(t) >= 0; });
              item.hidden = !ok;
              if (ok) { visiveis++; }
            });
            contador.textContent = !itens.length ? '' : termos.length
              ? visiveis + ' de ' + itens.length + ' ' + unidade
              : itens.length + ' ' + unidade;
            vazio.hidden = visiveis > 0 || !itens.length;
          }
          filtro.addEventListener('input', filtrar);
          filtro.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && filtro.value) { e.preventDefault(); filtro.value = ''; filtrar(); }
          });
          document.addEventListener('keydown', function (e) {
            var alvo = document.activeElement;
            var digitando = alvo && /^(INPUT|SELECT|TEXTAREA)$/.test(alvo.tagName);
            if (e.key === '/' && !digitando && !e.ctrlKey && !e.metaKey && !e.altKey) { e.preventDefault(); filtro.focus(); filtro.select(); }
          });
          function abrir(valor) { itens.forEach(function (i) { if (!i.hidden) { i.open = valor; } }); }
          document.getElementById('abrir-todos').addEventListener('click', function () { abrir(true); });
          document.getElementById('fechar-todos').addEventListener('click', function () { abrir(false); });
          filtrar();
        })();
        """;

    /// <summary>Nome do arquivo: <c>mailqueue_AAAAMMDD_HHMMSS.html</c> ou <c>auditlog_AAAAMMDD_HHMMSS.html</c>.</summary>
    public static string NomeArquivo(Modulo modulo, DateTime exportadoEm)
    {
        var prefixo = modulo switch
        {
            Modulo.MailQueue => "mailqueue",
            Modulo.AuditLog => "auditlog",
            _ => throw new ArgumentOutOfRangeException(nameof(modulo), modulo, null),
        };

        return $"{prefixo}_{exportadoEm.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.html";
    }

    /// <summary>Quem exportou, pelas claims do SIA: "Nome (matrícula 12345)".</summary>
    public static string Autor(ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var nome = usuario.FindFirst("nome")?.Value;
        var matricula = usuario.FindFirst("matricula")?.Value;
        var descricaoMatricula = string.IsNullOrWhiteSpace(matricula) ? "matrícula não informada" : $"matrícula {matricula}";
        return string.IsNullOrWhiteSpace(nome) ? descricaoMatricula : $"{nome} ({descricaoMatricula})";
    }

    /// <summary>Documento do iframe do corpo: CSP, estilo base e o corpo (HTML como veio, ou texto escapado em &lt;pre&gt;).</summary>
    public static string SrcdocDoCorpo(string? corpo, bool ehHtml) =>
        CabecalhoSrcdoc + (ehHtml
            ? corpo ?? string.Empty
            : $"<pre style=\"white-space:pre-wrap;margin:0;font:inherit\">{Codificar(corpo)}</pre>");

    public ArquivoExportado MailQueue(CabecalhoExportacao cabecalho, IReadOnlyList<MensagemVM> mensagens)
    {
        ArgumentNullException.ThrowIfNull(cabecalho);
        ArgumentNullException.ThrowIfNull(mensagens);

        var html = new StringBuilder(4096 + (mensagens.Count * 2048));
        Abrir(html, cabecalho, mensagens.Count, "e-mails", "16px 84px minmax(0,1fr) minmax(0,230px) 120px");

        foreach (var m in mensagens)
        {
            html.Append("<details class=\"item\" data-chave=\"").Append(Codificar(m.MessageId))
                .Append("\" data-busca=\"").Append(Codificar(m.TextoBusca)).Append("\">");

            html.Append("<summary><span><span class=\"pilula ").Append(Codificar(m.SituacaoClasse)).Append("\">")
                .Append(Codificar(m.SituacaoRotulo)).Append("</span></span>");
            html.Append("<span class=\"cel\">").Append(m.TemAssunto ? Codificar(m.Assunto) : Suave(MensagemVM.SemAssunto)).Append("</span>");
            html.Append("<span class=\"cel suave\">");
            if (m.PrimeiroDestinatario is { } primeiro)
            {
                html.Append(Codificar(primeiro));
                if (m.DemaisDestinatarios > 0)
                {
                    html.Append(" +").Append(m.DemaisDestinatarios.ToString(CultureInfo.InvariantCulture));
                }
            }
            else
            {
                html.Append(Codificar(MensagemEnfileirada.SemDestinatarios));
            }

            html.Append("</span><span class=\"cel suave direita num\" title=\"").Append(Codificar(m.PostadoEmCompleto)).Append("\">")
                .Append(Codificar(m.PostadoEmRelativo)).Append("</span></summary>");

            html.Append("<div class=\"detalhe\"><dl class=\"campos\">");
            Campo(html, "Assunto", m.TemAssunto ? Codificar(m.Assunto) : Suave(MensagemVM.SemAssunto));
            Campo(html, "Para", m.ListaDestinatarios.Count > 0 ? Codificar(m.Destinatarios) : Suave(MensagemEnfileirada.SemDestinatarios));
            Campo(html, "Situação", $"<span class=\"pilula {Codificar(m.SituacaoClasse)}\">{Codificar(m.SituacaoDescricao)}</span>");
            Campo(html, "Postado em", Codificar(m.PostadoEmCompleto), "num");
            Campo(html, "Enviar após", m.EnviarAposTexto is { } enviarApos ? Codificar(enviarApos) : Suave("não informado"), "num");
            Campo(html, "Data de envio", m.DataEnvioTexto is { } dataEnvio ? Codificar(dataEnvio) : Suave(MensagemVM.NaoEnviado), "num");
            Campo(html, "MessageId", Codificar(m.MessageId), "mono");
            html.Append("</dl><div><span class=\"secao\">Corpo</span>")
                .Append("<iframe class=\"corpo\" sandbox=\"\" loading=\"lazy\" title=\"Corpo da mensagem\" srcdoc=\"")
                .Append(Codificar(SrcdocDoCorpo(m.Corpo, m.EhHtml)))
                .Append("\"></iframe></div></div></details>\n");
        }

        Fechar(html, "Fonte: banco MailQueue do servidor indicado, lido pela conta de serviço somente de leitura.");
        return Arquivo(cabecalho, html);
    }

    public ArquivoExportado AuditLog(
        CabecalhoExportacao cabecalho,
        IReadOnlyList<RegistroAuditoriaVM> registros,
        IReadOnlyDictionary<int, string> nomesAplicacoes)
    {
        ArgumentNullException.ThrowIfNull(cabecalho);
        ArgumentNullException.ThrowIfNull(registros);
        ArgumentNullException.ThrowIfNull(nomesAplicacoes);

        var html = new StringBuilder(4096 + (registros.Count * 1536));
        Abrir(html, cabecalho, registros.Count, "logs", "16px 92px 76px minmax(0,1fr) minmax(0,170px) 140px");

        foreach (var r in registros)
        {
            var nomeAplicacao = nomesAplicacoes.TryGetValue(r.AppId, out var nome) ? nome : null;
            var data = r.Data.ToString(FormatoData, PtBr);

            html.Append("<details class=\"item\" data-chave=\"").Append(Codificar(r.Chave))
                .Append("\" data-busca=\"").Append(Codificar(r.TextoBusca(nomeAplicacao))).Append("\">");

            html.Append("<summary><span class=\"nivel ").Append(Codificar(r.ClasseNivel)).Append("\">").Append(Codificar(r.Tipo)).Append("</span>");
            html.Append("<span class=\"cel mono suave\">").Append(Codificar(r.Chave)).Append("</span>");
            html.Append("<span class=\"cel\">");
            if (r.TemExcecao)
            {
                html.Append("<span class=\"pilula erro sem-ponto\">exceção</span> ");
            }

            html.Append(Codificar(r.ResumoMensagem)).Append("</span>");
            html.Append("<span class=\"cel suave\">").Append(Codificar(r.DescricaoAplicacao(nomeAplicacao))).Append("</span>");
            html.Append("<span class=\"cel suave direita num\">").Append(Codificar(data)).Append("</span></summary>");

            html.Append("<div class=\"detalhe\"><dl class=\"campos\">");
            Campo(html, "Mensagem", string.IsNullOrEmpty(r.Mensagem) ? Suave(RegistroAuditoriaVM.SemMensagem) : Codificar(r.Mensagem), estilo: "white-space:pre-wrap");
            Campo(html, "Tipo", $"<span class=\"nivel {Codificar(r.ClasseNivel)}\">{Codificar(r.Tipo)}</span> {Suave($"TypeID {r.TipoId.ToString(CultureInfo.InvariantCulture)}")}");
            Campo(html, "Data", Codificar(data), "num");
            Campo(html, "Aplicação", (nomeAplicacao is null ? string.Empty : Codificar(nomeAplicacao) + " ") + Suave($"(AppID {r.AppId.ToString(CultureInfo.InvariantCulture)})"));
            Campo(html, "LogID", Codificar(r.Chave), "mono");
            Campo(html, "LogUID", Codificar(r.LogUid ?? "(sem LogUID)"), "mono");
            html.Append("</dl>");

            if (r.TemExcecao)
            {
                html.Append("<div><span class=\"secao\">Exceção</span><pre class=\"codigo escuro\">");
                StackTrace(html, r.Excecao);
                html.Append("</pre></div>");
            }

            html.Append("<div><span class=\"secao\">LogEvent</span>");
            if (string.IsNullOrEmpty(r.LogEvent))
            {
                html.Append(Suave("(sem LogEvent)"));
            }
            else
            {
                html.Append("<pre class=\"codigo\">");
                LogEvent(html, r.LogEvent);
                html.Append("</pre>");
            }

            html.Append("</div></div></details>\n");
        }

        Fechar(html, "Fonte: AUDITORIADB do servidor indicado; nomes de aplicação de CSLIB_UserManager.");
        return Arquivo(cabecalho, html);
    }

    private static void Abrir(StringBuilder html, CabecalhoExportacao c, int total, string unidade, string colunas)
    {
        var modulo = c.Modulo.ToString();
        var servidor = c.Servidor;
        var exportadoEm = c.ExportadoEm.ToString(FormatoData, PtBr);
        var aviso = servidor.Ambiente switch
        {
            Ambiente.Dsv => "Desenvolvimento",
            Ambiente.Hml => "Homologação",
            _ => "Produção: dados de produção, consulta e exportação registradas em log.",
        };

        html.Append("<!DOCTYPE html>\n<html lang=\"pt-BR\">\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"").Append(Codificar(CspArquivo)).Append("\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<meta name=\"generator\" content=\"SIAT\">\n")
            .Append("<title>").Append(Codificar($"{modulo} · {servidor.Nome} · exportado em {exportadoEm}")).Append("</title>\n")
            .Append("<style>\n").Append(Estilo).Append("\n</style>\n</head>\n");

        html.Append("<body data-modulo=\"").Append(Codificar(modulo)).Append("\" data-unidade=\"").Append(Codificar(unidade)).Append("\">\n");

        html.Append("<header>\n<div class=\"contexto\" id=\"contexto\" data-amb=\"").Append(Codificar(servidor.DataAmb)).Append("\">")
            .Append("<span class=\"amb\">").Append(Codificar(servidor.AmbienteDescricao)).Append("</span>")
            .Append("<span>").Append(Codificar(modulo)).Append(" em <strong class=\"mono\">").Append(Codificar(servidor.Nome)).Append("</strong></span>")
            .Append("<span class=\"aviso\">").Append(Codificar(aviso)).Append("</span></div>\n");

        html.Append("<h1>").Append(Codificar($"{modulo} · {servidor.Nome}")).Append("</h1>\n");

        html.Append("<dl class=\"meta\" id=\"cabecalho\">");
        Campo(html, "Módulo", Codificar(modulo));
        Campo(html, "Servidor", Codificar(servidor.Nome), "mono");
        Campo(html, "Ambiente", $"<span class=\"amb\" data-amb=\"{Codificar(servidor.DataAmb)}\">{Codificar(servidor.AmbienteDescricao)}</span>");
        Campo(html, "Filtros", c.Filtros.Count == 0
            ? Suave("sem filtros")
            : string.Join(" · ", c.Filtros.Select(f => $"{Codificar(f.Rotulo)}: {Codificar(f.Valor)}")));
        Campo(html, "Exportado em", Codificar(exportadoEm), "num");
        Campo(html, "Exportado por", Codificar(c.ExportadoPor));
        Campo(html, "Itens", Codificar($"{total.ToString(PtBr)} {unidade}, na ordem da consulta"));
        html.Append("</dl>\n</header>\n<main>\n");

        html.Append("<div class=\"barra\">")
            .Append("<input id=\"filtro\" type=\"search\" autocomplete=\"off\" aria-label=\"Filtrar os itens exportados\" ")
            .Append("placeholder=\"Filtrar os itens exportados ( / )\" value=\"").Append(Codificar(c.FiltroLocal?.Trim())).Append("\">")
            .Append("<span class=\"contador\" id=\"contador\" aria-live=\"polite\"></span>")
            .Append("<span class=\"acoes\"><button type=\"button\" id=\"abrir-todos\">Abrir todos</button>")
            .Append("<button type=\"button\" id=\"fechar-todos\">Fechar todos</button></span></div>\n");

        html.Append("<div class=\"lista\" id=\"lista\" style=\"--colunas:").Append(Codificar(colunas)).Append("\">\n");
    }

    private static void Fechar(StringBuilder html, string fonte)
    {
        html.Append("</div>\n<p class=\"estado\" id=\"vazio\" hidden>Nenhum item exportado corresponde ao filtro.</p>\n</main>\n")
            .Append("<footer>").Append(Codificar(fonte)).Append(" Arquivo gerado pelo SIAT; abre sem o SIAT.</footer>\n")
            .Append("<script>\n").Append(Script).Append("\n</script>\n</body>\n</html>\n");
    }

    private static ArquivoExportado Arquivo(CabecalhoExportacao cabecalho, StringBuilder html) =>
        new(NomeArquivo(cabecalho.Modulo, cabecalho.ExportadoEm), Utf8SemBom.GetBytes(html.ToString()));

    private static void Campo(StringBuilder html, string rotulo, string valorHtml, string? classe = null, string? estilo = null)
    {
        html.Append("<dt>").Append(Codificar(rotulo)).Append("</dt><dd");
        if (classe is not null)
        {
            html.Append(" class=\"").Append(classe).Append('"');
        }

        if (estilo is not null)
        {
            html.Append(" style=\"").Append(estilo).Append('"');
        }

        html.Append('>').Append(valorHtml).Append("</dd>");
    }

    private void StackTrace(StringBuilder html, string? texto)
    {
        var linhas = formatadorStackTrace.Formatar(texto);
        for (var i = 0; i < linhas.Count; i++)
        {
            if (i > 0)
            {
                html.Append('\n');
            }

            foreach (var trecho in linhas[i].Trechos)
            {
                Trecho(html, ClasseStackTrace(trecho.Tipo), trecho.Texto);
            }
        }
    }

    private void LogEvent(StringBuilder html, string texto)
    {
        foreach (var trecho in formatadorLogEvent.Formatar(texto).Trechos)
        {
            Trecho(html, ClasseJson(trecho.Tipo), trecho.Texto);
        }
    }

    // Quebras de linha ficam literais dentro do <pre>; o resto do texto é codificado.
    private static void Trecho(StringBuilder html, string? classe, string texto)
    {
        if (classe is not null)
        {
            html.Append("<span class=\"").Append(classe).Append("\">");
        }

        var partes = texto.Split('\n');
        for (var i = 0; i < partes.Length; i++)
        {
            if (i > 0)
            {
                html.Append('\n');
            }

            html.Append(Codificar(partes[i].TrimEnd('\r')));
        }

        if (classe is not null)
        {
            html.Append("</span>");
        }
    }

    // Mesmas classes dos componentes BlocoStackTrace e BlocoLogEvent (siat.css).
    private static string? ClasseStackTrace(TipoTrechoStackTrace tipo) => tipo switch
    {
        TipoTrechoStackTrace.TipoExcecao => "st-tipo",
        TipoTrechoStackTrace.At => "st-at",
        TipoTrechoStackTrace.Namespace => "st-ns",
        TipoTrechoStackTrace.Metodo => "st-metodo",
        TipoTrechoStackTrace.TipoParametro => "st-tipo-param",
        TipoTrechoStackTrace.Parametro => "st-param",
        TipoTrechoStackTrace.Arquivo => "st-arquivo",
        TipoTrechoStackTrace.RotuloLinha => "st-linha-rotulo",
        TipoTrechoStackTrace.Separador => "st-ns",
        _ => null,
    };

    private static string? ClasseJson(TipoTrechoJson tipo) => tipo switch
    {
        TipoTrechoJson.Chave => "json-chave",
        TipoTrechoJson.Texto => "json-texto",
        TipoTrechoJson.Numero => "json-numero",
        TipoTrechoJson.Literal => "json-literal",
        _ => null,
    };

    private static string Suave(string texto) => $"<span class=\"suave\">{Codificar(texto)}</span>";

    private static string Codificar(string? texto) => string.IsNullOrEmpty(texto) ? string.Empty : Codificador.Encode(texto);
}
