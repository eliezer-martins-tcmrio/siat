# Protótipo: entrada pelo usuário de rede (opção B, modo misto)

Branch `features/prototipo_login_rede`. **Não é para publicar em PRD**: contraria o Princípio III da constituição
(autenticação só pelo SIA, por OIDC). Adotar exige emenda MAJOR, porque redefine um princípio.

## Como funciona

1. O IIS aceita anônimo e Windows. A autenticação do Windows é ligada pela infra no `applicationHost.config`, só para o
   aplicativo, como no SuporteASI; o projeto não tem `web.config` próprio (a publicação gera o padrão). A fallback
   policy exige usuário autenticado; sem ele, o handler Negotiate responde 401 e o navegador envia as credenciais do
   Windows. Sem a autenticação do Windows ligada no IIS, o SIAT não sobe (ver IIS Express abaixo).
2. `ClaimsDoUsuarioDeRede` (IClaimsTransformation) recebe `TCMNET\login`, consulta
   `CSLIB_UserManager.dbo.vwSIRHU_Integracao` (`LoginRede` -> `Matricula`, linha ativa) e `dbo.Users`
   (usuário habilitado com `Id` = matrícula) e acrescenta as claims `matricula`, `userId`, `nome` e `email`.
3. A partir daí é o que já existia: o `Tcmrj.Autorizacao.App` só lê a claim `matricula` e consulta as permissões do
   UserManager (AppId 93) a cada verificação.
4. Sem vínculo único com usuário habilitado, o usuário fica sem matrícula (todas as políticas negam) e a tela
   explica o motivo: domínio errado, sem matrícula no RH, mais de uma matrícula, usuário desabilitado ou cadastro
   indisponível. Só o vínculo encontrado fica em cache (5 minutos); o resto é consultado de novo ao recarregar.

Anônimos de propósito: arquivos estáticos e `/saude`, para o balanceador e o monitoramento.

O que saiu: o OIDC no SIA (`TcmrjOidc`, `Account/Login`, `Account/Logout`) e o botão "Sair". A inatividade
(`TcmrjSessao`) continua: o circuito fica anônimo e a tela oferece "Continuar", que recarrega a página e entra de novo.

## Testar na máquina (Kestrel)

`dotnet run --project src/Tcmrj.Siat.Web --launch-profile https` e abrir `https://localhost:7271`. Em `localhost` o
navegador envia as credenciais sem perguntar. Validado em 2026-11-19: entra sem senha, mostra nome e matrícula e
libera os módulos conforme o UserManager do DEV.

## Testar na máquina (IIS Express)

`.\tools\iniciar-iisexpress.ps1` (ou a configuração `siat-iisexpress` do `.claude/launch.json`) publica o projeto,
monta um `.iisexpress\applicationhost.config` a partir do modelo do IIS Express (seções de autenticação bloqueadas,
como no servidor), põe o SIAT como aplicativo em `/siat_dsv` (diretório virtual, como no servidor; `-Caminho /` para a
raiz), liga a autenticação do Windows só para ele com o mesmo comando da infra (`appcmd set config ...
/enabled:true /commit:apphost`) e sobe em `https://localhost:44371/siat_dsv/`. Exige o ASP.NET Core Module V2 do IIS
Express (vem com o Visual Studio). Para publicar de novo, pare o IIS Express antes.

Validado em 2026-11-19:

- Com `-SemAutenticacaoWindows` (o servidor antes do pedido à infra), o SIAT **não sobe**: HTTP 500 em **todas** as
  rotas, inclusive `/saude`. O log e o Visualizador de Eventos dizem por quê: "The Negotiate Authentication handler
  cannot be used on a server that directly supports Windows Authentication. Enable Windows Authentication for the
  server...". Publicar antes do pedido à infra derruba o aplicativo inteiro, como na liberação da seção (a versão
  anterior do protótipo declarava a seção no `web.config`; sem a liberação, HTTP 500.19 em todas as rotas).
- Com a autenticação ligada: `/saude` e arquivos estáticos respondem 200 sem credencial; a raiz responde 401 com
  `WWW-Authenticate: Negotiate, NTLM` (o desafio é do IIS); o log mostra "Deferring to the server's implementation of
  Windows Authentication"; no navegador, entra sem senha, a claim `matricula` chega ao circuito do Blazor e o log
  registra "Usuário de rede TCMNET\... vinculado à matrícula ...".
- Em `/siat_dsv`: `<base href="/siat_dsv/">`; CSS, `_framework`, `_blazor`, menu, marca, redirecionamento da página
  inicial e URL dos filtros ficam todos dentro de `/siat_dsv/`; link direto (`/siat_dsv/consultas/auditlog`) abre.

## Caminho do aplicativo (`SiatConfiguration:BlazorBaseHref`)

Mesmo padrão do SiCATI: vazio na raiz (`dotnet run`), e no `appsettings` de cada ambiente do `asi_builds` o caminho
do aplicativo no IIS, sem barra no fim: `/siat_dsv`, `/siat_hml`, `/siat`. Tem de ser igual ao caminho no IIS. Valor
malformado (`/siat/`, `siat`, `/`) impede a aplicação de subir. As rotas do `Navegacao` são relativas ao base href
(`consultas/mailqueue`, sem a barra inicial).

## O que os servidores mostraram (2026-11-19)

Diagnóstico como administrador no tcm2016web3 e no tcm2016web4, mais requisições de uma estação:

