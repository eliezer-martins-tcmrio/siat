# Especificação: Ficha do Sistema

**Branch da feature**: `features/<tarefa>_ficha_sistema` (ou `features/ficha_sistema` sem número de tarefa); a pasta da spec é `specs/004-ficha-sistema`

**Criada em**: 2026-11-25

**Status**: Rascunho

**Entrada**: Descrição do usuário: "VE-06 do @PRD.md"

## Contexto

Hoje, para responder "onde roda este sistema e qual versão está em produção?", a pessoa abre duas telas do SIAT: **Sistemas › Catálogo** (feature 002), que mostra URL, máquina e caminho de cada ambiente a partir do `sistemasDb.js` do TCMRJDoc no GitLab, e **Sistemas › Versões** (feature 003), que mostra a versão publicada em cada máquina a partir de `CSLIB_UserManager.dbo.Apps_VersionAddresses`. As duas fontes não têm ligação entre si: os nomes nem sempre coincidem, e um item do catálogo pode corresponder a vários registros da tabela (o sistema e seus componentes ou serviços).

Esta feature cria a **ficha do sistema**: uma página por item do catálogo que reúne, por ambiente, as instâncias do catálogo e a versão publicada em cada máquina, com divergência e erro destacados como na tela de Versões. O vínculo entre as duas fontes é explícito, escrito no próprio catálogo. Requisitos do PRD cobertos: VE-06 (VE-06.1 a VE-06.5), RG-03, RG-08, RG-11 a RG-14. A ligação com os builds do Jenkins (BU-02) fica para a fase 7.

## Esclarecimentos

### Sessão 2026-11-25

- P: Como a ficha organiza o catálogo e as versões? → R: Por ambiente: uma seção por ambiente (DSV, HML, PRD, Contingência), cada uma com as instâncias do catálogo e, logo abaixo, a versão de cada máquina de cada nome vinculado. Descartadas a ficha em dois blocos (catálogo e depois uma tabela igual à de Versões) e a tabela única com uma coluna por ambiente.
- P: Como funciona o filtro "Sem ficha" na tela de Versões? → R: É a quarta opção do filtro que já existe (Todos, Com divergência, Com erro, Sem ficha), escolhida uma por vez e combinada só com a busca. Descartados um marcador separado, combinável com as outras opções, e só uma contagem no resumo.
- P: A busca do Catálogo encontra um sistema também pelos nomes do campo `Versoes`? → R: Não. A busca continua só nos campos que a tela de Catálogo mostra, como no `sistemas.html`; quem conhece o nome de um componente o encontra em Versões, e o nome ali leva à ficha.

## Cenários de usuário e testes *(obrigatório)*

### História 1 — Ver num só lugar onde o sistema roda e o que está publicado (Prioridade: P1)

Uma pessoa do suporte recebe um chamado sobre o SIGARH em produção. Ela abre **Sistemas › Catálogo**, busca "sigarh" e aciona o nome do sistema. A ficha do SIGARH abre com uma seção por ambiente: em cada uma, a URL, a máquina e o caminho do catálogo e, logo abaixo, a versão publicada em cada máquina do SIGARH e dos componentes que o catálogo liga a ele. Em PRD, as duas máquinas mostram versões diferentes, destacadas; ela copia o caminho consultado para conferir no Explorer, sem ter mudado de tela.

**Por que esta prioridade**: une as duas consultas mais frequentes do suporte numa só página; sozinha, entrega o valor do VE-06.

**Teste independente**: com um usuário que tem `Catalogo_Sistemas.CanSelect` e `Versoes_Sistemas.CanSelect` e um item do catálogo com vínculo preenchido, abrir a ficha pelo Catálogo e conferir as instâncias com a tela de Catálogo e as versões com a tela de Versões, no mesmo servidor e no mesmo momento.

**Cenários de aceitação**:

