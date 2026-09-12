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

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer discovery tool vswhere.exe was not found.'
}

$visualStudioInstallation = & $vswhere -latest `
    -products Microsoft.VisualStudio.Product.Community `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath
$visualStudioVersion = & $vswhere -latest `
    -products Microsoft.VisualStudio.Product.Community `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationVersion
if (-not $visualStudioInstallation -or -not $visualStudioVersion -or
    $visualStudioVersion.Split('.')[0] -ne '18') {
    throw 'Visual Studio Community 2026 with Desktop development with C++ was not found.'
}

$msvcTools = Get-ChildItem -LiteralPath (Join-Path $visualStudioInstallation 'VC\Tools\MSVC') `
        -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin\Hostx64\x64\link.exe') } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $msvcTools) {
    throw "The x64 MSVC linker was not found in Visual Studio Community 2026 at '$visualStudioInstallation'."
}

$windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$windowsSdk = Get-ChildItem -LiteralPath (Join-Path $windowsKitsRoot 'Lib') -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'um\x64\kernel32.lib') } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $windowsSdk) {
    throw 'A Windows SDK containing the x64 kernel32 import library was not found.'
}

$sdkVersion = $windowsSdk.Name
$msvcBin = Join-Path $msvcTools.FullName 'bin\Hostx64\x64'
$env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_LINKER = Join-Path $msvcBin 'link.exe'
$env:PATH = $msvcBin + ';' + (Join-Path $windowsKitsRoot "bin\$sdkVersion\x64") + ';' + $env:PATH
$env:LIB = (Join-Path $msvcTools.FullName 'lib\x64') + ';' +
    (Join-Path $windowsSdk.FullName 'um\x64') + ';' +
    (Join-Path $windowsSdk.FullName 'ucrt\x64')
Write-Host "Using Visual Studio Community 2026 MSVC tools '$($msvcTools.Name)' from '$visualStudioInstallation'."

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
