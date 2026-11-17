# Quickstart: validar Consultas (MailQueue e AuditLog)

Guia de execução e validação. Detalhes de rotas, configuração e log estão em [contracts/](./contracts/) e no [data-model.md](./data-model.md).

## Pré-requisitos

- SDK do .NET 10 (último patch) e acesso ao feed TCMRIO INTERNO no NuGet.Config.
- Scripts `dbchange/<versão>/001` e `002` aplicados no `CSLIB_UserManager` de DSV ([contracts/usermanager.md](./contracts/usermanager.md)).
- SIA de desenvolvimento rodando (`https://localhost:44382`, como no SiCATI) ou o de DSV.
- Usuário de teste em cada grupo: `Consultas`, `Consultas` + `Consultas PRD`, e um sem nenhum grupo do SIAT.
- Conta Windows com leitura em `MailQueue`, `AUDITORIADB` e `CSLIB_UserManager` dos servidores do `servidores.json` de desenvolvimento.
- Para os testes de interface: navegadores do Playwright (`pwsh src/Tcmrj.Siat.Test.Interface/bin/Debug/net10.0/playwright.ps1 install chromium`).

## Configurar

```powershell
dotnet user-secrets --project src/Tcmrj.Siat.Web set "TcmrjOidc:ClientSecret" "<segredo do SIAT-dev>"
```

Conferir em `src/Tcmrj.Siat.Web/appsettings.Development.json`: `TcmrjOidc:Authority`, `TcmrjOidc:ClientId = SIAT-dev`, `UserManagerService:ApplicationId`, `TcmrjSessao` (para testar a inatividade, `MinutosSemUsoParaLogout = 2`), `ConnectionStrings:UserManagerConnection`, `ConnectionStrings:AuditoriaDbConnection`, `ConnectionStrings:Consultas`. Conferir `servidores.json` com pelo menos um servidor de DSV e um de HML.

## Rodar

```powershell
dotnet build src/Siat.slnx
dotnet test src/Tcmrj.Siat.Test
dotnet test src/Tcmrj.Siat.Test.Interface
dotnet run --project src/Tcmrj.Siat.Web
```

Resultado esperado: build sem erros nem avisos novos; todos os testes passam; a aplicação abre em `https://localhost:<porta>` e redireciona para o login do SIA. Fazer o login logo nesse momento: um `ClientSecret` errado só aparece aqui, como `invalid_client` do SIA.

## Cenários de validação

