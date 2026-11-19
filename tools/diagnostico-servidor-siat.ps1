#Requires -Version 5.1
<#
.SYNOPSIS
    PROTÓTIPO (features/prototipo_login_rede): verifica, sem alterar nada, se o servidor IIS (tcm2016web3, tcm2016web4)
    está pronto para o SIAT com autenticação do Windows, e grava um relatório em texto.

.DESCRIPTION
    Só leitura: não grava configuração do IIS, não reinicia nada e não mostra senha de app pool. Rodar no próprio
    servidor (área de trabalho remota), num PowerShell de 64 bits aberto como administrador. Por sessão remota
    (Invoke-Command, Enter-PSSession) as consultas ao AD (setspn) e ao SQL esbarram no "double hop" do Kerberos.

    Seções do relatório:
      1. Servidor: nome, domínio, sistema, usuário, elevação, IPs.
      2. IIS: versão, serviços, recursos "Autenticação do Windows" e "WebSocket".
      3. ASP.NET Core: ASP.NET Core Module V2, runtime Microsoft.AspNetCore.App 10, Hosting Bundle.
      4. applicationHost.config: bloqueio das seções de autenticação, liberações por caminho, ARR.
      5. Sites, aplicativos e app pools (identidade, nunca a senha). Para cada caminho de -Caminhos: se a seção
         windowsAuthentication está liberada, a autenticação efetiva e o ASPNETCORE_ENVIRONMENT. E quais aplicativos
         do servidor já usam autenticação do Windows.
      6. Certificados das associações https.
      7. DNS dos nomes de acesso e SPNs HTTP (Kerberos).
      8. Log do IIS dos caminhos: de onde vêm as requisições (ARR?), usuários autenticados e códigos de status.
      9. SQL: a view do vínculo lida com o usuário que roda o script, e o acesso das contas dos app pools ao
         CSLIB_UserManager.
     10. Pastas de publicação (W:\TcmRoot\<ambiente>\Sites).
     Resumo: as linhas [FALTA], [ATENCAO] e [ERRO].

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\diagnostico-servidor-siat.ps1
    Grava siat-diagnostico-<SERVIDOR>-<data>.txt na pasta atual.
.EXAMPLE
    .\diagnostico-servidor-siat.ps1 -SemSql -Caminhos /siat_hml,/sicati_hml
#>
[CmdletBinding()]
param(
    # Aplicativos a detalhar. O SIAT ainda não existe no servidor; SiCATI (Blazor, mesmo deploy) e SCAF mostram como
    # os aplicativos de lá estão configurados.
    [ValidatePattern('^(/[A-Za-z0-9_.\-]+)+$')]
    [string[]] $Caminhos = @('/siat_dsv', '/siat_hml', '/sicati_dsv', '/sicati_hml', '/scaf_dsv', '/scaf_hml'),

    # Nomes pelos quais os usuários chegam aos aplicativos (DNS e SPN).
    [string[]] $Nomes = @('tcmrj2.tcmnet.tcm', 'tcmrj.tcmnet.tcm'),

    # Domínio dos usuários de rede (UsuarioDeRede:Dominio no appsettings do SIAT).
    [string] $Dominio = 'TCMNET',

    # Onde fica o CSLIB_UserManager do ambiente (UserManagerConnection no appsettings do SIAT).
    [string] $ServidorSql = 'TCMBD3\DEV',
    [string] $BancoUserManager = 'CSLIB_UserManager',
    [switch] $SemSql,

    # Linhas do fim do log mais recente de cada site.
    [ValidateRange(1000, 500000)]
    [int] $LinhasDeLog = 50000,

    # Arquivo do relatório. Padrão: siat-diagnostico-<SERVIDOR>-<data>.txt na pasta atual.
    [string] $Saida
)

$ErrorActionPreference = 'Stop'

if (-not $Saida) {
    $Saida = Join-Path (Get-Location).ProviderPath ('siat-diagnostico-{0}-{1:yyyyMMdd-HHmm}.txt' -f $env:COMPUTERNAME, (Get-Date))
}

# Sem elevação o IIS não deixa ler o applicationHost.config, e o ServerManager devolve listas vazias sem erro: o
# relatório diria que aplicativos existentes não existem. Em 32 bits, System32 vira SysWOW64, sem a configuração do IIS.
$identidade = [Security.Principal.WindowsIdentity]::GetCurrent()
$elevado = (New-Object Security.Principal.WindowsPrincipal($identidade)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $elevado -or -not [Environment]::Is64BitProcess) {
    $motivos = @()
    if (-not $elevado) { $motivos += 'o PowerShell não foi aberto como administrador' }
    if (-not [Environment]::Is64BitProcess) { $motivos += 'o PowerShell é o de 32 bits (x86)' }
    Write-Host ('Diagnóstico não executado: {0}.' -f ($motivos -join ' e ')) -ForegroundColor Red
    Write-Host 'Abra "Windows PowerShell" (não o x86 nem o ISE x86) com "Executar como administrador" e rode:' -ForegroundColor Yellow
    Write-Host ('    powershell -ExecutionPolicy Bypass -File "{0}"' -f $MyInvocation.MyCommand.Path) -ForegroundColor Yellow
    exit 1
}
$script:inetsrv = Join-Path $env:windir 'System32\inetsrv'

$script:relatorio = New-Object 'System.Collections.Generic.List[string]'
$script:pendencias = New-Object 'System.Collections.Generic.List[string]'
$script:apps = New-Object 'System.Collections.Generic.List[object]'
$script:appsComWindows = New-Object 'System.Collections.Generic.List[object]'
$script:certificados = New-Object 'System.Collections.Generic.List[object]'
$script:hostsDosBindings = New-Object 'System.Collections.Generic.List[string]'
$script:pools = @{}
$script:ipsLocais = @()
$script:fqdn = $env:COMPUTERNAME
$script:contaDoComputador = $null
$script:gerenciador = $null

#region Utilitários

function Write-Linha {
    param([string] $Texto = '', [string] $Cor)
    $script:relatorio.Add($Texto)
    if ($Cor) { Write-Host $Texto -ForegroundColor $Cor } else { Write-Host $Texto }
}

function Write-Secao([string] $Titulo) {
    Write-Linha ''
    Write-Linha ('=' * 100) -Cor Cyan
    Write-Linha $Titulo -Cor Cyan
    Write-Linha ('=' * 100) -Cor Cyan
}

