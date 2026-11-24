# AGENTS.md

Instruções para agentes de código (Copilot, Claude Code, Codex, Cursor e outros) no SIAT.

As instruções ficam em `.github/` e valem para qualquer agente. Leia antes de trabalhar:

1. **[.github/copilot-instructions.md](.github/copilot-instructions.md)** — visão geral, comandos, arquitetura e
   convenções. Comece por aqui.
2. **[.specify/memory/constitution.md](.specify/memory/constitution.md)** — regras obrigatórias do projeto;
   prevalecem sobre tudo.

Instruções por tipo de arquivo, em `.github/instructions/` (leia a que corresponde ao que vai alterar):

| Arquivo | Quando ler |
|---|---|
| [csharp.instructions.md](.github/instructions/csharp.instructions.md) | ao editar `*.cs` ou `*.razor` |
| [tests.instructions.md](.github/instructions/tests.instructions.md) | ao criar ou editar testes em `src/Tcmrj.Siat.Test` |
| [scripts-sqlserver.instructions.md](.github/instructions/scripts-sqlserver.instructions.md) | ao criar ou editar `*.sql` (`dbchange/` ou `Infra/Sql`) |
| [database.instructions.md](.github/instructions/database.instructions.md) | ao consultar os bancos de desenvolvimento |
| [code-review-generic.instructions.md](.github/instructions/code-review-generic.instructions.md) | ao revisar código |

Skills de fluxo de trabalho (Spec Kit e spec-driven) ficam em `.github/skills/` e `.claude/skills/`.

## Essencial

- Build: `dotnet build src/Siat.slnx`
- Testes: `dotnet test --solution src/Siat.slnx`
- Executar: `dotnet run --project src/Tcmrj.Siat.Web`
- Tudo em português do Brasil: interface, mensagens, identificadores, comentários e respostas.
