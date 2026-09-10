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

# Visual Studio's lightweight C++ SDK contains the linker, runtime libraries and
# headers required by the MSVC Rust target even when the full Desktop C++ workload
# is not installed. Configure it as a deterministic fallback for developer hosts.
if (-not (Get-Command link.exe -ErrorAction SilentlyContinue)) {
    $scopeCppRoot = Join-Path $env:ProgramFiles 'Microsoft Visual Studio\18\Community\SDK\ScopeCppSDK\vc15\VC'
    $scopeLinker = Join-Path $scopeCppRoot 'bin\link.exe'
    $windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
    $windowsSdk = Get-ChildItem -LiteralPath (Join-Path $windowsKitsRoot 'Lib') -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'um\x64\kernel32.lib') } |
        Sort-Object { [version]$_.Name } -Descending |
        Select-Object -First 1
    if ((Test-Path -LiteralPath $scopeLinker) -and $windowsSdk) {
        $sdkVersion = $windowsSdk.Name
        $sdkInclude = Join-Path $windowsKitsRoot "Include\$sdkVersion"
        $env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_LINKER = $scopeLinker
        $env:PATH = (Join-Path $scopeCppRoot 'bin') + ';' +
            (Join-Path $windowsKitsRoot "bin\$sdkVersion\x64") + ';' + $env:PATH
        $env:LIB = (Join-Path $scopeCppRoot 'lib') + ';' +
            (Join-Path $windowsSdk.FullName 'um\x64') + ';' +
            (Join-Path $windowsSdk.FullName 'ucrt\x64')
        $env:INCLUDE = (Join-Path $scopeCppRoot 'include') + ';' +
            (Join-Path $sdkInclude 'ucrt') + ';' +
            (Join-Path $sdkInclude 'shared') + ';' +
            (Join-Path $sdkInclude 'um') + ';' +
            (Join-Path $sdkInclude 'winrt')
    }
}

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
