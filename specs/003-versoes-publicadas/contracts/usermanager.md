# Contrato: recurso Versoes_Sistemas no UserManager

Nenhum script novo. O script `dbchange/196.1.1/001 - TCMRio - CSLIB_UserManager - Criação de app, recursos e grupos do SIAT.sql` já cria, de forma idempotente:

| Tabela | Registro |
|---|---|
| `AppResources` | `Id = 40`, `ResourceKey = 'Versoes_Sistemas'`, `Description = 'Versões de sistemas'`, `ParentId = NULL` |
| `Accesses` | Grupo `Sistemas`: `CanSelect = 1` em `Versoes_Sistemas`; demais permissões em 0 |

- O recurso libera as versões de todos os ambientes e de todos os servidores da lista ([research.md R6](../research.md#r6-autorização-e-registro)).
- Quem entra no grupo `Sistemas` é decisão da coordenação; o mesmo grupo já dá o Catálogo.
- Política no SIAT: `Modulo.Versoes_Sistemas.Politica()` = `Versoes_Sistemas.CanSelect`. O nome do valor do enum é o `ResourceKey`, como `Catalogo_Sistemas`.
- Antes de publicar, conferir no `CSLIB_UserManager` de DSV e de PRD que o script 001 foi executado na versão que contém o recurso 40.
