# Contrato: registro de consulta e exportação no log

Gravado pelo `Tcmrj.CoreLib.DBLog` em `AUDITORIADB.audit.Log` do servidor configurado em `ConnectionStrings:AuditoriaDbConnection` (o de PRD na publicação). Ver a decisão R7 em [research.md](../research.md).

| Coluna | Valor |
|---|---|
| `AppID` | AppId do SIAT (o mesmo de `UserManagerService:ApplicationId`, criado no `dbchange`) |
| `TypeID` | `10` (Log de Informações Gerais) |
| `LogUID` | gerado por `LogErrorWithLogUID` |
| `Message` | `Consulta MailQueue em TCMBD4\HML (HML) por 12345` (ou `Exportação ...`) |
| `LogEvent` | JSON com as propriedades abaixo |

Propriedades no `LogEvent`:

| Propriedade | Exemplo |
|---|---|
| `Operacao` | `Consulta` ou `Exportacao` |
| `Modulo` | `MailQueue` ou `AuditLog` |
| `Servidor` | `tcmbd4-hml` |
| `ServidorNome` | `TCMBD4\HML` |
| `Ambiente` | `DSV`, `HML` ou `PRD` |
| `Matricula` | `12345` |
| `Usuario` | valor da claim `nome` |
| `Filtros` | `{ "SmtpId": 3, "Texto": "boleto", "Quantidade": 100 }` ou `{ "AppId": 12, "PrefixoLogId": "4363", "Quantidade": 50 }` |
| `Itens` | quantidade devolvida (consulta concluída) ou exportada |
| `Resultado` | `Sucesso`, `Cancelada`, `Erro` ou `AcessoNegado` |

Regras:

- Toda tentativa de consulta que passou da validação é registrada, inclusive negada, cancelada ou com erro (SC-005, SC-006).
- O texto de busca entra como digitado; o corpo dos e-mails e o conteúdo dos logs consultados nunca entram no registro.
- Falha ao gravar o log não impede a consulta nem aparece para o usuário (o DBLog já trata a indisponibilidade do banco de log).
