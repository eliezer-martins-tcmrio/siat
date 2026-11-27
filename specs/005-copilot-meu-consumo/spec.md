# Especificação: Meu consumo do Copilot

**Branch da feature**: `features/<tarefa>_copilot_meu_consumo` (ou `features/copilot_meu_consumo` sem número de tarefa); a pasta da spec é `specs/005-copilot-meu-consumo`

**Criada em**: 2026-11-27

**Status**: Rascunho

**Entrada**: Descrição do usuário: "iniciar a portabilidade do gestão copilot; nessa primeira etapa, apenas a visão do próprio usuário logado via OAuth, a tela de pessoas/<usuario> do D:\Projetos\gestao-copilot"

## Contexto

Hoje o consumo de GitHub AI Credits do Copilot é consultado no `gestao-copilot`, uma aplicação separada com login próprio pelo GitHub. Nela, um owner da organização vê o painel completo, e um membro vê só a página dos próprios dados, `/pessoas/<login>`: licença, times, limite, consumo no período, evolução diária e consumo por modelo.

O PRD prevê o porte do `gestao-copilot` para o SIAT na fase 4 (seção 7.7). Esta feature é a primeira etapa desse porte e cobre só o **CP-03**: a página "Meu consumo", em que a pessoa vê o próprio consumo depois de ligar a sua conta do GitHub ao SIAT por "Conectar ao GitHub" (OAuth). O painel completo (Visão geral, Pessoas, Modelos, CP-01) e a gestão de limites (CP-02) ficam para as próximas etapas. Como no `gestao-copilot`, as regras de negócio do projeto atual (README e testes) são a referência, com as adaptações às regras do SIAT descritas abaixo. Requisitos do PRD cobertos: CP-03, CP-04 (no que toca a esta página), RG-02, RG-03, RG-07, RG-08 e RG-11 a RG-14.

## Esclarecimentos

### Sessão 2026-11-27

- P: Quem não tem exceção individual vê o valor do limite padrão da organização? → R: Sim. O padrão é o limite vigente da própria pessoa, então a página mostra o valor e calcula o uso do limite. O limite global, a capacidade e os limites de outras pessoas continuam fora da página. Isso muda a regra do `gestao-copilot`, que mostrava ao membro só a própria exceção e, sem ela, deixava o uso do limite indisponível.
- P: Por quanto tempo o SIAT lembra a conta do GitHub conectada? → R: 30 dias, num cookie protegido no navegador, preso ao usuário do SIAT que conectou; sair do SIAT não desfaz a ligação, e **Desconectar do GitHub** desfaz. Descartadas a ligação só durante a sessão do SIAT (reconectar a cada entrada) e o cookie de 8 horas.
- P: Qual o maior intervalo que se pode consultar de uma vez? → R: 92 dias, dentro da janela de 24 meses. Cada dia é uma leitura com a credencial de billing compartilhada por todos os usuários; o limite cobre o mês atual, o anterior e um trimestre. Descartados 31 dias e o intervalo livre do `gestao-copilot` (até cerca de 730 leituras numa consulta).
- P: Cada consulta ao próprio consumo é registrada no log de auditoria? → R: Não. O próprio consumo não é dado sensível de terceiros; o log registra a conexão, a desconexão e as recusas de conexão da conta do GitHub (FR-012). Descartado registrar também cada consulta, como no MailQueue e no AuditLog.

## Cenários de usuário e testes *(obrigatório)*

### História 1 — Ver o próprio consumo do mês (Prioridade: P1)

Uma desenvolvedora quer saber quanto já usou do Copilot neste mês e quanto falta para o limite. Ela entra no SIAT, abre **Gestão › Copilot** e, como a conta do GitHub já está conectada neste navegador, vê a própria página: nome e foto do GitHub, licença ativa, os times, o consumo bruto e líquido do mês até hoje, os créditos consumidos, o limite vigente e o uso do limite, a evolução diária e o consumo por modelo.