1. **Dado** um usuário com os dois recursos, **quando** aciona o nome de um sistema na tela de Catálogo, **então** abre a ficha daquele sistema, com o nome, a observação e o caminho de navegação **Sistemas › Catálogo › nome do sistema**.
2. **Dado** a ficha aberta, **quando** é exibida, **então** há uma seção por ambiente, na ordem DSV, HML, PRD e Contingência, cada uma identificada pela sigla e pela cor, com as instâncias do catálogo (URL, máquina e caminho) apresentadas pelas mesmas regras da tela de Catálogo.
3. **Dado** um item do catálogo vinculado a dois nomes da tabela de endereços (o sistema e um serviço dele), **quando** a ficha é exibida, **então** as seções de DSV, HML e PRD mostram, para cada nome vinculado, na ordem em que aparecem no vínculo, a Máquina 1 e a Máquina 2 com a mesma situação da tela de Versões: versão lida, versão divergente, "Erro" ou "não configurado".
4. **Dado** um nome vinculado cujas duas máquinas de um ambiente têm versões lidas e diferentes, **quando** a seção é exibida, **então** as duas são destacadas como divergentes, com indicação em texto e não só pela cor.
5. **Dado** uma máquina com versão lida, com erro ou com caminho dependente do sistema raiz, **quando** a ficha é exibida, **então** o usuário vê o caminho ou endereço efetivamente consultado, com botão Copiar, e a mensagem de erro, quando houver.
6. **Dado** a seção de Contingência, **quando** é exibida, **então** mostra só as instâncias do catálogo, com uma nota de que não há endereços de versão para esse ambiente.
7. **Dado** a ficha com versões, **quando** é exibida, **então** o topo resume a situação das versões do sistema (sem pendências, com divergência, com erro) considerando todos os nomes vinculados, e mostra o servidor de onde vieram os endereços, o horário em que as versões foram geradas e quanto tempo a leitura levou.

---

### História 2 — Chegar à ficha pela tela de Versões e compartilhar (Prioridade: P2)

Um desenvolvedor está em **Sistemas › Versões** com o servidor de DSV escolhido, vê um erro no SIGARH e aciona o nome do sistema. A ficha abre já com o servidor de DSV, mostrando junto as instâncias do catálogo, o que o ajuda a achar a máquina com problema. Ele copia o endereço da ficha e manda a um colega, que a abre com o mesmo sistema e o mesmo servidor.

**Por que esta prioridade**: a ficha já entrega valor aberta pelo Catálogo; chegar por Versões e compartilhar o link completam o uso no dia a dia.

**Teste independente**: na tela de Versões, com um servidor que não seja o padrão, acionar o nome de um sistema vinculado, conferir o servidor na ficha, copiar a URL e abri-la com outro usuário autorizado.

**Cenários de aceitação**:

1. **Dado** a tela de Versões com um servidor escolhido, **quando** o usuário aciona o nome de um sistema vinculado a um item do catálogo, **então** abre a ficha desse item com o mesmo servidor.
2. **Dado** a ficha aberta pelo Catálogo, sem servidor no endereço, **quando** as versões são exibidas, **então** vêm do servidor padrão da configuração (o de PRD nas publicações), indicado no seletor de servidor.
3. **Dado** a ficha aberta, **quando** o usuário escolhe outro servidor da lista fechada, **então** as versões passam a vir dos endereços cadastrados naquele servidor, e o endereço da página muda para incluí-lo.
4. **Dado** uma ficha com um servidor escolhido, **quando** o usuário copia o endereço da página e outra pessoa autorizada o abre, **então** a ficha abre com o mesmo sistema e o mesmo servidor.
5. **Dado** um sistema da tela de Versões que nenhum item do catálogo vincula, **quando** é exibido, **então** o nome aparece sem link; e o filtro **Sem ficha**, com a sua quantidade, mostra só os sistemas nessa situação, combinado com a busca.
6. **Dado** as telas de Catálogo, Versões e a ficha, **quando** navegadas só por teclado e leitor de tela, **então** os links de ficha, o seletor de servidor e os botões de cópia são alcançáveis, com foco visível, e cada um diz o que faz (por exemplo, "Ficha do SIGARH", "Copiar caminho de PRD, máquina 2, do SIGARH Agendador").

---

### História 3 — Vínculo explícito e avisos de vínculo quebrado (Prioridade: P2)

