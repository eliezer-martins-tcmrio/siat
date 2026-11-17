# Contrato: cadastro do SIAT no UserManager (`dbchange`)

Scripts idempotentes em `dbchange/<versão>/`, executados em `CSLIB_UserManager` de DSV e de PRD, no padrão do SiCATI (`sicati-controle-de-ativos/dbchange/163.1.1/005` e `006`).

## 001: aplicação, recursos e grupos

| Tabela | Registro |
|---|---|
| `Apps` | `Id = @AppId` (fixo no script, mesmo valor em DSV e PRD, conferido antes da publicação), `Name = 'SIAT'` |
| `AppResources` | `MailQueue`, `MailQueuePRD`, `AuditLog`, `AuditLogPRD` (`ResourceKey` = nome do recurso; `Description` em português) |
| `Groups` | `Consultas` e `Consultas PRD` |
| `Accesses` | `Consultas`: `CanSelect` em `MailQueue` e `AuditLog`. `Consultas PRD`: `CanSelect` em `MailQueuePRD` e `AuditLogPRD`. Demais permissões em 0 |

Os recursos dos módulos futuros (`Sistemas`, `Versoes`, `Documentacao`, `Copilot`, `CopilotProprio`, `Builds`) entram nos scripts das suas próprias features.

## 002: clientes OIDC (`OpenIddict.OpenIddictApplications`)

| ClientId | DisplayName | RedirectUris | PostLogoutRedirectUris |
|---|---|---|---|
| `SIAT-dev` | `[DEV] SIAT` | `https://localhost:<porta>/signin-oidc` | `https://localhost:<porta>/signout-callback-oidc` |
| `SIAT` | `SIAT` | URL de PRD + `/signin-oidc` (questão em aberto 5 do PRD: nome DNS) | URL de PRD + `/signout-callback-oidc` |

Comum aos dois: `Type = confidential`, `Permissions = ["ept:authorization","ept:logout","ept:token","gt:authorization_code","rst:code","scp:profile","scp:openid"]`, `Requirements = ["ft:pkce"]`, `ClientSecret` com o hash do OpenIddict. O segredo em claro fica fora do repositório (user-secrets em desenvolvimento, configuração protegida no IIS).

## Configuração correspondente no SIAT

Seções lidas pelo pacote `Tcmrj.Autenticacao.AspNetCore.Oidc` (`AddTcmrjOidc` e `AddTcmrjAutorizacaoBlazor`, [research.md R2](../research.md#r2-autenticação-por-oidc-tcmrjautenticacaoaspnetcoreoidc)):

```json
"ConnectionStrings": { "UserManagerConnection": "Server=<PRD>;Database=CSLIB_UserManager;..." },
"TcmrjOidc": { "Authority": "https://tcmrj.tcmnet.tcm/scaf", "ClientId": "SIAT", "RequireHttpsMetadata": true },
"UserManagerService": { "ApplicationId": 0 },
"TcmrjSessao": { "MinutosSemUsoParaLogout": 30, "MinutosParaRevalidacao": 1 }
```

- `TcmrjOidc:ClientSecret` nunca vai para o `appsettings`: user-secrets em desenvolvimento e variável de ambiente `TcmrjOidc__ClientSecret` no IIS. O `appsettings.Development.json` sobrescreve `ClientId` com `SIAT-dev` e `Authority` com o SIA de desenvolvimento.
- `AddTcmrjOidc` falha na inicialização sem `Authority` ou `ClientId`; um `ClientSecret` ausente ou errado só aparece no login (`invalid_client`).
- `ApplicationId` recebe o `@AppId` do script. O pacote não valida esse valor; o SIAT acrescenta `AddOptions<UserManagerServiceOptions>().Validate(o => o.ApplicationId > 0).ValidateOnStart()`, para não subir sem ele.
- As URLs `/signin-oidc` e `/signout-callback-oidc` são os caminhos padrão do handler usados pelo pacote; se o SIAT for publicado num subdiretório do IIS, o subdiretório entra antes deles no cadastro.

## Claims esperadas do SIA

`nome`, `matricula`, `email`, `cpf_cnpj`, `userId` e `sessionId` (`TcmClaimTypes`), lidas com `MapInboundClaims = false` (fixo no pacote) por `Tcmrj.Autenticacao.AspNetCore.Extensions`. `User.Identity.Name` é a matrícula; a autorização usa a `matricula`.
