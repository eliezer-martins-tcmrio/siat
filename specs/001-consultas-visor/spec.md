# Especificação: Consultas — MailQueue e AuditLog

**Branch da feature**: `001-consultas-visor`

**Criada em**: 2026-11-10

**Status**: Rascunho

**Entrada**: Descrição do usuário: "@PRD.md quero começar trazendo o visor, porém quero mudar o nome da seção no menu use "consultas""

## Contexto

Esta é a primeira entrega do SIAT. O PRD previa o Visor (MailQueue e AuditLog) na fase 3, depois de Sistemas e Versões; por decisão do responsável, ele passa a ser a primeira entrega. Por isso esta feature inclui também a base mínima da aplicação (login, permissões, layout e menu) necessária para o módulo funcionar sozinho.

No menu lateral, a seção que o PRD e o sistema de design chamam de **Visor** passa a se chamar **Consultas**, com os itens **MailQueue** e **AuditLog**. Requisitos do PRD cobertos: RG-01 a RG-05, RG-09 a RG-14 e MQ-01 a MQ-04, AL-01 a AL-04.

## Cenários de usuário e testes *(obrigatório)*

### História 1 — Consultar e-mails enfileirados em DSV/HML (Prioridade: P1)

Uma pessoa do suporte precisa saber se um e-mail foi enfileirado em homologação. Ela entra no SIAT com o login do Tribunal, abre **Consultas › MailQueue**, escolhe um servidor de HML na lista, opcionalmente escolhe o SMTP e digita um trecho do destinatário ou do assunto, e vê a lista dos e-mails encontrados, do mais recente para o mais antigo. Ao abrir uma linha, vê o detalhe do e-mail, com o corpo exibido de forma isolada.

**Por que esta prioridade**: é a pergunta mais frequente que o Visor responde hoje e, com a base da aplicação, já entrega valor sozinha, substituindo o executável local na maior parte dos usos.

**Teste independente**: com um usuário que tem só o recurso `MailQueue.CanSelect`, consultar um servidor de HML com e sem filtros e conferir que os resultados batem com os do Visor atual para os mesmos filtros.

**Cenários de aceitação**:

1. **Dado** um usuário autenticado com `MailQueue.CanSelect`, **quando** abre o SIAT, **então** vê no menu a seção **Consultas** com o item **MailQueue** e não vê **AuditLog**.
2. **Dado** a tela MailQueue aberta, **quando** o usuário abre o seletor de servidor, **então** vê apenas os servidores de DSV e HML da lista fechada, cada um com seu ambiente identificado, e nenhum campo para digitar servidor.
3. **Dado** um servidor escolhido, **quando** o usuário consulta sem filtros, **então** vê os 50 e-mails mais recentes de todos os SMTPs.
4. **Dado** um servidor escolhido, **quando** o usuário escolhe um SMTP, informa um texto e uma quantidade de 1 a 1000, **então** vê apenas os e-mails daquele SMTP cujo assunto, corpo, destinatários ou identificador da mensagem contêm o texto, limitados à quantidade.
5. **Dado** um resultado carregado, **quando** o usuário abre uma linha, **então** vê o detalhe do e-mail e o corpo exibido isolado, sem executar scripts nem afetar a página.
6. **Dado** uma consulta feita, **quando** o usuário copia a URL e outra pessoa com a mesma permissão a abre, **então** a consulta é refeita com o mesmo servidor e os mesmos filtros.

---

### História 2 — Consultar o log de auditoria em DSV/HML (Prioridade: P2)

Um desenvolvedor investiga um erro reportado em HML. Ele abre **Consultas › AuditLog**, escolhe o servidor, escolhe a aplicação na lista e vê os registros de log mais recentes. Ao abrir um registro, vê a exceção com o stack trace formatado e o LogEvent formatado e legível. Quando tem o número do log informado pelo usuário, busca pelo início do número.

**Por que esta prioridade**: é o segundo uso do Visor, essencial para desenvolvedores; depende da mesma base da História 1, mas é testável sem ela estar completa.

**Teste independente**: com um usuário que tem só `AuditLog.CanSelect`, consultar uma aplicação em HML e buscar um LogID conhecido, comparando com o Visor atual.

**Cenários de aceitação**:

