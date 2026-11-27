using AwesomeAssertions;
using Tcmrj.Siat.App.Web;
using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// Lista única de ferramentas, lida pelo menu e pela página Início (.specs/features/pagina-home, critérios 2, 3, 4 e 9;
/// AD-001): só as permitidas, agrupadas em Sistemas, Consultas e Copilot (specs/005-copilot-meu-consumo, FR-002), na ordem do menu, sem grupo vazio.
/// </summary>
public sealed class FerramentasTests
{
    private static Task<IReadOnlyList<GrupoDeFerramentas>> PermitidasCom(params Modulo[] modulos) =>
        Ferramentas.PermitidasAsync(modulo => Task.FromResult(modulos.Contains(modulo)));

    private static string[] Resumo(IReadOnlyList<GrupoDeFerramentas> grupos) =>
        [.. grupos.Select(g => $"{g.Nome}: {string.Join(", ", g.Ferramentas.Select(f => f.Rotulo))}")];

    [Fact]
    public async Task Com_todas_as_permissoes_traz_Sistemas_Consultas_e_Copilot_na_ordem_do_menu()
    {
        var grupos = await PermitidasCom(Enum.GetValues<Modulo>());

        Resumo(grupos).Should().Equal("Sistemas: Catálogo, Versões", "Consultas: MailQueue, AuditLog", "Copilot: Meu consumo");
    }

    [Fact]
    public async Task So_com_MailQueue_traz_apenas_o_grupo_Consultas_com_MailQueue()
    {
        var grupos = await PermitidasCom(Modulo.MailQueue);

        Resumo(grupos).Should().Equal("Consultas: MailQueue");
    }

    [Fact]
    public async Task Com_Catalogo_e_AuditLog_traz_um_item_em_cada_grupo()
    {
        var grupos = await PermitidasCom(Modulo.AuditLog, Modulo.Catalogo_Sistemas);

        Resumo(grupos).Should().Equal("Sistemas: Catálogo", "Consultas: AuditLog");
    }

    [Fact]
    public async Task So_com_CopilotProprio_traz_apenas_o_grupo_Copilot()
    {
        var grupos = await PermitidasCom(Modulo.CopilotProprio);

        Resumo(grupos).Should().Equal("Copilot: Meu consumo");
    }

    [Fact]
    public async Task Sem_nenhuma_permissao_nao_traz_grupo()
    {
        var grupos = await PermitidasCom();

        grupos.Should().BeEmpty();
    }

    [Fact]
    public async Task Pergunta_a_permissao_de_cada_modulo_uma_vez()
    {
        var perguntados = new List<Modulo>();

        await Ferramentas.PermitidasAsync(modulo =>
        {
            perguntados.Add(modulo);
            return Task.FromResult(false);
        });

        perguntados.Should().Equal(Modulo.Catalogo_Sistemas, Modulo.Versoes_Sistemas, Modulo.MailQueue, Modulo.AuditLog, Modulo.CopilotProprio);
    }

    [Fact]
    public void Cada_ferramenta_tem_rota_icone_e_descricao_do_plano()
    {
        Ferramentas.Grupos.SelectMany(g => g.Ferramentas)
            .Select(f => (f.Modulo, f.Rotulo, f.Rota, f.Icone, f.Descricao))
            .Should().Equal(
                (Modulo.Catalogo_Sistemas, "Catálogo", "sistemas/catalogo", "server",
                    "Onde cada sistema roda: URL, máquina e caminho por ambiente."),
                (Modulo.Versoes_Sistemas, "Versões", "sistemas/versoes", "git-compare",
                    "Versão de cada sistema em DSV, HML e PRD, nas duas máquinas de cada ambiente."),
                (Modulo.MailQueue, "MailQueue", "consultas/mailqueue", "mail",
                    "E-mails enfileirados pelos sistemas, do mais recente para o mais antigo."),
                (Modulo.AuditLog, "AuditLog", "consultas/auditlog", "list",
                    "Registros de auditoria (AUDITORIADB · audit.Log), do mais recente para o mais antigo."),
                (Modulo.CopilotProprio, "Meu consumo", "copilot/meu-consumo", "bot",
                    "Seu consumo de AI Credits do Copilot: período, modelos e limite."));
    }
}
