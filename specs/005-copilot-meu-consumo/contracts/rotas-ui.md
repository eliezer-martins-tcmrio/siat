# Contrato: rotas e tela do Copilot

## Rotas (relativas ao `<base href>`)

| Rota | Tipo | Exige | Faz |
|---|---|---|---|
| `copilot/meu-consumo` | Página Blazor | `CopilotProprio.CanSelect` | Sem conexão: explicação e **Conectar ao GitHub**. Com conexão: navega para `copilot/meu-consumo/{login}`, mantendo `de` e `ate` |
| `copilot/meu-consumo/{login}` | Página Blazor (a mesma) | `CopilotProprio.CanSelect` | Página do próprio consumo; `{login}` diferente do conectado (sem diferenciar maiúsculas) navega para o próprio, mantendo `de` e `ate`; sem conexão, mostra a explicação |
| `copilot/github/conectar?retorno={caminho}` | Endpoint HTTP | `CopilotProprio.CanSelect` | Desafio do esquema `GitHubOAuth`; `retorno` só local, senão `copilot/meu-consumo` |
| `copilot/github/retorno` | Callback do OAuth | tratado pelo manipulador | Valida e cria a ligação ([conexao-github.md](./conexao-github.md)); volta a `retorno` ou a `copilot/meu-consumo?conexao={motivo}` |
| `copilot/github/desconectar?motivo={motivo}&retorno={caminho}` | Endpoint HTTP | usuário autenticado | Apaga o cookie `GitHubConexao`, registra e volta a `copilot/meu-consumo?conexao=nao-membro-desconectada` (motivo `nao-membro`), a `retorno` (local) ou a `copilot/meu-consumo?conexao=desconectada` |

Constantes em `Navegacao`: `RotaMeuConsumo = "copilot/meu-consumo"`, `RotaConectarGitHub`, `RotaDesconectarGitHub`; montagem por `Navegacao.MeuConsumo(login?, de?, ate?)`. Os endpoints do GitHub continuam em `copilot/github/*` (o retorno está cadastrado no OAuth App).

## Parâmetros da página

| Parâmetro | Valores | Ausente |
|---|---|---|
| `de` | `yyyy-MM-dd` | Dia 1 do mês atual (UTC) |
| `ate` | `yyyy-MM-dd` | Hoje (UTC) |
| `conexao` | `negada`, `falha`, `nao-membro`, `outro-usuario`, `desconectada`, `nao-membro-desconectada` | Sem mensagem |

O período padrão não vai para o endereço. Valores inválidos de `de`/`ate` mostram a mensagem de período inválido e nada é consultado. `conexao` é lido uma vez para a mensagem e retirado do endereço (`replace`).

## Menu e Início

`Ferramentas.Grupos` ganha o grupo **Copilot**, depois de **Consultas**: `Ferramenta(Modulo.CopilotProprio, "Meu consumo", "Seu consumo de AI Credits do Copilot: período, modelos e limite.", Navegacao.RotaMeuConsumo, "bot")`. O item fica marcado como atual em `copilot` e em `copilot/pessoas/*`.

## Estrutura da página

1. `CabecalhoPagina` com trilha **Copilot › Meu consumo**; título = nome (ou login); foto; ações **Atualizar agora** e **Desconectar do GitHub**; etiquetas de licença e times.
2. `FiltroDePeriodo` (De, Até, Aplicar, atalhos, Limpar período).
3. Avisos: mensagem de conexão, erros de cada parte, período inválido.
4. `ResumoNumerico`: Consumo bruto (créditos e período), Valor líquido ("Não inclui o preço das licenças"), Limite vigente (origem), Uso do limite (`BarraDeUso` ou "—").
5. `EvolucaoDiaria` (métrica; por modelo).
6. `TabelaDeModelos` (Modelo, Créditos consumidos, Valor bruto, Valor líquido, Participação) ou "Sem consumo reportado neste período".
7. Nota: licença, times e limite refletem a situação atual; relatórios podem ter atraso; horário da consulta.

## Explicação de conexão (sem conta conectada)

Título "Conecte sua conta do GitHub"; texto: o SIAT precisa saber qual é a sua conta no GitHub para mostrar o seu consumo; a conexão pede só a identificação da conta e fica neste navegador por 30 dias. Botão **Conectar ao GitHub** (link para `copilot/github/conectar?retorno=...`, com navegação completa).

## Acessibilidade

- Pessoa e explicação navegáveis só por teclado, foco visível, ordem do documento igual à visual.
- Cada dia da evolução é um `button` com `aria-label` com data, métrica, valor e "Filtrar este dia".
- Links de times: "{time} (abre em nova aba)".
- Faixas de uso, licença e indisponível com texto, não só cor.
- Andamento da leitura em região `aria-live="polite"`.
