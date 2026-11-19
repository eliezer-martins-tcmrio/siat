# Protótipo: entrada pelo usuário de rede (opção B, modo misto)

Branch `features/prototipo_login_rede`. **Não é para publicar em PRD**: contraria o Princípio III da constituição
(autenticação só pelo SIA, por OIDC). Adotar exige emenda MAJOR, porque redefine um princípio.

## Como funciona

1. O IIS aceita anônimo e Windows (`src/Tcmrj.Siat.Web/web.config`). A fallback policy exige usuário autenticado;
   sem ele, o handler Negotiate responde 401 e o navegador envia as credenciais do Windows, sem pedir senha.
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
raiz), libera a seção `windowsAuthentication` só para ele e sobe em `https://localhost:44371/siat_dsv/`. Exige o
ASP.NET Core Module V2 do IIS Express (vem com o Visual Studio). Para publicar de novo, pare o IIS Express antes.

Validado em 2026-11-19:

- Com `-SemDesbloquear`, o IIS responde **HTTP 500.19** (0x80070021, seção bloqueada num nível pai) em **todas** as
  rotas, inclusive `/saude`: publicar antes da liberação da infra derruba o aplicativo inteiro.
- Liberada a seção: `/saude` e arquivos estáticos respondem 200 sem credencial; a raiz responde 401 com
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

## Para testar no DSV (pedidos à infra)

- [ ] Recurso "Autenticação do Windows" do IIS instalado no tcm2016web3 e no tcm2016web4.
- [ ] Seção `system.webServer/security/authentication/windowsAuthentication` liberada **só** para o aplicativo
      do SIAT (`appcmd unlock config "<site>/siat_dsv" -section:system.webServer/security/authentication/windowsAuthentication -commit:apphost`),
      **antes** da primeira publicação. Sem isso: HTTP 500.19 em todas as rotas. A seção `anonymousAuthentication`
      não precisa ser liberada: o `web.config` não a declara, e o anônimo já vem ligado no IIS.
- [ ] `appsettings.Development.json` do SIAT no `asi_builds` com `SiatConfiguration:BlazorBaseHref = /siat_dsv` (igual
      ao caminho do aplicativo no IIS).
- [ ] GPO do Edge e do Chrome: `AuthServerAllowlist = tcmrj.tcmnet.tcm,tcmrj2.tcmnet.tcm` (só esses hosts). Sem isso
      o navegador abre uma janela pedindo usuário e senha.
- [ ] App pool com conta de domínio de serviço, com SELECT em `vwSIRHU_Integracao` (e no que ela lê do RH) e em `Users`.
- [ ] Teste através do ARR (`tcmrj2` -> TCM2016ARR2): NTLM costuma falhar através do ARR. Se falhar: SPN
      `HTTP/tcmrj2.tcmnet.tcm` na conta do app pool e `useAppPoolCredentials="true"`, para usar Kerberos.
- [ ] PRD (`tcmrj`, provavelmente IP de balanceamento entre web1 e web2): confirmar que o balanceador mantém a conexão
      inteira num servidor (NTLM é por conexão).
