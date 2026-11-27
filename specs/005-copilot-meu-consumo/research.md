# Pesquisa: Meu consumo do Copilot

**Feature**: [spec.md](./spec.md) | **Plano**: [plan.md](./plan.md) | **Data**: 2026-11-27

As decisões abaixo resolvem os pontos em aberto do contexto técnico. A referência de regras é o `gestao-copilot` (commit `58c76f0`): `src/lib/github.ts`, `usage.ts`, `users.ts`, `user-details.ts`, `teams.ts`, `profiles.ts`, `usage-period.ts`, `period-usage.ts`, `model-series.ts` e `src/components/user-detail.tsx`.

## R1. Origem dos dados e credencial de leitura

- **Decisão**: API REST do GitHub (`https://api.github.com`), com os cabeçalhos do `gestao-copilot`: `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2026-03-10`, `Authorization: Bearer <token>` e `User-Agent: SIAT`, sem seguir redirecionamentos. `HttpClient` tipado (`AddHttpClient`) com tempo limite configurável (padrão 15 s, o do `gestao-copilot`). Chamadas usadas ([contracts/github-api.md](./contracts/github-api.md)):

| Dado | Chamada | Cache (R8) |
|---|---|---|
| Associação à organização | `GET /orgs/{org}/memberships/{login}` | sem cache (FR-010) |
| Perfil (login atual, nome, foto) | `GET /user/{id}` | 6 h, por conta |
| Licenças Copilot | `GET /orgs/{org}/copilot/billing/seats?per_page=100&page=n` | 5 min, da organização |
| Budgets de AI Credits | `GET /organizations/{org}/settings/billing/budgets?per_page=100&page=n` | 30 s, da organização |
| Times e membros | `GET /orgs/{org}/teams` e `GET /orgs/{org}/teams/{slug}/members` (paginados) | 5 min, da organização |
| Consumo de um dia ou de um mês | `GET /organizations/{org}/settings/billing/ai_credit/usage?year&month[&day]&user={login}` | R8 |

- **Credencial**: token *fine-grained* com dono a organização, com **Administration: Read-only** e **Members: Read-only** nas permissões da organização. O `gestao-copilot` pede *Read and write* em Administration porque grava budgets; esta etapa só lê, e as escritas do CP-02 usarão o token do próprio usuário (Princípio II). O token fica em `Copilot:GitHub:Token`, no `appsettings`, como o do GitLab (constituição 3.2.0, Princípio III) ([contracts/configuracao.md](./contracts/configuracao.md)). Tem validade: a renovação vai para o quickstart.
- **Por que não o token do usuário para ler**: o relatório de consumo, as licenças e os budgets da organização exigem permissão de administração da organização, que um membro não tem; o token do usuário serve só para provar qual conta é dele (R2).
- **Alternativas consideradas**: GitHub App instalada na organização (token de instalação renovado a cada hora, sem validade anual; mais uma peça para cadastrar e um fluxo de JWT a mais, sem ganho nesta etapa, que só lê); API GraphQL (não expõe os relatórios de billing nem os budgets).

## R2. "Conectar ao GitHub" (OAuth)