**Por que esta prioridade**: é a consulta que os membros fazem hoje no `gestao-copilot`; sozinha, entrega o valor do CP-03 e permite deixar de usar a ferramenta antiga para esse fim.

**Teste independente**: com um usuário que tem `CopilotProprio.CanSelect` e uma conta do GitHub com licença na organização, conectar a conta, abrir a página e conferir os valores com a página da mesma pessoa no `gestao-copilot`, no mesmo período e no mesmo momento.

**Cenários de aceitação**:

1. **Dado** um usuário com `CopilotProprio.CanSelect` e a conta do GitHub conectada, **quando** abre **Gestão › Copilot**, **então** vê a página do próprio login do GitHub com o período padrão, do primeiro dia do mês atual até hoje (UTC), e o caminho de navegação **Gestão › Copilot › Meu consumo**.
2. **Dado** a página aberta, **quando** é exibida, **então** o cabeçalho mostra o nome e a foto do perfil do GitHub (ou o login, sem nome público), a situação da licença ("Licença Copilot ativa" ou "Sem licença Copilot") e os times da pessoa, cada um com link para o time no GitHub, em nova aba, e a indicação "via sub-time" quando o vínculo é herdado.
3. **Dado** a página aberta, **quando** o consumo do período é carregado, **então** o resumo mostra o consumo bruto em USD com os créditos consumidos e o período, o valor líquido com a nota "Não inclui o preço das licenças", o limite vigente com a sua origem (exceção individual ou padrão da organização) e o uso do limite.
4. **Dado** um período que inclui o mês atual, **quando** o uso do limite é exibido, **então** ele compara o consumo bruto acumulado do mês atual (todos os modelos) com o limite vigente, com faixas de atenção a partir de 80% e de limite atingido a partir de 100%, em texto e não só pela cor, e indica quanto falta para o limite no relatório.
5. **Dado** um período que não inclui o mês atual, **quando** o resumo é exibido, **então** o uso do limite aparece como "—", com a nota "Disponível em períodos que incluem o mês atual".
6. **Dado** consumo no período, **quando** a página é exibida, **então** mostra a evolução diária (valores bruto e líquido ou créditos, e a visão por modelo com os cinco modelos de maior consumo e os demais em "Outros") e a tabela de consumo por modelo, com créditos, valor bruto, valor líquido e participação no valor bruto, ordenada por créditos.
7. **Dado** um período sem consumo reportado, **quando** a página é exibida, **então** a tabela de modelos mostra "Sem consumo reportado neste período", e os totais aparecem como zero, porque o relatório informou zero.

---

### História 2 — Conectar a conta do GitHub (Prioridade: P1)

Um desenvolvedor abre **Gestão › Copilot** pela primeira vez. O SIAT não sabe qual é a conta dele no GitHub, então a página explica por que precisa da conexão e oferece **Conectar ao GitHub**. Ele autoriza no GitHub e volta para a própria página, já com os dados.

**Por que esta prioridade**: sem a conexão o SIAT não sabe de quem é o consumo; a História 1 depende dela.

**Teste independente**: com um usuário que tem `CopilotProprio.CanSelect`, abrir a página sem conexão, conectar com uma conta membro da organização, com uma conta que não é membro e cancelando a autorização no GitHub; depois sair do SIAT, entrar de novo e conferir que a conexão continua; entrar com outro usuário do SIAT no mesmo navegador e conferir que a conexão é pedida.

**Cenários de aceitação**:

1. **Dado** um usuário com `CopilotProprio.CanSelect` sem conta do GitHub conectada, **quando** abre **Gestão › Copilot**, **então** vê a explicação de que o SIAT precisa identificar a sua conta do GitHub para mostrar o consumo e o botão **Conectar ao GitHub**, e nenhum dado do Copilot é lido.
2. **Dado** a explicação, **quando** o usuário aciona **Conectar ao GitHub** e autoriza no GitHub, **então** volta para a própria página no SIAT, com o período que estava no endereço, e os dados são carregados.
3. **Dado** a autorização no GitHub, **quando** o SIAT a recebe, **então** confirma que a conta é membro ativo da organização configurada; se não for, a conexão é recusada com a mensagem "A conta <login> do GitHub não é membro ativo da organização <organização>.", e nenhum dado é exibido.
4. **Dado** a autorização cancelada ou recusada no GitHub, **quando** o usuário volta ao SIAT, **então** vê a mensagem de que a conexão não foi feita e pode tentar de novo.
5. **Dado** uma conta conectada, **quando** o usuário aciona **Desconectar do GitHub**, **então** a ligação acaba, a página volta à explicação com **Conectar ao GitHub**, e ele pode conectar outra conta.
6. **Dado** uma conta conectada, **quando** o usuário sai do SIAT e entra de novo no mesmo navegador dentro de 30 dias, **então** a página abre já com a conta conectada, sem pedir a conexão; depois de 30 dias da conexão, ou em outro navegador, a conexão é pedida de novo.
7. **Dado** uma conta conectada que deixou de ser membro ativo da organização, **quando** a página é carregada ou atualizada, **então** a ligação acaba com a mensagem correspondente, e nenhum dado é exibido.
8. **Dado** uma conta conectada por um usuário do SIAT, **quando** outro usuário do SIAT entra no mesmo navegador, **então** a ligação do primeiro não vale para ele, e a conexão é pedida.

---

### História 3 — Consultar outro período e compartilhar o link (Prioridade: P2)

A desenvolvedora quer comparar o consumo do mês anterior. Ela escolhe o atalho **Mês anterior**, e a página mostra o consumo daquele mês; na evolução diária, aciona o dia de maior consumo e vê só aquele dia. Ela copia o endereço da página para abri-lo depois.

**Por que esta prioridade**: a página já entrega valor com o mês atual; o histórico e o link completam o uso do dia a dia.

**Teste independente**: escolher cada atalho e um intervalo manual, acionar um dia na evolução, recarregar a página e abrir o endereço copiado numa nova aba.

**Cenários de aceitação**:

1. **Dado** a página aberta, **quando** o usuário informa a data inicial e a final (inclusivas, em UTC) e aplica, **então** o consumo, a evolução e a tabela de modelos passam a ser do intervalo, e as datas ficam no endereço da página.
2. **Dado** a página aberta, **quando** o usuário escolhe um atalho (**Hoje**, **Ontem**, **Últimos 7 dias**, **Últimos 30 dias**, **Mês atual**, **Mês anterior**), **então** o intervalo é preenchido e consultado.
3. **Dado** um intervalo, **quando** o usuário aciona **Limpar período**, **então** volta ao período padrão.
4. **Dado** uma data inválida, futura, invertida, anterior ao início dos 24 meses disponíveis ou um intervalo de mais de 92 dias, **quando** o usuário aplica, **então** a página mostra "Selecione datas válidas, em ordem, num intervalo de até 92 dias entre o início dos 24 meses disponíveis e hoje (UTC).", e nada é consultado.
5. **Dado** a evolução diária, **quando** o usuário aciona um dia, **então** o período passa a ser aquele dia.
6. **Dado** um endereço com período, **quando** o próprio usuário o abre de novo, **então** a página restaura o período. Licença, times e limite são sempre os vigentes, mesmo num período histórico, e a página diz isso.

---

### História 4 — Página rápida, que não trava e mostra falhas como falhas (Prioridade: P2)

Um desenvolvedor consulta os últimos 30 dias, o que exige um relatório por dia. A página mostra quantos dias já leu, o tempo decorrido e **Cancelar**. Na volta à página minutos depois, o resultado aparece sem nova leitura. Quando o GitHub limita as requisições, a página diz quando tentar de novo, em vez de mostrar zero.

**Por que esta prioridade**: é a regra de desempenho e resiliência de todo o SIAT; não muda o conteúdo das demais histórias.

**Teste independente**: consultar 30 dias com o cache vazio, cancelar no meio, repetir a consulta, voltar à página dentro do prazo do cache, acionar **Atualizar agora** e simular falha e limite de requisições do GitHub.

