# Plano de implementação: Consultas — MailQueue e AuditLog

**Branch**: `001-consultas-visor` | **Data**: 2026-11-10 (revisado em 2026-11-17: autenticação pelo pacote `Tcmrj.Autenticacao.AspNetCore.Oidc`) | **Spec**: [spec.md](./spec.md)

**Entrada**: especificação da feature em `specs/001-consultas-visor/spec.md`

## Resumo

Primeira entrega do SIAT: a base da aplicação (login, permissões, layout, menu, temas) e a seção **Consultas** com MailQueue e AuditLog, porte do `Tcmrj.Visor` para uma aplicação web única. A solução segue o projeto-modelo do Tribunal (`Domain`, `Infra`, `App`, `Web`, `Test`), com o `Web` em Blazor Web App (Interactive Server) e Blazor Blueprint, como indica o PRD. O login é por OpenID Connect no SIA de PRD pelo pacote interno `Tcmrj.Autenticacao.AspNetCore.Oidc` (`AddTcmrjOidc` + `AddTcmrjAutorizacaoBlazor`), como o SiCATI já faz: o pacote traz o cliente OIDC com PKCE, as políticas por recurso do UserManager (`Tcmrj.Autorizacao.App`), a verificação de permissão segura para o circuito Blazor, o `ITcmrjContext` por circuito e o logout por inatividade, sem código próprio de autenticação no SIAT. O AppId, os recursos, os grupos e os clientes OIDC são criados pelos scripts do `dbchange`. As consultas usam o SQL do Visor sem mudança, com `SqlParameter`, conta de serviço só de leitura, tempo limite, cancelamento no servidor de banco e registro de cada consulta no DBLog.

## Contexto técnico

**Linguagem/versão**: C# 14, .NET 10 (último patch da 10.0)

