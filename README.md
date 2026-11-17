# SIAT

Sistema de Informações e Apoio Técnico do TCMRJ. A primeira entrega é o Visor de consultas: **MailQueue** e **AuditLog**, somente leitura, sobre uma lista fechada de servidores SQL Server.

Especificação, plano e tarefas: `specs/001-consultas-visor/`. Design: `DESIGN.md`.

## Lista de servidores (`servidores.json`)

Os servidores consultáveis vêm de uma lista fechada, no arquivo `servidores.json` ao lado do executável (`src/Tcmrj.Siat.Web/servidores.json` no código-fonte). O arquivo é opcional e recarregado ao ser alterado. Não existe campo de servidor livre na tela: só aparecem as chaves desta lista, filtradas pelas permissões do usuário.

| Campo | Significado |
|---|---|
| `Chave` | Identificador estável usado na URL (`?servidor=<Chave>`) e no registro de consulta. Único. |
| `Nome` | Nome mostrado ao usuário. |
| `Endereco` | Instância SQL Server (`servidor\instância` ou `servidor,porta`). |
| `Ambiente` | `DSV`, `HML` ou `PRD`. Obrigatório: sem ele a lista é recusada. |
| `BancoMailQueue`, `BancoAuditoria` | Opcionais; trocam o nome padrão do banco no servidor. |

O `servidores.json` do repositório, usado em desenvolvimento, **não contém servidores de PRD**. Os de PRD ficam só no arquivo do servidor de publicação.

### Exemplo de entrada de PRD (arquivo do servidor de publicação)

```json
{
  "Servidores": [
    { "Chave": "tcmbd4-hml", "Nome": "TCMBD4\\HML", "Endereco": "TCMBD4\\HML", "Ambiente": "HML" },
    { "Chave": "tcmbd-prd", "Nome": "TCMBD\\PRD", "Endereco": "TCMBD\\PRD", "Ambiente": "PRD" }
  ]
}
```

### Permissões por ambiente

| Ambiente | Recurso do UserManager (permissão `CanSelect`) |
|---|---|
| DSV e HML | `MailQueue`, `AuditLog` |
| PRD | `MailQueuePRD`, `AuditLogPRD` |

Quem não tem o recurso de PRD do módulo não vê servidores de PRD no seletor, e a URL com a chave de PRD mostra "Acesso negado" sem consultar o banco. Em PRD a faixa de contexto fica destacada e avisa que a consulta é registrada em log. Toda tentativa de consulta, inclusive a negada, é registrada em `AUDITORIADB.audit.Log` (`TypeID` 10).

## Compilar e testar

```
dotnet build src/Siat.slnx
dotnet test --project src/Tcmrj.Siat.Test/Tcmrj.Siat.Test.csproj
dotnet test --project src/Tcmrj.Siat.Test.Interface/Tcmrj.Siat.Test.Interface.csproj
```

Os testes de interface usam o Microsoft Edge instalado (Playwright, sem baixar navegadores).