**Cenários de aceitação**:

1. **Dado** uma consulta em andamento, **quando** a página é exibida, **então** mostra o progresso (dias lidos do total), o tempo decorrido e **Cancelar**; cancelar interrompe as leituras restantes e devolve a página ao resultado anterior ou ao estado com **Tentar de novo**, e cancelamento não conta como falha.
2. **Dado** um período já consultado dentro do prazo do cache, **quando** o usuário volta à página, **então** o resultado aparece sem novas leituras de relatórios, licença, times e limite (só a confirmação de que a conta continua membro, FR-010), com o horário da consulta original.
3. **Dado** a página aberta, **quando** o usuário aciona **Atualizar agora**, **então** a página relê a licença, os times, o limite e os dias do período que ainda podem mudar, e mostra o novo horário.
4. **Dado** a falha de algum dia do período, **quando** a consulta termina, **então** o consumo, a evolução e a tabela de modelos aparecem como "Indisponível", com o motivo e **Tentar de novo**; um total incompleto nunca é exibido como completo, e uma falha nunca é exibida como zero.
5. **Dado** o limite de requisições do GitHub atingido, **quando** a consulta é feita, **então** a página interrompe as leituras restantes e informa quando tentar de novo, se o GitHub informar.
6. **Dado** a falha só dos times ou só do limite, **quando** a página é exibida, **então** o restante aparece normalmente, e o item com falha aparece como indisponível ("Times indisponíveis", "Limite indisponível").

---

### História 5 — Acesso só aos próprios dados (Prioridade: P3)

Um usuário com acesso só ao próprio consumo cola na barra de endereço a página de outro login. O SIAT o leva à própria página, sem mostrar nada da outra pessoa.

**Por que esta prioridade**: a regra de acesso é a mesma da base do SIAT; não muda o uso das demais histórias, mas é inegociável.

**Teste independente**: abrir a página do próprio login e a de outro login com um usuário sem `CopilotProprio.CanSelect`, com o recurso e sem conta conectada, e com o recurso e conta conectada; abrir com login escrito com maiúsculas diferentes.

**Cenários de aceitação**:

1. **Dado** um usuário sem `CopilotProprio.CanSelect`, **quando** abre a página por qualquer endereço, **então** vê a mensagem de acesso negado com o recurso necessário, o item **Copilot** não aparece no menu nem na página Início, e nenhum dado do Copilot é lido.
2. **Dado** um usuário com o recurso e conta conectada, **quando** abre o endereço da página de outro login, **então** é levado à própria página, com o mesmo período, e nenhum dado do outro login é lido nem enviado ao navegador.
3. **Dado** o endereço do próprio login escrito com maiúsculas diferentes, **quando** é aberto, **então** a página é a do próprio usuário.
4. **Dado** um usuário com o recurso e sem conta conectada, **quando** abre o endereço de qualquer login, **então** vê a explicação com **Conectar ao GitHub**, e nenhum dado é lido.
5. **Dado** a página e a explicação de conexão, **quando** navegadas só por teclado e leitor de tela, **então** o filtro de período, os atalhos, **Conectar ao GitHub**, **Desconectar do GitHub**, **Atualizar agora**, os links dos times e os dias da evolução são alcançáveis, com foco visível, e cada um diz o que faz.

### Casos extremos

