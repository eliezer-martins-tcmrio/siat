# Contrato: conexão com o GitHub (OAuth)

Decisões em [research.md R2](../research.md#r2-conectar-ao-github-oauth) e [R3](../research.md#r3-a-conexão-dentro-do-circuito-blazor).

## Aplicação OAuth no GitHub

| Campo | Desenvolvimento | Servidor |
|---|---|---|
| Tipo | OAuth App da organização | OAuth App da organização |
| Application name | `SIAT (desenvolvimento)` | `SIAT` |
| Homepage URL | `https://localhost:7271/` | `https://<host do SIAT>/` |
| Authorization callback URL | `https://localhost:7271/` | `https://<host do SIAT>/` (aceita `/siat_dsv/copilot/github/retorno`, `/siat/copilot/github/retorno` etc.) |
| Enable Device Flow | desmarcado | desmarcado |

Quem cria: owner da organização ou quem a coordenação indicar (PRD, questão em aberto 2). Se a organização restringe OAuth Apps, um owner aprova a aplicação. `ClientId` e `ClientSecret` vão para `Copilot:OAuth` ([configuracao.md](./configuracao.md)).

## Esquemas no ASP.NET Core

| Esquema | Tipo | Configuração |
|---|---|---|
| `GitHubOAuth` | `AddOAuth` | `AuthorizationEndpoint = https://github.com/login/oauth/authorize`; `TokenEndpoint = https://github.com/login/oauth/access_token`; `UserInformationEndpoint = https://api.github.com/user`; `CallbackPath = /copilot/github/retorno`; escopo vazio; `UsePkce = true`; `SaveTokens = false`; `SignInScheme = GitHubConexao` |
| `GitHubConexao` | `AddCookie` | Nome `.Siat.GitHub`; `HttpOnly`; `SecurePolicy = Always`; `SameSite = Lax`; caminho = `<base href>`; `ExpireTimeSpan = 30 dias`; `SlidingExpiration = false`; sem redirecionamentos de login/acesso negado |

Os esquemas só são registrados quando `Copilot:OAuth` está completo. Nenhum dos dois é esquema padrão: a autenticação do SIAT (OIDC ou usuário de rede) não muda.

## Fluxo

1. `copilot/github/conectar` (usuário com `CopilotProprio.CanSelect`): guarda `matricula` e `retorno` nas propriedades do desafio (protegidas no `state`) e redireciona ao GitHub.
2. GitHub → `copilot/github/retorno`: o manipulador confere `state`, cookie de correlação e PKCE, troca o código pelo token.
3. `OnCreatingTicket`:
   1. `GET https://api.github.com/user` com o token do usuário → `id`, `login`.
   2. `AuthenticateAsync()` do esquema padrão → matrícula do usuário atual; diferente da guardada no passo 1 → falha `outro-usuario`.
   3. `GET /orgs/{org}/memberships/{login}` com o token de leitura → `state = active` e `user.id = id`, senão falha `nao-membro`.
   4. Identidade com `github_id`, `github_login`, `matricula`; `IsPersistent = true`, `ExpiresUtc = agora + 30 dias`, `AllowRefresh = false`.
   5. Registra "Conexão" no log; o token do usuário é descartado.
4. `OnRemoteFailure` (acesso negado no GitHub, `state` inválido, falha do passo 3): registra "Recusa" com o motivo, `HandleResponse()` e redireciona para `copilot/meu-consumo?conexao={motivo}`.

## Leitura no circuito

`ConexaoGitHubMiddleware`, depois de `UseAuthentication`, em toda requisição com usuário autenticado: `AuthenticateAsync("GitHubConexao")`; se válido e `matricula` igual à do usuário, `HttpContext.User.AddIdentity(new ClaimsIdentity([github_id, github_login], "GitHubConexao"))`. `IConexaoGitHubService.ObterAsync()` lê essa identidade do `IUsuarioAtualService` e devolve `ConexaoGitHub?`.

## Desconexão

`copilot/github/desconectar`: `SignOutAsync("GitHubConexao")`, registra "Desconexão" (motivo `usuario` ou `nao-membro`) e redireciona com navegação completa: com motivo `nao-membro`, sempre para `copilot/meu-consumo?conexao=nao-membro-desconectada`; senão, para `retorno`, se for local, ou para `copilot/meu-consumo?conexao=desconectada`. A página chama com `forceLoad: true`, para abrir um circuito sem a identidade.

## Registro no log (DBLog, TypeID 10)

| Operação | Quando | Campos |
|---|---|---|
| `Conexao` | Ligação criada | Matrícula, nome, `GitHubLogin`, `GitHubId`, `Organizacao` |
| `Recusa` | Fluxo terminou sem ligação | Matrícula, `GitHubLogin` e `GitHubId` quando conhecidos, `Motivo` (`negada`, `falha`, `nao-membro`, `outro-usuario`) |
| `Desconexao` | Cookie apagado | Matrícula, `GitHubLogin`, `GitHubId`, `Motivo` (`usuario`, `nao-membro`) |

Nunca registra token, código de autorização nem `state`.
