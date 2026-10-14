[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$artifacts=Join-Path $repoRoot '.artifacts/rust-ui'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$readiness='http://localhost:22543/health/launch-ready'
$ready=$false
try {$ready=(Invoke-WebRequest $readiness -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200} catch {}
if(-not $ready){
 $listener=Get-NetTCPConnection -LocalPort 22543 -State Listen -ErrorAction SilentlyContinue
 if(-not $listener){
  if(-not $NoBuild){
   & dotnet build (Join-Path $repoRoot 'TomasAI.IFM.Application.Api.Server/TomasAI.IFM.Application.Api.Server.csproj') -c Debug -m:1 --nologo
   if($LASTEXITCODE -ne 0){throw 'Development API build failed'}
  }
  $api=Join-Path $repoRoot 'TomasAI.IFM.Application.Api.Server/bin/Debug/net10.0/TomasAI.IFM.Application.Api.Server.exe'
  if(-not (Test-Path -LiteralPath $api)){throw 'Build the Development API before using -NoBuild'}
  & "$PSScriptRoot/Start-RecoveryDevelopmentApi.ps1" -RepositoryRoot $repoRoot -WorkflowDelivery
  Write-Host 'Started owned Development API with workflow delivery and offline broker emulator enabled. It remains running after testing.'

 }
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot '.vscode/Wait-IFMDevelopmentApi.ps1')
if($LASTEXITCODE -ne 0){throw 'Backend launch readiness failed; live tests were not run'}
if(-not $NoBuild){& "$PSScriptRoot/Build-RustUi.ps1" -Release}
& "$PSScriptRoot/Test-RustUi.ps1" -Live
