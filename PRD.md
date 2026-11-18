# PRD — SIAT: Sistema Integrado de Apoio Técnico

> Documento de requisitos de produto do SIAT; o desenho técnico detalhado fica em documento próprio, por fase. Projeto: `Tcmrj.Siat`; repositório: `siat` (sugestão).

| | |
|---|---|
| **Status** | Rascunho |
| **Data** | 09/11/2026 |
| **Responsável** | Eliezer Martins |

## 1. Contexto

Suporte e desenvolvimento usam hoje um conjunto de ferramentas internas espalhadas, cada uma com sua forma de executar e de acessar:

| Ferramenta | O que faz | Como roda hoje |
|---|---|---|
| `TCMRJDoc/sistemas.html` | Lista sistemas com URL, máquina e caminho por ambiente (DSV, HML, PRD, Contingência) | HTML estático + `sistemasDb.js` |
| `Geral/Ferramentas/VersaoDeSistemasPublicados.ps1` | Mostra a versão publicada de cada sistema em DSV, HML e PRD (duas máquinas por ambiente), com divergências e erros | Script PowerShell na máquina do usuário, gera um HTML temporário |
| Consulta local de MailQueue e AuditLog | Consulta `MailQueue` (e-mails enfileirados) e `AUDITORIADB` (`audit.Log`) em qualquer servidor | `.exe` local que sobe um servidor em `localhost` |
| `handbook` / `TCMRJDoc` | Base de conhecimento, processos, procedimentos | Markdown no GitLab, leitura pela interface do GitLab ou clone |
| `ebooker` | Editor de ebooks em Markdown com prévia | HTML local, grava em pasta via File System Access API |
| `gestao-copilot` | Consumo de AI Credits e budgets do GitHub Copilot | Aplicação Next.js separada, login GitHub, cache SQLite |
| `asi_builds` / Jenkins | Builds e publicações | Jenkins (servidor Windows) |

Consequências: cada ferramenta precisa ser instalada, atualizada ou executada à parte; os dados dependem das credenciais da máquina de quem roda; não há um ponto único de entrada; a documentação é difícil de consultar e de editar para quem não usa Git no dia a dia.

## 2. Problema

Quem dá suporte ou desenvolve perde tempo para responder perguntas rotineiras ("qual versão está em produção?", "onde roda esse sistema?", "o e-mail foi enfileirado em HML?", "o que diz o log de auditoria?", "como faço tal procedimento?") porque as respostas estão em ferramentas diferentes, algumas só disponíveis para quem tem o script ou o executável e as permissões certas na própria máquina.

## 3. Objetivos

1. **Um ponto único de entrada**, no navegador, para as ferramentas de suporte e desenvolvimento, sem instalação na máquina do usuário.
2. **Login único** com o SSO corporativo (UserManager) e **permissões cadastradas no UserManager**.
3. **Nenhum banco próprio**: a aplicação só consulta as fontes que já existem (SQL Server, compartilhamentos, GitLab, Jenkins, GitHub).
4. **Documentação consultável e editável** no próprio SIAT, com o GitLab como fonte de verdade.
5. **Visual moderno e consistente** entre todas as ferramentas.

## 4. Fora do escopo

- Substituir o GitLab, o Jenkins ou o UserManager.
- Persistir dados próprios (histórico de versões, cópias de documentos, preferências no servidor).
- Executar ações em produção (publicar, reiniciar serviços, reenviar e-mails). O SIAT é de consulta, com duas exceções explícitas: edição de documentação (via GitLab) e budgets do Copilot (via API do GitHub).
- O chatbot local do handbook (Ollama).
- Acesso externo à rede do Tribunal.

## 5. Usuários

| Perfil | Necessidade principal |
|---|---|
| **Suporte** | Localizar sistema, URL, máquina e versão; checar e-mails enfileirados e logs de auditoria; consultar procedimentos. |
| **Desenvolvedor** | Tudo do suporte, mais conferir o que foi publicado em cada ambiente, investigar HML/DSV e manter a documentação de APIs e projetos, consultar o próprio consumo do Copilot. |
| **Coordenação / gestão** | Acompanhar o consumo do Copilot e ajustar budgets; editar o handbook. |
| **Grupo restrito de PRD** | Consultar dados sensíveis de produção (e-mails e auditoria de PRD). |

