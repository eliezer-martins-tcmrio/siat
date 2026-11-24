# Especificação: Versões Publicadas

**Branch da feature**: `features/<tarefa>_versoes_publicadas` (ou `features/versoes_publicadas` sem número de tarefa); a pasta da spec é `specs/003-versoes-publicadas`

**Criada em**: 2026-11-24

**Status**: Rascunho

**Entrada**: Descrição do usuário: "implementar o versões D:\Projetos\Geral\Ferramentas\VersaoDeSistemasPublicados.ps1"

## Contexto

O `Geral/Ferramentas/VersaoDeSistemasPublicados.ps1` é um script que cada pessoa roda na própria máquina, com as próprias credenciais. Ele lê da tabela `CSLIB_UserManager.dbo.Apps_VersionAddresses` (registros com `Ativo = 1`) onde fica o arquivo de versão de cada sistema em DSV, HML e PRD, em até duas máquinas por ambiente; lê cada arquivo (`VERSAO.TXT`, `.INI` com `VERSAO=x.y.z` ou um endereço `http`), monta uma página HTML temporária com a versão de cada máquina, destaca divergências entre as máquinas de um ambiente e erros de leitura, e apaga a página segundos depois. As leituras são feitas uma a uma, e o resultado é perdido ao fechar a página.

Esta feature traz essa consulta para o SIAT como o módulo **Versões**, no grupo **Sistemas** do menu lateral, ao lado do **Catálogo** (fase 2 do roteiro do PRD; a base e o Catálogo foram entregues nas features 001 e 002). Requisitos do PRD cobertos: VE-01 a VE-05, RG-02, RG-03, RG-04, RG-08, RG-11 a RG-14. VE-06 (unir Sistemas e Versões numa visão por sistema) fica fora. A tela de exemplo `design/versoes.html` é o contrato visual.

## Esclarecimentos

### Sessão 2026-11-24

- P: De qual banco o SIAT lê a tabela `Apps_VersionAddresses`? → R: Do `CSLIB_UserManager` do servidor escolhido pelo usuário na lista fechada de servidores, como no MailQueue e no AuditLog; o padrão é o servidor de PRD (`TCMBD`), o mesmo do script.
- P: Arquivo vazio ou sem `VERSAO=x.y.z` aparece como erro ou como no script? → R: Como no script: a célula fica sem versão, mostrada como "não configurado", e não conta como erro nem como divergência.

## Cenários de usuário e testes *(obrigatório)*

### História 1 — Saber qual versão está publicada em cada ambiente (Prioridade: P1)

Uma pessoa do suporte precisa saber se a correção do SIGARH já chegou a produção. Ela abre **Sistemas › Versões** e vê, numa tabela, a versão de cada sistema em DSV, HML e PRD, nas duas máquinas de cada ambiente. Em PRD, o SIGARH mostra versões diferentes nas duas máquinas, destacadas; ela abre a linha e vê o caminho consultado em cada máquina, que copia para conferir no Explorer.

**Por que esta prioridade**: é a pergunta que o script responde hoje ("qual versão está em PRD?"); sozinha, já permite aposentá-lo.

**Teste independente**: com um usuário que tem só `Versoes_Sistemas.CanSelect`, abrir a tela e conferir as versões exibidas com as que o script atual mostra para os mesmos sistemas.

**Cenários de aceitação**:

1. **Dado** um usuário com `Versoes_Sistemas.CanSelect`, **quando** abre o SIAT, **então** vê no menu o grupo **Sistemas** com o item **Versões**.
2. **Dado** a tela aberta sem servidor escolhido, **quando** a leitura termina, **então** os endereços vêm do servidor padrão da configuração (o de PRD nas publicações), indicado no seletor de servidor; e vê todos os sistemas ativos em ordem alfabética, cada um numa linha com seis células: Máquina 1 e Máquina 2 de DSV, de HML e de PRD, cada ambiente identificado pela sigla e pela cor.
3. **Dado** uma máquina com o arquivo de versão lido, **quando** é exibida, **então** a célula mostra o número da versão (por exemplo, `5.11.3`).
4. **Dado** um ambiente em que as duas máquinas têm versões lidas e diferentes, **quando** é exibido, **então** as duas células são destacadas como divergentes, com indicação em texto (não só pela cor) de que diferem da outra máquina.
5. **Dado** uma máquina cujo arquivo não pôde ser lido, **quando** é exibida, **então** a célula mostra "Erro", e ao abrir a linha o usuário vê a mensagem de erro e o caminho consultado.
6. **Dado** uma máquina sem endereço cadastrado, **quando** é exibida, **então** a célula mostra o marcador de "não configurado", distinguível por leitor de tela.
7. **Dado** uma linha, **quando** o usuário a abre, **então** vê, para cada máquina configurada, o ambiente, a máquina, o caminho ou endereço consultado com botão Copiar e, quando houver, a mensagem de erro.
8. **Dado** um sistema cujo caminho depende da versão do "sistema raiz", **quando** é lido, **então** o SIAT lê primeiro a versão do sistema raiz, monta o caminho com ela e mostra a versão do sistema e, ao abrir a linha, o caminho já montado.