Quem mantém o catálogo liga cada item aos nomes da tabela de endereços escrevendo esses nomes no próprio `sistemasDb.js`. A ficha avisa quando um nome vinculado não existe no servidor escolhido, e a tela de Versões mostra os sistemas que ainda não têm ficha, para que o catálogo seja completado.

**Por que esta prioridade**: sem vínculo a ficha mostra só o catálogo; os avisos tornam o vínculo verificável e corrigível, mas a História 1 já funciona com os itens vinculados.

**Teste independente**: vincular a um item um nome existente, um nome escrito com maiúsculas e acentos diferentes e um nome inexistente; conferir a ficha em PRD e em DSV e o filtro **Sem ficha** na tela de Versões.

**Cenários de aceitação**:

1. **Dado** um item do catálogo sem vínculo, **quando** um usuário com `Versoes_Sistemas.CanSelect` abre a ficha, **então** vê o catálogo e uma nota de que o sistema não tem versões vinculadas e de como vinculá-lo, com link para o arquivo do catálogo no GitLab.
2. **Dado** um nome vinculado escrito com acentos, maiúsculas ou espaços nas pontas diferentes do nome na tabela, **quando** a ficha é exibida, **então** o vínculo é feito normalmente.
3. **Dado** um nome vinculado que não existe nos registros ativos da tabela do servidor escolhido, **quando** a ficha é exibida, **então** ela mostra um aviso com o nome e o servidor, e os demais nomes vinculados aparecem normalmente.
4. **Dado** um item com o vínculo escrito em formato inválido, **quando** a ficha é exibida, **então** ela mostra o catálogo normalmente e um aviso de que o vínculo é inválido; a tela de Catálogo continua igual.
5. **Dado** o catálogo com vínculos preenchidos, **quando** a tela de Catálogo é exibida ou buscada, **então** mostra e encontra exatamente o mesmo que antes do vínculo.

---

### História 4 — Ficha rápida, atual e que não trava (Prioridade: P2)

Quem abre a ficha vê o catálogo em até 2 segundos, sem esperar as versões. Se a tela de Versões acabou de ser lida, as versões aparecem nesse prazo também; se não, a ficha lê só os arquivos daquele sistema, com o tempo decorrido e a opção de cancelar. Depois de uma publicação, o desenvolvedor aciona "Atualizar agora" na ficha para ver a versão nova sem reler todos os sistemas.

**Por que esta prioridade**: é a regra de desempenho e resiliência de todo o SIAT; não muda o conteúdo das demais histórias.

**Teste independente**: abrir a ficha logo após a tela de Versões e conferir que não houve nova leitura; esperar o cache vencer, abrir a ficha de um sistema com uma máquina inacessível e conferir o tempo e o erro só naquela célula; publicar uma versão em DSV e acionar "Atualizar agora".

**Cenários de aceitação**:

1. **Dado** o catálogo já lido, **quando** a ficha é aberta, **então** a parte do catálogo aparece em até 2 segundos, sem esperar as versões.
2. **Dado** um resultado da tela de Versões ainda válido para o servidor escolhido, **quando** a ficha é aberta, **então** as versões aparecem em até 2 segundos (SC-002), com o horário em que foram geradas, sem nova leitura.
3. **Dado** nenhum resultado válido, **quando** a ficha é aberta, **então** ela lê só os endereços dos nomes vinculados e só os arquivos desses endereços, mostrando o que está lendo (por exemplo, "Lendo 4 arquivos de versão"), o tempo decorrido e "Cancelar", enquanto a parte do catálogo continua utilizável.
4. **Dado** uma máquina que não responde, **quando** a leitura do seu arquivo passa do tempo limite, **então** só a célula dela mostra "Erro" com a mensagem de tempo esgotado, e as demais são lidas ao mesmo tempo.
5. **Dado** uma leitura de versões em andamento, **quando** o usuário aciona "Cancelar", **então** a seção de versões volta ao resultado anterior, se houver, ou ao estado com "Tentar de novo", e as leituras são interrompidas quando ninguém mais espera por elas.
6. **Dado** uma versão nova publicada, **quando** o usuário aciona "Atualizar agora" na ficha, **então** a ficha relê o catálogo e as versões só daquele sistema no servidor escolhido, e passa a mostrar a versão nova e o novo horário.
7. **Dado** a tabela de endereços indisponível, **quando** a ficha lê as versões, **então** a parte do catálogo continua exibida; sem resultado anterior, a seção de versões mostra o erro com o motivo e "Tentar de novo"; com resultado anterior, continua com ele e avisa da falha e do horário dos dados exibidos.
8. **Dado** o catálogo indisponível e nenhuma leitura anterior válida, **quando** o usuário abre a ficha, **então** vê o estado de erro com o motivo e o que conferir, como na tela de Catálogo, e os demais módulos do SIAT continuam funcionando.

