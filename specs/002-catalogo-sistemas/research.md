# Pesquisa: Catálogo de Sistemas

**Feature**: [spec.md](./spec.md) | **Plano**: [plan.md](./plan.md) | **Data**: 2026-11-24

As decisões abaixo resolvem os pontos em aberto do contexto técnico. A análise do `sistemasDb.js` foi feita sobre o arquivo do branch `master` do TCMRJDoc em 2026-11-24 (commit `f2db440`).

## R1. Como o SIAT obtém o `sistemasDb.js`

- **Decisão**: API de arquivos do GitLab, `GET {BaseUrl}/api/v4/projects/{asi%2FTCMRJDoc}/repository/files/{sistemasDb.js}/raw?ref=master`, com o token no cabeçalho `PRIVATE-TOKEN`. `BaseUrl` confirmado em 2026-11-24: `https://git.tcmnet.tcm` (a API responde 401 sem token, com certificado aceito numa estação da rede; `http://git` e `http://git.tcmnet.tcm` redirecionam para ele). Falta conferir o mesmo a partir dos servidores do IIS, na publicação. `HttpClient` tipado (`AddHttpClient`, do próprio ASP.NET Core) com `Timeout` configurável (padrão 10 s).
- **Credencial**: token de acesso do projeto TCMRJDoc (project access token), papel Reporter, escopo `read_repository`. A documentação do GitLab descreve esse escopo como leitura do repositório "por Git-over-HTTP ou pela API de arquivos do repositório"; `read_api` não é necessário. O token expira em até 365 dias (limite padrão do GitLab): a renovação é tarefa de operação, registrada no quickstart, e o erro de token expirado tem mensagem própria (R6).
- **Onde fica o token**: fora do repositório (Princípio III). Em desenvolvimento, `dotnet user-secrets` (`CatalogoSistemas:GitLab:Token`); no IIS, variável de ambiente `CatalogoSistemas__GitLab__Token` no `applicationHost.config` do site, como a infraestrutura já faz para a autenticação do Windows. Não é a exceção do `ClientSecret` do OIDC: essa exceção é única.
- **Por que não o token do usuário**: o Princípio II exige token do usuário só para escrita; ler o catálogo é igual para todos e não deixa autoria. O "Conectar ao GitLab" (RG-07) é da fase de Documentação e obrigaria todo usuário do suporte a conectar uma conta só para ver o catálogo.
- **Alternativas consideradas**: clone local do repositório atualizado por tarefa agendada (estado em disco, mais uma peça para operar); leitura do arquivo num compartilhamento de rede (não há cópia publicada; seria outra fonte a manter); URL "raw" da interface web (`/-/raw/master/...`) com o mesmo token (não é API documentada para tokens; muda entre versões).

## R2. Interpretação do `sistemasDb.js`

O arquivo é JavaScript, não JSON. A análise encontrou, além do esperado (`var data = [ ... ];`, BOM, vírgulas sobrando em 63 objetos):

| Particularidade | Ocorrências | Exemplo |
|---|---|---|
| Nome de campo sem aspas | 1 | `OBS: "Novo site feito para o DGP"` (Portal do Servidor - INTERNET) |
| Barra invertida antes de caractere que não forma escape JSON | 17, em 15 sistemas | `"G:\DESENVOLVIMENTO\VBSCP\\SCP..."`, `"W:\\TcmRoot\\\DSV\\Sites..."`, `"C:\\TcmRoot\SIGARH_Ag..."` |

O `System.Text.Json` recusa as duas. No JavaScript, uma barra antes de caractere sem significado especial é descartada: `\D` vira `D`. Por isso o `sistemas.html` mostra hoje `G:DESENVOLVIMENTOVBSCP\SCP DESENVOLVIMENTO.EXE` (perde barras) e `W:\TcmRoot\DSV\Sites\...` (fica certo por acaso).

- **Decisão**: um leitor próprio e pequeno (`LeitorSistemasDb`, no `Infra`) que converte o subconjunto de JavaScript usado pelo arquivo em JSON estrito e entrega o resultado ao `System.Text.Json` (`AllowTrailingCommas = true`, `CommentHandling = Skip`). O leitor:
  - descarta o BOM e tudo antes do primeiro `[` e depois do último `]` (a declaração `var data =` e o `;`);
  - percorre o texto caractere a caractere, sabendo se está dentro de um texto entre aspas;
  - fora de texto, põe aspas em nome de campo sem aspas (identificador seguido de `:`) e mantém comentários `//` e `/* */`, que o `System.Text.Json` ignora;
  - dentro de texto, aceita aspas simples ou duplas e traduz os escapes com a regra do JavaScript: `\n`, `\r`, `\t`, `\b`, `\f`, `\v`, `\0`, `\xHH`, `\uXXXX`, `\u{...}`, `\'`, `\"`, `\\`, barra seguida de quebra de linha (continuação, vira nada); qualquer outro `\c` vira `c`.
  - Qualquer outra construção (expressão, concatenação, função, `undefined`) é erro de formato, com linha e coluna (R6).