---

### História 2 — Encontrar pendências e compartilhar o resultado (Prioridade: P2)

Antes de uma publicação, um desenvolvedor quer ver só o que está fora do lugar. No resumo do topo, ele vê quantos sistemas estão sem pendências, com divergência e com erro; clica em "Com divergência", digita "scp" na busca e copia o endereço da página para mandar a um colega, que abre o link e vê o mesmo filtro aplicado.

**Por que esta prioridade**: acelera o uso da História 1, que já entrega valor sem ela, e segue as regras de experiência de todo o SIAT.

**Teste independente**: aplicar filtros e busca, copiar a URL, abri-la com outro usuário autorizado e conferir o mesmo resultado; repetir o fluxo só com teclado.

**Cenários de aceitação**:

1. **Dado** a leitura concluída, **quando** a tela é exibida, **então** o resumo do topo mostra o total de sistemas, os sem pendências, os com divergência e os com erro.
2. **Dado** os filtros "Todos", "Com divergência" e "Com erro", cada um com a sua quantidade, **quando** o usuário escolhe um, **então** a lista mostra só os sistemas daquela situação.
3. **Dado** a busca "scp", **quando** o usuário digita, **então** a lista mostra só os sistemas cujo nome contém o texto, sem diferenciar acentos nem maiúsculas, combinada com o filtro escolhido.
4. **Dado** um servidor, uma busca e um filtro aplicados, **quando** o usuário copia a URL e outra pessoa autorizada a abre, **então** a página abre com o mesmo servidor, a mesma busca e o mesmo filtro.
5. **Dado** o seletor de servidor, **quando** o usuário escolhe outro servidor da lista (por exemplo, `TCMBD3\DEV`), **então** a tela lê os endereços cadastrados no UserManager daquele servidor e mostra as versões desses endereços, com o servidor indicado no cabeçalho e no rodapé.
6. **Dado** uma combinação de busca e filtro sem resultado, **quando** a lista fica vazia, **então** a tela diz que não há sistema naquela situação e oferece "Ver todos", que limpa a busca e o filtro.
7. **Dado** o foco fora da busca, **quando** o usuário tecla `/`, **então** o foco vai para a busca; **quando** tecla `Esc` na busca, **então** ela é limpa.
8. **Dado** a tela, **quando** navegada só por teclado e leitor de tela, **então** filtros, abertura de linha e botões de cópia são alcançáveis, com foco visível, e cada botão diz o que faz (por exemplo, "Caminhos consultados do SIGARH", "Copiar caminho de PRD, máquina 2, do SIGARH").

---

### História 3 — Resultado rápido, atual e que não trava (Prioridade: P2)

O script atual leva minutos, porque lê um arquivo de cada vez e espera cada máquina que não responde. No SIAT, quem abre a tela vê o resultado da última leitura se ela tiver poucos minutos; se não, acompanha a leitura com o tempo decorrido e pode cancelá-la. Uma máquina fora do ar vira "Erro" só na sua célula, sem atrasar as demais. Depois de uma publicação, o desenvolvedor aciona "Atualizar agora" para ver a versão nova.

**Por que esta prioridade**: é o ganho principal sobre o script ("de minutos para segundos", métrica do PRD) e a regra de resiliência do SIAT.

**Teste independente**: com uma máquina inacessível na lista, abrir a tela e conferir o tempo total e o erro só naquela célula; publicar uma versão nova em DSV, acionar "Atualizar agora" e ver a versão nova.

**Cenários de aceitação**:

1. **Dado** uma leitura feita há menos de 5 minutos, **quando** alguém abre ou recarrega a tela, **então** vê esse resultado na hora, com o horário em que foi gerado e quanto tempo levou.
2. **Dado** nenhuma leitura recente, **quando** a tela é aberta, **então** a leitura começa, a tela mostra o que está lendo (por exemplo, "Lendo 26 arquivos de versão") com o tempo decorrido e "Cancelar".
3. **Dado** uma máquina que não responde, **quando** a leitura do seu arquivo passa do tempo limite, **então** a célula mostra "Erro" com a mensagem de tempo esgotado, e as demais células são lidas normalmente, ao mesmo tempo.
4. **Dado** uma versão nova publicada, **quando** o usuário aciona "Atualizar agora", **então** a tela relê todos os arquivos e passa a mostrar a versão nova e o novo horário.
5. **Dado** uma leitura em andamento, **quando** o usuário aciona "Cancelar", **então** a tela volta ao resultado anterior, se houver, ou ao estado com "Tentar de novo", e as leituras são interrompidas quando ninguém mais espera por elas.
6. **Dado** a tabela de endereços indisponível e nenhum resultado anterior, **quando** o usuário abre a tela, **então** vê uma mensagem de erro com o motivo e o que conferir, e os demais módulos do SIAT continuam funcionando.
7. **Dado** a tabela de endereços indisponível e um resultado anterior, **quando** a releitura falha, **então** a tela continua mostrando o resultado anterior, com aviso de que a atualização falhou e do horário dos dados exibidos.

---

### História 4 — Acesso controlado às versões (Prioridade: P3)

Só quem tem o recurso de versões vê o item no menu e os dados; quem não tem e abre um link compartilhado recebe a orientação de a quem pedir acesso.

**Por que esta prioridade**: a regra de acesso é a mesma da base do SIAT; não muda o uso das demais histórias.

**Teste independente**: com um usuário sem `Versoes_Sistemas.CanSelect`, conferir a ausência do item no menu e a negação ao abrir a URL direta da tela.

**Cenários de aceitação**:

1. **Dado** um usuário sem `Versoes_Sistemas.CanSelect`, **quando** abre o SIAT, **então** não vê o item **Versões**, e o grupo **Sistemas** some quando não houver nenhum item dele visível.
2. **Dado** um usuário sem `Versoes_Sistemas.CanSelect`, **quando** abre a URL da tela de Versões (com ou sem filtros), **então** não vê nenhuma versão nem caminho, e vê a mensagem de acesso negado indicando o recurso `Versoes_Sistemas.CanSelect`; nenhuma leitura de arquivo é feita por causa dele.
3. **Dado** um usuário com `Versoes_Sistemas.CanSelect` e sem `Catalogo_Sistemas.CanSelect`, **quando** entra no SIAT pela página inicial, **então** é levado à tela de Versões.

---

### Casos extremos

