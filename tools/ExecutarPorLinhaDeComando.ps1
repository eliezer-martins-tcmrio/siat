$VerbosePreference = "Continue"
$ErrorActionPreference = "Stop" 

# Pegar em variável o caminho de onde o script está sendo executado
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

Set-Location $scriptPath\..\src\Tcmrj.Siat.Web

dotnet run --framework net10.0 --configuration Release

Set-Location $scriptPath

[System.Console]::WriteLine("Aperte qualquer tecla para fechar")
[System.Console]::ReadLine()