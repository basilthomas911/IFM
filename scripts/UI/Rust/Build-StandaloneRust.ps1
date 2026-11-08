param(
    [switch]$DebugBuild,
    [ValidateSet('Development','Release')][string]$Profile='Development',
    [ValidateRange(1,64)][int]$Jobs=4,
    [string]$OutputDirectory='.artifacts/standalone-rust-ui/app'
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
& (Join-Path $repoRoot 'scripts/Development/Initialize-RustBuildTarget.ps1') -ProjectDirectory (Join-Path $repoRoot 'TomasAI.IFM.UI.Rust.eGui')
Push-Location $repoRoot
$priorTargetFlags = $env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS
try {
    # Separate target output leaves ordinary tests and the existing hybrid UI
    # untouched. Static CRT avoids shipping the Visual C++ runtime separately.
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS = ($priorTargetFlags + ' -C target-feature=+crt-static').Trim()
    if ($DebugBuild -and $PSBoundParameters.ContainsKey('Profile')) { throw 'Choose either -DebugBuild or -Profile, not both.' }
    $profileDirectory = if ($DebugBuild) { 'debug' } elseif ($Profile -eq 'Development') { 'development' } else { 'release' }
    Write-Host "Building standalone Rust UI: $profileDirectory profile, up to $Jobs parallel compiler jobs."
    $cargoArguments = @('build', '--jobs', [string]$Jobs, '--locked', '--target', 'x86_64-pc-windows-msvc', '--manifest-path', 'TomasAI.IFM.UI.Rust.eGui/Cargo.toml')
    if ($profileDirectory -eq 'development') { $cargoArguments += @('--profile', 'development') }
    elseif ($profileDirectory -eq 'release') { $cargoArguments += '--release' }
    $ErrorActionPreference = 'Continue'
    & cargo @cargoArguments 2>&1 | ForEach-Object { $_.ToString() }
    $nativeExitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($nativeExitCode -ne 0) { throw 'Standalone Rust build failed' }
    $output = if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # Rust crate names cannot contain dots. Assign the requested Windows artifact
    # name when packaging; no managed assemblies or config secrets are copied.
    Copy-Item -LiteralPath "TomasAI.IFM.UI.Rust.eGui/target/x86_64-pc-windows-msvc/$profileDirectory/ifm-ui-egui.exe" -Destination (Join-Path $output 'TomasAI.IFM.UI.Rust.eGui.exe')
    $sourceSymbols = "TomasAI.IFM.UI.Rust.eGui/target/x86_64-pc-windows-msvc/$profileDirectory/ifm_ui_egui.pdb"
    if (!(Test-Path -LiteralPath $sourceSymbols)) { throw 'Full-debug Rust UI PDB is missing.' }
    Copy-Item -LiteralPath $sourceSymbols -Destination (Join-Path $output 'ifm_ui_egui.pdb')
    & python 'TomasAI.IFM.UI.Rust.eGui/tools/audit_package.py' (Join-Path $output 'TomasAI.IFM.UI.Rust.eGui.exe') (Join-Path $output 'package-audit.json')
    if ($LASTEXITCODE -ne 0) { throw 'Standalone executable dependency audit failed' }
    Write-Output (Join-Path $output 'TomasAI.IFM.UI.Rust.eGui.exe')
} finally {
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS = $priorTargetFlags
    Pop-Location
}
