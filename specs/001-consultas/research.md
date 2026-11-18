# Pesquisa (Fase 0): Consultas — MailQueue e AuditLog

Fontes examinadas: `C:\Projetos\projeto-modelo` (estrutura e convenções), `C:\Projetos\sicati-controle-de-ativos` (Blazor + OIDC), `C:\Projetos\autenticacao` (SIA, `Tcmrj.Autenticacao.AspNetCore`, `Tcmrj.Autorizacao.App`), `C:\Projetos\corelib` (DBLog), feed interno `\\unity\corp2\desenvolvimento\DESENVOL\NUGET` e nuget.org (versões consultadas em 2026-11-10).

## R1. Estrutura da solução

- **Decisão**: seguir o projeto-modelo sem os projetos de API (`--IncluirApi false`): `Tcmrj.Siat.Domain`, `Tcmrj.Siat.Infra`, `Tcmrj.Siat.App`, `Tcmrj.Siat.Web` e `Tcmrj.Siat.Test`. Solução em `src/Siat.slnx`; scripts de banco em `dbchange/<versão>/`. O `Web` é Blazor Web App (Interactive Server), não MVC.
- **Motivo**: pedido do responsável; mantém o SIAT igual aos demais sistemas do Tribunal (camadas, features com `IMediator`, `Result<T, Erro>`, validadores FluentValidation, mapeamento manual, `.editorconfig` e `.gitignore` do modelo).
- **Adaptações ao modelo**, porque o SIAT não tem banco próprio (Princípio I):
  - `Infra` não tem `DbContext` próprio, Unit of Work nem Specifications: os repositórios executam SQL parametrizado com `Microsoft.Data.SqlClient` nos servidores da lista fechada. Ardalis.Specification e EF Core entram só por dependência das bibliotecas do UserManager.
  - Só há features de leitura (`Query`/`QueryHandler`/`QueryValidator`); não há `Command`.
  - Os projetos Api, Client e AspNetCore não existem (não há consumidores externos).
- **Alternativas consideradas**: projeto único Blazor (mais simples, mas foge do padrão pedido); manter MVC do modelo (contraria o PRD RG-10 e a constituição).

## R2. Autenticação por OIDC (`Tcmrj.Autenticacao.AspNetCore.Oidc`)

*Revisado em 2026-11-17. A versão anterior copiava a configuração OIDC do SiCATI e criava `SiatUserManagerService` e um `RevalidatingServerAuthenticationStateProvider` próprios. Desde então o código virou o pacote interno `Tcmrj.Autenticacao.AspNetCore.Oidc` (`C:\Projetos\autenticacao`, commit `b468de4`), e o SiCATI já o adotou (`fce1433`, "usa Tcmrj.Autenticacao.AspNetCore.Oidc para OIDC, permissões e inatividade").*