- **Decisão**: o manipulador OAuth do próprio ASP.NET Core (`AddOAuth`, de `Microsoft.AspNetCore.Authentication.OAuth`, já no framework compartilhado) num esquema `GitHubOAuth`, que entra num esquema de cookie próprio, `GitHubConexao` ([contracts/conexao-github.md](./contracts/conexao-github.md)):
  - **Aplicação OAuth do GitHub** (OAuth App, não GitHub App): uma por endereço de retorno (desenvolvimento local e servidor). O GitHub aceita como `redirect_uri` qualquer caminho abaixo da URL cadastrada no mesmo host, então uma aplicação cadastrada com a raiz do servidor atende a todas as publicações nele (`/siat_dsv`, `/siat_hml`, `/siat`). `ClientId` e `ClientSecret` em `Copilot:OAuth`.
  - **Escopo vazio**: sem escopo, o token só lê os dados públicos da conta (`GET /user`), o suficiente para saber o `id` e o `login` (FR-007). `read:user`, pedido pelo `gestao-copilot`, daria acesso ao e-mail e ao perfil privado sem necessidade.
  - **Proteção do retorno**: o manipulador já gera e confere o `state` com o cookie de correlação (falsificação de requisição) e usa PKCE (`UsePkce = true`; o GitHub aceita PKCE nas OAuth Apps). O endereço de volta para o SIAT só pode ser local.
  - **Validação no retorno** (`OnCreatingTicket`): lê `GET /user` com o token do usuário; confere, com a credencial de leitura (R1), `GET /orgs/{org}/memberships/{login}` com `state = active` e `user.id` igual ao `id` da conta (a mesma regra do `accessRole` do `gestao-copilot`); confere que o usuário do SIAT que voltou é o mesmo que pediu a conexão (matrícula guardada nas propriedades protegidas do desafio e comparada com a do esquema padrão no retorno). Qualquer falha encerra o fluxo sem criar a ligação e volta à página com o motivo (`OnRemoteFailure`).
  - **Ligação**: cookie `GitHubConexao` com as claims `github_id`, `github_login` e `matricula` (a do usuário do SIAT que conectou), protegido pelo Data Protection do ASP.NET Core (criptografado e assinado), `HttpOnly`, `Secure`, `SameSite=Lax`, caminho igual ao `<base href>`, persistente, com expiração absoluta de 30 dias e sem renovação deslizante (FR-009). `SaveTokens = false`: o token do usuário é descartado depois do `GET /user`.
  - **Desconectar**: `GET copilot/github/desconectar` apaga o cookie (`SignOutAsync("GitHubConexao")`), registra no log e volta à página. É `GET` porque também é o destino da desconexão forçada (conta que deixou de ser membro, R4), feita por navegação; o efeito de uma requisição forjada é só pedir a conexão de novo, o mesmo risco aceito no logout do SIA.
- **Chaves do Data Protection**: o cookie só é lido enquanto a chave que o protegeu existir. No IIS com o perfil do usuário do pool carregado, as chaves persistem no perfil e valem por 90 dias, renovadas sozinhas; se o pool não carregar o perfil, as chaves somem a cada reciclagem, e a conexão é pedida de novo (o cookie do OIDC tem a mesma dependência). Nenhuma configuração nova: o quickstart confere o comportamento depois de uma reciclagem.
- **Alternativas consideradas**: guardar a ligação matrícula → conta num arquivo ou tabela (estado próprio, contra o Princípio I); ligação só na sessão do SIAT (descartada no esclarecimento de 2026-11-27); fluxo de dispositivo do GitHub (código digitado em outra aba, pior para o usuário); biblioteca `AspNet.Security.OAuth.GitHub` (pacote a mais para preencher três URLs).

## R3. A conexão dentro do circuito Blazor

O SIAT renderiza tudo em modo interativo sem pré-renderização (`InteractiveServerRenderMode(prerender: false)`); o componente não tem `HttpContext` nem pode ler o cookie. O circuito conhece o usuário pelo `HttpContext.User` da conexão do SignalR (`/_blazor`), que passa pelo pipeline de autenticação.