# [FALTA]: impede o SIAT de funcionar. [ATENCAO]: risco ou item a confirmar. [ERRO]: a verificação não conseguiu rodar.
function Write-Status {
    param(
        [Parameter(Mandatory)] [ValidateSet('OK', 'INFO', 'ATENCAO', 'FALTA', 'ERRO')] [string] $Nivel,
        [Parameter(Mandatory)] [string] $Texto
    )
    $cor = @{ OK = 'Green'; INFO = ''; ATENCAO = 'Yellow'; FALTA = 'Red'; ERRO = 'Magenta' }[$Nivel]
    $linha = '[{0}] {1}' -f $Nivel, $Texto
    Write-Linha $linha -Cor $cor
    if ($Nivel -in 'ATENCAO', 'FALTA', 'ERRO') { $script:pendencias.Add($linha) }
}

function Write-Detalhe([string] $Texto) { Write-Linha ('        ' + $Texto) }

# Cada seção roda isolada: uma falha vira [ERRO] no relatório e a próxima seção roda.
function Invoke-Secao([string] $Titulo, [scriptblock] $Bloco) {
    Write-Secao $Titulo
    try { & $Bloco }
    catch { Write-Status ERRO ('A verificação desta seção parou: {0}' -f (Format-Erro $_)) }
}

# Programa externo (setspn) sem transformar a saída de erro em exceção.
function Invoke-Nativo([string] $Programa, [string[]] $Argumentos) {
    $ErrorActionPreference = 'Continue'
    $linhas = @(& $Programa @Argumentos 2>&1 | ForEach-Object { "$_" })
    [pscustomobject]@{ Codigo = $LASTEXITCODE; Linhas = $linhas }
}

# Mensagens do IIS e do SQL vêm em várias linhas; no relatório, uma só.
function Format-Erro($Erro) { ($Erro.Exception.Message -replace '\s+', ' ').Trim() }

function Test-CaminhoDoSiat([string] $Caminho) { $Caminho -like '/siat*' }

# O Microsoft.Web.Administration devolve atributos enumerados como número.
function ConvertTo-TokenChecking($Valor) {
    switch ("$Valor") { '0' { 'None' } '1' { 'Allow' } '2' { 'Require' } default { "$Valor" } }
}

# Allow ou Deny: se o web.config do caminho pode configurar a autenticação do Windows (o que o appcmd unlock muda).
function Get-LiberacaoWindowsAuth([string] $Site, [string] $Caminho) {
    try {
        $secao = $script:gerenciador.GetApplicationHostConfiguration().GetSection(
            'system.webServer/security/authentication/windowsAuthentication', $Site + $Caminho)
        [string] $secao.OverrideModeEffective
    }
    catch { 'desconhecido ({0})' -f (Format-Erro $_) }
}

# Autenticação efetiva de um caminho, como o IIS vai aplicar (applicationHost.config + web.config). Lança exceção quando
# o web.config declara uma seção bloqueada: é o mesmo erro que o IIS devolve como HTTP 500.19.
function Get-Autenticacao([string] $Site, [string] $Caminho) {
    $cfg = $script:gerenciador.GetWebConfiguration($Site, $Caminho)
    $wa = $cfg.GetSection('system.webServer/security/authentication/windowsAuthentication')
    $an = $cfg.GetSection('system.webServer/security/authentication/anonymousAuthentication')
    $provedores = @($wa.GetCollection('providers') | ForEach-Object { $_.GetAttributeValue('value') }) -join ', '
    $webSocket = $null
    try { $webSocket = $cfg.GetSection('system.webServer/webSocket').GetAttributeValue('enabled') } catch { }
    $tokenChecking = $wa.GetChildElement('extendedProtection').GetAttributeValue('tokenChecking')
    [pscustomobject]@{
        Windows            = [bool] $wa.GetAttributeValue('enabled')
        Provedores         = $provedores
        KernelMode         = $wa.GetAttributeValue('useKernelMode')
        AppPoolCredentials = $wa.GetAttributeValue('useAppPoolCredentials')
        TokenChecking      = (ConvertTo-TokenChecking $tokenChecking)
        Anonimo            = [bool] $an.GetAttributeValue('enabled')
        UsuarioAnonimo     = [string] $an.GetAttributeValue('userName')
        WebSocket          = $webSocket
    }
}

function Format-Autenticacao($A) {
    'Windows={0} (provedores: {1}; kernel mode={2}; useAppPoolCredentials={3}; extendedProtection={4}); anônimo={5}{6}; WebSocket={7}' -f
        $A.Windows, $A.Provedores, $A.KernelMode, $A.AppPoolCredentials, $A.TokenChecking, $A.Anonimo,
        $(if ($A.UsuarioAnonimo) { " (como $($A.UsuarioAnonimo))" } else { ' (como a identidade do pool)' }), $A.WebSocket
}

function Get-NomeReverso([string] $Ip) {
    try { [Net.Dns]::GetHostEntry($Ip).HostName } catch { '?' }
}

function Invoke-Consulta($Conexao, [string] $Sql, [hashtable] $Parametros = @{}) {
    $comando = $Conexao.CreateCommand()
    try {
        $comando.CommandText = $Sql
        $comando.CommandTimeout = 30
        foreach ($nome in $Parametros.Keys) { [void] $comando.Parameters.AddWithValue($nome, $Parametros[$nome]) }
        $tabela = New-Object System.Data.DataTable
        $leitor = $comando.ExecuteReader()
        try { $tabela.Load($leitor) } finally { $leitor.Dispose() }
        # O PowerShell enumera a DataTable: quem chama recebe as linhas.
        $tabela
    }
    finally { $comando.Dispose() }
}

#endregion

Write-Linha 'Diagnóstico do servidor para o SIAT com autenticação do Windows (protótipo features/prototipo_login_rede)'
Write-Linha ('Gerado em {0:dd/MM/yyyy HH:mm} em {1} por {2}. Só leitura: nada foi alterado.' -f (Get-Date), $env:COMPUTERNAME, $identidade.Name)
Write-Linha ('Parâmetros: Caminhos={0}; Nomes={1}; Dominio={2}; ServidorSql={3}; Banco={4}; SemSql={5}' -f
    ($Caminhos -join ','), ($Nomes -join ','), $Dominio, $ServidorSql, $BancoUserManager, [bool] $SemSql)

