---
description: 'Como consultar os bancos de desenvolvimento usados pelo SIAT'
applyTo: "**"
excludeAgent: "code-review"
---

# Bancos de dados no desenvolvimento

O SIAT **não tem banco próprio** (constituição, Princípio I). Ele lê:

| Banco | Para quê | Connection string (`appsettings.json`) |
|---|---|---|
| `CSLIB_UserManager` | app, recursos, grupos e cliente OIDC do SIAT; vínculo do usuário de rede | `UserManagerConnection` |
| `AUDITORIADB` | log do próprio SIAT (`Tcmrj.CoreLib.DBLog`) | `AuditoriaDbConnection` |
| `MailQueue` / `AuditLog` de cada servidor | módulos de Consultas | modelo `Consultas` + servidor e banco do `servidores.json` |

Em desenvolvimento, `CSLIB_UserManager` e `AUDITORIADB` estão em `TCMBD3\DEV`, com autenticação Windows.

## Regras

- Use o `sqlcmd` só para **leitura** (investigar dados, conferir estrutura). Não altere dados nem estrutura pela
  linha de comando: mudança no `CSLIB_UserManager` é script em `dbchange/` (ver
  [scripts-sqlserver.instructions.md](scripts-sqlserver.instructions.md)).
- Não consulte servidores de produção.

## Comandos

```powershell
# Consulta inline (-W tira espaços, -y 0 evita truncar colunas largas)
sqlcmd -S "TCMBD3\DEV" -d CSLIB_UserManager -E -W -Q "SELECT Id, Name FROM Apps WHERE Name = 'SIAT'"

# Executar um script lendo em UTF-8
sqlcmd -S "TCMBD3\DEV" -d CSLIB_UserManager -E -f 65001 -i "D:\caminho\script.sql"

# Listar tabelas
sqlcmd -S "TCMBD3\DEV" -d CSLIB_UserManager -E -W -Q "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY 1, 2"

# Colunas de uma tabela
sqlcmd -S "TCMBD3\DEV" -d CSLIB_UserManager -E -W -Q "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' ORDER BY ORDINAL_POSITION"
```

Troque `-d` por `AUDITORIADB` para o log do SIAT. Os SQL que o SIAT executa em produção ficam em
`src/Tcmrj.Siat.Infra/Sql/`; `tools/` tem scripts auxiliares de desenvolvimento.