- **Decisão**: usar `Tcmrj.Autenticacao.AspNetCore.Oidc` 196.1.1 no `Web`, com as duas chamadas do README do pacote, num `AuthenticationConfiguration` igual ao do SiCATI:
  - `builder.Services.AddTcmrjOidc(builder.Configuration)`: esquemas `Cookies`/`oidc` (`TcmrjOidcDefaults`), `ResponseType = code`, `UsePkce = true`, `MapInboundClaims = false`, `NameClaimType = matricula`, escopos `openid profile`, `SaveTokens = true`. Lê a seção `TcmrjOidc` (`Authority`, `ClientId`, `ClientSecret`, `RequireHttpsMetadata`) e falha na inicialização se faltar `Authority` ou `ClientId`.
  - `builder.Services.AddTcmrjAutorizacaoBlazor(builder.Configuration)`: políticas dinâmicas `"<Recurso>.<Permissão>"` (`UserManagerPolicyProvider` + `PermissionHandler`), `TcmrjBlazorUserManagerService` (um `UserManagerContext` por verificação via `IDbContextFactory`, seguro para verificações concorrentes no circuito), `ITcmrjContext` por circuito (`TcmrjContextCircuitHandler`), `AddCascadingAuthenticationState` e o `AuthenticationStateProvider` de inatividade (`ITcmrjInatividade`, seção `TcmrjSessao`). Usa a connection string padrão `UserManagerConnection` e `UserManagerService:ApplicationId`.
  - **O que fica na aplicação** (o pacote pede, seções 4 e 5 do README): `Pages/Account/Login.cshtml` (challenge no esquema `oidc`, só `returnUrl` local); `Pages/Account/Logout.cshtml` que aceita só **POST** com antiforgery (GET volta para `/`); botão **Sair** do menu como formulário POST com `<AntiforgeryToken />`; `MainLayout` chamando `ITcmrjInatividade.Interagir()` a cada navegação; componente `EncerrarSessao` no `<NotAuthorized>` do `AuthorizeRouteView`, que envia o POST de logout depois da renderização (o circuito vira anônimo pela inatividade, mas o cookie e a sessão do SIA seguem vivos; ir para o Login reautenticaria na hora).
  - **Inatividade no SIAT**: além da navegação, `Interagir()` é chamado ao consultar, filtrar localmente, abrir um item e exportar, porque o uso típico é ficar numa só tela fazendo várias consultas. Valores padrão `MinutosSemUsoParaLogout = 30` e `MinutosParaRevalidacao = 1`.
  - **Claims**: lidas por `Tcmrj.Autenticacao.AspNetCore.Extensions` (`GetMatricula`, `GetUserName`, `GetUserEmail`, `GetSessionId`), que chega dentro do `Tcmrj.Autorizacao.App`. Nome ausente volta `null` (a cópia antiga do SiCATI devolvia string vazia); quem mostra o nome trata o nulo.
  - **Nome das políticas**: o `Domain` continua sem referência ao pacote, e `Modulo.Politica()` segue devolvendo o nome `"<Recurso>.CanSelect"` ([data-model.md](./data-model.md)). Um teste de unidade confere que o nome é igual ao de `Politica.Para(recurso, AccessType.CanSelect)` (`Tcmrj.Autorizacao.App.AspNetCore`), o formato que o `UserManagerPolicyProvider` entende.
- **Configuração e segredos**: `appsettings.json` versionado traz `TcmrjOidc:Authority` e `TcmrjOidc:ClientId` do SIA de PRD; `appsettings.Development.json` traz o `ClientId` de desenvolvimento. O `ClientSecret` também fica no `appsettings` (seção `TcmrjOidc`), como no SiCATI. Como o pacote só detecta segredo errado no login (`invalid_client`), o quickstart testa o login logo depois da configuração.
- **Data Protection**: com `SaveTokens = true`, os tokens do SIA ficam no cookie criptografado pelo Data Protection. O SIAT tem instância única no IIS; basta o app pool com perfil de usuário carregado. Se isso não for possível no servidor, `AddDataProtection().SetApplicationName("siat").PersistKeysToFileSystem(...)` numa pasta local com acesso só da conta do pool (sem estado de negócio, Princípio I).
- **O que não se copia do SiCATI**: `AddOpenIddict().AddCore()` com `ScafDbContext` (o SiCATI usa para outra função; o cliente OIDC não precisa) e o segundo argumento `"SCAFConnection"` (o SIAT usa a connection string padrão `UserManagerConnection`).
- **Testes** (R13): como no SiCATI, um teste do grafo real de DI (a cadeia de autorização resolve dentro de um escopo e não a partir do provedor raiz; `AuthenticationStateProvider` é o mesmo objeto que `ITcmrjInatividade`) e um teste das claims do SIA lidas pela biblioteca. O comportamento interno do pacote já é testado no repositório `autenticacao` (`Tcmrj.Autenticacao.Test/Oidc`).
- **Alternativas consideradas**: manter a configuração OIDC e os serviços próprios copiados do SiCATI (versão anterior deste plano: duplica o pacote e contraria a constituição, que manda reaproveitar as bibliotecas `Tcmrj.*`); `AddTcmrjAutenticacao` (cookie do SSO antigo, sem OIDC); `AddTcmrjAutorizacaoHttp` (é para MVC/APIs; no Blazor Server o `ITcmrjContext` por requisição não acompanha o circuito).

