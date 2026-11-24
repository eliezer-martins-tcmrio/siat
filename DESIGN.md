# Sistema de design do SIAT

Referência visual: [design/index.html](design/index.html) (tokens e componentes) e as telas de exemplo em `design/`. Os tokens estão em [design/siat.css](design/siat.css). As telas são HTML estático com dados fictícios e consultas simuladas; servem de contrato visual para a implementação em Blazor.

## Direção

Identidade do TCMRio em versão contida, mais o ambiente em primeiro plano. Tokens com os mesmos nomes do `gestao-copilot`.

- Painel de trabalho sóbrio, usado em sequência durante um atendimento, em monitor de escritório. Tema claro, escuro e do sistema, aplicado antes da primeira pintura.
- Cores da marca do TCMRio: marinho `#083050` e celeste `#3499b8`. O marinho fica só no que é clicável (ação primária, links) e no item ativo do menu; o celeste, na barra do item ativo e em decoração.
- Texto em grafite neutro, quase sem matiz, inclusive nos títulos: `ink` para corpo e títulos, `muted` para o auxiliar. Nada de texto azulado.
- Menu lateral claro, separado do conteúdo por uma borda fina, sem bloco de cor. Conteúdo em fundo branco, painéis de borda fina. Sem degradês.
- Cor forte só para duas coisas: ambiente (DSV, HML, PRD, Contingência) e estado (ok, aviso, erro).
- Densidade maior apenas nas listas de resultado (13px); o resto segue 14px.

## Navegação

Menu lateral de 208px, fixo, em grupos: **Sistemas** (Catálogo, Versões), **Consultas** (MailQueue, AuditLog), **Conhecimento** (Documentação), **Gestão** (Copilot, Builds). Item ativo com fundo `sidebar-active` (celeste bem claro), texto em marinho e barra de acento à esquerda. Cada item só aparece para quem tem o recurso do UserManager. Rodapé: Tema, conta e Sair. Abaixo de 768px o menu vira barra superior com rolagem horizontal.

## Marca

Logo simplificado do TCMRio (sigla) no topo do menu, com 20px de altura, seguido de "SIAT" após um divisor fino. Versão positiva no tema claro (`design/img/tcmrio-sigla.png`) e negativa no escuro (`design/img/tcmrio-sigla-negativo.png`), conforme o manual de marca. Não alterar cores nem proporções do logo.

Do restante da identidade, o SIAT usa só o marinho e o celeste. Areia, coral, gradientes, ondas e Lato/Merriweather ficam de fora: são para peças de comunicação, e coral competiria com o vermelho de PRD e de erro.

## Ambientes

| Ambiente | Token | Cor |
|---|---|---|
| DSV | `--color-env-dsv` | azul-violeta |
| HML | `--color-env-hml` | âmbar |
| PRD | `--color-env-prd` | vermelho |
| Contingência | `--color-env-cont` | cinza |

Cada um tem `-soft` (fundo) e `-line` (borda); PRD tem também `-solid`. Os componentes leem `--amb`, `--amb-soft` e `--amb-line`, definidos por `data-amb="dsv|hml|prd|cont"`.

- **Etiqueta de ambiente** (`.amb`): sigla em caixa reta com borda. **Pílula de estado** (`.pilula`): arredondada, preenchida, com ponto. A forma diferente impede que PRD seja lido como erro.
- **Seletor de servidor** (`.seletor-servidor`): lista fechada agrupada por ambiente, com a etiqueta do ambiente dentro do controle. Não existe campo de servidor livre.
- **Faixa de contexto** (`.contexto`): primeira coisa da página em toda tela que consulta um servidor, fixa ao rolar. DSV e HML em fundo suave; PRD em fundo sólido, com o aviso de que a consulta fica registrada em log.

## Tokens

Mesmos nomes do `gestao-copilot`, em OKLCH, redefinidos em `:root[data-theme="dark"]`: `brand`, `action`/`on-action`, `accent` (decoração, nunca sob texto), `accent-soft`/`accent-ink`, `ink`, `muted`, `line`, `field` (borda de campos, 3:1), `soft`, `canvas`, `surface`, `sidebar`/`sidebar-ink`/`sidebar-strong`, `track`, `warn`, `bad`, `ok` (cada um com `-soft`), `neutral-soft`. Novos no SIAT: `sidebar-line` (borda do menu), `sidebar-active` (fundo do item ativo), `env-*`, `warn-line`, `bad-line`, `code-*`. No tema claro, `canvas` e `surface` são brancos e os painéis se distinguem pela borda; no escuro, o menu fica em azul-noite próximo do fundo. Raios: `--radius-card` 10px, `--radius-control` 7px.

## Tipografia

Segoe UI e fontes de sistema. Cascadia Mono para o que se copia: caminhos, versões, ids, nomes de servidor. Escala: 22 (título da página), 16, 14 (padrão), 13 (tabelas e listas), 12 (rótulos; mínimo). Algarismos tabulares em colunas numéricas e horários.

## Componentes

Os recorrentes viram componentes próprios do SIAT por cima do Blazor Blueprint (RNF-06):

- **Cabeçalho de página** (`.cabecalho`): título, uma linha de descrição, ações e status da leitura à direita.
- **Seletor de servidor** e **faixa de contexto**: ver Ambientes.
- **Painel** (`.painel`): superfície com borda fina; seções internas separadas por linha. Não aninhar painéis.
- **Faixa de resumo** (`.resumo`): uma superfície dividida em células, nunca cartões soltos.
- **Tabela** (`.tabela`): cabeçalho fixo, rolagem horizontal própria. Só a exceção ganha cor; o valor normal é texto simples.
- **Lista expansível** (`.lista`): linha em grade que abre o detalhe no lugar; a linha aberta vai para a URL (`sel`).
- **Botões**: uma ação primária por tela; 40px de altura mínima (32px no pequeno); foco de 3px.
- **Copiar**: botão sempre visível ao lado do valor, com confirmação por 1,5 s.
- **Busca**: atalho `/` foca, `Esc` limpa; vários termos, sem diferenciar acentos e maiúsculas.
- **Estados**: carregando (esqueleto estático, tempo decorrido e Cancelar), vazio (diz o que foi procurado e oferece a saída), erro (mensagem da origem e o que conferir), aviso (a página continua utilizável).
- **Código**: stack trace em bloco escuro nos dois temas; JSON realçado no tema da página; ambos com Copiar.

## Regras

- Filtros e seleção sempre na URL.
- Conteúdo externo (corpo de e-mail) em `iframe` com `sandbox` vazio.
- Estado nunca é só cor: sempre há texto ou rótulo para leitor de tela.
- Movimento: apenas transições de cor e rotação de 150ms; respeitar `prefers-reduced-motion`.
- Texto em português do Brasil, sem travessões.