---

### História 5 — Acesso controlado à ficha (Prioridade: P3)

A ficha segue os recursos já existentes: o catálogo exige o recurso do catálogo, e as versões, o de versões. Quem tem só um deles vê só a parte que lhe cabe.

**Por que esta prioridade**: a regra de acesso é a mesma da base do SIAT; não muda o uso das demais histórias.

**Teste independente**: abrir a URL direta de uma ficha, com e sem servidor no endereço, com um usuário sem nenhum dos recursos, com só o do catálogo e com só o de versões.

**Cenários de aceitação**:

1. **Dado** um usuário sem `Catalogo_Sistemas.CanSelect`, **quando** abre a URL de uma ficha, **então** não vê nenhum dado do catálogo nem de versões, vê a mensagem de acesso negado indicando o recurso `Catalogo_Sistemas.CanSelect`, e nenhuma leitura é feita por causa dele.
2. **Dado** um usuário com `Catalogo_Sistemas.CanSelect` e sem `Versoes_Sistemas.CanSelect`, **quando** abre uma ficha (com ou sem servidor no endereço), **então** vê só o catálogo: nem seletor de servidor, nem versões, caminhos consultados, avisos de vínculo ou nota de como vincular; e nenhuma leitura de versões é feita por causa dele.
3. **Dado** um usuário com `Versoes_Sistemas.CanSelect` e sem `Catalogo_Sistemas.CanSelect`, **quando** abre a tela de Versões, **então** os nomes dos sistemas aparecem sem link, o filtro **Sem ficha** não existe, e o catálogo não é lido por causa dele.
4. **Dado** qualquer usuário, **quando** abre o SIAT, **então** o menu não ganha item novo: a ficha só é aberta pelos nomes dos sistemas no Catálogo e em Versões, ou por um link compartilhado.

---

### Casos extremos

- Sistema não encontrado no catálogo (item renomeado, removido ou link antigo): a ficha diz que o sistema não foi encontrado e oferece abrir o Catálogo com o nome na busca.
- Nome de sistema com espaços, acentos ou caracteres que têm significado num endereço (`/`, `?`, `#`, `&`, `%`): o link abre a ficha certa, e a URL compartilhada também.
- Dois itens do catálogo com o mesmo nome (ignorando acentos, maiúsculas e espaços nas pontas): a ficha mostra o primeiro na ordem do arquivo e avisa que há outro item com o mesmo nome; a correção é renomear na origem. O catálogo atual não tem nomes repetidos.
- O mesmo nome da tabela vinculado por dois itens do catálogo (um componente compartilhado): aparece nas duas fichas; na tela de Versões, o nome leva à ficha do primeiro desses itens, em ordem alfabética.
- Nome repetido dentro do vínculo de um item: é considerado uma vez. Nome vazio ou só com espaços no vínculo: é ignorado. Vínculo presente, mas sem nenhum nome válido: tratado como item sem vínculo.
- Nome vinculado que corresponde a mais de um registro ativo da tabela (nome repetido na tabela): cada registro aparece separado, como na tela de Versões.
- Nome vinculado que existe em PRD e não em DSV (ou o contrário): o aviso aparece só quando o servidor escolhido não o tem.
- Todos os nomes vinculados inexistentes no servidor escolhido: a ficha mostra os avisos e nenhuma versão; não é erro de leitura e não conta como "com erro" no resumo.
- Caminho que depende da versão do sistema raiz, arquivo vazio, conteúdo sem `VERSAO=`, endereço não suportado: mesmas regras e mesmas mensagens da tela de Versões.
- Servidor desconhecido no endereço, ou fora da lista fechada: a ficha abre com o servidor padrão e avisa que o servidor pedido não está na lista; não existe campo de servidor livre.
- Servidor no endereço da ficha aberta por um usuário sem `Versoes_Sistemas.CanSelect`: o parâmetro é ignorado, sem aviso.
- Resultado da tela de Versões mais antigo que uma leitura feita pela ficha (ou o contrário): a ficha usa o mais recente ainda válido; cada tela mostra o horário dos dados que exibe, e a leitura da ficha não altera o resultado mostrado na tela de Versões.
- Catálogo indisponível quando a tela de Versões é aberta: Versões funciona normalmente, com os nomes sem link, um aviso de que as fichas estão indisponíveis e sem o filtro **Sem ficha**.
- Duas pessoas abrem, sem resultado válido, a ficha do mesmo sistema no mesmo servidor ao mesmo tempo: a leitura é feita uma vez, e as duas veem o mesmo resultado.
- Filtro **Sem ficha** no endereço da tela de Versões aberta por quem não tem `Catalogo_Sistemas.CanSelect`: a tela abre com "Todos".
- O usuário perde um dos recursos durante a sessão: a próxima leitura ou atualização da parte correspondente é negada.