## R3. Cadastro no UserManager (`dbchange`)

- **Decisão**: dois scripts idempotentes em `dbchange/<versão>/`, no padrão de `sicati/dbchange/163.1.1/005` e `006`:
  1. `001 - TCMRio - CSLIB_UserManager - Criação de app, recursos e grupos do SIAT.sql`: `Apps` (o **AppId** é fixo no script: `@AppId = 93`, o mesmo em DSV e PRD), `AppResources` (`MailQueue`, `AuditLog`), `Groups` e `Accesses` com `CanSelect`.
  2. `002 - TCMRio - CSLIB_UserManager - OpenIddictApplications do SIAT.sql`: clientes `SIAT-dev` (redirect `https://localhost:<porta>/signin-oidc`) e `SIAT` (URL de PRD), `confidential`, permissões `ept:authorization`, `ept:logout`, `ept:token`, `gt:authorization_code`, `rst:code`, `scp:openid`, `scp:profile`, requisito `ft:pkce`. O `ClientSecret` vai como hash (formato do OpenIddict); o segredo em claro não entra no repositório.
- O mesmo AppId é usado em `UserManagerService:ApplicationId` (autorização) e no `UseTcmrjDBLog(appId)` (coluna `AppID` do log).
- Grupo inicial: `Consultas` (MailQueue e AuditLog nos três ambientes). Os membros são incluídos pelo SIA.
- **Alternativas consideradas**: clientes por ambiente (`-dev`, `-hml`, PRD) como no SiCATI; o SIAT tem instância única, então só existe o cliente de PRD mais o de desenvolvimento local.

## R4. Autorização por módulo

- **Decisão**: a permissão exigida depende só do módulo: `MailQueue.CanSelect` e `AuditLog.CanSelect` liberam DSV, HML e PRD. Não há recurso separado para PRD (decisão revista: a versão anterior tinha `MailQueuePRD` e `AuditLogPRD`). A regra fica no Domain (`Modulo.Politica()`), coberta por cenários Reqnroll; PRD continua com a faixa de contexto destacada e com o registro de toda consulta.
- Três pontos de verificação no servidor:
  1. Página: exige usuário autenticado e verifica no `OnInitializedAsync` a política do módulo; sem ela mostra acesso negado, com o nome do recurso a pedir.
  2. Lista de servidores: a feature `Servidores.ListarPermitidos` devolve a lista fechada inteira, agrupada por ambiente, para quem tem a política do módulo; para quem não tem, nenhuma.
  3. Cada consulta e cada exportação: o handler resolve o servidor pela chave (nunca pelo endereço), calcula a política e chama `IAuthorizationService.AuthorizeAsync(usuario, politica)` antes de abrir a conexão. Negado devolve `Erros.Consultas.AcessoNegado` e nada é consultado. Isso cobre a URL compartilhada e a perda de permissão durante a sessão (não há cache de permissão).
- Menu: política do módulo (`MailQueue.CanSelect`, `AuditLog.CanSelect`) por item; a seção some quando nenhum aparece. É conveniência; o controle de acesso é o servidor.

## R5. Acesso aos bancos consultados

