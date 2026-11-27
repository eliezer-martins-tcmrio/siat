# Contrato: leituras na API do GitHub

Todas com a credencial de leitura (`Copilot:GitHub:Token`), exceto `GET /user` no retorno do OAuth, que usa o token do usuário. Decisões em [research.md R1](../research.md#r1-origem-dos-dados-e-credencial-de-leitura), [R5](../research.md#r5-regras-portadas-do-gestao-copilot) e [R9](../research.md#r9-erros-e-mensagens).

## Requisição

- Base `https://api.github.com`; redirecionamento não é seguido.
- Cabeçalhos: `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2026-03-10`, `Authorization: Bearer <token>`, `User-Agent: SIAT`.
- Tempo limite por chamada: `Copilot:GitHub:TempoLimiteSegundos` (padrão 15); o `CancellationToken` do pedido é repassado.
- Logins e slugs entram no caminho com `Uri.EscapeDataString`.

## Chamadas e campos lidos

| Chamada | Campos usados | Observação |
|---|---|---|
| `GET /orgs/{org}/memberships/{login}` | `state`, `role`, `user.id` | 404 = não é membro |
| `GET /user/{id}` | `login`, `name`, `avatar_url` | Perfil público |
| `GET /orgs/{org}/copilot/billing/seats?per_page=100&page={n}` | `total_seats`, `seats[].assignee.id`, `.login` | Para quando `recebidos ≥ total_seats` ou página vazia; recebidos < total → erro de paginação; máximo 1.000 páginas |
| `GET /organizations/{org}/settings/billing/budgets?per_page=100&page={n}` | `budgets[]` (`id`, `budget_scope`, `budget_amount`, `budget_product_sku(s)`, `user`, `budget_entity_name`), `has_next_page` | Só `ai_credits` e escopos `organization`, `multi_user_customer`, `user` |
| `GET /orgs/{org}/teams?per_page=100&page={n}` | `id`, `name`, `slug` | Para quando a página tem menos de 100 |
| `GET /orgs/{org}/teams/{slug}/members?per_page=100&page={n}` | `login`, `inherited` (opcional) | Idem |
| `GET /organizations/{org}/settings/billing/ai_credit/usage?year={a}&month={m}&day={d}&user={login}` | `usageItems[]` (`model`, `unitType`, `grossQuantity`, `grossAmount`, `netAmount`) | Sem `day`: acumulado do mês. `month` e `day` sem zero à esquerda |
| `GET /user` (token do usuário) | `id`, `login` | Só no retorno do OAuth |

## Respostas de erro

| Resposta | Erro |
|---|---|
| 401 | `Erros.Copilot.TokenRecusado` |
| 429, ou 403 com `x-ratelimit-remaining: 0` ou com `retry-after` | `Erros.Copilot.LimiteDeRequisicoes(tenteEm)`, de `retry-after` (segundos) ou `x-ratelimit-reset` (época) |
| 403 (outros) | `Erros.Copilot.AcessoRecusadoPeloGitHub` |
| 404 na associação | `Erros.Copilot.NaoMembro` |
| 404 (outros) | `Erros.Copilot.NaoEncontrado` |
| 5xx, tempo limite, falha de rede | `Erros.Copilot.GitHubIndisponivel` |
| JSON fora do formato, unidade desconhecida, paginação incompleta | `Erros.Copilot.RespostaInesperada` |
| Cancelamento do pedido | `Erros.Copilot.Cancelada` (não é falha) |

O corpo das respostas de erro do GitHub não é mostrado nem registrado.

## Permissões do token de leitura

Fine-grained, dono a organização, **Repository access: Public repositories**, **Organization permissions**: Administration **Read-only**, Members **Read-only**. Se a organização exige aprovação de tokens, um owner aprova. Validade: a maior que a política da organização permitir; a data vai para a agenda de quem opera o SIAT.
