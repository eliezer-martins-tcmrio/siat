---
description: 'Padrões para scripts SQL do SIAT (dbchange e SQL embutido)'
applyTo: '**/*.sql'
excludeAgent: "code-review"
---

# Scripts SQL

Há dois tipos de SQL no repositório:

1. **SQL embutido** (`src/Tcmrj.Siat.Infra/Sql/*.sql`): consultas que o SIAT executa. Só `SELECT`, com parâmetros
   (`@Nome`) preenchidos por `SqlParameter` tipado; nunca concatenar valor do usuário.
2. **Scripts de alteração** (`dbchange/`): o SIAT não tem banco próprio, então só existem para cadastrar o SIAT
   em bancos de outros sistemas, hoje o `CSLIB_UserManager` (app, recursos, grupos e cliente OIDC).

O restante deste arquivo trata dos scripts de `dbchange/`.

## Pasta

- Subpastas por versão do sistema, no padrão `MAJOR.MINOR.BUG` (ex.: `dbchange/196.1.1`).
- Pergunte ao usuário em qual versão o script entra, se não estiver claro.

## Nome do arquivo

`NNN - TCMRio - <Banco> - <Descrição sucinta>.sql`

- `NNN`: sequencial de três dígitos dentro da pasta (001, 002, …), que define a ordem de execução.
- `<Banco>`: banco alterado (ex.: `CSLIB_UserManager`).
- Exemplo: `001 - TCMRio - CSLIB_UserManager - Criação de app, recursos e grupos do SIAT.sql`.

## Encoding

- UTF-8. Ao executar pelo `sqlcmd`, use `-f 65001`.

## Conteúdo

- Primeira instrução seleciona o banco:

```sql
USE [CSLIB_UserManager]
GO
```

- Use transação com `SET XACT_ABORT ON` e `BEGIN TRY … BEGIN TRANSACTION … COMMIT` / `ROLLBACK` no `CATCH`, como
  nos scripts existentes.
- O script deve ser **idempotente**: rodar de novo não gera erro nem duplica dados. Verifique antes de inserir ou
  alterar:

```sql
IF NOT EXISTS (SELECT * FROM AppResources WHERE [AppId] = @SIAT_AppID AND [Id] = @Recurso_Catalogo)
BEGIN
    INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
    VALUES (...)
END
```

- Segredos não entram no script: no cliente OIDC vai só o hash do `ClientSecret`.
- Novo recurso do UserManager precisa do valor correspondente no enum `Modulo` e da política `Recurso.CanSelect`.
