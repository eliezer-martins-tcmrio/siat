# SIAT

Sistema Integrado de Apoio Técnico do TCMRio. A primeira entrega é a seção **Consultas**, que traz para o SIAT as consultas do Visor (`Tcmrj.Visor`): **MailQueue** e **AuditLog**, somente leitura, sobre uma lista fechada de servidores SQL Server.

Especificação, plano e tarefas: `specs/001-consultas-visor/`. Design: `DESIGN.md`.

## Como rodar

O passo a passo completo, com os cenários de validação, está em [specs/001-consultas-visor/quickstart.md](specs/001-consultas-visor/quickstart.md). Em resumo:

1. Instalar o SDK do .NET (veja [Versão do SDK](#versão-do-sdk)) e ter acesso ao feed `TCMRIO INTERNO` no `NuGet.Config`.
2. Aplicar os scripts de `dbchange/196.1.1` no `CSLIB_UserManager` (veja [Scripts do banco](#scripts-do-banco-dbchange)).
3. Guardar o `ClientSecret` do cliente `SIAT-dev` nos user-secrets (veja [ClientSecret](#clientsecret)).
4. Rodar:

```
dotnet run --project src/Tcmrj.Siat.Web
```

A aplicação abre em `https://localhost:7271` e redireciona para o login do SIA. Em desenvolvimento valem `src/Tcmrj.Siat.Web/appsettings.json` e `appsettings.Development.json`.

## Versão do SDK

O `global.json` fixa o SDK `10.0.100` com `rollForward: latestMajor`: o SDK 10 ou o 11 compilam o projeto, que continua em `net10.0`. Os testes usam o executor Microsoft.Testing.Platform, por isso o comando é `dotnet test --project ...`.

## Configuração

| Seção | Chaves | Observação |
|---|---|---|
| `TcmrjOidc` | `Authority`, `ClientId`, `RequireHttpsMetadata`, `ClientSecret` | Login no SIA (OpenIddict). `ClientId` é `SIAT-dev` em desenvolvimento e `SIAT` em PRD. O `ClientSecret` nunca vai para arquivo versionado (veja abaixo). |
| `TcmrjSessao` | `MinutosSemUsoParaLogout` (30), `MinutosParaRevalidacao` (1) | Logout por inatividade. Consultar, filtrar, abrir um item, exportar e navegar contam como uso. Para testar na mão, use `MinutosSemUsoParaLogout = 2`. |
| `UserManagerService` | `ApplicationId` | Id do SIAT na tabela `Apps` do `CSLIB_UserManager`: o `@AppId` do `dbchange/196.1.1/001...sql`. Sem ele a aplicação não sobe. |
| `ConnectionStrings` | `UserManagerConnection`, `AuditoriaDbConnection`, `Consultas` | As duas primeiras apontam para o `CSLIB_UserManager` (permissões e nomes de aplicação) e para o `AUDITORIADB` (log do próprio SIAT). `Consultas` é o modelo da conexão com os servidores da lista (o servidor e o banco vêm do `servidores.json`; autenticação integrada, conta de serviço somente leitura). |
| `ConsultasOptions` | `CommandTimeout` (60 s), `ConnectTimeout` (15 s) | Tempos limite das consultas. |
| `Servidores` | veja [Lista de servidores](#lista-de-servidores-servidoresjson) | Vem do `servidores.json`. |

### ClientSecret

O `ClientSecret` não fica em `appsettings`. Em desenvolvimento:

```
dotnet user-secrets --project src/Tcmrj.Siat.Web set "TcmrjOidc:ClientSecret" "<segredo do SIAT-dev>"
```

No IIS, defina a variável de ambiente `TcmrjOidc__ClientSecret` (dois sublinhados) para o processo do site. Um segredo errado só aparece no primeiro login, como `invalid_client` do SIA.

## Publicação no IIS

- Instalar o Hosting Bundle do .NET 10 no servidor.
- Ligar o recurso **WebSockets** do IIS: o Blazor Server depende de uma conexão contínua (SignalR). Sem ele a tela cai para um transporte mais lento ou não conecta.
- Definir `TcmrjOidc__ClientSecret` e conferir `TcmrjOidc:Authority`, `ClientId = SIAT`, `UserManagerService:ApplicationId` e as connection strings do ambiente.
- **Chaves de proteção de dados** (cookie de sessão e antiforgery): o pool de aplicativos precisa de **perfil de usuário carregado** (`Load User Profile = True`), para o ASP.NET Core guardar as chaves no perfil da conta do pool. Sem isso as chaves ficam só na memória e o login e os formulários quebram a cada reinício. A alternativa é chamar `AddDataProtection().PersistKeysToFileSystem(...)` apontando para uma pasta que só a conta do pool lê e grava (o código do SIAT ainda não faz essa chamada).
- **URLs de retorno do login**: o cliente OIDC precisa ter `https://<nome-do-siat>/signin-oidc` e `https://<nome-do-siat>/signout-callback-oidc` cadastrados (script `002` do `dbchange`). Se o SIAT for publicado num subdiretório do IIS, o subdiretório entra antes: `https://<nome>/<subdiretório>/signin-oidc`.
- Conferir que a conta do pool tem leitura em `CSLIB_UserManager` e `AUDITORIADB` e nos servidores do `servidores.json`.
- Os servidores de PRD ficam só no `servidores.json` do servidor de publicação.

## Scripts do banco (`dbchange`)

Em `dbchange/196.1.1`, para o `CSLIB_UserManager` (veja [contracts/usermanager.md](specs/001-consultas-visor/contracts/usermanager.md)):

| Script | O que faz |
|---|---|
| `001 ... Criação de app, recursos e grupos do SIAT.sql` | Cadastra a aplicação SIAT, os recursos `MailQueue`, `AuditLog`, `MailQueuePRD` e `AuditLogPRD` e os grupos `Consultas` e `Consultas PRD`. Idempotente. |
| `002 ... OpenIddictApplications do SIAT.sql` | Cadastra os clientes OIDC `SIAT-dev` e `SIAT`. Idempotente. |

Antes de executar:

- **`@AppId` do `001`**: o valor `178` é provisório. Confirmar o maior `Id` de `Apps` em DSV e em PRD (`SELECT MAX([Id]) FROM Apps`; em DSV era 177 quando o script foi escrito) e usar um valor livre nos dois. O mesmo valor vai em `UserManagerService:ApplicationId`.
- **Hashes do `002`**: `@HashSecretDev` e `@HashSecretPrd` são placeholders (`<HASH_DO_CLIENTSECRET_...>`). O script se recusa a inserir o cliente enquanto não forem trocados pelo hash do `ClientSecret` (o cabeçalho do script explica como gerar o hash). O segredo em claro nunca entra no repositório.
- **Nome DNS de PRD**: o `@UrlBasePrd` do `002` é um placeholder (`https://siat.tcmrj.tcmnet.tcm`). Confirmar o nome e o subdiretório, se houver.

## Lista de servidores (`servidores.json`)

Os servidores consultáveis vêm de uma lista fechada, no arquivo `servidores.json` ao lado do executável (`src/Tcmrj.Siat.Web/servidores.json` no código-fonte). O arquivo é opcional e recarregado ao ser alterado. Não existe campo de servidor livre na tela: só aparecem as chaves desta lista, filtradas pelas permissões do usuário.

| Campo | Significado |
|---|---|
| `Chave` | Identificador estável usado na URL (`?servidor=<Chave>`) e no registro de consulta. Único. |
| `Nome` | Nome mostrado ao usuário. |
| `Endereco` | Instância SQL Server (`servidor\instância` ou `servidor,porta`). |
| `Ambiente` | `DSV`, `HML` ou `PRD`. Obrigatório: sem ele a lista é recusada. |
| `BancoMailQueue`, `BancoAuditoria` | Opcionais; trocam o nome padrão do banco no servidor. |

O `servidores.json` do repositório, usado em desenvolvimento, **não contém servidores de PRD**. Os de PRD ficam só no arquivo do servidor de publicação.

### Exemplo de entrada de PRD (arquivo do servidor de publicação)

```json
{
  "Servidores": [
    { "Chave": "tcmbd4-hml", "Nome": "TCMBD4\\HML", "Endereco": "TCMBD4\\HML", "Ambiente": "HML" },
    { "Chave": "tcmbd-prd", "Nome": "TCMBD\\PRD", "Endereco": "TCMBD\\PRD", "Ambiente": "PRD" }
  ]
}
```

### Permissões por ambiente

| Ambiente | Recurso do UserManager (permissão `CanSelect`) |
|---|---|
| DSV e HML | `MailQueue`, `AuditLog` |
| PRD | `MailQueuePRD`, `AuditLogPRD` |

Quem não tem o recurso de PRD do módulo não vê servidores de PRD no seletor, e a URL com a chave de PRD mostra "Acesso negado" sem consultar o banco. Em PRD a faixa de contexto fica destacada e avisa que a consulta é registrada em log. Toda tentativa de consulta, inclusive a negada, é registrada em `AUDITORIADB.audit.Log` (`TypeID` 10).

## Compilar e testar

```
dotnet build src/Siat.slnx
dotnet test --project src/Tcmrj.Siat.Test/Tcmrj.Siat.Test.csproj
dotnet test --project src/Tcmrj.Siat.Test.Interface/Tcmrj.Siat.Test.Interface.csproj
```

O build deve terminar sem avisos. Os testes de interface (Playwright) sobem o SIAT real numa porta livre, com autenticação de teste e bancos em memória: nenhum SIA nem SQL Server é tocado. Eles usam o **Microsoft Edge instalado** (`Channel = "msedge"`), sem baixar navegadores. Variáveis de ambiente:

| Variável | Efeito |
|---|---|
| `SIAT_PLAYWRIGHT_CHANNEL` | Troca o canal do navegador (por exemplo, `chrome`). Vazia, usa o Chromium do Playwright, se estiver instalado. |
| `SIAT_PLAYWRIGHT_HEADED` | `1` ou `true` mostra a janela do navegador. |

O que não dá para automatizar (contraste e foco visível nos dois temas, paridade com o Visor, desempenho, login real no SIA) está no roteiro manual do [quickstart](specs/001-consultas-visor/quickstart.md).

## Pendências

- **Nível do registro das consultas no log (T111).** O DBLog (`Tcmrj.CoreLib.DBLog`) só grava no `AUDITORIADB.audit.Log` eventos de nível Error com `LogUID`. Por isso o `RegistroConsultaService` registra cada consulta com `LogErrorWithLogUID(10, ...)`, mesmo sendo um evento comum (o `TypeID` 10 identifica o registro de consulta). Há um pedido pendente à equipe da CoreLib para o DBLog aceitar o nível Information com `LogUID`. Quando sair, a linha a trocar é a chamada `LogErrorWithLogUID` em `src/Tcmrj.Siat.App/Web/Services/RegistroConsultaService.cs` (veja [research.md R7](specs/001-consultas-visor/research.md)).
- **Nome DNS de PRD**: o `@UrlBasePrd` do script `002` é um placeholder (questão em aberto 5 do PRD).
- **`AppId` do SIAT**: confirmar o valor livre em DSV e PRD (hoje 178, provisório).
- **Hashes do `ClientSecret`** de `SIAT-dev` e `SIAT` no script `002`.
- **Quem recebe o grupo `Consultas PRD`**: questão em aberto 4 do PRD.
- **Validação manual (T110)**: cenários 1 a 22 do quickstart, paridade com o Visor (SC-002), desempenho (SC-003 e SC-004, com os planos de execução com e sem filtros) e o roteiro de acessibilidade WCAG 2.2 AA.
