# Modelo de dados: Meu consumo do Copilot

**Feature**: [spec.md](./spec.md) | **Pesquisa**: [research.md](./research.md)

Nada é persistido pelo SIAT (Princípio I). As entidades abaixo existem em memória, lidas do GitHub ou do cookie de conexão, e as regras ficam no `Domain` (`Models/Copilot/`), puras e testáveis sem rede, salvo indicação.

## ConexaoGitHub

Conta do GitHub ligada ao usuário do SIAT (FR-006 a FR-011, [R2](./research.md#r2-conectar-ao-github-oauth), [R3](./research.md#r3-a-conexão-dentro-do-circuito-blazor)).

| Campo | Tipo | Regra |
|---|---|---|
| `GitHubId` | `long` | Identidade numérica da conta; é ela que liga, não o login |
| `Login` | `string` | Login na hora da conexão; pode ficar velho se a conta for renomeada |

Guardada só no cookie `GitHubConexao`, com a `matricula` de quem conectou e a expiração (30 dias). O middleware só a entrega ao circuito quando a `matricula` do cookie é a do usuário autenticado.

**Estados** (vistos pela página):

```text
Sem conexão ──Conectar + autorizar + membro ativo──▶ Conectada
Sem conexão ──autorização negada/cancelada/falha────▶ Sem conexão (com mensagem)
Sem conexão ──conta fora da organização──────────────▶ Sem conexão (com mensagem, registrada)
Conectada ───Desconectar─────────────────────────────▶ Sem conexão (registrada)
Conectada ───deixou de ser membro (na carga)─────────▶ Sem conexão (desconexão forçada, registrada)
Conectada ───30 dias / outro usuário do SIAT / chave do Data Protection perdida ──▶ Sem conexão
```

## PessoaCopilot

A conta conectada, como membro da organização (FR-013, FR-021, [R5](./research.md#r5-regras-portadas-do-gestao-copilot)).

| Campo | Tipo | Regra |
|---|---|---|
| `GitHubId` | `long` | Da conexão |
| `Login` | `string` | Login atual, do perfil (`GET /user/{id}`); com o perfil indisponível, o da conexão |
| `PerfilDisponivel` | `bool` | Falso quando o perfil não respondeu: `Nome` e `FotoUrl` nulos, e a página avisa "Perfil do GitHub indisponível." (FR-032, [R4](./research.md#r4-autorização-e-isolamento-entre-pessoas)) |
| `Nome` | `string?` | Nome público sem espaços nas pontas; vazio vira `null` e a página mostra o login |
| `FotoUrl` | `string?` | `avatar_url` do perfil; só `https` |
| `TemLicenca` | `bool` | Há licença Copilot com `assignee.id = GitHubId` |
| `Times` | `Result<IReadOnlyList<TimeDaPessoa>, Erro>` | Lista (pode ser vazia) ou erro "Times indisponíveis" |
| `Limite` | `Result<LimiteVigente, Erro>` | Limite ou erro "Limite indisponível" |

Existe quando `TemLicenca` ou há budget individual da pessoa; senão, a leitura devolve `Erros.Copilot.SemLicencaNemLimite`. A leitura do período e a do mês atual só começam depois de a pessoa existir: com qualquer erro da pessoa, devolvem o mesmo erro sem ler relatório (FR-010, FR-021, [R4](./research.md#r4-autorização-e-isolamento-entre-pessoas)).

## TimeDaPessoa

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | `long` | Identidade do time |
| `Nome` | `string` | Exibido como texto; ordem por nome (pt-BR) |
| `Slug` | `string` | Monta o link `https://github.com/orgs/{org}/teams/{slug}` |
| `Herdado` | `bool?` | `true` mostra "via sub-time"; `null` quando a API não informa |

## Budget (interno ao serviço)

Budget de AI Credits lido da organização; só os de `ai_credits` nos escopos abaixo são considerados. Nunca sai do `ConsumoCopilotService`: a página recebe só o `LimiteVigente`.

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | `string` | |
| `Escopo` | `EscopoDeBudget` | `Organizacao` (global), `Padrao` (`multi_user_customer`), `Individual` (`user`) |
| `Valor` | `decimal` | USD, não negativo |
| `Usuario` | `string?` | Login, só no escopo `Individual` (`user` ou `budget_entity_name`) |

## LimiteVigente

Limite que vale hoje para a pessoa (FR-015, esclarecimento de 2026-11-27).

| Tipo | Quando | Página |
|---|---|---|
| `Individual(Valor)` | Uma exceção individual da pessoa | Valor + "Exceção individual" |
| `Padrao(Valor)` | Nenhuma exceção e um budget padrão | Valor + "Padrão da organização" |
| `Ambiguo` | Mais de um budget no nível aplicado | "Múltiplos budgets" |
| `Nenhum` | Nenhuma exceção e nenhum padrão | "Sem limite configurado" |

`LimiteVigente.Calcular(budgets, login)`: individuais da pessoa (login sem diferenciar maiúsculas); sem nenhum, os padrões. O global nunca entra.

## Consumo e ConsumoPorModelo

| Entidade | Campos | Regra |
|---|---|---|
| `Consumo` | `Creditos`, `ValorBruto`, `ValorLiquido` (`decimal`) | Soma dos itens; `+` soma campo a campo |
| `ConsumoPorModelo` | `Modelo` (`string?`), `Consumo` | `null` = "Modelo não informado"; chave = modelo sem espaços nas pontas, em minúsculas |

## RelatorioDeConsumo

Um relatório do GitHub (um dia ou o acumulado de um mês), já normalizado.

| Campo | Tipo | Regra |
|---|---|---|
| `Total` | `Consumo` | |
| `Modelos` | `IReadOnlyList<ConsumoPorModelo>` | Ordem: créditos decrescentes, depois nome (pt-BR) |
| `LidoEm` | `DateTimeOffset` | Horário da leitura (base do cache) |

`RelatorioDeConsumo.De(itens)` recusa unidade diferente de `credits`/`ai-credits` ([R5](./research.md#r5-regras-portadas-do-gestao-copilot)).

## Periodo

Intervalo consultado (FR-023 a FR-025, [R6](./research.md#r6-período)).

| Campo | Tipo | Regra |
|---|---|---|
| `De` | `DateOnly` | Inclusiva, UTC |
| `Ate` | `DateOnly` | Inclusiva, UTC |

- `Periodo.Padrao(hoje)`: dia 1 do mês de `hoje` até `hoje`.
- `Periodo.Validar(de, ate, hoje)`: `Result<Periodo, Erro>`; recusa formato inválido, `De > Ate`, `Ate > hoje`, `De` antes do dia 1 do mês 23 meses antes de `hoje`, ou mais de 92 dias (`Erros.Copilot.PeriodoInvalido`).
- `Dias`: lista das datas de `De` a `Ate`.
- `IncluiMesAtual(hoje)`: `Ate ≥ dia 1 do mês de hoje`.
- `Periodo.Atalhos(hoje)`: Hoje, Ontem, Últimos 7 dias, Últimos 30 dias, Mês atual, Mês anterior.
- `EhRecente(data, hoje)`: `data ≥ hoje − 2 dias` (os três últimos dias, incluindo hoje); define o cache do relatório ([R8](./research.md#r8-cache-em-memória-e-atualizar-agora)).

## ConsumoDoPeriodo

| Campo | Tipo | Regra |
|---|---|---|
| `Periodo` | `Periodo` | |
| `Total` | `Consumo` | Soma dos dias; só existe se todos os dias foram lidos |
| `Dias` | `IReadOnlyList<(DateOnly Data, RelatorioDeConsumo Relatorio)>` | Um por dia, em ordem |
| `Modelos` | `IReadOnlyList<ConsumoPorModelo>` | Mescla dos dias pela chave do modelo, mesma ordem |
| `ConsultadoEm` | `DateTimeOffset` | Horário mostrado na página |

## UsoDoLimite

Uso do limite no mês atual (FR-016).

| Campo | Tipo | Regra |
|---|---|---|
| `Faixa` | `FaixaDeUso` | `Normal` (< 80%), `Atencao` (≥ 80%), `Atingido` (≥ 100%), `LimiteZero`, `Indisponivel` |
| `Razao` | `decimal?` | Consumo bruto do mês ÷ limite; `null` em `LimiteZero` e `Indisponivel` |
| `Restante` | `decimal?` | Limite − consumo bruto do mês (pode ser negativo) |
| `Mes` | `string` | `yyyy-MM` do acumulado |

`UsoDoLimite.Calcular(consumoBrutoDoMes, limite)`: `Indisponivel` se faltar um dos dois ou o limite for `Ambiguo`/`Nenhum`; `LimiteZero` se o valor for 0.

## SerieDeModelos (App, view model)

Linhas da evolução por modelo (FR-017): os cinco modelos de maior total no período, na métrica escolhida (`ValorBruto` ou `Creditos`), e "Outros" com a soma do resto; cada linha tem um valor por dia. Fica em `App/Web/ViewModels/Copilot`, porque só existe para a tela, e é testada lá.

## View models da página (App)

| VM | Conteúdo |
|---|---|
| `PessoaCopilotVM` | `PessoaCopilot` pronto para exibir + organização |
| `ConsumoDoPeriodoVM` | Totais, dias, modelos com participação no valor bruto, `ConsultadoEm` |
| `UsoDoLimiteVM` | `UsoDoLimite` + rótulos de faixa e nota |

Nenhum deles leva budgets da organização, o global, a capacidade nem dados de outra conta (FR-015).