- **Decisão**: `Microsoft.Data.SqlClient` 7.1.1, com a connection string base no `appsettings` (`ConnectionStrings:Consultas`: `Integrated Security`, `TrustServerCertificate`, `Connect Timeout`, `Application Name=SIAT`) e `SqlConnectionStringBuilder` preenchendo `DataSource` e `InitialCatalog` a partir da lista fechada. A conta é a identidade do pool do IIS (conta de serviço só de leitura). Essa infraestrutura já existe: o SIAT é publicado no mesmo servidor e segue as mesmas regras do SiCATI, então não há tarefa de provisionamento de conta ou permissão nesta feature.
- **SQL embutido**: os textos ficam como arquivos `.sql` embutidos no `Infra` (`mensagens.sql`, `smtps.sql`, `logs.sql`, `apps.sql`), em LF e com `.gitattributes` `*.sql eol=lf` para o Git não converter (o plano em cache depende do texto). Só o AuditLog usa `OPTION (RECOMPILE)`. O `mensagens.sql` traz `m.SmtpId AS SmtpId`, para mostrar o SMTP de cada e-mail ao lado da situação.
- Todos os parâmetros com `SqlParameter` tipado (`@Top int`, `@SmtpId int`, `@Texto nvarchar(max)`, `@AppID int`, `@LogID varchar(20)`); texto escapado para `LIKE` por `PadraoLike` (`[` → `[[]`, `%` → `[%]`, `_` → `[_]`).
- **Tempo limite e cancelamento** (Princípio VII): `CommandTimeout` configurável (padrão 60 s) e `ConnectTimeout` 15 s; o `CancellationToken` vai para `OpenAsync`, `ExecuteReaderAsync` e `ReadAsync`, e o SqlClient envia o sinal de atenção ao servidor, interrompendo o comando lá (SC-004). Cada página mantém um `CancellationTokenSource` por consulta; nova consulta cancela a anterior, e o resultado só é aplicado se o token ainda for o atual ("vale só a última").
- Leituras independentes em paralelo: ao escolher o servidor, a lista de SMTPs (ou de aplicações) e a consulta da URL rodam juntas.
- **Alternativas consideradas**: EF Core com `FromSql` (camada a mais, sem ganho para duas consultas fixas); Dapper (dependência a mais; o mapeamento manual é pequeno).

## R6. Lista fechada de servidores

- **Decisão**: arquivo `servidores.json` versionado no projeto `Web`, carregado com `IOptionsMonitor` (`reloadOnChange`), com chave estável, nome de exibição, endereço e ambiente. A URL leva só a chave. Esquema em [contracts/servidores.schema.json](./contracts/servidores.schema.json).
- O arquivo pode ser trocado no servidor sem nova publicação; mover para o GitLab (RG-06) fica para quando o catálogo de Sistemas chegar.
- Lista vazia ou inválida: a validação de options na inicialização registra o erro e a tela mostra "não há servidores configurados", sem derrubar a aplicação.

## R7. Registro das consultas no log (`Tcmrj.CoreLib.DBLog`)

- **Achado**: em `Tcmrj.CoreLib.DBLog.AspNetCore` 187.2.3, o `UseTcmrjDBLog` grava em `audit.Log` só eventos com `LogUID` e nível **Error** (`restrictedToMinimumLevel: Error`). O `LogInformationWithLogUID` da biblioteca não chega ao banco. A coluna de nível não é gravada; o tipo vai em `TypeID`.
- **Decisão**: `RegistroConsultaService` usa `logger.LogErrorWithLogUID(10, "Consulta {Modulo} em {Servidor} ({Ambiente}) por {Matricula}", ...)` com `TypeID 10` ("Log de Informações Gerais", já cadastrado em `audit.Log_Types`). As propriedades `Modulo`, `Operacao` (consulta ou exportação), `Servidor`, `Ambiente`, `Matricula`, `Usuario` e `Filtros` ficam no `LogEvent` (JSON); `AppID` é o do SIAT. O próprio módulo AuditLog do SIAT mostra esses registros.
- O efeito colateral é a linha aparecer como erro no console e no Trace. Fica registrado como melhoria a pedir na CoreLib: aceitar nível Information quando houver `LogUID`. Quando sair, troca-se uma linha.
- **Alternativas consideradas**: um sink Serilog próprio para `audit.Log` (duplica a CoreLib, contra o Princípio VIII); novo `TypeID` "Consultas do SIAT" (exigiria script em `AUDITORIADB`; o `AppID` já separa os registros do SIAT).

## R8. Interface com Blazor Blueprint

