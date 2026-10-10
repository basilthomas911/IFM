[CmdletBinding()]
param([string]$OutputRoot='.artifacts/postgres-tuning', [string[]]$OnlyProfiles=@())
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runId=[Guid]::NewGuid().ToString('N').Substring(0,12)
$runRoot=Join-Path $repoRoot (Join-Path $OutputRoot $runId)
New-Item -ItemType Directory -Force $runRoot | Out-Null
$project='TomasAI.IFM.Application.Storage.Postgres.Benchmarks'
$network="ifm-pgtune-$runId"
$containers=[Collections.Generic.List[string]]::new()
$volumes=[Collections.Generic.List[string]]::new()
$oldEnv=@{}
foreach($key in @('POSTGRES_PASSWORD','IFM_POSTGRES_BENCHMARK_PASSWORD','IFM_POSTGRES_BENCHMARK_PORT','IFM_PG_TUNING_PROFILE','IFM_PG_TUNING_HOST','IFM_PG_TUNING_EVIDENCE')) { $oldEnv[$key]=[Environment]::GetEnvironmentVariable($key,'Process') }
$env:POSTGRES_PASSWORD=[Guid]::NewGuid().ToString('N');$env:IFM_POSTGRES_BENCHMARK_PASSWORD=$env:POSTGRES_PASSWORD
$profiles=@(
    @{Name='Baseline';Options=@();Methods=@('*');Client='Windows'},
    @{Name='Memory1GB';Options=@('shared_buffers=1GB');Methods=@('WorkingSetRead','History100');Client='Windows'},
    @{Name='WalLz4';Options=@('wal_compression=lz4');Methods=@('AppendEvent','AppendBatch128','ConcurrentAppend8');Client='Windows'},
    @{Name='Wal4GB';Options=@('max_wal_size=4GB');Methods=@('AppendEvent','AppendBatch128','ConcurrentAppend8');Client='Windows'},
    @{Name='JitOff';Options=@('jit=off');Methods=@('SortWindow');Client='Windows'},
    @{Name='WorkMem32MB';Options=@('work_mem=32MB');Methods=@('SortWindow');Client='Windows'},
    @{Name='IoSync';Options=@('io_method=sync');Methods=@('WorkingSetRead','History100');Client='Windows'},
    @{Name='IoUring';Options=@('io_method=io_uring');Methods=@('WorkingSetRead','History100');Client='Windows'},
    @{Name='LinuxClient';Options=@();Methods=@('*');Client='Linux'},
    @{Name='SustainedBaseline';Options=@();Methods=@();Client='Windows';Sustain=$true},
    @{Name='SustainedCandidate';Options=@('shared_buffers=1GB','wal_compression=lz4','max_wal_size=4GB');Methods=@();Client='Windows';Sustain=$true}
)
function Invoke-TestDocker([string[]]$Arguments) { $result=& docker @Arguments; if($LASTEXITCODE -ne 0){throw "Docker command failed: $($Arguments[0])"};return $result }
function Save-ServerLogs([string]$Name,[string]$Directory) {
    $log=Start-Process docker -ArgumentList @('logs',$Name) -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Directory 'server.stdout.log') -RedirectStandardError (Join-Path $Directory 'server.stderr.log')
    if($log.ExitCode -ne 0){throw 'Failed collecting server logs.'}
}
function Remove-TestContainer([string]$Name) {
    if(@(& docker ps -a --filter "name=^/$Name$" --format '{{.Names}}').Count -eq 0){return}
    $info=(& docker inspect $Name | ConvertFrom-Json)[0]
    if($info.Config.Labels.'ifm.postgres.tuning' -ne $runId){throw 'Refusing to remove a container with a different run label.'}
    Invoke-TestDocker @('rm','-f','-v',$Name) | Out-Null
}
function Remove-TestVolume([string]$Name) {
    if(@(& docker volume ls --filter "name=^$Name$" --format '{{.Name}}').Count -eq 0){return}
    $info=(& docker volume inspect $Name | ConvertFrom-Json)[0]
    if($info.Labels.'ifm.postgres.tuning' -ne $runId){throw 'Refusing to remove a volume with a different run label.'}
    Invoke-TestDocker @('volume','rm',$Name) | Out-Null
}
try {
    Push-Location $repoRoot
    & dotnet build $project -c Release *> (Join-Path $runRoot 'build.log')
    if($LASTEXITCODE -ne 0){throw 'Tuning benchmark build failed.'}
    Invoke-TestDocker @('network','create','--label',"ifm.postgres.tuning=$runId",$network) | Out-Null
    $manifest=[Collections.Generic.List[object]]::new()
    foreach($profile in $profiles) {
        if($OnlyProfiles.Count -gt 0 -and $profile.Name -notin $OnlyProfiles){continue}
        $name="ifm-pgtune-$runId-$($profile.Name.ToLower())";$volume="$name-data"
        $profileRoot=Join-Path $runRoot $profile.Name;New-Item -ItemType Directory -Force $profileRoot | Out-Null
        $containers.Add($name);$volumes.Add($volume)
        Invoke-TestDocker @('volume','create','--label',"ifm.postgres.tuning=$runId",$volume) | Out-Null
        $create=@('create','--name',$name,'--label',"ifm.postgres.tuning=$runId",'--network',$network,'--network-alias','pg-tuning',
            '--cpus','4','--memory','4g','--shm-size','1g','-e','POSTGRES_PASSWORD','-e','POSTGRES_USER=benchmark','-e','POSTGRES_DB=ifm_pg_benchmark','-e','PGDATA=/benchmark-data/pgdata',
            '-p','127.0.0.1:58118:5432','-v',"${volume}:/benchmark-data",'postgres:18.6-bookworm',
            '-c','shared_buffers=256MB','-c','effective_cache_size=2GB','-c','work_mem=16MB','-c','fsync=on','-c','synchronous_commit=on','-c','full_page_writes=on',
            '-c','max_wal_size=2GB','-c','checkpoint_timeout=15min','-c','track_io_timing=on','-c','track_wal_io_timing=on')
        if($profile.Sustain){$create+=@('-c','checkpoint_timeout=30s')}
        foreach($option in $profile.Options){$create+=@('-c',$option)}
        Invoke-TestDocker $create | Out-Null;Invoke-TestDocker @('start',$name) | Out-Null
        $ready=$false
        for($attempt=0;$attempt -lt 30;$attempt++) { try { & docker exec $name pg_isready -h 127.0.0.1 -U benchmark -d ifm_pg_benchmark *> $null;if($LASTEXITCODE -eq 0){$ready=$true;break} } catch { };Start-Sleep -Seconds 1 }
        if(-not $ready) {
            Save-ServerLogs $name $profileRoot
            if($profile.Name -eq 'IoUring') {
                $manifest.Add(@{Profile=$profile.Name;Status='Unavailable';Reason='Default Docker/kernel security did not allow PostgreSQL io_uring startup; see server.stderr.log.'})
                Remove-TestContainer $name;Remove-TestVolume $volume;continue
            }
            throw "Server $($profile.Name) was not ready."
        }
        $settings=(Invoke-TestDocker @('exec',$name,'psql','-U','benchmark','-d','ifm_pg_benchmark','-At','-c',"SELECT json_object_agg(name,setting) FROM pg_settings WHERE name IN ('server_version','shared_buffers','effective_cache_size','work_mem','fsync','synchronous_commit','full_page_writes','wal_compression','max_wal_size','checkpoint_timeout','jit','io_method','effective_io_concurrency','track_io_timing','track_wal_io_timing')")) -join ''
        $settings | Set-Content (Join-Path $profileRoot 'settings.json')
        $manifest.Add(@{Profile=$profile.Name;Status='Running';Client=$profile.Client;Settings=($settings|ConvertFrom-Json);DatabaseCpus=4;DatabaseMemoryBytes=4294967296})
        $manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $runRoot 'manifest.json')
        $env:IFM_POSTGRES_BENCHMARK_PORT='58118';$env:IFM_PG_TUNING_HOST='127.0.0.1';$env:IFM_PG_TUNING_PROFILE=$profile.Name;$env:IFM_PG_TUNING_EVIDENCE=$profileRoot
        Write-Host "Running $($profile.Name) ($($profile.Client))."
        $arguments=@('run','--project',$project,'-c','Release','--no-build','--')
        if($profile.Sustain){$arguments+='--sustain'}
        else {$arguments+='--filter';foreach($method in $profile.Methods){$arguments+="*PostgresTuningBenchmarks.$method*"};$arguments+=@('--artifacts',$profileRoot,'--exporters','json','csv')}
        $clientName=$null
        if($profile.Client -eq 'Linux') {
            $clientName="$name-client";$containers.Add($clientName)
            $arguments=@('run','--name',$clientName,'--label',"ifm.postgres.tuning=$runId",'--network',$network,'--cpus','2','--memory','2g',
                '--mount',"type=bind,source=$(Join-Path $repoRoot $project),target=/source,readonly",'--mount',"type=bind,source=$profileRoot,target=/results",
                '-e','IFM_POSTGRES_BENCHMARK_PASSWORD','-e',"IFM_PG_TUNING_PROFILE=$($profile.Name)",'-e','IFM_POSTGRES_BENCHMARK_PORT=5432','-e','IFM_PG_TUNING_HOST=pg-tuning','-e','IFM_PG_TUNING_EVIDENCE=/results',
                'mcr.microsoft.com/dotnet/sdk:10.0-noble','sh','/source/run-linux.sh')
            $executable='docker'
        } else {$executable='dotnet'}
        $process=Start-Process $executable -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $profileRoot 'stdout.log') -RedirectStandardError (Join-Path $profileRoot 'stderr.log')
        $processHandle=$process.Handle
        $samples=[Collections.Generic.List[object]]::new()
        while(-not $process.HasExited) {
            $stats=(Invoke-TestDocker @('stats',$name,'--no-stream','--format','{{json .}}')) -join ''
            $available=[long]((Invoke-TestDocker @('exec',$name,'sh','-c','df -Pk /benchmark-data | tail -1')) -split '\s+')[3]*1024
            $samples.Add(@{Utc=[DateTime]::UtcNow.ToString('O');AvailableBytes=$available;Stats=($stats|ConvertFrom-Json)})
            if($available -lt 3GB) { $process.Kill();$process.WaitForExit();throw 'Docker storage reserve fell below 3 GiB; benchmark stopped before exhausting the shared filesystem.' }
            Start-Sleep -Seconds 5;$process.Refresh()
        }
        $process.WaitForExit();$samples | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $profileRoot 'docker-stats.json')
        Save-ServerLogs $name $profileRoot
        if($process.ExitCode -ne 0){throw "Profile $($profile.Name) exited $($process.ExitCode)."}
        if($profile.Sustain) {
            $report=Get-Content (Join-Path $profileRoot "$($profile.Name)-sustained.json") -Raw | ConvertFrom-Json
            if($report.Events -le 0 -or ($report.TimedCheckpoints+$report.RequestedCheckpoints) -lt 2){throw 'Invalid sustained-run evidence.'}
        } else {
            $report=Get-Content (Join-Path $profileRoot 'results/TomasAI.IFM.Application.Storage.Postgres.Benchmarks.PostgresTuningBenchmarks-report-full-compressed.json') -Raw | ConvertFrom-Json
            $expected=if($profile.Methods[0] -eq '*'){9}else{$profile.Methods.Count}
            if($report.Benchmarks.Count -ne $expected -or @($report.Benchmarks|Where-Object{$null -eq $_.Statistics -or $_.Statistics.N -lt 6}).Count -gt 0){throw 'One or more BDN measurements are missing or insufficient.'}
        }
        $manifest[$manifest.Count-1].Status='Complete'
        $manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $runRoot 'manifest.json')
        if($clientName){Remove-TestContainer $clientName}
        Remove-TestContainer $name;Remove-TestVolume $volume
        Write-Host "Completed $($profile.Name)."
    }
    Write-Host "Tuning matrix complete: $runRoot"
}
finally {
    foreach($name in $containers){Remove-TestContainer $name}
    foreach($volume in $volumes){Remove-TestVolume $volume}
    if(@(& docker network ls --filter "name=^$network$" --format '{{.Name}}').Count -gt 0){
        $info=(& docker network inspect $network|ConvertFrom-Json)[0]
        if($info.Labels.'ifm.postgres.tuning' -eq $runId){& docker network rm $network *> $null}
    }
    foreach($entry in $oldEnv.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value,'Process')}
    Pop-Location
}





