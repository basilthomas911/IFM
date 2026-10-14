[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$managerDirectory = Join-Path $repositoryRoot 'TomasAI.IFM.Application.ServerManager/bin/Debug/net10.0-windows7.0'
$manager = Join-Path $managerDirectory 'IFMServerManager.exe'
$rust = Join-Path $repositoryRoot '.artifacts/rust-ui/app/ifm-ui-rust.exe'
if (!(Test-Path -LiteralPath $manager) -or !(Test-Path -LiteralPath $rust)) {
    throw 'Build the API + Rust UI Development task before launching.'
}
$previous = @{}
foreach ($name in @('DOTNET_ENVIRONMENT', 'IFM_REPOSITORY_ROOT', 'IFM_DEVELOPMENT_UI')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:IFM_REPOSITORY_ROOT = $repositoryRoot
    $env:IFM_DEVELOPMENT_UI = 'Rust'
    $process = Start-Process -FilePath $manager -WorkingDirectory $managerDirectory -WindowStyle Hidden -PassThru
    Write-Host "Started Server Manager PID $($process.Id): API + live Rust UI + Scheduler."
    Write-Host 'Stop with IFM: Stop API + UI (Development).'
}
finally {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
}
