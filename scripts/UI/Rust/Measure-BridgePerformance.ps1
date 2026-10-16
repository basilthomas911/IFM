[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Label,
 [string]$BridgeAssemblyRoot,
 [string]$Filter = '*',
 [switch]$SkipBridgeBuild
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$destination=Join-Path $repo ".artifacts/rust-ui/performance/$Label"
New-Item -ItemType Directory -Force -Path $destination | Out-Null
function Invoke-Dotnet([string[]]$Arguments,[string]$Log) {
 & dotnet @Arguments 2>&1 | ForEach-Object {$_.ToString()} | Tee-Object -FilePath $Log
 if($LASTEXITCODE -ne 0){throw "dotnet failed; see $Log"}
}
$oldRoot=$env:IFM_BRIDGE_BENCHMARK_ROOT;$oldGc=$env:DOTNET_gcServer
Push-Location $repo
try {
 if(-not $SkipBridgeBuild){Invoke-Dotnet @('build','TomasAI.IFM.UI.Rust.Bridge.Net','-c','Release','-v:minimal') (Join-Path $destination 'bridge-build.log')}
 if($BridgeAssemblyRoot){$env:IFM_BRIDGE_BENCHMARK_ROOT=(Resolve-Path -LiteralPath $BridgeAssemblyRoot).Path}
 $ErrorActionPreference='Continue'
 Invoke-Dotnet @('run','-c','Release','--project','TomasAI.IFM.UI.Rust.Benchmarks','--','--filter',$Filter,'--artifacts',(Join-Path $destination 'benchmarkdotnet')) (Join-Path $destination 'benchmarks.log')
 Invoke-Dotnet @('build','TomasAI.IFM.UI.Rust.Benchmarks','-c','Release','-o',(Join-Path $destination 'probe'),'-v:minimal') (Join-Path $destination 'probe-build.log')
 foreach($gc in @('0','1')) {
  $env:DOTNET_gcServer=$gc
  Invoke-Dotnet @((Join-Path $destination 'probe/TomasAI.IFM.UI.Rust.Benchmarks.dll'),'--probe',(Join-Path $destination "probe-gc$gc.json")) (Join-Path $destination "probe-gc$gc.log")
 }
} finally {$env:IFM_BRIDGE_BENCHMARK_ROOT=$oldRoot;$env:DOTNET_gcServer=$oldGc;Pop-Location}