| # | História | Passos | Resultado esperado |
|---|---|---|---|
| 1 | Base | Entrar com o usuário sem grupo do SIAT | Seção Consultas ausente; `/` mostra a mensagem de sem acesso; `/consultas/mailqueue` digitado na barra mostra acesso negado |
| 2 | H1 | Entrar com usuário `Consultas`; abrir Consultas › MailQueue | Menu com MailQueue e AuditLog; seletor só com DSV e HML, agrupados por ambiente; nenhum campo de servidor livre |
| 3 | H1 | Escolher servidor de HML e consultar sem filtros | 50 e-mails mais recentes, do mais novo para o mais antigo; faixa de contexto HML |
| 4 | H1 | Escolher SMTP, texto `[teste]_%` e quantidade 10 | Só e-mails daquele SMTP com o texto literal; no máximo 10 |
| 5 | H1 | Abrir uma linha com corpo HTML contendo `<script>` e imagem externa | Corpo exibido sem executar script e sem carregar a imagem externa |
| 6 | H1 | Copiar a URL e abrir em outra janela com outro usuário `Consultas` | Mesma consulta refeita, com a mesma linha aberta |
| 7 | H2 | AuditLog em HML, escolher aplicação | Registros da aplicação, do mais recente para o mais antigo, 50 por padrão |
| 8 | H2 | LogID `4363` e depois `43a` | Primeiro: registros cujo LogID começa com 4363. Segundo: mensagem pedindo só dígitos, sem consulta |
| 9 | H2 | Abrir registro com exceção e LogEvent | Stack trace com quebras e realce; JSON indentado; LogEvent inválido aparece como texto |
| 10 | H2 | Servidor sem acesso ao `CSLIB_UserManager` | Campo numérico de aplicação com aviso de que a lista não pôde ser carregada |
| 11 | H3 | Usuário `Consultas` abre URL com `servidor=<chave de PRD>` | Acesso negado, nenhum dado; registro `AcessoNegado` no log |
| 12 | H3 | Usuário `Consultas PRD` escolhe servidor de PRD | Faixa de contexto sólida de PRD com o aviso de registro em log |
| 13 | H3 | Após as consultas acima, consultar no SIAT › AuditLog o AppId do SIAT | Um registro por consulta e exportação, com usuário, servidor, ambiente e filtros ([contracts/registro-consulta.md](./contracts/registro-consulta.md)) |
| 14 | H4 | Consulta lenta (AuditLog de aplicação sem registros) e Cancelar | Tempo decorrido e Cancelar após 1 s; o comando some de `sys.dm_exec_requests` do servidor em até 2 s; resultado anterior permanece |
| 15 | H4 | Com resultado, teclar `/`, digitar termo sem acento | Foco no filtro; filtra sem diferenciar acentos e maiúsculas; "X de Y" |
| 16 | H4 | Exportar e abrir o arquivo com o SIAT fechado | Nome no padrão; filtro e detalhes funcionam; corpo isolado |
| 17 | Casos extremos | Servidor da lista desligado ou inexistente | Mensagem com servidor e motivo após o tempo limite; a outra tela continua funcionando |
| 18 | Casos extremos | Parar a aplicação durante o uso e reiniciar | Aviso de reconexão; ao voltar, a URL refaz a consulta |
| 19 | Sessão | Clicar em **Sair** no menu | Sessão do SIAT e do SIA encerradas; abrir `/consultas/mailqueue` pede login de novo |
| 20 | Sessão | Abrir `/Account/Logout` direto na barra (GET) | Volta para `/` com a sessão ainda ativa |
| 21 | Sessão | Com `MinutosSemUsoParaLogout = 2`, fazer consultas seguidas na mesma tela por 3 min | Continua logado (consultar conta como interação) |
| 22 | Sessão | Com `MinutosSemUsoParaLogout = 2`, deixar a tela parada 3 min | Sessão encerrada no SIAT e no SIA; ao voltar, login e retorno à URL da consulta |

## Paridade com o Visor (SC-002)

Para cada servidor de DSV e HML do `servidores.json`, rodar `Tcmrj.Visor.exe` e o SIAT com os mesmos filtros (sem filtro; SMTP; texto; aplicação; prefixo de LogID; quantidade 1, 50 e 1000) e comparar a lista de `MessageId` e de `LogID`. Resultado esperado: listas idênticas e na mesma ordem.

## Acessibilidade (WCAG 2.2 AA)

Conferência manual (FR-024), nas telas Home, MailQueue e AuditLog, nos temas claro e escuro:

- Contraste de texto e de componentes conferido nas ferramentas do navegador (Edge ou Chrome, painel de acessibilidade): 4,5:1 para texto e 3:1 para bordas e ícones de controle.
- Todo campo tem rótulo visível ou nome acessível; mensagens de validação e de erro são lidas pelo leitor de tela (Narrador do Windows).
- O foco é sempre visível e segue a ordem da tela; nenhum elemento prende o foco.
- A faixa de contexto de PRD não depende só da cor: tem o texto do ambiente e o aviso.
- A página continua utilizável com zoom de 200% e na largura de 320 px.

## Desempenho (SC-003, SC-004)

- Abrir as telas com o cache do navegador limpo: até 2 s até a tela utilizável.
- Medir as consultas com filtro em servidores disponíveis: até 5 s na maioria; registrar os planos de execução com e sem filtros antes da publicação (RNF-03).
