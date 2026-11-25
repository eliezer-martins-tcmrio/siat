# Revisão: Página inicial

Revisado: commits após `04a0bd9` (só `2842733`) e o que não está commitado (só a linha "Review" do `plan.md`).
Fontes: `request.md`, `plan.md` (aprovado), `quickstart.md`. A pasta `captures/` não existe; o plano declara que não
houve captura ("Larguras: não há captura") e nenhuma fonte vinculante além do pedido.

## Veredito

**Aprovado, sem problema que bloqueie.** O pedido foi atendido: `/` deixou de ir para `sistemas/catalogo` e virou a
página "Início" com atalhos. Os 11 critérios estão implementados e conferidos (testes e app rodando). Ficam três
observações menores, nenhuma exige correção para entregar.

## Como foi verificado

- `dotnet test --solution src/Siat.slnx`: 865 aprovados, 0 falhas.
- App rodando (`preview_start siat`, https://localhost:7271, usuário de desenvolvimento com as quatro permissões),
  a 1280 px e a 375 px:
  - `/` fica em `/`, aba "Início · SIAT", h1 "Início", descrição "Sistema Integrado de Apoio Técnico do TCMRio.",
    grupos Sistemas [Catálogo, Versões] e Consultas [MailQueue, AuditLog], com ícone e as descrições exatas do critério 4.
  - Marca do menu a partir de `/sistemas/versoes`: vai para `/`, `history.length` passa de 4 para 5 (só a entrada do
    clique; nenhum encaminhamento depois); nenhum item do menu fica com `aria-current`; o menu não tem item "Início".
  - Teclado: Tab até o atalho MailQueue mostra o contorno de 3 px para dentro (`outline-offset: -3px`, `:focus-visible`);
    Enter abre `/consultas/mailqueue` na mesma aba e o item MailQueue do menu fica com `aria-current="page"`. Voltar
    (navegador) retorna ao Início.
  - 1280 px: `scrollWidth` = 1280 (sem rolagem horizontal), atalhos em 2 colunas.
  - 375 px: `scrollWidth` = 375; cada atalho com 341 px numa área de conteúdo de 343 px (ocupa a largura do
    conteúdo). A barra superior do menu rola na horizontal dentro dela mesma, como já era.
  - Estado "só Versões" (sem permissão no app real): **simulado no DOM do navegador** removendo os outros atalhos e o
    grupo Consultas, só para ver o layout; o atalho único ocupa a largura toda do painel. Os estados com permissão
    parcial, sem permissão e sem matrícula **não foram vistos no app**: as permissões vêm das claims do SIA num circuito
    Blazor Server sem prerender, então não há resposta HTTP para trocar nem banco para copiar. Esses estados estão
    cobertos pelos testes bUnit (abaixo). Nenhum dado do app foi alterado.

## Pedido, frase a frase

| Pedido | Construído |
|---|---|
| "quere construir uma página home" | `Home.razor` agora é a página "Início" com atalhos agrupados (Sistemas, Consultas), ícone, rótulo e descrição |
| "hoje a home vai direto para sistemas/catalogo" | o `NavigateTo(rota, replace: true)` saiu; nenhum outro ponto do código leva `/` ao Catálogo (login e logout voltam para `~/`, a marca aponta para `""`) |

## Critérios e os testes que os provam

| # | Implementado | Teste | O teste falharia se a regra estivesse errada? |
|---|---|---|---|
| 1 | sim (visto no app) | `HomeTests.Com_ferramentas_fica_no_Inicio_sem_navegar`, `So_com_Versoes_fica_no_Inicio_com_um_atalho`, `A_aba_se_chama_Inicio_SIAT` | Sim para navegação (`History` vazio e `Uri == BaseUri`; um `NavigateTo`, mesmo com `replace`, entra no `History` do bUnit), h1, descrição e contagem. A aba é provada só indiretamente (observação 2) |
| 2 | sim | `HomeTests` (3 casos do critério) e `FerramentasTests` (mesmos 3 casos na lista) | Sim: ordem de grupos e itens com `Equal`; um grupo vazio exibido quebraria `So_com_MailQueue_mostra_apenas_o_grupo_Consultas` |
| 3 | sim | `Sem_AuditLog_a_pagina_nao_cita_AuditLog`, `So_com_Catalogo_a_pagina_nao_cita_as_outras_ferramentas` | Sim: procuram o texto no markup inteiro, então um atalho desabilitado também falharia |
| 4 | sim (textos conferidos no app) | `Cada_atalho_tem_rotulo_descricao_e_rota_da_ferramenta`, `FerramentasTests.Cada_ferramenta_tem_rota_icone_e_descricao_do_plano` | Sim: comparam o texto exato das quatro descrições |
| 5 | sim (clique por teclado no app, item marcado) | `Cada_atalho_...` (rotas), `Atalho_abre_na_mesma_aba`; marcação no menu por `MenuTests.Item_da_pagina_atual_fica_marcado` | Sim para rota e `target`; o teste de `target` olha só o primeiro atalho (todos vêm do mesmo laço, risco baixo) |
| 6 | sim | `Sem_nenhum_modulo_explica_a_quem_pedir_acesso_citando_sistemas_e_consultas`, `Sem_nenhum_modulo_e_sem_matricula_mostra_o_aviso_de_vinculo_de_rede` | Sim: checam h1 "Início", ausência de atalhos, a mensagem com os cinco termos e o aviso de vínculo |
| 7 | sim | `Durante_a_verificacao_nao_mostra_atalhos_nem_falta_de_acesso` | Sim: se `grupos` começasse como lista vazia, `.estado` apareceria durante a espera e o teste falharia |
| 8 | sim | `Exige_so_autenticacao_e_nao_le_fonte_de_dados` | Sim: falha com política/roles no `[Authorize]` ou com qualquer `@inject` além de `IAutorizacaoConsultaService` (que só avalia política sobre as claims) |
| 9 | sim (visto no app) | `MenuTests` (inalterados, todos verdes) + `FerramentasTests` | Grupos, itens, rótulos, rotas e ordem: sim. Um item "Início" quebraria os `Equal` de rótulos. Ícones: só `FerramentasTests` confere (observação 2) |
| 10 | sim (visto no app, 1280 e 375) | nenhum automatizado; roteiro manual seção 5 | Não há teste; verificado no navegador |
| 11 | sim | n/a | `quickstart.md` tem os quatro fluxos pedidos (seções 1 a 4) e mais larguras |

## Observações (menores, não bloqueiam)

1. **Bordas do painel de atalhos.** O painel `.painel.telas` tem `border-radius: 10px` e `overflow: visible`; os atalhos
   têm cantos retos e `box-shadow` de 1 px à esquerda e acima. Além disso, a regra
   `.painel:not(.filtros) > * + * { border-top: 1px }` dá `border-top` ao 2º atalho de cada painel (medido:
   Versões e AuditLog com `border-top-width: 1px`, Catálogo e MailQueue com `0px`). Efeito: a 375 px aparece um fio
   saindo dos cantos arredondados e a linha entre os dois atalhos fica com 2 px; a 1280 px a borda de cima fica mais
   grossa só sobre a coluna da direita; no hover, o fundo quadrado passa dos cantos arredondados. É o mesmo padrão de
   `design/index.html` ("Telas de exemplo"), que o plano mandou reaproveitar, então não contraria o plano; é um
   acabamento a considerar (por exemplo, `overflow: hidden` no painel e anular o `border-top` em `.telas > a`).
2. **Duas provas indiretas.** `A_aba_se_chama_Inicio_SIAT` confere o parâmetro `Titulo` do `CabecalhoPagina`, não o
   título da aba; um `<PageTitle>` solto na `Home` passaria no teste (no app a aba está certa). E os ícones do menu
   (critério 9) só são conferidos na lista (`FerramentasTests`), não no `Menu` renderizado; como o `Menu` agora lê
   `Ferramenta.Icone` da lista, o risco é baixo.
3. **Documentos antigos desatualizados.** `specs/001`, `002` e `003` (`contracts/rotas-ui.md`, FR-004/FR-006) e o
   exemplo entre parênteses da constituição (`.specify/memory/constitution.md:56`) ainda descrevem `/` como
   encaminhamento. O plano tira isso do escopo de propósito ("Out of scope") e registra AD-001 em `.specs/STATE.md`;
   fica só como lembrete para a coordenação.