**Dependências principais**: Blazor Web App (Interactive Server, sem prerender); BlazorBlueprint.Components 4.2.0; Tcmrj.Autenticacao.AspNetCore.Oidc 196.1.1 e Tcmrj.Autorizacao.App 196.1.1 (inclui Tcmrj.Autenticacao.AspNetCore, Tcmrj.UserManager.Domain e Infra); Microsoft.AspNetCore.Authentication.OpenIdConnect 10.0.15 (eleva a 10.0.14 que vem do pacote); Tcmrj.CoreLib.DBLog.AspNetCore 187.2.3; Microsoft.Data.SqlClient 7.1.1; NetDevPack.SimpleMediator 1.2.0; FluentValidation 12.2.0; Tcmrj.CSharpFunctionalExtensions 140.1.2. Tabela completa com licenças em [research.md R14](./research.md#r14-versões-dos-pacotes-constituição-110-seção-dependências).

**Armazenamento**: nenhum próprio. Leitura em `MailQueue`, `AUDITORIADB` e `CSLIB_UserManager` dos servidores da lista fechada (`servidores.json`); autorização no `CSLIB_UserManager` de PRD; registro de consultas em `AUDITORIADB.audit.Log` pelo DBLog.

**Testes**: xUnit v3 (Microsoft Testing Platform), AwesomeAssertions, Moq, Reqnroll (regras), bUnit (componentes), Playwright com `WebApplicationFactory` em Kestrel e autenticação de teste (interface).

**Plataforma**: IIS no servidor do UserManager, HTTPS e WebSockets; navegadores Edge e Chrome atuais na rede do Tribunal.

**Tipo de projeto**: aplicação web (Blazor Server) em solução de camadas do projeto-modelo.

**Metas de desempenho**: telas em até 2 s (SC-003); consultas com filtro em até 5 s na maioria dos casos; cancelamento interrompe o comando no banco em até 2 s (SC-004).

**Restrições**: só leitura nos bancos consultados; servidor apenas da lista fechada (nem na tela nem na URL); SQL parametrizado e idêntico ao do Visor (plano em cache depende do texto); tempo limite em toda chamada externa; corpo de e-mail isolado; WCAG 2.2 AA; português do Brasil; pacotes na última versão estável com licença gratuita.

**Escala/escopo**: equipes de suporte e desenvolvimento (dezenas de usuários simultâneos); 2 telas de consulta + base; até 1000 itens por consulta; lista de servidores com dezenas de itens.

## Verificação da constituição (v1.1.1)

*Gate: antes da Fase 0 e de novo depois da Fase 1.*

| Princípio | Como o plano atende | Situação |
|---|---|---|
| I. Sem estado próprio | Sem banco nem `DbContext` próprio; lista de servidores em `servidores.json` versionado; tema no `localStorage`; nenhum cache de dados nesta feature | Atende |
| II. Consulta por padrão | Só `SELECT` nos bancos consultados; nenhuma escrita; o registro no log é feito pelo DBLog, previsto no Princípio III | Atende |
| III. Segurança e menor privilégio | OIDC no SIA de PRD com PKCE pelo `AddTcmrjOidc` (R2); logout por POST com antiforgery e por inatividade (`TcmrjSessao`); política por recurso verificada no servidor na página, na lista de servidores, em cada consulta e em cada exportação (R4); a página inicial e o logout não exibem dados e exigem só autenticação, e o login é anônimo (emenda 1.1.1); recursos separados para PRD; conta de serviço só de leitura; lista fechada com chave na URL (R6); `SqlParameter` e connection string base no `appsettings` (R5); iframe `sandbox` + CSP (R10); `ClientSecret` fora do repositório; registro de toda consulta e exportação (R7) | Atende |
| IV. Módulos independentes | MailQueue e AuditLog são features e páginas separadas; erro de um servidor afeta só a tela que o consultou; Visor continua disponível até a validação | Atende |
| V. Testes | Reqnroll para permissão por ambiente e validação de filtros; unidade para formatadores, escape do `LIKE`, montagem de exportação; bUnit para componentes com lógica; Playwright para os fluxos das histórias 1 a 4 (R13) | Atende |
| VI. Experiência consistente | Blazor Blueprint 4.2.0 fixado; componentes próprios do `DESIGN.md` em `Components/Shared`; temas claro/escuro/sistema; atalho `/`; filtros e `sel` na URL; pt-BR | Atende |
| VII. Desempenho e resiliência | `ConnectTimeout` 15 s e `CommandTimeout` configurável; cancelamento via `CancellationToken` até o SqlClient; leituras independentes em paralelo; consulta anterior cancelada | Atende |
| VIII. Simplicidade | ADO.NET direto para duas consultas fixas (sem EF próprio, sem Dapper); bibliotecas Tcmrj reaproveitadas; OIDC, permissões no circuito e inatividade vêm de `Tcmrj.Autenticacao.AspNetCore.Oidc` (R2), sem `UserManagerService`, `RevalidatingServerAuthenticationStateProvider` nem configuração OIDC próprios; o único código de autenticação no SIAT são as páginas `Account/Login` e `Account/Logout` e o componente `EncerrarSessao`, que o pacote pede à aplicação, mais o serviço `Interacao` (motivo: o pacote só renova a inatividade quando a aplicação chama `Interagir()`, e o uso típico é fazer várias consultas na mesma tela; o serviço centraliza essa chamada nas ações de consulta, filtro, abertura de item e exportação) | Atende |
| Restrições técnicas | .NET 10, Blazor Interactive Server, instância única, estrutura do projeto-modelo com Web em Blazor | Atende |
| Dependências | Últimas versões estáveis (R14); só licenças gratuitas; sem pré-lançamento. StyleCop fica de fora porque a única versão utilizável é beta (R14, nota) | Atende |

Reavaliação depois da Fase 1: o modelo de dados, os contratos e o quickstart não introduzem estado, escrita nem dependência nova. **Gate aprovado.**

Reavaliação em 2026-11-17 (troca para `Tcmrj.Autenticacao.AspNetCore.Oidc`): o pacote é interno, estável (196.1.1, última do feed), `net10.0`, e cumpre o Princípio III da constituição (OIDC com PKCE no SIA, claims por `Tcmrj.Autenticacao.AspNetCore`, autorização por `Tcmrj.Autorizacao.App`). Remove código próprio (Princípio VIII). O `SaveTokens = true` do pacote guarda os tokens no cookie criptografado; não cria estado no servidor (Princípio I). **Gate aprovado.**

## Estrutura do projeto

### Documentação (esta feature)

```text
specs/001-consultas-visor/
├── plan.md              # este arquivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/           # Fase 1
│   ├── rotas-ui.md
│   ├── servidores.schema.json
│   ├── registro-consulta.md
│   ├── exportacao-html.md
│   └── usermanager.md
├── checklists/
└── tasks.md             # Fase 2 (/speckit-tasks)
```

### Código-fonte (raiz do repositório)

```text
.editorconfig / .gitignore / .gitattributes   # do projeto-modelo; *.sql eol=lf
global.json                                   # Microsoft Testing Platform
dbchange/<versão>/
├── 001 - TCMRio - CSLIB_UserManager - Criação de app, recursos e grupos do SIAT.sql
└── 002 - TCMRio - CSLIB_UserManager - OpenIddictApplications do SIAT.sql

src/
├── Siat.slnx
├── Tcmrj.Siat.Domain/
│   ├── Enums/                 # Ambiente, Modulo
│   ├── Models/                # Servidor, Smtp, MensagemEnfileirada, Aplicacao, RegistroAuditoria,
│   │                          # FiltroMailQueue, FiltroAuditLog, Erros.cs
│   ├── Extensions/            # Modulo.PoliticaPara(Ambiente), descrições
│   └── Repositories/          # IServidoresRepository, IMailQueueRepository, IAuditLogRepository
├── Tcmrj.Siat.Infra/
│   ├── Configurations/        # ServidoresOptions + validação, ConsultasOptions (timeouts)
│   ├── Conexoes/              # FabricaDeConexao (SqlConnectionStringBuilder)
│   ├── Sql/                   # mensagens.sql, smtps.sql, logs.sql, apps.sql (embutidos, LF, iguais ao Visor)
│   └── Repositories/          # ServidoresRepository, MailQueueRepository, AuditLogRepository
├── Tcmrj.Siat.App/
│   └── Web/
│       ├── Features/
│       │   ├── Servidores/ListarPermitidos.cs
│       │   ├── MailQueue/ListarSmtps.cs, Pesquisar.cs, Exportar.cs
│       │   └── AuditLog/ListarAplicacoes.cs, Pesquisar.cs, Exportar.cs
│       ├── Services/          # AutorizacaoConsultaService, RegistroConsultaService,
│       │                      # FormatadorStackTrace, FormatadorLogEvent, ExportacaoHtmlService, FiltroLocal
│       │   └── Interfaces/
│       ├── Validators/        # Mensagens.cs, validadores das Queries
│       ├── ViewModels/        # MailQueue/, AuditLog/, Shared/ (ServidorVM, ResultadoConsultaVM)
│       └── Navegacao.cs       # montagem das URLs com parâmetros
├── Tcmrj.Siat.Web/
│   ├── Program.cs
│   ├── appsettings.json / appsettings.Development.json / servidores.json
│   ├── Configurations/        # AuthenticationConfiguration (AddTcmrjOidc + AddTcmrjAutorizacaoBlazor),
│   │                          # RazorConfiguration, DBLogConfiguration, SiatOptions
│   ├── Extensions/ServiceCollectionExtensions.cs   # AddTcmrjSiat (features, repositórios, services)
│   ├── Services/              # Interacao (renova a inatividade nas ações de consulta)
│   ├── Pages/Account/         # Login.cshtml (challenge), Logout.cshtml (só POST com antiforgery)
│   ├── Components/
│   │   ├── App.razor, Routes.razor (NotAuthorized → EncerrarSessao), _Imports.razor, EncerrarSessao.razor
│   │   ├── Layout/            # MainLayout (ITcmrjInatividade.Interagir na navegação), Menu, Marca,
│   │   │                      # RodapeMenu (Sair por POST), ReconexaoModal
│   │   ├── Pages/             # Home.razor, AcessoNegado.razor, Consultas/MailQueue.razor, Consultas/AuditLog.razor
│   │   └── Shared/            # CabecalhoPagina, SeletorServidor, FaixaContexto, ListaExpansivel,
│   │                          # FiltroLocal, EstadoCarregando, EstadoVazio, EstadoErro, Aviso,
│   │                          # BlocoCodigo, CorpoEmail, BotaoCopiar, SeletorTema
│   └── wwwroot/               # css/siat.css (tokens do design/), css/blueprint-tema.css, js/siat.js, img/ (logos)
├── Tcmrj.Siat.Test/
│   ├── Features/              # Permissoes.feature, FiltrosMailQueue.feature, FiltrosAuditLog.feature + Steps/
│   ├── Unidade/               # grafo de DI da autorização e claims do SIA (como no SiCATI), formatadores, escape LIKE, exportação, filtro local, handlers com repositórios falsos
│   └── Componentes/           # testes bUnit
└── Tcmrj.Siat.Test.Interface/
    ├── Infra/                 # SiatWebApplicationFactory (Kestrel), AutenticacaoDeTeste, repositórios falsos
    └── Fluxos/                # MailQueue, AuditLog, Prd, CancelarFiltrarExportar
```

**Decisão de estrutura**: projeto-modelo sem os projetos de API, com `Web` em Blazor e um projeto a mais só para os testes de interface (R1). As camadas seguem a regra de referência do modelo: `Domain` não referencia ninguém; `Infra` → `Domain`; `App` → `Domain`; `Web` → `App`, `Domain`, `Infra`; testes → os projetos que testam.

## Riscos e pontos para a implementação

| Item | Tratamento |
|---|---|
| DBLog só grava nível Error no banco (R7) | Registro com `LogErrorWithLogUID(10, ...)`; pedir à CoreLib suporte a Information com `LogUID` |
| Bibliotecas Tcmrj em `net8.0` numa aplicação `net10.0` | `Tcmrj.Autorizacao.App` 196.1.1 ainda é `net8.0`; o `Tcmrj.Autenticacao.AspNetCore.Oidc` é `net10.0`. Mesma combinação do SiCATI; validar no primeiro build |
| Tokens no cookie (`SaveTokens = true` no pacote) e chaves do Data Protection | No IIS, o app pool precisa carregar o perfil do usuário para as chaves sobreviverem à reciclagem; senão, `PersistKeysToFileSystem` numa pasta protegida do servidor (README do pacote, seção 3). Instância única, então não há chave compartilhada entre máquinas |
| Inatividade numa página só | `Interagir()` na navegação e também ao consultar, filtrar, abrir item e exportar; senão quem trabalha 30 min na mesma tela é deslogado (README do pacote, seção 5) |
| `ClientSecret` errado ou ausente | O pacote só falha no login (`invalid_client`); o quickstart confere o login logo após configurar |
| Texto do SQL alterado por conversão de quebra de linha | `.sql` embutido com `eol=lf` no `.gitattributes`; teste que compara o texto com o do Visor |
| Corpo de e-mail grande no circuito | Detalhe renderizado só ao abrir; limite de 1000 itens |
| AppId e URL de PRD ainda não definidos | AppId conferido em DSV e PRD ao escrever o script; URL de PRD depende da questão em aberto 5 do PRD |
| História 3 (PRD) depende das páginas das Histórias 1 e 2 | Exceção consciente à independência das histórias: PRD é um corte transversal dos dois módulos (seletor, faixa de contexto e negação pela URL). A regra de política por ambiente e o registro ficam na Base, e a História 3 só completa e testa o comportamento de PRD nas páginas já existentes |
| Conta de leitura nos bancos consultados | Fora do escopo do projeto: o SIAT é publicado no mesmo servidor e com as mesmas regras do SiCATI (Blazor no IIS, identidade do pool), cuja infraestrutura já existe. Nenhuma tarefa de provisionamento |
| Quem recebe os recursos de PRD (questão em aberto 4) | História 3 só é liberada em PRD depois da definição; o código e os testes não dependem disso |

## Acompanhamento de complexidade

Nenhuma violação da constituição a justificar. O projeto extra `Tcmrj.Siat.Test.Interface` é uma adaptação da estrutura do modelo (R1), não uma camada de código de produção.
