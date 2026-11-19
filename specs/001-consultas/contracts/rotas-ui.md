# Contrato: rotas e parâmetros de URL

Toda rota exige usuário autenticado (fallback policy), menos `/Account/Login`, os arquivos estáticos e `/saude`. Pela constituição 1.5.0 (Princípio III), páginas que exibem dados exigem também um recurso do UserManager; a página inicial e o logout não exibem dados e exigem só autenticação. Sem sessão, o usuário vai para `Account/Login` (challenge OIDC no SIA de PRD) e volta para a URL pedida.

As rotas são relativas ao caminho do aplicativo no IIS (`SiatConfiguration:BlazorBaseHref`, por exemplo `/siat_dsv`). Esta tabela é a do modo padrão, `SiatConfiguration:Autenticacao = Oidc`; no modo experimental `UsuarioDeRede` (só DSV e HML), `/Account/Login`, `/Account/Logout` e os callbacks do OIDC não existem, o menu não tem **Sair**, e a entrada é pelo usuário do Windows ([prototipo-login-rede](../../prototipo-login-rede/README.md)).

| Rota | Acesso | Comportamento |
|---|---|---|
| `/` | autenticado | Vai para a primeira ferramenta de Consultas permitida (MailQueue, depois AuditLog). Sem nenhuma: mensagem "Você não tem acesso às ferramentas do SIAT", com a orientação de pedir acesso ao grupo `Consultas` no SIA (FR-006). |
| `/consultas/mailqueue` | `MailQueue.CanSelect` | Tela MailQueue (DSV, HML e PRD). Sem o recurso: acesso negado. |
| `/consultas/auditlog` | `AuditLog.CanSelect` | Tela AuditLog (DSV, HML e PRD). Sem o recurso: acesso negado. |
| `/Account/Login` | anônimo | Challenge no esquema `oidc` (`TcmrjOidcDefaults.OidcScheme`); `returnUrl` só se for local, senão a página inicial do aplicativo (`~/`). |
| `/Account/Logout` | autenticado | Só **POST** com token antiforgery: sign-out do `oidc` e do cookie `Cookies`, com volta do SIA para a página inicial do aplicativo (`~/`). GET apenas redireciona para ela (impede logout por link de outro site). O botão **Sair** do menu é um formulário POST com `<AntiforgeryToken />`. |
| `/signin-oidc`, `/signout-callback-oidc` | handler OIDC | Callbacks cadastrados no `OpenIddictApplications`. |
| `/saude` | anônimo | Responde `ok` (texto), para o balanceador e o monitoramento. Não lê nenhuma fonte de dados. |

**Fim de sessão por inatividade**: depois de `TcmrjSessao:MinutosSemUsoParaLogout` minutos sem navegação nem ação de consulta, o circuito passa a anônimo e o `<NotAuthorized>` do `Routes.razor` renderiza `EncerrarSessao`, que envia o POST de `/Account/Logout`. Isso encerra o cookie e a sessão no SIA; na próxima ação o usuário volta ao login e, depois dele, à URL pedida (que refaz a consulta). No modo `UsuarioDeRede` não há sessão para encerrar: renderiza `SessaoEncerrada`, cujo botão **Continuar** recarrega a página e entra de novo pelo usuário do Windows.

## Parâmetros de consulta

Parâmetros desconhecidos são ignorados. Parâmetros inválidos não disparam consulta; a tela mostra a mensagem do campo.

### `/consultas/mailqueue`

| Parâmetro | Tipo | Regra |
|---|---|---|
| `servidor` | chave da lista fechada | obrigatório para consultar; chave inexistente: "Servidor não encontrado na lista"; sem `MailQueue.CanSelect`: acesso negado, sem consulta |
| `smtp` | int | opcional; ausente = todos os SMTPs |
| `texto` | string | opcional; procurado literalmente em assunto, corpo, destinatários e MessageId |
| `qtd` | int | 1 a 1000, padrão 50 (regra de ajuste no [data-model](../data-model.md)) |
| `sel` | MessageId | opcional; linha aberta |

Exemplo: `/consultas/mailqueue?servidor=tcmbd4-hml&smtp=3&texto=boleto&qtd=100&sel=5F0A...`

### `/consultas/auditlog`

| Parâmetro | Tipo | Regra |
|---|---|---|
| `servidor` | chave da lista fechada | como na MailQueue, com `AuditLog.CanSelect` |
| `app` | int | opcional; ausente = todas |
| `logid` | dígitos (1 a 20) | opcional; prefixo do LogID; outro conteúdo: "Informe apenas dígitos no LogID." |
| `qtd` | int | 1 a 1000, padrão 50 |
| `sel` | LogID | opcional; linha aberta |

## Menu

Seção **Consultas** com **MailQueue** e **AuditLog**. Cada item aparece se o usuário tiver ao menos uma das duas políticas do módulo; a seção some se nenhum item aparecer (FR-003). Rodapé: tema (claro, escuro, sistema), nome do usuário e Sair.

## Atalhos de teclado

| Tecla | Efeito |
|---|---|
| `/` | foca o filtro local da lista (fora de campos de texto) |
| `Esc` no filtro | limpa o filtro |
| `↑` / `↓` | move entre as linhas da lista |
| `Enter` / `Espaço` na linha | abre ou fecha o detalhe |
