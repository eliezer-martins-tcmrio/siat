---
description: 'Padrões de testes do SIAT (xUnit v3, Reqnroll, bUnit)'
applyTo: 'src/Tcmrj.Siat.Test/**'
excludeAgent: "code-review"
---

# Testes no SIAT

A constituição (Princípio V) exige teste automatizado para regras de negócio (validação de filtros, permissões,
interpretação de dados) e teste de componente para componentes com lógica. Fluxos de ponta a ponta na tela vão
para o roteiro manual `specs/NNN-*/quickstart.md`, não para teste de navegador.

## Stack

| Pacote | Papel |
|---|---|
| `xunit.v3` | framework, sobre o Microsoft.Testing.Platform (`global.json`) |
| `Reqnroll.xunit.v3` | BDD, feature files em português (`reqnroll.json`: `"feature": "pt"`) |
| `bunit` | testes de componentes Razor |
| `AwesomeAssertions` | assertions (`Should()`). **Não use FluentAssertions** (licença paga a partir da 8) |
| `Moq` | dublês (`MockBehavior.Strict` por padrão) |
| `Microsoft.Extensions.TimeProvider.Testing` | `FakeTimeProvider` para tempo, cache e expiração |

O projeto é `Exe`, com `<Using Include="Xunit" />` no `.csproj` e referência a App, Domain, Infra e Web.

## Execução

```powershell
dotnet test --solution src/Siat.slnx
dotnet test --project src/Tcmrj.Siat.Test
```

Com MTP, a solução ou o projeto vão em `--solution` / `--project`, não como argumento posicional.

## Estrutura

```
src/Tcmrj.Siat.Test/
  Unidade/                 testes xUnit de handlers, serviços, repositórios e opções
    {Modulo}/              ex.: Catalogo/, MailQueue/, AuditLog/ (amostras em Amostras/)
  Features/                regras de negócio em Gherkin
    {Regra}.feature        ex.: Permissoes.feature, FiltrosMailQueue.feature
    Steps/{Regra}Steps.cs  passos da feature
  Componentes/             testes bUnit dos componentes (classe herda BunitContext)
```

Namespaces seguem as pastas: `Tcmrj.Siat.Test.Unidade.Catalogo`, `Tcmrj.Siat.Test.Features.Steps`,
`Tcmrj.Siat.Test.Componentes`.

## Quando usar cada tipo

- **Feature (Reqnroll)**: regra que o negócio lê e discute — tabelas de exemplos de filtros válidos e inválidos,
  política exigida por módulo. Use `Esquema do Cenário` com `Exemplos` para variações.
- **Unidade (xUnit)**: handlers (autoriza antes de ler, registra a consulta, converte erros e cancelamento),
  serviços, leitores/parsers, validação de opções.
- **Componente (bUnit)**: componentes de `Components/Shared` e páginas com lógica (estados vazio/erro/carregando,
  filtros na URL, acessibilidade básica: rótulos, papéis, foco).

## Convenções

- Nomes de método em português com sublinhados, descrevendo o comportamento:
  `Sem_filtros_nao_oferece_Remover_filtros`. Classe `{Alvo}Tests`, `sealed` quando possível.
- `/// <summary>` na classe dizendo o que ela cobre e citando a spec (história, FR-xxx).
- Mocks `MockBehavior.Strict`, configurados no construtor; o handler é construído com os `.Object`.
- Dublês manuais (`*Falso`, ex.: `AuditLogRepositoryFalso`) quando o Moq ficar mais confuso que uma classe simples.
- Verifique `Result`: `resultado.IsSuccess.Should().BeTrue()` / `resultado.Error.Should().Be(Erros.X.Y)`.
- Em métodos `async`, passe `TestContext.Current.CancellationToken` (regra `xUnit1051`).
- Tempo sempre por `FakeTimeProvider`; nunca `DateTime.Now` nem `Task.Delay` real.
- Testes não acessam rede, GitLab nem SQL Server reais.

## Steps do Reqnroll

- Classe `[Binding]`, `sealed`, com `[Scope(Feature = "<título da Funcionalidade>")]` quando os passos forem
  exclusivos daquela feature; estado em campos privados da própria classe.
- Atributos `[Given]`/`[When]`/`[Then]` com regex e textos em português (`@"o módulo ""(.*)"""`); métodos
  `DadoX`, `QuandoY`, `EntaoZ`.
- Não edite os `*.feature.cs`: são gerados pelo Reqnroll.

## Checklist de uma regra nova

1. Regra discutível com o negócio → cenário no `.feature` + steps; demais → teste em `Unidade/{Modulo}`.
2. Handler novo → testes de: sem permissão (não lê a fonte), filtro inválido, sucesso, erro da fonte e
   cancelamento.
3. Componente com lógica → teste bUnit em `Componentes/`.
4. Fluxo de tela → passo no `quickstart.md` da feature.
5. `dotnet test --solution src/Siat.slnx` passando.
