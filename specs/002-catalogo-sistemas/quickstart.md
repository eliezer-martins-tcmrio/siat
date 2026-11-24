# Roteiro de validação: Catálogo de Sistemas

Roteiro manual dos fluxos da feature (Princípio V). Regras e componentes têm testes automatizados; este roteiro cobre o que só se vê com o SIAT rodando. Referências: [spec](./spec.md), [rotas e tela](./contracts/rotas-ui.md), [origem](./contracts/origem-sistemasdb.md), [configuração](./contracts/configuracao.md).

## Pré-requisitos

1. Script `003` do `dbchange` executado no `CSLIB_UserManager` de DSV ([contracts/usermanager.md](./contracts/usermanager.md)).
2. Dois usuários de teste: **A** no grupo `Catalogo`; **B** sem esse grupo (pode estar em `Consultas`).
3. Token do projeto `asi/TCMRJDoc` (Reporter, `read_repository`) gravado em user-secrets:

   ```powershell
   dotnet user-secrets set "CatalogoSistemas:GitLab:Token" "<token>" --project src/Tcmrj.Siat.Web
   ```

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

1. Para dez sistemas escolhidos (incluindo SCP - Sistema de Controle de Processos, SIGA-RH - AgenteCalculo, Portal do Servidor - INTERNET, RabbitMQ - Gerenciamento e STP), comparar cada célula do SIAT com a do `sistemas.html`: mesmos endereços, máquinas, caminhos e observação.
2. Confirmar que o caminho de DSV do SCP aparece igual nas duas páginas (`G:DESENVOLVIMENTOVBSCP\SCP DESENVOLVIMENTO.EXE`) e que a observação do Portal do Servidor - INTERNET ("Novo site feito para o DGP", campo sem aspas no arquivo) aparece.
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
| Com a tela já carregada, trocar o token por um inválido em user-secrets e acionar "Atualizar agora" | Catálogo anterior continua; aviso "Não foi possível atualizar..." com o motivo de token recusado e o horário dos dados |
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
- Configurar `CatalogoSistemas__GitLab__Token` no `applicationHost.config` do site em cada servidor e conferir que o servidor alcança o GitLab por HTTPS (certificado da autoridade interna confiável).
- Executar o script `003` em PRD.
- Repetir as seções 1 e 5 no ambiente publicado.
- Sugerir ao TCMRJDoc o commit que corrige as barras perdidas ([contracts/origem-sistemasdb.md](./contracts/origem-sistemasdb.md#valores-do-arquivo-atual-que-perdem-a-barra)).