- **Decisão**: um middleware `ConexaoGitHubMiddleware`, logo depois de `UseAuthentication`, que em toda requisição autenticada lê o cookie com `AuthenticateAsync("GitHubConexao")` e, se ele for válido e a `matricula` dele for igual à do usuário autenticado, acrescenta ao `HttpContext.User` uma segunda identidade (`AuthenticationType = "GitHubConexao"`) com `github_id` e `github_login`. Cookie de outro usuário do SIAT, vencido ou adulterado é ignorado (FR-009). O `IUsuarioAtualService` do circuito passa a ver essa identidade; `IConexaoGitHubService.ObterAsync()` a extrai como `ConexaoGitHub?`.
- Como a identidade é fixada quando o circuito abre, conectar e desconectar terminam sempre com navegação completa (`forceLoad`), que abre um circuito novo.
- **Por que middleware e não `IClaimsTransformation`**: no modo `UsuarioDeRede` o SIAT já registra uma transformação (`ClaimsDoUsuarioDeRede`), e o ASP.NET Core usa só uma; compô-las acoplaria o Copilot ao modo de autenticação. O middleware vale igual nos dois modos.
- **Verificação**: o quickstart confere, nos dois modos de autenticação, que a página reconhece a conexão depois do retorno do GitHub e deixa de reconhecer depois de desconectar (passo crítico; a primeira tarefa da implementação é esse esqueleto ponta a ponta).
- **Alternativas consideradas**: `IHttpContextAccessor` dentro do circuito (a documentação do Blazor desaconselha: o contexto da conexão do SignalR não é confiável depois da abertura); passar a conexão como parâmetro do componente raiz a partir do `App.razor` (mudaria o componente raiz de todas as páginas por causa de um módulo).

## R4. Autorização e isolamento entre pessoas

- **Decisão**: novo `Modulo.CopilotProprio`, cuja `Politica()` resulta em `CopilotProprio.CanSelect` pela regra existente (PRD RG-02). Menu, página Início e página usam `PodeAcessarModuloAsync(Modulo.CopilotProprio)`; cada handler autoriza de novo antes de ler (FR-001). Os endpoints `copilot/github/conectar` e `retorno` também exigem o recurso: sem ele, ninguém liga conta nem gera chamada ao GitHub.
- **Os handlers não recebem login**: `ObterPessoaCopilot`, `ObterConsumoDoPeriodo` e `ObterUsoDoLimite` leem a conta da conexão (`IConexaoGitHubService`), nunca do endereço. O login do endereço só serve para a página comparar (sem diferenciar maiúsculas) e, se for outro, navegar para o próprio login, mantendo `de` e `ate` (FR-004). Assim não existe caminho de código que leia dados de outra conta.
- **Membro ativo a cada carga** (FR-010): `ConsumoCopilotService.ObterPessoaAsync` consulta a associação a cada carga. Se o GitHub responder 404 para o login guardado, o perfil é relido pelo `id` sem cache: se o login mudou (conta renomeada), a associação é consultada com o login atual e a página passa a usá-lo; se a conta não é mais membro ativo ou o `id` não confere, devolve `Erros.Copilot.NaoMembro`, e a página navega para `copilot/github/desconectar?motivo=nao-membro`, que volta a `copilot?conexao=nao-membro-desconectada`.
- **A pessoa vem antes dos relatórios** (FR-010, FR-021): a pessoa montada (associação, perfil, licença, limite e times) fica na instância "Pessoa da carga" do `LeituraEmCache`, com validade fixa de 10 segundos, só para que os três pedidos da mesma carga compartilhem uma leitura; depois disso, cada carga lê a associação de novo, e "Atualizar agora" relê na hora. `ObterPeriodoAsync` e `ObterMesAtualAsync` aguardam essa pessoa antes de ler qualquer relatório: se ela for erro (`NaoMembro`, `SemLicencaNemLimite` ou falha da associação, que impede confirmar o vínculo), devolvem o mesmo erro sem ler relatório. Falhas só dos times, do limite ou do perfil não bloqueiam o consumo.
- **Perfil indisponível** (FR-032): a falha de `GET /user/{id}` na carga normal não derruba a pessoa: ela vem com o login da conexão, sem nome e sem foto, e a página mostra o aviso "Perfil do GitHub indisponível." No caso do 404 da associação, em que o perfil é a única forma de saber se a conta foi renomeada, a falha do perfil é a falha da pessoa.
- `Copilot.CanSelect` e `Copilot.CanEdit` não são usados nem criados nesta etapa (FR-005).
- **Alternativas consideradas**: handler com parâmetro de login e verificação de igualdade (uma verificação esquecida vazaria dados; sem o parâmetro, não há o que esquecer); verificar a associação só ao conectar (a spec exige a cada carga).