## 6. Requisitos gerais

### 6.1 Autenticação e autorização

- RG-01: Login pelo SSO do UserManager de **PRD**, reaproveitando `Tcmrj.Autenticacao.AspNetCore`.
- RG-02: A aplicação é cadastrada como uma nova aplicação no UserManager. Cada módulo e cada nível de sensibilidade é um **recurso** próprio (o UserManager não tem granularidade por ambiente):

| Recurso | Permite |
|---|---|
| `Sistemas.CanSelect` | Ver o catálogo de sistemas |
| `Versoes.CanSelect` | Ver as versões publicadas |
| `MailQueue.CanSelect` | Consultar MailQueue em DSV, HML e PRD |
| `AuditLog.CanSelect` | Consultar AuditLog em DSV, HML e PRD |
| `Documentacao.CanSelect` | Ler documentação e handbook |
| `Documentacao.CanEdit` | Editar documentação e handbook |
| `Copilot.CanSelect` | Painel completo do Copilot |
| `Copilot.CanEdit` | Alterar budgets e limites do Copilot |
| `CopilotProprio.CanSelect` | Ver o próprio consumo do Copilot |
| `Builds.CanSelect` | Ver builds e publicações do Jenkins |

- RG-03: Menus, páginas e opções de servidor aparecem só para quem tem o recurso correspondente; o servidor valida de novo cada operação.

### 6.2 Fontes de dados e acesso

- RG-04: Consultas a SQL Server e compartilhamentos usam uma **conta de serviço só de leitura**, com permissão apenas no necessário.
- RG-05: A aplicação é **única e sem ambiente**: o usuário escolhe o servidor a consultar. A escolha é sempre de uma **lista fechada de servidores**, cada um marcado com seu ambiente (DSV, HML, PRD). Não existe campo de servidor livre.
- RG-06: A lista de servidores e o catálogo de sistemas ficam em arquivo JSON versionado no GitLab (ou em configuração da aplicação), não em banco.
- RG-07: Ações feitas em nome do usuário em sistemas externos usam o token **do próprio usuário**, obtido por "Conectar ao GitLab" e "Conectar ao GitHub" (OAuth), para que commits e alterações fiquem com o autor correto.
- RG-08: Caches são apenas em memória (ou arquivos temporários em disco), com tempo de expiração e opção de "Atualizar agora".

### 6.3 Auditoria

- RG-09: Toda consulta a MailQueue e AuditLog registra no log (`Tcmrj.CoreLib.DBLog`) quem consultou, qual servidor e quais filtros. Toda edição de documentação e de budget também.

### 6.4 Interface

- RG-10: Blazor (Interactive Server) com a biblioteca **Blazor Blueprint**, tema claro, escuro e do sistema.
- RG-11: Textos, identificadores e mensagens em português do Brasil.
- RG-12: Navegação por teclado, atalho `/` para busca nas listas, foco visível, contraste adequado (WCAG AA).
- RG-13: Estado de filtros e seleção na URL, para que links possam ser compartilhados.
- RG-14: Consultas longas mostram tempo decorrido e podem ser canceladas, interrompendo o comando no SQL Server.

## 7. Módulos

### 7.1 Sistemas

Substitui `TCMRJDoc/sistemas.html`.

- SI-01: Lista de sistemas com, para cada ambiente (DSV, HML, PRD, Contingência), uma ou mais instâncias com URL, máquina e caminho, além de observação.
- SI-02: Busca por vários termos, sem diferenciar acentos e maiúsculas, em todos os campos.
- SI-03: URLs clicáveis; caminhos e máquinas copiáveis com um clique.
- SI-04: Dados lidos do JSON no GitLab (migração do `sistemasDb.js`). Editar o catálogo é editar esse arquivo pelo módulo de Documentação ou pelo GitLab.

### 7.2 Versões publicadas

Substitui `VersaoDeSistemasPublicados.ps1`.

