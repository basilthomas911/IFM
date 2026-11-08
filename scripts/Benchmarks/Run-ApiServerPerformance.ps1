[CmdletBinding()]
param(
    [string]$RunRoot = ('E:\IFM\BenchmarkArtifacts\ApiServerPerformance\' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [string[]]$Filter = @('*ApiServerPerformance*'),
    [string]$Project = 'TomasAI.IFM.Application.Api.Server.Benchmarks\TomasAI.IFM.Application.Api.Server.Benchmarks.csproj'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
if ([IO.Path]::GetPathRoot($RunRoot) -ine 'E:\') { throw 'Benchmark outputs and generated builds must stay on E:.' }
if (Test-Path -LiteralPath $RunRoot) { throw "Use a new run directory to preserve earlier results: $RunRoot" }
$projectFile = [IO.Path]::GetFullPath((Join-Path $repository $Project))
if (-not $projectFile.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $projectFile)) {
    throw 'The benchmark project must exist inside this repository.'
}
$development = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'IFMServerManager.exe' -or $_.Name -eq 'TomasAI.IFM.Application.Api.Server.exe' -or
    $_.CommandLine -match '(?i)(IFMServerManager|TomasAI\.IFM\.Application\.Api\.Server)\.dll(?:\s|"|$)' -or
    $_.Name -match '(?i)^(TomasAI\.IFM\.UI\.Rust\.eGui|ifm-marketdata-host)\.exe$'
})
if ($development.Count) { throw 'Stop Development before acceptance timings; this runner does not stop it implicitly.' }
$qualification = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'testhost.exe' -and $_.CommandLine -like '*ApiServerPerformance*'
})
if ($qualification.Count) { throw 'Wait for API performance qualification tests to finish before acceptance timings.' }

New-Item -ItemType Directory -Path $RunRoot | Out-Null
& (Join-Path $repository 'scripts\Build\Initialize-DotNetEnvironment.ps1')
$oldBuild = $env:IFM_API_PERF_BUILD_ROOT
$oldProject = $env:IFM_API_PERF_PROJECT
try {
    $env:IFM_API_PERF_BUILD_ROOT = Join-Path $RunRoot 'generated'
    $env:IFM_API_PERF_PROJECT = $projectFile
    $revision = & git -C $repository rev-parse HEAD
    & git -C $repository diff --binary HEAD | Set-Content -LiteralPath (Join-Path $RunRoot 'working-tree.patch')
    & git -C $repository status --short | Set-Content -LiteralPath (Join-Path $RunRoot 'working-tree-status.txt')
    & dotnet --info | Set-Content -LiteralPath (Join-Path $RunRoot 'dotnet-environment.txt')
    $sources = @(& git -C $repository ls-files --cached --others --exclude-standard | Where-Object { $_ -match '\.(cs|csproj|props|targets|json|ps1)$' } | ForEach-Object {
        $file = Join-Path $repository $_
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            [ordered]@{ Path = $_; Sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
        }
    })
    [ordered]@{ Revision = $revision; StartedUtc = [DateTime]::UtcNow; Project = $projectFile; Filter = $Filter; Sources = $sources } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $RunRoot 'provenance.json')
    Write-Host 'Building with at most two MSBuild nodes; benchmark groups build sequentially.'
    & dotnet build $projectFile -c Release --artifacts-path (Join-Path $RunRoot 'build') /p:DatabentoNativeImplementation=Rust /p:DatabentoNativeConfiguration=Debug /m:2 *> (Join-Path $RunRoot 'build.log')
    if ($LASTEXITCODE) { throw "Benchmark build failed; see $RunRoot\build.log" }
    $name = [IO.Path]::GetFileNameWithoutExtension($projectFile)
    $binary = Join-Path $RunRoot "build\bin\$name\release\$name.dll"
    & dotnet $binary --filter @Filter --artifacts (Join-Path $RunRoot 'paired') --exporters json --buildTimeout 600 |
        Tee-Object -FilePath (Join-Path $RunRoot 'benchmark-console.log')
    if ($LASTEXITCODE) { throw "Benchmark process failed with exit code $LASTEXITCODE." }
    $reports = @(Get-ChildItem -LiteralPath (Join-Path $RunRoot 'paired\results') -Filter '*-report-full*.json' -File)
    if (-not $reports.Count) { throw 'BenchmarkDotNet produced no raw measurements; an exit code of zero is insufficient.' }
    $rows = @($reports | ForEach-Object {
        $report = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        foreach ($benchmark in $report.Benchmarks) {
            if ($null -eq $benchmark.Statistics) { throw "A benchmark did not execute: $($benchmark.FullName)" }
            $benchmark
        }
    })
    if (-not $rows.Count) { throw 'No benchmark cases executed.' }
    $rows | Select-Object FullName, Parameters, @{n='MeanNanoseconds';e={$_.Statistics.Mean}},
        @{n='AllocatedBytesPerOperation';e={$_.Memory.BytesAllocatedPerOperation}},
        @{n='Gen0Per1000Operations';e={($_.Metrics | Where-Object {$_.Descriptor.Id -eq 'Gen0Collects'}).Value}},
        @{n='Gen1Per1000Operations';e={($_.Metrics | Where-Object {$_.Descriptor.Id -eq 'Gen1Collects'}).Value}},
        @{n='Gen2Per1000Operations';e={($_.Metrics | Where-Object {$_.Descriptor.Id -eq 'Gen2Collects'}).Value}},
        @{n='MonitorContentionsPerOperation';e={($_.Metrics | Where-Object {$_.Descriptor.Id -eq 'LockContentionCount'}).Value}},
        @{n='ThreadPoolCompletionsPerOperation';e={($_.Metrics | Where-Object {$_.Descriptor.Id -eq 'CompletedWorkItemCount'}).Value}} |
        Export-Csv -LiteralPath (Join-Path $RunRoot 'measurement-index.csv') -NoTypeInformation
    Write-Host "Verified $($rows.Count) measured cases. Raw allocation, GC and threading evidence: $RunRoot\paired\results"
}
finally {
    $env:IFM_API_PERF_BUILD_ROOT = $oldBuild
    $env:IFM_API_PERF_PROJECT = $oldProject
}
