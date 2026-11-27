# Sistema de design do SIAT

Referência visual: [design/index.html](design/index.html) (tokens e componentes) e as telas de exemplo em `design/`. Os tokens estão em [design/siat.css](design/siat.css). As telas são HTML estático com dados fictícios e consultas simuladas; servem de contrato visual para a implementação em Blazor.

## Direção

Identidade do TCMRio em versão contida, mais o ambiente em primeiro plano. Tokens com os mesmos nomes do `gestao-copilot`.

- Painel de trabalho sóbrio, usado em sequência durante um atendimento, em monitor de escritório. Tema claro, escuro e do sistema, aplicado antes da primeira pintura.
- Cores da marca do TCMRio: marinho `#083050` e celeste `#3499b8`. O marinho fica só no que é clicável (ação primária, links) e no item ativo do menu; o celeste, na barra do item ativo e em decoração.
- Texto em grafite neutro, quase sem matiz, inclusive nos títulos: `ink` para corpo e títulos, `muted` para o auxiliar. Nada de texto azulado.
- Menu lateral claro, separado do conteúdo por uma borda fina, sem bloco de cor. Conteúdo em fundo branco, painéis de borda fina. Sem degradês.
- Cor forte só para três coisas: ambiente do servidor (DSV, HML, PRD, Contingência), estado (ok, aviso, erro) e a fita da publicação do próprio SIAT fora de produção (ver Publicação).
- Densidade maior apenas nas listas de resultado (13px); o resto segue 14px.

## Navegação

Menu lateral de 208px, fixo, em grupos: **Sistemas** (Catálogo, Versões), **Consultas** (MailQueue, AuditLog), **Conhecimento** (Documentação), **Copilot** (Meu consumo), **Gestão** (Builds). Item ativo com fundo `sidebar-active` (celeste bem claro), texto em marinho e barra de acento à esquerda. Cada item só aparece para quem tem o recurso do UserManager. Rodapé: Tema, conta e Sair. Abaixo de 768px o menu vira barra superior com rolagem horizontal.

## Marca

Logo simplificado do TCMRio (sigla) no topo do menu, com 20px de altura, seguido de "SIAT" após um divisor fino; embaixo, "Sistema Integrado de Apoio Técnico" numa linha só (11px). Fora de produção, a fita da publicação fica acima do logo (ver Publicação). Versão positiva no tema claro (`design/img/tcmrio-sigla.png`) e negativa no escuro (`design/img/tcmrio-sigla-negativo.png`), conforme o manual de marca. Não alterar cores nem proporções do logo.

Do restante da identidade, o SIAT usa só o marinho e o celeste. Areia, coral, gradientes, ondas e Lato/Merriweather ficam de fora: são para peças de comunicação, e coral competiria com o vermelho de DSV e de erro.

## Ambientes

| Ambiente | Token | Cor |
|---|---|---|
| DSV | `--color-env-dsv` | vermelho |
| HML | `--color-env-hml` | âmbar |
| PRD | `--color-env-prd` | azul-violeta |
| Contingência | `--color-env-cont` | cinza |

Cada um tem `-soft` (fundo) e `-line` (borda); PRD tem também `-solid`. Os componentes leem `--amb`, `--amb-soft` e `--amb-line`, definidos por `data-amb="dsv|hml|prd|cont"`.

- **Etiqueta de ambiente** (`.amb`): sigla em caixa reta com borda. **Pílula de estado** (`.pilula`): arredondada, preenchida, com ponto. A forma diferente impede que PRD seja lido como erro.
- **Seletor de servidor** (`.seletor-servidor`): lista fechada agrupada por ambiente, com a etiqueta do ambiente dentro do controle. Não existe campo de servidor livre.
- **Faixa de contexto** (`.contexto`): primeira coisa da página em toda tela que consulta um servidor, fixa ao rolar. DSV e HML em fundo suave; PRD em fundo sólido, com o aviso de que a consulta fica registrada em log.

## Publicação