## Requisitos *(obrigatório)*

### Requisitos funcionais

**Acesso e navegação**

- **FR-001**: A ficha DEVE exigir `Catalogo_Sistemas.CanSelect`, verificado no servidor; sem ele, nenhum dado do catálogo nem de versões é enviado ao navegador, nenhuma leitura é feita, e a tela mostra a mensagem de acesso negado indicando o recurso.
- **FR-002**: A parte de versões da ficha (seletor de servidor, versões, caminhos consultados, mensagens de erro, resumo das versões, avisos de vínculo e nota de como vincular) DEVE exigir também `Versoes_Sistemas.CanSelect`, verificado no servidor; sem ele, a ficha mostra só o catálogo, e nenhuma leitura de versões é feita por causa dele.
- **FR-003**: A ficha NÃO DEVE ter item próprio no menu; DEVE mostrar o caminho de navegação **Sistemas › Catálogo › nome do sistema**, com volta à tela de Catálogo, e o menu DEVE indicar o item **Catálogo** como o atual.
- **FR-004**: Na tela de Catálogo, o nome de cada sistema DEVE ser um link para a sua ficha.
- **FR-005**: Na tela de Versões, para quem tem também `Catalogo_Sistemas.CanSelect`, o nome de cada sistema vinculado a um item do catálogo DEVE ser um link para a ficha desse item, levando o servidor escolhido; sistema sem item vinculado DEVE aparecer sem link. Para quem não tem `Catalogo_Sistemas.CanSelect`, os nomes DEVEM ficar sem link e o catálogo NÃO DEVE ser lido por causa dele.
- **FR-006**: O filtro da tela de Versões DEVE ganhar a quarta opção **Sem ficha**, depois de "Todos", "Com divergência" e "Com erro", com a sua quantidade, só para quem tem `Catalogo_Sistemas.CanSelect`. Como as demais, é escolhida uma por vez: mostra só os sistemas sem item do catálogo vinculado, qualquer que seja a situação das versões, combina-se só com a busca e fica no endereço da página como os demais filtros.

**Vínculo entre catálogo e versões**

- **FR-007**: Cada item do `sistemasDb.js` PODE ter o campo `Versoes`, uma lista de textos com os nomes de `Apps_VersionAddresses.Sistema` que correspondem a ele. O SIAT NÃO DEVE deduzir vínculo de nenhuma outra forma, nem por semelhança de nomes.
- **FR-008**: A comparação entre um nome de `Versoes` e o nome na tabela DEVE ignorar acentos, maiúsculas e espaços nas pontas.
- **FR-009**: Nomes vazios em `Versoes` DEVEM ser ignorados, e nomes repetidos, considerados uma vez; a ficha DEVE exibir os nomes vinculados na ordem do campo.
- **FR-010**: Um campo `Versoes` em formato inválido DEVE ser ignorado, com aviso na ficha para quem tem `Versoes_Sistemas.CanSelect`; o item NÃO DEVE sair do catálogo por isso.
- **FR-011**: O campo `Versoes` NÃO DEVE mudar o que a tela de Catálogo mostra nem o que a busca do Catálogo encontra: a busca NÃO DEVE procurar nos nomes vinculados.

