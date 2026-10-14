[CmdletBinding()]
param([string]$RepositoryRoot,[switch]$WorkflowDelivery)
if([string]::IsNullOrWhiteSpace($RepositoryRoot)){$RepositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path}
$ErrorActionPreference='Stop'
$env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_ENVIRONMENT='Development'
$artifacts=Join-Path $RepositoryRoot '.artifacts/rust-ui'
$api=Join-Path $RepositoryRoot 'TomasAI.IFM.Application.Api.Server/bin/Debug/net10.0/TomasAI.IFM.Application.Api.Server.exe'
$workflowBefore=$env:AppSettings__IntrinsicTimeStrategyWorkflow__Enabled
$simulationBefore=$env:TradeBroker__Emulator__OfflineSimulation__Enabled
try {
if($WorkflowDelivery){$env:AppSettings__IntrinsicTimeStrategyWorkflow__Enabled='true';$env:TradeBroker__Emulator__OfflineSimulation__Enabled='true'}
$process=Start-Process -FilePath $api -WorkingDirectory (Join-Path $RepositoryRoot 'TomasAI.IFM.Application.Api.Server') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifacts 'recovery-api-stdout.log') -RedirectStandardError (Join-Path $artifacts 'recovery-api-stderr.log') -PassThru
} finally {$env:AppSettings__IntrinsicTimeStrategyWorkflow__Enabled=$workflowBefore;$env:TradeBroker__Emulator__OfflineSimulation__Enabled=$simulationBefore}
@{WorkflowDelivery=[bool]$WorkflowDelivery;Environment='Development'} | ConvertTo-Json | Set-Content (Join-Path $artifacts 'live-api-options.json')
$process.Id | Set-Content (Join-Path $artifacts 'live-api.pid')
Write-Output $process.Id