O ambiente do **servidor consultado** (acima) e a **publicação do próprio SIAT** são informações diferentes: uma cópia de desenvolvimento do SIAT pode consultar um servidor de produção. Por isso a publicação tem cor, forma e lugar próprios.

| Publicação | Token | Cor |
|---|---|---|
| Desenvolvimento (`Development`) | `--color-publicacao-dsv` | vermelho |
| Homologação (`Staging`) | `--color-publicacao-hml` | verde |
| Produção | — | sem fita |

- **Fita da publicação** (`.fita-publicacao`): faixa sólida de borda a borda no topo do menu, com a palavra inteira em caixa alta e texto branco, na convenção dos outros sistemas do Tribunal (como o SiCATI). Só aparece fora de produção.
- Forma e lugar exclusivos: a fita nunca aparece no conteúdo e nunca usa sigla; o ambiente do servidor nunca aparece no menu como fita. Assim o vermelho da fita não é lido como DSV nem como erro, e o verde não é lido como ok.
- Os tokens `--color-publicacao-*` só são usados pela fita.

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
- **Filtro segmentado** (`.segmentado`): filtros exclusivos num grupo (`role="group"` com nome), cada botão com `aria-pressed` e a quantidade; o escolhido vai para a URL.
- **Tabela** (`.tabela`): cabeçalho fixo, rolagem horizontal própria. Só a exceção ganha cor; o valor normal é texto simples.
- **Lista expansível** (`.lista`): linha em grade que abre o detalhe no lugar; a linha aberta vai para a URL (`sel`).
- **Linha expansível de tabela** (`.linha-expansivel` + `.linha-detalhe`): botão expansor com `aria-expanded` e nome acessível na primeira célula; o detalhe ocupa a linha de baixo inteira (Versões: caminhos consultados).
- **Trilha de navegação** (`.trilha`): acima do título das páginas de detalhe (ficha do sistema), `nav` com nome "Trilha de navegação" e lista ordenada; o item atual com `aria-current="page"`. O menu continua marcando a tela de origem (Catálogo).
- **Seções por ambiente** (`.ficha-ambiente`): um painel por ambiente na ordem DSV, HML, PRD e Contingência, com a sigla na cor do ambiente e o `h2` como nome da seção; dentro, blocos com título pequeno ("No catálogo", "Versões em DSV"). Os caminhos ficam sempre visíveis embaixo de cada linha, sem expansor.
- **Filtro de período** (`FiltroDePeriodo`, `.filtro-periodo`): campos De e Até com `min`/`max` da janela, atalhos com `aria-pressed`, **Aplicar** e **Limpar período**; período inválido mostra a mensagem ligada aos campos por `aria-describedby` e não aplica. O período vai para a URL (`de`, `ate`); o padrão fica fora dela (Copilot).
- **Resumo numérico** (`ResumoNumerico`): a faixa de resumo como `dl` com nome; cada indicador com rótulo, valor e detalhe. Parte que falhou mostra "Indisponível", nunca zero.
- **Barra de uso** (`BarraDeUso`, `.barra-uso`): percentual, faixa em texto ("Atenção", "Limite atingido no relatório", "Limite zero", "Indisponível") e barra; a cor nunca é a única indicação.
- **Evolução diária** (`EvolucaoDiaria`, `.evolucao`): SVG próprio, uma coluna por dia; cada dia é um `button` cujo `aria-label` traz a data e o valor (ou as séries, na visão por modelo) e que filtra o período para aquele dia. Seletor de métrica em `.segmentado` e visão por modelo com cinco séries mais "Outros" (classes `.serie-1` a `.serie-5` e `.serie-outros`) e legenda.
- **Tabela de modelos** (`TabelaDeModelos`, `.tabela-modelos`): modelo, créditos, valor bruto, valor líquido e participação com barra; "Modelo não informado" em texto diferenciado (`.sem-modelo`). Valores em USD como "US$ 1,234.56" e créditos como "1.235".
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
