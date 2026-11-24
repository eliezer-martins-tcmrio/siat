---
description: 'Padrões de C# e Razor do SIAT'
applyTo: '**/*.cs, **/*.razor'
excludeAgent: "code-review"
---

# C# e Razor no SIAT

## Linguagem e formatação

- .NET 10 / C# 14, com `Nullable` e `ImplicitUsings` habilitados (`src/Directory.Build.props`).
- Siga o `src/.editorconfig` e os analisadores (`AnalysisMode=Recommended`); não suprima aviso sem comentar o motivo.
- Identificadores, mensagens e comentários em **português do Brasil** (ex.: `Pesquisar`, `ObterCatalogo`, `Erros.Catalogo.AcessoNegado`).
- Prefira construtores primários, `sealed` em classes e records, expressões `switch` e pattern matching.
- Use `is null` / `is not null`; confie nas anotações de nulidade. Em pontos de entrada públicos use
  `ArgumentNullException.ThrowIfNull`.
- Use `nameof` em vez de literais com nomes de membros.
- Comentários XML (`/// <summary>`) explicam o porquê e citam a spec quando existir (FR-xxx, research.md Rn).

## Organização

- Feature nova: `src/Tcmrj.Siat.App/Web/Features/{Modulo}/{Acao}.cs`, classe `static` com `Query`/`Command`
  (`IRequest<Result<T, Erro>>`) e `Handler`. Validador em `Web/Validators`, view model em `Web/ViewModels/{Modulo}`.
- Contrato de repositório em `Tcmrj.Siat.Domain/Repositories`; implementação em `Tcmrj.Siat.Infra/Repositories`.
- Opções de configuração em `Tcmrj.Siat.Infra/Configurations` (classe `*Options` + `*Configuration` para o registro),
  validadas na subida.
- Página em `Tcmrj.Siat.Web/Components/Pages/{Secao}`; estado vazio, erro, carregamento, cabeçalho e seletor de
  servidor usam os componentes de `Components/Shared` — não recrie esses padrões na página.

## Resultados e erros

- Retorne `Result<T, Erro>` / `UnitResult<Erro>` (`Tcmrj.CSharpFunctionalExtensions`). Erros conhecidos ficam em
  `Erros.{Modulo}` no Domain, com código e mensagem para o usuário.
- Não use exceção como fluxo normal. Capture `OperationCanceledException` (e a `SqlException` de cancelamento) e
  converta em `Erros.*.Cancelada`.
- Mensagens de erro e logs nunca expõem segredos (token do GitLab, `ClientSecret`, connection string).

## Acesso a dados

- Sem EF Core para dados de negócio: use `Microsoft.Data.SqlClient` com o SQL em `Tcmrj.Siat.Infra/Sql/*.sql`
  (embutido como recurso) e a conexão da `IFabricaDeConexao`.
- Só `SELECT`. Sempre `SqlParameter` tipado; nada de concatenar valor do usuário no SQL (para `LIKE`, use `PadraoLike`).
- O servidor vem sempre da lista fechada (`servidores.json` / `IServidoresRepository`), nunca de texto livre.
- Repasse o `CancellationToken` a `OpenAsync`, `ExecuteReaderAsync` e `ReadAsync`; respeite os tempos limite de
  `ConsultasOptions`. Chamadas HTTP (GitLab) também têm tempo limite.

## Segurança

- Autorize no handler, antes de tocar na fonte (`IAutorizacaoConsultaService`). Esconder item na interface não é
  controle de acesso.
- Consultas a dados sensíveis passam pelo `IRegistroConsultaService` (usuário, servidor e filtros), inclusive
  quando negadas, canceladas ou com erro.
- Conteúdo externo (corpo de e-mail, HTML) é exibido isolado ou sanitizado (ver `CorpoEmail.razor`).

## Blazor

- Interactive Server com Blazor Blueprint (versão fixada no `.csproj`).
- Filtros e seleção refletidos na URL (`[SupplyParameterFromQuery]`), para links compartilháveis.
- Acessibilidade WCAG 2.2 AA: rótulos, foco visível, navegação por teclado e atalho "/" para busca.
- Consultas longas mostram o tempo decorrido e podem ser canceladas.

## Testes

- Toda regra nova vem com teste — ver [tests.instructions.md](tests.instructions.md).
