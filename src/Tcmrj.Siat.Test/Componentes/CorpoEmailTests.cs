using AwesomeAssertions;
using Bunit;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Corpo do e-mail isolado (R10, FR-012): iframe com sandbox vazio e CSP no início do srcdoc.</summary>
public sealed class CorpoEmailTests : BunitContext
{
    private const string MetaCsp =
        "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">";

    private IRenderedComponent<CorpoEmail> Renderizar(string? corpo, bool ehHtml) =>
        Render<CorpoEmail>(p => p.Add(c => c.Corpo, corpo).Add(c => c.EhHtml, ehHtml));

    [Fact]
    public void Iframe_tem_sandbox_vazio()
    {
        var iframe = Renderizar("<p>Olá</p>", ehHtml: true).Find("iframe");

        iframe.HasAttribute("sandbox").Should().BeTrue();
        iframe.GetAttribute("sandbox").Should().BeEmpty();
        iframe.GetAttribute("title").Should().NotBeNullOrWhiteSpace();
        iframe.ClassList.Should().Contain("corpo-email");
    }

    [Fact]
    public void Srcdoc_comeca_pela_meta_CSP()
    {
        var srcdoc = Renderizar("<p>Olá</p>", ehHtml: true).Find("iframe").GetAttribute("srcdoc");

        srcdoc.Should().StartWith(MetaCsp);
    }

    [Fact]
    public void Corpo_HTML_vai_como_veio_depois_da_CSP()
    {
        const string corpo = "<p>Olá, <b>Ana</b>.</p><script>alert('x')</script><img src=\"https://externo.exemplo/a.png\">";

        var srcdoc = Renderizar(corpo, ehHtml: true).Find("iframe").GetAttribute("srcdoc");

        srcdoc.Should().EndWith(corpo);
        srcdoc.IndexOf(MetaCsp, StringComparison.Ordinal).Should().BeLessThan(srcdoc.IndexOf(corpo, StringComparison.Ordinal));
    }

    [Fact]
    public void Texto_simples_vai_escapado_dentro_de_pre()
    {
        const string corpo = "Linha 1 <b>não é negrito</b> & \"aspas\"\nLinha 2";

        var srcdoc = Renderizar(corpo, ehHtml: false).Find("iframe").GetAttribute("srcdoc");

        srcdoc.Should().StartWith(MetaCsp);
        srcdoc.Should().Contain("<pre");
        srcdoc.Should().Contain("Linha 1 &lt;b&gt;não é negrito&lt;/b&gt; &amp; &quot;aspas&quot;\nLinha 2</pre>");
        srcdoc.Should().NotContain("<b>");
    }

    [Fact]
    public void Corpo_vazio_ainda_tem_a_CSP()
    {
        var srcdoc = Renderizar(null, ehHtml: true).Find("iframe").GetAttribute("srcdoc");

        srcdoc.Should().StartWith(MetaCsp);
    }
}
