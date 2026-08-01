param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$sourceDirectory = $PSScriptRoot
$buildDirectory = Join-Path $sourceDirectory 'out\build'
$cmake = Get-Command cmake -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source

if (-not $cmake) {
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $installation = & $vswhere -latest -products * -property installationPath
        if ($installation) {
            $candidate = Join-Path $installation 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
            if (Test-Path -LiteralPath $candidate) {
                $cmake = $candidate
            }
        }
    }
}

if (-not $cmake) {
    throw 'CMake 3.24 or later was not found on PATH or in the latest Visual Studio installation.'
}

& $cmake -S $sourceDirectory -B $buildDirectory `
    -DIFM_DATABENTO_ENABLE_LIVE=OFF `
    -DIFM_DATABENTO_BUILD_TESTS=ON
if ($LASTEXITCODE -ne 0) {
    throw "Native CMake configuration failed with exit code $LASTEXITCODE."
}

& $cmake --build $buildDirectory --config $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Native build failed with exit code $LASTEXITCODE."
}

if ($RunTests) {
    $ctest = Join-Path (Split-Path -Parent $cmake) 'ctest.exe'
    & $ctest --test-dir $buildDirectory -C $Configuration --output-on-failure --timeout 30
    if ($LASTEXITCODE -ne 0) {
        throw "Native tests failed with exit code $LASTEXITCODE."
    }
}