Invoke-Secao '1. Servidor' {
    $cs = Get-CimInstance Win32_ComputerSystem
    $so = Get-CimInstance Win32_OperatingSystem
    try { $script:fqdn = [Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName } catch { }

    # Conta com que os app pools ApplicationPoolIdentity e NetworkService saem para a rede (SQL, AD).
    try {
        $sid = (New-Object Security.Principal.NTAccount("$env:COMPUTERNAME`$")).Translate([Security.Principal.SecurityIdentifier])
        $script:contaDoComputador = $sid.Translate([Security.Principal.NTAccount]).Value
    }
    catch { $script:contaDoComputador = "$Dominio\$env:COMPUTERNAME`$" }

    Write-Status INFO ('Servidor {0} ({1}), domínio {2}; conta do computador na rede: {3}' -f $env:COMPUTERNAME, $script:fqdn, $cs.Domain, $script:contaDoComputador)
    Write-Status INFO ('{0} (versão {1}); último boot {2:dd/MM/yyyy HH:mm}' -f $so.Caption, $so.Version, $so.LastBootUpTime)
    Write-Status OK ('PowerShell {0} de 64 bits, como administrador ({1}).' -f $PSVersionTable.PSVersion, $identidade.Name)

    $script:ipsLocais = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -ne '127.0.0.1' } | ForEach-Object { $_.IPAddress })
    Write-Status INFO ('IPs: {0}' -f ($script:ipsLocais -join ', '))
}

Invoke-Secao '2. IIS e recursos do Windows' {
    $inetStp = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\InetStp' -ErrorAction SilentlyContinue
    if (-not $inetStp) { Write-Status FALTA 'IIS não instalado.'; return }
    Write-Status INFO ('IIS {0}.{1}' -f $inetStp.MajorVersion, $inetStp.MinorVersion)

    foreach ($nome in 'W3SVC', 'WAS') {
        $servico = Get-Service $nome -ErrorAction SilentlyContinue
        if (-not $servico) { Write-Status FALTA "Serviço $nome não existe." }
        elseif ($servico.Status -eq 'Running') { Write-Status OK "Serviço $nome rodando." }
        else { Write-Status ATENCAO ('Serviço {0}: {1}.' -f $nome, $servico.Status) }
    }

    # Nome do recurso, descrição, nível se faltar e a DLL do módulo (para quando Get-WindowsFeature não existe).
    $recursos = @(
        @('Web-Windows-Auth', 'Autenticação do Windows', 'FALTA', 'authsspi.dll'),
        @('Web-WebSockets', 'Protocolo WebSocket (o Blazor Server usa; sem ele cai para long polling, mais lento)', 'ATENCAO', 'iiswsock.dll')
    )
    $temGetWindowsFeature = [bool] (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue)
    foreach ($r in $recursos) {
        if ($temGetWindowsFeature) { $instalado = (Get-WindowsFeature -Name $r[0]).Installed }
        else { $instalado = Test-Path (Join-Path $script:inetsrv $r[3]) }
        if ($instalado) { Write-Status OK ('{0} ({1}) instalado.' -f $r[1], $r[0]) }
        else { Write-Status $r[2] ('{0} ({1}) NÃO instalado.' -f $r[1], $r[0]) }
    }
}

Invoke-Secao '3. ASP.NET Core (o SIAT é .NET 10, hospedado in-process)' {
    $ancm = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'
    if (Test-Path $ancm) { Write-Status OK ('ASP.NET Core Module V2 {0}' -f (Get-Item $ancm).VersionInfo.FileVersion) }
    else { Write-Status FALTA 'ASP.NET Core Module V2 não instalado (vem com o Hosting Bundle).' }

    $pasta = Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.AspNetCore.App'
    $versoes = @()
    if (Test-Path $pasta) {
        $versoes = @(Get-ChildItem $pasta -Directory | ForEach-Object { $_.Name } |
                Sort-Object { [version] ($_ -replace '-.*$', '') })
    }
    if ($versoes.Count -gt 0) { Write-Status INFO ('Runtimes Microsoft.AspNetCore.App: {0}' -f ($versoes -join ', ')) }
    $dez = @($versoes | Where-Object { $_ -like '10.*' })
    if ($dez.Count -gt 0) { Write-Status OK ('Runtime do ASP.NET Core 10 instalado ({0}).' -f ($dez -join ', ')) }
    else { Write-Status FALTA 'Runtime do ASP.NET Core 10 (Microsoft.AspNetCore.App 10.x) ausente: instalar o Hosting Bundle do .NET 10.' }

    $bundles = @(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like '*Hosting*' } | Sort-Object DisplayName -Unique)
    foreach ($b in $bundles) { Write-Detalhe ('Instalado: {0} {1}' -f $b.DisplayName, $b.DisplayVersion) }
}

