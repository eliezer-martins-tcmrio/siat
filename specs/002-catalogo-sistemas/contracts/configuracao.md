# Contrato: configuração do Catálogo

## `appsettings.json` (versionado)

```json
"CatalogoSistemas": {
  "GitLab": {
    "BaseUrl": "https://git.tcmnet.tcm",
    "Projeto": "asi/TCMRJDoc",
    "Arquivo": "sistemasDb.js",
    "Ref": "master",
    "Token": "<token>"
  },
  "TempoLimiteSegundos": 10,
  "MinutosDeCache": 10
}
```

## Token (no `appsettings`)

O token fica em `CatalogoSistemas:GitLab:Token`, no `appsettings`, junto do resto da seção, como o `ClientSecret` do OIDC (constituição 3.1.0, Princípio III). O SIAT não usa `dotnet user-secrets`.

| Onde | Como |
|---|---|
| Desenvolvimento | `src/Tcmrj.Siat.Web/appsettings.json` |
| IIS (DSV, HML, PRD) | `appsettings.json` publicado no site |

- Token de acesso do projeto `asi/TCMRJDoc`, papel **Reporter**, escopo **`read_repository`**, com data de validade (máximo de 365 dias no padrão do GitLab). Quem cria: mantenedor do TCMRJDoc. A data de validade vai para a agenda de quem opera o SIAT.

## Validação

- `CatalogoSistemasOptions` é validado na leitura, não na subida: `BaseUrl` absoluto `http`/`https`, `Projeto`, `Arquivo` e `Ref` não vazios, `TempoLimiteSegundos` entre 1 e 60, `MinutosDeCache` entre 1 e 60. Configuração inválida ou token ausente resulta em `Erros.Catalogo.NaoConfigurado` na tela de Sistemas; a aplicação e os outros módulos sobem normalmente (Princípio IV).
- O token nunca aparece em mensagem de erro, log nem exceção registrada.
