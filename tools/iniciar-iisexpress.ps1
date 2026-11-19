<#
.SYNOPSIS
    PROTÓTIPO (features/prototipo_login_rede): roda o SIAT no IIS Express a partir da publicação, para testar a parte da
    autenticação Windows que o Kestrel não cobre: o repasse do Negotiate para o IIS, a transformação de claims no
    hosting in-process e a autenticação do Windows ligada no applicationHost.config, como a infra faz no servidor.

.DESCRIPTION
    1. Publica o projeto Web, como o build do asi_builds, com o ambiente no web.config (no deploy quem faz isso é o XDT),
       e grava um appsettings.Development.json com o SiatConfiguration:BlazorBaseHref do caminho (no deploy, é o
       appsettings do ambiente que vem do asi_builds).
    2. Recria .iisexpress\applicationhost.config a partir do modelo do IIS Express, com o ASP.NET Core Module V2.
    3. Cria o site SIAT em https://localhost:<Porta>, com o SIAT como aplicativo em <Caminho> (diretório virtual,
       como no servidor), e liga a autenticação do Windows só para ele, com o mesmo comando que a infra roda no
       servidor (appcmd set config ... /commit:apphost, como no SuporteASI). O anônimo continua ligado (modo misto).
       Com -SemAutenticacaoWindows, não liga: é o servidor antes do pedido à infra.
    4. Inicia o IIS Express em primeiro plano (Q para parar). Para publicar de novo, pare o IIS Express antes.

.EXAMPLE
    .\tools\iniciar-iisexpress.ps1
    Abre em https://localhost:44371/siat_dsv/
.EXAMPLE
    .\tools\iniciar-iisexpress.ps1 -Caminho /
    O SIAT na raiz do site.
.EXAMPLE
    .\tools\iniciar-iisexpress.ps1 -SemAutenticacaoWindows
#>
param(
    [switch] $SemAutenticacaoWindows,

    # Caminho do aplicativo no site: "/" para a raiz ou um diretório virtual como no servidor (/siat_dsv).
    [ValidatePattern('^(/|(/[A-Za-z0-9_.\-]+)+)$')]
    [string] $Caminho = '/siat_dsv',

    # O certificado de desenvolvimento do IIS Express já fica associado às portas 44300 a 44399.
    [ValidateRange(44300, 44399)]
    [int] $Porta = 44371
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path $PSScriptRoot -Parent
$pasta = Join-Path $raiz '.iisexpress'
$publicacao = Join-Path $pasta 'site'
$raizDoSite = Join-Path $pasta 'raiz'
$config = Join-Path $pasta 'applicationhost.config'
$naRaiz = $Caminho -eq '/'
$baseHref = if ($naRaiz) { '' } else { $Caminho }
$iisExpress = Join-Path $env:ProgramFiles 'IIS Express'
$appcmd = Join-Path $iisExpress 'appcmd.exe'

if (-not (Test-Path (Join-Path $iisExpress 'Asp.Net Core Module\V2\aspnetcorev2.dll'))) {
    throw 'O ASP.NET Core Module V2 do IIS Express não está instalado (vem com o Visual Studio ou com o Hosting Bundle).'
}

Write-Host '1/4 Publicando o Tcmrj.Siat.Web...'
dotnet publish (Join-Path $raiz 'src\Tcmrj.Siat.Web') -c Debug -o $publicacao -p:EnvironmentName=Development --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou. Se o IIS Express estiver rodando, pare-o antes (ele prende as DLLs).' }

# O <base href> tem de ser o caminho do aplicativo no IIS, senão as rotas e os arquivos estáticos saem dele.
@{ SiatConfiguration = @{ BlazorBaseHref = $baseHref } } | ConvertTo-Json |
    Set-Content (Join-Path $publicacao 'appsettings.Development.json') -Encoding utf8

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

$url = "https://localhost:$Porta" + $baseHref + '/'
Write-Host "3/4 Criando o site SIAT ($url)..."
& $appcmd delete site 'WebSite1' /apphostconfig:$config | Out-Null
if ($naRaiz) {
    & $appcmd add site /name:SIAT /bindings:"https/*:${Porta}:localhost" /physicalPath:$publicacao /apphostconfig:$config | Out-Null
}
else {
    # Raiz do site vazia e o SIAT como aplicativo no caminho, como em W:\TcmRoot\dsv\Sites\siat_dsv.
    New-Item -ItemType Directory -Force $raizDoSite | Out-Null
    & $appcmd add site /name:SIAT /bindings:"https/*:${Porta}:localhost" /physicalPath:$raizDoSite /apphostconfig:$config | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'appcmd add site falhou.' }
    & $appcmd add app /site.name:SIAT /path:$Caminho /physicalPath:$publicacao /apphostconfig:$config | Out-Null
}
if ($LASTEXITCODE -ne 0) { throw 'appcmd add site/app falhou.' }

$caminhoDeConfiguracao = 'SIAT' + $(if ($naRaiz) { '/' } else { $Caminho })
if ($SemAutenticacaoWindows) {
    Write-Warning 'Autenticação do Windows desligada (-SemAutenticacaoWindows): o servidor antes do pedido à infra.'
}
else {
    # O mesmo pedido que vai para a infra: ligar a autenticação do Windows só para o aplicativo do SIAT (location no
    # applicationHost.config), nunca para o servidor todo, e sem mexer no anônimo.
    & $appcmd set config $caminhoDeConfiguracao -section:system.webServer/security/authentication/windowsAuthentication /enabled:true /commit:apphost /apphostconfig:$config
    if ($LASTEXITCODE -ne 0) { throw 'appcmd set config falhou.' }
}

Write-Host "4/4 Iniciando o IIS Express em $url (Q para parar)..."
& (Join-Path $iisExpress 'iisexpress.exe') /config:$config /site:SIAT