- Conta do GitHub membro da organização, mas sem licença Copilot e sem exceção individual de limite: a página mostra a identificação da pessoa e a mensagem "Sua conta não tem licença Copilot nem limite individual na organização <organização>.", sem consumo.
- Conta sem licença, mas com exceção individual de limite: a página aparece normalmente, com "Sem licença Copilot".
- Mais de uma exceção individual para a mesma pessoa: o limite vigente aparece como "Múltiplos budgets", e o uso do limite como indisponível.
- Limite vigente igual a zero: o uso do limite aparece como "Limite zero", não como divisão por zero nem como 0%.
- Registros sem identificação do modelo: aparecem como "Modelo não informado", em texto diferenciado.
- O dia de hoje pode estar parcial no relatório: a página informa que relatórios podem ter atraso e não afirma que a pessoa está bloqueada.
- Login do GitHub renomeado depois da conexão: a ligação é pela identidade numérica da conta; a página passa a usar o login atual na próxima carga.
- Endereço com login inválido (caracteres que um login do GitHub não aceita): a página trata como outro login e leva à própria página.
- O mesmo usuário do SIAT com duas abas abertas: as duas usam a mesma conta conectada; desconectar numa vale para a outra na próxima carga.
- Organização configurada sem o token de leitura de billing ou com token inválido: a página mostra que o módulo Copilot está indisponível por configuração, sem afetar os outros módulos.
- Indisponibilidade do GitHub durante a conexão: a página informa a falha e oferece tentar de novo.

## Requisitos *(obrigatório)*

### Requisitos funcionais

**Acesso e navegação**

- **FR-001**: A página DEVE exigir `CopilotProprio.CanSelect`, verificado no servidor; sem ele, nenhum dado do Copilot é lido nem enviado ao navegador, e a tela mostra a mensagem de acesso negado indicando o recurso.
- **FR-002**: O menu DEVE ter o item **Copilot** no grupo **Gestão** e a página Início, o atalho correspondente, só para quem tem `CopilotProprio.CanSelect`; o item leva à página do próprio usuário (ou à explicação de conexão, sem conta conectada).
- **FR-003**: O endereço da página DEVE identificar o login do GitHub e o período; a página DEVE mostrar o caminho de navegação **Gestão › Copilot › Meu consumo**.
- **FR-004**: A página DEVE mostrar só os dados da conta do GitHub conectada pelo próprio usuário. O endereço de outro login DEVE levar à página do próprio usuário, mantendo o período, sem ler nem enviar ao navegador nenhum dado do outro login. A comparação de logins DEVE ignorar maiúsculas.
- **FR-005**: Nesta etapa, nenhum outro recurso (inclusive `Copilot.CanSelect` e `Copilot.CanEdit`) DEVE dar acesso a dados de outras pessoas nem permitir alterar limites; a página NÃO DEVE ter nenhuma ação de escrita.

**Conexão com o GitHub**

- **FR-006**: Sem conta conectada, a página DEVE explicar que o SIAT precisa identificar a conta do GitHub para mostrar o consumo e oferecer **Conectar ao GitHub**, sem ler dados do Copilot.
- **FR-007**: **Conectar ao GitHub** DEVE usar a autorização OAuth do GitHub numa aplicação OAuth do SIAT, pedindo só a identificação da conta, sem acesso a repositórios, à organização nem a dados privados, e DEVE proteger o retorno contra falsificação de requisição.
- **FR-008**: Depois da autorização, o SIAT DEVE confirmar, com a credencial de leitura do servidor, que a conta é membro ativo da organização configurada e que a identidade numérica da conta confere; se não, DEVE recusar a conexão com a mensagem de que a conta não é membro ativo da organização.
- **FR-009**: A ligação entre o usuário do SIAT e a conta do GitHub DEVE ficar só no navegador, num cookie protegido contra leitura por scripts e contra alteração, preso ao usuário do SIAT que conectou, e DEVE durar 30 dias a partir da conexão, mesmo que a pessoa saia do SIAT; termina antes com **Desconectar do GitHub** ou quando a conta deixa de ser membro ativo (FR-010). Um cookie de outro usuário do SIAT, vencido ou adulterado DEVE ser ignorado. O SIAT NÃO DEVE gravar a ligação em banco nem em arquivo no servidor, e NÃO DEVE guardar o token do usuário depois de identificar a conta.
- **FR-010**: A cada carga da página e a cada **Atualizar agora**, o SIAT DEVE confirmar que a conta conectada continua membro ativo da organização antes de ler o consumo; se não, DEVE desfazer a ligação e informar o motivo. As leituras simultâneas de uma mesma carga compartilham uma confirmação.
- **FR-011**: Autorização cancelada, recusada ou com falha DEVE voltar à página com a mensagem correspondente e a opção de tentar de novo.
- **FR-012**: A conexão e a desconexão da conta do GitHub, e as recusas de conexão, DEVEM ser registradas no log de auditoria do SIAT com o usuário do SIAT, o login e a identidade numérica do GitHub e o resultado.

