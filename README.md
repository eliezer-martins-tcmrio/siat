# SIAT

Sistema Integrado de Apoio Técnico do TCMRio. Tem duas seções, ambas somente leitura:

- **Sistemas › Catálogo**: onde cada sistema roda (URL, máquina e caminho por ambiente), lido do `sistemasDb.js` do TCMRJDoc no GitLab.
- **Sistemas › Versões**: a versão publicada de cada sistema em DSV, HML e PRD, nas duas máquinas de cada ambiente, lida dos endereços de `CSLIB_UserManager.dbo.Apps_VersionAddresses` (substitui o `VersaoDeSistemasPublicados.ps1`).
- **Ficha do sistema** (Sistemas › Catálogo › {sistema}): reúne, por ambiente, as instâncias do catálogo e a versão publicada em cada máquina dos componentes vinculados ao item. Abre pelo nome do sistema no Catálogo e em Versões.
- **Consultas**: **MailQueue** e **AuditLog**, sobre uma lista fechada de servidores SQL Server.
- **Gestão › Copilot**: o consumo de AI Credits do Copilot da própria pessoa (período, evolução diária, modelos, limite vigente e uso do limite no mês), lido da API do GitHub para a conta do GitHub que ela conecta ao SIAT.

Especificação, plano e tarefas: `specs/001-consultas/`, `specs/002-catalogo-sistemas/`, `specs/003-versoes-publicadas/`, `specs/004-ficha-sistema/` e `specs/005-copilot-meu-consumo/`. Design: `DESIGN.md`.

## Como rodar

O passo a passo completo, com os cenários de validação, está em [specs/001-consultas/quickstart.md](specs/001-consultas/quickstart.md). Em resumo:

1. Instalar o SDK do .NET (veja [Versão do SDK](#versão-do-sdk)) e ter acesso ao feed `TCMRIO INTERNO` no `NuGet.Config`.
2. Aplicar os scripts de `dbchange/196.1.1` no `CSLIB_UserManager` (veja [Scripts do banco](#scripts-do-banco-dbchange)).
3. Gravar o token do GitLab do catálogo em `CatalogoSistemas:GitLab:Token` no `appsettings.json` (veja [Catálogo de sistemas](#catálogo-de-sistemas-catalogosistemas)). Sem ele o SIAT sobe e só a tela Catálogo mostra o erro.
4. Conferir `TcmrjOidc:ClientId` e `TcmrjOidc:ClientSecret` no `appsettings` (veja [ClientSecret](#clientsecret)).
5. Rodar:

```
dotnet run --project src/Tcmrj.Siat.Web
```

A aplicação abre em `https://localhost:7271` e redireciona para o login do SIA. Em desenvolvimento valem `src/Tcmrj.Siat.Web/appsettings.json` e `appsettings.Development.json`.

## Versão do SDK

O `global.json` fixa o SDK `10.0.100` com `rollForward: latestMajor`: o SDK 10 ou o 11 compilam o projeto, que continua em `net10.0`. Os testes usam o executor Microsoft.Testing.Platform, por isso o comando é `dotnet test --project ...`.

## Configuração

| Seção | Chaves | Observação |
|---|---|---|
| `TcmrjOidc` | `Authority`, `ClientId`, `RequireHttpsMetadata`, `ClientSecret` | Login no SIA (OpenIddict). `ClientId` é `SIAT-dev` em desenvolvimento e `SIAT` em PRD. O `ClientSecret` fica no `appsettings`, junto do `ClientId` (veja abaixo). |
| `TcmrjSessao` | `MinutosSemUsoParaLogout` (30), `MinutosParaRevalidacao` (1) | Logout por inatividade. Consultar, filtrar, abrir um item, exportar e navegar contam como uso. Para testar na mão, use `MinutosSemUsoParaLogout = 2`. |
| `UserManagerService` | `ApplicationId` | Id do SIAT na tabela `Apps` do `CSLIB_UserManager`: o `@AppId` do `dbchange/196.1.1/001...sql`. Sem ele a aplicação não sobe. |
| `ConnectionStrings` | `UserManagerConnection`, `AuditoriaDbConnection`, `Consultas` | As duas primeiras apontam para o `CSLIB_UserManager` (permissões e nomes de aplicação) e para o `AUDITORIADB` (log do próprio SIAT). `Consultas` é o modelo da conexão com os servidores da lista (o servidor e o banco vêm do `servidores.json`; autenticação integrada, conta de serviço somente leitura). |
| `ConsultasOptions` | `CommandTimeout` (60 s), `ConnectTimeout` (15 s) | Tempos limite das consultas. |
| `Servidores` | veja [Lista de servidores](#lista-de-servidores-servidoresjson) | Vem do `servidores.json`. |
| `Versoes` | `ServidorPadrao` (`tcmbd-prd`), `TempoLimiteSegundos` (5), `LeiturasSimultaneas` (32), `MinutosDeCache` (5) | Versões publicadas (veja [Versões publicadas](#versões-publicadas-versoes)). |
| `CatalogoSistemas` | `GitLab:BaseUrl`, `GitLab:Projeto`, `GitLab:Arquivo`, `GitLab:Ref`, `GitLab:Token`, `TempoLimiteSegundos` (10), `MinutosDeCache` (10) | Origem do catálogo de sistemas (veja [Catálogo de sistemas](#catálogo-de-sistemas-catalogosistemas)). O token fica no `appsettings`, como o `ClientSecret`. |
| `Copilot` | `Organizacao`, `GitHub:Token`, `GitHub:TempoLimiteSegundos` (15), `OAuth:ClientId`, `OAuth:ClientSecret`, `LeiturasSimultaneas` (4), `Cache:*` | Consumo do Copilot (veja [Copilot](#copilot-copilot)). O token de leitura e o segredo da OAuth App ficam no `appsettings`, como o `ClientSecret`. |

### ClientSecret

O `ClientSecret` vem do `appsettings`, como o `ClientId`: `TcmrjOidc:ClientSecret` no `appsettings.json` (e, se o cliente de desenvolvimento usar outro segredo, no `appsettings.Development.json`, que já sobrescreve o `ClientId` com `SIAT-dev`). O segredo precisa corresponder ao hash cadastrado para o cliente no script `002`. Um segredo errado só aparece no primeiro login, como `invalid_client` do SIA.

### Catálogo de sistemas (`CatalogoSistemas`)

A tela **Sistemas › Catálogo** lê o `sistemasDb.js` do projeto `asi/TCMRJDoc` (o mesmo arquivo usado pelo `sistemas.html`) pela API de arquivos do GitLab, com um token de acesso do projeto TCMRJDoc (papel Reporter, escopo `read_repository`), criado pelo mantenedor do TCMRJDoc ([contracts/configuracao.md](specs/002-catalogo-sistemas/contracts/configuracao.md)). A leitura fica em cache por `MinutosDeCache` minutos; "Atualizar agora" relê na hora. Para corrigir um dado, edite o `sistemasDb.js` no GitLab (o rodapé da tela tem o link).

O token vai em `CatalogoSistemas:GitLab:Token`, no `appsettings.json`, junto do resto da seção, como o `ClientSecret` (constituição, Princípio III). Isso vale em desenvolvimento e no `appsettings.json` publicado no IIS. O SIAT não usa `dotnet user-secrets`. O token nunca aparece em mensagem de erro nem em log.

Sem token, ou com a configuração inválida, o SIAT sobe normalmente e só a tela Catálogo mostra "A origem do catálogo não está configurada". O token do GitLab expira em até 365 dias: **renove-o uma vez por ano**, antes do vencimento, e troque o valor no `appsettings.json` de cada ambiente. Token vencido aparece na tela como "O GitLab recusou o acesso ao catálogo (token inválido ou expirado)".

### Versões publicadas (`Versoes`)

A tela **Sistemas › Versões** lê os endereços ativos de `CSLIB_UserManager.dbo.Apps_VersionAddresses` no servidor escolhido (lista fechada do `servidores.json`; sem escolha, o `ServidorPadrao`) e, para cada endereço, o arquivo de versão (`VERSAO.TXT`, `.INI` em compartilhamento de rede) ou a URL `https`, com até `LeiturasSimultaneas` leituras em paralelo e `TempoLimiteSegundos` por arquivo. O resultado fica em cache por `MinutosDeCache` minutos por servidor; "Atualizar agora" relê na hora ([contracts/configuracao.md](specs/003-versoes-publicadas/contracts/configuracao.md)).

A tela abre com o banco de produção (`ServidorPadrao = tcmbd-prd`, também no `appsettings.json` do repositório); DSV e HML ficam no seletor. Uma chave que não está na lista não impede a subida: a tela pede para escolher o servidor. Não há segredo novo: SQL e compartilhamentos usam a conta do pool do IIS.

### Ficha do sistema e o campo `Versoes` do `sistemasDb.js`

A ficha (`/sistemas/catalogo/ficha?sistema={nome}`, com `&servidor={chave}` opcional) exige `Catalogo_Sistemas.CanSelect`; a parte de versões aparece só para quem tem também `Versoes_Sistemas.CanSelect`. Não há configuração nova: a ficha usa as seções `CatalogoSistemas` e `Versoes` e os mesmos caches das duas telas.

O vínculo entre um item do catálogo e as linhas de `Apps_VersionAddresses` é explícito: o campo opcional `Versoes` do item, no `sistemasDb.js`, lista os nomes de `Apps_VersionAddresses.Sistema` daquele sistema e dos seus componentes, por exemplo `"Versoes": ["SIGARH", "SIGARH Agendador"]`. A comparação ignora acentos, maiúsculas e espaços nas pontas. Sem o campo, a ficha mostra só o catálogo e explica como vincular; um campo que não é lista de nomes vira aviso na ficha; um nome que não está entre os registros ativos do servidor escolhido também. Em Versões, cada nome vinculado vira link para a ficha e o filtro **Sem ficha** mostra os que nenhum item cita ([contracts/campo-versoes.md](specs/004-ficha-sistema/contracts/campo-versoes.md)).

### Copilot (`Copilot`)

A tela **Gestão › Copilot** (`/copilot`, que leva a `/copilot/pessoas/{login}`) exige `CopilotProprio.CanSelect` e mostra só a conta do GitHub conectada pela própria pessoa ([contracts/configuracao.md](specs/005-copilot-meu-consumo/contracts/configuracao.md)). São duas credenciais, ambas no `appsettings.json` de cada ambiente, sem `dotnet user-secrets`:

- **Token de leitura** (`Copilot:GitHub:Token`): token fine-grained criado por um owner da organização, com **Administration: Read-only** e **Members: Read-only** e acesso a repositórios públicos ([contracts/github-api.md](specs/005-copilot-meu-consumo/contracts/github-api.md)). Lê licenças, budgets, times, associação e os relatórios de consumo; o SIAT só faz `GET` no GitHub. O token tem validade: **anote o vencimento e renove-o antes**, trocando o valor no `appsettings.json` de cada ambiente. Token vencido ou revogado aparece na tela como "O token de billing do GitHub não foi aceito".
- **OAuth App** (`Copilot:OAuth:ClientId` e `ClientSecret`): uma por ambiente, sem Device Flow, com Homepage e Authorization callback URL iguais à raiz do site (`https://localhost:7271/` em desenvolvimento; `https://<host do SIAT>/` no IIS). O retorno é `copilot/github/retorno` sob o `<base href>` ([contracts/conexao-github.md](specs/005-copilot-meu-consumo/contracts/conexao-github.md)). A conexão pede só a identificação da conta (escopo vazio): o token do usuário é usado uma vez para ler `GET /user` e descartado. A ligação fica num cookie próprio (`.Siat.GitHub`) por 30 dias, sem renovação; "Desconectar do GitHub" apaga o cookie. Conexão, recusa e desconexão vão para o `AUDITORIADB.audit.Log` com `TypeID` 10.

Os dados ficam em memória: licenças e times por `Cache:MinutosLicencasETimes` (5), budgets por `SegundosBudgets` (30), perfil por `HorasPerfil` (6), relatórios dos três últimos dias e do mês atual por `MinutosRelatoriosRecentes` (10) e os demais dias por `HorasRelatoriosFechados` (24); a associação à organização é relida a cada carga. "Atualizar agora" relê tudo, menos os dias fechados.

Seção incompleta: o SIAT sobe normalmente, os esquemas OAuth não são registrados e só a tela do Copilot mostra "O módulo Copilot não está configurado no servidor.". O token e o segredo nunca aparecem em mensagem de erro nem em log.

### Testar uma edição do `sistemasDb.js` sem subir para o GitLab

O SIAT só lê o catálogo pela API de arquivos do GitLab, mas aceita qualquer `BaseUrl`. Para ver na tela uma edição feita num clone local do TCMRJDoc (um vínculo novo no campo `Versoes`, por exemplo) antes do commit, aponte o SIAT para um servidor local que devolve esse arquivo em qualquer caminho:

1. Suba o servidor e deixe o terminal aberto. Ele relê o arquivo a cada pedido, então dá para continuar editando sem reiniciá-lo (troque o caminho pelo do seu clone):

   ```powershell
   python -c "import http.server as h
   class H(h.BaseHTTPRequestHandler):
       def do_GET(s):
           d = open(r'D:\Projetos\TCMRJDoc\sistemasDb.js', 'rb').read()
           s.send_response(200); s.send_header('Content-Type', 'text/plain; charset=utf-8'); s.end_headers(); s.wfile.write(d)
   h.HTTPServer(('127.0.0.1', 8085), H).serve_forever()"
   ```

2. No `appsettings.json` local, troque `CatalogoSistemas:GitLab:BaseUrl` por `http://127.0.0.1:8085` e `Versoes:ServidorPadrao` por `tcmbd3-dev`, para não consultar produção da máquina de desenvolvimento. Mantenha o `Token` preenchido: a validação exige um token, e ele só vai para o servidor local.
3. Rode o SIAT (`dotnet run --project src/Tcmrj.Siat.Web`) e abra a ficha, por exemplo `/sistemas/catalogo/ficha?sistema=SGQ`, ou a tela de Versões com o filtro **Sem ficha**. O catálogo fica em cache por `MinutosDeCache` minutos: depois de editar o arquivo, use "Atualizar agora".

O link do `sistemasDb.js` no rodapé aponta para o servidor local, e não convém escolher `TCMBD` no seletor de servidor. Ao terminar, volte os dois valores (`https://git.tcmnet.tcm` e `tcmbd-prd`) e não faça commit dessas trocas.

## Publicação no IIS

- Instalar o Hosting Bundle do .NET 10 no servidor.
- Ligar o recurso **WebSockets** do IIS: o Blazor Server depende de uma conexão contínua (SignalR). Sem ele a tela cai para um transporte mais lento ou não conecta.
- Conferir `TcmrjOidc:Authority`, `ClientId = SIAT`, `ClientSecret`, `UserManagerService:ApplicationId` e as connection strings do ambiente.
- Conferir o token do catálogo (`CatalogoSistemas:GitLab:Token`) no `appsettings.json` publicado e que o servidor alcança `https://git.tcmnet.tcm`.
- Versões publicadas: `ServidorPadrao = tcmbd-prd` na seção `Versoes`; `SELECT` em `dbo.Apps_VersionAddresses` para a conta do pool no `CSLIB_UserManager` dos três servidores; leitura nos compartilhamentos cadastrados na tabela (`\\tcm2016web1..4\TcmRoot`, `\\tcm2016webc\TcmRoot`, `\\unity\Corp2\Desenvolvimento`, `\\unity\producao\prod`) e HTTPS de saída para os endereços `https` (veja [contracts/configuracao.md](specs/003-versoes-publicadas/contracts/configuracao.md), Operação). A falta de permissão aparece só na célula ("Acesso negado à conta do SIAT").
- **Chaves de proteção de dados** (cookie de sessão e antiforgery): o pool de aplicativos precisa de **perfil de usuário carregado** (`Load User Profile = True`), para o ASP.NET Core guardar as chaves no perfil da conta do pool. Sem isso as chaves ficam só na memória e o login e os formulários quebram a cada reinício. A alternativa é chamar `AddDataProtection().PersistKeysToFileSystem(...)` apontando para uma pasta que só a conta do pool lê e grava (o código do SIAT ainda não faz essa chamada).
- **URLs de retorno do login**: o cliente OIDC precisa ter `https://<nome-do-siat>/signin-oidc` e `https://<nome-do-siat>/signout-callback-oidc` cadastrados (script `002` do `dbchange`). Se o SIAT for publicado num subdiretório do IIS, o subdiretório entra antes: `https://<nome>/<subdiretório>/signin-oidc`.
- Conferir que a conta do pool tem leitura em `CSLIB_UserManager` e `AUDITORIADB` e nos servidores do `servidores.json`.
- Copilot: seção `Copilot` completa no `appsettings.json` publicado (token de leitura e a OAuth App do ambiente, com o callback na raiz do site) e HTTPS de saída para `https://github.com` e `https://api.github.com`. O cookie da conexão é protegido pelas mesmas chaves de proteção de dados do item acima: sem elas persistidas, cada reciclagem do pool desfaz as conexões e as pessoas precisam conectar de novo.

## Scripts do banco (`dbchange`)

Em `dbchange/196.1.1`, para o `CSLIB_UserManager` (veja [contracts/usermanager.md](specs/001-consultas/contracts/usermanager.md)):

| Script | O que faz |
|---|---|
| `001 ... Criação de app, recursos e grupos do SIAT.sql` | Cadastra a aplicação SIAT, os recursos `MailQueue` (`Id` 10), `AuditLog` (20), `Catalogo_Sistemas` (30) e `Versoes_Sistemas` (40) e os grupos `Consultas` (MailQueue e AuditLog) e `Sistemas` (catálogo e versões), com `CanSelect`. Idempotente. |
| `002 ... OpenIddictApplications do SIAT.sql` | Cadastra os clientes OIDC `SIAT-dev` e `SIAT`. Idempotente. |
| `002 ... Recurso CopilotProprio e grupo Copilot do SIAT.sql` | Cadastra o recurso `CopilotProprio` (`Id` 50) e o grupo `Copilot`, com `CanSelect`. Não inclui usuários. Idempotente. |

Antes de executar:

- **`@AppId` do `001`**: o SIAT usa o `Id` `93` em `Apps`, o mesmo em DSV e em PRD. O script recusa a execução se esse `Id` já pertencer a outra aplicação. O mesmo valor está em `UserManagerService:ApplicationId`.
- **Hashes do `002`**: `@HashSecretDev` e `@HashSecretPrd` são placeholders (`<HASH_DO_CLIENTSECRET_...>`). O script se recusa a inserir o cliente enquanto não forem trocados pelo hash do `ClientSecret` (o cabeçalho do script explica como gerar o hash). O segredo em claro nunca entra no repositório.
- **Nome DNS de PRD**: o `@UrlBasePrd` do `002` é um placeholder (`https://siat.tcmrj.tcmnet.tcm`). Confirmar o nome e o subdiretório, se houver.

## Lista de servidores (`servidores.json`)

Os servidores consultáveis vêm de uma lista fechada, no arquivo `servidores.json` ao lado do executável (`src/Tcmrj.Siat.Web/servidores.json` no código-fonte). O arquivo é opcional e recarregado ao ser alterado. Não existe campo de servidor livre na tela: só aparecem as chaves desta lista, filtradas pelas permissões do usuário.

| Campo | Significado |
|---|---|
| `Chave` | Identificador estável usado na URL (`?servidor=<Chave>`) e no registro de consulta. Único. |
| `Nome` | Nome mostrado ao usuário. |
| `Endereco` | Instância SQL Server (`servidor\instância` ou `servidor,porta`). |
| `Ambiente` | `DSV`, `HML` ou `PRD`. Obrigatório: sem ele a lista é recusada. |
| `BancoMailQueue`, `BancoAuditoria` | Opcionais; trocam o nome padrão do banco no servidor. |

O `servidores.json` do repositório tem os três ambientes:

```json
{
  "Servidores": [
    { "Chave": "tcmbd3-dev", "Nome": "TCMBD3\\DEV", "Endereco": "TCMBD3\\DEV", "Ambiente": "DSV" },
    { "Chave": "tcmbd4-hml", "Nome": "TCMBD4\\HML", "Endereco": "TCMBD4\\HML", "Ambiente": "HML" },
    { "Chave": "tcmbd-prd", "Nome": "TCMBD", "Endereco": "TCMBD", "Ambiente": "PRD" }
  ]
}
```

Quem tem o recurso do módulo vê e consulta os três, inclusive quando roda o SIAT localmente.

### Permissões

Um recurso por módulo (permissão `CanSelect`): `Catalogo_Sistemas` e `Versoes_Sistemas` (grupo `Sistemas`), `MailQueue` e `AuditLog` (grupo `Consultas`) e `CopilotProprio` (grupo `Copilot`). Nas consultas, o recurso libera os três ambientes (DSV, HML e PRD). No Copilot, o recurso libera só o consumo da conta do GitHub que a própria pessoa conectou.

Quem não tem o recurso do módulo não vê o item no menu (nem servidores no seletor, nas consultas), e a URL da consulta mostra "Acesso negado" sem consultar o banco. Em PRD a faixa de contexto fica destacada e avisa que a consulta é registrada em log. Toda tentativa de consulta, inclusive a negada, é registrada em `AUDITORIADB.audit.Log` (`TypeID` 10). A leitura das versões não vai para esse log: versões e caminhos de publicação não são dados sensíveis.

## Compilar e testar

```
dotnet build src/Siat.slnx
dotnet test --project src/Tcmrj.Siat.Test/Tcmrj.Siat.Test.csproj
```

O build deve terminar sem avisos. Os testes (unidade, cenários Reqnroll e componentes bUnit) não tocam o SIA nem nenhum SQL Server.

Os fluxos das telas, o login real no SIA, o contraste e o foco visível nos dois temas e o desempenho são conferidos pelo roteiro manual do [quickstart](specs/001-consultas/quickstart.md).

## Pendências

- **Nível do registro das consultas no log (T111).** O DBLog (`Tcmrj.CoreLib.DBLog`) só grava no `AUDITORIADB.audit.Log` eventos de nível Error com `LogUID`. Por isso o `RegistroConsultaService` registra cada consulta com `LogErrorWithLogUID(10, ...)`, mesmo sendo um evento comum (o `TypeID` 10 identifica o registro de consulta). Há um pedido pendente à equipe da CoreLib para o DBLog aceitar o nível Information com `LogUID`. Quando sair, a linha a trocar é a chamada `LogErrorWithLogUID` em `src/Tcmrj.Siat.App/Web/Services/RegistroConsultaService.cs` (veja [research.md R7](specs/001-consultas/research.md)).
- **Nome DNS de PRD**: o `@UrlBasePrd` do script `002` é um placeholder (questão em aberto 5 do PRD).
- **Hashes do `ClientSecret`** de `SIAT-dev` e `SIAT` no script `002`.
- **Catálogo de sistemas (T052, T053)**: roteiro do [quickstart da 002](specs/002-catalogo-sistemas/quickstart.md) com o token real e o script `003` aplicado, e a comparação visual com `design/sistemas.html`.
- **Versões publicadas (T059, T060)**: roteiro do [quickstart da 003](specs/003-versoes-publicadas/quickstart.md) com o script `VersaoDeSistemasPublicados.ps1` lado a lado (SC-003) e as conferências de operação antes de publicar.
- **Validação manual (T110)**: cenários 1 a 22 do quickstart, desempenho (SC-003 e SC-004, com os planos de execução com e sem filtros) e o roteiro de acessibilidade WCAG 2.2 AA.
