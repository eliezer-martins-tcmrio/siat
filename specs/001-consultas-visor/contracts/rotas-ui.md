# Contrato: rotas e parâmetros de URL

Toda rota exige usuário autenticado (fallback policy), menos `/Account/Login`. Pela constituição 1.1.1 (Princípio III), páginas que exibem dados exigem também um recurso do UserManager; a página inicial e o logout não exibem dados e exigem só autenticação. Sem sessão, o usuário vai para `Account/Login` (challenge OIDC no SIA de PRD) e volta para a URL pedida.

| Rota | Acesso | Comportamento |
|---|---|---|
| `/` | autenticado | Vai para a primeira ferramenta de Consultas permitida (MailQueue, depois AuditLog). Sem nenhuma: mensagem "Você não tem acesso às ferramentas do SIAT", com a orientação de pedir acesso ao grupo `Consultas` no SIA (FR-006). |
| `/consultas/mailqueue` | `MailQueue.CanSelect` ou `MailQueuePRD.CanSelect` | Tela MailQueue. Sem nenhum dos dois: acesso negado. |
| `/consultas/auditlog` | `AuditLog.CanSelect` ou `AuditLogPRD.CanSelect` | Tela AuditLog. Sem nenhum dos dois: acesso negado. |
| `/Account/Login` | anônimo | Challenge no esquema `oidc` (`TcmrjOidcDefaults.OidcScheme`); `returnUrl` só se for local, senão `/`. |
| `/Account/Logout` | autenticado | Só **POST** com token antiforgery: sign-out do `oidc` e do cookie `Cookies`. GET apenas redireciona para `/` (impede logout por link de outro site). O botão **Sair** do menu é um formulário POST com `<AntiforgeryToken />`. |
| `/signin-oidc`, `/signout-callback-oidc` | handler OIDC | Callbacks cadastrados no `OpenIddictApplications`. |

**Fim de sessão por inatividade**: depois de `TcmrjSessao:MinutosSemUsoParaLogout` minutos sem navegação nem ação de consulta, o circuito passa a anônimo e o `<NotAuthorized>` do `Routes.razor` renderiza `EncerrarSessao`, que envia o POST de `/Account/Logout`. Isso encerra o cookie e a sessão no SIA; na próxima ação o usuário volta ao login e, depois dele, à URL pedida (que refaz a consulta).

## Parâmetros de consulta

Parâmetros desconhecidos são ignorados. Parâmetros inválidos não disparam consulta; a tela mostra a mensagem do campo.

### `/consultas/mailqueue`

| Parâmetro | Tipo | Regra |
|---|---|---|
| `servidor` | chave da lista fechada | obrigatório para consultar; chave inexistente: "Servidor não encontrado na lista"; chave de PRD sem `MailQueuePRD.CanSelect`: acesso negado, sem consulta |
| `smtp` | int | opcional; ausente = todos os SMTPs |
| `texto` | string | opcional; procurado literalmente em assunto, corpo, destinatários e MessageId |
| `qtd` | int | 1 a 1000, padrão 50 (regra de ajuste no [data-model](../data-model.md)) |
| `sel` | MessageId | opcional; linha aberta |

Exemplo: `/consultas/mailqueue?servidor=tcmbd4-hml&smtp=3&texto=boleto&qtd=100&sel=5F0A...`

### `/consultas/auditlog`

| Parâmetro | Tipo | Regra |
|---|---|---|
| `servidor` | chave da lista fechada | como na MailQueue, com `AuditLogPRD.CanSelect` para PRD |
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
