<#
.SYNOPSIS
    PROTÓTIPO (features/prototipo_login_rede): roda o SIAT no IIS Express a partir da publicação, para testar a parte da
    autenticação Windows que o Kestrel não cobre: o repasse do Negotiate para o IIS, a transformação de claims no
    hosting in-process e a seção windowsAuthentication do web.config, que vem bloqueada como no servidor.

.DESCRIPTION
    1. Publica o projeto Web, como o build do asi_builds, com o ambiente no web.config (no deploy quem faz isso é o XDT).
    2. Recria .iisexpress\applicationhost.config a partir do modelo do IIS Express, com o ASP.NET Core Module V2.
    3. Cria o site SIAT em https://localhost:<Porta> e libera a seção windowsAuthentication só para ele, com o mesmo
       comando que a infra roda no servidor (appcmd unlock config). Com -SemDesbloquear, não libera: o IIS responde
       HTTP 500.19, que é o erro do servidor antes do pedido à infra.
    4. Inicia o IIS Express em primeiro plano (Q para parar). Para publicar de novo, pare o IIS Express antes.

.EXAMPLE
    .\tools\iniciar-iisexpress.ps1
.EXAMPLE
    .\tools\iniciar-iisexpress.ps1 -SemDesbloquear
#>
param(
    [switch] $SemDesbloquear,

    # O certificado de desenvolvimento do IIS Express já fica associado às portas 44300 a 44399.
    [ValidateRange(44300, 44399)]
    [int] $Porta = 44371
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path $PSScriptRoot -Parent
$pasta = Join-Path $raiz '.iisexpress'
$publicacao = Join-Path $pasta 'site'
$config = Join-Path $pasta 'applicationhost.config'
$iisExpress = Join-Path $env:ProgramFiles 'IIS Express'
$appcmd = Join-Path $iisExpress 'appcmd.exe'

if (-not (Test-Path (Join-Path $iisExpress 'Asp.Net Core Module\V2\aspnetcorev2.dll'))) {
    throw 'O ASP.NET Core Module V2 do IIS Express não está instalado (vem com o Visual Studio ou com o Hosting Bundle).'
}

Write-Host '1/4 Publicando o Tcmrj.Siat.Web...'
dotnet publish (Join-Path $raiz 'src\Tcmrj.Siat.Web') -c Debug -o $publicacao -p:EnvironmentName=Development --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou. Se o IIS Express estiver rodando, pare-o antes (ele prende as DLLs).' }

Write-Host '2/4 Recriando o applicationhost.config...'
New-Item -ItemType Directory -Force $pasta | Out-Null
Copy-Item (Join-Path $iisExpress 'config\templates\PersonalWebServer\applicationhost.config') $config -Force

# O modelo não traz o ASP.NET Core Module: declara a seção aspNetCore e registra o módulo, como o Visual Studio faz.
[xml] $xml = Get-Content $config -Raw
$grupo = $xml.SelectSingleNode("/configuration/configSections/sectionGroup[@name='system.webServer']")
$secao = $xml.CreateElement('section')
$secao.SetAttribute('name', 'aspNetCore')
$secao.SetAttribute('overrideModeDefault', 'Allow')
[void] $grupo.AppendChild($secao)

$globais = $xml.SelectSingleNode('/configuration/system.webServer/globalModules')
$modulo = $xml.CreateElement('add')
$modulo.SetAttribute('name', 'AspNetCoreModuleV2')
$modulo.SetAttribute('image', '%IIS_BIN%\Asp.Net Core Module\V2\aspnetcorev2.dll')
[void] $globais.AppendChild($modulo)

$modulos = $xml.SelectSingleNode("/configuration/location[@path='']/system.webServer/modules")
$ativo = $xml.CreateElement('add')
$ativo.SetAttribute('name', 'AspNetCoreModuleV2')
$ativo.SetAttribute('lockItem', 'true')
[void] $modulos.AppendChild($ativo)
$xml.Save($config)

Write-Host "3/4 Criando o site SIAT em https://localhost:$Porta ..."
& $appcmd delete site 'WebSite1' /apphostconfig:$config | Out-Null
& $appcmd add site /name:SIAT /bindings:"https/*:${Porta}:localhost" /physicalPath:$publicacao /apphostconfig:$config | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'appcmd add site falhou.' }

if ($SemDesbloquear) {
    Write-Warning 'Seção windowsAuthentication bloqueada (-SemDesbloquear): espere HTTP 500.19.'
}
else {
    # O mesmo pedido que vai para a infra: liberar a seção só para o aplicativo do SIAT, nunca para o servidor todo.
    & $appcmd unlock config 'SIAT/' -section:system.webServer/security/authentication/windowsAuthentication /commit:apphost /apphostconfig:$config
    if ($LASTEXITCODE -ne 0) { throw 'appcmd unlock config falhou.' }
}

Write-Host "4/4 Iniciando o IIS Express em https://localhost:$Porta (Q para parar)..."
& (Join-Path $iisExpress 'iisexpress.exe') /config:$config /site:SIAT
