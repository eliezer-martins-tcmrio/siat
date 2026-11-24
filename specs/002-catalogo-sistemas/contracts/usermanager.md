# Contrato: recurso Sistemas no UserManager (`dbchange`)

Script idempotente `003 - TCMRio - CSLIB_UserManager - Recurso Sistemas e grupo Catalogo do SIAT.sql`, em `dbchange/196.1.1/` (pasta da sprint atual, a mesma do script 001 e a mais recente entre os repositórios do Tribunal em 2026-11-24; trocar se a sprint tiver mudado), executado em `CSLIB_UserManager` de DSV e de PRD, no mesmo padrão do script 001 ([specs/001-consultas/contracts/usermanager.md](../../001-consultas/contracts/usermanager.md)).

| Tabela | Registro |
|---|---|
| `Apps` | Nenhum novo: usa a aplicação `SIAT` (`@AppId` 93, conferido pelo nome; erro se não existir) |
| `AppResources` | `Id = 30`, `ResourceKey = 'Sistemas'`, `Description = 'Catálogo de sistemas'`, `ParentId = NULL` |
| `Groups` | `Catalogo` (Id sequencial na tabela inteira, como no 001) |
| `Accesses` | `Catalogo`: `CanSelect = 1` em `Sistemas`; demais permissões em 0 |

- O recurso libera o catálogo de todos os ambientes ([research.md R5](../research.md#r5-permissão)).
- Quem entra no grupo `Catalogo` é decisão da coordenação; o script não inclui usuários.
- Política no SIAT: `Modulo.Sistemas.Politica()` = `Sistemas.CanSelect`.
