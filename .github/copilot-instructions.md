# Instruções para agentes no SIAT

SIAT (Sistema Integrado de Apoio Técnico) é a aplicação web interna do TCMRio que reúne ferramentas de
consulta para suporte e desenvolvedores: **Sistemas › Catálogo e Versões**, com a **ficha do sistema** (catálogo e
versões vinculadas pelo campo `Versoes` do `sistemasDb.js`), **Consultas** (MailQueue e AuditLog) e
**Gestão › Copilot** (o consumo de AI Credits do Copilot da própria pessoa, lido da API do GitHub para a conta que
ela conecta ao SIAT).
Visão geral, configuração e publicação: [README.md](../README.md). Design: [DESIGN.md](../DESIGN.md).

## Regras que prevalecem

A constituição em [.specify/memory/constitution.md](../.specify/memory/constitution.md) prevalece sobre
especificações, planos e estas instruções. Leia-a antes de mudar comportamento. Em resumo:

- **Sem estado próprio**: o SIAT não tem banco nem persiste dados de negócio; cache só em memória, com
  expiração e "atualizar agora".
- **Somente leitura**: nenhuma ação em produção; nova operação de escrita exige emenda à constituição.
- **Segurança (inegociável)**: toda página que exibe dados e toda operação exigem um recurso do UserManager,
  verificado no servidor (no handler, antes de ler a fonte); servidor só de uma lista fechada; todo SQL
  parametrizado (`SqlParameter`); consultas a dados sensíveis registradas com `Tcmrj.CoreLib.DBLog`;
  conteúdo externo (e-mail, HTML, Markdown) isolado ou sanitizado. Não use `dotnet user-secrets`.
- **Módulos independentes**: a falha de uma fonte afeta só o módulo ou o item afetado.
- **Testes**: regras de negócio e componentes com lógica têm testes automatizados; fluxos principais vão no
  `quickstart.md` da feature.
- **Português do Brasil** em toda a interface, mensagens, identificadores e comentários.
- **Resiliência**: toda chamada externa tem tempo limite e aceita `CancellationToken`, repassado até a origem.
- **Simplicidade**: sem camadas ou dependências que não resolvam um problema presente; reaproveite as
  bibliotecas internas `Tcmrj.*`.

## Comandos

- Build: `dotnet build src/Siat.slnx`
- Testes (Microsoft.Testing.Platform, ver `global.json`): `dotnet test --solution src/Siat.slnx`
- Executar: `dotnet run --project src/Tcmrj.Siat.Web` (abre em `https://localhost:7271`)
- Perfis de execução (OIDC, usuário de rede, IIS Express): `.claude/launch.json` e `tools/iniciar-iisexpress.ps1`

## Arquitetura