## R5. Regras portadas do `gestao-copilot`

Ficam no `Domain`, puras e testáveis sem rede:

- **Consumo** (`normalizeUsageReport`): cada item do relatório tem `model`, `unitType`, `grossQuantity`, `grossAmount`, `netAmount`. `unitType` aceito: `credits` ou `ai-credits`, sem diferenciar maiúsculas; outro valor é erro "unidade de consumo não reconhecida" (não é somado). Créditos = soma de `grossQuantity`; valor bruto e líquido = somas dos valores. Por modelo: chave `model` sem espaços nas pontas e em minúsculas, vazio vira "Modelo não informado" (`null`), mantendo a grafia da primeira ocorrência. Ordem: créditos decrescentes e, no empate, nome do modelo pela cultura pt-BR.
- **Soma do período** (`periodUsage`): soma dia a dia, mesclando modelos pela mesma chave; a lista de dias guarda total e modelos de cada dia (para a evolução).
- **Pessoa** (`userIdentity`, `mergeBudgetUsers`): a conta aparece se tiver licença (`seats`, pelo `id`) ou budget individual (`scope = user` com `user` igual ao login, sem diferenciar maiúsculas). Sem as duas: "Sua conta não tem licença Copilot nem limite individual" e nenhuma leitura de consumo (FR-021). Se os budgets falharem e não houver licença, o erro dos budgets vence (não se afirma que a pessoa não existe).
- **Budgets considerados** (`normalizeBudget`): só os que têm `ai_credits` entre os SKUs e escopo `organization`, `multi_user_customer` ou `user`.
- **Limite vigente** (`effectiveLimit`, com o esclarecimento de 2026-11-27): exceções individuais da pessoa; sem nenhuma, os budgets `multi_user_customer` (padrão). Mais de um no nível aplicado: "Múltiplos budgets". Nenhum: "Sem limite configurado". O budget global (`organization`) e os budgets de outras pessoas nunca saem do serviço.
- **Uso do limite** (`usageState`): sem consumo ou sem limite, indisponível; limite zero, "Limite zero"; razão ≥ 1, limite atingido; ≥ 0,8, atenção; senão, normal. A diferença mostrada é `limite − consumo bruto do mês`.
- **Times** (`teamsForUsers`): percorre os times da organização e os membros de cada um (4 em paralelo); a pessoa é comparada pelo login sem diferenciar maiúsculas; `inherited` vem da API e pode faltar. Uma falha em qualquer time torna "Times indisponíveis" (uma varredura incompleta não prova que a pessoa não tem time). Ordem por nome.
- **Evolução por modelo** (`modelSeries`): os cinco modelos de maior total no período, na métrica escolhida; os demais somados em "Outros".

## R6. Período

- **Decisão**: `Periodo(De, Ate)` no `Domain`, com `Validar(hoje)`: datas `yyyy-MM-dd` válidas, `De ≤ Ate`, ambas entre o primeiro dia do mês 23 meses antes do atual e hoje (UTC), e no máximo 92 dias (esclarecimento de 2026-11-27). Padrão: primeiro dia do mês atual até hoje. Atalhos calculados a partir de hoje (UTC): Hoje, Ontem, Últimos 7 dias, Últimos 30 dias, Mês atual (= padrão) e Mês anterior (do dia 1 ao último dia do mês anterior). "Hoje" vem de `TimeProvider`, para testes.
- **Endereço**: parâmetros `de` e `ate`, os mesmos nomes do AuditLog (`Navegacao.ParametroDe`/`ParametroAte`). Período padrão fica fora do endereço. Valor inválido no endereço não é corrigido em silêncio: a página mostra a mensagem de período inválido (FR-025).
- **Inclui o mês atual**: `Ate ≥ primeiro dia do mês atual`; só nesse caso o uso do limite é consultado.
- **Alternativas consideradas**: aceitar `month=AAAA-MM` (sem links antigos do SIAT, spec); calcular os atalhos no fuso de Brasília (o relatório do GitHub é em UTC, e o `gestao-copilot` também usa UTC).

