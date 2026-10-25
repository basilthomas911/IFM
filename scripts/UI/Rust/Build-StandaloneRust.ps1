param([switch]$DebugBuild,[string]$OutputDirectory='.artifacts/standalone-rust-ui/app')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Push-Location $repoRoot
$priorTargetFlags = $env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS
try {
    # Separate target output leaves ordinary tests and the existing hybrid UI
    # untouched. Static CRT avoids shipping the Visual C++ runtime separately.
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS = ($priorTargetFlags + ' -C target-feature=+crt-static').Trim()
    $cargoArguments = @('build', '--jobs', '1', '--locked', '--target', 'x86_64-pc-windows-msvc', '--manifest-path', 'TomasAI.IFM.UI.Rust.eGui/Cargo.toml')
    if (-not $DebugBuild) { $cargoArguments += '--release' }
    $ErrorActionPreference = 'Continue'
    & cargo @cargoArguments 2>&1 | ForEach-Object { $_.ToString() }
    $nativeExitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($nativeExitCode -ne 0) { throw 'Standalone Rust build failed' }
    $profile = if ($DebugBuild) { 'debug' } else { 'release' }
    $output = if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))}
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # Rust crate names cannot contain dots. Assign the requested Windows artifact
    # name when packaging; no managed assemblies or config secrets are copied.
    Copy-Item -LiteralPath "TomasAI.IFM.UI.Rust.eGui/target/x86_64-pc-windows-msvc/$profile/ifm-ui-egui.exe" -Destination (Join-Path $output 'TomasAI.IFM.UI.Rust.eGui.exe')
    & python 'TomasAI.IFM.UI.Rust.eGui/tools/audit_package.py' (Join-Path $output 'TomasAI.IFM.UI.Rust.eGui.exe') (Join-Path $output 'package-audit.json')
    if ($LASTEXITCODE -ne 0) { throw 'Standalone executable dependency audit failed' }
    Write-Output (Join-Path $output 'TomasAI.IFM.UI.Rust.eGui.exe')
} finally {
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS = $priorTargetFlags
    Pop-Location
}
