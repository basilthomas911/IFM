[CmdletBinding()]
param([string]$OutputRoot = '.artifacts/postgres-version-benchmarks',
    [ValidateSet('17.2','17.11','18.6')][string[]]$Versions = @('17.2','17.11','18.6'))
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$runRoot = Join-Path $repoRoot (Join-Path $OutputRoot $runId)
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

$containers = @()
$volumes = @()
$oldEnvironment = @{}
foreach ($key in @('POSTGRES_PASSWORD','IFM_POSTGRES_BENCHMARK_PASSWORD','IFM_POSTGRES_BENCHMARK_VERSION','IFM_POSTGRES_BENCHMARK_PORT')) {
    $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}
$env:POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:IFM_POSTGRES_BENCHMARK_PASSWORD = $env:POSTGRES_PASSWORD
function Invoke-Docker([string[]]$DockerArguments) {
    $result = & docker @DockerArguments
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed: $($DockerArguments[0])" }
    return $result
}
try {
    Push-Location $repoRoot
    & dotnet build TomasAI.IFM.Application.Storage.Benchmarks -c Release --no-restore *> (Join-Path $runRoot 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Benchmark build failed; inspect $runRoot/build.log" }
    $metadata = @()
    for ($i = 0; $i -lt $versions.Count; $i++) {
        $version = $versions[$i]
        $image = "postgres:$version-bookworm"
        $name = "ifm-pgbench-$runId-$($version.Replace('.','-'))"
        $volume = "$name-data"
        $port = 56417 + $i
        Invoke-Docker @('volume','create','--label',"ifm.postgres.benchmark=$runId",$volume) | Out-Null
        $volumes += $volume
        Invoke-Docker @('create','--name',$name,'--label',"ifm.postgres.benchmark=$runId",'--cpus','4','--memory','4g','--shm-size','512m',
            '-e','POSTGRES_PASSWORD','-e','POSTGRES_USER=benchmark','-e','POSTGRES_DB=ifm_pg_benchmark','-e','PGDATA=/benchmark-data/pgdata',
            '-p',"127.0.0.1:${port}:5432",'-v',"${volume}:/benchmark-data",$image,
            '-c','shared_buffers=256MB','-c','effective_cache_size=2GB','-c','work_mem=16MB',
            '-c','fsync=on','-c','synchronous_commit=on','-c','full_page_writes=on','-c','max_wal_size=2GB','-c','checkpoint_timeout=15min') | Out-Null
        $containers += $name
        Invoke-Docker @('start',$name) | Out-Null
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            & docker exec $name pg_isready -U benchmark -d ifm_pg_benchmark *> $null
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) { throw "Benchmark container $name did not become ready." }
        $serverVersion = Invoke-Docker @('exec',$name,'postgres','--version')
        $serverSettings = Invoke-Docker @('exec',$name,'psql','-U','benchmark','-d','ifm_pg_benchmark','-At','-c',
            "SELECT json_object_agg(name,setting) FROM pg_settings WHERE name IN ('server_version','fsync','synchronous_commit','full_page_writes','shared_buffers','effective_cache_size','work_mem','max_wal_size','checkpoint_timeout','autovacuum','jit','io_method')")
        $imageInfo = (Invoke-Docker @('image','inspect',$image,'--format','{{json .RepoDigests}}')) -join ''
        $metadata += [pscustomobject]@{Version=$version;Image=$image;Digest=($imageInfo|ConvertFrom-Json);ServerVersion=($serverVersion -join '');Settings=($serverSettings|ConvertFrom-Json);CpuLimit=4;MemoryBytes=4294967296;Port=$port;Volume=$volume}
        $metadata | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $runRoot 'metadata.json')
        $env:IFM_POSTGRES_BENCHMARK_VERSION = $version
        $env:IFM_POSTGRES_BENCHMARK_PORT = "$port"
        $versionRoot = Join-Path $runRoot $version
        New-Item -ItemType Directory -Path $versionRoot -Force | Out-Null
        Write-Host "Benchmarking PostgreSQL $version; other benchmark servers are stopped."
        $process = Start-Process dotnet -ArgumentList @('run','--project','TomasAI.IFM.Application.Storage.Benchmarks','-c','Release','--no-build','--',
            '--filter','*PostgresVersionBenchmarks*','--artifacts',$versionRoot,'--exporters','json','csv') -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $versionRoot 'stdout.log') -RedirectStandardError (Join-Path $versionRoot 'stderr.log')
        # Retain the process handle so Windows PowerShell 5 can read ExitCode after polling HasExited.
        $benchmarkProcessHandle = $process.Handle
        $samples = [Collections.Generic.List[object]]::new()
        while (-not $process.HasExited) {
            $stats = (Invoke-Docker @('stats',$name,'--no-stream','--format','{{json .}}')) -join ''
            $samples.Add([pscustomobject]@{Utc=[DateTime]::UtcNow.ToString('O');Stats=($stats|ConvertFrom-Json)})
            Start-Sleep -Seconds 5
            $process.Refresh()
        }
        $process.WaitForExit()
        $samples | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $versionRoot 'docker-stats.json')
        # PostgreSQL writes normal server messages to stderr; avoid PowerShell 5 treating them as terminating errors.
        $logProcess = Start-Process docker -ArgumentList @('logs',$name) -WindowStyle Hidden -Wait -PassThru `
            -RedirectStandardOutput (Join-Path $versionRoot 'postgres.stdout.log') -RedirectStandardError (Join-Path $versionRoot 'postgres.stderr.log')
        if ($logProcess.ExitCode -ne 0) { throw 'Could not collect benchmark server logs.' }
        if ($process.ExitCode -ne 0) { throw "Benchmark process for $version exited with $($process.ExitCode)." }
        if (-not (Test-Path (Join-Path $versionRoot 'results/TomasAI.IFM.Application.Storage.Benchmarks.PostgresVersionBenchmarks-report-full-compressed.json'))) {
            throw "Benchmark report is missing for $version; inspect stdout.log."
        }
        $reportPath = Join-Path $versionRoot 'results/TomasAI.IFM.Application.Storage.Benchmarks.PostgresVersionBenchmarks-report-full-compressed.json'
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if ($report.Benchmarks.Count -ne 5 -or @($report.Benchmarks | Where-Object { $null -eq $_.Statistics -or $_.Statistics.N -lt 6 }).Count -ne 0) {
            throw "One or more workloads produced missing or insufficient measurements for $version."
        }
        Invoke-Docker @('stop',$name) | Out-Null
    }
    $summary = foreach ($version in $versions) {
        $reportPath = Join-Path $runRoot "$version/results/TomasAI.IFM.Application.Storage.Benchmarks.PostgresVersionBenchmarks-report-full-compressed.json"
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        foreach ($result in $report.Benchmarks) {
            [pscustomobject]@{Version=$version;Method=$result.Method;MeanMilliseconds=$result.Statistics.Mean/1e6;
                ErrorMilliseconds=$result.Statistics.ConfidenceInterval.Margin/1e6;
                StandardDeviationMilliseconds=$result.Statistics.StandardDeviation/1e6;RetainedIterations=$result.Statistics.N}
        }
    }
    $summary | Export-Csv -NoTypeInformation (Join-Path $runRoot 'summary.csv')
    Write-Host "All requested versions completed. Results: $runRoot"
}
finally {
    foreach ($name in $containers) {
        $inspection = (& docker inspect $name | ConvertFrom-Json)[0]
        $label = $inspection.Config.Labels.'ifm.postgres.benchmark'
        if ($LASTEXITCODE -eq 0 -and $label -eq $runId) { & docker rm -f -v $name *> $null }
    }
    foreach ($volume in $volumes) {
        $inspection = (& docker volume inspect $volume | ConvertFrom-Json)[0]
        $label = $inspection.Labels.'ifm.postgres.benchmark'
        if ($LASTEXITCODE -eq 0 -and $label -eq $runId) { & docker volume rm $volume *> $null }
    }
    foreach ($entry in $oldEnvironment.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value,'Process') }
    Pop-Location
}