**Conteúdo da ficha**

- **FR-012**: A ficha DEVE identificar o sistema pelo nome do item do catálogo, comparado sem diferenciar acentos, maiúsculas e espaços nas pontas, e mostrar o nome e a observação pelas regras de exibição da tela de Catálogo.
- **FR-013**: A ficha DEVE ter uma seção por ambiente, na ordem DSV, HML, PRD e Contingência, cada uma identificada pela sigla e pela cor, nunca só pela cor, com as instâncias do catálogo pelas mesmas regras da tela de Catálogo (rótulos de URL, máquina e caminho; links em nova aba; vários endereços separados por `|`; várias máquinas copiáveis uma a uma; botão Copiar; marcador de "não configurado").
- **FR-014**: Nas seções de DSV, HML e PRD, para quem tem `Versoes_Sistemas.CanSelect`, a ficha DEVE mostrar, logo abaixo das instâncias do catálogo daquele ambiente, para cada registro ativo da tabela correspondente a um nome vinculado, a Máquina 1 e a Máquina 2, cada uma com a situação (versão lida, versão divergente, "Erro" ou "não configurado") calculada pelas mesmas regras da tela de Versões, o caminho ou endereço efetivamente consultado com botão Copiar e, quando houver, a mensagem de erro. A seção de Contingência DEVE mostrar só o catálogo, com uma nota de que não há endereços de versão para esse ambiente.
- **FR-015**: O topo da ficha DEVE resumir a situação das versões do sistema, considerando todos os nomes vinculados: sem pendências, com divergência e com erro (as duas últimas podem valer ao mesmo tempo), pelas mesmas regras da tela de Versões.
- **FR-016**: A ficha DEVE mostrar o servidor de onde vieram os endereços, o horário em que as versões exibidas foram geradas, quanto tempo a leitura levou e a legenda das quatro situações de célula.
- **FR-017**: Cada nome vinculado sem registro ativo na tabela do servidor escolhido DEVE aparecer como aviso na ficha, com o nome e o servidor; isso não é erro de leitura e não conta no resumo.
- **FR-018**: Sem vínculo (campo ausente, vazio ou sem nome válido), a ficha DEVE mostrar só o catálogo e, para quem tem `Versoes_Sistemas.CanSelect`, uma nota de que o sistema não tem versões vinculadas e de como vinculá-lo, com link para o arquivo do catálogo no GitLab.
- **FR-019**: Quando o nome pedido não existe no catálogo, a ficha DEVE dizer que o sistema não foi encontrado e oferecer abrir a tela de Catálogo com o nome na busca.
- **FR-020**: Quando houver mais de um item do catálogo com o mesmo nome, a ficha DEVE mostrar o primeiro na ordem do arquivo e avisar da repetição.
- **FR-021**: Todo texto vindo do catálogo, da tabela, dos arquivos e das mensagens de erro DEVE ser exibido como texto, sem interpretar marcação.

**Servidor e endereço da página**

- **FR-022**: A ficha DEVE ter o seletor de servidor do SIAT, só para quem tem `Versoes_Sistemas.CanSelect`, com a mesma lista fechada da tela de Versões; sem escolha, o servidor é o padrão da configuração da aplicação, que nas publicações é o de PRD.
- **FR-023**: O sistema e o servidor DEVEM ficar no endereço da página (o servidor padrão pode ficar fora), e abrir o endereço DEVE restaurá-los; servidor desconhecido ou fora da lista DEVE abrir a ficha com o servidor padrão e um aviso.

**Leitura e cache**