- **Consequência**: o SIAT mostra exatamente o que o `sistemas.html` mostra (FR-020, SC-002), inclusive os 17 valores com barra perdida. Corrigir é editar a origem (trocar `\` por `\\` nesses caminhos); a lista vai para o quickstart como sugestão de commit no TCMRJDoc, fora desta feature.
- **Alternativas consideradas**: manter a barra nos escapes inválidos (corrige `G:\DESENVOLVIMENTO`, mas estraga `W:\TcmRoot\\DSV` e faz as duas páginas divergirem); avaliar o arquivo num motor JavaScript como o Jint (fidelidade total, mas executa código vindo do repositório dentro do servidor do SIAT e acrescenta dependência); biblioteca JSON5 para .NET (as disponíveis estão sem manutenção, e JSON5 também não cobre todos os escapes); expressões regulares sobre o texto inteiro (não distinguem o que está dentro ou fora de texto, e erram em valores com `:` ou `\`).

## R3. Validação dos itens

- **Decisão**: depois da conversão, cada item da lista é validado: `Title` texto não vazio depois de remover espaços das pontas; `DSV`, `HML`, `PRD` e `CONT`, quando presentes, listas de objetos; `Machine`, `URL` e `Path` textos opcionais; `OBS` texto opcional. Campos desconhecidos são ignorados. Item inválido fica de fora e entra na contagem de ignorados (FR-024), com o motivo no log da aplicação (`ILogger`, nível Warning). Lista vazia ou raiz que não é lista é erro de formato.
- Instância com `Machine`, `URL` e `Path` vazios (3 no arquivo atual) é descartada na leitura; ambiente sem instância restante mostra "não configurado" (FR-008). "Contratos" não tem ambiente nenhum e aparece com os quatro como "não configurado".
- `URL` é dividida por `|` e `Machine` por `,`, com espaços das pontas removidos e partes vazias descartadas. Só endereço que começa por `http://` ou `https://` vira link (FR-009); o resto (`N/A`, `AgenteSIGA.EXE (diversos agendamentos)`) é texto.
- `Title` é exibido sem os espaços das pontas ("SCP Api ") e a ordenação usa a cultura pt-BR (`CompareInfo` de `pt-BR`, como o `localeCompare(..., "pt-BR")` da página antiga).

## R4. Cache e "Atualizar agora"

- **Decisão**: serviço singleton `CatalogoSistemasService` no `App`, com a última leitura válida em memória, validade configurável (padrão 10 min, `MinutosDeCache`) e `TimeProvider` para testes. Releitura por expiração ou por "Atualizar agora".
  - **Uma leitura por vez**: pedidos simultâneos aguardam a mesma tarefa de leitura (um `Task` compartilhado sob `SemaphoreSlim`), cobrindo o caso de duas pessoas acionarem "Atualizar agora" juntas.
  - **Leitura anterior preservada**: se a releitura falhar e houver leitura válida, o serviço devolve a anterior com a falha anexada (horário e motivo); sem leitura anterior, devolve o erro (FR-023).
  - **Cancelamento** (Princípio VII): a leitura compartilhada tem o seu próprio `CancellationTokenSource` e uma contagem de quem espera por ela. Cada pedido entra na contagem e espera com `WaitAsync(cancellationToken)`; quando o pedido é cancelado (botão "Cancelar" ou saída da página), ele sai da contagem e, se a contagem chegar a zero, o serviço cancela a leitura, o que interrompe a requisição HTTP ao GitLab. Leitura cancelada não altera `ultimaValida` nem `ultimaFalha` e não é falha da origem. O tempo limite do `HttpClient` continua valendo para todos.
- Sem `IMemoryCache`: há um único valor, sem chave nem política de remoção; um campo no singleton é o mais simples (Princípio VIII). Nada em disco (Princípio I).
- **Alternativas consideradas**: leitura não cancelável, limitada só pelo tempo limite (rejeitada: o Princípio VII exige cancelamento com interrupção na origem); cancelar a leitura compartilhada assim que qualquer pedido cancela (atrapalharia quem continua esperando); leitura a cada abertura da tela (pressão desnecessária no GitLab e abertura mais lenta); devolver o dado vencido e reler em segundo plano (mais rápido na expiração, mas o SC-004 fica menos previsível e o código mais complexo); pré-carga na inicialização da aplicação (acopla a subida do SIAT ao GitLab, contra o Princípio IV).

## R5. Permissão

- **Decisão**: novo valor `Modulo.Sistemas`, cuja `Politica()` já resulta em `Sistemas.CanSelect` pela regra existente (`ModuloExtensions`). O menu, a página inicial e a página usam `IAutorizacaoConsultaService.PodeAcessarModuloAsync(Modulo.Sistemas)`, e a feature `ObterCatalogo` autoriza de novo antes de devolver qualquer dado (FR-003). O catálogo não depende de servidor, então `AutorizarAsync(modulo, servidor)` não é usado.
- **Um recurso para todos os ambientes**: segue o PRD (RG-02, `Sistemas.CanSelect`). A regra da constituição que exigia recurso próprio para dados de produção foi retirada na emenda 3.0.0, por decisão do responsável depois do `/speckit-analyze`; o Princípio III continua exigindo recurso verificado no servidor para toda página com dados. O catálogo descreve onde os sistemas rodam (URL, máquina e pasta), não dados de negócio, e hoje está aberto a quem lê o TCMRJDoc; por isso a leitura não é consulta a dado sensível e não vai para o registro de consultas (`DBLog`).
- **UserManager**: script novo no `dbchange` com o recurso `Sistemas` (Id 30) e o grupo `Catalogo` com `CanSelect` nele, no mesmo padrão idempotente do script 001 ([contracts/usermanager.md](./contracts/usermanager.md)). Quem entra no grupo é decisão da coordenação.
- **Alternativas consideradas**: `[Authorize(Policy = "Sistemas.CanSelect")]` na página (o padrão das páginas de Consultas é verificar no `OnInitializedAsync` e mostrar o `AcessoNegado` com o recurso faltante; o atributo levaria ao fluxo de sessão encerrada); enum próprio de módulos do catálogo (duplicaria `Politica()`).

## R6. Erros e mensagens

| Situação | Mensagem (resumo) | O que conferir |
|---|---|---|
| Token ou endereço não configurados | "A origem do catálogo não está configurada." | `CatalogoSistemas:GitLab` no servidor |
| 401 do GitLab | "O GitLab recusou o acesso ao catálogo (token inválido ou expirado)." | Validade do token do projeto TCMRJDoc |
| 403/404 | "O arquivo do catálogo não foi encontrado no GitLab." | Projeto, arquivo, branch e papel do token |
| Tempo esgotado / falha de rede | "O GitLab não respondeu em N s." | Disponibilidade do GitLab |
| Formato inválido | "O sistemasDb.js não pôde ser lido: {motivo} (linha L, coluna C)." | Último commit do arquivo |
| Cancelada por quem pediu | "A leitura do catálogo foi cancelada." (não é falha da origem; o repositório a devolve como `Result`, no padrão dos repositórios das Consultas) | Nada |

- Os erros entram em `Erros.Catalogo` no `Domain`, no padrão de `Erros.Consultas`. A mensagem nunca inclui o token.
- Com leitura anterior válida, a tela mostra o catálogo e um `Aviso` com a mensagem e o horário dos dados (FR-023).

## R7. Tela

- **Rota**: `catalogo/sistemas`, com a busca no parâmetro `busca` (identificador em português, Princípio VI). A busca é gravada na URL com `replace: true` a cada alteração, para não encher o histórico a cada tecla.
- **Filtro**: o `FiltroLocal` existente (pt-BR, `IgnoreCase | IgnoreNonSpace`, vários termos) aplicado sobre um texto de busca por sistema montado uma vez na leitura (nome, observação, endereços completos, máquinas e caminhos de todas as instâncias). Com 63 sistemas, o filtro no circuito responde em poucos milissegundos (SC-003).
- **Componentes**: reaproveita `CabecalhoPagina`, `EstadoCarregando` (com tempo decorrido e Cancelar, como nas Consultas), `EstadoErro`, `EstadoVazio`, `Aviso`, `BotaoCopiar` e o componente `FiltroLocal` das Consultas, que já tem o campo com `data-busca` (atalho `/` pelo `siat.js`), `Esc` para limpar, o contador "X de Y" com `aria-live` e o filtro pt-BR. O `FiltroLocal` ganha só o parâmetro opcional `Placeholder` (hoje o placeholder é o próprio rótulo), sem mudar as telas de Consultas. Um componente novo, `CelulaAmbiente`, monta uma célula (instâncias, links, cópias, "não configurado"), por ser a parte com lógica e com teste de componente.
- **Observação**: exibida como texto, com `white-space: pre-line` para respeitar quebras de linha; o `<br />` do SCP (Visualização) aparece como texto, como hoje (FR-012).
- **Quebra de valores longos**: `<wbr>` após `/ \ _ . -`, como no `design/sistemas.html`, montado em componente (sem `MarkupString` com texto da origem).
- **Alternativas consideradas**: filtro no navegador em JavaScript, como a página antiga (duplicaria a regra de busca, que já é testada no `FiltroLocal`); usar o `ListaExpansivel` (o catálogo é tabela de seis colunas, não lista com detalhe).

## R8. Pacotes

Nenhum pacote novo. `AddHttpClient` vem de `Microsoft.Extensions.Http`, parte do framework compartilhado do ASP.NET Core; `System.Text.Json` é do .NET; os testes usam os pacotes já presentes (xUnit v3, AwesomeAssertions, Moq, Reqnroll, bUnit, `Microsoft.Extensions.TimeProvider.Testing`). O `HttpMessageHandler` falso dos testes é uma classe de teste, sem biblioteca.
