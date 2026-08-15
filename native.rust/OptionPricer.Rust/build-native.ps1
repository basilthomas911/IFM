param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$RunTests
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
$target = 'x86_64-pc-windows-msvc'
$manifestPath = Join-Path $crateDirectory 'Cargo.toml'

if ($RunTests) {
    $testArguments = @('test', '--manifest-path', $manifestPath, '--target', $target) + $configurationArgument
    & $cargo @testArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Rust native tests failed with exit code $LASTEXITCODE."
    }
}

$buildArguments = @('build', '--manifest-path', $manifestPath, '--target', $target) + $configurationArgument
& $cargo @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Rust native build failed with exit code $LASTEXITCODE."
}

$cargoProfile = if ($Configuration -eq 'Release') { 'release' } else { 'debug' }
$sourceDll = Join-Path $crateDirectory "target\$target\$cargoProfile\ifm_option_pricer_native.dll"
$outputDirectory = Join-Path $crateDirectory "out\build\$Configuration"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $outputDirectory 'ifm_option_pricer_native.dll') -Force
Copy-Item -LiteralPath (Join-Path $crateDirectory 'include\ifm_option_pricer_native.h') -Destination $outputDirectory -Force
Write-Output (Join-Path $outputDirectory 'ifm_option_pricer_native.dll')
