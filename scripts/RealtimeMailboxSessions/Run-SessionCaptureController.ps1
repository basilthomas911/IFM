[CmdletBinding()]
param([Parameter(Mandatory)][string]$Config,[Parameter(Mandatory)][string]$SecretFile,[switch]$ValidateNow,
    [switch]$BoundarySmoke,[string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$settings=Get-Content -LiteralPath $Config -Raw|ConvertFrom-Json
if(-not $ValidateNow -and -not $BoundarySmoke -and $settings.PSObject.Properties['bootstrapBeforeInitial'] -and $settings.bootstrapBeforeInitial){
    $statePath=Join-Path $settings.outputDirectory 'controller.json'
    $resumeState=if(Test-Path -LiteralPath $statePath){(Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json).state}else{$null}
    if($resumeState -and $resumeState -notin @('ArmedWaitingInitialBoundary','CapturingOriginalFlow')){throw 'A partially owned boundary requires inspection before any profile restart.'}
    if($resumeState -ne 'CapturingOriginalFlow'){
        $bootstrapArguments=@{Config=$Config;SecretFile=$SecretFile}
        if($settings.PSObject.Properties['bootstrapSkipBuild'] -and $settings.bootstrapSkipBuild){$bootstrapArguments.SkipBuild=$true}
        & (Join-Path $PSScriptRoot 'Start-CaptureDevelopmentProfile.ps1') @bootstrapArguments
        if($LASTEXITCODE -ne 0){throw 'Prequalification bootstrap failed; no owner baseline was certified.'}
    }
}
$secret=ConvertTo-SecureString ([IO.File]::ReadAllText([IO.Path]::GetFullPath($SecretFile)).Trim())
$pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $token=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ($token.Length -lt 32 -or $token.Length -gt 256) { throw 'Protected capture token has invalid bounds.' }
    $env:IFM_SESSION_CAPTURE_ACCESS_TOKEN=$token
    $env:SessionCapture__AccessToken=$token
    $env:IFM_REPLAY_CONTROL_TOKEN=$token
    $command=if($BoundarySmoke){'boundary-smoke'}elseif($ValidateNow){'validate-now'}else{'run'}
    $extra=if($BoundarySmoke){if(-not $OutputDirectory){throw 'Boundary smoke requires a new explicit output directory.'};@('--output',[IO.Path]::GetFullPath($OutputDirectory))}else{@()}
    & python (Join-Path $PSScriptRoot 'capture_controller.py') $command --config ([IO.Path]::GetFullPath($Config)) @extra
    if ($LASTEXITCODE -ne 0) { throw 'Capture controller stopped with retained evidence; inspect its protected output directory.' }
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    Remove-Item Env:IFM_SESSION_CAPTURE_ACCESS_TOKEN,Env:SessionCapture__AccessToken,Env:IFM_REPLAY_CONTROL_TOKEN -ErrorAction SilentlyContinue
    $token=$null;$secret.Dispose()
}