Invoke-Secao '4. applicationHost.config (bloqueio das seções de autenticação)' {
    $arquivo = Join-Path $script:inetsrv 'config\applicationHost.config'
    if (-not (Test-Path $arquivo)) { Write-Status ERRO "Não encontrado: $arquivo"; return }
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($arquivo)

    $grupo = "/configuration/configSections/sectionGroup[@name='system.webServer']/sectionGroup[@name='security']/sectionGroup[@name='authentication']"
    foreach ($secao in 'windowsAuthentication', 'anonymousAuthentication') {
        $definicao = $xml.SelectSingleNode("$grupo/section[@name='$secao']")
        if (-not $definicao) { Write-Status ATENCAO "Seção $secao não declarada."; continue }
        $padrao = $definicao.GetAttribute('overrideModeDefault')
        if (-not $padrao) { $padrao = 'Allow' }
        Write-Status INFO ('Seção {0}: overrideModeDefault={1} (Deny é o padrão do IIS: o web.config só pode configurá-la onde houver liberação)' -f $secao, $padrao)
    }

    foreach ($modulo in 'WindowsAuthenticationModule', 'AnonymousAuthenticationModule', 'AspNetCoreModuleV2', 'WebSocketModule') {
        $global = $xml.SelectSingleNode("/configuration/system.webServer/globalModules/add[@name='$modulo']")
        if ($global) { Write-Status OK "Módulo $modulo registrado." }
        elseif ($modulo -eq 'WebSocketModule') { Write-Status ATENCAO "Módulo $modulo não registrado." }
        else { Write-Status FALTA "Módulo $modulo não registrado." }
    }

    if ($xml.SelectSingleNode("/configuration/system.webServer/globalModules/add[contains(@image, 'requestRouter.dll')]")) {
        Write-Status INFO 'ARR (Application Request Routing) instalado neste servidor.'
    }
    else { Write-Status INFO 'ARR não instalado neste servidor.' }

    # Cada windowsAuthentication declarado no applicationHost.config e a liberação (overrideMode) do location dele.
    # O "appcmd unlock config <site>/siat_dsv" grava um location overrideMode="Allow" com essa seção vazia.
    Write-Linha ''
    Write-Linha 'windowsAuthentication no applicationHost.config (por location):'
    foreach ($wa in $xml.SelectNodes('//system.webServer/security/authentication/windowsAuthentication')) {
        $local = $wa.ParentNode.ParentNode.ParentNode.ParentNode
        $caminho = '(servidor)'
        $modo = ''
        if ($local.LocalName -eq 'location') {
            if ($local.GetAttribute('path')) { $caminho = $local.GetAttribute('path') }
            $modo = $local.GetAttribute('overrideMode')
            if (-not $modo -and $local.GetAttribute('allowOverride')) { $modo = 'allowOverride=' + $local.GetAttribute('allowOverride') }
        }
        $atributos = @($wa.Attributes | ForEach-Object { '{0}={1}' -f $_.Name, $_.Value }) -join ' '
        $provedores = @($wa.SelectNodes('providers/add') | ForEach-Object { $_.GetAttribute('value') }) -join ', '
        $protecao = $wa.SelectSingleNode('extendedProtection')
        $tokenChecking = ''
        if ($protecao) { $tokenChecking = $protecao.GetAttribute('tokenChecking') }
        Write-Detalhe ('"{0}": overrideMode={1}; {2}; provedores: {3}; extendedProtection={4}' -f $caminho,
            $(if ($modo) { $modo } else { '(herda)' }), $(if ($atributos) { $atributos } else { '(sem atributos)' }),
            $(if ($provedores) { $provedores } else { '(herda)' }), $(if ($tokenChecking) { $tokenChecking } else { '(herda)' }))
    }
}

