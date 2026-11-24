# Especificação: Catálogo de Sistemas

**Branch da feature**: `features/<tarefa>_catalogo_sistemas` (ou `features/catalogo_sistemas` sem número de tarefa); a pasta da spec é `specs/002-catalogo-sistemas`

**Criada em**: 2026-11-24

**Status**: Rascunho

**Entrada**: Descrição do usuário: "implementar o D:/Projetos/TCMRJDoc/sistemas.html"

## Contexto

O `TCMRJDoc/sistemas.html` é uma página estática, aberta a partir do repositório TCMRJDoc, que lista onde cada sistema do Tribunal roda: para cada ambiente (Desenvolvimento, Homologação, Produção e Contingência), uma ou mais instâncias com URL, máquina e caminho, além de uma observação. Os dados ficam no `sistemasDb.js`, mantido por commits no repositório TCMRJDoc do GitLab (hoje 63 sistemas).

Esta feature traz essa consulta para o SIAT como o módulo **Sistemas**, no grupo **Catálogo** do menu lateral (fase 1 do roteiro do PRD; a base da aplicação já foi entregue na feature 001). Requisitos do PRD cobertos: SI-01 a SI-04, RG-02, RG-03, RG-06, RG-08, RG-11 a RG-13. A tela de exemplo `design/sistemas.html` é o contrato visual.

## Esclarecimentos

### Sessão 2026-11-24

- P: De onde o SIAT lê o catálogo? → R: Do próprio `sistemasDb.js` do TCMRJDoc no GitLab, fonte única com o `sistemas.html` durante a transição; a migração para `sistemas.json` fica para a aposentadoria do `sistemas.html`.
- P: Produção precisa de recurso próprio no catálogo? → R: Não. A regra que exigia recurso separado para dados de produção foi retirada da constituição (v3.0.0); `Sistemas.CanSelect` libera todos os ambientes.
- P: O catálogo novo aparece sozinho numa tela já aberta? → R: Não. Depois de no máximo 10 minutos, aparece ao abrir ou recarregar a tela; na hora, com "Atualizar agora".
- P: A leitura do catálogo pode ser cancelada? → R: Sim. Enquanto o catálogo é lido, a tela mostra o tempo decorrido e "Cancelar"; a leitura no GitLab é interrompida quando ninguém mais espera por ela.

## Cenários de usuário e testes *(obrigatório)*

### História 1 — Localizar onde um sistema roda (Prioridade: P1)

Uma pessoa do suporte recebe um chamado sobre o SCP-Multas em homologação. Ela abre **Catálogo › Sistemas**, digita "multas hml" na busca e vê só os sistemas que casam com os dois termos, com a URL, a máquina e o caminho de cada ambiente lado a lado. Clica na URL de HML para abrir o sistema em outra aba e copia o caminho com um clique para colar no Explorer.

**Por que esta prioridade**: é o uso diário do `sistemas.html`; sozinha, já permite aposentá-lo.

**Teste independente**: com um usuário que tem só `Sistemas.CanSelect`, abrir a tela, buscar sistemas conhecidos e conferir os dados com o arquivo do catálogo.

**Cenários de aceitação**:

1. **Dado** um usuário com `Sistemas.CanSelect`, **quando** abre o SIAT, **então** vê no menu o grupo **Catálogo** com o item **Sistemas**.
2. **Dado** a tela aberta, **quando** nada foi buscado, **então** vê todos os sistemas do catálogo em ordem alfabética, com uma coluna por ambiente (DSV, HML, PRD, Contingência, cada um identificado pela sua cor e pela sigla) e a observação, e um contador com o total de sistemas.
3. **Dado** um sistema com mais de uma instância num ambiente, **quando** é exibido, **então** cada instância aparece separada dentro da célula daquele ambiente, com seus próprios URL, máquina e caminho.
4. **Dado** um ambiente sem nenhuma instância preenchida, **quando** é exibido, **então** a célula mostra um marcador de "não configurado", distinguível por leitor de tela.
5. **Dado** a busca "multas hml", **quando** o usuário digita, **então** a lista mostra apenas os sistemas que contêm os dois termos em qualquer campo (nome, URL, máquina, caminho ou observação, de qualquer ambiente) e o contador passa a "N de 63 sistemas".
6. **Dado** a busca "sessoes", **quando** existe o sistema "SCP-Sessões", **então** ele aparece: a busca não diferencia acentos nem maiúsculas.
7. **Dado** uma URL exibida, **quando** o usuário a aciona, **então** o endereço abre em nova aba, e o leitor de tela anuncia que abre em nova aba.
8. **Dado** uma máquina ou um caminho exibido, **quando** o usuário aciona o botão Copiar ao lado, **então** o valor exato vai para a área de transferência e o botão confirma a cópia por 1,5 s.

