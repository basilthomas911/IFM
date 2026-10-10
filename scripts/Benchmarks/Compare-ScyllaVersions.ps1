[CmdletBinding()]
param([string[]]$OnlyProfiles=@(),[switch]$ManageWslAioLimit)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$run=[Guid]::NewGuid().ToString('N').Substring(0,12)
$root=Join-Path $repo ".artifacts/scylla-version-benchmarks/$run"
New-Item -ItemType Directory -Force $root|Out-Null
$project='TomasAI.IFM.Application.Storage.Scylla.Benchmarks'
$containers=[Collections.Generic.List[string]]::new();$volumes=[Collections.Generic.List[string]]::new()
$envKeys=@('IFM_SCYLLA_PROFILE','IFM_SCYLLA_VERSION','IFM_SCYLLA_SYNC','IFM_SCYLLA_BENCHMARK_PORT','IFM_SCYLLA_EVIDENCE');$previous=@{}
foreach($key in $envKeys){$previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
$profiles=@()
foreach($version in @('6.2.2','2026.3.3')){$profiles+=@{Name="$version-periodic";Version=$version;Mode='periodic';Methods=@('*')}}
foreach($version in @('6.2.2','2026.3.3')){$profiles+=@{Name="$version-batch";Version=$version;Mode='batch';Methods=@('AppendPlan','SamePartitionBatch32','ConcurrentWrite8')}}
foreach($version in @('6.2.2','2026.3.3')){$profiles+=@{Name="$version-sustained";Version=$version;Mode='periodic';Sustain=$true;Methods=@()}}
function Invoke-ScyllaDocker([string[]]$Arguments){$output=& docker @Arguments;if($LASTEXITCODE -ne 0){throw "Docker failed: $($Arguments[0])"};return $output}
function Remove-Container([string]$Name){
    if(@(& docker ps -a --filter "name=^/$Name$" --format '{{.Names}}').Count -eq 0){return}
    $info=(& docker inspect $Name|ConvertFrom-Json)[0];if($info.Config.Labels.'ifm.scylla.benchmark' -ne $run){throw 'Wrong container label'}
    Invoke-ScyllaDocker @('rm','-f','-v',$Name)|Out-Null
}
function Remove-Volume([string]$Name){
    if(@(& docker volume ls --filter "name=^$Name$" --format '{{.Name}}').Count -eq 0){return}
    $info=(& docker volume inspect $Name|ConvertFrom-Json)[0];if($info.Labels.'ifm.scylla.benchmark' -ne $run){throw 'Wrong volume label'}
    Invoke-ScyllaDocker @('volume','rm',$Name)|Out-Null
}
try{
    $originalAioLimit=$null
    Push-Location $repo
    if($ManageWslAioLimit){
        $originalAioLimit=[long]((& wsl --distribution docker-desktop --user root cat /proc/sys/fs/aio-max-nr)-join '')
        if($LASTEXITCODE -ne 0){throw 'Cannot read WSL AIO ceiling'}
        if($originalAioLimit -lt 1048576){& wsl --distribution docker-desktop --user root sysctl -w fs.aio-max-nr=1048576;if($LASTEXITCODE -ne 0){throw 'Cannot raise WSL AIO ceiling'}}
    }
    & dotnet build $project -c Release *> (Join-Path $root 'build.log');if($LASTEXITCODE -ne 0){throw 'Build failed'}
    $manifest=[Collections.Generic.List[object]]::new()
    foreach($profile in $profiles){
        if($OnlyProfiles.Count -gt 0 -and $profile.Name -notin $OnlyProfiles){continue}
        $name="ifm-scyllabdn-$run-$($profile.Name.Replace('.','-'))";$volume="$name-data";$directory=Join-Path $root $profile.Name
        New-Item -ItemType Directory -Force $directory|Out-Null;$containers.Add($name);$volumes.Add($volume)
        Invoke-ScyllaDocker @('volume','create','--label',"ifm.scylla.benchmark=$run",$volume)|Out-Null
        $arguments=@('create','--name',$name,'--label',"ifm.scylla.benchmark=$run",'--cpus','4','--memory','4g',
            '-p','127.0.0.1:59142:59142','-p','127.0.0.1:59143:59143','-p','127.0.0.1:59144:10000','-p','127.0.0.1:59145:9180',
            '-v',"${volume}:/var/lib/scylla","scylladb/scylla:$($profile.Version)",
            '--smp','4','--memory','3G','--reserve-memory','256M','--overprovisioned','1','--developer-mode','1','--reactor-backend','epoll',
            '--rpc-address','0.0.0.0','--api-address','0.0.0.0','--prometheus-address','0.0.0.0','--native-transport-port','59142','--native-shard-aware-transport-port','59143','--broadcast-rpc-address','127.0.0.1',
            '--commitlog-sync',$profile.Mode,'--commitlog-sync-period-in-ms','10000','--commitlog-sync-batch-window-in-ms','1',
            '--commitlog-total-space-in-mb','256','--commitlog-segment-size-in-mb','32')
        if($profile.Version -eq '2026.3.3'){$arguments+=@('--critical-disk-utilization-level','0.995')}
        Invoke-ScyllaDocker $arguments|Out-Null;Invoke-ScyllaDocker @('start',$name)|Out-Null
        $ready=$false
        for($attempt=0;$attempt -lt 90;$attempt++){
            try{& docker exec $name cqlsh 127.0.0.1 59142 -e 'SELECT release_version FROM system.local;' *> $null;if($LASTEXITCODE -eq 0){$ready=$true;break}}catch{}
            Start-Sleep -Seconds 2
        }
        if(-not $ready){throw "Scylla $($profile.Name) failed startup"}
        $serverVersion=(Invoke-ScyllaDocker @('exec',$name,'scylla','--version')) -join ''
        $settings=@{ServerVersion=$serverVersion;Image="scylladb/scylla:$($profile.Version)";CriticalDiskUtilization=if($profile.Version -eq '2026.3.3'){0.995}else{'Version default'};Sync=$profile.Mode;SyncPeriodMs=10000;BatchWindowMs=1;DatabaseCpus=4;ContainerMemoryBytes=4GB;ScyllaMemory='3G';ReserveMemory='256M';DeveloperMode=$true;CommitlogSpaceMiB=256;CommitlogSegmentMiB=32}
        $settings|ConvertTo-Json|Set-Content (Join-Path $directory 'settings.json')
        $manifest.Add(@{Profile=$profile.Name;Status='Running';Settings=$settings});$manifest|ConvertTo-Json -Depth 5|Set-Content (Join-Path $root 'manifest.json')
        $env:IFM_SCYLLA_PROFILE=$profile.Name;$env:IFM_SCYLLA_VERSION=$profile.Version;$env:IFM_SCYLLA_SYNC=$profile.Mode;$env:IFM_SCYLLA_BENCHMARK_PORT='59142';$env:IFM_SCYLLA_EVIDENCE=$directory
        Write-Host "Running $($profile.Name)."
        $arguments=@('run','--project',$project,'-c','Release','--no-build','--')
        if($profile.Sustain){$arguments+='--sustain'}else{$arguments+='--filter';foreach($method in $profile.Methods){$arguments+="*ScyllaVersionBenchmarks.$method*"};$arguments+=@('--artifacts',$directory,'--exporters','json','csv')}
        $process=Start-Process dotnet -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'stdout.log') -RedirectStandardError (Join-Path $directory 'stderr.log');$handle=$process.Handle
        $samples=[Collections.Generic.List[object]]::new()
        while(-not $process.HasExited){
            $stats=(Invoke-ScyllaDocker @('stats',$name,'--no-stream','--format','{{json .}}'))-join ''
            $available=[long]((Invoke-ScyllaDocker @('exec',$name,'sh','-c','df -Pk /var/lib/scylla | tail -1')) -split '\s+')[3]*1024
            $samples.Add(@{Utc=[DateTime]::UtcNow.ToString('O');AvailableBytes=$available;Stats=($stats|ConvertFrom-Json)})
            if($available -lt 3GB){$process.Kill();$process.WaitForExit();throw 'Docker storage below 3 GiB reserve'}
            Start-Sleep -Seconds 5;$process.Refresh()
        }
        $process.WaitForExit();$samples|ConvertTo-Json -Depth 5|Set-Content (Join-Path $directory 'docker-stats.json')
        if($process.ExitCode -ne 0){throw "Benchmark process failed: $($profile.Name)"}
        if($profile.Sustain){$r=Get-Content (Join-Path $directory "$($profile.Name)-sustained.json") -Raw|ConvertFrom-Json;if($r.SuccessfulFlushes -ne 2 -or $r.Events -le 0){throw 'Invalid sustained results'}}
        else{
            $r=Get-Content (Join-Path $directory 'results/TomasAI.IFM.Application.Storage.Scylla.Benchmarks.ScyllaVersionBenchmarks-report-full-compressed.json') -Raw|ConvertFrom-Json
            $count=if($profile.Methods[0] -eq '*'){9}else{$profile.Methods.Count}
            if($r.Benchmarks.Count -ne $count -or @($r.Benchmarks|Where-Object{$null -eq $_.Statistics -or $_.Statistics.N -lt 6}).Count -gt 0){throw 'Missing/insufficient BDN results'}
        }
        $manifest[$manifest.Count-1].Status='Complete';$manifest|ConvertTo-Json -Depth 5|Set-Content (Join-Path $root 'manifest.json')
        Start-Process docker -ArgumentList @('logs',$name) -Wait -WindowStyle Hidden -RedirectStandardOutput (Join-Path $directory 'server.stdout.log') -RedirectStandardError (Join-Path $directory 'server.stderr.log')
        Remove-Container $name;Remove-Volume $volume;Write-Host "Completed $($profile.Name)."
    }
    Write-Host "Scylla comparison complete: $root"
}finally{
    foreach($name in $containers){
        if(@(& docker ps -a --filter "name=^/$name$" --format '{{.Names}}').Count -gt 0){
            $dirName=$name.Substring(("ifm-scyllabdn-$run-").Length)
            $stdout=Join-Path $root "$dirName-server.stdout.log";$stderr=Join-Path $root "$dirName-server.stderr.log"
            Start-Process docker -ArgumentList @('logs',$name) -Wait -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
            Remove-Container $name
        }
    }
    foreach($volume in $volumes){Remove-Volume $volume}
    foreach($key in $envKeys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}
    if($null -ne $originalAioLimit){& wsl --distribution docker-desktop --user root sysctl -w "fs.aio-max-nr=$originalAioLimit";if($LASTEXITCODE -ne 0){throw 'Cannot restore WSL AIO ceiling'}}
    Pop-Location
}






