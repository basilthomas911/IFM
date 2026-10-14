param([switch]$NoBuild)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
& "$repoRoot/.vscode/Wait-IFMDevelopmentApi.ps1"
if($LASTEXITCODE -ne 0){throw 'Start the Development API before the two UI clients'}
Push-Location $repoRoot
try {
 if(-not $NoBuild){
  & "$PSScriptRoot/Build-RustUi.ps1" -Release
  dotnet build 'TomasAI.IFM.UI.Net/TomasAI.IFM.UI.Net.csproj' -c Release
  if($LASTEXITCODE -ne 0){throw 'WinForms build failed'}
 }
 $winforms=Join-Path $repoRoot 'TomasAI.IFM.UI.Net/bin/Release/net10.0-windows10.0.17763.0/TomasAI.IFM.UI.Net.exe'
 $rust=Join-Path $repoRoot '.artifacts/rust-ui/app/ifm-ui-rust.exe'
 foreach($exe in @($winforms,$rust)){if(-not(Test-Path -LiteralPath $exe)){throw "Build the UI before launching: $exe"}}
 $previousEnvironment=$env:DOTNET_ENVIRONMENT;$previousMode=$env:IFM_RUST_UI_MODE
 try {
  $env:DOTNET_ENVIRONMENT='Development';$env:IFM_RUST_UI_MODE='live'
  # These are the explicitly requested interactive UI windows, not background helpers.
  $wf=Start-Process -FilePath $winforms -WorkingDirectory (Split-Path $winforms) -WindowStyle Normal -PassThru
  $native=Start-Process -FilePath $rust -WorkingDirectory (Split-Path $rust) -WindowStyle Normal -PassThru
  [pscustomobject]@{StartedAtUtc=[DateTimeOffset]::UtcNow;WinFormsProcessId=$wf.Id;RustProcessId=$native.Id;Backend='http://localhost:22543';Environment='Development'} |
   ConvertTo-Json | Set-Content '.artifacts/rust-ui/development-ui-processes.json' -Encoding UTF8
  Write-Output "Started WinForms PID $($wf.Id) and Rust live PID $($native.Id). Both clients use the running Development backend."
 }finally{$env:DOTNET_ENVIRONMENT=$previousEnvironment;$env:IFM_RUST_UI_MODE=$previousMode}
}finally{Pop-Location}
