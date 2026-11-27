# Contrato: configuração do Copilot

## `appsettings.json` (versionado)

```json
"Copilot": {
  "Organizacao": "<organização no GitHub>",
  "GitHub": {
    "Token": "<token de leitura de billing>",
    "TempoLimiteSegundos": 15
  },
  "OAuth": {
    "ClientId": "<Client ID da OAuth App>",
    "ClientSecret": "<Client secret da OAuth App>"
  },
  "LeiturasSimultaneas": 4,
  "Cache": {
    "MinutosLicencasETimes": 5,
    "SegundosBudgets": 30,
    "HorasPerfil": 6,
    "MinutosRelatoriosRecentes": 10,
    "HorasRelatoriosFechados": 24
  }
}
```

`Copilot:GitHub:Token` e `Copilot:OAuth:ClientSecret` ficam no `appsettings`, junto do resto da seção, como o token do GitLab (constituição 3.2.0, Princípio III). O SIAT não usa `dotnet user-secrets`.

| Onde | Como |
|---|---|
| Desenvolvimento | `src/Tcmrj.Siat.Web/appsettings.json`, com a OAuth App de desenvolvimento (`https://localhost:7271/`) |
| IIS (DSV, HML, PRD) | `appsettings.json` publicado no site, com a OAuth App do servidor |

## Validação

`CopilotOptions` é validado na leitura, não na subida:

- `Organizacao`, `GitHub:Token`, `OAuth:ClientId` e `OAuth:ClientSecret` não vazios;
- `TempoLimiteSegundos` entre 1 e 60; `LeiturasSimultaneas` entre 1 e 8;
- prazos de cache maiores que zero, com `HorasRelatoriosFechados` até 168.

Seção incompleta: a página do Copilot mostra "O módulo Copilot não está configurado no servidor.", os esquemas OAuth não são registrados e o resto do SIAT sobe e funciona (Princípio IV). O token e o segredo nunca aparecem em mensagem de erro, log nem exceção registrada.
