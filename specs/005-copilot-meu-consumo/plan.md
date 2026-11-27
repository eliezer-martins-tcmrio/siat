# Plano de implementação: Meu consumo do Copilot

**Branch**: `features/<tarefa>_copilot_meu_consumo` (ou `features/copilot_meu_consumo` sem número; pasta da spec: `005-copilot-meu-consumo`) | **Data**: 2026-11-27 | **Spec**: [spec.md](./spec.md)

**Entrada**: especificação da feature em `specs/005-copilot-meu-consumo/spec.md`

## Resumo

Primeira etapa do porte do `gestao-copilot` (PRD CP-03): a página **Gestão › Copilot** (`copilot/pessoas/{login}?de=&ate=`), em que quem tem `CopilotProprio.CanSelect` vê o próprio consumo de AI Credits. Ela mostra licença, times, limite vigente, uso do limite, evolução diária e consumo por modelo, num período de até 92 dias dentro de 24 meses. A pessoa liga a sua conta do GitHub por **Conectar ao GitHub**, feito com o manipulador OAuth do ASP.NET Core sem escopo. A ligação fica num cookie protegido de 30 dias, preso à matrícula, sem estado no servidor. Um middleware a entrega ao circuito Blazor como uma segunda identidade do usuário. Os dados vêm da API REST do GitHub com um token de leitura de billing guardado no servidor (`appsettings`, emenda 3.2.0). As leituras ficam em cache só em memória (`LeituraEmCache`, com prazos por tipo de dado e limpeza das entradas vencidas), e as regras do `gestao-copilot` são portadas para o `Domain`. Os handlers nunca recebem o login: leem a conta da conexão, o que torna impossível ler dados de outra pessoa. Nenhuma escrita no GitHub.

## Contexto técnico

**Linguagem/versão**: C# 14, .NET 10 (último patch da 10.0), como as features 001 a 004.

