param([switch]$Start,[string]$PackageDirectory,[string]$NatsUrl,
    [ValidateSet('Debug','Release')][string]$ApiConfiguration='Release',
    [string]$ManagerDirectory)
$ErrorActionPreference='Stop'
$implementation=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repository=(Resolve-Path (Join-Path $implementation '..')).Path
$managerSource=Join-Path $repository 'TomasAI.IFM.Application.ServerManager/bin/Debug/net10.0-windows7.0'
$package=if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $repository '.artifacts/rust-marketdata-package/Release'}
$api=Join-Path $repository ('TomasAI.IFM.Application.Api.Server/bin/'+$ApiConfiguration+'/net10.0')
$destination=if($ManagerDirectory){[IO.Path]::GetFullPath($ManagerDirectory)}else{Join-Path $repository '.artifacts/rust-marketdata-acceptance/Manager'}
$profile=Join-Path $repository 'TomasAI.IFM.UI.Net/appsettings.Development.json'
foreach($file in @((Join-Path $managerSource 'IFMServerManager.exe'),(Join-Path $package 'TomasAI.IFM.Application.MarketData.Host.exe'),(Join-Path $api 'TomasAI.IFM.Application.Api.Server.exe'),$profile,(Join-Path $repository '.artifacts/standalone-rust-ui/app/TomasAI.IFM.UI.Rust.eGui.exe'))){if(!(Test-Path -LiteralPath $file)){throw ('Required acceptance artifact is absent: '+$file)}}
# Never replace a running Manager or overwrite the current application's package.
# This command prepares an independent Manager package/configuration only.
if(Get-Process -Name IFMServerManager -ErrorAction SilentlyContinue){throw 'Stop the existing Server Manager before preparing this separate acceptance session.'}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $managerSource | Copy-Item -Destination $destination -Recurse -Force
$configuration=Join-Path $destination 'appsettings.Development.json'
$settings=Get-Content -LiteralPath $configuration -Raw | ConvertFrom-Json
$settings.ServerManager.Processes[0].WorkingDirectory=$api
$market=@($settings.ServerManager.Processes | Where-Object Key -eq 'market-data')
if($market.Count -ne 1){throw 'Expected exactly one managed market-data process.'}
$market[0].WorkingDirectory=$package
# An explicit test endpoint applies to native publication and passive fact ports
# together. Do not edit the normal launch configuration or Windows networking.
if($NatsUrl){
    if($NatsUrl -notmatch '^nats://[^\s]+$'){throw 'Expected an explicit nats:// acceptance endpoint.'}
    $market[0].Arguments=@($market[0].Arguments | Where-Object {$_ -notlike '--MarketDataHost:NatsUrl=*'})+@('--MarketDataHost:NatsUrl='+$NatsUrl)
}
$settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $configuration -Encoding UTF8
Write-Host ('Prepared opt-in native acceptance Manager: '+$destination)
Write-Host 'Current Host/Manager configuration and default VS Code launch remain unchanged.'
if(!$Start){Write-Host 'Run this script with -Start to launch the prepared API/native Host/eGui session.';exit 0}
$active=Get-Process -ErrorAction SilentlyContinue | Where-Object ProcessName -in @('TomasAI.IFM.Application.Api.Server','TomasAI.IFM.Application.MarketData.Host','TomasAI.IFM.UI.Rust.eGui','ifm-ui-rust')
if($active){throw 'An IFM API/Host/UI is already running; this acceptance launcher does not stop or take over existing processes.'}
$saved=@{}
try{
    foreach($entry in @{DOTNET_ENVIRONMENT='Development';IFM_REPOSITORY_ROOT=$repository;IFM_DEVELOPMENT_UI='eGui';IFM_UI_CONFIG_PATH=$profile}.GetEnumerator()){
        $saved[$entry.Key]=[Environment]::GetEnvironmentVariable($entry.Key)
        [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)
    }
    $process=Start-Process -FilePath (Join-Path $destination 'IFMServerManager.exe') -WorkingDirectory $destination -WindowStyle Hidden -PassThru
    Write-Host ('Opt-in acceptance Manager started; PID '+$process.Id)
}finally{foreach($entry in $saved.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)}}
