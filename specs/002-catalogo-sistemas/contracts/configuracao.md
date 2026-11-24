# Contrato: configuração do Catálogo

## `appsettings.json` (versionado, sem segredo)

```json
"CatalogoSistemas": {
  "GitLab": {
    "BaseUrl": "https://git.tcmnet.tcm",
    "Projeto": "asi/TCMRJDoc",
    "Arquivo": "sistemasDb.js",
    "Ref": "master"
  },
  "TempoLimiteSegundos": 10,
  "MinutosDeCache": 10
}
```

## Token (fora do repositório)

| Onde | Como |
|---|---|
| Desenvolvimento | `dotnet user-secrets set "CatalogoSistemas:GitLab:Token" "<token>" --project src/Tcmrj.Siat.Web` (o `Tcmrj.Siat.Web.csproj` passa a ter `UserSecretsId`) |
| IIS (DSV, HML, PRD) | Variável de ambiente `CatalogoSistemas__GitLab__Token` no `<aspNetCore>` do site, no `applicationHost.config`, mantida pela infraestrutura |

- Token de acesso do projeto `asi/TCMRJDoc`, papel **Reporter**, escopo **`read_repository`**, com data de validade (máximo de 365 dias no padrão do GitLab). Quem cria: mantenedor do TCMRJDoc. A data de validade vai para a agenda de quem opera o SIAT.

## Validação

- `CatalogoSistemasOptions` é validado na leitura, não na subida: `BaseUrl` absoluto `http`/`https`, `Projeto`, `Arquivo` e `Ref` não vazios, `TempoLimiteSegundos` entre 1 e 60, `MinutosDeCache` entre 1 e 60. Configuração inválida ou token ausente resulta em `Erros.Catalogo.NaoConfigurado` na tela de Sistemas; a aplicação e os outros módulos sobem normalmente (Princípio IV).
- O token nunca aparece em mensagem de erro, log nem exceção registrada.
