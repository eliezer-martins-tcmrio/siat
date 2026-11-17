using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Tcmrj.Siat.Web.Components.Shared;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Filtro local (FR-020): contador "X de Y" acompanha o texto; Esc limpa; filtrar conta como interação.</summary>
public sealed class FiltroLocalComponentTests : BunitContext
{
    private static readonly IReadOnlyList<string> Itens =
    [
        "Notificação de prazo",
        "Comunicação processual",
        "NOTIFICACAO de pagamento",
        "Relatório mensal",
    ];

    private readonly FakeTimeProvider _relogio = new();
    private readonly Mock<IInteracao> _interacao = new();
    private string? _texto;

    public FiltroLocalComponentTests()
    {
        Services.AddSingleton<TimeProvider>(_relogio);
        Services.AddSingleton(_interacao.Object);
    }

    private IRenderedComponent<FiltroLocal<string>> Renderizar(string? texto = null)
    {
        _texto = texto;
        return Render<FiltroLocal<string>>(p => p
            .Add(c => c.Itens, Itens)
            .Add(c => c.TextoDe, i => i)
            .Add(c => c.Texto, texto)
            .Add(c => c.TextoChanged, (string? t) => _texto = t)
            .Add(c => c.Unidade, "e-mails")
            .Add(c => c.Acoes, "<button type=\"button\" id=\"exportar\">Exportar HTML</button>")
            .Add(c => c.ChildContent, (IReadOnlyList<string> visiveis) => (RenderFragment)(b =>
            {
                foreach (var item in visiveis)
                {
                    b.OpenElement(0, "p");
                    b.AddAttribute(1, "class", "visivel");
                    b.AddContent(2, item);
                    b.CloseElement();
                }
            })));
    }

    private static string[] Visiveis(IRenderedComponent<FiltroLocal<string>> filtro) =>
        filtro.FindAll("p.visivel").Select(p => p.TextContent).ToArray();

    [Fact]
    public void Sem_filtro_mostra_todos_e_o_total()
    {
        var filtro = Renderizar();

        filtro.Find(".contador").TextContent.Should().Be("4 e-mails");
        Visiveis(filtro).Should().Equal(Itens);
        var campo = filtro.Find("input#filtro-local");
        campo.HasAttribute("data-busca").Should().BeTrue("o atalho / do siat.js procura [data-busca]");
        campo.GetAttribute("type").Should().Be("search");
        filtro.Find(".contador").GetAttribute("aria-live").Should().Be("polite");
        filtro.Find("#exportar").Should().NotBeNull();
    }

    [Fact]
    public void Contador_X_de_Y_acompanha_o_texto_digitado()
    {
        var filtro = Renderizar();

        filtro.Find("input").Input("notificacao");
        filtro.Find(".contador").TextContent.Should().Be("2 de 4 e-mails");
        Visiveis(filtro).Should().Equal("Notificação de prazo", "NOTIFICACAO de pagamento");
        _texto.Should().Be("notificacao");

        filtro.Find("input").Input("notificação prazo");
        filtro.Find(".contador").TextContent.Should().Be("1 de 4 e-mails");
        Visiveis(filtro).Should().Equal("Notificação de prazo");

        filtro.Find("input").Input("inexistente");
        filtro.Find(".contador").TextContent.Should().Be("0 de 4 e-mails");
        Visiveis(filtro).Should().BeEmpty();
    }

    [Fact]
    public void Esc_limpa_o_filtro_e_volta_a_mostrar_todos()
    {
        var filtro = Renderizar();
        filtro.Find("input").Input("relatorio");
        Visiveis(filtro).Should().ContainSingle();

        filtro.Find("input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        _texto.Should().BeEmpty();
        filtro.Find("input").GetAttribute("value").Should().BeEmpty();
        filtro.Find(".contador").TextContent.Should().Be("4 e-mails");
        Visiveis(filtro).Should().Equal(Itens);
    }

    [Fact]
    public void Outras_teclas_nao_limpam()
    {
        var filtro = Renderizar();
        filtro.Find("input").Input("prazo");

        filtro.Find("input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });

        _texto.Should().Be("prazo");
        Visiveis(filtro).Should().ContainSingle();
    }

    [Fact]
    public void Texto_vindo_do_pai_ja_filtra()
    {
        var filtro = Renderizar("comunicacao");

        filtro.Find(".contador").TextContent.Should().Be("1 de 4 e-mails");
        filtro.Find("input").GetAttribute("value").Should().Be("comunicacao");
    }

    [Fact]
    public void Filtrar_conta_como_interacao_no_maximo_a_cada_5_s()
    {
        var filtro = Renderizar();

        filtro.Find("input").Input("n");
        filtro.Find("input").Input("no");
        filtro.Find("input").Input("not");
        _interacao.Verify(i => i.Interagir(), Times.Once);

        _relogio.Advance(TimeSpan.FromSeconds(5));
        filtro.Find("input").Input("noti");
        _interacao.Verify(i => i.Interagir(), Times.Exactly(2));
    }
}
