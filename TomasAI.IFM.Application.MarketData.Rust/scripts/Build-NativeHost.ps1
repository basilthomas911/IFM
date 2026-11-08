param([ValidateSet('Debug','Release')][string]$Configuration='Debug',[string]$OutputDirectory,[string]$ArtifactsDirectory)
$ErrorActionPreference='Stop'
function Invoke-Native([string]$Executable,[string[]]$Arguments) {
    Get-Command $Executable -CommandType Application -ErrorAction Stop | Out-Null
    $previous=$ErrorActionPreference
    try {
        # PowerShell 5.1 wraps ordinary Cargo stderr progress in ErrorRecord.
        $ErrorActionPreference='Continue'
        & $Executable @Arguments 2>&1 | ForEach-Object { $_.ToString() }
        $code=$LASTEXITCODE
    } finally { $ErrorActionPreference=$previous }
    if($code -ne 0){throw ($Executable+' failed with exit code '+$code)}
}
$implementation=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repository=(Resolve-Path (Join-Path $implementation '..')).Path
& (Join-Path $repository 'scripts/Development/Initialize-RustBuildTarget.ps1') -ProjectDirectory $implementation
$profile=if($Configuration -eq 'Release'){'release'}else{'development'}
$output=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $repository ('.artifacts/rust-marketdata-package/'+$Configuration)}
# Check before publishing any file: an older opt-in package may still be running.
# Use another output directory instead of replacing a loaded package piecemeal.
$prefix=[IO.Path]::GetFullPath($output).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
foreach($running in Get-Process -ErrorAction SilentlyContinue){
    try{$image=$running.MainModule.FileName}catch{continue}
    if($image -and $image.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){
        throw ('Package output is in use by '+$running.ProcessName+' (PID '+$running.Id+'). Supply a separate -OutputDirectory.')
    }
}
# Separate output only: neither current Host binaries nor its native assets are
# overwritten. Release packaging must consume the release native profile.
Invoke-Native 'cargo' @('build','--locked','--profile',$profile,'--manifest-path',(Join-Path $implementation 'Cargo.toml'))
$publishArguments=@('publish',(Join-Path $implementation 'host'),'-c',$Configuration,'--self-contained','false',('-p:NativeProfile='+$profile),'-o',$output,'-v','quiet')
if($ArtifactsDirectory){$publishArguments+=@('--artifacts-path',[IO.Path]::GetFullPath($ArtifactsDirectory))}
Invoke-Native 'dotnet' $publishArguments
$library=Join-Path $output 'ifm_market_data_native.dll'
$source=Join-Path $implementation ('target/'+$profile+'/ifm_market_data_native.dll')
if(!(Test-Path -LiteralPath $library) -or (Get-FileHash -LiteralPath $library).Hash -ne (Get-FileHash -LiteralPath $source).Hash){throw 'Packaged native library does not match its selected profile.'}
$symbols=Join-Path $output 'ifm_market_data_native.pdb'
$sourceSymbols=Join-Path $implementation ('target/'+$profile+'/ifm_market_data_native.pdb')
if(!(Test-Path -LiteralPath $symbols) -or !(Test-Path -LiteralPath $sourceSymbols) -or (Get-FileHash -LiteralPath $symbols).Hash -ne (Get-FileHash -LiteralPath $sourceSymbols).Hash){throw 'Packaged native PDB is missing or does not match its selected build.'}
$legacy=Get-ChildItem -LiteralPath $output -File -Recurse | Where-Object Name -in @('databento_feed_native.dll','ifm_treasury_native.dll')
if($legacy){throw 'Package unexpectedly contains legacy provider libraries.'}
[ordered]@{configuration=$Configuration;nativeProfile=$profile;nativeSha256=(Get-FileHash -LiteralPath $library).Hash.ToLowerInvariant();nativeBytes=(Get-Item -LiteralPath $library).Length;nativeSymbols='ifm_market_data_native.pdb';nativeSymbolsSha256=(Get-FileHash -LiteralPath $symbols).Hash.ToLowerInvariant();nativeSymbolsBytes=(Get-Item -LiteralPath $symbols).Length;host='TomasAI.IFM.Application.MarketData.Host.exe';productionAccepted=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'native-package.json') -Encoding UTF8
Write-Host ('Native Host package: '+$output)

exit 0
