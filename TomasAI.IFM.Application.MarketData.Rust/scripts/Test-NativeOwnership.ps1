param(
    [ValidateSet('debug','development','release')][string]$NativeProfile='development',
    [switch]$IncludeProductionDeadlines,
    [string]$BrokerUrl
)
$ErrorActionPreference='Stop'
$implementation=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repository=(Resolve-Path (Join-Path $implementation '..')).Path
function Invoke-Checked([string]$Executable,[string[]]$Arguments) {
    $previous=$ErrorActionPreference
    try {
        # Windows PowerShell wraps normal Cargo progress on stderr as errors.
        $ErrorActionPreference='Continue'
        & $Executable @Arguments 2>&1 | ForEach-Object { $_.ToString() }
        $result=$LASTEXITCODE
    } finally { $ErrorActionPreference=$previous }
    if($result -ne 0){throw ($Executable+' failed with exit code '+$result)}
}
# All SDK, Treasury, broker and stalled-store data used by these tests are local
# fixtures. Nothing selects a production backend, opens a live trade or injects
# an error into an existing Host/API process. Real trade acceptance is separate.
$saved=@{}
foreach($name in @('CARGO_INCREMENTAL','CARGO_PROFILE_TEST_DEBUG','CARGO_PROFILE_DEV_DEBUG','IFM_NATIVE_QUALIFICATION_LIBRARY','IFM_MARKETDATA_RECOVERY_OUTBOX')){
    $saved[$name]=[Environment]::GetEnvironmentVariable($name)
}
Push-Location $implementation
try {
    # Cargo fixtures and all managed children share an isolated test-only root.
    if(-not $env:IFM_MARKETDATA_RECOVERY_OUTBOX){$env:IFM_MARKETDATA_RECOVERY_OUTBOX=Join-Path ([IO.Path]::GetTempPath()) ('ifm-native-qualification-audit/'+[Guid]::NewGuid().ToString('N'))}
    $env:CARGO_INCREMENTAL='0';$env:CARGO_PROFILE_TEST_DEBUG='0';$env:CARGO_PROFILE_DEV_DEBUG='0'
    $cargoProfile=if($NativeProfile -eq 'debug'){'dev'}else{$NativeProfile}
    Invoke-Checked 'cargo' @('build','--locked','--profile',$cargoProfile,'--lib')
    $library=(Resolve-Path ('target/'+$NativeProfile+'/ifm_market_data_native.dll')).Path
    foreach($helper in @('OwnerBrowserBridge','OwnerPreparationBridge','NativeOwnershipBridge','FacadeContract','HostCompositionBridge','HostExecutableBridge')){
        Invoke-Checked 'dotnet' @('build',('qualification/'+$helper+'/'+$helper+'.csproj'),'-v','quiet',('-p:NativeProfile='+$NativeProfile))
    }
    Invoke-Checked 'dotnet' @('qualification/FacadeContract/bin/Debug/net10.0/FacadeContract.dll')
    Invoke-Checked 'dotnet' @('qualification/HostCompositionBridge/bin/Debug/net10.0/HostCompositionBridge.dll',$library)
    Invoke-Checked 'cargo' @('clippy','--locked','--all-targets','--','-D','warnings')
    # Limit simultaneous socket fixtures for reproducible workstation scheduling;
    # individual tests still exercise actual concurrent sources/tasks/consumers.
    Invoke-Checked 'cargo' @('test','--locked','--all-targets','--','--test-threads=8')
    $env:IFM_NATIVE_QUALIFICATION_LIBRARY=$library
    if($IncludeProductionDeadlines){
        Invoke-Checked 'cargo' @('test','--locked','--test','lane_transport','--','--ignored','--nocapture','--test-threads=6')
        Invoke-Checked 'cargo' @('test','--locked','--test','dns_lifecycle','production_dns_deadline','--','--ignored','--nocapture')
        foreach($gate in @(@('native_evidence_exchange','native_spool_storage_failure_expires'),@('native_futures_bootstrap','original_120_second_bootstrap_deadline'),@('production_deadline','production_deadline_expires_without_waiting'))){
            Invoke-Checked 'cargo' @('test','--locked','--test',$gate[0],$gate[1],'--','--ignored','--nocapture')
        }
    } else {
        Invoke-Checked 'cargo' @('test','--locked','--test','lane_transport','crosses_actual','--','--ignored','--nocapture')
        Invoke-Checked 'cargo' @('test','--locked','--test','lane_transport','native_preparation_rebuilds_while_the_csharp_business_commit_is_stalled','--','--ignored','--nocapture')
    }
    if($BrokerUrl){
        # The executable fixture uses unique subjects and fabricated storage. Its
        # own Host child is joined/killed in finally; it never takes over a Host.
        $output=Join-Path $repository ('.artifacts/rust-marketdata-qualification-host/'+$NativeProfile+'-'+[Guid]::NewGuid().ToString('N'))
        Invoke-Checked 'dotnet' @('publish','host','-c','Debug','--self-contained','false',('-p:NativeProfile='+$NativeProfile),'-o',$output,'-v','quiet')
        Invoke-Checked 'dotnet' @('qualification/HostExecutableBridge/bin/Debug/net10.0/HostExecutableBridge.dll',(Join-Path $output 'TomasAI.IFM.Application.MarketData.Host.exe'),$BrokerUrl)
    }
    Write-Host ('Native ownership qualification passed for '+$NativeProfile+'. Real-store/API/UI live acceptance remains a separate gate.')
} finally {
    Pop-Location
    foreach($entry in $saved.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)}
}