Invoke-Secao '5. Sites, aplicativos e app pools' {
    $dll = Join-Path $script:inetsrv 'Microsoft.Web.Administration.dll'
    if (-not (Test-Path $dll)) { Write-Status ERRO "IIS não instalado: $dll não existe."; return }
    Add-Type -Path $dll
    # Só leitura: o ServerManager só grava com CommitChanges, que este script nunca chama.
    $sm = New-Object Microsoft.Web.Administration.ServerManager
    if ($sm.Sites.Count -eq 0) {
        # Sem permissão de leitura o ServerManager devolve listas vazias em vez de erro.
        Write-Status ERRO 'Nenhum site lido do IIS (sem permissão no applicationHost.config?): sites, aplicativos e logs não verificados.'
        return
    }
    $script:gerenciador = $sm

    Write-Linha 'App pools (identidade e conta que sai para a rede; a senha nunca é lida):'
    foreach ($pool in $sm.ApplicationPools) {
        $pm = $pool.ProcessModel
        $tipo = [string] $pm.IdentityType
        $conta = $tipo
        $contaDeRede = $null
        if ($tipo -eq 'SpecificUser') { $conta = $pm.UserName; $contaDeRede = $pm.UserName }
        elseif ($tipo -eq 'ApplicationPoolIdentity') { $conta = "IIS AppPool\$($pool.Name)"; $contaDeRede = $script:contaDoComputador }
        elseif ($tipo -eq 'NetworkService') { $contaDeRede = $script:contaDoComputador }
        $estado = '?'
        try { $estado = [string] $pool.State } catch { }
        $clr = $pool.ManagedRuntimeVersion
        if (-not $clr) { $clr = 'No Managed Code' }
        $script:pools[$pool.Name] = [pscustomobject]@{ Nome = $pool.Name; Tipo = $tipo; Conta = $conta; ContaDeRede = $contaDeRede }
        Write-Detalhe ('{0}: {1} (rede: {2}); {3}; pipeline {4}; 32 bits={5}; {6}' -f $pool.Name, $conta,
            $(if ($contaDeRede) { $contaDeRede } else { '-' }), $clr, $pool.ManagedPipelineMode, $pool.Enable32BitAppOnWin64, $estado)
    }

    foreach ($site in $sm.Sites) {
        Write-Linha ''
        $estado = '?'
        try { $estado = [string] $site.State } catch { }
        Write-Linha ('Site "{0}" (id {1}, {2}):' -f $site.Name, $site.Id, $estado)
        foreach ($b in $site.Bindings) {
            $texto = '{0} {1}' -f $b.Protocol, $b.BindingInformation
            if ($b.Protocol -eq 'https' -and $b.CertificateHash) {
                $hash = ($b.CertificateHash | ForEach-Object { $_.ToString('X2') }) -join ''
                $loja = $b.CertificateStoreName
                if (-not $loja) { $loja = 'My' }
                $texto += (' (certificado {0} em {1})' -f $hash, $loja)
                $script:certificados.Add([pscustomobject]@{ Site = $site.Name; Binding = $b.BindingInformation; Hash = $hash; Loja = $loja })
            }
            if ($b.Protocol -in 'http', 'https') {
                # Host curinga (*.tcmnet.tcm) não é nome que se resolva no DNS.
                try { if ($b.Host -and -not $b.Host.Contains('*')) { $script:hostsDosBindings.Add($b.Host) } } catch { }
            }
            Write-Detalhe $texto
        }
        foreach ($app in $site.Applications) {
            $raiz = $app.VirtualDirectories['/']
            $fisico = ''
            if ($raiz) { $fisico = [Environment]::ExpandEnvironmentVariables($raiz.PhysicalPath) }
            $script:apps.Add([pscustomobject]@{ Site = $site.Name; SiteId = $site.Id; Caminho = $app.Path; Pool = $app.ApplicationPoolName; Fisico = $fisico })
            Write-Detalhe ('aplicativo {0} -> pool {1} -> {2}' -f $app.Path, $app.ApplicationPoolName, $fisico)
        }
    }

    Write-Linha ''
    Write-Linha 'Aplicativos que já usam autenticação do Windows neste servidor:'
    foreach ($app in $script:apps) {
        try {
            $a = Get-Autenticacao $app.Site $app.Caminho
            if ($a.Windows) {
                $script:appsComWindows.Add($app)
                Write-Detalhe ('{0}{1}: {2}' -f $app.Site, $app.Caminho, (Format-Autenticacao $a))
            }
        }
        catch { Write-Detalhe ('{0}{1}: configuração ilegível ({2})' -f $app.Site, $app.Caminho, (Format-Erro $_)) }
    }
    if ($script:appsComWindows.Count -eq 0) { Write-Detalhe '(nenhum)' }

    foreach ($caminho in $Caminhos) {
        Write-Linha ''
        Write-Linha ('--- {0} ---' -f $caminho) -Cor Cyan
        $doSiat = Test-CaminhoDoSiat $caminho
        $encontrados = @($script:apps | Where-Object { $_.Caminho -ieq $caminho })

        if ($encontrados.Count -eq 0) {
            if ($doSiat) { Write-Status INFO "Aplicativo $caminho ainda não existe neste servidor (a infra cria antes do primeiro deploy)." }
            else { Write-Status INFO "Aplicativo $caminho não existe neste servidor." }
            if (-not $doSiat) { continue }
            # O SIAT ainda não existe: verifica se a seção já está liberada para esse caminho em cada site.
            $liberado = $false
            foreach ($site in $sm.Sites) {
                $liberacao = Get-LiberacaoWindowsAuth $site.Name $caminho
                if ($liberacao -eq 'Allow') {
                    $liberado = $true
                    Write-Status OK ('Seção windowsAuthentication já liberada para "{0}{1}".' -f $site.Name, $caminho)
                }
                else { Write-Detalhe ('Seção windowsAuthentication para "{0}{1}": {2}' -f $site.Name, $caminho, $liberacao) }
            }
            if (-not $liberado) {
                # Sugere o site onde estão os outros aplicativos do mesmo deploy (SiCATI).
                $vizinho = @($script:apps | Where-Object { $_.Caminho -like '/sicati*' } | Select-Object -First 1)
                $siteSugerido = '<site>'
                if ($vizinho.Count -gt 0) { $siteSugerido = $vizinho[0].Site }
                Write-Status FALTA ('{0}: antes do primeiro deploy, a infra cria o aplicativo e libera a seção só para ele: appcmd unlock config "{1}{0}" -section:system.webServer/security/authentication/windowsAuthentication -commit:apphost' -f $caminho, $siteSugerido)
            }
            continue
        }

        foreach ($app in $encontrados) {
            $local = $app.Site + $app.Caminho
            $pool = $script:pools[$app.Pool]
            Write-Status INFO ('{0}: pool {1}, identidade {2}, rede {3}; pasta {4}' -f $local, $app.Pool, $pool.Conta,
                $(if ($pool.ContaDeRede) { $pool.ContaDeRede } else { '-' }), $app.Fisico)

            $liberacao = Get-LiberacaoWindowsAuth $app.Site $app.Caminho
            if ($liberacao -eq 'Allow') { Write-Status OK "Seção windowsAuthentication liberada para $local." }
            elseif ($doSiat) {
                Write-Status FALTA ('Seção windowsAuthentication bloqueada ({0}) para {1}: com o web.config do protótipo, HTTP 500.19 em todas as rotas. Pedir: appcmd unlock config "{1}" -section:system.webServer/security/authentication/windowsAuthentication -commit:apphost' -f $liberacao, $local)
            }
            else { Write-Status INFO "Seção windowsAuthentication para ${local}: $liberacao." }

            try {
                $a = Get-Autenticacao $app.Site $app.Caminho
                Write-Status INFO ('Autenticação efetiva: {0}' -f (Format-Autenticacao $a))
                if ($doSiat) {
                    if (-not $a.Windows) { Write-Status ATENCAO "Autenticação do Windows desligada em $local (o web.config do protótipo liga ao publicar)." }
                    if (-not $a.Anonimo) { Write-Status ATENCAO "Anônimo desligado em ${local}: /saude e os arquivos estáticos deixam de responder sem credencial (modo misto precisa dos dois)." }
                    if ($a.TokenChecking -eq 'Require') { Write-Status ATENCAO "extendedProtection=Require em ${local}: costuma quebrar a autenticação através do ARR." }
                    if ($a.WebSocket -eq $false) { Write-Status ATENCAO "WebSocket desligado em ${local}." }
                }
            }
            catch {
                $nivel = 'ATENCAO'
                if ($doSiat) { $nivel = 'FALTA' }
                Write-Status $nivel ('Configuração de {0} não pode ser lida ({1}). Se for "seção bloqueada", o IIS responde HTTP 500.19.' -f $local, (Format-Erro $_))
            }

            try {
                $aspNetCore = $sm.GetWebConfiguration($app.Site, $app.Caminho).GetSection('system.webServer/aspNetCore')
                $ambiente = @($aspNetCore.GetCollection('environmentVariables') |
                        Where-Object { $_.GetAttributeValue('name') -eq 'ASPNETCORE_ENVIRONMENT' } |
                        ForEach-Object { $_.GetAttributeValue('value') }) -join ','
                $hosting = "$($aspNetCore.GetAttributeValue('hostingModel'))"
                if (-not $hosting) { $hosting = '(não definido: outofprocess)' }
                Write-Detalhe ('aspNetCore: processPath={0}; hostingModel={1}; ASPNETCORE_ENVIRONMENT={2}; stdoutLogEnabled={3}' -f
                    $aspNetCore.GetAttributeValue('processPath'), $hosting, $(if ($ambiente) { $ambiente } else { '(não definido)' }),
                    $aspNetCore.GetAttributeValue('stdoutLogEnabled'))
            }
            catch { Write-Detalhe ('aspNetCore: não lido ({0})' -f (Format-Erro $_)) }

            if ($app.Fisico -and (Test-Path $app.Fisico)) {
                $configs = @(Get-ChildItem $app.Fisico -Filter 'appsettings*.json' -File -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
                $web = Get-Item (Join-Path $app.Fisico 'web.config') -ErrorAction SilentlyContinue
                Write-Detalhe ('pasta: web.config {0}; {1}' -f $(if ($web) { 'de {0:dd/MM/yyyy HH:mm}' -f $web.LastWriteTime } else { 'ausente' }),
                    $(if ($configs) { $configs -join ', ' } else { 'sem appsettings' }))
            }
            else { Write-Status ATENCAO "Pasta física de $local não existe: $($app.Fisico)" }
        }
    }
}

Invoke-Secao '6. Certificados das associações https' {
    if ($script:certificados.Count -eq 0) { Write-Status INFO 'Nenhuma associação https com certificado.'; return }
    foreach ($c in $script:certificados) {
        $cert = Get-Item ('Cert:\LocalMachine\{0}\{1}' -f $c.Loja, $c.Hash) -ErrorAction SilentlyContinue
        if (-not $cert) { Write-Status ATENCAO ('Certificado {0} de "{1}" ({2}) não encontrado em LocalMachine\{3}.' -f $c.Hash, $c.Site, $c.Binding, $c.Loja); continue }
        $nomes = @($cert.DnsNameList | ForEach-Object { $_.Unicode })
        $dias = [int] ($cert.NotAfter - (Get-Date)).TotalDays
        $texto = '"{0}" {1}: {2}; nomes: {3}; vence em {4:dd/MM/yyyy} ({5} dias)' -f $c.Site, $c.Binding, $cert.Subject, ($nomes -join ', '), $cert.NotAfter, $dias
        if ($dias -lt 30) { Write-Status ATENCAO $texto } else { Write-Status INFO $texto }
        foreach ($nome in $Nomes) {
            $cobre = @($nomes | Where-Object { $nome -like $_ }).Count -gt 0
            if (-not $cobre) { Write-Detalhe "não cobre $nome (só importa se o TLS terminar neste servidor e não no ARR)" }
        }
    }
}

Invoke-Secao '7. DNS e Kerberos (SPN)' {
    $todos = @(@($Nomes) + $script:hostsDosBindings.ToArray() + @($script:fqdn) | Where-Object { $_ } | Sort-Object -Unique)
    $passaPorOutroServidor = @{}
    foreach ($nome in $todos) {
        try {
            $respostas = @(Resolve-DnsName $nome -ErrorAction Stop)
            $cnames = @($respostas | Where-Object { $_.Type -eq 'CNAME' } | ForEach-Object { $_.NameHost })
            $ips = @($respostas | Where-Object { $_.Type -eq 'A' } | ForEach-Object { $_.IPAddress })
            $local = @($ips | Where-Object { $script:ipsLocais -contains $_ }).Count -gt 0
            $passaPorOutroServidor[$nome] = -not $local
            $destino = @($ips | ForEach-Object { '{0} ({1})' -f $_, (Get-NomeReverso $_) }) -join ', '
            Write-Status INFO ('{0}{1} -> {2}{3}' -f $nome, $(if ($cnames) { ' -> CNAME ' + ($cnames -join ' -> ') } else { '' }), $destino,
                $(if ($local) { ' [este servidor]' } else { ' [outro servidor: ARR ou balanceador]' }))
        }
        catch { Write-Status ATENCAO "DNS não resolve $nome." }
    }

    if (-not (Get-Command setspn.exe -ErrorAction SilentlyContinue)) { Write-Status ERRO 'setspn.exe não encontrado.'; return }
    Write-Linha ''
    foreach ($nome in @(@($Nomes) + @($script:fqdn) | Sort-Object -Unique)) {
        $spn = "HTTP/$nome"
        $r = Invoke-Nativo 'setspn.exe' @('-Q', $spn)
        $donos = @($r.Linhas | Where-Object { $_ -match '^\s*CN=' } | ForEach-Object { $_.Trim() })
        if ($donos.Count -gt 1) { Write-Status ATENCAO ('SPN {0} duplicado (Kerberos falha): {1}' -f $spn, ($donos -join ' | ')) }
        elseif ($donos.Count -eq 1) { Write-Status INFO ('SPN {0} registrado em {1}' -f $spn, $donos[0]) }
        elseif ($nome -eq $script:fqdn) { Write-Status INFO "Sem SPN $spn explícito (o HOST/ da conta do computador já cobre o nome do próprio servidor)." }
        elseif ($passaPorOutroServidor[$nome]) {
            Write-Status ATENCAO "Sem SPN ${spn}: por esse nome o navegador não consegue Kerberos e usa NTLM, que costuma falhar através do ARR. Se o teste falhar: SPN na conta do app pool e useAppPoolCredentials=true."
        }
        else { Write-Status INFO "Sem SPN ${spn}." }
    }

    $contas = @($script:pools.Values | Where-Object { $_.Tipo -eq 'SpecificUser' -and $_.ContaDeRede -like '*\*' } |
            ForEach-Object { $_.ContaDeRede } | Sort-Object -Unique)
    foreach ($conta in @($contas) + @($env:COMPUTERNAME)) {
        $r = Invoke-Nativo 'setspn.exe' @('-L', $conta)
        $spns = @($r.Linhas | Where-Object { $_ -match '^\s+\S+/\S+' } | ForEach-Object { $_.Trim() })
        $http = @($spns | Where-Object { $_ -like 'HTTP/*' })
        Write-Detalhe ('SPNs de {0}: {1} no total; HTTP: {2}' -f $conta, $spns.Count, $(if ($http) { $http -join ', ' } else { 'nenhum' }))
    }

    # Teste no navegador do próprio servidor por um nome que não é o dele dá 401 em loop (loopback check do NTLM).
    $lsa = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Lsa' -ErrorAction SilentlyContinue
    $msv = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0' -ErrorAction SilentlyContinue
    Write-Status INFO ('Loopback check: DisableLoopbackCheck={0}; BackConnectionHostNames={1} (testar o login de uma estação, não do navegador do servidor)' -f
        $lsa.DisableLoopbackCheck, (@($msv.BackConnectionHostNames) -join ','))
}

Invoke-Secao '8. Log do IIS (requisições reais aos caminhos)' {
    if (-not $script:gerenciador) { Write-Status ERRO 'Sem a seção 5 não há como achar os logs.'; return }
    $alvos = @(@($script:apps | Where-Object { $Caminhos -contains $_.Caminho }) + $script:appsComWindows.ToArray() |
            Sort-Object Site, Caminho -Unique)
    if ($alvos.Count -eq 0) { Write-Status INFO 'Nenhum dos caminhos existe aqui; nada a analisar.'; return }

    foreach ($grupo in ($alvos | Group-Object SiteId)) {
        $site = $script:gerenciador.Sites | Where-Object { $_.Id -eq [long] $grupo.Name }
        if (-not $site) { continue }
        $pasta = Join-Path ([Environment]::ExpandEnvironmentVariables($site.LogFile.Directory)) "W3SVC$($site.Id)"
        $arquivo = Get-ChildItem $pasta -Filter '*.log' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $arquivo) { Write-Status INFO ('Site "{0}": sem log em {1} (formato {2}).' -f $site.Name, $pasta, $site.LogFile.LogFormat); continue }

        $linhas = @(Get-Content $arquivo.FullName -Tail $LinhasDeLog)
        $cabecalho = @($linhas | Where-Object { $_ -like '#Fields:*' } | Select-Object -Last 1)
        if ($cabecalho.Count -eq 0) { $cabecalho = @(Get-Content $arquivo.FullName -TotalCount 20 | Where-Object { $_ -like '#Fields:*' } | Select-Object -Last 1) }
        if ($cabecalho.Count -eq 0) { Write-Status ATENCAO "Log $($arquivo.FullName) sem linha #Fields (não é W3C)."; continue }
        $campos = $cabecalho[0].Substring(9).Trim().Split(' ')
        $indice = @{}
        for ($k = 0; $k -lt $campos.Count; $k++) { $indice[$campos[$k]] = $k }
        Write-Linha ''
        Write-Linha ('Site "{0}": {1} ({2:dd/MM/yyyy HH:mm}), últimas {3} linhas; campos: {4}' -f $site.Name, $arquivo.FullName, $arquivo.LastWriteTime, $linhas.Count, ($campos -join ' '))
        if (-not $indice.ContainsKey('cs-uri-stem')) { Write-Status ATENCAO 'O log não tem cs-uri-stem.'; continue }

        foreach ($alvo in $grupo.Group) {
            $requisicoes = New-Object 'System.Collections.Generic.List[object]'
            foreach ($linha in $linhas) {
                if ($linha.Length -eq 0 -or $linha[0] -eq '#') { continue }
                if ($linha.IndexOf($alvo.Caminho, [StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
                $partes = $linha.Split(' ')
                if ($partes.Count -ne $campos.Count) { continue }
                $uri = $partes[$indice['cs-uri-stem']]
                if ($uri -ieq $alvo.Caminho -or $uri.StartsWith($alvo.Caminho + '/', [StringComparison]::OrdinalIgnoreCase)) { $requisicoes.Add($partes) }
            }
            if ($requisicoes.Count -eq 0) { Write-Detalhe ('{0}: nenhuma requisição nessas linhas.' -f $alvo.Caminho); continue }

            $periodo = ''
            if ($indice.ContainsKey('date') -and $indice.ContainsKey('time')) {
                $primeira = $requisicoes[0]; $ultima = $requisicoes[$requisicoes.Count - 1]
                $periodo = ' de {0} {1} a {2} {3} (UTC)' -f $primeira[$indice['date']], $primeira[$indice['time']], $ultima[$indice['date']], $ultima[$indice['time']]
            }
            Write-Detalhe ('{0}: {1} requisições{2}' -f $alvo.Caminho, $requisicoes.Count, $periodo)

            if ($indice.ContainsKey('c-ip')) {
                $origens = $requisicoes | Group-Object { $_[$indice['c-ip']] } | Sort-Object Count -Descending | Select-Object -First 5
                Write-Detalhe ('  origem (c-ip): {0}' -f (@($origens | ForEach-Object { '{0} {1} ({2})' -f $_.Name, (Get-NomeReverso $_.Name), $_.Count }) -join '; '))
            }
            if ($indice.ContainsKey('cs-username')) {
                $autenticadas = @($requisicoes | Where-Object { $_[$indice['cs-username']] -ne '-' })
                $usuarios = @($autenticadas | ForEach-Object { $_[$indice['cs-username']] } | Sort-Object -Unique)
                $dominios = @($usuarios | ForEach-Object { if ($_ -like '*\*') { $_.Split('\')[0] } else { '(sem domínio)' } } | Sort-Object -Unique)
                Write-Detalhe ('  usuário (cs-username): {0} requisições autenticadas, {1} usuários distintos, domínios: {2}' -f
                    $autenticadas.Count, $usuarios.Count, $(if ($dominios) { $dominios -join ', ' } else { '-' }))
            }
            if ($indice.ContainsKey('sc-status')) {
                $temSub = $indice.ContainsKey('sc-substatus')
                $status = $requisicoes | Group-Object {
                    if ($temSub) { '{0}.{1}' -f $_[$indice['sc-status']], $_[$indice['sc-substatus']] } else { $_[$indice['sc-status']] }
                } | Sort-Object Count -Descending | Select-Object -First 8
                Write-Detalhe ('  status: {0}' -f (@($status | ForEach-Object { '{0} ({1})' -f $_.Name, $_.Count }) -join '; '))
                if (@($status | Where-Object { $_.Name -eq '500.19' }).Count -gt 0) {
                    Write-Status ATENCAO "HTTP 500.19 no log de $($alvo.Caminho): configuração inválida ou seção bloqueada."
                }
            }
        }
    }
}

Invoke-Secao "9. SQL ($ServidorSql, $BancoUserManager)" {
    if ($SemSql) { Write-Status INFO 'Pulado (-SemSql).'; return }
    $conexao = New-Object System.Data.SqlClient.SqlConnection(
        "Server=$ServidorSql;Database=$BancoUserManager;Integrated Security=SSPI;Application Name=SIAT diagnostico;Connect Timeout=10")
    try {
        $conexao.Open()
        $quem = @(Invoke-Consulta $conexao 'SELECT SUSER_SNAME() AS Login, @@SERVERNAME AS Servidor, DB_NAME() AS Banco')[0]
        Write-Status OK ('Conectado como {0} em {1}/{2} (com a conta de quem roda o script, não a do app pool).' -f $quem.Login, $quem.Servidor, $quem.Banco)

        try {
            $vinculos = @(Invoke-Consulta $conexao @'
SELECT COUNT(*) AS Ativos,
       COUNT(DISTINCT LoginRede) AS Logins,
       (SELECT COUNT(*) FROM (SELECT LoginRede FROM dbo.vwSIRHU_Integracao
                              WHERE Ativo = 1 AND LoginRede <> '' GROUP BY LoginRede HAVING COUNT(*) > 1) d) AS Duplicados
FROM dbo.vwSIRHU_Integracao
WHERE Ativo = 1 AND LoginRede IS NOT NULL AND LoginRede <> ''
'@)[0]
            Write-Status OK ('vwSIRHU_Integracao: {0} linhas ativas com LoginRede, {1} logins distintos.' -f $vinculos.Ativos, $vinculos.Logins)
            if ($vinculos.Duplicados -gt 0) { Write-Status INFO ('{0} logins com mais de uma matrícula ativa: esses usuários veem "mais de uma matrícula" e ficam sem acesso.' -f $vinculos.Duplicados) }
        }
        catch { Write-Status ATENCAO ('vwSIRHU_Integracao não pôde ser lida com {0}: {1}' -f $quem.Login, (Format-Erro $_)) }

        # A mesma consulta do SIAT (src/Tcmrj.Siat.Infra/Sql/usuario-de-rede.sql), para quem roda o script.
        if ($env:USERDOMAIN -ieq $Dominio) {
            try {
                $eu = @(Invoke-Consulta $conexao @'
SELECT TOP (2) f.Matricula, u.Name AS Nome, u.Email, CAST(ISNULL(u.Enabled, 0) AS bit) AS Habilitado
FROM dbo.vwSIRHU_Integracao f
LEFT JOIN dbo.Users u ON u.Id = f.Matricula
WHERE f.LoginRede = @LoginRede AND f.Ativo = 1
'@ @{ '@LoginRede' = $env:USERNAME })
                if ($eu.Count -eq 1 -and $eu[0].Habilitado) { Write-Status OK ('Vínculo de {0}\{1}: matrícula {2}, usuário habilitado no UserManager.' -f $env:USERDOMAIN, $env:USERNAME, $eu[0].Matricula) }
                else { Write-Status INFO ('Vínculo de {0}\{1}: {2} linha(s); habilitado: {3}.' -f $env:USERDOMAIN, $env:USERNAME, $eu.Count, $(if ($eu.Count -gt 0) { $eu[0].Habilitado } else { '-' })) }
            }
            catch { Write-Status ATENCAO ('Consulta do vínculo falhou: {0}' -f (Format-Erro $_)) }
        }

        # Contas que os app pools usam na rede: precisam de SELECT em dbo.vwSIRHU_Integracao (e no que ela lê) e dbo.Users.
        $contas = @($script:pools.Values | Where-Object { $_.ContaDeRede } | ForEach-Object { $_.ContaDeRede } | Sort-Object -Unique)
        Write-Linha ''
        Write-Linha 'Contas dos app pools no banco (o acesso por grupo do AD não aparece aqui; sem permissão de ver, a conta some):'
        foreach ($conta in $contas) {
            $usuarios = @(Invoke-Consulta $conexao @'
SELECT dp.principal_id AS Id, dp.name AS Nome, dp.type_desc AS Tipo,
       ISNULL(STUFF((SELECT ', ' + r.name FROM sys.database_role_members rm
                     JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
                     WHERE rm.member_principal_id = dp.principal_id FOR XML PATH('')), 1, 2, ''), '') AS Papeis,
       ISNULL(STUFF((SELECT ', ' + p.state_desc + ' ' + p.permission_name
                            + CASE WHEN p.class = 1 THEN ' em ' + OBJECT_SCHEMA_NAME(p.major_id) + '.' + OBJECT_NAME(p.major_id)
                                   WHEN p.class = 3 THEN ' no schema ' + SCHEMA_NAME(p.major_id) ELSE '' END
                     FROM sys.database_permissions p WHERE p.grantee_principal_id = dp.principal_id FOR XML PATH('')), 1, 2, ''), '') AS Permissoes
FROM sys.database_principals dp
WHERE dp.name = @Conta OR dp.sid = SUSER_SID(@Conta)
'@ @{ '@Conta' = $conta })
            if ($usuarios.Count -eq 0) { Write-Detalhe ('{0}: não aparece como usuário do banco.' -f $conta); continue }
            foreach ($u in $usuarios) {
                Write-Detalhe ('{0}: usuário {1} ({2}); papéis: {3}; permissões: {4}' -f $conta, $u.Nome, $u.Tipo,
                    $(if ($u.Papeis) { $u.Papeis } else { '-' }), $(if ($u.Permissoes) { $u.Permissoes } else { '-' }))
            }
        }

        $grupos = @(Invoke-Consulta $conexao "SELECT name AS Nome FROM sys.database_principals WHERE type = 'G' ORDER BY name")
        Write-Detalhe ('Grupos do AD com usuário no banco: {0}' -f $(if ($grupos) { @($grupos | ForEach-Object { $_.Nome }) -join ', ' } else { 'nenhum visível' }))
        Write-Status INFO 'O SIAT também abre AUDITORIADB e os servidores das consultas com a identidade do app pool; isso não é verificado aqui.'
    }
    catch {
        $sql = $_.Exception
        while ($sql -and -not ($sql -is [System.Data.SqlClient.SqlException])) { $sql = $sql.InnerException }
        if ($sql -and $sql.Number -eq 18456) {
            # Conta administrativa do servidor (ex.: _a1) sem login no SQL: não é pendência do servidor.
            Write-Status ERRO ('SQL não verificado: {0} não tem login em {1}. Rode com -SemSql; o acesso das contas dos app pools (seção 5) dá para conferir de uma estação.' -f $identidade.Name, $ServidorSql)
        }
        else { Write-Status ERRO ('SQL: {0}' -f (Format-Erro $_)) }
    }
    finally { $conexao.Dispose() }
}

Invoke-Secao '10. Pastas de publicação' {
    foreach ($ambiente in 'dsv', 'hml') {
        $pasta = "W:\TcmRoot\$ambiente\Sites"
        if (-not (Test-Path $pasta)) { Write-Status INFO "$pasta não existe neste servidor."; continue }
        $sub = @(Get-ChildItem $pasta -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like 'siat*' -or $_.Name -like 'sicati*' -or $_.Name -like 'scaf*' })
        Write-Status INFO ('{0}: {1}' -f $pasta, $(if ($sub) { @($sub | ForEach-Object { '{0} ({1:dd/MM/yyyy})' -f $_.Name, $_.LastWriteTime }) -join ', ' } else { 'sem siat*, sicati*, scaf*' }))
    }
    $w = Get-PSDrive W -ErrorAction SilentlyContinue
    if ($w -and $w.Free) { Write-Status INFO ('Unidade W: {0:N1} GB livres.' -f ($w.Free / 1GB)) }
}

Write-Secao 'Resumo: o que falta ou precisa de atenção'
if ($script:pendencias.Count -eq 0) { Write-Linha 'Nada pendente.' -Cor Green }
foreach ($p in $script:pendencias.ToArray()) { Write-Linha $p }

$script:relatorio | Out-File -FilePath $Saida -Encoding utf8
Write-Host ''
Write-Host "Relatório gravado em $Saida" -ForegroundColor Green