---

### História 2 — Compartilhar e navegar pelo teclado (Prioridade: P2)

Um desenvolvedor quer mandar a um colega os dados do eTCM. Ele busca "etcm", copia o endereço da página e envia; o colega abre o link e vê a mesma busca já aplicada. Durante o uso, ele não tira as mãos do teclado: `/` leva à busca, `Esc` a limpa e `Tab` percorre links e botões de cópia.

**Por que esta prioridade**: segue as regras de experiência de todo o SIAT e melhora a História 1, que já entrega valor sem ela.

**Teste independente**: buscar, copiar a URL, abri-la em outra janela com outro usuário autorizado e conferir o mesmo resultado; repetir o fluxo só com teclado.

**Cenários de aceitação**:

1. **Dado** uma busca digitada, **quando** o usuário copia a URL e outra pessoa autorizada a abre, **então** a página abre com a mesma busca e o mesmo resultado.
2. **Dado** o foco fora da busca, **quando** o usuário tecla `/`, **então** o foco vai para a busca; **quando** tecla `Esc` na busca, **então** ela é limpa e a lista volta completa.
3. **Dado** uma busca sem resultado, **quando** a lista fica vazia, **então** a tela diz que nenhum sistema foi encontrado, lembra onde a busca procura e oferece "Limpar busca".
4. **Dado** a tela, **quando** navegada só por teclado e leitor de tela, **então** todos os links e botões de cópia são alcançáveis, com foco visível, e cada botão de cópia diz o que copia (por exemplo, "Copiar caminho de PRD do SGQ").

---

### História 3 — Ver o catálogo sempre atual e saber de onde ele vem (Prioridade: P2)

Quem corrige um dado do catálogo (por exemplo, a troca de máquina de um site) faz a correção no arquivo de origem. Quem consulta o SIAT vê quando o catálogo foi lido e pode pedir "Atualizar agora" para ver a correção sem esperar. Se a origem estiver indisponível, a tela explica o problema sem derrubar o resto do SIAT.

**Por que esta prioridade**: garante que o SIAT não diverge da fonte de verdade, condição para aposentar o `sistemas.html`.

**Teste independente**: alterar um dado na origem, acionar "Atualizar agora" e ver a alteração; tornar a origem indisponível e conferir a mensagem e o funcionamento dos outros módulos.

**Cenários de aceitação**:

1. **Dado** a tela aberta, **quando** o catálogo é exibido, **então** o cabeçalho mostra o horário da leitura e o botão "Atualizar agora", e o rodapé indica a origem dos dados e como corrigi-los, com link para o arquivo de origem.
2. **Dado** um dado alterado na origem, **quando** o usuário aciona "Atualizar agora", **então** a lista passa a mostrar o dado novo e o horário da leitura muda.
3. **Dado** um dado alterado na origem, **quando** ninguém aciona "Atualizar agora", **então** quem abre ou recarrega a tela depois de no máximo 10 minutos vê o dado novo (a tela já aberta não muda sozinha).
6. **Dado** o catálogo sendo lido, **quando** a leitura demora, **então** a tela mostra o tempo decorrido e "Cancelar"; ao cancelar, a tela volta ao catálogo anterior, se houver, ou ao estado de erro com "Tentar de novo", e a leitura no GitLab é interrompida quando ninguém mais espera por ela.
4. **Dado** a origem indisponível ou ilegível e nenhuma leitura anterior válida, **quando** o usuário abre a tela, **então** vê uma mensagem de erro com o motivo e o que conferir, e os demais módulos do SIAT continuam funcionando.
5. **Dado** a origem indisponível ou ilegível e uma leitura anterior válida, **quando** a releitura falha, **então** a tela continua mostrando a leitura anterior, com um aviso de que a atualização falhou e do horário dos dados exibidos.

---

### História 4 — Acesso controlado ao catálogo (Prioridade: P3)