1. **Dado** um usuário com `AuditLog.CanSelect`, **quando** abre **Consultas › AuditLog** e escolhe servidor e aplicação, **então** vê os registros daquela aplicação, do mais recente para o mais antigo, limitados à quantidade (padrão 50).
2. **Dado** a lista de aplicações indisponível no servidor escolhido, **quando** a tela é aberta, **então** aparece um campo numérico para informar o identificador da aplicação, com um aviso de que a lista não pôde ser carregada.
3. **Dado** o campo LogID, **quando** o usuário informa só dígitos, **então** vê os registros cujo LogID começa com esse número; **quando** informa outro caractere, **então** vê uma mensagem pedindo apenas dígitos e a consulta não é feita.
4. **Dado** um registro com exceção e LogEvent, **quando** o usuário o abre, **então** vê o stack trace com quebras de linha e destaque das linhas, e o LogEvent indentado e legível; se o LogEvent não for válido, vê o texto original sem erro na tela.

---

### História 3 — Consultar dados de PRD com permissão própria (Prioridade: P3)

Uma pessoa do grupo restrito de PRD precisa confirmar se um e-mail de produção foi enfileirado ou ver um erro de produção. Ela escolhe um servidor de PRD, que só aparece para quem tem o recurso de PRD, e a tela deixa claro que está consultando produção e que a consulta fica registrada.

**Por que esta prioridade**: os dados de PRD são os mais sensíveis; a entrega de DSV/HML já tem valor sozinha, e PRD só é liberado com os controles de acesso e auditoria validados.

**Teste independente**: com dois usuários, um com `MailQueue.CanSelect` e outro com `MailQueue.CanSelect` e `MailQueuePRD.CanSelect`, conferir que só o segundo vê e consegue consultar servidores de PRD, inclusive tentando pela URL.

**Cenários de aceitação**:

1. **Dado** um usuário sem `MailQueuePRD.CanSelect`, **quando** abre o seletor de servidor da MailQueue, **então** não vê nenhum servidor de PRD.
2. **Dado** o mesmo usuário, **quando** abre uma URL compartilhada que aponta para um servidor de PRD, **então** a consulta não é feita e ele vê uma mensagem de acesso negado, sem nenhum dado de PRD.
3. **Dado** um usuário com o recurso de PRD, **quando** escolhe um servidor de PRD, **então** a faixa de contexto no topo da tela fica destacada como produção e avisa que a consulta é registrada em log.
4. **Dado** qualquer consulta, em qualquer ambiente, **quando** é executada, **então** fica registrado no log quem consultou, o servidor, o módulo e os filtros usados.
5. As mesmas regras valem para o AuditLog com `AuditLogPRD.CanSelect`.

---

### História 4 — Acompanhar, cancelar, filtrar e exportar (Prioridade: P3)

Durante uma consulta lenta (por exemplo, uma aplicação sem registros, que obriga o banco a ler muito), o usuário vê o tempo decorrido e pode cancelar. Com o resultado na tela, pode filtrar os itens carregados, navegar pelo teclado e exportar o resultado em um arquivo HTML para anexar a um chamado.

**Por que esta prioridade**: melhora o uso das Histórias 1 e 2, que já entregam valor sem isto.

**Teste independente**: fazer uma consulta lenta e cancelá-la; filtrar uma lista carregada; exportar e abrir o arquivo sem o SIAT.

**Cenários de aceitação**:

1. **Dado** uma consulta em andamento, **quando** passa do primeiro segundo, **então** o usuário vê o tempo decorrido e um botão **Cancelar**.
2. **Dado** uma consulta em andamento, **quando** o usuário cancela, **então** a consulta é interrompida também no servidor de banco e o resultado anterior continua na tela.
3. **Dado** um resultado carregado, **quando** o usuário digita `/`, **então** o foco vai para o filtro da lista, que filtra só os itens já carregados, sem diferenciar acentos e maiúsculas, mostrando "X de Y".
4. **Dado** um resultado carregado, **quando** o usuário exporta, **então** recebe um arquivo HTML autocontido, com nome no padrão `mailqueue_AAAAMMDD_HHMMSS.html` ou `auditlog_AAAAMMDD_HHMMSS.html`, que abre sem o SIAT, mantém o filtro, os detalhes expansíveis e, na MailQueue, o corpo isolado dos e-mails.
5. **Dado** uma consulta sem resultados com filtros preenchidos, **quando** a lista vem vazia, **então** o usuário vê a opção de remover os filtros com um clique.

---

### Casos extremos

