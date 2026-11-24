# Roteiro de validação: Catálogo de Sistemas

Roteiro manual dos fluxos da feature (Princípio V). Regras e componentes têm testes automatizados; este roteiro cobre o que só se vê com o SIAT rodando. Referências: [spec](./spec.md), [rotas e tela](./contracts/rotas-ui.md), [origem](./contracts/origem-sistemasdb.md), [configuração](./contracts/configuracao.md).

## Pré-requisitos

1. Script `003` do `dbchange` executado no `CSLIB_UserManager` de DSV ([contracts/usermanager.md](./contracts/usermanager.md)).
2. Dois usuários de teste: **A** no grupo `Catalogo`; **B** sem esse grupo (pode estar em `Consultas`).
3. Token do projeto `asi/TCMRJDoc` (Reporter, `read_repository`) em `CatalogoSistemas:GitLab:Token`, no `src/Tcmrj.Siat.Web/appsettings.json` ([contracts/configuracao.md](./contracts/configuracao.md)).

4. Testes automatizados verdes:

   ```powershell
   dotnet test --solution src/Siat.slnx
   ```

5. SIAT rodando localmente (perfil de desenvolvimento) e o `sistemas.html` antigo aberto lado a lado, a partir de um clone atualizado do TCMRJDoc.

## 1. Localizar onde um sistema roda (História 1)

| Passo | Esperado |
|---|---|
| Entrar com A | Menu com o grupo **Catálogo** acima de **Consultas**; a página inicial leva a Sistemas |
| Olhar a tela sem busca | "63 sistemas"; ordem alfabética igual à da página antiga; cabeçalhos DSV, HML, PRD, CONT com sigla e cor |
| Abrir o sistema **Contratos** | Quatro ambientes "não configurado" |
| Abrir **WebNews - Intranet** | HML "não configurado" (instância vazia no arquivo) |
| Buscar `multas hml` | Só SCP-Multas e SCP-Multas API; contador "2 de 63 sistemas" |
| Buscar `sessoes` | SCP-Sessões e SCP-Sessões API aparecem |
| Buscar `TCM2016WEB1` | Todos os sistemas com essa máquina em qualquer ambiente |
| Olhar **RabbitMQ - Gerenciamento** em PRD | Quatro links separados |
| Olhar **SIGA-RH - AgenteSIGA** | "AgenteSIGA.EXE (...)" como texto, sem link |
| Olhar **SCP (Visualização de Processos)** | Observação com `<br />` como texto, como na página antiga |
| Acionar uma URL | Abre em nova aba |
| Copiar uma máquina de "TCM2016WEB1, TCM2016WEB2" e um caminho | Cada máquina copia sozinha; o caminho copia exato; o botão confirma por 1,5 s |

## 2. Conferência com a página antiga (SC-002)

1. Para dez sistemas escolhidos (incluindo SCP - Sistema de Controle de Processos, SIGA-RH - AgenteCalculo, Portal do Servidor - INTRANET, RabbitMQ - Gerenciamento e STP), comparar cada célula do SIAT com a do `sistemas.html`: mesmos endereços, máquinas, caminhos e observação.
2. Confirmar que o caminho de DSV do SCP aparece igual nas duas páginas (`G:DESENVOLVIMENTOVBSCP\SCP DESENVOLVIMENTO.EXE`) e que a observação do Portal do Servidor - INTRANET ("Novo site feito para o DGP", campo sem aspas no arquivo) aparece.
3. O teste automatizado com a amostra cobre as particularidades; a contagem completa (63 sistemas, 155 instâncias, 3 descartadas por vazias) é conferida aqui: com a busca vazia, o contador mostra 63.

## 3. Compartilhar e teclado (História 2)

| Passo | Esperado |
|---|---|
| Buscar `etcm`, copiar a URL e abrir em outra janela com A | Mesma busca e mesmo resultado |
| Fora da busca, teclar `/` | Foco na busca |
| Na busca, teclar `Esc` | Busca limpa, lista completa, `busca` sai da URL |
| Buscar `xyzxyz` | Estado vazio com "Limpar busca"; o botão volta a lista completa e o foco à busca |
| Percorrer a tabela com `Tab` e leitor de tela | Todos os links e botões alcançáveis, foco visível; botão anunciado como "Copiar caminho de PRD do SGQ" |
| Alternar tema claro, escuro e do sistema | Tabela legível nos três; cores de ambiente visíveis |

