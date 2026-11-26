[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Config,
    [string]$SecretFile,
    [switch]$SkipBuild
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $repository
$settings=Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$overlayPath=if($settings.PSObject.Properties['runtimeOverlayPath']){$settings.runtimeOverlayPath}else{Join-Path $PSScriptRoot 'prepared-capture-overlay.json'}
$overlay=Get-Content -LiteralPath $overlayPath -Raw | ConvertFrom-Json
$token=$env:IFM_SESSION_CAPTURE_ACCESS_TOKEN
if($SecretFile){
    $secure=(Get-Content -LiteralPath $SecretFile -Raw).Trim() | ConvertTo-SecureString
    $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try{$token=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)}
    finally{[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer);$secure.Dispose()}
}
if(!$token -or $token.Length -lt 32 -or $token.Length -gt 256){throw 'A protected CurrentUser capture token is required.'}
$checkpointDirectory=if($settings.PSObject.Properties['nativeCheckpointDirectory']){$settings.nativeCheckpointDirectory}else{'D:/IFM/SessionQualification/nov30-dec1/native-checkpoints'}
$logRoot=Join-Path (Split-Path $checkpointDirectory) 'bootstrap-logs'
New-Item -ItemType Directory -Path $checkpointDirectory,$logRoot -Force | Out-Null
$saved=@{}
function Set-CaptureEnvironment([string]$Name,[string]$Value){
    if(!$saved.ContainsKey($Name)){$saved[$Name]=[Environment]::GetEnvironmentVariable($Name)}
    [Environment]::SetEnvironmentVariable($Name,$Value)
}
function Invoke-Build([string]$Project,[string]$Name){
    & ./scripts/Build/Invoke-DotNet.ps1 build $Project --configuration Debug --nologo /maxcpucount:1 *> (Join-Path $logRoot ($Name+'.log'))
    if($LASTEXITCODE -ne 0){throw ('Capture bootstrap build failed: '+$Name)}
}
try{
    Set-CaptureEnvironment 'SessionCapture__AccessToken' $token
    Set-CaptureEnvironment 'IFM_SESSION_CAPTURE_ACCESS_TOKEN' $token
    foreach($pair in @{
        'Actors__RealtimeMailboxes__Enabled'='false';'Actors__RealtimeMailboxes__CapacityQualified'='false';
        'Actors__SessionCapture__Enabled'='true';'Actors__SessionCapture__Directory'=[string]$overlay.Actors.SessionCapture.Directory;
        'Actors__SessionCapture__SessionId'=[string]$overlay.Actors.SessionCapture.SessionId;
        'Actors__SessionCapture__MaximumWireBytes'=[string]$overlay.Actors.SessionCapture.MaximumWireBytes;
        'Actors__SessionCapture__MaximumArchiveBytes'=[string]$overlay.Actors.SessionCapture.MaximumArchiveBytes;
        'Actors__SessionCapture__MaximumReceiptBytes'=[string]$overlay.Actors.SessionCapture.MaximumReceiptBytes;
        'SessionCapture__Enabled'='true';'SessionCapture__Boundary__Enabled'='true';'SessionCapture__OwnerCheckpoints__Enabled'='true';
        'SessionCapture__CheckpointDirectory'=[string]$checkpointDirectory;
        'IFM_REALTIME_SOURCE_CAPTURE_ENABLED'='1';'IFM_SESSION_CAPTURE_BOUNDARY_ENABLED'='1';
        # Preserve the original raw owner, including sequence.bin and all WAL cursors. Native capture
        # previously disabled is initialized at its original default location, never an empty E: replacement.
        'IFM_REALTIME_SOURCE_DIRECTORY'=(Join-Path $env:LOCALAPPDATA 'IFM/RealtimeSourceCapture');
        'RawFuturesStorage__Directory'=(Join-Path $env:LOCALAPPDATA 'IFM/RawFuturesStorage')
    }.GetEnumerator()){Set-CaptureEnvironment $pair.Key $pair.Value}
    if($settings.PSObject.Properties['ownerPathsFile']){
        $ownerPaths=Get-Content -LiteralPath $settings.ownerPathsFile -Raw | ConvertFrom-Json
        foreach($owner in $ownerPaths.PSObject.Properties){
            if(![IO.Path]::IsPathFullyQualified([string]$owner.Value)){throw 'Owner paths must be absolute.'}
            Set-CaptureEnvironment ($owner.Name.Replace(':','__')) ([string]$owner.Value)
        }
    }
    # This is a profile bootstrap, not a qualifying snapshot shutdown. The snapshot coordinator uses
    # its separate no-force capture lifecycle and independently verifies every writer has exited.
    $previous=@(& ./scripts/Development/Get-IFMDevelopmentProcess.ps1)
    if(@($previous|Where-Object Role -eq 'api').Count -gt 0){
        foreach($required in @('apiUrl','nativeUrl','managerControl','powershell','archiveDirectory','archiveSessionId','outputDirectory')){
            if(-not $settings.PSObject.Properties[$required]){throw ('Existing profile shutdown requires the full controller configuration; missing '+$required+'. Use control/controller.json, not the old minimal bootstrap configuration.')}
        }
        try{$captureState=Invoke-RestMethod -Uri ($settings.apiUrl+'/internal/session-capture/status') -Headers @{'X-IFM-Session-Capture'=$token} -TimeoutSec 5}
        catch{throw 'Existing API capture status is unavailable; do not force away unknown original source ownership.'}
        if($captureState.enabled){
            & python (Join-Path $PSScriptRoot 'capture_controller.py') prepare-shutdown --config $Config --output (Join-Path $logRoot ('shutdown-'+[Guid]::NewGuid().ToString('N')))
            if($LASTEXITCODE -ne 0){throw 'Original native/API sources could not be parked and drained before bootstrap shutdown.'}
        }
    }
    & ./scripts/Development/Stop-IFMDevelopment.ps1 -TimeoutSeconds 300 -VerifyStopped *> (Join-Path $logRoot 'bootstrap-stop.log')
    if(@(& ./scripts/Development/Get-IFMDevelopmentProcess.ps1).Count -ne 0){throw 'Existing Development processes remain.'}
    if(!$SkipBuild){
        & ./TomasAI.IFM.Application.MarketData.Rust/scripts/Build-NativeHost.ps1 -Configuration Debug *> (Join-Path $logRoot 'native-build.log')
        if($LASTEXITCODE -ne 0){throw 'Capture bootstrap native package build failed.'}
        Invoke-Build 'TomasAI.IFM.Application.Api.Server/TomasAI.IFM.Application.Api.Server.csproj' 'api-build'
        Invoke-Build 'TomasAI.IFM.Application.ServerManager/TomasAI.IFM.Application.ServerManager.csproj' 'manager-build'
    }
    $global:LASTEXITCODE=0
    & {
        # Existing profile merging permits optional JSON properties absent from an override.
        Set-StrictMode -Off
        & ./TomasAI.IFM.Application.MarketData.Rust/scripts/Prepare-NativeAcceptance.ps1 -ApiConfiguration Debug `
            -PackageDirectory (Join-Path $repository '.artifacts/rust-marketdata-package/Debug') `
            -ManagerDirectory (Join-Path $repository '.artifacts/rust-marketdata-development/Manager') -Start
    } *> (Join-Path $logRoot 'profile-start.log')
    if($LASTEXITCODE -ne 0){throw 'Capture-only profile launch failed.'}
    [pscustomobject]@{version=1;state='CaptureOnlyProfileLaunchRequested';activationEnabled=$false;qualified=$false;
        nativeSourceBase=(Join-Path $env:LOCALAPPDATA 'IFM/RealtimeSourceCapture');rawStorageRoot=(Join-Path $env:LOCALAPPDATA 'IFM/RawFuturesStorage');
        checkpointDirectory=$checkpointDirectory;readinessVerified=$false} | ConvertTo-Json -Compress
}
finally{
    foreach($entry in $saved.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)}
    $token=$null
}