Solução .NET 10 (`net10.0`, C# 14) no padrão do projeto-modelo do Tribunal, com o Web em Blazor:

- `Tcmrj.Siat.Web`: Blazor Web App (Interactive Server) com **Blazor Blueprint** (`BlazorBlueprint.Components`,
  ícones Lucide). Páginas em `Components/Pages/{Sistemas,Consultas,Copilot}`, componentes próprios em
  `Components/Shared` (cabeçalho, estados vazio/erro/carregando, seletor de servidor etc.), registro de
  dependências em `Configurations/`, autenticação OIDC no SIA (`Tcmrj.Autenticacao.AspNetCore.Oidc`) ou
  modo experimental `UsuarioDeRede` (Negotiate), escolhido por `SiatConfiguration:Autenticacao`.
  **Conexão com o GitHub** (Copilot): `Configurations/CopilotGitHubConfiguration.cs` registra, só com
  `Copilot:OAuth` completo, o cookie `GitHubConexao` e o OAuth `GitHubOAuth` (nenhum é o esquema padrão) e os
  endpoints `copilot/github/conectar` e `desconectar`; `Services/ValidacaoRetornoGitHub.cs` valida o retorno e
  `Services/ConexaoGitHubMiddleware` acrescenta a identidade da conexão ao usuário do SIAT.
- `Tcmrj.Siat.App`: casos de uso em `Web/Features/{Modulo}/{Acao}.cs` com NetDevPack.SimpleMediator,
  validadores FluentValidation em `Web/Validators`, view models em `Web/ViewModels`, serviços de aplicação
  (autorização, registro de consulta) em `Web/Services`. No Copilot, `Web/Features/Copilot` não recebe login:
  a conta vem só de `IConexaoGitHubService`, e o `ConsumoCopilotService` (singleton) guarda os dados do GitHub em
  `LeituraEmCache` por tipo de dado.
- `Tcmrj.Siat.Domain`: modelos, enums (`Modulo`, ambientes), erros (`Erros.*`) e contratos de repositório.
- `Tcmrj.Siat.Infra`: repositórios com `Microsoft.Data.SqlClient` e SQL embutido em `Sql/*.sql`
  (`EmbeddedResource`), fábrica de conexões (`Conexoes/`), leitura do catálogo no GitLab (`Catalogo/`), leitura dos arquivos de versão (`Versoes/`), leitura da API do
  GitHub com o token de leitura, só `GET` (`Copilot/`) e
  opções de configuração (`Configurations/`). **Não há EF Core nem `DbContext` de negócio.**
- `Tcmrj.Siat.Test`: xUnit v3 + Reqnroll (BDD em português) + bUnit + AwesomeAssertions + Moq.

## Convenções do código

- Uma feature é uma classe `static` com tipos aninhados `Query`/`Command` (records que implementam
  `IRequest<Result<T, Erro>>`) e `Handler`/`QueryHandler` com construtor primário. A página envia com
  `Mediator.Send(...)`.
- Ordem no handler: validar (FluentValidation) → autorizar (`IAutorizacaoConsultaService`, pelo `Modulo` e
  pelo servidor) → ler a fonte → registrar a consulta quando o dado é sensível.
- Retornos usam `Result<T, Erro>`/`UnitResult<Erro>` de `Tcmrj.CSharpFunctionalExtensions`
  (`using CSharpFunctionalExtensions;`); erros conhecidos ficam em `Erros.{Modulo}`. Não use exceção como
  fluxo normal; cancelamento vira `Erros.*.Cancelada`.
- Políticas de autorização vêm do enum `Modulo` (`modulo.Politica()`, formato `Recurso.CanSelect` do
  UserManager).
- Filtros e seleção da tela ficam na URL, para o link poder ser compartilhado.
- Comentários XML (`/// <summary>`) explicam o porquê e citam a spec (FR-xxx, research.md Rn) quando houver.

## Fluxo de trabalho

- Features seguem o Spec Kit: `specs/NNN-nome/` (spec, plan, tasks, quickstart, contracts). Skills em
  `.github/skills/` (Copilot) e `.claude/skills/` (Claude Code).
- Branches: `features/`, `issues/` ou `releases/`, em minúsculo, só letras sem acento, números, `_`, `.` e `-`.
- Scripts de banco (só no `CSLIB_UserManager`, para cadastro do app, recursos e cliente OIDC) ficam em
  `dbchange/` — ver [scripts-sqlserver.instructions.md](instructions/scripts-sqlserver.instructions.md).

## Instruções por tipo de arquivo

| Arquivo | Aplica-se a |
|---|---|
| [csharp.instructions.md](instructions/csharp.instructions.md) | `**/*.cs`, `**/*.razor` |
| [tests.instructions.md](instructions/tests.instructions.md) | `src/Tcmrj.Siat.Test/**` |
| [scripts-sqlserver.instructions.md](instructions/scripts-sqlserver.instructions.md) | `**/*.sql` |
| [database.instructions.md](instructions/database.instructions.md) | consultas aos bancos de desenvolvimento |
| [code-review-generic.instructions.md](instructions/code-review-generic.instructions.md) | revisão de código |
