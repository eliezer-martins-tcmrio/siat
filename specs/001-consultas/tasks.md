---

description: "Lista de tarefas da feature 001 — Consultas (MailQueue e AuditLog)"
---

# Tarefas: Consultas — MailQueue e AuditLog

**Entrada**: documentos de design em `specs/001-consultas/`

**Pré-requisitos**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Testes**: incluídos. A constituição (Princípio V, "Testes como requisito de entrega") e o plano (R13) exigem Reqnroll para regras, testes de unidade, bUnit para componentes com lógica; os fluxos das histórias 1 a 4 ficam no roteiro manual do quickstart. Os testes de cada história são escritos antes da implementação e devem falhar primeiro.

**Organização**: as tarefas estão agrupadas por história, para que cada uma seja implementada, testada e entregue de forma independente.

## Formato: `[ID] [P?] [História] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem depender de tarefa incompleta)
- **[US1]…[US4]**: história da [spec.md](./spec.md) a que a tarefa pertence
- Todo caminho é relativo à raiz do repositório (`C:\Projetos\siat`)

## Convenções de caminho

Solução em `src/Siat.slnx`, com os projetos `Tcmrj.Siat.Domain`, `Tcmrj.Siat.Infra`, `Tcmrj.Siat.App`, `Tcmrj.Siat.Web`, e `Tcmrj.Siat.Test` (estrutura em [plan.md](./plan.md#código-fonte-raiz-do-repositório)). Scripts de banco em `dbchange/<versão>/`, onde `<versão>` segue o padrão `AAA.N.N` dos outros sistemas (por exemplo, o SiCATI usa `195.1.1`): use a versão da sprint atual.

Referências externas usadas nas tarefas:

- Adoção do pacote de autenticação: `C:\Projetos\sicati-controle-de-ativos` (commit `fce1433`)
- README do pacote `Tcmrj.Autenticacao.AspNetCore.Oidc`: `C:\Projetos\autenticacao\src\Tcmrj.Autenticacao.AspNetCore.Oidc\README.md`
- Projeto-modelo: `C:\Projetos\projeto-modelo` (template `tcmrj-modelo`)
- Telas de referência: `design/mailqueue.html`, `design/auditlog.html`, `design/siat.css`, `design/siat.js`, `DESIGN.md`

---

## Fase 1: Preparação (estrutura da solução)

**Objetivo**: criar a solução no padrão do projeto-modelo, com o `Web` em Blazor e os pacotes fixados.

- [X] T001 Gerar a solução na raiz com `dotnet new tcmrj-modelo -n Siat --IncluirExemplos false --IncluirApi false` (template `Tcmrj.Templates` do feed TCMRIO INTERNO), mantendo `.editorconfig` e `.gitignore` do modelo; converter a solução para `src/Siat.slnx` (`dotnet sln src/Siat.sln migrate`) e apagar o `.sln`
- [X] T002 Trocar o projeto MVC por Blazor Web App em `src/Tcmrj.Siat.Web/Tcmrj.Siat.Web.csproj`: remover `Controllers/` e `Views/` do modelo; criar `src/Tcmrj.Siat.Web/Program.cs` com `AddRazorComponents().AddInteractiveServerComponents()`, `AddRazorPages()`, `UseAuthentication()`, `UseAuthorization()`, `UseAntiforgery()`, `MapRazorPages()` e `MapRazorComponents<App>().AddInteractiveServerRenderMode()` (seção 2 do README do pacote Oidc)
- [X] T003 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*
- [X] T004 [P] Criar `global.json` na raiz com o SDK .NET 10 e o runner `Microsoft.Testing.Platform` (como o SiCATI) e `.gitattributes` com `*.sql eol=lf`
- [X] T005 [P] Criar `src/Directory.Build.props` com `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `AnalysisLevel 10.0`, `AnalysisMode Recommended` e `EnableNETAnalyzers true`, sem StyleCop ([research.md R14](./research.md#r14-versões-dos-pacotes-constituição-110-seção-dependências), nota StyleCop)
- [X] T006 Fixar os pacotes da tabela R14 nos `.csproj`: `src/Tcmrj.Siat.Web/Tcmrj.Siat.Web.csproj` (Tcmrj.Autenticacao.AspNetCore.Oidc 196.1.1, Tcmrj.Autorizacao.App 196.1.1, Microsoft.AspNetCore.Authentication.OpenIdConnect 10.0.15, Microsoft.AspNetCore.Authentication.JwtBearer 10.0.15, Microsoft.EntityFrameworkCore.SqlServer 10.0.15, Tcmrj.CoreLib.DBLog.AspNetCore 187.2.3, BlazorBlueprint.Components 4.2.0, BlazorBlueprint.Icons.Lucide 2.0.2), `src/Tcmrj.Siat.App/Tcmrj.Siat.App.csproj` (Tcmrj.CoreLib.DBLog 187.2.3, FluentValidation 12.2.0, NetDevPack.SimpleMediator 1.2.0), `src/Tcmrj.Siat.Infra/Tcmrj.Siat.Infra.csproj` (Microsoft.Data.SqlClient 7.1.1), `src/Tcmrj.Siat.Domain/Tcmrj.Siat.Domain.csproj` (Tcmrj.CSharpFunctionalExtensions 140.1.2), `src/Tcmrj.Siat.Test/Tcmrj.Siat.Test.csproj` (xunit.v3 4.0.3, AwesomeAssertions 9.6.0, Moq 4.21.0, Reqnroll.xunit.v3 3.3.4, bunit 2.15.2, coverlet.collector 10.1.0); conferir no feed se ainda são as últimas estáveis antes de fixar
- [X] T007 [P] Copiar os tokens e estilos de `design/siat.css` para `src/Tcmrj.Siat.Web/wwwroot/css/siat.css` e os logos de `design/img/` para `src/Tcmrj.Siat.Web/wwwroot/img/`
- [X] T008 Rodar `dotnet build src/Siat.slnx` e confirmar zero erros com as bibliotecas `net8.0` (`Tcmrj.Autorizacao.App`) numa aplicação `net10.0` (risco do plano)

**Checkpoint**: solução compila, vazia, com os seis projetos e as referências entre camadas do modelo (`Infra` → `Domain`; `App` → `Domain`; `Web` → `App`, `Domain`, `Infra`).

---

## Fase 2: Base (pré-requisitos de todas as histórias)

**Objetivo**: login, permissões, lista fechada de servidores, registro de consultas, layout, menu e componentes comuns.

**⚠️ CRÍTICO**: nenhuma história começa antes do fim desta fase.

### Cadastro no UserManager

- [X] T009 [P] Escrever `dbchange/<versão>/001 - TCMRio - CSLIB_UserManager - Criação de app, recursos e grupos do SIAT.sql`, idempotente, no padrão de `sicati-controle-de-ativos/dbchange/163.1.1/005`: `Apps` com `@AppId` fixo (conferir o maior `Id` em DSV e PRD; em DSV hoje é 177) e `Name = 'SIAT'`; `AppResources` `MailQueue`, `MailQueuePRD`, `AuditLog`, `AuditLogPRD`; `Groups` `Consultas` e `Consultas PRD`; `Accesses` com `CanSelect` conforme [contracts/usermanager.md](./contracts/usermanager.md) e demais permissões em 0
- [X] T010 [P] Escrever `dbchange/<versão>/002 - TCMRio - CSLIB_UserManager - OpenIddictApplications do SIAT.sql`, idempotente, no padrão de `sicati-controle-de-ativos/dbchange/163.1.1/006`: clientes `SIAT-dev` (`https://localhost:<porta>/signin-oidc`, `/signout-callback-oidc`) e `SIAT` (URL de PRD), `Type = confidential`, `Permissions = ["ept:authorization","ept:logout","ept:token","gt:authorization_code","rst:code","scp:profile","scp:openid"]`, `Requirements = ["ft:pkce"]`, `ClientSecret` só como hash do OpenIddict

### Domain

- [X] T011 [P] Criar os enums `Ambiente` (`Dsv`, `Hml`, `Prd`; descrição `DSV`/`HML`/`PRD`; `data-amb` `dsv`/`hml`/`prd`) e `Modulo` (`MailQueue`, `AuditLog`) em `src/Tcmrj.Siat.Domain/Enums/Ambiente.cs` e `src/Tcmrj.Siat.Domain/Enums/Modulo.cs`
- [X] T012 [P] Criar `src/Tcmrj.Siat.Domain/Models/Servidor.cs` com `Chave` ("obrigatória, única na lista, `^[a-z0-9-]{1,40}$`; é o único valor que vai na URL"), `Nome` ("obrigatório, até 60"), `Endereco` ("instância SQL Server (`MAQUINA\INSTANCIA`); nunca vem do usuário", até 128), `Ambiente`, `BancoMailQueue` (padrão `MailQueue`) e `BancoAuditoria` (padrão `AUDITORIADB`)
- [X] T013 [P] Criar `src/Tcmrj.Siat.Domain/Extensions/ModuloExtensions.cs` com `PoliticaPara(this Modulo, Ambiente)` (DSV/HML → `"<Modulo>.CanSelect"`, PRD → `"<Modulo>PRD.CanSelect"`) e `Politicas(this Modulo)` (as duas políticas do módulo), conforme [data-model.md](./data-model.md#modulo)
- [X] T014 [P] Criar `src/Tcmrj.Siat.Domain/Models/Erros.cs` com os `Erro` de `Tcmrj.CSharpFunctionalExtensions`: `Erros.Consultas.AcessoNegado`, `ServidorNaoEncontrado` ("Servidor não encontrado na lista"), `ServidorIndisponivel` (servidor e motivo), `TempoEsgotado` (servidor), `BancoInacessivel` (servidor e banco) e `Cancelada`
- [X] T015 [P] Criar `src/Tcmrj.Siat.Domain/Repositories/IServidoresRepository.cs` (`Listar()`, `ObterPorChave(string)`)

### Infra

- [X] T016 Criar `src/Tcmrj.Siat.Infra/Configurations/ServidoresOptions.cs` com validação na inicialização: lista inválida (chave repetida, campo ausente, chave fora do padrão) é recusada inteira, o erro é registrado no log e a aplicação sobe com lista vazia ([research.md R6](./research.md#r6-lista-fechada-de-servidores))
- [X] T017 [P] Criar `src/Tcmrj.Siat.Web/servidores.json` válido pelo [contracts/servidores.schema.json](./contracts/servidores.schema.json), com pelo menos um servidor de DSV e um de HML, carregado com `reloadOnChange` e lido via `IOptionsMonitor<ServidoresOptions>`
- [X] T018 Criar `src/Tcmrj.Siat.Infra/Configurations/ConsultasOptions.cs` (`CommandTimeout` padrão 60 s, `ConnectTimeout` 15 s) e `src/Tcmrj.Siat.Infra/Conexoes/FabricaDeConexao.cs`, que parte de `ConnectionStrings:Consultas` (`Integrated Security`, `TrustServerCertificate`, `Application Name=SIAT`) e preenche `DataSource` e `InitialCatalog` com `SqlConnectionStringBuilder` a partir do `Servidor` ([research.md R5](./research.md#r5-acesso-aos-bancos-consultados))
- [X] T019 [P] Criar `src/Tcmrj.Siat.Infra/Conexoes/PadraoLike.cs` com o escape para `LIKE`: `[` → `[[]`, `%` → `[%]`, `_` → `[_]`, resultado `%texto%`
- [X] T020 Criar `src/Tcmrj.Siat.Infra/Repositories/ServidoresRepository.cs` sobre `IOptionsMonitor<ServidoresOptions>`

### Autenticação e autorização (pacote `Tcmrj.Autenticacao.AspNetCore.Oidc`)

- [X] T021 Criar `src/Tcmrj.Siat.Web/Configurations/AuthenticationConfiguration.cs` com `AddTcmrjOidc(builder.Configuration)`, `AddTcmrjAutorizacaoBlazor(builder.Configuration)` (connection string padrão `UserManagerConnection`), `AddOptions<UserManagerServiceOptions>().Validate(o => o.ApplicationId > 0).ValidateOnStart()` e fallback policy de usuário autenticado; sem `AddOpenIddict` e sem segundo argumento de connection string ([research.md R2](./research.md#r2-autenticação-por-oidc-tcmrjautenticacaoaspnetcoreoidc)); chamar a partir do `src/Tcmrj.Siat.Web/Program.cs`
- [X] T022 [P] Preencher `src/Tcmrj.Siat.Web/appsettings.json` (`TcmrjOidc:Authority = https://tcmrj.tcmnet.tcm/scaf`, `TcmrjOidc:ClientId = SIAT`, `RequireHttpsMetadata = true`, `UserManagerService:ApplicationId`, `TcmrjSessao` 30/1, `ConnectionStrings:UserManagerConnection`, `AuditoriaDbConnection`, `Consultas`, `ConsultasOptions`) e `src/Tcmrj.Siat.Web/appsettings.Development.json` (`ClientId = SIAT-dev`, `Authority` do SIA de desenvolvimento); nenhum `ClientSecret` nos dois arquivos; `UserSecretsId` no `.csproj` do Web
- [X] T023 [P] Criar `src/Tcmrj.Siat.Web/Pages/Account/Login.cshtml` (challenge em `TcmrjOidcDefaults.OidcScheme`, só `returnUrl` local) e `src/Tcmrj.Siat.Web/Pages/Account/Logout.cshtml` (GET → `LocalRedirect("~/")`; POST com antiforgery → sign-out de `OidcScheme` e de `CookieScheme`), conforme a seção 4 do README do pacote e [contracts/rotas-ui.md](./contracts/rotas-ui.md)
- [X] T024 [P] Criar `src/Tcmrj.Siat.Web/Components/EncerrarSessao.razor` (formulário POST para `Account/Logout` com `<AntiforgeryToken />`, enviado uma vez em `OnAfterRenderAsync(firstRender)`), conforme a seção 5 do README do pacote
- [X] T025 Criar `src/Tcmrj.Siat.Web/Components/Routes.razor` com `AuthorizeRouteView` (layout `MainLayout`) e `<NotAuthorized><EncerrarSessao /></NotAuthorized>`
- [X] T026 [P] Criar `src/Tcmrj.Siat.Web/Services/Interacao.cs`, serviço scoped que encapsula `ITcmrjInatividade.Interagir()` para as páginas chamarem ao consultar, filtrar, abrir item e exportar (risco "Inatividade numa página só" do plano)

### Registro de consultas e autorização por consulta

- [X] T027 Criar `src/Tcmrj.Siat.Web/Configurations/DBLogConfiguration.cs` com `UseTcmrjDBLog(appId)` gravando em `ConnectionStrings:AuditoriaDbConnection` e `AppID` = `UserManagerService:ApplicationId` ([research.md R7](./research.md#r7-registro-das-consultas-no-log-tcmrjcorelibdblog))
- [X] T028 Criar `src/Tcmrj.Siat.App/Web/Services/Interfaces/IRegistroConsultaService.cs` e `src/Tcmrj.Siat.App/Web/Services/RegistroConsultaService.cs` com `LogErrorWithLogUID(10, "{Operacao} {Modulo} em {ServidorNome} ({Ambiente}) por {Matricula}", ...)`, em que `{Operacao}` é `Consulta` ou `Exportação` na mensagem, e as propriedades `Operacao`, `Modulo`, `Servidor`, `ServidorNome`, `Ambiente`, `Matricula`, `Usuario`, `Filtros`, `Itens`, `Resultado` (`Sucesso`, `Cancelada`, `Erro`, `AcessoNegado`) de [contracts/registro-consulta.md](./contracts/registro-consulta.md); nunca registra corpo de e-mail nem conteúdo de log; falha ao gravar não chega ao usuário
- [X] T029 Criar `src/Tcmrj.Siat.App/Web/Services/Interfaces/IAutorizacaoConsultaService.cs` e `src/Tcmrj.Siat.App/Web/Services/AutorizacaoConsultaService.cs`: resolve o servidor pela chave (nunca pelo endereço), calcula `Modulo.PoliticaPara(Ambiente)`, chama `IAuthorizationService.AuthorizeAsync` com o usuário do circuito sem cache; negado devolve `Erros.Consultas.AcessoNegado`; chave inexistente devolve `ServidorNaoEncontrado` ([research.md R4](./research.md#r4-autorização-por-ambiente-do-servidor)). **Não grava no log**: o registro de consulta é feito só pelo handler da feature, uma vez por tentativa, inclusive negada (T090)
- [X] T030 Criar `src/Tcmrj.Siat.App/Web/Features/Servidores/ListarPermitidos.cs` (Query/QueryHandler) que filtra a lista fechada pelas políticas do módulo pedidas e devolve `ServidorVM` agrupados por ambiente, e `src/Tcmrj.Siat.App/Web/ViewModels/Shared/ServidorVM.cs` e `ResultadoConsultaVM.cs`
- [X] T031 Criar `src/Tcmrj.Siat.Web/Extensions/ServiceCollectionExtensions.cs` com `AddTcmrjSiat` (SimpleMediator com o mediador autorizado do modelo, validadores FluentValidation, repositórios, `FabricaDeConexao`, services do App, `Interacao`, options de servidores e consultas)

### Layout, menu e componentes comuns

- [X] T032 Criar `src/Tcmrj.Siat.Web/Components/App.razor` com `InteractiveServerRenderMode(prerender: false)`, script inline no `<head>` que aplica o tema do `localStorage` antes da primeira pintura, `wwwroot/css/siat.css`, `wwwroot/js/siat.js` e `src/Tcmrj.Siat.Web/Components/_Imports.razor`
- [X] T033 Mapear os tokens de `wwwroot/css/siat.css` nas variáveis de tema do Blazor Blueprint em `src/Tcmrj.Siat.Web/wwwroot/css/blueprint-tema.css` (claro e escuro), e registrar o Blueprint em `src/Tcmrj.Siat.Web/Configurations/RazorConfiguration.cs`
- [X] T034 Criar `src/Tcmrj.Siat.Web/Components/Layout/MainLayout.razor` (estrutura de `design/index.html`; `ITcmrjInatividade.Interagir()` em `NavigationManager.LocationChanged`, com `IDisposable`), `Marca.razor` e `ReconexaoModal.razor` (`components-reconnect-modal` em português)
- [X] T035 Criar `src/Tcmrj.Siat.Web/Components/Layout/Menu.razor` com a seção **Consultas** e os itens **MailQueue** e **AuditLog**, cada um visível com qualquer das duas políticas do módulo (`Modulo.Politicas()`), e a seção oculta quando nenhum item aparece (FR-003)
- [X] T036 Criar `src/Tcmrj.Siat.Web/Components/Layout/RodapeMenu.razor` com `SeletorTema`, o nome do usuário (`GetUserName()`, tratando `null`) e **Sair** como `<form action="Account/Logout" method="post">` com `<AntiforgeryToken />`; e `src/Tcmrj.Siat.Web/Components/Shared/SeletorTema.razor` (claro, escuro, sistema, no `localStorage`)
- [X] T037 [P] Criar `src/Tcmrj.Siat.Web/Components/Shared/CabecalhoPagina.razor`, `EstadoErro.razor` (servidor e motivo), `Aviso.razor`, `BotaoCopiar.razor` e `BlocoCodigo.razor` (bloco escuro nos dois temas, com Copiar), seguindo `DESIGN.md`
- [X] T038 [P] Criar `src/Tcmrj.Siat.Web/Components/Shared/SeletorServidor.razor`: lista só os `ServidorVM` permitidos, agrupados por ambiente, sem campo livre; com a lista fechada vazia ou inválida (R6), mostra "Não há servidores configurados." no lugar do seletor; e `src/Tcmrj.Siat.Web/Components/Shared/FaixaContexto.razor` com servidor e ambiente (`data-amb`) em DSV/HML
- [X] T039 [P] Criar `src/Tcmrj.Siat.Web/Components/Shared/ListaExpansivel.razor` genérica: linhas com colunas por template, detalhe renderizado só quando a linha é aberta (R10), item aberto exposto para o `sel` da URL; e `EstadoCarregando.razor` e `EstadoVazio.razor` básicos
- [X] T040 Criar `src/Tcmrj.Siat.Web/Components/Pages/Home.razor` (vai para a primeira ferramenta permitida, MailQueue e depois AuditLog; sem nenhuma, "Você não tem acesso às ferramentas do SIAT" com a orientação de pedir o grupo `Consultas` no SIA, FR-006) e `src/Tcmrj.Siat.Web/Components/Pages/AcessoNegado.razor`
- [X] T041 Criar `src/Tcmrj.Siat.App/Web/Navegacao.cs` com a montagem das URLs de `/consultas/mailqueue` e `/consultas/auditlog` e seus parâmetros ([contracts/rotas-ui.md](./contracts/rotas-ui.md#parâmetros-de-consulta))

### Testes da base

- [X] T042 [P] Escrever `src/Tcmrj.Siat.Test/Features/Permissoes.feature` e `src/Tcmrj.Siat.Test/Features/Steps/PermissoesSteps.cs` (Reqnroll): a tabela de `PoliticaPara` para os 2 módulos × 3 ambientes; `Politicas()` devolve as duas; o nome é igual ao de `Politica.Para(recurso, AccessType.CanSelect)` de `Tcmrj.Autorizacao.App.AspNetCore`
- [X] T043 [P] Escrever `src/Tcmrj.Siat.Test/Unidade/AutorizacaoDependencyInjectionTests.cs` no padrão de `sicati-controle-de-ativos/src/Tcmrj.SiCATI.Test/AuthorizationDependencyInjectionTests.cs`: a cadeia resolve dentro de um escopo (`IAuthorizationService`, `PermissionHandler`, `IUserManagerService` é `TcmrjBlazorUserManagerService`, `AuthenticationStateProvider` é o mesmo objeto que `ITcmrjInatividade`) e não resolve no provedor raiz; e que a aplicação não sobe com `ApplicationId <= 0`
- [X] T044 [P] Escrever `src/Tcmrj.Siat.Test/Unidade/ClaimsDoSiaTests.cs`, como `ClaimsDaBibliotecaTests` do SiCATI: `email`, `matricula` e `sessionId` lidos por `Tcmrj.Autenticacao.AspNetCore.Extensions`; nome ausente volta `null`
- [X] T045 [P] Escrever `src/Tcmrj.Siat.Test/Unidade/ServidoresOptionsTests.cs`: lista válida carrega; chave repetida, campo ausente ou chave fora de `^[a-z0-9-]{1,40}$` recusa a lista inteira
- [X] T046 [P] Escrever `src/Tcmrj.Siat.Test/Unidade/PadraoLikeTests.cs`: `[teste]_%` vira `%[[]teste][_][%]%`; texto comum vira `%texto%`
- [X] T047 [P] Escrever `src/Tcmrj.Siat.Test/Unidade/AutorizacaoConsultaServiceTests.cs` com `IAuthorizationService` e `IRegistroConsultaService` simulados: negado devolve `AcessoNegado` e não chama `IRegistroConsultaService` (quem registra é o handler); chave inexistente devolve `ServidorNaoEncontrado`; permissão retirada entre duas chamadas nega a segunda (sem cache)
- [X] T048 [P] Escrever `src/Tcmrj.Siat.Test/Unidade/ListarPermitidosTests.cs`: usuário só com o recurso comum não recebe servidores de PRD; usuário com os dois recebe todos, agrupados por ambiente
- [X] T049 [P] Escrever `src/Tcmrj.Siat.Test/Componentes/MenuTests.cs` (bUnit, com `TestAuthorizationContext`): seção oculta sem recursos; só MailQueue com `MailQueue.CanSelect`; item visível também com só o recurso de PRD
- [X] T050 [P] Escrever `src/Tcmrj.Siat.Test/Componentes/SeletorServidorTests.cs` (bUnit): agrupa por ambiente; não mostra servidores de PRD que não vieram da feature; não tem campo de texto livre; lista vazia mostra "Não há servidores configurados."
- [X] T051 [P] Escrever `src/Tcmrj.Siat.Test/Componentes/ListaExpansivelTests.cs` (bUnit): o detalhe só é renderizado depois de abrir a linha; `sel` de um item que não está no resultado é ignorado; `sel` existente abre a linha
- [X] T052 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*

**Checkpoint**: login no SIA de desenvolvimento funciona (quickstart, "Rodar"), o menu respeita as permissões, Sair encerra a sessão e todos os testes da base passam.

---

## Fase 3: História 1 — Consultar e-mails enfileirados em DSV/HML (Prioridade: P1) 🎯 MVP

**Objetivo**: tela **Consultas › MailQueue** com seletor de servidor, filtros por SMTP, texto e quantidade, lista do mais recente para o mais antigo, detalhe com corpo isolado e URL compartilhável.

**Teste independente**: com um usuário que tem só `MailQueue.CanSelect`, consultar um servidor de HML com e sem filtros e conferir que os resultados batem com os dados do banco para os mesmos filtros (cenários 2 a 6 do [quickstart.md](./quickstart.md)).

### Testes da História 1 ⚠️

> Escrever primeiro e confirmar que falham.

- [X] T053 [P] [US1] Escrever `src/Tcmrj.Siat.Test/Features/FiltrosMailQueue.feature` e `src/Tcmrj.Siat.Test/Features/Steps/FiltrosMailQueueSteps.cs`: `Texto` "aparado; vazio = sem filtro; até 500 caracteres"; `Quantidade` "1 a 1000, padrão 50"; não numérica é recusada com mensagem; numérica fora do limite é ajustada ao limite mais próximo (`Math.Clamp`); `SmtpId` nulo = todos
- [X] T054 *(removida em 2026-11-18: o SQL embutido não é mais comparado com o de outra ferramenta)*
- [X] T055 [P] [US1] Escrever `src/Tcmrj.Siat.Test/Unidade/MailQueue/PesquisarHandlerTests.cs` com repositório falso: autoriza antes de consultar; registra `Consulta` com `Sucesso` e `Itens`; erro do repositório vira `EstadoErro` com servidor e motivo e registra `Erro`; parâmetros chegam com `PadraoLike` aplicado
- [X] T056 [P] [US1] Escrever `src/Tcmrj.Siat.Test/Unidade/MailQueue/SituacaoEnvioTests.cs`: `Enviado` → "Enviado em {DataEnvio}"; não enviado com `EnviarApos` futuro → "Agendado"; senão "Na fila"
- [X] T057 [P] [US1] Escrever `src/Tcmrj.Siat.Test/Componentes/CorpoEmailTests.cs` (bUnit): `iframe` com `sandbox=""` e `srcdoc` iniciado pela meta CSP `default-src 'none'; img-src data:; style-src 'unsafe-inline'`; texto simples escapado dentro de `<pre>`
- [X] T058 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*

### Implementação da História 1

- [X] T059 [P] [US1] Criar `src/Tcmrj.Siat.Domain/Models/Smtp.cs` (`SmtpId` int, `Descricao` string) e `src/Tcmrj.Siat.Domain/Models/MensagemEnfileirada.cs` com os campos de [data-model.md](./data-model.md#mensagemenfileirada): `MessageId` "string (36)", `Assunto` string?, `DataPostagem`, `EnviarApos` DateTime?, `DataEnvio` DateTime?, `Enviado` bool (`ISNULL(Sent, 0)`), `EhHtml`, `Corpo` string?, `Destinatarios` (ou `(sem destinatários)`)
- [X] T060 [P] [US1] Criar `src/Tcmrj.Siat.Domain/Models/FiltroMailQueue.cs` (objeto de valor com as regras de [data-model.md](./data-model.md#filtromailqueue-objeto-de-valor)) e `src/Tcmrj.Siat.Domain/Repositories/IMailQueueRepository.cs` (`ListarSmtpsAsync(Servidor, CancellationToken)`, `PesquisarAsync(Servidor, FiltroMailQueue, CancellationToken)`)
- [X] T061 [P] [US1] Escrever a consulta de mensagens e `SELECT SmtpId, Description FROM dbo.SMTPs ORDER BY SmtpId` em `src/Tcmrj.Siat.Infra/Sql/mensagens.sql` e `src/Tcmrj.Siat.Infra/Sql/smtps.sql`, com quebras LF, como `EmbeddedResource` no `src/Tcmrj.Siat.Infra/Tcmrj.Siat.Infra.csproj`
- [X] T062 [US1] Criar `src/Tcmrj.Siat.Infra/Repositories/MailQueueRepository.cs` com ADO.NET: `SqlParameter` tipado (`@Top int`, `@SmtpId int`, `@Texto nvarchar(max)` com `PadraoLike`), `CommandTimeout` de `ConsultasOptions`, `CancellationToken` em `OpenAsync`, `ExecuteReaderAsync` e `ReadAsync`; erros de conexão, tempo e permissão convertidos nos `Erros.Consultas` com servidor e banco
- [X] T063 [P] [US1] Criar `src/Tcmrj.Siat.App/Web/ViewModels/MailQueue/SmtpVM.cs`, `MensagemVM.cs` (com `SituacaoEnvio` derivada) e `FiltroMailQueueVM.cs`
- [X] T064 [US1] Criar `src/Tcmrj.Siat.App/Web/Validators/Mensagens.cs` (textos em pt-BR) e `src/Tcmrj.Siat.App/Web/Validators/PesquisarMailQueueQueryValidator.cs` (servidor obrigatório, texto até 500, quantidade numérica)
- [X] T065 [US1] Criar `src/Tcmrj.Siat.App/Web/Features/MailQueue/ListarSmtps.cs` (autoriza via `IAutorizacaoConsultaService`; indisponível devolve lista vazia com aviso)
- [X] T066 [US1] Criar `src/Tcmrj.Siat.App/Web/Features/MailQueue/Pesquisar.cs` (Query, QueryHandler, validação; autoriza, consulta, registra `Consulta` com o `Resultado` e os `Filtros`; devolve `Result<ResultadoConsultaVM<MensagemVM>, Erro>`)
- [X] T067 [P] [US1] Criar `src/Tcmrj.Siat.Web/Components/Shared/CorpoEmail.razor`: `<iframe sandbox="" srcdoc>` com a meta CSP de [research.md R10](./research.md#r10-corpo-do-e-mail-isolado) no início, altura fixa com redimensionamento; `EhHtml = false` vai escapado em `<pre>`
- [X] T068 [US1] Criar `src/Tcmrj.Siat.Web/Components/Pages/Consultas/MailQueue.razor` (`@page "/consultas/mailqueue"`), seguindo `design/mailqueue.html`. **Colunas da linha**, nesta ordem: Situação (pílula Enviado, Agendado ou Na fila); Assunto (ou "(sem assunto)"); Para (primeiro destinatário e "+N" dos demais, ou "(sem destinatários)"; oculta em tela estreita); Postado em (relativo, com a data completa no `title`, alinhado à direita). **Campos do detalhe**: Assunto; Para (todos os destinatários); Situação; Postado em; Enviar após; Data de envio (ou "não enviado"); MessageId (monoespaçado, com Copiar); Corpo (`CorpoEmail`). O filtro local procura em assunto, destinatários, MessageId e corpo sem tags. Comportamento: verifica no `OnInitializedAsync` se o usuário tem uma das políticas do módulo (senão `AcessoNegado`); `[SupplyParameterFromQuery]` para `servidor`, `smtp`, `texto`, `qtd`, `sel`; ao escolher servidor, carrega SMTPs e a consulta da URL em paralelo; ao consultar, atualiza a URL com `Navegacao` (`replace: false`); estados `Ociosa`/`Consultando`/`ComResultado`/`ComErro` de [data-model.md](./data-model.md#estado-da-tela-web-não-persistido) com "vale só a última"; `sel` só aplicado se o item existir; `FaixaContexto` com o servidor; chama `Interacao` ao consultar e ao abrir item
- [X] T069 [US1] Rodar `dotnet test src/Tcmrj.Siat.Test` e confirmar T053–T057 passando, com o item **MailQueue** do menu (T035) e o encaminhamento da `src/Tcmrj.Siat.Web/Components/Pages/Home.razor` (T040) levando a `/consultas/mailqueue`

**Checkpoint**: História 1 funcional e testável sozinha. Validar os cenários 2 a 6 do quickstart em um servidor de HML.

---

## Fase 4: História 2 — Consultar o log de auditoria em DSV/HML (Prioridade: P2)

**Objetivo**: tela **Consultas › AuditLog** com seletor de servidor, aplicação (ou campo numérico quando a lista não carrega), prefixo de LogID e quantidade, com stack trace e LogEvent formatados.

**Teste independente**: com um usuário que tem só `AuditLog.CanSelect`, consultar uma aplicação em HML e buscar um LogID conhecido, comparando com o `AUDITORIADB` (cenários 7 a 10 do quickstart).

### Testes da História 2 ⚠️

- [X] T070 [P] [US2] Escrever `src/Tcmrj.Siat.Test/Features/FiltrosAuditLog.feature` e `src/Tcmrj.Siat.Test/Features/Steps/FiltrosAuditLogSteps.cs`: `PrefixoLogId` `^\d{1,20}$`, outro conteúdo recusado com "Informe apenas dígitos no LogID." e sem consulta; `Quantidade` "1 a 1000, padrão 50" com a mesma regra de ajuste; `AppId` nulo = todas
- [X] T071 [P] [US2] Escrever `src/Tcmrj.Siat.Test/Unidade/AuditLog/FormatadorStackTraceTests.cs` (quebra por linha, realce das linhas `at ...` e da linha da mensagem) e `src/Tcmrj.Siat.Test/Unidade/AuditLog/FormatadorLogEventTests.cs` (JSON válido indentado com realce por tipo de token; inválido devolve o texto original sem exceção)
- [X] T072 *(removida em 2026-11-18: o SQL embutido não é mais comparado com o de outra ferramenta)*
- [X] T073 [P] [US2] Escrever `src/Tcmrj.Siat.Test/Unidade/AuditLog/PesquisarHandlerTests.cs` e `ListarAplicacoesHandlerTests.cs` com repositório falso: autoriza antes; registra `Consulta`; lista de aplicações indisponível devolve vazia com aviso
- [X] T074 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*

### Implementação da História 2

- [X] T075 [P] [US2] Criar `src/Tcmrj.Siat.Domain/Models/Aplicacao.cs` (`Id` int, `Nome` string) e `src/Tcmrj.Siat.Domain/Models/RegistroAuditoria.cs` com `LogId` long, `TipoId` byte, `Tipo` (ou `(tipo não cadastrado)`), `Data`, `Mensagem` string?, `LogUid` string?, `AppId` int, `Excecao` string?, `LogEvent` string?
- [X] T076 [P] [US2] Criar `src/Tcmrj.Siat.Domain/Models/FiltroAuditLog.cs` com as regras de [data-model.md](./data-model.md#filtroauditlog-objeto-de-valor) e `src/Tcmrj.Siat.Domain/Repositories/IAuditLogRepository.cs` (`ListarAplicacoesAsync`, `PesquisarAsync`, com `CancellationToken`)
- [X] T077 [P] [US2] Escrever a consulta de logs e a de aplicações em `src/Tcmrj.Siat.Infra/Sql/logs.sql` e `src/Tcmrj.Siat.Infra/Sql/apps.sql`, em LF, como `EmbeddedResource`
- [X] T078 [US2] Criar `src/Tcmrj.Siat.Infra/Repositories/AuditLogRepository.cs` com `@Top int`, `@AppID int`, `@LogID varchar(20)`, timeout e cancelamento como em T062; falha de acesso ao `CSLIB_UserManager` na lista de aplicações devolve indisponível sem derrubar a consulta de logs
- [X] T079 [P] [US2] Criar `src/Tcmrj.Siat.App/Web/Services/FormatadorStackTrace.cs` e `src/Tcmrj.Siat.App/Web/Services/FormatadorLogEvent.cs` (`JsonDocument.Parse` + `JsonSerializer` indentado; inválido devolve o original), com interfaces em `src/Tcmrj.Siat.App/Web/Services/Interfaces/`
- [X] T080 [P] [US2] Criar `src/Tcmrj.Siat.App/Web/ViewModels/AuditLog/AplicacaoVM.cs`, `RegistroAuditoriaVM.cs` e `FiltroAuditLogVM.cs`
- [X] T081 [US2] Criar `src/Tcmrj.Siat.App/Web/Validators/PesquisarAuditLogQueryValidator.cs` (mensagem "Informe apenas dígitos no LogID.") e acrescentar os textos a `src/Tcmrj.Siat.App/Web/Validators/Mensagens.cs`
- [X] T082 [US2] Criar `src/Tcmrj.Siat.App/Web/Features/AuditLog/ListarAplicacoes.cs` e `src/Tcmrj.Siat.App/Web/Features/AuditLog/Pesquisar.cs` (autoriza, consulta, registra, devolve `Result`)
- [X] T083 [US2] Criar `src/Tcmrj.Siat.Web/Components/Pages/Consultas/AuditLog.razor` (`@page "/consultas/auditlog"`), seguindo `design/auditlog.html`. **Colunas da linha**, nesta ordem: Tipo (etiqueta com a cor do nível); LogID (monoespaçado; oculta em tela estreita); Mensagem (pílula "exceção" quando há `Excecao`, e a mensagem cortada em 300 caracteres com "…"); Aplicação (nome, ou "AppID N" quando não há nome; oculta em tela estreita); Data (relativa, com a data completa no `title`, alinhada à direita). **Campos do detalhe**: Mensagem completa (ou "(sem mensagem)"); Tipo (etiqueta e "TypeID N"); Data; Aplicação (nome e "(AppID N)"); LogID (com Copiar); LogUID (monoespaçado); Exceção (stack trace formatado em bloco escuro, com Copiar, só quando houver); LogEvent (JSON formatado, com Copiar). O filtro local procura em LogID, mensagem, tipo, AppID e nome da aplicação. Comportamento: mesma estrutura de T068 com `servidor`, `app`, `logid`, `qtd`, `sel`; lista de aplicações indisponível troca o seletor por campo numérico com aviso; detalhe com `BlocoCodigo` para stack trace e LogEvent formatados; chama `Interacao` ao consultar e ao abrir item
- [X] T084 [US2] Rodar `dotnet test src/Tcmrj.Siat.Test` e confirmar T070–T073 passando, com o item **AuditLog** do menu (T035) e a `src/Tcmrj.Siat.Web/Components/Pages/Home.razor` (T040) levando a `/consultas/auditlog` quando o usuário não tiver MailQueue

**Checkpoint**: Histórias 1 e 2 funcionam cada uma sozinha. Validar os cenários 7 a 10 do quickstart.

---

## Fase 5: História 3 — Consultar dados de PRD com permissão própria (Prioridade: P3)

**Objetivo**: servidores de PRD só para quem tem `MailQueuePRD.CanSelect` / `AuditLogPRD.CanSelect`, negação também pela URL, faixa de PRD destacada e registro de toda consulta.

**Teste independente**: com um usuário só com `MailQueue.CanSelect` e outro com `MailQueue.CanSelect` + `MailQueuePRD.CanSelect`, conferir que só o segundo vê e consulta servidores de PRD, inclusive pela URL (cenários 11 a 13 do quickstart).

### Testes da História 3 ⚠️

- [X] T085 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*
- [X] T086 [P] [US3] Escrever `src/Tcmrj.Siat.Test/Unidade/RegistroConsultaServiceTests.cs` com `ILogger` simulado: cada `Resultado` (`Sucesso`, `Cancelada`, `Erro`, `AcessoNegado`) gera um evento `TypeID 10` com `Operacao`, `Modulo`, `Servidor`, `ServidorNome`, `Ambiente`, `Matricula`, `Usuario`, `Filtros`, `Itens`; nunca inclui corpo de e-mail nem conteúdo de log; exceção do logger não se propaga
- [X] T087 [P] [US3] Escrever `src/Tcmrj.Siat.Test/Componentes/FaixaContextoTests.cs` (bUnit): em PRD, `data-amb="prd"`, estilo destacado e o aviso "consulta registrada em log"; em DSV/HML, sem o aviso

### Implementação da História 3

- [X] T088 [US3] Completar `src/Tcmrj.Siat.Web/Components/Shared/FaixaContexto.razor` com a variante sólida de PRD e o aviso de registro em log (FR-009), conforme `DESIGN.md`, seção Ambientes
- [X] T089 [US3] Completar `src/Tcmrj.Siat.Web/Components/Shared/SeletorServidor.razor` com o grupo de PRD destacado e conferir em `src/Tcmrj.Siat.Web/Components/Pages/Consultas/MailQueue.razor` e `AuditLog.razor` que uma chave de PRD vinda da URL sem permissão mostra a mensagem de acesso negado, sem consulta, e que nenhum dado anterior fica visível
- [X] T090 [US3] Revisar `src/Tcmrj.Siat.App/Web/Features/MailQueue/Pesquisar.cs`, `src/Tcmrj.Siat.App/Web/Features/AuditLog/Pesquisar.cs` e as features de lista (SMTPs, aplicações) para que toda tentativa que passou da validação seja registrada, inclusive negada, cancelada ou com erro (SC-005, SC-006, [contracts/registro-consulta.md](./contracts/registro-consulta.md))
- [X] T091 [US3] Manter os servidores de PRD fora do `src/Tcmrj.Siat.Web/servidores.json` de desenvolvimento; documentar no `README.md` da raiz um exemplo de entrada de PRD (`"Ambiente": "PRD"`) para o arquivo do servidor de publicação; rodar T085–T087, que usam uma lista de teste com PRD

**Checkpoint**: PRD só aparece e só é consultado com o recurso de PRD; toda consulta aparece no AuditLog do SIAT. A liberação em PRD aguarda a questão em aberto 4 do PRD.

---

## Fase 6: História 4 — Acompanhar, cancelar, filtrar e exportar (Prioridade: P3)

**Objetivo**: tempo decorrido e Cancelar (interrompendo no banco), filtro local com `/`, navegação por teclado, exportação em HTML autocontido e "Remover filtros".

**Teste independente**: fazer uma consulta lenta e cancelá-la; filtrar uma lista carregada; exportar e abrir o arquivo sem o SIAT (cenários 14 a 16 do quickstart).

### Testes da História 4 ⚠️

- [X] T092 [P] [US4] Escrever `src/Tcmrj.Siat.Test/Unidade/FiltroLocalTests.cs`: normalização com `CompareInfo.IndexOf(..., IgnoreCase | IgnoreNonSpace)` em pt-BR; vários termos; contador "X de Y"
- [X] T093 [P] [US4] Escrever `src/Tcmrj.Siat.Test/Unidade/ExportacaoHtmlServiceTests.cs` e `src/Tcmrj.Siat.Test/Unidade/ExportarHandlerTests.cs`: a exportação autoriza de novo no servidor e, negada, não gera arquivo e registra `Exportacao` com `AcessoNegado`; nome `mailqueue_AAAAMMDD_HHMMSS.html` / `auditlog_AAAAMMDD_HHMMSS.html`; nenhum `src`/`href` externo; cabeçalho com módulo, servidor, ambiente (PRD destacado), filtros, data e quem exportou; corpo em `iframe sandbox` com a CSP de R10; AuditLog já formatado; filtro local pré-preenchido
- [X] T094 [P] [US4] Escrever `src/Tcmrj.Siat.Test/Componentes/EstadoCarregandoTests.cs` (bUnit, `TimeProvider` falso): tempo decorrido e **Cancelar** só depois de 1 s; e `src/Tcmrj.Siat.Test/Componentes/EstadoVazioTests.cs`: "Remover filtros" só com filtros preenchidos
- [X] T095 [P] [US4] Escrever `src/Tcmrj.Siat.Test/Unidade/CancelamentoTests.cs`: nova consulta cancela a anterior e só a última é aplicada; cancelar mantém o resultado anterior e registra `Cancelada`
- [X] T096 [P] [US4] Escrever `src/Tcmrj.Siat.Test/Componentes/FiltroLocalComponentTests.cs` (bUnit): o contador "X de Y" acompanha o texto digitado; `Esc` limpa o filtro e volta a mostrar todos os itens
- [X] T097 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*

### Implementação da História 4

- [X] T098 [US4] Completar `src/Tcmrj.Siat.Web/Components/Shared/EstadoCarregando.razor` com tempo decorrido e **Cancelar** após 1 s (injetando `TimeProvider`) e `EstadoVazio.razor` com "Remover filtros"
- [X] T099 [US4] Aplicar o cancelamento em `src/Tcmrj.Siat.Web/Components/Pages/Consultas/MailQueue.razor` e `AuditLog.razor`: um `CancellationTokenSource` por consulta, cancelado por Cancelar, por nova consulta e no `Dispose`; o token chega ao SqlClient (SC-004); cancelar volta ao estado anterior
- [X] T100 [P] [US4] Criar `src/Tcmrj.Siat.App/Web/Services/FiltroLocal.cs` e `src/Tcmrj.Siat.Web/Components/Shared/FiltroLocal.razor` (contador "X de Y"; `Esc` limpa), usados nas duas páginas sobre os itens já carregados; chamar `Interacao` ao filtrar
- [X] T101 [P] [US4] Criar `src/Tcmrj.Siat.Web/wwwroot/js/siat.js` com o atalho `/` (fora de campos de texto) para focar o filtro, `↑`/`↓` entre linhas e `Enter`/`Espaço` para abrir ou fechar, só movendo o foco ([contracts/rotas-ui.md](./contracts/rotas-ui.md#atalhos-de-teclado)), e o download de arquivo via `DotNetStreamReference`
- [X] T102 [US4] Criar `src/Tcmrj.Siat.App/Web/Services/Interfaces/IExportacaoHtmlService.cs` e `src/Tcmrj.Siat.App/Web/Services/ExportacaoHtmlService.cs` conforme [contracts/exportacao-html.md](./contracts/exportacao-html.md) (CSS e script mínimo de filtro e expansão embutidos)
- [X] T103 [US4] Criar `src/Tcmrj.Siat.App/Web/Features/MailQueue/Exportar.cs` e `src/Tcmrj.Siat.App/Web/Features/AuditLog/Exportar.cs`: autorizam de novo pelo servidor da consulta com `IAutorizacaoConsultaService` antes de montar o arquivo (constituição, Princípio III: toda operação verificada no servidor); negado devolve `AcessoNegado` sem arquivo; montam o arquivo a partir dos itens carregados e registram `Exportacao` com `Itens` e `Resultado`
- [X] T104 [US4] Ligar o botão **Exportar** em `src/Tcmrj.Siat.Web/Components/Pages/Consultas/MailQueue.razor` e `AuditLog.razor` (download pelo `siat.js`, `Interacao` ao exportar) e rodar T092–T097

**Checkpoint**: todas as histórias funcionam cada uma sozinha.

---

## Fase 7: Acabamento e pontos transversais

- [X] T105 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*
- [X] T106 *(removida em 2026-11-18 junto com o projeto de testes de interface (constituição 1.3.0); os fluxos ficam no roteiro manual do [quickstart.md](./quickstart.md))*
- [X] T107 [P] Revisar textos de tela, mensagens de validação e de erro em pt-BR em `src/Tcmrj.Siat.App/Web/Validators/Mensagens.cs` e nos componentes de `src/Tcmrj.Siat.Web/Components/`
- [X] T108 [P] Conferir que `DESIGN.md` e `design/*.html` já usam "Consultas" como nome da seção (commit `3349270`) e alinhar qualquer resto
- [X] T109 [P] Escrever `README.md` na raiz com: como rodar (link ao quickstart), configuração (`TcmrjOidc`, `TcmrjSessao`, `UserManagerService`, connection strings, `servidores.json`) e publicação no IIS (WebSockets ligados; `TcmrjOidc__ClientSecret` como variável de ambiente; app pool com perfil de usuário carregado ou `PersistKeysToFileSystem` numa pasta só da conta do pool; URLs `/signin-oidc` e `/signout-callback-oidc` com o subdiretório, se houver)
- [ ] T110 Rodar a validação completa do [quickstart.md](./quickstart.md) (cenários 1 a 22 e o roteiro de acessibilidade), os resultados contra os dados do banco (SC-002) e as medições de desempenho (SC-003, SC-004), registrando os planos de execução com e sem filtros
- [ ] T111 Registrar o pedido à equipe da CoreLib para o DBLog aceitar nível Information com `LogUID` ([research.md R7](./research.md#r7-registro-das-consultas-no-log-tcmrjcorelibdblog)) e anotar no `README.md` a linha a trocar em `RegistroConsultaService` quando sair
- [X] T112 Rodar `dotnet build src/Siat.slnx` e `dotnet test` nos dois projetos de teste, sem erros nem avisos novos, e conferir de novo no feed se os pacotes `Tcmrj.*` continuam na última versão estável (constituição, seção Dependências)

---

## Dependências e ordem de execução

### Entre fases

- **Preparação (Fase 1)**: começa logo.
- **Base (Fase 2)**: depende da Fase 1 e **bloqueia todas as histórias**.
- **História 1 (Fase 3)**: depende só da Base.
- **História 2 (Fase 4)**: depende só da Base; pode correr junto com a História 1. Compartilha `Mensagens.cs` (T064/T081): quem chegar depois acrescenta ao arquivo.
- **História 3 (Fase 5)**: depende da Base e de pelo menos uma das Histórias 1 ou 2 (atua nas páginas de consulta); fica completa com as duas. É a exceção à independência das histórias: PRD é um corte transversal dos dois módulos, e a dependência está registrada no plano (riscos).
- **História 4 (Fase 6)**: depende das páginas das Histórias 1 e 2 (T068, T083).
- **Acabamento (Fase 7)**: depende das histórias que forem entregues.

### Dentro da Base

- T011–T015 (Domain) antes de T016–T020 (Infra) e de T028–T030 (services do App).
- T021 depende de T006; T023–T025 dependem de T021.
- T029 depende de T013, T014, T020 e T028; T030 depende de T029.
- T034–T036 dependem de T032; T040 depende de T035.
- T052 depende de T021, T031 e T034.

### Dentro de cada história

- Testes primeiro (devem falhar), depois modelos do Domain, SQL e repositório, features do App, componentes e página.
- Página da história (T068, T083) por último, depois das features.

---

## Exemplos de paralelismo

### Base

```text
T009 script 001 do dbchange
T010 script 002 do dbchange
T011 enums  |  T012 Servidor  |  T013 ModuloExtensions  |  T014 Erros  |  T015 IServidoresRepository
T042–T051 testes da base (depois das classes que testam)
```

### História 1

```text
Testes:        T053 FiltrosMailQueue.feature | T055 PesquisarHandler | T056 SituacaoEnvio | T057 CorpoEmail
Modelos e SQL: T059 Smtp/MensagemEnfileirada | T060 FiltroMailQueue/IMailQueueRepository | T061 mensagens.sql/smtps.sql
Depois:        T062 MailQueueRepository → T065/T066 features → T068 página
```

### História 2

```text
Testes:        T070 FiltrosAuditLog.feature | T071 formatadores | T073 handlers
Modelos e SQL: T075 Aplicacao/RegistroAuditoria | T076 FiltroAuditLog/IAuditLogRepository | T077 logs.sql/apps.sql | T079 formatadores | T080 ViewModels
```

### História 4

```text
T092 FiltroLocal | T093 Exportação | T094 componentes de estado | T095 cancelamento | T096 FiltroLocal (bUnit)
T100 FiltroLocal | T101 siat.js
```

---

## Estratégia de implementação

### MVP primeiro (só a História 1)

1. Fase 1: Preparação.
2. Fase 2: Base (login, permissões, menu, servidores, registro).
3. Fase 3: História 1.
4. **Parar e validar**: cenários 1 a 6 do quickstart em HML.
5. Publicar para o suporte.

### Entrega incremental

1. Preparação + Base → base pronta.
2. História 1 (MailQueue DSV/HML) → validar → publicar (MVP).
3. História 2 (AuditLog DSV/HML) → validar → publicar.
4. História 4 (cancelar, filtrar, exportar) → validar → publicar.
5. História 3 (PRD) → validar → publicar quando a questão em aberto 4 do PRD estiver resolvida e os grupos de PRD definidos.

### Em equipe

Depois da Base, uma pessoa na História 1 e outra na História 2; a História 4 começa quando as duas páginas existirem, e a História 3 em seguida.

---

## Observações

- [P] = arquivos diferentes, sem dependência de tarefa incompleta.
- A etiqueta de história liga a tarefa à história da spec, para rastreio.
- Confirmar que os testes falham antes de implementar.
- Fazer commit a cada tarefa ou grupo lógico, no padrão de mensagens do repositório.
- Nenhuma tarefa cria banco, tabela ou `DbContext` próprio do SIAT (Princípio I); só os scripts de cadastro no `CSLIB_UserManager`.