- O servidor escolhido está fora do ar ou não responde: a consulta termina pelo tempo limite com mensagem clara (servidor e motivo), sem afetar a outra tela nem o resto da aplicação.
- A conta de serviço não tem acesso a um banco num servidor da lista: mensagem de erro identificando servidor e banco; a lista de SMTPs ou de aplicações mostra o estado de indisponível.
- A lista fechada de servidores está vazia ou ilegível: a tela informa que não há servidores configurados.
- O usuário não tem nenhum recurso de Consultas: a seção **Consultas** não aparece no menu, e o acesso direto às páginas é negado.
- O usuário perde a permissão durante a sessão: a próxima consulta é negada pelo servidor.
- Quantidade fora de 1 a 1000 ou não numérica: o valor é ajustado ao limite ou recusado com mensagem, nunca enviado como está.
- Texto de busca com caracteres especiais (`%`, `_`, `[`, aspas): é procurado literalmente.
- Corpo de e-mail com scripts, formulários ou recursos externos: não executa nada e não carrega nada da página do SIAT.
- E-mail ou registro com conteúdo muito grande: a lista continua utilizável e o detalhe abre sob demanda.
- A conexão com a aplicação cai durante o uso: o usuário vê o aviso de reconexão e, ao voltar, pode refazer a consulta pela URL.
- Duas consultas disparadas em seguida: vale só a última; o resultado da anterior não sobrescreve a tela.

## Requisitos *(obrigatório)*

### Requisitos funcionais

**Base da aplicação**

- **FR-001**: O sistema DEVE exigir login pelo SSO corporativo do UserManager de PRD antes de mostrar qualquer página.
- **FR-002**: O sistema DEVE ser cadastrado no UserManager como aplicação própria, com os recursos `MailQueue.CanSelect`, `MailQueuePRD.CanSelect`, `AuditLog.CanSelect` e `AuditLogPRD.CanSelect`.
- **FR-003**: O menu lateral DEVE ter a seção **Consultas** (no lugar de "Visor") com os itens **MailQueue** e **AuditLog**; cada item só aparece para quem tem o recurso correspondente, e a seção some quando o usuário não tem nenhum dos dois.
- **FR-004**: Toda página e toda consulta DEVEM ser autorizadas no servidor pelo recurso correspondente, independentemente do que a interface mostra.
- **FR-005**: O layout DEVE seguir o sistema de design do SIAT (`DESIGN.md`): menu lateral com logo, temas claro, escuro e do sistema, e rodapé com tema, conta e sair.
- **FR-006**: A página inicial DEVE levar o usuário à primeira ferramenta de Consultas a que ele tem acesso ou, se não tiver nenhuma, informar que ele não tem acesso e a quem pedir.

**Servidores**

- **FR-007**: O servidor a consultar DEVE ser escolhido de uma lista fechada, em que cada servidor tem nome e ambiente (DSV, HML ou PRD); NÃO DEVE existir campo de servidor livre, nem na tela nem na URL.
- **FR-008**: O seletor DEVE mostrar apenas os servidores permitidos ao usuário: DSV e HML com o recurso comum do módulo; PRD apenas com o recurso de PRD do módulo.
- **FR-009**: Toda tela de consulta DEVE mostrar a faixa de contexto com servidor e ambiente; em PRD, destacada e com o aviso de que a consulta é registrada em log.

**MailQueue**

- **FR-010**: Os usuários DEVEM poder filtrar por SMTP (lista vinda do servidor escolhido, com "Todos os SMTPs"), por texto (procurado em assunto, corpo, destinatários e identificador da mensagem) e por quantidade de 1 a 1000, padrão 50.
- **FR-011**: O resultado DEVE vir do mais recente para o mais antigo, em lista expansível com as colunas e o detalhe equivalentes aos do Visor atual.
- **FR-012**: O corpo do e-mail DEVE ser exibido isolado da página, sem executar scripts.

**AuditLog**

- **FR-013**: Os usuários DEVEM poder filtrar por aplicação (lista do UserManager; campo numérico quando a lista não estiver disponível), por início do LogID (só dígitos, até 20) e por quantidade de 1 a 1000, padrão 50.
- **FR-014**: O resultado DEVE vir do mais recente para o mais antigo, em lista expansível com as colunas e o detalhe equivalentes aos do Visor atual.
- **FR-015**: O detalhe DEVE exibir a exceção com o stack trace formatado e o LogEvent indentado; conteúdo inválido DEVE ser exibido como texto original.

**Comportamentos comuns**