- **FR-024**: A parte do catálogo DEVE vir da mesma leitura em cache usada pela tela de Catálogo, com o mesmo prazo, e aparecer sem esperar as versões.
- **FR-025**: A parte de versões DEVE usar o resultado mais recente ainda válido (até 5 minutos, o mesmo prazo da tela de Versões) que tenha os endereços do servidor escolhido: o da tela de Versões ou o de uma leitura anterior da própria ficha para aquele sistema. Sem resultado válido, a ficha DEVE ler na tabela do servidor só os registros ativos dos nomes vinculados e só os arquivos desses registros.
- **FR-026**: As leituras de versões feitas pela ficha DEVEM seguir as mesmas regras da tela de Versões: interpretação do conteúdo, sistema raiz lido primeiro, tempo limite de 5 segundos por arquivo, leituras em paralelo, cada arquivo lido no máximo uma vez por leitura, conta de serviço somente de leitura e nenhuma gravação nos locais lidos.
- **FR-027**: O resultado de uma leitura feita pela ficha DEVE ficar em cache em memória pelo mesmo prazo, separado por servidor e por sistema, e NÃO DEVE alterar o resultado exibido na tela de Versões. Pedidos simultâneos da mesma ficha no mesmo servidor DEVEM gerar uma só leitura.
- **FR-028**: Durante a leitura de versões, a ficha DEVE mostrar o que está lendo (o servidor, enquanto lê a tabela; depois, a quantidade de arquivos), o tempo decorrido e "Cancelar"; cancelar DEVE devolver a seção de versões ao resultado anterior (ou ao estado com "Tentar de novo") e interromper as leituras quando nenhuma outra pessoa estiver esperando por elas. Cancelamento não conta como falha. A parte do catálogo DEVE continuar utilizável durante a leitura.
- **FR-029**: "Atualizar agora" na ficha DEVE reler o catálogo e, para quem tem `Versoes_Sistemas.CanSelect`, os endereços e os arquivos só daquele sistema no servidor escolhido.
- **FR-030**: A falha da leitura de versões NÃO DEVE esconder a parte do catálogo: sem resultado anterior, a seção de versões DEVE mostrar o erro com o motivo e "Tentar de novo"; com resultado anterior, DEVE continuar com ele e avisar da falha. A falha da leitura do catálogo DEVE seguir as regras da tela de Catálogo (estado de erro sem leitura anterior; leitura anterior com aviso).
- **FR-031**: Na tela de Versões, o catálogo usado para os links e para o filtro **Sem ficha** DEVE vir da leitura em cache do catálogo ou ser lido ao mesmo tempo que as versões, sem atrasar a exibição da tabela; se o catálogo falhar, a tela DEVE funcionar com os nomes sem link, um aviso de que as fichas estão indisponíveis e sem o filtro **Sem ficha**.
- **FR-032**: Falhas do catálogo ou das versões NÃO DEVEM afetar os outros módulos do SIAT.

**Apresentação**

- **FR-033**: A ficha DEVE reaproveitar os componentes e a aparência das telas de Catálogo e Versões (cabeçalho de página, seletor de servidor, resumo, células de versão, estados vazio/erro/carregando, avisos, botão Copiar), nos temas claro, escuro e do sistema.
- **FR-034**: A ficha e as mudanças nas telas de Catálogo e Versões DEVEM atender à WCAG 2.2 AA e ser usáveis só por teclado, com foco visível; o link para a ficha DEVE dizer de qual sistema é, e cada botão de cópia DEVE dizer o que copia, incluindo o ambiente, a máquina e o nome vinculado, quando houver.
- **FR-035**: Todos os textos DEVEM estar em português do Brasil.

### Entidades-chave