## R7. Consumo do período e uso do limite

- **Decisão**: `ConsumoCopilotService.ObterPeriodoAsync(conta, periodo, atualizar, progresso, ct)` lê os relatórios diários da conta com no máximo 4 leituras simultâneas (`Parallel.ForEachAsync` com `MaxDegreeOfParallelism`), informa os dias concluídos por `IProgress<int>` e, na primeira falha, no limite de requisições ou no cancelamento, cancela as leituras restantes (um `CancellationTokenSource` ligado ao do pedido). Um dia sem resposta faz o resultado inteiro ser erro: nunca há total parcial (FR-020). O acumulado do mês atual é um único relatório mensal (`year` + `month`, sem `day`), lido em paralelo com o período.
- **Três consultas na página**: pessoa (identidade, licença, times, limite), consumo do período e uso do limite (acumulado do mês) vão em três `Mediator.Send` paralelos. O período e o mês só leem relatórios depois que a pessoa compartilhada da carga é confirmada (R4): sem licença nem limite, conta fora da organização ou associação sem resposta, nenhum relatório é lido (FR-010, FR-021). Confirmada a pessoa, a falha de uma parte não esconde as outras (FR-032).
- **Andamento e cancelamento**: a página mostra "Lendo N de M dias", o tempo decorrido e Cancelar com o `EstadoCarregando` existente; cancelar devolve a página ao resultado anterior ou ao estado com "Tentar de novo" (FR-028). O cancelamento interrompe as requisições HTTP em curso, mas um relatório que já está sendo lido por outra pessoa continua (contagem de quem espera do `LeituraEmCache`, R8).
- **Estimativa** (SC-002): com 4 leituras simultâneas e ~0,5 s por relatório, o mês atual (até 31 dias) leva cerca de 4 s com o cache vazio; 92 dias, cerca de 12 s (até 15 s, a meta do plano).

## R8. Cache em memória e "Atualizar agora"

- **Decisão**: reaproveitar `LeituraEmCache<TChave, TValor>` (uma leitura por chave, falha com valor anterior, cancelamento pela contagem de quem espera), com uma instância por tipo de dado no `ConsumoCopilotService` (singleton):

| Instância | Chave | Validade (configurável) |
|---|---|---|
| Licenças | organização | 5 min |
| Budgets | organização | 30 s |
| Times e membros | organização | 5 min |
| Perfil | `id` da conta | 6 h |
| Pessoa da carga (associação + pessoa montada, R4) | `id` da conta | 10 s (fixo, não configurável) |
| Relatórios recentes | conta + data (ou mês atual) | 10 min |
| Relatórios fechados | conta + data | 24 h |

  "Recente" é o dia nos últimos três dias em UTC ou o acumulado do mês atual; os demais são "fechados", porque o GitHub já não os altera (regra do `gestao-copilot`, que os guardava sem expiração no SQLite). Na memória eles ganham uma validade longa, mas finita (Princípio I).