**Conteúdo da página**

- **FR-013**: O cabeçalho DEVE mostrar o nome e a foto do perfil do GitHub (o login, sem nome público), a situação da licença ("Licença Copilot ativa" ou "Sem licença Copilot"), os times visíveis à credencial de leitura com link para o time no GitHub em nova aba e "via sub-time" quando o vínculo é herdado; "Sem time visível" quando a consulta completa não encontra vínculo; e "Times indisponíveis" quando a consulta falha.
- **FR-014**: O resumo DEVE mostrar, para o período: consumo bruto em USD, com os créditos consumidos e o período; valor líquido em USD, com a nota "Não inclui o preço das licenças"; limite vigente; e uso do limite.
- **FR-015**: O limite vigente DEVE ser a exceção individual da pessoa, quando existir ("Exceção individual"); com mais de uma, "Múltiplos budgets"; sem exceção, DEVE mostrar o valor do limite padrão da organização ("Padrão da organização"), que é o limite da pessoa. O limite global, a capacidade, o total alocado e os limites de outras pessoas NÃO DEVEM ser lidos para exibição nem enviados ao navegador.
- **FR-016**: Quando o período inclui o mês atual, o uso do limite DEVE ser o consumo bruto acumulado do mês atual em UTC, de todos os modelos, consultado à parte do período, dividido pelo limite vigente, com faixas de atenção a partir de 80% e de limite atingido no relatório a partir de 100%, indicadas em texto e não só por cor, e a diferença para o limite. Limite zero DEVE aparecer como "Limite zero". Quando o período não inclui o mês atual, DEVE aparecer "—" com a nota "Disponível em períodos que incluem o mês atual".
- **FR-017**: A página DEVE mostrar a evolução diária do período com os valores bruto e líquido ou os créditos, e a visão por modelo (valor bruto ou créditos) com os cinco modelos de maior consumo e os demais em "Outros"; acionar um dia DEVE mudar o período para aquele dia. A evolução DEVE ter alternativa em texto acessível a leitor de tela.
- **FR-018**: A tabela **Consumo por modelo** DEVE mostrar modelo, créditos consumidos, valor bruto, valor líquido e participação no valor bruto do período, ordenada por créditos (decrescente) e, no empate, pelo nome do modelo; registros sem modelo DEVEM aparecer como "Modelo não informado"; sem consumo, "Sem consumo reportado neste período".
- **FR-019**: A página DEVE informar que licença, times e limite refletem a situação atual, mesmo num período histórico, e que relatórios podem ter atraso; NÃO DEVE afirmar que a pessoa está bloqueada com base no relatório.
- **FR-020**: Valores indisponíveis DEVEM aparecer como indisponíveis, nunca como zero; um total com algum dia sem resposta NÃO DEVE ser exibido.
- **FR-021**: Quando a conta conectada não tem licença nem exceção individual na organização, a página DEVE dizer isso e não consultar o consumo.
- **FR-022**: Todo texto vindo do GitHub (nomes, logins, times, modelos, mensagens de erro) DEVE ser exibido como texto, sem interpretar marcação; mensagens de erro NÃO DEVEM expor tokens nem respostas brutas do GitHub.

**Período e endereço da página**

- **FR-023**: O período DEVE ter data inicial e final inclusivas, em UTC, entre o primeiro dia do mais antigo dos 24 meses disponíveis (o mês atual e os 23 anteriores) e hoje, com no máximo 92 dias por consulta; o padrão é do primeiro dia do mês atual até hoje.
- **FR-024**: A página DEVE oferecer os atalhos **Hoje**, **Ontem**, **Últimos 7 dias**, **Últimos 30 dias**, **Mês atual** e **Mês anterior**, e **Limpar período**, que volta ao padrão.
- **FR-025**: As datas DEVEM ficar no endereço da página, e abrir o endereço DEVE restaurá-las; datas inválidas, futuras, invertidas, fora da janela ou num intervalo de mais de 92 dias NÃO DEVEM ser consultadas, e a página DEVE mostrar a mensagem de período inválido.