- VE-01: Lê os endereços de `CSLIB_UserManager.dbo.Apps_VersionAddresses` (registros ativos), como o script atual.
- VE-02: Para cada sistema, mostra a versão em DSV, HML e PRD, nas duas máquinas de cada ambiente, lendo `VERSAO.TXT`, `.INI` ou URL, inclusive o caso de caminho que depende da versão do "sistema raiz".
- VE-03: Destaca divergência entre as máquinas de um ambiente e erro de leitura (com a mensagem e o caminho consultado).
- VE-04: Resumo no topo: total de sistemas, sem pendências, com divergência, com erro. Filtros por texto, "com divergência" e "com erro".
- VE-05: Leituras em paralelo, com tempo limite por arquivo; o resultado fica em cache por alguns minutos e mostra quando foi gerado.
- VE-06 (desejável): Unir Sistemas e Versões numa visão por sistema.

### 7.3 Consultas — MailQueue

- MQ-01: Escolha do servidor (lista fechada, filtrada pelas permissões) e do SMTP.
- MQ-02: Filtros por SMTP e por texto (assunto, destinatários, MessageId e corpo), com quantidade de 1 a 1000 (padrão 50).
- MQ-03: Lista expansível; o corpo do e-mail é exibido isolado (iframe com sandbox, sem scripts).
- MQ-04: Exportar o resultado em HTML autocontido.

### 7.4 Consultas — AuditLog

- AL-01: Escolha do servidor e da aplicação (lista de `CSLIB_UserManager`, com campo numérico como alternativa quando a lista não estiver disponível).
- AL-02: Filtros por aplicação, período e busca por id de log.
- AL-03: Formatação própria para stack trace da exceção e para o JSON do LogEvent.
- AL-04: Exportar o resultado em HTML autocontido.

### 7.5 Documentação

Leitura de `handbook`, `TCMRJDoc` e da documentação de APIs e projetos.

- DO-01: Lista de repositórios e pastas de documentação configurada (JSON), lida pela API do GitLab.
- DO-02: Navegação em árvore e renderização de Markdown com imagens, links internos, tabelas e blocos de código.
- DO-03: Busca por texto no conteúdo (pela API de busca do GitLab ou por índice em memória).
- DO-04: Link para o arquivo no GitLab e para o histórico de alterações.

### 7.6 Edição de documentação

Adaptação do `ebooker` para gravar no GitLab.

- ED-01: Editor Markdown com prévia, a partir do editor do ebooker.
- ED-02: Gravação pela API do GitLab em nome do usuário (RG-07), com mensagem de commit.
- ED-03: Modo configurável por repositório: commit direto no branch ou criação de merge request.
- ED-04: Detecção de conflito quando o arquivo mudou no GitLab desde a abertura.
- ED-05: Envio de imagens para a pasta de uploads do repositório.

### 7.7 Gestão do Copilot

Porte do `gestao-copilot`. As regras de negócio do projeto atual (README e testes) são a especificação.

- CP-01: Visão geral, Pessoas, Detalhes da pessoa e Modelos, com filtro de período (até 24 meses, atalhos de período, datas em UTC).
- CP-02: Limites: organização, distribuir para time e transferir, para quem tem `Copilot.CanEdit`.
- CP-03: "Meu consumo" para quem tem `CopilotProprio.CanSelect`, identificando o login do GitHub por "Conectar ao GitHub" (OAuth).
- CP-04: O PAT de billing fica só no servidor. O cache passa a ser em memória.

### 7.8 Builds e publicações

Integração com o Jenkins (`asi_builds`).

- BU-01: Lista de jobs com o último build, o resultado, a data e quem disparou, pela API HTTP do Jenkins.
- BU-02: Ligação entre sistema, job e versão publicada, quando houver correspondência.
- BU-03 (futuro): Ao final de uma publicação, o pipeline atualiza automaticamente a documentação ou o catálogo de sistemas no GitLab.

## 8. Requisitos não funcionais