## 4. Catálogo atual e falhas (História 3)

| Passo | Esperado |
|---|---|
| Abrir a tela | "Catálogo lido às HH:mm" e "Atualizar agora"; rodapé com link para o `sistemasDb.js` no GitLab |
| Num branch de teste do TCMRJDoc, apontar `CatalogoSistemas:GitLab:Ref` para ele, alterar a observação de um sistema e acionar "Atualizar agora" | A observação nova aparece e o horário muda |
| Alterar de novo e esperar 10 minutos sem atualizar | A alteração aparece ao recarregar a página depois desse prazo |
| Com a tela já carregada, trocar o token por um inválido no `appsettings.json` e acionar "Atualizar agora" | Catálogo anterior continua; aviso "Não foi possível atualizar..." com o motivo de token recusado e o horário dos dados |
| Reiniciar o SIAT ainda com o token inválido e abrir Sistemas | Estado de erro com a mensagem de token e "Tentar de novo"; MailQueue e AuditLog funcionam |
| Remover o token e reiniciar | Estado de erro "A origem do catálogo não está configurada."; o SIAT sobe normalmente |
| Reiniciar o SIAT, simular GitLab lento (por exemplo, `BaseUrl` apontando para um endereço que não responde, como `https://10.255.255.1`) e abrir Sistemas | Em até 2 s, o carregamento com tempo decorrido e "Cancelar" (SC-003) |
| Acionar "Cancelar" | Estado de erro com "Tentar de novo"; no log da aplicação, a requisição ao GitLab aparece cancelada antes do tempo limite de 10 s |
| Com dois navegadores na tela durante a mesma leitura lenta, cancelar só em um | O outro continua esperando até o fim da leitura ou do tempo limite |
| No branch de teste, quebrar o arquivo (apagar um `]`) e atualizar | Erro de formato com linha e coluna (ou aviso, se houver leitura anterior) |
| No branch de teste, apagar o `Title` de um sistema e atualizar | Os demais aparecem; aviso "1 item do sistemasDb.js foi ignorado..." |

Ao fim, restaurar o token, `BaseUrl` e `Ref = master`.

Com o cache quente, medir a abertura da tela (navegação até a lista visível): até 2 s (SC-003).

## 5. Acesso (História 4)

| Passo | Esperado |
|---|---|
| Entrar com B | Sem o grupo **Catálogo** no menu; a página inicial leva a MailQueue (se B tiver Consultas) ou explica a falta de acesso |
| Com B, abrir `catalogo/sistemas?busca=etcm` | Acesso negado citando `Sistemas.CanSelect`; nenhum dado do catálogo na página (conferir o HTML recebido pelo circuito nas ferramentas do navegador) |

## 6. Publicação