**Leitura, cache e resiliência**

- **FR-026**: As leituras de consumo, licenças, times, limites e perfis DEVEM usar a credencial de leitura de billing do SIAT, guardada só no servidor e nunca enviada ao navegador; a organização DEVE ser fixa na configuração, sem escolha pelo usuário.
- **FR-027**: O consumo do período DEVE ser montado dos relatórios diários da pessoa, com leituras em paralelo limitadas, tempo limite em cada chamada e interrupção das leituras restantes na primeira falha, no cancelamento ou no limite de requisições do GitHub.
- **FR-028**: Durante a consulta, a página DEVE mostrar o progresso (dias lidos do total), o tempo decorrido e **Cancelar**; cancelar DEVE interromper as leituras restantes e devolver a página ao resultado anterior ou ao estado com **Tentar de novo**; cancelamento não conta como falha.
- **FR-029**: Os resultados DEVEM ficar em cache só em memória, com expiração. Perfis e relatórios ficam separados por conta do GitHub; licenças, times, membros e limites são listas da organização, guardadas uma vez para todos, das quais só sai para a página o que é da conta conectada. Prazos: relatórios dos últimos três dias em UTC, 10 minutos; relatórios mais antigos, que já não mudam, prazo maior definido na configuração; licenças, times e membros, 5 minutos; limites, 30 segundos; perfis, 6 horas. Pedidos simultâneos do mesmo relatório DEVEM gerar uma só leitura.
- **FR-030**: **Atualizar agora** DEVE reler a confirmação de membro, a licença, os times, o limite, o perfil e os relatórios dos últimos três dias do período e do acumulado do mês, sem esperar o prazo do cache, e mostrar o horário da nova consulta.
- **FR-031**: Ao atingir o limite de requisições do GitHub, a página DEVE informar a situação e, quando o GitHub informar, quando tentar de novo.
- **FR-032**: A falha de uma parte (times, limite, perfil, consumo do período, acumulado do mês) DEVE afetar só aquela parte da página; falhas do GitHub ou da configuração do Copilot NÃO DEVEM afetar os outros módulos do SIAT.

**Apresentação**

- **FR-033**: A página DEVE usar os componentes próprios do SIAT (cabeçalho de página, filtro de período, resumo, tabela de resultados, estados vazio/erro/carregando, avisos) e a aparência do SIAT, nos temas claro, escuro e do sistema, seguindo o desenho da página de pessoa do `gestao-copilot`.
- **FR-034**: A página DEVE atender à WCAG 2.2 AA e ser usável só por teclado, com foco visível; estados (licença, faixas de uso, indisponível) NÃO DEVEM depender só de cor; links que abrem nova aba DEVEM dizer isso.
- **FR-035**: Todos os textos DEVEM estar em português do Brasil; valores financeiros DEVEM ser identificados como USD.

### Entidades-chave

