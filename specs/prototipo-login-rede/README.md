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

## Para testar no DSV (pedidos à infra)

- [ ] Recurso "Autenticação do Windows" do IIS instalado no tcm2016web3 e no tcm2016web4.
- [ ] Seção `system.webServer/security/authentication/windowsAuthentication` desbloqueada **só** para o aplicativo
      do SIAT (`appcmd unlock config "<site>/siat_dsv" -section:... -commit:apphost`). Sem isso: HTTP 500.19.
- [ ] GPO do Edge e do Chrome: `AuthServerAllowlist = tcmrj.tcmnet.tcm,tcmrj2.tcmnet.tcm` (só esses hosts). Sem isso
      o navegador abre uma janela pedindo usuário e senha.
- [ ] App pool com conta de domínio de serviço, com SELECT em `vwSIRHU_Integracao` (e no que ela lê do RH) e em `Users`.
- [ ] Teste através do ARR (`tcmrj2` -> TCM2016ARR2): NTLM costuma falhar através do ARR. Se falhar: SPN
      `HTTP/tcmrj2.tcmnet.tcm` na conta do app pool e `useAppPoolCredentials="true"`, para usar Kerberos.
- [ ] Confirmar que a claim `matricula` aparece no IIS in-process (a transformação depende do esquema padrão
      Negotiate, que repassa para o IIS).
- [ ] PRD (`tcmrj`, provavelmente IP de balanceamento entre web1 e web2): confirmar que o balanceador mantém a conexão
      inteira num servidor (NTLM é por conexão).
