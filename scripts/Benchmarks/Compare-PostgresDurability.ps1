[CmdletBinding()]
param(
    [ValidateRange(1,3600)][int]$LoadSeconds = 120,
    [ValidateSet('AtomicDurable','AtomicAsyncCommit','AtomicNoFsync','AtomicNoFsyncAsyncCommit','SplitSameInstanceAsyncProjection','SplitSeparateUnsafeProjection')]
    [string[]]$Profiles = @('AtomicDurable','AtomicAsyncCommit','AtomicNoFsync','AtomicNoFsyncAsyncCommit','SplitSameInstanceAsyncProjection','SplitSeparateUnsafeProjection'),
    [ValidateSet('BusinessOnly','PlansOnly','PlansAndBusiness','MixedWithReads')]
    [string[]]$Modes = @('BusinessOnly','PlansOnly','PlansAndBusiness','MixedWithReads'),
    [ValidateRange(1,32)][int]$Writers = 4,
    [ValidateRange(1,10000)][int]$PlanRate = 5,
    [ValidateRange(1,10000)][int]$BusinessRate = 1,
    [ValidateRange(1,10000)][int]$ReadRate = 2,
    [string]$OutputRoot = '.artifacts/postgres-durability'
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runId = [Guid]::NewGuid().ToString('N').Substring(0,12)
$runRoot = Join-Path $repoRoot (Join-Path $OutputRoot $runId)
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$ownedContainers = @()
$ownedVolumes = @()
$telemetryProcess = $null
$telemetryStop = $null
$keys = @('POSTGRES_PASSWORD','POSTGRES_DEV_KEY','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','IFM_PLAN_BENCHMARK_CONNECTION','IFM_PLAN_BENCHMARK_PROJECTION_CONNECTION','IFM_PLAN_BENCHMARK_PROFILE')
$oldEnvironment = @{}
foreach ($key in $keys) { $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key,'Process') }
$env:POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
$env:POSTGRES_DEV_KEY = @{userid='benchmark';password=$env:POSTGRES_PASSWORD} | ConvertTo-Json -Compress
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
function Invoke-BenchmarkDocker([string[]]$Arguments) {
    $output = & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker operation failed: $($Arguments[0])" }
    return $output
}
function New-BenchmarkInstance([string]$Suffix,[int]$Port,[string]$Fsync,[string]$Sync) {
    $name = "ifm-durability-$runId-$Suffix"
    $volume = "$name-data"
    Invoke-BenchmarkDocker @('volume','create','--label',"ifm.durability.benchmark=$runId",$volume) | Out-Null
    $script:ownedVolumes += $volume
    Invoke-BenchmarkDocker @('create','--name',$name,'--label',"ifm.durability.benchmark=$runId",'--cpus','4','--memory','4g','--shm-size','512m',
        '-e','POSTGRES_PASSWORD','-e','POSTGRES_USER=benchmark','-e','POSTGRES_DB=ifm_durability_benchmark','-e','PGDATA=/benchmark-data/pgdata',
        '-p',"127.0.0.1:${Port}:5432",'-v',"${volume}:/benchmark-data",'postgres:17.2',
        '-c',"fsync=$Fsync",'-c',"synchronous_commit=$Sync",'-c','full_page_writes=on',
        '-c','shared_buffers=128MB','-c','max_connections=100','-c','max_wal_size=1GB','-c','track_io_timing=on','-c','track_wal_io_timing=on') | Out-Null
    $script:ownedContainers += $name
    Invoke-BenchmarkDocker @('start',$name) | Out-Null
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        & docker exec $name pg_isready -U benchmark -d ifm_durability_benchmark *> $null
        if ($LASTEXITCODE -eq 0) { $ready=$true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'Dedicated benchmark instance failed readiness.' }
    return $name
}
try {
    Push-Location $repoRoot
    & dotnet build TomasAI.IFM.Application.Storage.Benchmarks -c Release --no-restore *> (Join-Path $runRoot 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Benchmark build failed.' }
    $metadata = @()
    foreach ($profile in $Profiles) {
        $profileRoot = Join-Path $runRoot $profile
        New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null
        $fsync = if ($profile -in @('AtomicNoFsync','AtomicNoFsyncAsyncCommit')) { 'off' } else { 'on' }
        $sync = if ($profile -in @('AtomicAsyncCommit','AtomicNoFsyncAsyncCommit')) { 'off' } else { 'on' }
        $source = New-BenchmarkInstance "$profile-source" 56540 $fsync $sync
        $projection = $null
        $env:IFM_PLAN_BENCHMARK_CONNECTION = 'Host=127.0.0.1;Port=56540;Database=ifm_durability_benchmark'
        $env:IFM_PLAN_BENCHMARK_PROFILE = $profile
        $env:IFM_PLAN_BENCHMARK_PROJECTION_CONNECTION = $null
        if ($profile -eq 'SplitSeparateUnsafeProjection') {
            $projection = New-BenchmarkInstance "$profile-projection" 56541 'off' 'off'
            $env:IFM_PLAN_BENCHMARK_PROJECTION_CONNECTION = 'Host=127.0.0.1;Port=56541;Database=ifm_durability_benchmark'
        }
        $settings = Invoke-BenchmarkDocker @('exec',$source,'psql','-U','benchmark','-d','ifm_durability_benchmark','-At','-c',
            "SELECT json_object_agg(name,setting) FROM pg_settings WHERE name IN ('server_version','fsync','synchronous_commit','full_page_writes','shared_buffers','max_wal_size','checkpoint_timeout','wal_sync_method','track_io_timing','track_wal_io_timing')")
        $metadata += [pscustomobject]@{Profile=$profile;SourceSettings=($settings|ConvertFrom-Json);SeparateProjectionInstance=($null -ne $projection);ProjectionTransactionSync= $(if ($profile.StartsWith('Split')) {'off'} else {$sync});Image='postgres:17.2';CpuLimitPerInstance=4;MemoryBytesPerInstance=4294967296;LoadSecondsPerMode=$LoadSeconds;SustainedModes=$Modes;Writers=$Writers;PlanRatePerWriter=$PlanRate;BusinessRatePerWriter=$BusinessRate;ReadRatePerWriter=$ReadRate}
        $metadata | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $runRoot 'metadata.json')
        # SQL rollback probe: an inserted source row followed by a failed projection leaves neither row committed.
        $probeSql = "CREATE TABLE durability_probe(id int PRIMARY KEY);`nBEGIN;`nINSERT INTO durability_probe VALUES (1);`nINSERT INTO durability_probe VALUES (1);`nROLLBACK;`nSELECT count(*) FROM durability_probe;"
        $ErrorActionPreference = 'Continue' # The injected constraint violation is expected.
        $probe = $probeSql | & docker exec -i $source psql -U benchmark -d ifm_durability_benchmark -At 2> (Join-Path $profileRoot 'expected-rollback-error.txt')
        $probeExitCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($probeExitCode -ne 0) { throw 'Rollback probe command failed.' }
        if (($probe | Select-Object -Last 1) -ne '0') { throw 'Rollback probe failed.' }
        $probe | Set-Content (Join-Path $profileRoot 'rollback-probe.txt')
        Write-Host "Starting full BenchmarkDotNet workload: $profile"
        & dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release --no-build -- --plan-business --artifacts $profileRoot --exporters json csv *> (Join-Path $profileRoot 'benchmark.log')
        if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $profile" }
        $reports = @(Get-ChildItem (Join-Path $profileRoot 'results') -Filter '*report.csv')
        if ($reports.Count -eq 0 -or (Get-Content $reports[0].FullName -Raw) -match ',NA,') { throw "Missing measurement: $profile" }
        $telemetryStop = Join-Path $profileRoot 'telemetry.stop'
        $telemetryTargets = if ($projection) { "$source,$projection" } else { $source }
        $telemetryProcess = Start-Process python -ArgumentList @(
            (Join-Path $repoRoot 'scripts/Benchmarks/Collect-PostgresDurabilityTelemetry.py'),
            '--run-id', $runId, '--containers', $telemetryTargets,
            '--output', (Join-Path $profileRoot 'telemetry.jsonl'), '--stop-file', $telemetryStop
        ) -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $profileRoot 'telemetry-errors.log')
        $null = $telemetryProcess.Handle # Retain the process handle so Windows PowerShell can read its exit code.
        Write-Host "Starting sustained workload: $profile"
        & dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release --no-build -- --plan-business-load --seconds $LoadSeconds --writers $Writers --plan-rate $PlanRate --business-rate $BusinessRate --read-rate $ReadRate --modes ($Modes -join ',') *> (Join-Path $profileRoot 'load.log')
        if ($LASTEXITCODE -ne 0) { throw "Load run failed: $profile" }
        New-Item -ItemType File -Path $telemetryStop -Force | Out-Null
        if (-not $telemetryProcess.WaitForExit(45000)) { $telemetryProcess.Kill() }
        if ($telemetryProcess.ExitCode -ne 0) { throw "Telemetry failed: $profile" }
        $telemetryProcess = $null
        $paths = Get-Content (Join-Path $profileRoot 'load.log') | Where-Object { $_ -like 'Results: *' }
        foreach ($path in $paths) { Copy-Item -LiteralPath (Join-Path $repoRoot $path.Substring(9)) -Destination $profileRoot }
        $statsSql = "SELECT json_build_object('database',row_to_json(d),'wal',row_to_json(w)) FROM pg_stat_database d CROSS JOIN pg_stat_wal w WHERE datname='ifm_durability_benchmark'"
        Invoke-BenchmarkDocker @('exec',$source,'psql','-U','benchmark','-d','ifm_durability_benchmark','-At','-c',$statsSql) | Set-Content (Join-Path $profileRoot 'source-stats.json')
        Invoke-BenchmarkDocker @('stop',$source) | Out-Null
        if ($projection) { Invoke-BenchmarkDocker @('stop',$projection) | Out-Null }
        Write-Host "Completed: $profile"
    }
    'All requested profiles completed.' | Set-Content (Join-Path $runRoot 'completed.txt')
    Write-Host "Reports: $runRoot"
}
finally {
    if ($telemetryProcess -and -not $telemetryProcess.HasExited) {
        New-Item -ItemType File -Path $telemetryStop -Force | Out-Null
        if (-not $telemetryProcess.WaitForExit(45000)) { $telemetryProcess.Kill() }
    }
    # Only containers and volumes whose exact generated names were recorded by this invocation are removed.
    foreach ($name in $ownedContainers) { & docker rm -f $name *> $null }
    foreach ($volume in $ownedVolumes) { & docker volume rm $volume *> $null }
    foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key,$oldEnvironment[$key],'Process') }
    Pop-Location
}
