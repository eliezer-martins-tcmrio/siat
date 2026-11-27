# Contrato: recurso CopilotProprio no UserManager (`dbchange`)

Script idempotente `002 - TCMRio - CSLIB_UserManager - Recurso CopilotProprio e grupo Copilot do SIAT.sql`, em `dbchange/196.1.1/` (pasta da sprint do script 001; trocar se a sprint tiver mudado), executado em `CSLIB_UserManager` de DSV e de PRD, no padrão do script 001 e de [scripts-sqlserver.instructions.md](../../../.github/instructions/scripts-sqlserver.instructions.md).

| Tabela | Registro |
|---|---|
| `Apps` | Nenhum novo: usa a aplicação `SIAT` (`@SIAT_AppID`, conferido pelo nome; erro se não existir) |
| `AppResources` | `Id = 50`, `ResourceKey = 'CopilotProprio'`, `Description = 'Consumo próprio do Copilot'`, `ParentId = NULL` |
| `Groups` | `Copilot` (Id sequencial na tabela inteira, como no 001) |
| `Accesses` | `Copilot`: `CanSelect = 1` em `CopilotProprio`; demais permissões em 0 |

- Os recursos `Copilot` (painel, CP-01) e a permissão `CanEdit` (limites, CP-02) entram nos scripts das próximas etapas.
- Quem entra no grupo `Copilot` é decisão da coordenação (PRD, questão em aberto 4); o script não inclui usuários.
- Política no SIAT: `Modulo.CopilotProprio.Politica()` = `CopilotProprio.CanSelect`.
