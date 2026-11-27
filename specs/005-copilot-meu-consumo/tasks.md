---

description: "Lista de tarefas da feature 005 — Meu consumo do Copilot"
---

# Tarefas: Meu consumo do Copilot

**Entrada**: documentos de design em `specs/005-copilot-meu-consumo/`

**Pré-requisitos**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Testes**: incluídos. A constituição (Princípio V) exige testes automatizados das regras e testes de componente para componentes com lógica; os fluxos das histórias ficam no roteiro manual do [quickstart](./quickstart.md). Os testes de cada fase são escritos antes da implementação e devem falhar primeiro.

**Organização**: tarefas agrupadas por história da [spec.md](./spec.md). As Histórias 1 e 2 são ambas P1; a 2 (conectar a conta) vem antes, porque a 1 só é visível com uma conta conectada e porque a leitura da conexão no circuito é o ponto de maior risco do plano ([research.md R3](./research.md#r3-a-conexão-dentro-do-circuito-blazor)). As duas juntas formam o MVP.

## Formato: `[ID] [P?] [História] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem depender de tarefa incompleta)
- **[US1]…[US5]**: história da spec a que a tarefa pertence
- Todo caminho é relativo à raiz do repositório (`D:\Projetos\siat`)

## Convenções de caminho

Solução em `src/Siat.slnx`, com `Tcmrj.Siat.Domain`, `Tcmrj.Siat.Infra`, `Tcmrj.Siat.App`, `Tcmrj.Siat.Web` e `Tcmrj.Siat.Test`. Siga os padrões das features 002 a 004:

- `Result<T, Erro>` (`Tcmrj.CSharpFunctionalExtensions`); erros conhecidos em classes aninhadas de `src/Tcmrj.Siat.Domain/Models/Erros.cs`, com constantes `Codigo...` (exemplo: `Erros.Catalogo`);
- features com `Query` + `Handler` do `NetDevPack.SimpleMediator` que autorizam no servidor antes de qualquer leitura (exemplo: `src/Tcmrj.Siat.App/Web/Features/Catalogo/ObterCatalogo.cs`);
- cliente HTTP tipado, opções validadas na leitura e `HttpMessageHandler` falso nos testes (exemplos: `src/Tcmrj.Siat.Infra/Configurations/CatalogoSistemasConfiguration.cs`, `CatalogoSistemasOptions.cs` e `src/Tcmrj.Siat.Infra/Repositories/CatalogoSistemasRepository.cs`);
- cache, leitura única e cancelamento por contagem com `LeituraEmCache<TChave, TValor>` (`src/Tcmrj.Siat.App/Web/Services/LeituraEmCache.cs`);
- página com andamento, tempo decorrido, Cancelar e "Atualizar agora" (exemplo: `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor` com `EstadoCarregando`); parâmetros na URL por `Navegacao`;
- testes bUnit herdando de `BunitContext`, com `IMediator` falso (`src/Tcmrj.Siat.Test/Componentes/FichaPaginaTests.cs`); testes de unidade com `FakeTimeProvider` e leituras controladas por `TaskCompletionSource` (`src/Tcmrj.Siat.Test/Unidade/LeituraEmCacheTests.cs`);
- DBLog com `LogErrorWithLogUID(10, ...)` e escopo (exemplo: `src/Tcmrj.Siat.App/Web/Services/RegistroConsultaService.cs`).

Referências: regras em `D:\Projetos\gestao-copilot\src\lib` (`github.ts`, `usage.ts`, `users.ts`, `user-details.ts`, `teams.ts`, `usage-period.ts`, `period-usage.ts`, `model-series.ts`) e tela em `src\components\user-detail.tsx` e `usage-evolution.tsx`; contratos em [contracts/](./contracts/). Nenhum pacote novo ([research.md R13](./research.md#r13-pacotes)).

---

## Fase 1: Preparação

**Objetivo**: branch, linha de base verde, credenciais de desenvolvimento, recurso no UserManager e contrato visual.

- [X] T001 Criar o branch `features/<tarefa>_copilot_meu_consumo` (ou `features/copilot_meu_consumo`, sem número de tarefa) a partir de `main` e conferir que `dotnet build src/Siat.slnx` e `dotnet test --solution src/Siat.slnx` passam antes de qualquer mudança
- [ ] T002 [P] Fora do repositório, com um owner da organização: criar o token fine-grained de leitura (Administration **Read-only** e Members **Read-only**, Public repositories) e a OAuth App de desenvolvimento com Homepage e callback `https://localhost:7271/`, sem Device Flow, conforme [contracts/github-api.md](./contracts/github-api.md#permissões-do-token-de-leitura) e [contracts/conexao-github.md](./contracts/conexao-github.md#aplicação-oauth-no-github); anotar a validade do token
- [ ] T003 [P] Criar `dbchange/196.1.1/002 - TCMRio - CSLIB_UserManager - Recurso CopilotProprio e grupo Copilot do SIAT.sql`, idempotente, no padrão do script `001` da mesma pasta e de [scripts-sqlserver.instructions.md](../../.github/instructions/scripts-sqlserver.instructions.md): `@SIAT_AppID` conferido pelo nome `SIAT` (erro se não existir); `AppResources` `Id = 50`, `ResourceKey = 'CopilotProprio'`, `Description = 'Consumo próprio do Copilot'`, `ParentId = NULL`; grupo `Copilot` com Id sequencial na tabela inteira; `Accesses` com `CanSelect = 1` e as demais permissões em 0 ([contracts/usermanager.md](./contracts/usermanager.md)); o script não inclui usuários; executar em DSV e incluir à mão os usuários de teste A e B do [quickstart](./quickstart.md#pré-requisitos) no grupo
- [X] T004 [P] Criar o contrato visual `design/copilot.html` com `design/siat.css` e `design/siat.js` (servido pela configuração "design" do `.claude/launch.json`, porta 5178), a partir de `user-detail.tsx` e `usage-evolution.tsx` do `gestao-copilot` e da estrutura de [contracts/rotas-ui.md](./contracts/rotas-ui.md#estrutura-da-página): estado sem conexão (título "Conecte sua conta do GitHub", texto e botão), página completa (cabeçalho com trilha **Gestão › Copilot › Meu consumo**, foto, nome, etiquetas de licença e times, ações; filtro de período com atalhos; resumo com quatro indicadores e barra de uso nas faixas normal, atenção, atingido e limite zero; evolução diária em SVG com seletor de métrica e visão por modelo; tabela de modelos com participação; nota final), estados de carregando com andamento, erro por parte e "Indisponível"; temas claro e escuro; acrescentar o item **Copilot** no grupo **Gestão** do menu de exemplo

---

## Fase 2: Base (bloqueia todas as histórias)

**Objetivo**: módulo e erros, regras puras do `Domain`, cliente do GitHub, configuração, limpeza do cache, rotas e leitura da conexão no circuito.

**⚠️ Nenhuma história começa antes desta fase.**

### Testes da Base ⚠️

- [X] T005 [P] Em `src/Tcmrj.Siat.Test/Features/Permissoes.feature`, acrescentar a linha `| CopilotProprio | CopilotProprio.CanSelect | CopilotProprio |` aos exemplos de "Política exigida para consultar um módulo" e citar a spec 005 na descrição da funcionalidade
- [X] T006 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/PeriodoTests.cs` para `Periodo` ([data-model.md](./data-model.md#periodo), [research.md R6](./research.md#r6-período)), com `hoje = 2026-11-27`: `Padrao` = 2026-11-01 a 2026-11-27; `Validar` aceita "2026-11-01"/"2026-11-27", um único dia, o primeiro dia da janela "2024-12-01" com `ate` em 2025-03-02 (92 dias) e recusa com `Erros.Copilot.PeriodoInvalido`: formato inválido ("2026-13-01", "27/11/2026", ""), `De > Ate`, `Ate` amanhã, `De` "2024-11-30" (antes do dia 1 do mês 23 meses antes), 93 dias; `Dias` lista todas as datas em ordem; `IncluiMesAtual` verdadeiro para `Ate` 2026-11-01 e falso para 2026-10-31; `Atalhos` = Hoje (27 a 27), Ontem (26 a 26), Últimos 7 dias (21 a 27), Últimos 30 dias (2026-10-29 a 27), Mês atual (= padrão), Mês anterior (2026-10-01 a 2026-10-31), e Mês anterior em `hoje = 2026-03-15` termina em 2026-02-28; `EhRecente`: 25, 26 e 27 verdadeiros, 24 falso
- [X] T007 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/RelatorioDeConsumoTests.cs` e `ConsumoDoPeriodoTests.cs` ([research.md R5](./research.md#r5-regras-portadas-do-gestao-copilot)): `unitType` "credits", "ai-credits" e "AI-Credits" aceitos; "minutes" → `Erros.Copilot.RespostaInesperada`; créditos = soma de `grossQuantity`, valores brutos e líquidos somados; modelos " GPT-5 " e "gpt-5" viram uma linha com a grafia da primeira ocorrência sem espaços nas pontas; `model` nulo, vazio ou só espaços → `Modelo = null` ("Modelo não informado"); ordem por créditos decrescentes e, no empate, nome em pt-BR; lista vazia → zeros e nenhum modelo; `ConsumoDoPeriodo` soma três dias, mescla os modelos pela mesma chave, mantém um item por dia em ordem e a mesma ordenação dos modelos
- [X] T008 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/LimiteVigenteTests.cs` e `UsoDoLimiteTests.cs` ([data-model.md](./data-model.md#limitevigente)): só budgets com `ai_credits` e escopo `organization`, `multi_user_customer` ou `user` são considerados; uma exceção individual com login em maiúsculas diferentes → `Individual(valor)`; sem exceção e um padrão → `Padrao(valor)`; duas exceções → `Ambiguo`; dois padrões sem exceção → `Ambiguo`; só o global → `Nenhum`; o global nunca é usado. `UsoDoLimite.Calcular`: 40 de 50 → `Atencao` (0,8) com restante 10; 39,99 de 50 → `Normal`; 50 de 50 e 60 de 50 → `Atingido` (restante −10); limite 0 → `LimiteZero` com `Razao = null`; consumo nulo, limite `Ambiguo` ou `Nenhum` → `Indisponivel`
- [X] T009 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/GitHubCopilotRepositoryTests.cs` com `HttpMessageHandler` falso ([contracts/github-api.md](./contracts/github-api.md)): cabeçalhos `Accept`, `X-GitHub-Api-Version: 2026-03-10`, `Authorization: Bearer`, `User-Agent: SIAT` e nenhum redirecionamento seguido; login e slug escapados no caminho; relatório diário com `year=2026&month=11&day=7&user=ana` (sem zero à esquerda) e mensal sem `day`; licenças paginadas até `total_seats` e erro de paginação quando a página vem vazia antes do total; budgets até `has_next_page = false`; times e membros até página com menos de 100 e `inherited` ausente → `null`; associação, perfil por id; mapeamento de erros: 401 → `TokenRecusado`; 429 com `retry-after: 120` e 403 com `x-ratelimit-remaining: 0` e `x-ratelimit-reset` → `LimiteDeRequisicoes` com o horário para tentar de novo; 403 comum → `AcessoRecusadoPeloGitHub`; 404 na associação → `NaoMembro`; 404 em outra chamada → `NaoEncontrado`; 500, tempo esgotado e `HttpRequestException` → `GitHubIndisponivel`; JSON fora do formato → `RespostaInesperada`; cancelamento do token do pedido → `Cancelada`; o token nunca aparece na mensagem de nenhum erro
- [X] T010 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/CopilotOptionsTests.cs` ([contracts/configuracao.md](./contracts/configuracao.md#validação)): válido com todos os campos; inválido com `Organizacao`, `GitHub:Token`, `OAuth:ClientId` ou `OAuth:ClientSecret` vazios, `TempoLimiteSegundos` 0 ou 61, `LeiturasSimultaneas` 0 ou 9, prazos de cache ≤ 0 e `HorasRelatoriosFechados` 169; a mensagem de validação não contém o token nem o segredo
- [X] T011 [P] Em `src/Tcmrj.Siat.Test/Unidade/LeituraEmCacheTests.cs`, testar a limpeza ([research.md R8](./research.md#r8-cache-em-memória-e-atualizar-agora)): depois de uma leitura nova, entradas de outras chaves vencidas há mais que a validade e sem leitura em curso deixam de existir (o próximo pedido delas chama a função de leitura); entrada vencida com leitura em curso e entrada ainda válida continuam; a contagem de entradas exposta para teste (`internal`) cai depois da limpeza; os testes atuais continuam passando
- [X] T012 [P] Em `src/Tcmrj.Siat.Test/Unidade/NavegacaoTests.cs`, testar as rotas do Copilot ([contracts/rotas-ui.md](./contracts/rotas-ui.md)): `RotaCopilot` = `copilot`; `Navegacao.Copilot()` = `copilot`; `Navegacao.Copilot("ana-m")` = `copilot/pessoas/ana-m`; com `de`/`ate` → `copilot/pessoas/ana-m?de=2026-10-01&ate=2026-10-31`; login com caracteres de endereço escapado; `Navegacao.ConectarGitHub("copilot/pessoas/ana-m?de=2026-10-01")` escapa o retorno; `Navegacao.DesconectarGitHub("nao-membro")`
- [X] T013 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/ConexaoGitHubMiddlewareTests.cs` e `ConexaoGitHubServiceTests.cs` ([research.md R3](./research.md#r3-a-conexão-dentro-do-circuito-blazor)), com `IAuthenticationService` falso: usuário autenticado com `matricula` 123 e cookie válido com `matricula` 123 → `HttpContext.User` ganha a identidade `GitHubConexao` com `github_id` e `github_login`, e a identidade original continua a primeira; cookie com `matricula` 456 → nenhuma identidade nova; cookie ausente, vencido ou falha de autenticação → nenhuma identidade nova e nenhuma exceção; usuário anônimo → o cookie nem é lido; `ConexaoGitHubService.ObterAsync()` devolve `ConexaoGitHub(GitHubId, Login)` só quando a identidade existe e `github_id` é número

### Implementação da Base

- [X] T014 Em `src/Tcmrj.Siat.Domain/Enums/Modulo.cs`, acrescentar `CopilotProprio` (comentário XML citando PRD RG-02 e a spec 005); em `src/Tcmrj.Siat.Domain/Models/Erros.cs`, criar `Erros.Copilot` com `AcessoNegado`, `NaoConfigurado`, `SemConexao`, `NaoMembro`, `SemLicencaNemLimite`, `PeriodoInvalido`, `TokenRecusado`, `AcessoRecusadoPeloGitHub`, `NaoEncontrado`, `LimiteDeRequisicoes(DateTimeOffset? tenteEm)`, `GitHubIndisponivel`, `RespostaInesperada` e `Cancelada`, com as mensagens da tabela de [research.md R9](./research.md#r9-erros-e-mensagens) (as que citam login e organização recebem os valores)
- [X] T015 [P] Criar `src/Tcmrj.Siat.Domain/Models/Copilot/ConexaoGitHub.cs` (`record ConexaoGitHub(long GitHubId, string Login)`) e `Periodo.cs` conforme [data-model.md](./data-model.md#periodo): `De`/`Ate` `DateOnly` inclusivos em UTC; `Padrao(hoje)`, `Validar(string? de, string? ate, DateOnly hoje)` com formato `yyyy-MM-dd` (cultura invariante), "no máximo 92 dias por consulta" e janela "entre o primeiro dia do mais antigo dos 24 meses disponíveis (o mês atual e os 23 anteriores) e hoje"; `Dias`, `IncluiMesAtual(hoje)`, `Atalhos(hoje)` (rótulo + período) e `EhRecente(data, hoje)` (data ≥ hoje − 2 dias); faz T006 passar
- [X] T016 [P] Criar `src/Tcmrj.Siat.Domain/Models/Copilot/Consumo.cs` (`Consumo`, `ConsumoPorModelo`, `ItemDeConsumo` com `Modelo`, `Unidade`, `Quantidade`, `ValorBruto`, `ValorLiquido`), `RelatorioDeConsumo.cs` (`De(itens, lidoEm)` → `Result<RelatorioDeConsumo, Erro>`) e `ConsumoDoPeriodo.cs` (`De(periodo, dias, consultadoEm)`), com as regras de [research.md R5](./research.md#r5-regras-portadas-do-gestao-copilot) (comparação de modelos por `ToLowerInvariant` sem espaços nas pontas; ordenação com `CompareInfo` de `pt-BR`); faz T007 passar
- [X] T017 [P] Criar `src/Tcmrj.Siat.Domain/Models/Copilot/Budget.cs` (`Budget`, `EscopoDeBudget` = `Organizacao`, `Padrao`, `Individual`, e `Budget.Normalizar(id, escopo, valor, skus, usuario, nomeDaEntidade)` → `Budget?`, nulo fora de `ai_credits` ou dos três escopos), `LimiteVigente.cs` (`Individual`, `Padrao`, `Ambiguo`, `Nenhum`; `Calcular(budgets, login)`), `UsoDoLimite.cs` (`FaixaDeUso` e `Calcular(consumoBrutoDoMes, limite, mes)`) e `PessoaCopilot.cs` (`PessoaCopilot`, `TimeDaPessoa`, `Licenca(long Id, string Login)`, `Associacao(string Estado, string Papel, long UsuarioId)`, `PerfilGitHub(long Id, string Login, string? Nome, string? FotoUrl)`), conforme [data-model.md](./data-model.md); faz T008 passar
- [X] T018 Criar `src/Tcmrj.Siat.Domain/Repositories/IGitHubCopilotRepository.cs` com `ObterAssociacaoAsync(login, ct)`, `ObterPerfilAsync(id, ct)`, `ListarLicencasAsync(ct)`, `ListarBudgetsAsync(ct)`, `ListarTimesAsync(ct)`, `ListarMembrosDoTimeAsync(slug, ct)` (login + `bool? Herdado`) e `ObterRelatorioAsync(login, ano, mes, dia?, ct)` → `Result<IReadOnlyList<ItemDeConsumo>, Erro>`, todos `Task<Result<T, Erro>>` (depende de T014 a T017)
- [X] T019 Criar `src/Tcmrj.Siat.Infra/Configurations/CopilotOptions.cs` (seção `Copilot`, propriedades e `Validar()` de [contracts/configuracao.md](./contracts/configuracao.md), padrões `TempoLimiteSegundos = 15`, `LeiturasSimultaneas = 4` e os prazos de cache) e `CopilotConfiguration.cs` (`AddCopilotConfiguration`: opções sem `ValidateOnStart`, `AddHttpClient<IGitHubCopilotRepository, GitHubCopilotRepository>` com `BaseAddress = https://api.github.com/`, `AllowAutoRedirect = false` e `Timeout` das opções); faz T010 passar
- [X] T020 Criar `src/Tcmrj.Siat.Infra/Copilot/GitHubCopilotRepository.cs` conforme [contracts/github-api.md](./contracts/github-api.md): cabeçalhos por requisição, token lido das opções a cada chamada, `NaoConfigurado` quando as opções são inválidas, paginação (no máximo 1.000 páginas), `System.Text.Json` com classes de leitura privadas, mapeamento de erros e de limite de requisições (`retry-after` em segundos ou `x-ratelimit-reset` em época), `Budget.Normalizar` na lista de budgets, corpo de erro do GitHub nunca lido para mensagem nem log; faz T009 passar
- [X] T021 [P] Em `src/Tcmrj.Siat.App/Web/Services/LeituraEmCache.cs`, acrescentar a remoção das entradas vencidas e sem leitura em curso a cada leitura nova, sob a mesma trava, e a contagem `internal` para teste (com `InternalsVisibleTo` já usado pelo projeto de testes ou acrescentado ao `.csproj`); atualizar o comentário XML citando a spec 005 e [research.md R8](./research.md#r8-cache-em-memória-e-atualizar-agora); faz T011 passar
- [X] T022 [P] Em `src/Tcmrj.Siat.App/Web/Navegacao.cs`, acrescentar `RotaCopilot = "copilot"`, `RotaPessoaCopilot = "copilot/pessoas"`, `RotaConectarGitHub = "copilot/github/conectar"`, `RotaDesconectarGitHub = "copilot/github/desconectar"`, `RotaRetornoGitHub = "copilot/github/retorno"`, `ParametroConexao = "conexao"`, `ParametroRetorno = "retorno"`, `ParametroMotivo = "motivo"` e os métodos `Copilot(login?, de?, ate?)`, `ConectarGitHub(retorno)` e `DesconectarGitHub(motivo?, retorno?)`, reaproveitando `ParametroDe`/`ParametroAte`; faz T012 passar
- [X] T023 Criar `src/Tcmrj.Siat.App/Web/Services/Interfaces/IConexaoGitHubService.cs` e `src/Tcmrj.Siat.App/Web/Services/ConexaoGitHubService.cs` (lê a identidade `GitHubConexao` do `IUsuarioAtualService`; constantes públicas `TipoDeAutenticacao = "GitHubConexao"`, `ClaimId = "github_id"`, `ClaimLogin = "github_login"`, `ClaimMatricula = "matricula"`) e `src/Tcmrj.Siat.Web/Services/ConexaoGitHubMiddleware.cs` (depois de `UseAuthentication`; só com usuário autenticado; `AuthenticateAsync("GitHubConexao")`; `AddIdentity` quando a `matricula` do cookie é igual à do usuário; nunca lança); registrar o serviço em `src/Tcmrj.Siat.Web/Extensions/ServiceCollectionExtensions.cs` e o middleware em `src/Tcmrj.Siat.Web/Configurations/RazorConfiguration.cs` (entre `UseAuthentication` e `UseAuthorization`); faz T013 passar
- [X] T024 Em `src/Tcmrj.Siat.Web/Program.cs` e `src/Tcmrj.Siat.Web/appsettings.json`, chamar `AddCopilotConfiguration` e acrescentar a seção `Copilot` de [contracts/configuracao.md](./contracts/configuracao.md) com a organização, o token e a OAuth App de desenvolvimento de T002; conferir que `dotnet build src/Siat.slnx` e todos os testes passam

**Checkpoint**: regras, cliente do GitHub e leitura da conexão prontos e testados; nada visível ainda.

---

## Fase 3: História 2 — Conectar a conta do GitHub (Prioridade: P1) 🎯 MVP, parte 1

**Objetivo**: **Conectar ao GitHub**, validação no retorno, cookie de 30 dias preso à matrícula, **Desconectar** e registro no DBLog; página `copilot` com a explicação e, conectada, o login da conta.

**Teste independente**: [quickstart.md, seção 1](./quickstart.md#1-conectar-a-conta-história-2), nos dois modos de autenticação (`siat` e `siat-usuario-de-rede` do `.claude/launch.json`).

### Testes da História 2 ⚠️

- [X] T025 [P] [US2] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/ValidacaoRetornoGitHubTests.cs` para `ValidacaoRetornoGitHub` ([contracts/conexao-github.md](./contracts/conexao-github.md#fluxo)), com o repositório e o cliente do `GET /user` falsos: conta membro `active` com o mesmo `id` e matrícula igual à guardada no desafio → identidade com `github_id`, `github_login`, `matricula`, `IsPersistent = true`, `ExpiresUtc` = agora + 30 dias (`FakeTimeProvider`), `AllowRefresh = false` e registro "Conexao"; `state` `pending` → falha `nao-membro`; associação 404 → `nao-membro`; `user.id` diferente → `nao-membro`; matrícula do retorno diferente da guardada → `outro-usuario`; falha do GitHub → `falha`; acesso negado no GitHub (`error=access_denied`) → `negada`; `state` inválido ou ausente (falha de correlação) → `falha` (FR-007); toda falha redireciona para `copilot?conexao={motivo}`, registra "Recusa" com o motivo e não cria cookie; o token do usuário não fica nas propriedades nem nas claims
- [X] T026 [P] [US2] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/RegistroConexaoGitHubTests.cs` com `ILogger` falso: "Conexao", "Recusa" e "Desconexao" usam o TypeID 10 e o modelo `"{Operacao} da conta {GitHubLogin} do GitHub por {Matricula}"`, com `GitHubId`, `Organizacao`, `Motivo` e `Resultado` no escopo ([contracts/conexao-github.md](./contracts/conexao-github.md#registro-no-log-dblog-typeid-10)); nenhum valor do escopo contém token, código ou `state`; falha do logger não propaga
- [X] T027 [P] [US2] Criar `src/Tcmrj.Siat.Test/Componentes/CopilotPaginaTests.cs` com os casos de conexão ([contracts/rotas-ui.md](./contracts/rotas-ui.md)): sem conexão, `copilot` mostra "Conecte sua conta do GitHub" e o link **Conectar ao GitHub** para `copilot/github/conectar?retorno=...` com navegação completa e nenhum `Mediator.Send` de dados; `?conexao=nao-membro`, `negada`, `falha`, `outro-usuario`, `desconectada` e `nao-membro-desconectada` ("A conta do GitHub conectada não é mais membro ativo da organização {org}, e a ligação foi desfeita.") mostram a mensagem correspondente de [research.md R9](./research.md#r9-erros-e-mensagens) e o parâmetro sai do endereço; com conexão, `copilot?de=2026-10-01&ate=2026-10-31` navega para `copilot/pessoas/{login}` mantendo `de` e `ate`; **Desconectar do GitHub** navega com `forceLoad` para `copilot/github/desconectar`; sem `CopilotProprio.CanSelect`, `AcessoNegado` com o recurso
- [X] T028 [P] [US2] Em `src/Tcmrj.Siat.Test/Unidade/FerramentasTests.cs`, `src/Tcmrj.Siat.Test/Componentes/MenuTests.cs` e `HomeTests.cs`: grupo **Gestão** depois de **Consultas**, com a ferramenta **Copilot** (`Modulo.CopilotProprio`, rota `copilot`, ícone `bot`); visível só com a política; item marcado como atual em `copilot` e em `copilot/pessoas/ana`

### Implementação da História 2

- [X] T029 [US2] Criar `src/Tcmrj.Siat.Web/Services/RegistroConexaoGitHub.cs` (operações `Conexao`, `Recusa`, `Desconexao`; matrícula e nome do `HttpContext.User`; nunca propaga falha) conforme [research.md R10](./research.md#r10-registro-no-log); faz T026 passar
- [X] T030 [US2] Criar `src/Tcmrj.Siat.Web/Services/ValidacaoRetornoGitHub.cs` com a lógica de `OnCreatingTicket` e `OnRemoteFailure` de [contracts/conexao-github.md](./contracts/conexao-github.md#fluxo), isolada dos eventos para teste (o cliente do `GET /user` com o token do usuário recebe o `HttpClient` do manipulador); faz T025 passar
- [X] T031 [US2] Criar `src/Tcmrj.Siat.Web/Configurations/CopilotGitHubConfiguration.cs`: só com `Copilot:OAuth` completo, `AddCookie("GitHubConexao")` e `AddOAuth("GitHubOAuth")` com as opções da tabela de [contracts/conexao-github.md](./contracts/conexao-github.md#esquemas-no-aspnet-core) (escopo vazio, `UsePkce`, `SaveTokens = false`, cookie `.Siat.GitHub` com caminho do `<base href>`, 30 dias, sem renovação), eventos ligados a `ValidacaoRetornoGitHub`; endpoints `copilot/github/conectar` (exige `CopilotProprio.CanSelect`; guarda `matricula` e `retorno` local nas propriedades; `Results.Challenge`) e `copilot/github/desconectar` (`SignOutAsync`, registro com motivo `usuario` ou `nao-membro`; com `nao-membro`, redireciona sempre a `copilot?conexao=nao-membro-desconectada`; senão, a `retorno` local ou a `copilot?conexao=desconectada`, conforme [contracts/conexao-github.md](./contracts/conexao-github.md#desconexão)); chamar em `src/Tcmrj.Siat.Web/Program.cs` e no `UseRazorConfiguration`
- [X] T032 [US2] Em `src/Tcmrj.Siat.App/Web/Ferramentas.cs`, acrescentar o grupo **Gestão** com `new(Modulo.CopilotProprio, "Copilot", "Seu consumo de AI Credits do Copilot: período, modelos e limite.", Navegacao.RotaCopilot, "bot")`; conferir o item ativo em `src/Tcmrj.Siat.Web/Components/Layout/Menu.razor` para `copilot/pessoas/*`; faz T028 passar
- [X] T033 [US2] Criar `src/Tcmrj.Siat.Web/Components/Pages/Copilot/Copilot.razor` com `@page "/copilot"` e `@page "/copilot/pessoas/{Login}"`: autoriza `CopilotProprio` no `OnInitializedAsync` (senão `AcessoNegado`); lê a conexão por `IConexaoGitHubService`; sem conexão, a explicação de [contracts/rotas-ui.md](./contracts/rotas-ui.md#explicação-de-conexão-sem-conta-conectada) com o link de conexão (retorno = endereço atual); mensagens de `conexao`, retiradas do endereço com `replace`; com conexão em `copilot`, navega para a própria pessoa mantendo `de`/`ate`; em `copilot/pessoas/{Login}`, por ora, cabeçalho com a trilha **Gestão › Copilot › Meu consumo**, "Conectado como {login}" e **Desconectar do GitHub**; estilos em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css` seguindo `design/copilot.html`; faz T027 passar
- [ ] T034 [US2] Validar à mão as linhas de conexão da [seção 1 do quickstart](./quickstart.md#1-conectar-a-conta-história-2) (os dados da página e o tempo do SC-001 ficam para a T045, depois da História 1) com as configurações `siat` (OIDC) e `siat-usuario-de-rede` do `.claude/launch.json`: a página reconhece a conexão depois do retorno, continua conectada depois de sair e entrar de novo, não reconhece a conexão para outro usuário e deixa de reconhecer depois de desconectar ([research.md R3](./research.md#r3-a-conexão-dentro-do-circuito-blazor)); se o circuito não enxergar a identidade, parar e rever R3 antes da Fase 4

**Checkpoint**: conexão de ponta a ponta nos dois modos de autenticação, registrada no DBLog.

---

## Fase 4: História 1 — Ver o próprio consumo do mês (Prioridade: P1) 🎯 MVP, parte 2

**Objetivo**: pessoa (perfil, licença, times, limite vigente), consumo do mês atual, uso do limite, evolução diária e consumo por modelo, com cache; associação verificada a cada carga e desconexão forçada.

**Teste independente**: [quickstart.md, seção 2](./quickstart.md#2-próprio-consumo-do-mês-história-1), comparando com o `gestao-copilot`.

### Testes da História 1 ⚠️

- [X] T035 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/ConsumoCopilotServiceTests.cs` com repositório falso e `FakeTimeProvider` ([research.md R4, R5, R7, R8](./research.md)): `ObterPessoaAsync`: associação lida sempre sem cache; 404 com perfil relido sem cache mostrando login novo → associação consultada com o login novo e pessoa com o login atual; ainda 404 ou `id` diferente → `NaoMembro`; licença pelo `id`; sem licença e sem budget individual → `SemLicencaNemLimite`; sem licença, budgets com falha → o erro dos budgets; times: a pessoa em dois times (um herdado), ordenados por nome; falha em um time → `Times` com erro e o resto normal; falha dos budgets com licença → `Limite` com erro e o resto normal; o resultado nunca contém budgets nem o global; perfil com falha → pessoa com o login da conexão, `Nome` e `FotoUrl` nulos, `PerfilDisponivel = false` e o resto normal; perfil com falha depois do 404 da associação → a falha do perfil. Pessoa da carga: `ObterPessoaAsync`, `ObterPeriodoAsync` e `ObterMesAtualAsync` simultâneos geram uma só leitura da associação; depois de 10 s, nova carga lê a associação de novo; `NaoMembro`, `SemLicencaNemLimite` ou falha da associação → período e mês devolvem o mesmo erro e nenhum relatório é lido (o repositório falso registra as chamadas); falha só dos times ou do limite não impede o consumo; com a pessoa já no cache, a carga seguinte em que a associação responde 404 devolve `NaoMembro` (não a pessoa anterior) e nenhum relatório é lido; com a pessoa já no cache, a carga em que a associação falha devolve a falha (não a pessoa anterior). Valor anterior com falha ([research.md R8](./research.md#r8-cache-em-memória-e-atualizar-agora)): em toda instância, uma releitura com falha devolve o erro e nunca o valor anterior. `ObterPeriodoAsync`: um relatório por dia com no máximo `LeiturasSimultaneas` em paralelo (contador de concorrência no repositório falso); a primeira falha cancela os demais e devolve o erro (sem total); `IProgress` recebe 1..N. `ObterMesAtualAsync`: relatório mensal sem `day`. Cache: segunda chamada dentro da validade não chama o repositório; dia recente expira em 10 min; dia fechado continua depois de 10 min e expira em 24 h; licenças, budgets e times compartilhados entre duas contas; relatórios e perfis separados por conta
- [X] T036 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/ObterPessoaCopilotHandlerTests.cs`, `ObterConsumoDoPeriodoHandlerTests.cs` e `ObterUsoDoLimiteHandlerTests.cs`: sem a política → `AcessoNegado` e nenhuma chamada ao serviço; sem conexão → `SemConexao` e nenhuma chamada; as `Query` não têm propriedade de login (teste por reflexão) e o serviço recebe a conta da conexão; `NaoMembro` e `SemLicencaNemLimite` repassados; período inválido → `PeriodoInvalido` sem chamar o serviço; `ObterUsoDoLimite` calcula a faixa com o limite da pessoa e o bruto do mês e devolve `Indisponivel` quando o limite falhou; cancelamento → `Erros.Copilot.Cancelada`
- [X] T037 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/SerieDeModelosTests.cs` ([research.md R5](./research.md#r5-regras-portadas-do-gestao-copilot)): sete modelos → cinco linhas de maior total na métrica + "Outros" com a soma dos dois restantes; quatro modelos → quatro linhas, sem "Outros"; empate pelo nome em pt-BR; "Modelo não informado" como rótulo do modelo nulo; um valor por dia, zero no dia sem consumo do modelo
- [X] T038 [P] [US1] Criar `src/Tcmrj.Siat.Test/Componentes/BarraDeUsoTests.cs`, `ResumoNumericoTests.cs`, `TabelaDeModelosTests.cs` e `EvolucaoDiariaTests.cs`: `BarraDeUso` mostra "80%" e o texto da faixa (normal, "Atenção", "Limite atingido no relatório", "Limite zero", "Indisponível"), nunca só a cor; `TabelaDeModelos` com cabeçalhos Modelo, Créditos consumidos, Valor bruto, Valor líquido, Participação, valores "US$ 1,234.56"/créditos "1.235" conforme [research.md R11](./research.md#r11-tela-e-componentes), participação somando 100%, "Modelo não informado" com classe de texto diferenciado e "Sem consumo reportado neste período" sem linhas; `EvolucaoDiaria` com um `button` por dia e `aria-label` "27/11/2026, valor bruto US$ 1.23. Filtrar este dia.", troca de métrica e visão por modelo com legenda dos cinco + "Outros"; na visão por modelo, o `aria-label` do dia lista as séries com valor na ordem da legenda ("27/11/2026, valor bruto: GPT-5 US$ 0.80; Outros US$ 0.10. Filtrar este dia.") e o dia sem consumo diz "sem consumo" ([research.md R11](./research.md#r11-tela-e-componentes)); e o evento de dia acionado por clique e por Enter
- [X] T039 [P] [US1] Em `src/Tcmrj.Siat.Test/Componentes/CopilotPaginaTests.cs`, acrescentar a página com dados ([contracts/rotas-ui.md](./contracts/rotas-ui.md#estrutura-da-página)): nome (ou login, sem nome) e foto; "Licença Copilot ativa"/"Sem licença Copilot"; times com link `https://github.com/orgs/{org}/teams/{slug}` em nova aba, "(abre em nova aba)" para leitor de tela e "via sub-time"; "Sem time visível" e "Times indisponíveis"; resumo com consumo bruto, créditos e período, valor líquido com "Não inclui o preço das licenças", limite com "Exceção individual"/"Padrão da organização"/"Múltiplos budgets"/"Sem limite configurado"; uso do limite com "US$ N abaixo do limite no relatório · acumulado de novembro de 2026"; `SemLicencaNemLimite` mostra só a mensagem, no lugar do resumo, da evolução e da tabela (as consultas do período e do mês voltam com o mesmo erro, sem ler relatório, T035); `PerfilDisponivel = false` mostra o login, sem foto, e "Perfil do GitHub indisponível." com o resto normal; falha só do consumo mostra "Indisponível" no consumo e mantém a pessoa; `NaoMembro` navega com `forceLoad` para `copilot/github/desconectar?motivo=nao-membro`; nota final sobre situação atual e atraso dos relatórios; nenhum texto vindo do GitHub é interpretado como marcação

### Implementação da História 1

- [X] T040 [US1] Criar `src/Tcmrj.Siat.App/Web/Services/Interfaces/IConsumoCopilotService.cs` e `src/Tcmrj.Siat.App/Web/Services/ConsumoCopilotService.cs` (singleton): `ObterPessoaAsync(conexao, atualizar, ct)`, `ObterPeriodoAsync(conexao, periodo, atualizar, IProgress<int>?, ct)` e `ObterMesAtualAsync(conexao, atualizar, ct)`, com as instâncias de `LeituraEmCache` da tabela de [research.md R8](./research.md#r8-cache-em-memória-e-atualizar-agora) (prazos das opções; "Pessoa da carga" com 10 s fixos), período e mês aguardando a pessoa da carga antes de ler relatórios ([research.md R4](./research.md#r4-autorização-e-isolamento-entre-pessoas)), a pessoa da carga guardando o resultado inteiro da verificação e toda resposta com falha tratada como erro, sem usar o valor anterior ([research.md R8](./research.md#r8-cache-em-memória-e-atualizar-agora)), `Parallel.ForEachAsync` limitado por `LeiturasSimultaneas` com `CancellationTokenSource` ligado para parar na primeira falha, regras de [research.md R4 e R5](./research.md) e `TimeProvider`; registrar em `src/Tcmrj.Siat.Web/Extensions/ServiceCollectionExtensions.cs`; faz T035 passar
- [X] T041 [P] [US1] Criar `src/Tcmrj.Siat.App/Web/ViewModels/Copilot/PessoaCopilotVM.cs`, `ConsumoDoPeriodoVM.cs` (com participação por modelo no valor bruto), `UsoDoLimiteVM.cs` (rótulo da faixa e nota) e `SerieDeModelos.cs` ([data-model.md](./data-model.md#seriedemodelos-app-view-model)); nenhum VM leva budgets, o global ou dados de outra conta; faz T037 passar
- [X] T042 [US1] Criar `src/Tcmrj.Siat.App/Web/Features/Copilot/ObterPessoaCopilot.cs` (`Query(bool Atualizar)`), `ObterConsumoDoPeriodo.cs` (`Query(string? De, string? Ate, bool Atualizar)` + `IProgress<int>?` opcional, sem login) e `ObterUsoDoLimite.cs` (`Query(bool Atualizar)`): autorizam `Modulo.CopilotProprio`, leem a conexão, validam o período com `Periodo.Validar`, chamam o serviço e convertem cancelamento em `Erros.Copilot.Cancelada`; faz T036 passar
- [X] T043 [P] [US1] Criar `src/Tcmrj.Siat.Web/Components/Shared/BarraDeUso.razor`, `ResumoNumerico.razor`, `TabelaDeModelos.razor` e `EvolucaoDiaria.razor` (SVG próprio portado de `usage-evolution.tsx`, colunas por dia, seletor de métrica, visão por modelo com `SerieDeModelos`, um `button` focável por dia com `aria-label` da métrica ou, na visão por modelo, das séries do dia, `EventCallback<DateOnly>` do dia, cores dos tokens do `DESIGN.md`), com estilos em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css` iguais aos de `design/copilot.html`; faz T038 passar
- [X] T044 [US1] Completar `src/Tcmrj.Siat.Web/Components/Pages/Copilot/Copilot.razor` em `copilot/pessoas/{Login}` com o período padrão: três `Mediator.Send` em paralelo (pessoa, período, uso do limite só quando o período inclui o mês atual), que compartilham a pessoa da carga no serviço; erro da pessoa (`SemLicencaNemLimite`, `NaoMembro`, associação sem resposta) prevalece sobre os erros iguais do período e do mês; cabeçalho com foto, nome, etiquetas e times; avisos por parte; `ResumoNumerico` com `BarraDeUso`; `EvolucaoDiaria`; `TabelaDeModelos`; nota final com o horário da consulta; `NaoMembro` → `NavigateTo(Navegacao.DesconectarGitHub("nao-membro"), forceLoad: true)`; faz T039 passar
- [ ] T045 [US1] Validar à mão a [seção 2 do quickstart](./quickstart.md#2-próprio-consumo-do-mês-história-1), e as linhas da seção 1 que dependem dos dados (volta do GitHub com os dados e o tempo do SC-001), comparando com a página da mesma pessoa no `gestao-copilot` (SC-003) e medindo os tempos de SC-002

**Checkpoint**: MVP (Histórias 1 e 2) utilizável: a pessoa conecta a conta e vê o próprio consumo do mês.

---

## Fase 5: História 3 — Consultar outro período e compartilhar o link (Prioridade: P2)

**Objetivo**: filtro de período com atalhos, período na URL, dia da evolução como filtro, Limpar período.

**Teste independente**: [quickstart.md, seção 3](./quickstart.md#3-outro-período-e-link-história-3).

### Testes da História 3 ⚠️

- [X] T046 [P] [US3] Criar `src/Tcmrj.Siat.Test/Componentes/FiltroDePeriodoTests.cs`: campos De e Até com `min`/`max` da janela; os seis atalhos preenchem e aplicam o período; **Aplicar** com período inválido mostra a mensagem de `Erros.Copilot.PeriodoInvalido` ligada aos campos por `aria-describedby` e não dispara o evento; **Limpar período** dispara o padrão; desabilitado durante a leitura
- [X] T047 [P] [US3] Em `src/Tcmrj.Siat.Test/Componentes/CopilotPaginaTests.cs`: `?de=2026-10-01&ate=2026-10-31` consulta esse período e não pede o uso do limite ("—" com "Disponível em períodos que incluem o mês atual"); aplicar um período troca `de`/`ate` no endereço sem recarregar; o período padrão sai do endereço; `de`/`ate` inválidos no endereço mostram a mensagem e não chamam o mediador; acionar um dia da evolução troca o período para aquele dia; com conexão de `ana`, `copilot/pessoas/outra-pessoa?de=...&ate=...` navega para `copilot/pessoas/ana` com o mesmo período sem chamar o mediador; um login que o GitHub não aceita (`copilot/pessoas/%3Cscript%3E`, `copilot/pessoas/a..b`) também navega para `copilot/pessoas/ana` sem chamar o mediador e sem exibir o texto do endereço; e `copilot/pessoas/ANA` não navega

### Implementação da História 3

- [X] T048 [US3] Criar `src/Tcmrj.Siat.Web/Components/Shared/FiltroDePeriodo.razor` (datas, atalhos de `Periodo.Atalhos`, Aplicar, Limpar período, mensagem acessível), no padrão dos campos de data de `src/Tcmrj.Siat.Web/Components/Pages/Consultas/AuditLog.razor`; faz T046 passar
- [X] T049 [US3] Em `src/Tcmrj.Siat.Web/Components/Pages/Copilot/Copilot.razor`, ler `de`/`ate` com `[SupplyParameterFromQuery]`, aplicar por `Navegacao.Copilot(login, de, ate)` com `replace`, refazer as consultas na troca do período, ligar o dia da `EvolucaoDiaria` e redirecionar outro login para o próprio mantendo o período (comparação `OrdinalIgnoreCase`); faz T047 passar

**Checkpoint**: Histórias 1 a 3 funcionando de forma independente.

---

## Fase 6: História 4 — Página rápida, que não trava e mostra falhas como falhas (Prioridade: P2)

**Objetivo**: andamento, tempo decorrido e Cancelar; "Atualizar agora" relendo só o que pode mudar; mensagens de erro por parte, de limite de requisições e de módulo não configurado.

**Teste independente**: [quickstart.md, seção 4](./quickstart.md#4-rápida-que-não-trava-e-falhas-como-falhas-história-4).

### Testes da História 4 ⚠️

- [X] T050 [P] [US4] Em `src/Tcmrj.Siat.Test/Unidade/Copilot/ConsumoCopilotServiceTests.cs`: `atualizar = true` relê a pessoa da carga (associação incluída) dentro dos 10 s, perfil, licenças, budgets, times, os dias recentes do período e o mês atual, e não relê dias fechados; os três pedidos simultâneos de um "Atualizar agora" (pessoa, período e mês, todos com `atualizar = true`) geram uma só leitura da associação; um dia recente que falha ao atualizar devolve o erro, e o período fica "Indisponível" em vez de usar o valor anterior; dois pedidos simultâneos do mesmo dia geram uma leitura; cancelar o único pedido interrompe as leituras em curso (token cancelado no repositório falso) e não deixa valor nem falha no cache; cancelar um de dois pedidos do mesmo dia não interrompe a leitura do outro; `LimiteDeRequisicoes` interrompe os dias restantes e é devolvido com `tenteEm`
- [X] T051 [P] [US4] Em `src/Tcmrj.Siat.Test/Componentes/CopilotPaginaTests.cs`: durante a leitura do período, "Lendo N de M dias", tempo decorrido e **Cancelar** em região `aria-live="polite"`; Cancelar volta ao resultado anterior (ou a "Tentar de novo", sem anterior) sem mensagem de erro; **Atualizar agora** envia as três consultas com `Atualizar = true` e mostra o novo horário; `LimiteDeRequisicoes` mostra "Limite de chamadas do GitHub atingido" e "Tente de novo em N minutos"; `NaoConfigurado` mostra "O módulo Copilot não está configurado no servidor." no lugar da página; `TokenRecusado` e `GitHubIndisponivel` mostram a mensagem com "Tentar de novo"; nenhum valor vira "US$ 0.00" quando a parte falhou

### Implementação da História 4

- [X] T052 [US4] Em `src/Tcmrj.Siat.App/Web/Services/ConsumoCopilotService.cs`, completar o que T050 exige (releitura seletiva em `atualizar`, propagação de `LimiteDeRequisicoes`, cancelamento pela contagem de quem espera); faz T050 passar
- [X] T053 [US4] Em `src/Tcmrj.Siat.Web/Components/Pages/Copilot/Copilot.razor`, usar `EstadoCarregando` com andamento (`IProgress<int>` repassado à `Query`), tempo decorrido e Cancelar (`CancellationTokenSource` por carga, cancelado também ao sair da página); **Atualizar agora**; estados de erro por parte com "Tentar de novo"; estado de módulo não configurado; faz T051 passar

**Checkpoint**: Histórias 1 a 4 funcionando de forma independente.

---

## Fase 7: História 5 — Acesso só aos próprios dados (Prioridade: P3)

**Objetivo**: matriz de acesso coberta por cenários BDD e endpoints protegidos.

**Teste independente**: [quickstart.md, seção 5](./quickstart.md#5-acesso-história-5).

### Testes da História 5 ⚠️

- [X] T054 [P] [US5] Em `src/Tcmrj.Siat.Test/Features/Permissoes.feature` e `src/Tcmrj.Siat.Test/Features/Steps/PermissoesDoCopilotSteps.cs` (handlers reais com serviço e repositório falsos, como `PermissoesDaFichaSteps.cs`), o esquema "Próprio consumo do Copilot por recurso e conexão" com os exemplos: sem `CopilotProprio` (com e sem conexão) → acesso negado e nenhuma chamada ao GitHub; com o recurso e sem conexão → sem conexão e nenhuma chamada; com o recurso e conexão de membro ativo → dados só da conta conectada (o repositório falso registra os logins consultados e só contém o da conexão); com o recurso e conta que deixou de ser membro → não membro
- [X] T055 [P] [US5] Criar `src/Tcmrj.Siat.Test/Unidade/Copilot/EndpointsGitHubTests.cs` com `WebApplicationFactory` ou com os delegates dos endpoints testados diretamente: `copilot/github/conectar` sem `CopilotProprio.CanSelect` → 403 sem redirecionar ao GitHub; `retorno` externo (`https://exemplo.com`, `//exemplo.com`) é trocado por `copilot`; `desconectar` com `retorno` externo vai para `copilot?conexao=desconectada`; sem `Copilot:OAuth`, os esquemas `GitHubOAuth` e `GitHubConexao` não existem e `conectar` mostra a mensagem de não configurado

### Implementação da História 5

- [ ] T056 [US5] Ajustar `src/Tcmrj.Siat.Web/Configurations/CopilotGitHubConfiguration.cs` e os passos de T054 até os dois testes passarem; percorrer a página e a explicação só com teclado e com leitor de tela, nos temas claro, escuro e do sistema, corrigindo foco, nomes acessíveis e contraste em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css`

**Checkpoint**: todas as histórias funcionando de forma independente.

---

## Fase 8: Acabamento

- [X] T057 [P] Atualizar `README.md` (módulo **Gestão › Copilot**, configuração `Copilot`, token de leitura, OAuth Apps, renovação), `.github/copilot-instructions.md` (Copilot na visão geral e na arquitetura: `Features/Copilot`, `Infra/Copilot`, conexão com o GitHub) e `DESIGN.md` (componentes `FiltroDePeriodo`, `ResumoNumerico`, `BarraDeUso`, `EvolucaoDiaria`, `TabelaDeModelos`)
- [ ] T058 [P] Conferir que nenhum log da aplicação, mensagem de erro ou exceção registrada contém `Copilot:GitHub:Token`, `Copilot:OAuth:ClientSecret`, token do usuário, código ou `state` (busca no código por `Token`/`ClientSecret` em interpolação e nos logs de uma execução do quickstart)
- [X] T059 Rodar `dotnet build src/Siat.slnx` e `dotnet test --solution src/Siat.slnx` sem falhas nem avisos novos
- [ ] T060 Validar o [quickstart.md](./quickstart.md) inteiro, incluindo a reciclagem do pool na publicação de DSV e o tempo de SC-001, e registrar o resultado no PR
- [ ] T061 Revisar o diff com [code-review-generic.instructions.md](../../.github/instructions/code-review-generic.instructions.md), conferir cada linha da verificação da constituição do [plan.md](./plan.md#verificação-da-constituição-v320) e, pela seção Dependências da constituição, conferir nos `.csproj` de `src/` que os pacotes `Tcmrj.*` e os demais estão na última versão estável (`dotnet list src/Siat.slnx package --outdated`), registrando no PR o que foi atualizado ou por que não

---

## Dependências e ordem de execução

### Entre fases

- **Preparação (Fase 1)**: T001 primeiro; T002, T003 e T004 em paralelo. T002 é pré-requisito de T024 e da validação manual; T003, das validações manuais; T004, das tarefas de tela (T033, T043).
- **Base (Fase 2)**: depende de T001. Bloqueia todas as histórias.
- **História 2 (Fase 3)**: depende da Base. É a primeira, porque a História 1 só é visível com uma conta conectada e T034 confirma o ponto de maior risco.
- **História 1 (Fase 4)**: depende da Base e de T033 (página). Os testes e o serviço (T035 a T042) podem começar junto com a Fase 3.
- **História 3 (Fase 5)**, **História 4 (Fase 6)**: dependem da Fase 4 (mesma página e mesmo serviço); podem ser feitas em qualquer ordem entre si.
- **História 5 (Fase 7)**: depende das Fases 3 e 4.
- **Acabamento (Fase 8)**: depende das histórias que forem entregues.

### Dentro de cada história

Testes primeiro (devem falhar) → modelos e serviço → handlers → componentes → página → validação manual.

### Arquivos compartilhados

`Copilot.razor` é alterado por T033, T044, T049 e T053; `CopilotPaginaTests.cs` por T027, T039, T047 e T051; `ConsumoCopilotService.cs` por T040 e T052; `siat.css` por T033, T043 e T056. Tarefas que mexem no mesmo arquivo não rodam em paralelo.

## Paralelismo

### Preparação e Base

```text
T002, T003, T004                         # fora do código, script e contrato visual
T005 … T013                              # todos os testes da Base
T015, T016, T017                         # modelos do Domain (depois de T014)
T021, T022                               # LeituraEmCache e Navegacao
```

### Histórias 1 e 2

```text
T025, T026, T027, T028                   # testes da História 2
T035, T036, T037, T038                   # testes da História 1 (podem começar junto com a Fase 3)
T041, T043                               # view models e componentes da História 1
```

### Histórias 3 a 5

```text
T046, T047                               # testes da História 3
T050, T051                               # testes da História 4
T054, T055                               # testes da História 5
```

## Estratégia de implementação

### MVP primeiro (Histórias 2 e 1)

1. Fases 1 e 2.
2. Fase 3 (conectar) e **validar T034 nos dois modos de autenticação** antes de continuar.
3. Fase 4 (consumo do mês) e T045.
4. **Parar e validar**: a pessoa conecta a conta e vê o próprio consumo do mês, igual ao do `gestao-copilot`. Pode ser publicado para um grupo piloto.

### Entrega incremental

1. MVP → piloto.
2. História 3 (período e link) → publicar.
3. História 4 (andamento, cancelar, falhas) → publicar.
4. História 5 (matriz de acesso e acessibilidade) → publicar e abrir para o grupo `Copilot`.
5. O `gestao-copilot` continua no ar até a validação pelos membros (Princípio IV).

### Em equipe

Depois da Base: uma pessoa na Fase 3 (OAuth, middleware, página); outra nos testes, no serviço, nos handlers e nos componentes da Fase 4 (T035 a T043), que não dependem da página; a integração na página (T044) espera T033.

## Observações

- [P] = arquivos diferentes, sem depender de tarefa incompleta.
- Commit ao fim de cada tarefa ou grupo lógico, com a referência da tarefa (por exemplo, `feat: conexão com o GitHub (T029-T033)`).
- Nunca usar o token de billing ou credenciais reais em testes automatizados; os testes usam repositório e `HttpMessageHandler` falsos.
- Nenhuma tarefa grava no GitHub (Princípio II); budgets ficam para o CP-02.