- **Conexão com o GitHub**: ligação, guardada só no navegador por até 30 dias, entre o usuário autenticado no SIA e uma conta do GitHub (login e identidade numérica), confirmada como membro ativo da organização.
- **Pessoa do Copilot**: a conta do GitHub na organização, com nome e foto públicos, situação da licença Copilot e times visíveis.
- **Limite vigente**: o budget de AI Credits que vale hoje para a pessoa: a exceção individual ou o padrão da organização; pode ser ambíguo (mais de uma exceção) ou zero.
- **Relatório de consumo**: créditos, valor bruto e valor líquido de um dia (ou do acumulado de um mês), no total e por modelo.
- **Período**: intervalo de datas inclusivas em UTC, dentro da janela de 24 meses e com no máximo 92 dias, que define quais relatórios diários compõem a página.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: Um desenvolvedor que nunca conectou a conta chega do menu à própria página com os dados do mês em menos de 1 minuto, incluindo a autorização no GitHub.
- **SC-002**: Com a conta conectada, a página abre em até 2 segundos, e o consumo do mês atual aparece em até 10 segundos com o cache vazio e em até 2 segundos quando consultado nos últimos 10 minutos.
- **SC-003**: Para a mesma pessoa e o mesmo período, o consumo bruto, o valor líquido, os créditos, o limite e o consumo por modelo coincidem com os do `gestao-copilot` em 100% dos casos conferidos no roteiro de validação.
- **SC-004**: Nenhum usuário vê dados de outra conta do GitHub: todos os cenários de acesso (sem recurso, sem conexão, outro login, conta fora da organização) são cobertos por testes automatizados e passam.
- **SC-005**: Nenhuma falha de leitura aparece como zero: todos os cenários de falha simulados (dia sem resposta, limite de requisições, times, limite) mostram "Indisponível" com o motivo.
- **SC-006**: Três meses depois da entrega, os membros deixam de usar o `gestao-copilot` para consultar o próprio consumo (métrica do PRD, seção 10).

## Premissas

- Esta etapa cobre só o CP-03. O painel completo (Visão geral, Pessoas, Modelos, CP-01) e os limites (CP-02, com `Copilot.CanEdit`) ficam para as próximas etapas, que reaproveitam esta página como o "Detalhes da pessoa" de quem tem `Copilot.CanSelect`. O `gestao-copilot` continua disponível até o módulo do SIAT ser validado (Princípio IV).
- O SIAT só lê nesta etapa: nenhuma operação de escrita no GitHub. A exceção da constituição para budgets do Copilot (Princípio II) será usada no CP-02, com o token do próprio usuário.
- A conta do GitHub é identificada pelo usuário por OAuth (PRD RG-07, CP-03), não por um cadastro de correspondência entre usuários do SIA e do GitHub, que o SIAT não tem onde guardar (Princípio I). Por isso a ligação fica no navegador da pessoa (cookie de 30 dias), e não no servidor; com a autorização já dada no GitHub, reconectar depois do prazo é só um redirecionamento.
- O membro ativo da organização é confirmado como no `gestao-copilot` (associação ativa e identidade numérica igual à da conta autenticada). O papel no GitHub (owner ou membro) não muda o que esta página mostra: a autorização é do UserManager.
- A credencial de leitura de billing (token do GitHub só com as permissões de leitura de billing, licenças, membros e times) e o segredo da aplicação OAuth ficam no `appsettings`, como o token do GitLab do catálogo (constituição 3.2.0, Princípio III).
- A aplicação OAuth do GitHub do SIAT é criada por quem a coordenação indicar (PRD, questão em aberto 2), com o endereço de retorno do SIAT; a de desenvolvimento usa o endereço local.
- O recurso `CopilotProprio` entra no UserManager pelo script do `dbchange` desta feature, e a coordenação define quais grupos o recebem (PRD, questão em aberto 4).
- O próprio consumo não é dado sensível de terceiros: as consultas não são registradas no log de consultas a dados sensíveis; registram-se a conexão, a desconexão e as recusas de conexão, que ligam uma pessoa do Tribunal a uma conta externa.
- Os prazos de cache seguem os do `gestao-copilot`, passando do SQLite para a memória (CP-04). Relatórios com mais de três dias já não mudam; na memória eles ganham um prazo longo, mas finito (por exemplo, 24 horas), definido no plano.
- O SIAT não aceita o parâmetro mensal antigo (`month=AAAA-MM`) do `gestao-copilot`: não há links antigos do SIAT a preservar, e os links do `gestao-copilot` apontam para outro endereço.
- O detalhamento por pessoa da tela de Modelos, a comparação com o período anterior, os alertas da Visão geral, a projeção e a exportação CSV não fazem parte desta página.
- A tela de exemplo desta página, desenhada no plano a partir da página de pessoa do `gestao-copilot` e do `DESIGN.md` do SIAT, é o contrato visual.