- RNF-01: **Hospedagem** no servidor do UserManager (IIS), com HTTPS e **WebSockets habilitados** (necessários ao Blazor Server).
- RNF-02: **.NET 10**, com referência às bibliotecas internas (`Tcmrj.Autenticacao.AspNetCore`, `Tcmrj.Autorizacao.App`, `Tcmrj.CoreLib.*`).
- RNF-03: SQL sempre com `SqlParameter` e connection string montada com `SqlConnectionStringBuilder`. Planos de execução das consultas MailQueue e AuditLog medidos com e sem filtros antes da publicação.
- RNF-04: As páginas abrem em até 2 s. As consultas mostram o progresso e podem ser canceladas.
- RNF-05: Segredos (PAT do GitHub, segredos OAuth) fora do repositório, em configuração protegida do servidor.
- RNF-06: Versão do Blazor Blueprint fixada. Componentes de uso recorrente (tabela de resultados, cabeçalho de página, seletor de servidor) encapsulados em componentes próprios.
- RNF-07: Testes automatizados das regras (parâmetros, permissões, interpretação de versões, regras do Copilot) e de componentes; fluxos principais num roteiro de validação manual.

## 9. Roteiro

| Fase | Entrega | Substitui |
|---|---|---|
| 1 | Base: Blazor + SSO + permissões + layout + **Sistemas** | `sistemas.html` |
| 2 | **Versões publicadas** | `VersaoDeSistemasPublicados.ps1` |
| 3 | **MailQueue** e **AuditLog** | Consulta local de MailQueue e AuditLog |
| 4 | **Gestão do Copilot** | `gestao-copilot` |
| 5 | **Documentação** (leitura) | Consulta pelo GitLab |
| 6 | **Edição de documentação** | `ebooker` para documentação |
| 7 | **Builds** e automação pós-publicação | — |

Cada fase é publicada sozinha e entrega valor sem depender das seguintes. As ferramentas antigas continuam disponíveis até a fase correspondente ser validada pelos usuários.

## 10. Métricas de sucesso

- As ferramentas substituídas deixam de ser usadas três meses depois da entrega de cada fase.
- Nenhum acesso indevido a dados de PRD: toda consulta de PRD no log corresponde a um usuário com o recurso PRD.
- O tempo para responder "qual versão está em PRD?" cai de minutos (rodar o script) para segundos.
- Aumenta o número de commits na documentação feitos por pessoas que não editavam pelo Git.

## 11. Riscos

| Risco | Mitigação |
|---|---|
| A conta de serviço amplia o acesso: o SIAT vê o que cada usuário talvez não visse | Lista fechada de servidores, recursos separados para PRD, auditoria de cada consulta, conta só de leitura |
| Blazor Blueprint é recente e pode ter mudanças incompatíveis | Versão fixada e componentes próprios por cima dos dela |
| Bibliotecas internas em `net8.0` numa aplicação `net10.0` | Validar a compatibilidade na fase 1 |
| SSO do UserManager em outra aplicação (cookie, chaves do DataProtection) | Validar na fase 1, seguindo o padrão das outras aplicações do Tribunal |
| Blazor Server depende de conexão contínua (SignalR) | Habilitar WebSockets no IIS e tratar a reconexão na interface |
| Edição direta no branch pode quebrar a documentação | Merge request como padrão nos repositórios sensíveis |
| Sem cache persistente, os relatórios do Copilot repetem muitas chamadas à API | Cache em memória com expiração; se necessário, cache em arquivo |

## 12. Questões em aberto

1. Confirmar o nome do repositório no GitLab (sugestão: `siat`) e o grupo onde ele fica.
2. Quem cria e mantém as aplicações OAuth no GitLab e no GitHub (usuário, grupo ou admin da instância).
3. Lista inicial de servidores por ambiente e quem pode alterá-la.
4. Quais grupos do UserManager recebem os recursos de PRD.
5. Nome DNS e certificado HTTPS do SIAT (sugestão: um nome `siat` no domínio interno).
6. Repositórios de documentação incluídos na fase 5 e o modo de edição de cada um (commit direto ou merge request).
7. Se a correspondência entre sistema e job do Jenkins já existe em algum lugar ou precisa ser criada (no JSON do catálogo).
