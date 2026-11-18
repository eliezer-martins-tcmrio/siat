using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tcmrj.Siat.Web.Components.Shared;

namespace Tcmrj.Siat.Test.Componentes;

/// <summary>Estado carregando (história 4, cenário 1): tempo decorrido e Cancelar só depois de 1 s, com relógio falso.</summary>
public sealed class EstadoCarregandoTests : BunitContext
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 17, 14, 0, 0, TimeSpan.Zero));

    public EstadoCarregandoTests()
    {
        Services.AddSingleton<TimeProvider>(_relogio);
    }

    private IRenderedComponent<EstadoCarregando> Renderizar(Action? aoCancelar = null, long? inicio = null) =>
        Render<EstadoCarregando>(p =>
        {
            p.Add(c => c.Mensagem, @"Consultando TCMBD4\HML…").Add(c => c.Inicio, inicio);
            if (aoCancelar is not null)
            {
                p.Add(c => c.AoCancelar, aoCancelar);
            }
        });

    [Fact]
    public void Antes_de_1_s_mostra_so_a_mensagem()
    {
        var estado = Renderizar(() => { });

        estado.Find("[role=status]").TextContent.Should().Be(@"Consultando TCMBD4\HML…");
        estado.FindAll("#cancelar").Should().BeEmpty();
        estado.FindAll("[data-decorrido]").Should().BeEmpty();

        _relogio.Advance(TimeSpan.FromMilliseconds(999));

        estado.FindAll("#cancelar").Should().BeEmpty();
        estado.FindAll("[data-decorrido]").Should().BeEmpty();
    }

    [Fact]
    public void Depois_de_1_s_mostra_o_tempo_decorrido_e_Cancelar()
    {
        var cancelou = 0;
        var estado = Renderizar(() => cancelou++);

        _relogio.Advance(TimeSpan.FromSeconds(1));

        estado.WaitForAssertion(() => estado.Find("[data-decorrido]").TextContent.Should().Be("1,0 s"));
        var cancelar = estado.Find("#cancelar");
        cancelar.TextContent.Should().Be("Cancelar");
        cancelar.GetAttribute("type").Should().Be("button");

        _relogio.Advance(TimeSpan.FromMilliseconds(1400));
        estado.WaitForAssertion(() => estado.Find("[data-decorrido]").TextContent.Should().Be("2,4 s"));

        estado.Find("#cancelar").Click();
        cancelou.Should().Be(1);
    }

    [Fact]
    public void Sem_AoCancelar_mostra_so_o_tempo()
    {
        var estado = Renderizar();

        _relogio.Advance(TimeSpan.FromSeconds(3));

        estado.WaitForAssertion(() => estado.Find("[data-decorrido]").TextContent.Should().Be("3,0 s"));
        estado.FindAll("#cancelar").Should().BeEmpty();
    }

    [Fact]
    public void Conta_a_partir_do_inicio_informado()
    {
        var inicio = _relogio.GetTimestamp();
        _relogio.Advance(TimeSpan.FromMilliseconds(1500));

        var estado = Renderizar(() => { }, inicio);

        estado.Find("[data-decorrido]").TextContent.Should().Be("1,5 s");
        estado.FindAll("#cancelar").Should().ContainSingle();
    }

    [Fact]
    public void Ao_sair_da_tela_para_o_relogio()
    {
        var estado = Renderizar(() => { });

        estado.Instance.Dispose();
        var renderizacoes = estado.RenderCount;
        _relogio.Advance(TimeSpan.FromSeconds(5));

        estado.RenderCount.Should().Be(renderizacoes);
    }
}