- **Limpeza**: hoje o `LeituraEmCache` nunca remove uma chave, o que bastava para o catálogo (uma chave) e as versões (três servidores). Aqui há uma chave por conta e por dia; o `LeituraEmCache` ganha a remoção das entradas vencidas e sem leitura em curso, feita a cada leitura nova, com testes. Com 100 pessoas consultando 92 dias, são cerca de 9 mil entradas de algumas centenas de bytes.
- **Falha nunca mostra o valor anterior** (FR-010, FR-020): quando uma releitura falha, o `LeituraEmCache` devolve o valor anterior junto com a falha. O `ConsumoCopilotService` trata toda resposta com falha como erro e ignora o valor anterior, em todas as instâncias: um dia que falha ao atualizar fica "Indisponível", e não com o valor antigo. Na pessoa da carga, o valor guardado é o resultado inteiro da verificação (a pessoa, `NaoMembro` ou `SemLicencaNemLimite`), e não só a pessoa, para que "não é membro" e "sem licença nem limite" sejam respostas guardadas, e não falhas que trariam de volta a pessoa anterior; só a falha do GitHub (associação sem resposta, token recusado, limite de requisições) é falha.
- **O que fica por organização**: licenças, budgets e times são listas da organização, guardadas uma vez para todos; o serviço devolve só o que é da conta consultada. Relatórios e perfis ficam por conta (FR-029).
- **"Atualizar agora"** (FR-030): relê, sem esperar a validade, a pessoa da carga (com a associação), o perfil, as licenças, os budgets, os times, os relatórios recentes do período e o acumulado do mês. Relatórios fechados não são relidos.
- **Alternativas consideradas**: `IMemoryCache` com limite de tamanho (remoção automática, mas sem uma leitura por chave nem cancelamento pela contagem de quem espera, que teriam de ser reescritos por fora); arquivo temporário em disco, que o PRD admite (mais estado para operar, sem necessidade com 92 dias por consulta); um único prazo para todos os relatórios (10 min relê sem necessidade o histórico; 24 h deixa o dia de hoje velho).

## R9. Erros e mensagens

Erros conhecidos em `Erros.Copilot`, com as mensagens do `gestao-copilot` adaptadas; o token nunca aparece em mensagem nem em log.

| Situação | Mensagem (resumo) |
|---|---|
| Seção `Copilot` incompleta | "O módulo Copilot não está configurado no servidor." Só esta página; o resto do SIAT funciona (Princípio IV) |
| 401 | "O token de billing do GitHub não foi aceito. Verifique a configuração no servidor." |
| 403 sem limite de requisições | "O GitHub recusou o acesso. Verifique as permissões e a aprovação do token." |
| 404 da organização | "Organização não encontrada ou indisponível para a credencial." |
| 429, ou 403 com `x-ratelimit-remaining: 0` ou `retry-after` | "Limite de chamadas do GitHub atingido." + "Tente de novo em N minutos." quando há `retry-after` ou `x-ratelimit-reset` (FR-031) |
| 5xx, tempo limite, falha de rede | "O GitHub não respondeu. Tente de novo." |
| Resposta fora do formato, unidade desconhecida, paginação incompleta | "O GitHub retornou uma resposta inesperada." |
| Conta fora da organização (no retorno do OAuth, `conexao=nao-membro`) | "A conta {login} do GitHub não é membro ativo da organização {org}." |
| Conta que deixou de ser membro, ligação desfeita na carga (`conexao=nao-membro-desconectada`) | "A conta do GitHub conectada não é mais membro ativo da organização {org}, e a ligação foi desfeita." |
| Perfil do GitHub sem resposta (só essa parte) | "Perfil do GitHub indisponível." |
| Sem licença nem budget individual | "Sua conta não tem licença Copilot nem limite individual na organização {org}." |
| Autorização negada ou cancelada no GitHub | "A conexão com o GitHub não foi feita." + "Tentar de novo" |
| Período inválido | "Selecione datas válidas, em ordem, num intervalo de até 92 dias entre o início dos 24 meses disponíveis e hoje (UTC)." |
| Cancelamento | Não é erro: volta ao resultado anterior |

## R10. Registro no log

- **Decisão**: conexão, desconexão (pelo botão ou forçada) e recusa de conexão são registradas no DBLog com `TypeID 10`, como o `RegistroConsultaService`, pelo `RegistroConexaoGitHub` no `Web`: modelo `"{Operacao} da conta {GitHubLogin} do GitHub por {Matricula}"`, com `GitHubId`, `Organizacao`, `Motivo` e `Resultado` no escopo. Fica no `Web` porque roda na requisição HTTP do OAuth, fora do circuito, onde o `IUsuarioAtualService` não tem usuário; a matrícula vem do `HttpContext.User`.
- As consultas ao próprio consumo não são registradas (esclarecimento de 2026-11-27).
- **Alternativas consideradas**: reaproveitar `RegistroConsulta` (os campos de servidor e ambiente não têm sentido aqui, e o serviço lê o usuário do circuito).