- Prontos nos dois: recursos Autenticação do Windows e WebSocket, ASP.NET Core Module V2 20.0 e Hosting Bundle 10.0.15.
- Nenhum caminho tem a seção `windowsAuthentication` liberada. Os dois aplicativos que já usam autenticação do Windows
  (`SuporteASI_dsv`, `SuporteDSG_dsv`) a ligam no `applicationHost.config` (`location` com `enabled="true"`), não no
  `web.config`, com o anônimo desligado. Vale só para eles: SiCATI e SCAF, no mesmo site, seguem sem. O protótipo
  adotou esse padrão.
- O ARR não repassa a autenticação do Windows: responde 302 para o servidor. `tcmrj2/SuporteASI_dsv` vai para
  `http://tcm2016web3/SuporteASI_dsv/`; `tcmrj/SuporteASI` (PRD) para `http://tcm2016web1/SuporteASI/`. O SiCATI, que
  é Blazor e não usa autenticação do Windows, também: `tcmrj2/sicati_dsv` vai para
  `https://tcm2016web3.tcmnet.tcm/sicati_dsv/`. Só o SCAF, dos verificados, passa pelo ARR.
- Direto no servidor, o Kerberos funciona sem SPN novo (ticket `HTTP/tcm2016web3`, coberto pelo `HOST/` do computador):
  a preocupação com NTLM através do ARR e com SPN em `tcmrj2` deixa de existir se o SIAT seguir o mesmo caminho.
- O SiCATI roda com `TCMNET\svc-intranet`. No DEV ela tem `db_datareader` no `CSLIB_UserManager` e no `SIRHU`; a
  `vwSIRHU_Integracao` lê 8 tabelas do `SIRHU`, sem encadeamento de propriedade entre bancos, então a conta precisa dos
  dois. A `svc-etcm` (SCAF) tem no `SIRHU` só o papel `ComDireitoDeLeitura_ParaViews`, não verificado.

## Para testar no DSV (pedidos à infra)

- [x] Recurso "Autenticação do Windows" e Hosting Bundle do .NET 10 no tcm2016web3 e no tcm2016web4.
- [ ] Aplicativo `/siat_dsv` no `Default Web Site`, app pool próprio com `TCMNET\svc-intranet` (como o SiCATI).
- [ ] Autenticação do Windows ligada só para ele, **antes** da primeira publicação, como no SuporteASI, mas **sem
      desligar o anônimo** (`/saude` e arquivos estáticos):
      `appcmd set config "Default Web Site/siat_dsv" -section:system.webServer/security/authentication/windowsAuthentication /enabled:true /commit:apphost`.
      Sem isso o SIAT não sobe (HTTP 500 em todas as rotas, com a mensagem acima no Visualizador de Eventos).
- [ ] Regra no ARR2: `/siat_dsv` com 302 para `https://tcm2016web3.tcmnet.tcm/siat_dsv/`, como a do SiCATI (o Blazor
      Server precisa ficar num servidor só; e direto no servidor a autenticação do Windows funciona).
- [ ] `appsettings.Development.json` do SIAT no `asi_builds` com `SiatConfiguration:BlazorBaseHref = /siat_dsv`.
- [ ] Login sem senha: o Edge só envia a credencial sem perguntar para a zona Intranet local do Windows. Numa estação,
      `http://tcm2016web3` (sem ponto) é intranet e entra sem senha (validado com o Edge); `https://tcm2016web3.tcmnet.tcm`
      é zona Internet e pede usuário e senha. Para usar https sem senha: incluir o host na GPO de zonas que já existe
      (Site to Zone Assignment List, zona 1, onde estão os servidores de arquivos) ou, por usuário, em Opções da
      Internet > Intranet local > Sites.
- [ ] PRD: o mesmo, com o redirecionamento do ARR1 (`tcmrj`) para o tcm2016web1.

## Diagnóstico do servidor (`tools/diagnostico-servidor-siat.ps1`)

Só leitura: lê a configuração do IIS, o AD (`setspn -Q`/`-L`) e o SQL, sem alterar nada e sem ler senha de app pool.
Rodar no próprio tcm2016web3 e tcm2016web4 (área de trabalho remota; por sessão remota o setspn e o SQL esbarram no
double hop), num PowerShell de 64 bits como administrador:

```powershell
powershell -ExecutionPolicy Bypass -File .\diagnostico-servidor-siat.ps1
```

Sem elevação ou em 32 bits ele não roda (o IIS devolveria listas vazias sem erro). A conta administrativa do servidor
não tem login no SQL do DEV: use `-SemSql` e confira as contas dos pools de uma estação.

Grava `siat-diagnostico-<SERVIDOR>-<data>.txt` com o resumo de `[FALTA]`, `[ATENCAO]` e `[ERRO]` no fim. Cobre os
itens acima que dá para ver do servidor: recurso Autenticação do Windows, Hosting Bundle do .NET 10, autenticação do
Windows ligada para `/siat_dsv` e `/siat_hml` (com o comando a pedir, se não estiver), identidade dos app pools, SPNs,
quem chega pelo log do IIS (o ARR?) e o acesso das contas dos pools ao `CSLIB_UserManager`. Não cobre a GPO das
estações nem a configuração do ARR. Validado em 2026-11-19 nesta máquina (sem IIS), com as seções do IIS rodando contra
o `applicationhost.config` do IIS Express, com e sem a autenticação ligada, e nos dois servidores.