- Criar o token de projeto no TCMRJDoc e registrar a data de validade na agenda de quem opera o SIAT.
- Gravar o token em `CatalogoSistemas:GitLab:Token` no `appsettings.json` publicado em cada servidor e conferir que o servidor alcança o GitLab por HTTPS (certificado da autoridade interna confiável).
- Executar o script `003` em PRD.
- Repetir as seções 1 e 5 no ambiente publicado.
- Sugerir ao TCMRJDoc o commit que corrige as barras perdidas ([contracts/origem-sistemasdb.md](./contracts/origem-sistemasdb.md#valores-do-arquivo-atual-que-perdem-a-barra)).

## Resultado da validação (2026-11-24, DSV local, token real, origin/master do TCMRJDoc)

Feita no navegador do Claude Code com o usuário da rede (modo `UsuarioDeRede`), que está no grupo `Catalogo`.

| Seção | Resultado |
|---|---|
| Pré-requisitos | Script `003` aplicado (o usuário tem `Sistemas.CanSelect`); token no `appsettings.json`; 486 testes automatizados verdes |
| 1. Localizar | OK. Menu com Catálogo acima de Consultas; a página inicial leva a Sistemas; "63 sistemas" em ordem alfabética; Contratos com os quatro ambientes "não configurado"; WebNews - Intranet sem HML; `multas hml` → SCP-Multas e SCP-Multas API ("2 de 63 sistemas"); `sessoes` → SCP-Sessões e SCP-Sessões API; `TCM2016WEB1` → 43 sistemas (42 pela máquina e o RabbitMQ pela URL, como na página antiga); RabbitMQ com quatro links em PRD; AgenteSIGA como texto; `<br />` da observação como texto; os 123 links com `target="_blank"` e `noopener`; Copiar copia só a máquina (`TCM2016WEB2`) ou o caminho exato (`W:\TcmRoot\Sites\sgq`) e volta ao normal em 1,5 s |
| 2. Conferência | OK. As 63 linhas comparadas por script com o `sistemas.html` (mesmo `sistemasDb.js`, interpretado pelo Node): mesma ordem e mesmos endereços, máquinas, caminhos e observações em todas as células; 155 instâncias no arquivo; o caminho de DSV do SCP e o `OBS` do Portal do Servidor - INTRANET aparecem como no arquivo |
| 3. Compartilhar e teclado | OK. `?busca=etcm` abre com a busca aplicada (8 de 63); digitar grava a busca na URL sem criar entrada no histórico; `Esc` limpa a busca e a URL; `xyzxyz` → estado vazio, e "Limpar busca" volta à lista e põe o foco na busca; `/` fora de campo foca a busca; botões anunciados como "Copiar caminho de PRD do SGQ" |
| 4. Falhas | OK no que não exige alterar o TCMRJDoc nem o token. Com o cache quente, a lista aparece em 242 ms; com o GitLab lento (`BaseUrl` = `https://10.255.255.1`), o carregamento com tempo decorrido e "Cancelar" aparece em cerca de 1 s (1,2 s sem catálogo, logo após reiniciar); "Atualizar agora" que esgota o tempo mantém o catálogo e mostra "Não foi possível atualizar o catálogo: O GitLab não respondeu em 10 s. Mostrando os dados lidos às HH:mm."; "Cancelar" volta ao catálogo anterior e, sem catálogo, ao estado "Leitura do catálogo cancelada" com "Tentar de novo"; o log registra "Leitura do catálogo de sistemas cancelada antes do fim." 2 s depois do início; com duas abas na mesma leitura, cancelar numa não interrompe a outra, que vai até o tempo limite; reiniciado com o GitLab inacessível, a tela mostra o erro com "Tentar de novo" e a MailQueue funciona; `BaseUrl` inválido → "A origem do catálogo não está configurada."; o token não aparece no log |
| 4. Branch de teste | OK. Branch temporário `teste/siat-catalogo-sistemas` no TCMRJDoc, a partir do `origin/master`, com `Ref` apontado para ele (apagado ao fim, e `Ref` de volta a `master`). Observação do SGQ alterada: recarregar a página manteve a leitura em cache; "Atualizar agora" trouxe a nova e o horário passou de 09:46 para 09:47. Alterada de novo sem atualizar: aos 9 minutos, recarregar ainda mostrava a anterior; aos 10 min 20 s, recarregar trouxe a nova, com o horário novo. Sem o `]` que fecha o PRD do SGQ: aviso "O sistemasDb.js não pôde ser lido: ',' ou ']' esperado e encontrado ':' (linha 97, coluna 7)", com o catálogo anterior na tela. Sem o `Title` do STP: 62 sistemas, aviso "1 item do sistemasDb.js foi ignorado por estar incompleto." e o motivo no log ("Item 63 do sistemasDb.js ignorado: sem Title"). O token inválido não foi testado no navegador (exige trocar o token); está coberto pelos testes do repositório |
| 5. Acesso | Não executado no navegador: falta um usuário de teste sem o grupo `Catalogo`. Coberto pelos testes de página, menu, página inicial e handler |
| Visual (T053) | Comparado com `design/sistemas.html` nos temas claro e escuro e a 375 px. Estilos calculados iguais (fontes, cores, espaçamentos e larguras das colunas). Corrigido no `siat.css`: links de conteúdo sem sublinhado (o reset do BlazorBlueprint tirava); abaixo de 768 px a página inteira rolava na horizontal em vez da tabela (`.conteudo` com `margin: 0 auto` não esticava e os `.so-leitor` absolutos escapavam da `.tabela-rolagem`); o cabeçalho não ficava fixo ao rolar (a `.tabela-rolagem` de Sistemas passou a rolar também na vertical, com altura máxima da tela). O protótipo tem os dois últimos defeitos |
