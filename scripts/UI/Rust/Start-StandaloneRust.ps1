param(
 [Parameter(Mandatory=$true)][string]$ConfigPath,
 [string]$Executable='.artifacts/standalone-rust-ui/app/TomasAI.IFM.UI.Rust.eGui.exe'
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$configurationFile=(Resolve-Path -LiteralPath $ConfigPath).Path
$executablePath=if([IO.Path]::IsPathRooted($Executable)){$Executable}else{Join-Path $repoRoot $Executable}
$exe=(Resolve-Path -LiteralPath $executablePath).Path
if(-not [IO.File]::Exists($configurationFile)){throw 'Select an existing UI client JSON profile'}
if(-not [IO.File]::Exists($exe)){throw 'Build the standalone executable first'}
# The profile is data only. Do not launch or attach the managed bridge, and do
# not change ServerManager's default UI. The child's native loader validates it.
$prior=$env:IFM_UI_CONFIG_PATH
try {
 $env:IFM_UI_CONFIG_PATH=$configurationFile
 $child=Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) -PassThru
 Write-Output ('Standalone UI started, PID '+$child.Id)
} finally {$env:IFM_UI_CONFIG_PATH=$prior}