- **Decisão**: `BlazorBlueprint.Components` 4.2.0 (Apache-2.0, `net10.0`) com `BlazorBlueprint.Icons.Lucide` 2.0.2, versões fixadas. Os tokens do `design/siat.css` são mapeados nas variáveis de tema da biblioteca; os componentes recorrentes do `DESIGN.md` viram componentes próprios em `Web/Components/Shared` (cabeçalho de página, seletor de servidor, faixa de contexto, lista expansível, estados carregando/vazio/erro/aviso, bloco de código, corpo de e-mail, filtro local, botão copiar), para isolar o SIAT de mudanças da biblioteca.
- Renderização: `InteractiveServerRenderMode(prerender: false)`, como no SiCATI, para os métodos de ciclo de vida rodarem uma vez e o JS interop estar disponível.
- Tema claro/escuro/sistema guardado no `localStorage` e aplicado por script inline no `<head>` antes da primeira pintura (sem preferência no servidor, Princípio I).
- Reconexão do SignalR: tela de reconexão própria (`components-reconnect-modal`) em português; ao voltar, a URL refaz a consulta.

## R9. Estado na URL, filtro local, teclado

- Parâmetros com `[SupplyParameterFromQuery]`: MailQueue `servidor`, `smtp`, `texto`, `qtd`, `sel`; AuditLog `servidor`, `app`, `logid`, `qtd`, `sel`. Ao consultar, `NavigationManager.NavigateTo(..., replace: false)` atualiza a URL; ao abrir a página com parâmetros, a consulta é refeita depois da autorização. Contrato em [contracts/rotas-ui.md](./contracts/rotas-ui.md).
- Filtro local (FR-020) em C#, sobre os itens já carregados no circuito: normalização com `CompareInfo.IndexOf(..., CompareOptions.IgnoreCase | IgnoreNonSpace)` em pt-BR, vários termos, contador "X de Y". O atalho `/` e a navegação por setas ficam num módulo JS pequeno (`siat.js`) que só move o foco.

## R10. Corpo do e-mail isolado

- **Decisão**: `<iframe sandbox="" srcdoc="...">`, como pede o `DESIGN.md`, com altura fixa e redimensionamento (medir a altura exigiria `allow-same-origin`). O `srcdoc` recebe no início um `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'">`, que impede o carregamento de recursos externos. Texto simples (`IsHTML = 0`) vai escapado dentro de `<pre>`.
- O detalhe (iframe, stack trace, JSON) só é renderizado quando a linha é aberta, para não pesar a lista nem o tráfego do circuito.

## R11. Formatação de stack trace e LogEvent

- Stack trace: quebra por linha, realce das linhas `at ...` do próprio código e da linha da mensagem; bloco escuro nos dois temas, com Copiar.
- LogEvent: `JsonDocument.Parse` + `JsonSerializer` com indentação, realce por tipo de token; se não for JSON válido, mostra o texto original (FR-015). Os dois formatadores ficam em `App/Web/Services`, com testes de unidade.

## R12. Exportação em HTML autocontido

- **Decisão**: a feature `Exportar` monta o HTML no servidor a partir dos itens carregados (CSS e um script mínimo de filtro e expansão embutidos; corpo de e-mail em `iframe sandbox` com `srcdoc`), e o navegador baixa via `DotNetStreamReference` + JS. Nome `mailqueue_AAAAMMDD_HHMMSS.html` / `auditlog_AAAAMMDD_HHMMSS.html`. A exportação é registrada no log (R7). Contrato em [contracts/exportacao-html.md](./contracts/exportacao-html.md).

## R13. Testes

- **Unidade e regras** (`Tcmrj.Siat.Test`): xUnit v3 4.0.3 com Microsoft Testing Platform (como o SiCATI, `global.json`), AwesomeAssertions 9.6.0, Moq 4.21.0 e Reqnroll.xunit.v3 3.3.4 para os cenários de regra (permissão por ambiente, validação de filtros), no padrão `Features/*.feature` + `Steps` do modelo. Inclui os testes de autenticação de R2 (grafo de DI da autorização e claims do SIA).
- **Componentes**: bUnit 2.15.2 (seletor de servidor, faixa de contexto, filtro local, lista expansível, estados).
- **Fluxos das telas**: roteiro manual no [quickstart.md](./quickstart.md) (constituição 1.3.0, Princípio V); não há teste automatizado com navegador.