## R11. Tela e componentes

- **Página** `Copilot.razor` com duas rotas: `copilot` (sem conexão, mostra a explicação e **Conectar ao GitHub**; com conexão, navega para a própria pessoa) e `copilot/pessoas/{Login}` ([contracts/rotas-ui.md](./contracts/rotas-ui.md)). Trilha **Gestão › Copilot › Meu consumo** pelo `CabecalhoPagina` (parâmetro `Trilha` da 004). Ferramenta nova no grupo **Gestão** de `Ferramentas.Grupos` (menu e Início).
- **Componentes novos** em `Components/Shared` (reaproveitáveis no CP-01): `FiltroDePeriodo` (datas, atalhos, Aplicar, Limpar período, mensagem de erro com `aria-describedby`), `ResumoNumerico` (faixa de indicadores rotulados), `BarraDeUso` (faixa em texto e barra, nunca só cor), `EvolucaoDiaria` e `TabelaDeModelos`. A pessoa (foto, nome, licença, times) é marcada na própria página.
- **Gráfico próprio em SVG** (`EvolucaoDiaria`), portado do `usage-evolution.tsx`: colunas por dia, seletor de métrica (valor bruto, valor líquido, créditos) e visão por modelo; cada dia é um botão alcançável por teclado com `aria-label` ("27/11/2026, valor bruto US$ 1.23. Filtrar este dia."), que é a alternativa em texto (FR-017). Na visão por modelo, o `aria-label` lista as séries com valor naquele dia, na ordem da legenda ("27/11/2026, valor bruto: GPT-5 US$ 0.80; Claude Sonnet 4.5 US$ 0.43; Outros US$ 0.10. Filtrar este dia."); dia sem consumo diz "sem consumo". Os gráficos do Blazor Blueprint (`BbBarChart`, `BbLineChart`) desenham com uma biblioteca JavaScript cujos pontos não são alcançáveis por teclado nem expõem o valor a leitor de tela, o que não atende à WCAG 2.2 AA (Princípio VI). Cores dos tokens do `DESIGN.md`, nos dois temas.
- **Contrato visual**: `design/copilot.html`, desenhado na primeira tarefa a partir do `user-detail.tsx` do `gestao-copilot` e do `design/siat.css`, como o `design/ficha.html` da 004.
- **Valores**: USD com `CultureInfo("en-US")` e prefixo "US$" (como o `money` do `gestao-copilot`); créditos com separador de milhar pt-BR; datas `dd/MM/yyyy`.

## R12. Configuração

- **Decisão**: seção `Copilot` ([contracts/configuracao.md](./contracts/configuracao.md)) com `Organizacao`, `GitHub:Token`, `GitHub:TempoLimiteSegundos`, `OAuth:ClientId`, `OAuth:ClientSecret`, `LeiturasSimultaneas` e os prazos de cache. Validada na leitura, não na subida, como o Catálogo: sem ela, a página mostra "não configurado", o esquema OAuth não é registrado e o resto do SIAT sobe normalmente (Princípio IV).

## R13. Pacotes

Nenhum pacote novo. `AddOAuth` e o cookie vêm do framework compartilhado do ASP.NET Core (`Microsoft.AspNetCore.Authentication.OAuth` e `.Cookies`); `AddHttpClient`, `System.Text.Json` e `Parallel.ForEachAsync`, do .NET. Os testes usam os pacotes já presentes (xUnit v3, AwesomeAssertions, Moq, Reqnroll, bUnit, `Microsoft.Extensions.TimeProvider.Testing`) e um `HttpMessageHandler` falso, como na 002.
