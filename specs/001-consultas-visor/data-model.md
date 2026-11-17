# Modelo de dados (Fase 1): Consultas — MailQueue e AuditLog

O SIAT não tem banco próprio (Princípio I). As entidades abaixo são modelos de leitura no `Tcmrj.Siat.Domain`, montados a partir de configuração ou de consultas aos servidores da lista fechada. Nenhuma é gravada pelo SIAT; o único efeito persistente é o registro de consulta em `AUDITORIADB.audit.Log` pelo DBLog.

Criação por `Create(...)` que valida e devolve `Result<T, Erro>`, como no projeto-modelo; erros catalogados em `Domain/Models/Erros.cs`.

## Enumerações

### Ambiente
`Dsv`, `Hml`, `Prd`. Descrição para tela: `DSV`, `HML`, `PRD`; valor de `data-amb`: `dsv`, `hml`, `prd`.

### Modulo
`MailQueue`, `AuditLog`.

Regra `PoliticaPara(Ambiente)` (FR-008, R4):

| Módulo | DSV / HML | PRD |
|---|---|---|
| MailQueue | `MailQueue.CanSelect` | `MailQueuePRD.CanSelect` |
| AuditLog | `AuditLog.CanSelect` | `AuditLogPRD.CanSelect` |

`Politicas()` devolve as duas políticas do módulo (usada para decidir se o item aparece no menu e se a página abre).

## Servidor
Item da lista fechada (`servidores.json`, [contracts/servidores.schema.json](./contracts/servidores.schema.json)).

| Campo | Tipo | Regras |
|---|---|---|
| Chave | string | obrigatória, única na lista, `^[a-z0-9-]{1,40}$`; é o único valor que vai na URL |
| Nome | string | obrigatório, até 60; exibido no seletor e na faixa de contexto |
| Endereco | string | obrigatório, instância SQL Server (`MAQUINA\INSTANCIA`); nunca vem do usuário |
| Ambiente | Ambiente | obrigatório |
| BancoMailQueue | string | padrão `MailQueue` |
| BancoAuditoria | string | padrão `AUDITORIADB` |

Lista inválida (chave repetida, campo ausente) é recusada inteira na validação das options; a tela informa que não há servidores configurados.

## Smtp
Lido de `MailQueue.dbo.SMTPs` do servidor escolhido.

| Campo | Tipo | Origem |
|---|---|---|
| SmtpId | int | `SmtpId` |
| Descricao | string | `Description` |

## MensagemEnfileirada
Lida de `MailQueue.dbo.Messages` + `dbo.Recipients` (SQL do Visor).

| Campo | Tipo | Origem |
|---|---|---|
| MessageId | string (36) | `CONVERT(varchar(36), MessageId)` |
| Assunto | string? | `Subject` |
| DataPostagem | DateTime | `DatePosted`; ordem decrescente |
| EnviarApos | DateTime? | `SendAfter` |
| DataEnvio | DateTime? | `DateSent` |
| Enviado | bool | `ISNULL(Sent, 0)` |
| EhHtml | bool | `IsHTML` |
| Corpo | string? | `Message`; exibido só no detalhe, isolado (R10) |
| Destinatarios | string | `STRING_AGG` dos `Recipient`, ou `(sem destinatários)` |

Situação de envio derivada para a tela: `Enviado` → "Enviado em {DataEnvio}"; não enviado com `EnviarApos` futuro → "Agendado"; senão "Na fila".

## Aplicacao
Lida de `CSLIB_UserManager.dbo.Apps` do servidor escolhido.

| Campo | Tipo | Origem |
|---|---|---|
| Id | int | `Id` |
| Nome | string | `Name` |

Lista indisponível (sem acesso ao banco): a feature devolve lista vazia com aviso, e a tela troca o seletor por um campo numérico (cenário 2 da história 2).

## RegistroAuditoria
Lido de `AUDITORIADB.audit.Log` + `audit.Log_Types` (SQL do Visor).

| Campo | Tipo | Origem |
|---|---|---|
| LogId | long | `LogID`; ordem decrescente |
| TipoId | byte | `TypeID` |
| Tipo | string | `Type`, ou `(tipo não cadastrado)` |
| Data | DateTime | `Date` |
| Mensagem | string? | `Message` |
| LogUid | string? | `CONVERT(varchar(36), LogUID)` |
| AppId | int | `AppID` |
| Excecao | string? | `Exception`; formatada no detalhe (R11) |
| LogEvent | string? | `LogEvent`; JSON indentado ou texto original (R11) |

## FiltroMailQueue (objeto de valor)

| Campo | Tipo | Regras (FR-010, FR-016) |
|---|---|---|
| SmtpId | int? | nulo = todos os SMTPs |
| Texto | string? | aparado; vazio = sem filtro; até 500 caracteres; vai ao SQL como padrão `LIKE` escapado (`%texto%`, com `[`, `%`, `_` literais) |
| Quantidade | int | 1 a 1000, padrão 50 |

## FiltroAuditLog (objeto de valor)

| Campo | Tipo | Regras (FR-013) |
|---|---|---|
| AppId | int? | nulo = todas as aplicações |
| PrefixoLogId | string? | `^\d{1,20}$`; outro conteúdo é recusado com "Informe apenas dígitos no LogID." e a consulta não é feita |
| Quantidade | int | 1 a 1000, padrão 50 |

### Quantidade fora do limite
Na URL e no formulário: não numérica → recusada com mensagem; numérica fora de 1 a 1000 → ajustada ao limite mais próximo (mesmo comportamento do Visor `Math.Clamp`). Nunca é enviada como veio.

## RegistroConsulta (evento de log)
Não é tabela do SIAT: é o evento gravado pelo DBLog em `audit.Log` (R7). Formato em [contracts/registro-consulta.md](./contracts/registro-consulta.md).

| Campo | Origem |
|---|---|
| Operacao | `Consulta` ou `Exportacao` |
| Modulo | `MailQueue` ou `AuditLog` |
| Servidor | chave e nome |
| Ambiente | DSV, HML, PRD |
| Matricula, Usuario | claims `matricula` e `nome` |
| Filtros | objeto com os campos do filtro usado |
| Quantidade de itens | resultado (só na exportação e na consulta concluída) |
| Data e hora | carimbo do DBLog |

## Estado da tela (Web, não persistido)

Por página, no circuito Blazor:

```text
Ociosa ──consultar──▶ Consultando ──ok──▶ ComResultado (lista, sel?)
   ▲                     │  │                  │
   │                     │  └──erro/tempo──▶ ComErro (mantém resultado anterior visível)
   │                     └──cancelar──▶ estado anterior (resultado anterior permanece)
   └──────────── nova consulta cancela a em andamento; só a última é aplicada
```

- Depois de 1 s em `Consultando`, aparecem o tempo decorrido e **Cancelar** (história 4).
- `sel` (item aberto) só é aplicado se o item existir no resultado.
- `ComResultado` vazio com filtros preenchidos oferece "Remover filtros".