- **Ficha do sistema**: a página de um item do catálogo; reúne o item (nome, observação, instâncias por ambiente) e, para quem tem acesso, as leituras de versão dos registros vinculados num servidor.
- **Vínculo de versões**: o campo `Versoes` de um item do catálogo; lista ordenada de nomes de `Apps_VersionAddresses.Sistema`. Um item tem zero ou mais nomes; um nome pode estar no vínculo de mais de um item.
- **Aviso de vínculo**: problema do vínculo mostrado na ficha, sem impedir o resto: nome sem registro ativo no servidor escolhido, campo `Versoes` inválido ou nome de item repetido no catálogo.
- **Sistema sem ficha**: registro ativo da tabela de endereços cujo nome não está no vínculo de nenhum item do catálogo.
- Reaproveitadas das features 002 e 003: **Sistema** e **Instância** do catálogo; **Endereço de versão**, **Leitura de célula**, **Situação do sistema** e **Resultado da leitura**.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: Uma pessoa do suporte responde "onde roda e qual versão está em PRD?" para um sistema vinculado em menos de 20 segundos a partir da tela de Catálogo, sem trocar de tela depois de abrir a ficha, quando há versões em cache.
- **SC-002**: A ficha abre em até 2 segundos: com o catálogo já lido, mostra a parte do catálogo nesse prazo; com resultado de versões válido, mostra também as versões; sem ele, mostra nesse prazo o andamento da leitura com o tempo decorrido.
- **SC-003**: Sem resultado válido, a parte de versões de um sistema fica completa em até 15 segundos, mesmo com todas as máquinas dele fora do ar.
- **SC-004**: Para 100% dos nomes vinculados, a versão e a situação de cada máquina na ficha coincidem com as da tela de Versões no mesmo servidor e no mesmo momento, verificado célula a célula.
- **SC-005**: 100% dos nomes vinculados sem registro ativo no servidor escolhido aparecem como aviso na ficha, e 100% dos sistemas da tela de Versões sem item vinculado aparecem no filtro **Sem ficha**; nenhum link de Versões leva a uma ficha cujo vínculo não contenha aquele nome.
- **SC-006**: Zero dados do catálogo entregues a usuários sem `Catalogo_Sistemas.CanSelect` e zero versões, caminhos consultados ou mensagens de erro de versão entregues a usuários sem `Versoes_Sistemas.CanSelect`, verificado por teste de acesso direto pela URL, com e sem servidor no endereço.
- **SC-007**: Todo fluxo das Histórias 1 e 2 é concluído só com teclado, e a ficha não tem falhas WCAG 2.2 AA na verificação do roteiro de validação.
- **SC-008**: Três meses após a entrega, o filtro **Sem ficha** em PRD lista só sistemas que de fato não têm item no catálogo.

## Premissas

- O SIAT só lê: vincular um item é editar o `sistemasDb.js` no GitLab, como qualquer outra correção do catálogo (ou pelo módulo de Documentação, quando existir). O preenchimento inicial do campo `Versoes` é feito pela equipe no TCMRJDoc, fora do SIAT; o filtro **Sem ficha** mostra o que falta.
- O campo `Versoes` é compatível com as duas leituras do catálogo: o `sistemas.html` antigo percorre só os ambientes (`DSV`, `HML`, `PRD`, `CONT`) e a observação, e o leitor do catálogo do SIAT já ignora campos desconhecidos; por isso o campo novo não muda o que as duas páginas mostram.
- Os nomes do vínculo são escritos como estão na tabela do servidor de PRD; em DSV e HML a tabela pode ter nomes diferentes, e os avisos da ficha mostram essas diferenças.
- Não há recurso novo no UserManager: a ficha usa `Catalogo_Sistemas.CanSelect` e `Versoes_Sistemas.CanSelect` (PRD RG-02). Como no Catálogo e em Versões, os dados não são sensíveis, e a leitura não é registrada no log de consultas a dados sensíveis.
- Os prazos e tempos (10 minutos de cache do catálogo, 5 minutos de cache das versões, 5 segundos por arquivo) são os das telas de Catálogo e Versões e continuam na configuração da aplicação.
- O endereço da ficha identifica o sistema pelo nome no catálogo, que não tem identificador estável: renomear um item quebra links antigos, que caem na mensagem de "sistema não encontrado" com a busca já preenchida.
- A tela de exemplo `design/ficha.html`, desenhada no plano a partir de `design/sistemas.html` e `design/versoes.html`, é o contrato visual da ficha.
- O último build de cada job do Jenkins na ficha (BU-02, campo `Jenkins` no catálogo) fica para a fase 7; a ficha é a página onde ele entrará.
- A migração do `sistemasDb.js` para um `sistemas.json` continua para quando o `sistemas.html` for aposentado, e o campo `Versoes` vai junto.