## R14. Versões dos pacotes (constituição 1.1.0, seção Dependências)

Consultadas em 2026-11-10; pacotes de autenticação revistos em 2026-11-17. Internos: última estável no feed TCMRIO INTERNO. Externos: última estável compatível com `net10.0`, licença gratuita.

| Pacote | Versão | Licença | Projeto |
|---|---|---|---|
| Tcmrj.Autenticacao.AspNetCore.Oidc | 196.1.1 | interno | Web |
| Tcmrj.Autorizacao.App (inclui Tcmrj.Autenticacao.AspNetCore, Tcmrj.UserManager.Domain/Infra) | 196.1.1 (mesma versão exigida pelo pacote Oidc) | interno | Web |
| Tcmrj.CoreLib.DBLog.AspNetCore | 187.2.3 | interno | Web |
| Tcmrj.CoreLib.DBLog | 187.2.3 | interno | App |
| Tcmrj.CSharpFunctionalExtensions (`Result<T, Erro>`, `Erro`) | 140.1.2 | interno | Domain |
| Tcmrj.CoreLib.Domain | 195.1.1 | interno | transitiva (via Autorizacao.App); sem referência direta, pois o SIAT não usa Specifications nem repositório EF |
| Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.15 (referência direta só para elevar a 10.0.14 do pacote Oidc ao último patch) | MIT | Web |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.15 (idem; não é usado pelo SIAT, vem como dependência do pacote Oidc) | MIT | Web |
| Microsoft.EntityFrameworkCore.SqlServer (para o `UserManagerContext`) | 10.0.15 | MIT | Web |
| Microsoft.Data.SqlClient | 7.1.1 | MIT | Infra |
| BlazorBlueprint.Components | 4.2.0 | Apache-2.0 | Web |
| BlazorBlueprint.Icons.Lucide | 2.0.2 | MIT | Web |
| FluentValidation | 12.2.0 | Apache-2.0 | App |
| NetDevPack.SimpleMediator | 1.2.0 | MIT | App |
| StyleCop.Analyzers | 1.2.0-beta.556 (ver nota) | MIT | todos |
| xunit.v3 | 4.0.3 | Apache-2.0 | testes |
| AwesomeAssertions | 9.6.0 | Apache-2.0 | testes |
| Moq | 4.21.0 | BSD-3-Clause | testes |
| Reqnroll.xunit.v3 | 3.3.4 | BSD-3-Clause | Test |
| bunit | 2.15.2 | MIT | Test |
| coverlet.collector | 10.1.0 | MIT | testes |

- **Nota StyleCop**: a última estável (1.1.118, de 2019) não entende C# moderno; o projeto-modelo e o SiCATI usam a 1.2.0-beta. A constituição proíbe pré-lançamento, então o SIAT fica com as regras de estilo do `.editorconfig` do modelo e os analisadores do SDK (`AnalysisMode Recommended`), sem StyleCop. Se a coordenação quiser o StyleCop, é preciso uma exceção na constituição.
- **.NET 11**: já existem pacotes 11.0.0 do ASP.NET Core e do EF Core, mas a plataforma da constituição e do PRD é .NET 10 (LTS). Mudar de plataforma exige consultar o responsável; o plano fica no .NET 10, com os pacotes no último patch da 10.0.
- **Dependências antigas das bibliotecas internas**: `Tcmrj.Autorizacao.App` 196.1.1 ainda é `net8.0` e depende de `Tcmrj.CoreLib.DBLog.EF` 144.1.1 e EF Core 8; a referência direta ao EF Core 10.0.15 eleva a versão. A compatibilidade `net8.0` → `net10.0` já é usada no SiCATI (risco do PRD, item 3).
