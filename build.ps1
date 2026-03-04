<#
.SYNOPSIS
    Publica o UserTrace como EXE standalone self-contained.

.PARAMETER Rid
    Runtime identifier alvo. Padrão: todos (win-x64, win-x86, win-arm64).
    Exemplos: win-x64 | win-x86 | win-arm64

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Rid win-x64
#>
param(
    [string]$Rid = ""
)

$ErrorActionPreference = "Stop"
$ProjectFile = Join-Path $PSScriptRoot "UserTrace.csproj"

$Rids = if ($Rid) { @($Rid) } else { @("win-x64", "win-x86", "win-arm64") }

foreach ($rid in $Rids) {
    $output = Join-Path $PSScriptRoot "publish\$rid"
    Write-Host ""
    Write-Host "==> Publicando para $rid ..." -ForegroundColor Cyan

    if (Test-Path $output) {
        Remove-Item -Recurse -Force $output
    }

    dotnet publish $ProjectFile `
        --configuration Release `
        --runtime $rid `
        --self-contained true `
        --output $output `
        -p:WindowsAppSDKSelfContained=true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:EnableMsixTooling=true `
        -p:PublishReadyToRun=false `
        -p:PublishTrimmed=false

    if ($LASTEXITCODE -ne 0) {
        Write-Host "FALHA ao publicar para $rid (exit code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    $exe = Join-Path $output "UserTrace.exe"
    if (Test-Path $exe) {
        $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
        Write-Host "OK  $exe  ($size MB)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Publicacao concluida." -ForegroundColor Green
