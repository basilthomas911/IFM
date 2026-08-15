param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$RunTests,
    [switch]$EnableLive
)

$ErrorActionPreference = 'Stop'
$crateDirectory = $PSScriptRoot
$cargo = Join-Path $env:USERPROFILE '.cargo\bin\cargo.exe'
if (-not (Test-Path -LiteralPath $cargo)) {
    $cargoCommand = Get-Command cargo -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
    if (-not $cargoCommand) {
        throw 'Cargo was not found. Install the stable x86_64-pc-windows-msvc Rust toolchain.'
    }
    $cargo = $cargoCommand
}

$configurationArgument = if ($Configuration -eq 'Release') { @('--release') } else { @() }
$featureArguments = if ($EnableLive) { @('--features', 'live') } else { @() }
$target = 'x86_64-pc-windows-msvc'
$manifestPath = Join-Path $crateDirectory 'Cargo.toml'

if ($RunTests) {
    $testArguments = @('test', '--manifest-path', $manifestPath, '--target', $target) + $configurationArgument + $featureArguments
    & $cargo @testArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Rust native tests failed with exit code $LASTEXITCODE."
    }
}

$buildArguments = @('build', '--manifest-path', $manifestPath, '--target', $target) + $configurationArgument + $featureArguments
& $cargo @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Rust native build failed with exit code $LASTEXITCODE."
}

$cargoProfile = if ($Configuration -eq 'Release') { 'release' } else { 'debug' }
$sourceDll = Join-Path $crateDirectory "target\$target\$cargoProfile\databento_feed_native.dll"
$buildKind = if ($EnableLive) { 'live-build' } else { 'build' }
$outputDirectory = Join-Path $crateDirectory "out\$buildKind\$Configuration"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $outputDirectory 'databento_feed_native.dll') -Force
Write-Output (Join-Path $outputDirectory 'databento_feed_native.dll')