**Dependências principais**: as da 001 a 004, sem pacote novo. O OAuth (`AddOAuth`) e o cookie vêm do framework compartilhado do ASP.NET Core ([research.md R13](./research.md#r13-pacotes)).

**Armazenamento**: nenhum próprio. Origem nova: a API REST do GitHub ([contracts/github-api.md](./contracts/github-api.md)). A conexão fica no cookie `GitHubConexao` do navegador. Caches só em memória: licenças, budgets e times da organização; perfis e relatórios por conta ([R8](./research.md#r8-cache-em-memória-e-atualizar-agora)) (Princípio I).

**Testes**: xUnit v3, AwesomeAssertions, Moq, Reqnroll (permissões), bUnit (componentes) e `HttpMessageHandler` falso para o cliente do GitHub. Os fluxos das histórias estão no roteiro manual do [quickstart](./quickstart.md).

**Plataforma**: IIS no servidor do UserManager, como a 001. Acesso de rede novo: saída HTTPS para `api.github.com` e `github.com` a partir do servidor; o navegador do usuário vai a `github.com` para autorizar.

**Tipo de projeto**: aplicação web (Blazor Server) na solução de camadas do projeto-modelo.

**Metas de desempenho**: página em até 2 s; consumo do mês atual em até 10 s com o cache vazio e em até 2 s com cache (SC-002); 92 dias em cerca de 15 s com o cache vazio (4 leituras simultâneas, [R7](./research.md#r7-consumo-do-período-e-uso-do-limite)).

**Restrições**: só leitura no GitHub; nenhum dado de outra conta lido nem enviado ao navegador; o global, a capacidade e os budgets de outras pessoas não saem do serviço; nada exibido como zero quando falha; texto do GitHub exibido sem interpretar marcação; WCAG 2.2 AA; português do Brasil; falha do GitHub afeta só esta página.

**Escala/escopo**: uma organização; dezenas de usuários do grupo `Copilot`; até 92 relatórios diários por consulta; token de leitura compartilhado por todos (limite de requisições por hora do GitHub, mitigado pelo cache e pelo limite de 92 dias); 1 página nova (duas rotas), 3 endpoints HTTP e 1 item novo no menu e no Início.

## Verificação da constituição (v3.2.0)

*Gate: antes da Fase 0 e de novo depois da Fase 1.*

| Princípio | Como o plano atende | Situação |
|---|---|---|
| I. Sem estado próprio | Nada em banco nem em arquivo no servidor. A ligação com o GitHub fica no cookie do navegador (R2), e o GitHub é a fonte de licenças, budgets, times e consumo. Caches só em memória, com expiração por tipo de dado, limpeza das entradas vencidas e "Atualizar agora" (R8) | Atende |
| II. Consulta por padrão | Só `GET` na API do GitHub; nenhuma operação de budget nesta etapa (FR-005). O token de leitura tem só permissões de leitura. As escritas do CP-02 usarão o token do usuário | Atende |
| III. Segurança e menor privilégio | Recurso novo `CopilotProprio.CanSelect` (script `002` do `dbchange`), verificado no servidor na página, em cada handler e nos endpoints de conexão (R4). Os handlers não recebem login e leem a conta da conexão. O OAuth é feito sem escopo, com `state`, correlação e PKCE, e confere que a matrícula é a mesma e que a conta é membro ativo (R2). O cookie é protegido pelo Data Protection e preso à matrícula, e o token do usuário é descartado. O token de billing e o `ClientSecret` ficam no `appsettings`, pelas exceções da emenda 3.2.0, e nunca aparecem em mensagem nem log. Conexão, desconexão e recusa vão para o DBLog (R10); as consultas ao próprio consumo não são dado sensível de terceiros (esclarecimento de 2026-11-27). Texto do GitHub é exibido como texto | Atende |
| IV. Módulos independentes | Seção `Copilot` validada na leitura: sem ela, só a página mostra "não configurado", e os esquemas OAuth nem são registrados. Falhas do GitHub ficam na parte afetada da página (pessoa, período, uso do limite, times, limite). O `gestao-copilot` continua no ar até a validação | Atende |
| V. Testes | Unidade: `Periodo`, `RelatorioDeConsumo`, `ConsumoDoPeriodo`, `LimiteVigente`, `UsoDoLimite`, `SerieDeModelos`, `GitHubCopilotRepository` (cabeçalhos, paginação, erros, limite de requisições), `ConsumoCopilotService` (caches, recentes e fechados, pessoa da carga antes dos relatórios, atualizar, paralelismo, primeira falha, cancelamento), `LeituraEmCache` (limpeza), `ConexaoGitHubMiddleware` (matrícula igual e diferente, cookie inválido), validação do retorno do OAuth e handlers (matriz recurso × conexão × membro). Reqnroll: cenários de acesso em `Permissoes.feature`. bUnit: página `Copilot` (conexão, estados, redirecionamento de outro login), `FiltroDePeriodo`, `BarraDeUso`, `EvolucaoDiaria`, `TabelaDeModelos`, menu e Início. Fluxos das histórias 1 a 5 no quickstart | Atende |
| VI. Experiência consistente | Contrato visual `design/copilot.html`, a partir do `gestao-copilot` e do `design/siat.css`. Componentes próprios novos e reaproveitáveis no CP-01 (`FiltroDePeriodo`, `ResumoNumerico`, `BarraDeUso`, `EvolucaoDiaria`, `TabelaDeModelos`), mais os existentes (`CabecalhoPagina` com trilha, `Aviso`, estados). Período na URL (`de`, `ate`), temas, pt-BR. O gráfico é SVG próprio e acessível por teclado, porque o do Blueprint não atende à WCAG 2.2 AA (R11) | Atende |
| VII. Desempenho e resiliência | Tempo limite por chamada (15 s, configurável). Leituras do período em paralelo (4), mais pessoa, período e mês em paralelo. Andamento, tempo decorrido e Cancelar, que interrompe as requisições HTTP quando ninguém mais espera (R7, R8). Primeira falha interrompe as demais. Página em até 2 s | Atende |
| VIII. Simplicidade | Nenhum pacote novo. OAuth e cookie do próprio ASP.NET Core em vez de biblioteca ou mapeamento persistido. `LeituraEmCache` reaproveitado (só ganha a limpeza) em vez de `IMemoryCache` e de uma segunda lógica de concorrência. Um serviço e um repositório para o módulo. Código próprio novo, com o motivo: `ConexaoGitHubMiddleware` (o circuito não lê cookies, R3), `RegistroConexaoGitHub` (log fora do circuito, R10) e `EvolucaoDiaria` (acessibilidade, R11). As bibliotecas `Tcmrj.*` continuam na autenticação, na autorização e no DBLog | Atende |
| Restrições técnicas | .NET 10, Blazor Interactive Server, instância única (caches valem para todos), estrutura do projeto-modelo; integração com o GitHub só pela API HTTP oficial | Atende |
| Dependências | Nenhum pacote novo; versões da 001 a 004 mantidas | Atende |
| Fluxo de trabalho no Git | Branch no padrão `features/<tarefa>_copilot_meu_consumo` (ou `features/copilot_meu_consumo` sem número); `005-copilot-meu-consumo` é só o nome da pasta da spec | Atende |

**Emenda aplicada antes do plano**: a constituição passou de 3.1.0 para **3.2.0** em 2026-11-27, a pedido do dono do projeto. A emenda incluiu `Copilot:GitHub:Token` e `Copilot:OAuth:ClientSecret` nas exceções do Princípio III, que ficam no `appsettings`, e o PRD (RNF-05) foi alinhado. Sem ela, o Princípio III, que não admite exceção, reprovaria o gate.

Reavaliação depois da Fase 1: o modelo de dados, os contratos e o quickstart não introduzem estado persistente no servidor, escrita, origem nem pacote além dos já previstos. Os itens de operação são três, todos de fora do código e feitos antes da publicação:

- criar o token de leitura e a OAuth App;
- executar o script `002`;
- decidir quem entra no grupo `Copilot`.

**Gate aprovado.**

## Estrutura do projeto

### Documentação (esta feature)

```text
specs/005-copilot-meu-consumo/
├── plan.md              # este arquivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/           # Fase 1
│   ├── rotas-ui.md
│   ├── conexao-github.md
│   ├── github-api.md
│   ├── configuracao.md
│   └── usermanager.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks)

design/
└── + copilot.html       # contrato visual da página (primeira tarefa)
```

### Código-fonte (raiz do repositório)

Só o que esta feature cria (+) ou altera (~):

```text
dbchange/196.1.1/
└── + 002 - TCMRio - CSLIB_UserManager - Recurso CopilotProprio e grupo Copilot do SIAT.sql

src/
├── Tcmrj.Siat.Domain/
│   ├── Enums/~ Modulo.cs                    # + CopilotProprio
│   ├── Models/
│   │   ├── ~ Erros.cs                       # + Erros.Copilot (R9)
│   │   └── Copilot/
│   │       ├── + ConexaoGitHub.cs
│   │       ├── + Periodo.cs                 # validação, padrão, atalhos, dias, recente (R6)
│   │       ├── + Consumo.cs                 # Consumo, ConsumoPorModelo
│   │       ├── + RelatorioDeConsumo.cs      # normalização dos itens (R5)
│   │       ├── + ConsumoDoPeriodo.cs        # soma dos dias e mescla dos modelos
│   │       ├── + Budget.cs                  # + EscopoDeBudget
│   │       ├── + LimiteVigente.cs           # Calcular (individual, padrão, ambíguo, nenhum)
│   │       ├── + UsoDoLimite.cs             # faixas
│   │       └── + PessoaCopilot.cs           # + TimeDaPessoa, Licenca, Associacao, PerfilGitHub
│   └── Repositories/+ IGitHubCopilotRepository.cs
├── Tcmrj.Siat.Infra/
│   ├── Copilot/+ GitHubCopilotRepository.cs # HttpClient tipado, paginação, erros (contracts/github-api.md)
│   └── Configurations/
│       ├── + CopilotOptions.cs              # validação na leitura (contracts/configuracao.md)
│       └── + CopilotConfiguration.cs        # AddHttpClient, opções
├── Tcmrj.Siat.App/
│   └── Web/
│       ├── ~ Navegacao.cs                   # rotas e Copilot(login, de, ate)
│       ├── ~ Ferramentas.cs                 # grupo Gestão, ferramenta Copilot
│       ├── Features/Copilot/
│       │   ├── + ObterPessoaCopilot.cs      # Query(Atualizar) → PessoaCopilotVM; associação a cada carga, pessoa da carga compartilhada (R4)
│       │   ├── + ObterConsumoDoPeriodo.cs   # Query(De, Ate, Atualizar, Progresso) → ConsumoDoPeriodoVM
│       │   └── + ObterUsoDoLimite.cs        # Query(Atualizar) → UsoDoLimiteVM
│       ├── Services/
│       │   ├── ~ LeituraEmCache.cs          # + limpeza das entradas vencidas (R8)
│       │   ├── + ConsumoCopilotService.cs   # caches, regras de leitura, paralelismo (R7, R8)
│       │   ├── + ConexaoGitHubService.cs    # ConexaoGitHub? a partir do usuário do circuito
│       │   └── Interfaces/
│       │       ├── + IConsumoCopilotService.cs
│       │       └── + IConexaoGitHubService.cs
│       └── ViewModels/Copilot/
│           ├── + PessoaCopilotVM.cs
│           ├── + ConsumoDoPeriodoVM.cs      # + participação por modelo
│           ├── + UsoDoLimiteVM.cs
│           └── + SerieDeModelos.cs          # cinco + Outros (R5)
├── Tcmrj.Siat.Web/
│   ├── ~ Program.cs                         # AddCopilotConfiguration e os esquemas do GitHub
│   ├── ~ appsettings.json                   # seção Copilot
│   ├── Configurations/
│   │   ├── + CopilotGitHubConfiguration.cs  # esquemas GitHubOAuth e GitHubConexao, endpoints (contracts/conexao-github.md)
│   │   └── ~ RazorConfiguration.cs          # ConexaoGitHubMiddleware entre UseAuthentication e UseAuthorization; endpoints
│   ├── Extensions/~ ServiceCollectionExtensions.cs # IConexaoGitHubService, IConsumoCopilotService
│   ├── Services/
│   │   ├── + ConexaoGitHubMiddleware.cs     # segunda identidade no HttpContext.User (R3)
│   │   ├── + ValidacaoRetornoGitHub.cs      # OnCreatingTicket/OnRemoteFailure, testável
│   │   └── + RegistroConexaoGitHub.cs       # DBLog da conexão (R10)
│   ├── Components/
│   │   ├── Pages/Copilot/+ Copilot.razor    # rotas copilot e copilot/pessoas/{Login}
│   │   └── Shared/
│   │       ├── + FiltroDePeriodo.razor
│   │       ├── + ResumoNumerico.razor
│   │       ├── + BarraDeUso.razor
│   │       ├── + EvolucaoDiaria.razor       # SVG acessível (R11)
│   │       └── + TabelaDeModelos.razor
│   └── wwwroot/css/~ siat.css               # pessoa, resumo, barra de uso, evolução
└── Tcmrj.Siat.Test/
    ├── Features/~ Permissoes.feature        # + cenários do Copilot
    ├── Features/Steps/+ PermissoesDoCopilotSteps.cs
    ├── Componentes/
    │   ├── + CopilotPaginaTests.cs
    │   ├── + FiltroDePeriodoTests.cs
    │   ├── + BarraDeUsoTests.cs
    │   ├── + ResumoNumericoTests.cs
    │   ├── + EvolucaoDiariaTests.cs
    │   ├── + TabelaDeModelosTests.cs
    │   ├── ~ MenuTests.cs                   # grupo Gestão
    │   └── ~ HomeTests.cs                   # atalho do Copilot
    └── Unidade/
        ├── ~ LeituraEmCacheTests.cs         # limpeza
        ├── ~ NavegacaoTests.cs              # rotas do Copilot
        ├── ~ FerramentasTests.cs            # grupo Gestão
        └── Copilot/
            ├── + PeriodoTests.cs
            ├── + RelatorioDeConsumoTests.cs
            ├── + ConsumoDoPeriodoTests.cs
            ├── + LimiteVigenteTests.cs
            ├── + UsoDoLimiteTests.cs
            ├── + SerieDeModelosTests.cs
            ├── + GitHubCopilotRepositoryTests.cs
            ├── + CopilotOptionsTests.cs
            ├── + ConsumoCopilotServiceTests.cs
            ├── + ConexaoGitHubMiddlewareTests.cs
            ├── + ConexaoGitHubServiceTests.cs
            ├── + ValidacaoRetornoGitHubTests.cs
            ├── + RegistroConexaoGitHubTests.cs
            ├── + EndpointsGitHubTests.cs
            ├── + ObterPessoaCopilotHandlerTests.cs
            ├── + ObterConsumoDoPeriodoHandlerTests.cs
            └── + ObterUsoDoLimiteHandlerTests.cs
```

Documentação do projeto a atualizar na entrega: `README.md` (módulo e configuração), `.github/copilot-instructions.md` (Gestão › Copilot na visão geral) e `DESIGN.md` (componentes novos).

**Decisão de estrutura**: segue as camadas da 001 a 004.

- **`Domain`**: as regras do `gestao-copilot` (período, normalização do relatório, soma, limite, uso), puras.
- **`Infra`**: o acesso HTTP ao GitHub, com o mapeamento de erros.
- **`App`**: caches, paralelismo, autorização e a regra de "nunca receber login".
- **`Web`**: o OAuth, o cookie, o middleware, o log da conexão e a tela. O OAuth e o middleware rodam em requisições HTTP, fora do circuito, onde o `App` não tem usuário nem resposta para escrever cookie.

## Acompanhamento de complexidade

Nenhuma exceção à constituição. A única mudança de regra foi a emenda 3.2.0 (segredos do Copilot no `appsettings`), feita antes do plano e registrada no histórico da constituição.
