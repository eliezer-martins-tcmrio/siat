using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.App.Web;

/// <summary>Uma ferramenta do SIAT, como aparece no menu lateral e na página Início.</summary>
/// <param name="Modulo">Módulo cuja permissão libera a ferramenta.</param>
/// <param name="Rotulo">Nome no menu e no atalho da página Início.</param>
/// <param name="Descricao">Uma linha sobre o que a ferramenta faz, no atalho da página Início.</param>
/// <param name="Rota">Rota relativa ao <c>&lt;base href&gt;</c> (<see cref="Navegacao"/>).</param>
/// <param name="Icone">Nome do ícone Lucide.</param>
public sealed record Ferramenta(Modulo Modulo, string Rotulo, string Descricao, string Rota, string Icone);

/// <summary>Grupo de ferramentas (Sistemas, Consultas), com as ferramentas na ordem do menu.</summary>
public sealed record GrupoDeFerramentas(string Nome, IReadOnlyList<Ferramenta> Ferramentas);

/// <summary>
/// Lista única das ferramentas do SIAT, lida pelo menu lateral e pela página Início (.specs/STATE.md, AD-001): uma
/// ferramenta nova entra aqui e aparece nos dois. A ordem dos grupos e das ferramentas é a ordem em que aparecem.
/// </summary>
public static class Ferramentas
{
    public static IReadOnlyList<GrupoDeFerramentas> Grupos { get; } =
    [
        new("Sistemas",
        [
            new(Modulo.Catalogo_Sistemas, "Catálogo", "Onde cada sistema roda: URL, máquina e caminho por ambiente.",
                Navegacao.RotaCatalogo, "server"),
            new(Modulo.Versoes_Sistemas, "Versões",
                "Versão de cada sistema em DSV, HML e PRD, nas duas máquinas de cada ambiente.",
                Navegacao.RotaVersoes, "git-compare"),
        ]),
        new("Consultas",
        [
            new(Modulo.MailQueue, "MailQueue", "E-mails enfileirados pelos sistemas, do mais recente para o mais antigo.",
                Navegacao.RotaMailQueue, "mail"),
            new(Modulo.AuditLog, "AuditLog",
                "Registros de auditoria (AUDITORIADB · audit.Log), do mais recente para o mais antigo.",
                Navegacao.RotaAuditLog, "list"),
        ]),
    ];

    /// <summary>
    /// Grupos só com as ferramentas que <paramref name="podeAcessar"/> libera, na mesma ordem; um grupo sem nenhuma
    /// ferramenta liberada não aparece (.specs/features/pagina-home, critérios 2, 3 e 9). Mostrar ou esconder não é
    /// controle de acesso: cada página autoriza no servidor.
    /// </summary>
    public static async Task<IReadOnlyList<GrupoDeFerramentas>> PermitidasAsync(Func<Modulo, Task<bool>> podeAcessar)
    {
        ArgumentNullException.ThrowIfNull(podeAcessar);

        var permitidos = new List<GrupoDeFerramentas>(Grupos.Count);
        foreach (var grupo in Grupos)
        {
            var ferramentas = new List<Ferramenta>(grupo.Ferramentas.Count);
            foreach (var ferramenta in grupo.Ferramentas)
            {
                if (await podeAcessar(ferramenta.Modulo))
                {
                    ferramentas.Add(ferramenta);
                }
            }

            if (ferramentas.Count > 0)
            {
                permitidos.Add(grupo with { Ferramentas = ferramentas });
            }
        }

        return permitidos;
    }
}