- **FR-016**: O texto de busca DEVE ser procurado literalmente, sem interpretar curingas.
- **FR-017**: Servidor, filtros e o item aberto DEVEM ficar na URL; abrir a URL refaz a consulta, respeitando as permissões de quem a abre.
- **FR-018**: Consultas DEVEM mostrar o tempo decorrido e poder ser canceladas, interrompendo o comando no servidor de banco; o resultado anterior permanece na tela ao cancelar.
- **FR-019**: Toda consulta DEVE ter tempo limite e, ao falhar, mostrar mensagem com o servidor e o motivo, sem afetar o resto da aplicação.
- **FR-020**: A lista de resultados DEVE ter um filtro local dos itens carregados (atalho `/`, sem diferenciar acentos e maiúsculas, contador "X de Y") e navegação por teclado entre as linhas.
- **FR-021**: Os usuários DEVEM poder exportar o resultado carregado em um arquivo HTML autocontido, que abre sem o SIAT e mantém filtro, detalhes e corpo isolado dos e-mails.
- **FR-022**: Toda consulta DEVE ser registrada no log com usuário, data e hora, módulo, servidor, ambiente e filtros; toda exportação também.
- **FR-023**: As consultas DEVEM usar uma conta de serviço somente de leitura; o SIAT NÃO DEVE alterar nenhum dado da MailQueue nem do AuditLog.
- **FR-024**: Textos, mensagens e identificadores DEVEM estar em português do Brasil, e as telas DEVEM atender à WCAG 2.2 AA e permitir uso completo por teclado.

### Entidades-chave

- **Servidor**: instância de banco que pode ser consultada; tem nome de exibição, endereço e ambiente (DSV, HML, PRD). Faz parte da lista fechada, mantida fora do código.
- **SMTP**: configuração de envio cadastrada em cada servidor; tem identificador e descrição. Usado como filtro da MailQueue.
- **Mensagem enfileirada**: e-mail na MailQueue; tem identificador, SMTP, data, remetente, destinatários, assunto, corpo, situação de envio e identificador da mensagem.
- **Aplicação**: sistema cadastrado no UserManager do servidor; tem identificador e nome. Usada como filtro do AuditLog.
- **Registro de auditoria**: linha do log de auditoria; tem LogID, aplicação, data, nível, mensagem, exceção e LogEvent.
- **Registro de consulta**: entrada no log do SIAT sobre cada consulta ou exportação: usuário, momento, módulo, servidor, ambiente e filtros.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: Uma pessoa do suporte responde "o e-mail foi enfileirado em HML?" em menos de 30 segundos a partir do login, sem instalar nem executar nada na própria máquina.
- **SC-002**: Para os mesmos servidor e filtros, os resultados de MailQueue e AuditLog são idênticos aos do Visor atual em 100% dos casos de teste de aceitação.
- **SC-003**: As telas abrem em até 2 segundos; consultas com filtro em servidores disponíveis retornam em até 5 segundos na maioria dos casos medidos.
- **SC-004**: Um cancelamento interrompe a consulta no banco em até 2 segundos.
- **SC-005**: Zero consultas a PRD feitas por usuários sem o recurso de PRD, verificado pelo registro de consultas e por testes de acesso direto pela URL.
- **SC-006**: 100% das consultas e exportações aparecem no registro de consultas com usuário, servidor e filtros.
- **SC-007**: Três meses após a entrega, o executável do Visor deixou de ser usado pelas equipes de suporte e desenvolvimento.

## Premissas

- A base da aplicação (login, permissões, layout, menu, temas) entra nesta feature por ser a primeira entrega; Sistemas, Versões e os demais módulos continuam nas fases seguintes e aparecerão no menu em suas próprias seções.
- A seção de menu **Consultas** agrupa as consultas a bancos de servidores escolhidos pelo usuário; futuras consultas desse tipo podem entrar nela. O `DESIGN.md` e as telas de exemplo em `design/` serão atualizados para usar "Consultas" no lugar de "Visor".
- Filtros, colunas e detalhes reproduzem os do Visor atual (`Tcmrj.Visor`), que serve de referência de comportamento; as telas de exemplo `design/mailqueue.html` e `design/auditlog.html` são o contrato visual.
- A lista fechada de servidores fica em arquivo de configuração versionado, com ambiente por servidor; o conteúdo inicial e quem pode alterá-lo seguem como questão em aberto do PRD (item 3), sem bloquear o desenvolvimento.
- A conta de serviço somente de leitura terá acesso aos bancos MailQueue, AUDITORIADB e à lista de aplicações do UserManager em cada servidor da lista.
- Os grupos do UserManager que recebem os recursos de PRD são definidos pela coordenação (questão em aberto 4 do PRD); a entrega de PRD (História 3) só é liberada depois disso.
- O registro de consultas usa o mecanismo de log já adotado pelo Tribunal; o SIAT não guarda dados próprios.
- O Visor atual continua disponível até o módulo Consultas ser validado pelos usuários.
- Itens do Visor atual ligados à execução local (porta, abrir navegador, encerrar servidor, servidores sugeridos guardados no navegador, token de sessão na URL) não se aplicam e ficam fora do escopo.