Só quem tem o recurso do catálogo vê o item no menu e os dados; quem não tem e abre um link compartilhado recebe a orientação de a quem pedir acesso.

**Por que esta prioridade**: a regra de acesso é a mesma da base do SIAT e é verificada uma vez; não muda o uso das demais histórias.

**Teste independente**: com um usuário sem `Sistemas.CanSelect`, conferir a ausência do item no menu e a negação ao abrir a URL direta da tela.

**Cenários de aceitação**:

1. **Dado** um usuário sem `Sistemas.CanSelect`, **quando** abre o SIAT, **então** não vê o item **Sistemas**, e o grupo **Catálogo** some quando não houver nenhum item dele visível.
2. **Dado** um usuário sem `Sistemas.CanSelect`, **quando** abre a URL da tela de Sistemas (com ou sem busca), **então** não vê nenhum dado do catálogo e vê a mensagem de acesso negado indicando o recurso `Sistemas.CanSelect`.
3. **Dado** um usuário com `Sistemas.CanSelect`, **quando** entra no SIAT pela página inicial, **então** é levado à tela de Sistemas, primeira ferramenta do menu.

---

### Casos extremos

- Instância com URL, máquina e caminho todos vazios (como o HML do WebNews): é tratada como inexistente; se o ambiente não tiver outra, mostra "não configurado".
- Campo URL com vários endereços separados por `|` (como o RabbitMQ em PRD): cada endereço vira um link separado.
- Campo máquina com várias máquinas separadas por vírgula ("TCM2016WEB1, TCM2016WEB2"): cada máquina aparece e é copiada sozinha.
- URL que não começa por `http://` ou `https://` (como "N/A" e "AgenteSIGA.EXE (diversos agendamentos)" no catálogo atual, ou um `javascript:`): é exibida como texto, sem link.
- Sistema sem nenhum ambiente (como "Contratos"): aparece na lista com todos os ambientes como "não configurado".
- Valor com barra invertida antes de letra comum (como `G:\DESENVOLVIMENTO` no SCP): aparece como o `sistemas.html` mostra, mesmo quando isso perde a barra; corrigir é editar a origem.
- Observação com marcação HTML (como `<br />` no SCP Visualização): a marcação nunca é interpretada; quebras de linha da origem são mostradas como quebra de linha, e o resto aparece como texto.
- Nome de sistema com espaços sobrando ("SCP Api "): os espaços nas pontas são ignorados na exibição e na ordenação.
- Sistema sem nome ou com estrutura inválida no arquivo: é deixado de fora, e a tela avisa quantos itens foram ignorados, sem impedir a exibição dos demais.
- Valores longos (caminhos e URLs extensos): quebram após separadores (`/`, `\`, `_`, `.`, `-`), não no meio de palavras; a tabela rola na horizontal em telas estreitas, com o cabeçalho de colunas fixo ao rolar.
- Busca com caracteres especiais ou só espaços: espaços em excesso são ignorados; os demais caracteres são procurados literalmente.
- URL compartilhada com busca muito longa ou malformada: a tela abre com o texto recebido na busca, sem erro.
- O usuário perde o recurso durante a sessão: a próxima leitura ou atualização é negada.
- Duas pessoas acionam "Atualizar agora" ao mesmo tempo: a origem é lida uma vez, e as duas veem o mesmo resultado.

## Requisitos *(obrigatório)*

### Requisitos funcionais

**Acesso e navegação**

- **FR-001**: O SIAT DEVE ter o recurso `Sistemas.CanSelect` na sua aplicação no UserManager; ele libera o catálogo inteiro, de todos os ambientes.
- **FR-002**: O menu lateral DEVE ter o grupo **Catálogo**, antes do grupo **Consultas**, com o item **Sistemas**, visível só para quem tem `Sistemas.CanSelect`; o grupo some quando não houver item visível.
- **FR-003**: A tela e toda leitura do catálogo DEVEM ser autorizadas no servidor por `Sistemas.CanSelect`, independentemente do que a interface mostra; sem o recurso, nenhum dado do catálogo é enviado ao navegador.
- **FR-004**: A página inicial DEVE levar à tela de Sistemas quem tem `Sistemas.CanSelect`; sem ele, segue para a primeira ferramenta de Consultas permitida, como hoje; e a mensagem de "sem acesso" DEVE citar também o catálogo.

**Exibição**

- **FR-005**: A tela DEVE listar todos os sistemas do catálogo em ordem alfabética do nome (regras do português), um por linha, com as colunas Sistema, Desenvolvimento, Homologação, Produção, Contingência e Observação.
- **FR-006**: Cada coluna de ambiente DEVE ser identificada pela sigla e pela cor do ambiente, nunca só pela cor.
- **FR-007**: Cada instância DEVE mostrar, quando preenchidos e nesta ordem, URL, máquina e caminho, cada um com seu rótulo; instâncias do mesmo ambiente DEVEM aparecer separadas dentro da célula.
- **FR-008**: Ambiente sem instância preenchida e observação vazia DEVEM mostrar um marcador de "não configurado", com texto para leitor de tela.
- **FR-009**: Cada endereço `http://` ou `https://` DEVE ser um link que abre em nova aba, sem dar à página aberta acesso à página do SIAT; vários endereços separados por `|` DEVEM virar links separados; endereços de outros esquemas DEVEM aparecer como texto.
- **FR-010**: Cada máquina e cada caminho DEVEM ter um botão Copiar, sempre visível, que copia o valor exato e confirma a cópia por 1,5 s; várias máquinas separadas por vírgula DEVEM ser copiáveis uma a uma.
- **FR-011**: Caminhos e nomes de máquina DEVEM aparecer em fonte monoespaçada; valores longos DEVEM quebrar após separadores.
- **FR-012**: Todo texto vindo do catálogo DEVE ser exibido como texto, sem interpretar marcação.

**Busca**

- **FR-013**: A busca DEVE procurar cada termo digitado (separados por espaço) em todos os campos do sistema: nome, URL, máquina e caminho de qualquer instância de qualquer ambiente, e observação; um sistema aparece só se contiver todos os termos.
- **FR-014**: A busca NÃO DEVE diferenciar acentos nem maiúsculas e DEVE filtrar enquanto o usuário digita.
- **FR-015**: O contador DEVE mostrar o total de sistemas sem busca e "N de T sistemas" com busca, anunciado a leitores de tela.
- **FR-016**: Busca sem resultado DEVE mostrar o estado vazio, com o que é procurado e a ação "Limpar busca".
- **FR-017**: O texto da busca DEVE ficar na URL, e abrir a URL DEVE restaurar a busca.
- **FR-018**: O atalho `/` DEVE levar o foco à busca, e `Esc` na busca DEVE limpá-la.

**Origem dos dados**

- **FR-019**: O catálogo DEVE ser lido do próprio `sistemasDb.js` do repositório TCMRJDoc no GitLab, no formato em que está hoje, para que o `sistemas.html` antigo e o SIAT tenham uma única fonte durante a transição; o SIAT NÃO DEVE guardar cópia própria do catálogo além do cache em memória, nem exigir mudança no arquivo.
- **FR-020**: O conteúdo atual do `sistemasDb.js` (63 sistemas) DEVE aparecer no SIAT sem perda de sistema, instância ou campo, aceitando as particularidades do arquivo atual (declaração de variável antes da lista, vírgulas sobrando no fim de listas e objetos, marca de ordem de bytes no início, nome de campo sem aspas e barra invertida antes de letra comum) e interpretando cada valor como o `sistemas.html` o interpreta, para que as duas páginas mostrem exatamente o mesmo texto.
- **FR-021**: O catálogo lido DEVE ficar em cache em memória por no máximo 10 minutos; "Atualizar agora" DEVE reler a origem na hora. A tela já aberta não precisa se atualizar sozinha.
- **FR-022**: A tela DEVE mostrar o horário da leitura exibida e, no rodapé, a origem dos dados e como corrigi-los, com link para o arquivo de origem.
- **FR-023**: A leitura da origem DEVE ter tempo limite; falha ou conteúdo ilegível sem leitura anterior DEVE mostrar o estado de erro com o motivo e o que conferir; com leitura anterior válida, a tela DEVE continuar com ela e mostrar um aviso da falha.
- **FR-023a**: Durante a leitura, a tela DEVE mostrar o tempo decorrido e oferecer "Cancelar"; cancelar DEVE devolver a tela ao catálogo anterior (ou ao erro com "Tentar de novo", sem catálogo anterior) e interromper a leitura no GitLab quando nenhuma outra pessoa estiver esperando por ela. Cancelamento não conta como falha da origem.
- **FR-024**: Itens do catálogo sem nome ou com estrutura inválida DEVEM ser ignorados, com aviso na tela da quantidade ignorada.
- **FR-025**: A falha da origem do catálogo NÃO DEVE afetar os outros módulos do SIAT.

**Apresentação**

- **FR-026**: A tela DEVE seguir o contrato visual `design/sistemas.html` e os componentes do SIAT (cabeçalho de página, painel, tabela com cabeçalho fixo e rolagem horizontal, estados vazio/erro/carregando, botão Copiar), nos temas claro, escuro e do sistema.
- **FR-027**: A tela DEVE atender à WCAG 2.2 AA e ser usável só por teclado, com foco visível; cada botão de cópia DEVE dizer o que copia, incluindo sistema e ambiente.
- **FR-028**: Todos os textos DEVEM estar em português do Brasil.

### Entidades-chave

- **Sistema**: item do catálogo; tem nome, observação opcional e, para cada ambiente, zero ou mais instâncias.
- **Ambiente**: Desenvolvimento (DSV), Homologação (HML), Produção (PRD) ou Contingência (CONT); lista fixa, com sigla e cor.
- **Instância**: um lugar onde o sistema roda num ambiente; tem um ou mais endereços (URL), uma ou mais máquinas e um caminho, todos opcionais.
- **Leitura do catálogo**: o conteúdo lido da origem num momento; tem horário, sistemas válidos, quantidade de itens ignorados e, quando a última tentativa falhou, o motivo.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: Uma pessoa do suporte encontra a URL, a máquina e o caminho de um sistema num ambiente em menos de 15 segundos a partir da abertura da tela.
- **SC-002**: 100% dos 63 sistemas, instâncias e campos do `sistemasDb.js` atual aparecem no SIAT com os mesmos valores que o `sistemas.html` mostra, verificado por comparação item a item.
- **SC-003**: A tela abre em até 2 segundos: com o catálogo já lido, mostra a lista nesse prazo; com o catálogo ainda por ler, mostra nesse prazo o estado de carregamento com o tempo decorrido. O filtro responde a cada tecla sem atraso perceptível com o catálogo completo.
- **SC-004**: Uma correção feita na origem aparece no SIAT em no máximo 10 minutos, ou na hora com "Atualizar agora".
- **SC-005**: Zero dados do catálogo entregues a usuários sem `Sistemas.CanSelect`, verificado por teste de acesso direto pela URL.
- **SC-006**: Todo fluxo das Histórias 1 e 2 é concluído só com teclado, e a tela não tem falhas WCAG 2.2 AA na verificação do roteiro de validação.
- **SC-007**: Três meses após a entrega, suporte e desenvolvimento consultam o catálogo só pelo SIAT, e o `sistemas.html` pode ser aposentado.

## Premissas

- O SIAT só lê o catálogo; editá-lo continua sendo editar o arquivo de origem pelo GitLab. A edição pelo módulo de Documentação (fase 6) fica fora desta feature.
- Um único recurso, `Sistemas.CanSelect`, libera todos os ambientes, como define o PRD (RG-02) e permite a constituição desde a v3.0.0. O catálogo descreve onde os sistemas rodam, não dados de negócio; por isso a leitura não é registrada no log de consultas a dados sensíveis.
- Quem recebe `Sistemas.CanSelect` (grupo do UserManager) é definido pela coordenação; a mensagem de acesso negado indica o recurso.
- A leitura da origem usa credencial somente de leitura da aplicação, guardada fora do repositório, e não o token do usuário.
- O formato do catálogo mantém os campos atuais (nome, DSV, HML, PRD, CONT, observação; instâncias com URL, máquina e caminho); campos novos (tipo de sistema, job do Jenkins, endereço de versão) ficam para as fases de Versões e Builds.
- O `sistemas.html` continua disponível até o módulo ser validado pelos usuários (Princípio IV), lendo o mesmo `sistemasDb.js`. Migrar para um `sistemas.json` (PRD SI-04) fica para quando o `sistemas.html` for aposentado, e o PRD deve registrar essa decisão.
- O branch principal do repositório TCMRJDoc é a versão do catálogo que vale.
- A lista de servidores das Consultas (`servidores.json`) continua onde está; movê-la para o GitLab é decisão separada.
- Unir Sistemas e Versões numa visão por sistema (VE-06) fica para a fase de Versões.