- Arquivo `.INI` ou texto com mais de 12 caracteres: a versão é o primeiro `VERSAO=x.y.z` encontrado; se não houver, a célula fica sem versão e mostra "não configurado", como no script.
- Arquivo curto (até 12 caracteres, como `VERSAO.TXT` com só `1.4.2`): o conteúdo, sem espaços e quebras de linha nas pontas, é a versão.
- Endereço `http`/`https`: o conteúdo da resposta é a versão, com as mesmas regras de arquivo curto e de `VERSAO=`; resposta de erro do servidor (4xx, 5xx) é erro, com o código.
- Arquivo vazio ou só com espaços: a célula mostra "não configurado", como no script; não conta como erro nem como divergência.
- Sistema raiz sem `VERSAO=`: o caminho do sistema é montado sem a versão, como no script, e lido normalmente (em geral resulta em erro de caminho não encontrado, com o caminho montado). Sistema raiz ilegível (o script para por inteiro nesse caso): erro só naquela célula, dizendo que a versão do sistema raiz não foi obtida, com o caminho do sistema raiz.
- Mesmo arquivo (ou mesmo sistema raiz) usado por várias células: é lido uma vez por leitura.
- Duas máquinas com a mesma versão escrita de formas diferentes (`1.4.2` e `1.4.2 ` ou com quebra de linha): não é divergência, porque os espaços nas pontas são ignorados.
- Uma máquina com erro e a outra com versão: não é divergência; o sistema conta como "com erro". Um sistema pode estar ao mesmo tempo com divergência e com erro (em ambientes diferentes) e conta nas duas situações.
- Uma só máquina configurada no ambiente: não há divergência possível naquele ambiente.
- Sistema ativo sem nenhum endereço: aparece com as seis células "não configurado" e conta como sem pendências.
- Nenhum sistema ativo na tabela: a tela diz que não há sistema cadastrado e onde cadastrar, em vez de erro.
- Nome de sistema repetido na tabela: as linhas aparecem separadas, cada uma com seus endereços.
- Endereço com caractere inválido para caminho, caminho local ou com letra de unidade (`C:\...`, `G:\...`), caminho relativo ou esquema que não seja caminho de rede ou `http`/`https`: erro "Endereço não suportado" naquela célula, sem tentar ler. O SIAT lê com a conta de serviço para todos, e não pode ler arquivos do próprio servidor indicados pela tabela.
- Mensagem de erro e caminho longos: quebram após separadores (`\`, `/`, `.`, `_`, `-`); a tabela rola na horizontal em telas estreitas, com cabeçalho fixo.
- Duas pessoas acionam "Atualizar agora" ao mesmo tempo: a leitura é feita uma vez, e as duas veem o mesmo resultado.
- Filtro inválido na URL (`?filtro=xyz`): a tela abre com "Todos".
- Servidor desconhecido na URL, ou fora da lista: a tela abre com o servidor padrão e avisa que o servidor pedido não está na lista; não existe campo de servidor livre.
- O usuário perde o recurso durante a sessão: a próxima leitura ou atualização é negada.

## Requisitos *(obrigatório)*

### Requisitos funcionais

**Acesso e navegação**

- **FR-001**: O SIAT DEVE usar o recurso `Versoes_Sistemas.CanSelect` da sua aplicação no UserManager; ele libera as versões de todos os ambientes.
- **FR-002**: O menu lateral DEVE ter, no grupo **Sistemas**, depois de **Catálogo**, o item **Versões**, visível só para quem tem `Versoes_Sistemas.CanSelect`; o grupo some quando não houver item visível.
- **FR-003**: A tela e toda leitura de versões DEVEM ser autorizadas no servidor por `Versoes_Sistemas.CanSelect`; sem o recurso, nenhuma versão, caminho ou mensagem de erro é enviada ao navegador, e nenhuma leitura é disparada.
- **FR-004**: A página inicial DEVE levar à primeira ferramenta permitida na ordem do menu (Catálogo, Versões, depois Consultas), e a mensagem de "sem acesso" DEVE citar também as versões.

**Origem dos endereços e leitura das versões**

- **FR-004a**: A tela DEVE ter o seletor de servidor do SIAT, com a mesma lista fechada de servidores do MailQueue e do AuditLog (cada um com seu ambiente); sem escolha, o servidor é o padrão da configuração da aplicação, que nas publicações é o de PRD. O servidor escolhido é o de onde vêm os endereços; os arquivos lidos são os que os endereços apontam, qualquer que seja o servidor.
- **FR-005**: Os sistemas e seus endereços DEVEM ser lidos de `CSLIB_UserManager.dbo.Apps_VersionAddresses` do servidor escolhido, só os registros ativos, com as colunas que o script usa hoje (nome do sistema; endereço e endereço do sistema raiz de DSV, HML e PRD, para a máquina 1 e para a máquina 2). Incluir, alterar ou desativar um sistema continua sendo alterar essa tabela.
- **FR-006**: Cada endereço DEVE ser lido como o script lê: caminho de rede (`\\servidor\compartilhamento\...`) ou endereço `http`/`https`; conteúdo com mais de 12 caracteres tem a versão extraída do primeiro `VERSAO=x.y.z`; conteúdo menor é a própria versão, sem espaços nas pontas.
- **FR-007**: Quando a célula tem endereço do sistema raiz, a versão do sistema raiz DEVE ser lida primeiro (pela regra de `VERSAO=`) e colocada no lugar indicado no endereço da célula, antes de ler a versão do sistema.
- **FR-008**: Cada leitura de arquivo ou endereço DEVE ter tempo limite próprio de 5 segundos; tempo esgotado, arquivo inexistente, acesso negado, resposta de erro do endereço `http` e sistema raiz ilegível DEVEM virar erro só daquela célula, com mensagem em português. Conteúdo vazio ou sem `VERSAO=` (no arquivo com mais de 12 caracteres) NÃO é erro: a célula fica sem versão, como no script.
- **FR-009**: As leituras de arquivos DEVEM ser feitas em paralelo, com limite de leituras simultâneas, e cada arquivo DEVE ser lido no máximo uma vez por leitura geral.
- **FR-010**: A leitura dos arquivos e da tabela DEVE usar a conta de serviço somente de leitura do SIAT, não as credenciais do usuário, e o SIAT NÃO DEVE gravar nada nos locais lidos.
- **FR-011**: O resultado DEVE ficar em cache em memória por 5 minutos, separado por servidor; quem abre a tela nesse prazo vê o resultado guardado; "Atualizar agora" DEVE refazer a leitura na hora. A tela já aberta não precisa se atualizar sozinha.

**Exibição**

- **FR-012**: A tela DEVE listar os sistemas em ordem alfabética do nome (regras do português), com a coluna Sistema e, para DSV, HML e PRD, as colunas Máquina 1 e Máquina 2; cada ambiente identificado pela sigla e pela cor, nunca só pela cor.
- **FR-013**: Cada célula DEVE mostrar uma de quatro situações, distinguíveis por texto e não só por cor: versão lida, versão divergente (as duas máquinas do ambiente têm versões lidas e diferentes, comparadas sem espaços nas pontas), "Erro" ou "não configurado".
- **FR-014**: Cada linha DEVE poder ser aberta para mostrar, por máquina configurada, o ambiente, a máquina, o caminho ou endereço efetivamente consultado (com o sistema raiz já aplicado), um botão Copiar desse valor e, quando houver, a mensagem de erro.
- **FR-015**: O topo DEVE mostrar o resumo: total de sistemas, sem pendências (nenhuma divergência e nenhum erro), com divergência e com erro.
- **FR-016**: A tela DEVE oferecer os filtros "Todos", "Com divergência" e "Com erro", cada um com a sua quantidade, e uma busca pelo nome do sistema, sem diferenciar acentos nem maiúsculas, que filtra enquanto o usuário digita e se combina com o filtro.
- **FR-017**: Servidor, busca e filtro DEVEM ficar na URL (o servidor padrão pode ficar fora), e abrir a URL DEVE restaurá-los; valores desconhecidos na URL são ignorados.
- **FR-018**: Busca e filtro sem resultado DEVEM mostrar o estado vazio, com o motivo e a ação "Ver todos".
- **FR-019**: O atalho `/` DEVE levar o foco à busca, e `Esc` na busca DEVE limpá-la.
- **FR-020**: A tela DEVE mostrar o horário em que o resultado foi gerado e quanto tempo a leitura levou, e uma legenda das quatro situações de célula; o rodapé DEVE indicar a origem dos endereços (servidor escolhido e `CSLIB_UserManager.dbo.Apps_VersionAddresses`, registros ativos) e o tempo de cache.
- **FR-021**: Todo texto vindo da tabela, dos arquivos e das mensagens de erro DEVE ser exibido como texto, sem interpretar marcação.

**Andamento e falhas**

- **FR-022**: Durante a leitura, a tela DEVE mostrar o que está lendo (o servidor, enquanto lê a tabela; depois, a quantidade de arquivos), o tempo decorrido e "Cancelar"; cancelar DEVE devolver a tela ao resultado anterior (ou ao estado com "Tentar de novo") e interromper as leituras quando nenhuma outra pessoa estiver esperando por elas (a abertura de um arquivo de rede já em curso não pode ser interrompida pelo Windows: ela é abandonada e termina sozinha, sem efeito no resultado). Cancelamento não conta como falha.
- **FR-023**: A consulta à tabela de endereços DEVE ter tempo limite; falha sem resultado anterior DEVE mostrar o estado de erro com o motivo e o que conferir; com resultado anterior, a tela DEVE continuar com ele e mostrar um aviso da falha.
- **FR-024**: A falha da leitura de versões NÃO DEVE afetar os outros módulos do SIAT.

**Apresentação**

- **FR-025**: A tela DEVE seguir o contrato visual `design/versoes.html` e os componentes do SIAT (cabeçalho de página, seletor de servidor, resumo, filtro segmentado, tabela com cabeçalho fixo e rolagem horizontal, linha expansível, estados vazio/erro/carregando, botão Copiar), nos temas claro, escuro e do sistema.
- **FR-026**: A tela DEVE atender à WCAG 2.2 AA e ser usável só por teclado, com foco visível; os botões de abrir linha e de copiar DEVEM dizer o que fazem, incluindo sistema, ambiente e máquina.
- **FR-027**: Todos os textos DEVEM estar em português do Brasil.

### Entidades-chave

- **Endereço de versão**: registro ativo de `Apps_VersionAddresses`; tem o nome do sistema e, para cada ambiente (DSV, HML, PRD) e máquina (1 e 2), um endereço do arquivo de versão e, opcionalmente, um endereço do arquivo do sistema raiz.
- **Ambiente**: DSV, HML ou PRD; lista fixa, com sigla e cor.
- **Leitura de célula**: o resultado de uma máquina num ambiente; tem a situação (versão, erro ou não configurado), a versão, o caminho efetivamente consultado e a mensagem de erro.
- **Situação do sistema**: derivada das seis células; sem pendências, com divergência (em algum ambiente) e/ou com erro (em alguma célula).
- **Resultado da leitura**: o conjunto das linhas num momento, para um servidor; tem servidor, horário de geração, duração, totais do resumo e, quando a última tentativa falhou, o motivo.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: Uma pessoa do suporte responde "qual versão do sistema X está em PRD?" em menos de 15 segundos a partir da abertura da tela, quando há resultado em cache, contra minutos com o script.
- **SC-002**: Uma leitura completa de todos os sistemas ativos termina em até 30 segundos, mesmo com todas as máquinas fora do ar, porque nenhum arquivo espera mais que 5 segundos e as leituras correm em paralelo; com uma máquina ou compartilhamento fora do ar, o esperado é cerca de 10 segundos.
- **SC-003**: Para 100% dos sistemas ativos, versão, divergência, erro e "não configurado" exibidos pelo SIAT coincidem com os do script atual rodado no mesmo momento, verificado célula a célula (exceto o sistema raiz ilegível, em que o script para por inteiro e o SIAT mostra erro só na célula).
- **SC-004**: A tela abre em até 2 segundos: com resultado em cache, mostra a tabela nesse prazo; sem ele, mostra nesse prazo o andamento da leitura com o tempo decorrido.
- **SC-005**: Uma versão publicada aparece no SIAT em no máximo 5 minutos, ou na hora com "Atualizar agora".
- **SC-006**: Zero versões, caminhos ou mensagens de erro entregues a usuários sem `Versoes_Sistemas.CanSelect`, verificado por teste de acesso direto pela URL.
- **SC-007**: Todo fluxo das Histórias 1 e 2 é concluído só com teclado, e a tela não tem falhas WCAG 2.2 AA na verificação do roteiro de validação.
- **SC-008**: Três meses após a entrega, ninguém precisa rodar o `VersaoDeSistemasPublicados.ps1`, e ele pode ser aposentado.

## Premissas

- O SIAT só lê: nem a tabela `Apps_VersionAddresses` nem os arquivos de versão são alterados por ele. Manter a tabela continua sendo feito como hoje, fora do SIAT.
- A tabela é lida no `CSLIB_UserManager` do servidor escolhido na lista fechada (`servidores.json`, a mesma das Consultas), com a conta de serviço somente de leitura; o padrão é o servidor de PRD, como no script (`TCMBD`). Em DSV e HML, a tabela pode ter sistemas e endereços diferentes dos de PRD. O servidor padrão fica na configuração da aplicação: o de PRD nas publicações; no desenvolvimento local, o de DSV, para não consultar produção.
- A conta de serviço do SIAT recebe leitura em todos os compartilhamentos e máquinas citados na tabela; onde não tiver, a célula mostra "acesso negado", e a correção é dar a permissão, não mudar o SIAT.
- Versões publicadas e caminhos de rede não são dados sensíveis de negócio; por isso um único recurso libera todos os ambientes (PRD RG-02) e a leitura não é registrada no log de consultas a dados sensíveis.
- O marcador do sistema raiz no endereço (`{0}`) e o formato `VERSAO=x.y.z` continuam como o script usa hoje.
- O tempo limite de 5 s por arquivo e o cache de 5 minutos são os valores da tela de exemplo; ficam na configuração da aplicação para ajuste sem nova publicação.
- O script continua disponível até o módulo ser validado pelos usuários (Princípio IV).
- Unir Catálogo e Versões numa visão por sistema (VE-06) e ligar versão a build do Jenkins (BU-02) ficam para fases seguintes.
