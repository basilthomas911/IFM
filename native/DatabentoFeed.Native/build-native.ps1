param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$RunTests,
    [switch]$EnableLive,
    [string]$VcpkgRoot
)

$ErrorActionPreference = 'Stop'
$sourceDirectory = $PSScriptRoot
$buildDirectory = if ($EnableLive) {
    Join-Path $sourceDirectory 'out\live-build'
} else {
    Join-Path $sourceDirectory 'out\build'
}
$cmake = Get-Command cmake -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
$visualStudioInstallation = $null

if (-not $cmake) {
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $visualStudioInstallation = & $vswhere -latest -products * -property installationPath
        if ($visualStudioInstallation) {
            $candidate = Join-Path $visualStudioInstallation 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
            if (Test-Path -LiteralPath $candidate) {
                $cmake = $candidate
            }
        }
    }
}

if (-not $cmake) {
    throw 'CMake 3.24 or later was not found on PATH or in the latest Visual Studio installation.'
}

$liveValue = if ($EnableLive) { 'ON' } else { 'OFF' }
$configureArguments = @(
    '-S', $sourceDirectory,
    '-B', $buildDirectory,
    "-DIFM_DATABENTO_ENABLE_LIVE=$liveValue",
    '-DIFM_DATABENTO_BUILD_TESTS=ON'
)

if ($EnableLive) {
    if (-not $VcpkgRoot) {
        $visualStudioCandidates = @($visualStudioInstallation)
        $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path -LiteralPath $vswhere) {
            $visualStudioCandidates += @(
                & $vswhere -products * -property installationPath
            )
        }
        foreach ($candidateInstallation in $visualStudioCandidates) {
            if ($candidateInstallation) {
                $candidateVcpkg = Join-Path $candidateInstallation 'VC\vcpkg'
                if (Test-Path -LiteralPath (Join-Path $candidateVcpkg 'scripts\buildsystems\vcpkg.cmake')) {
                    $VcpkgRoot = $candidateVcpkg
                    break
                }
            }
        }
    }
    if (-not $VcpkgRoot -and $env:VCPKG_ROOT) {
        $VcpkgRoot = $env:VCPKG_ROOT
    }
    if (-not $VcpkgRoot) {
        throw 'A vcpkg installation is required for the live Databento build. Pass -VcpkgRoot or set VCPKG_ROOT.'
    }
    $toolchain = Join-Path $VcpkgRoot 'scripts\buildsystems\vcpkg.cmake'
    if (-not (Test-Path -LiteralPath $toolchain)) {
        throw "The vcpkg toolchain was not found at '$toolchain'."
    }
    $vcpkgCacheRoot = Join-Path $sourceDirectory 'out\vcpkg-cache'
    $registryCache = Join-Path $vcpkgCacheRoot 'registries'
    $downloadCache = Join-Path $vcpkgCacheRoot 'downloads'
    $binaryCache = Join-Path $vcpkgCacheRoot 'binaries'
    New-Item -ItemType Directory -Force -Path $registryCache, $downloadCache, $binaryCache | Out-Null
    if (-not $env:X_VCPKG_REGISTRIES_CACHE) {
        $env:X_VCPKG_REGISTRIES_CACHE = $registryCache
    }
    if (-not $env:VCPKG_DOWNLOADS) {
        $env:VCPKG_DOWNLOADS = $downloadCache
    }
    if (-not $env:VCPKG_DEFAULT_BINARY_CACHE) {
        $env:VCPKG_DEFAULT_BINARY_CACHE = $binaryCache
    }
    $configureArguments += "-DCMAKE_TOOLCHAIN_FILE=$toolchain"
    $vcpkgInstalledDirectory = Join-Path $sourceDirectory 'out\vcpkg-installed'
    $configureArguments += "-DVCPKG_INSTALLED_DIR=$vcpkgInstalledDirectory"
}

& $cmake @configureArguments
if ($LASTEXITCODE -ne 0) {
    throw "Native CMake configuration failed with exit code $LASTEXITCODE."
}

& $cmake --build $buildDirectory --config $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Native build failed with exit code $LASTEXITCODE."
}

if ($EnableLive) {
    $runtimeDirectory = Join-Path $vcpkgInstalledDirectory 'x64-windows\bin'
    $nativeOutputDirectory = Join-Path $buildDirectory $Configuration
    if (Test-Path -LiteralPath $runtimeDirectory) {
        Get-ChildItem -LiteralPath $runtimeDirectory -Filter '*.dll' | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $nativeOutputDirectory -Force
        }
    }
}

if ($RunTests) {
    $ctest = Join-Path (Split-Path -Parent $cmake) 'ctest.exe'
    & $ctest --test-dir $buildDirectory -C $Configuration --output-on-failure --timeout 30
    if ($LASTEXITCODE -ne 0) {
        throw "Native tests failed with exit code $LASTEXITCODE."
    }
}
