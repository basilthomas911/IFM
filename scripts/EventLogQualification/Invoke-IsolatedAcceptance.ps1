param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-f0-9]{12}$')][string]$RunId,
    [ValidateSet('Prepare','InitializeStores','Verify','Run','Cleanup')][string]$Action='Verify',
    [ValidateRange(30,900)][int]$StartupSeconds=180,
    [ValidateSet('None','Basic','PortfolioWrite','PortfolioRestart','Load','LoadRestart','Workflow','WorkflowRestart')][string]$AcceptanceSuite='None'
)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$hostDirectory=Join-Path $repoRoot 'TomasAI.IFM.Application.Api.Server\bin\Release\net10.0'
$artifactDirectory=Join-Path $repoRoot ("BenchmarkDotNet.Artifacts\event-log-v2\acceptance-"+$RunId)
$database="ifm_eventlog_bench_${RunId}_synthetic_host"
$containers=@("ifm-acceptance-$RunId-scylla","ifm-acceptance-$RunId-redis","ifm-acceptance-$RunId-nats")
function DockerChecked([string[]]$Arguments) {
    $result=& docker @Arguments
    if($LASTEXITCODE -ne 0){ throw "Docker operation failed: $($Arguments[0])" }
    return $result
}
function AssertOwned([string]$Name) {
    $item=(DockerChecked @('inspect',$Name)|ConvertFrom-Json)[0]
    if($item.Config.Labels.'ifm.qualification.run' -ne $RunId){throw "Refusing unowned container: $Name"}
    if($Name.EndsWith('-scylla')){$image='scylladb/scylla:6.2.2';$port='9042/tcp';$hostPort='29042'}
    elseif($Name.EndsWith('-redis')){$image='redis:latest';$port='6379/tcp';$hostPort='26379'}
    elseif($Name.EndsWith('-nats')){$image='nats:2.12.0-alpine';$port='4222/tcp';$hostPort='24223'}
    else {throw 'Unknown qualification container role'}
    $binding=$item.HostConfig.PortBindings.$port
    if($item.Config.Image -ne $image -or $binding.Count -ne 1 -or $binding[0].HostIp -ne '127.0.0.1' -or $binding[0].HostPort -ne $hostPort){
        throw "Unexpected qualification image or binding: $Name"
    }
}
function AssertPostgres {
    $item=(DockerChecked @('inspect','ifm-eventlog-benchmark-20260919')|ConvertFrom-Json)[0]
    if($item.Config.Image -ne 'postgres:17.2' -or $item.Config.Labels.'ifm.purpose' -ne 'eventlog-v2-benchmark'){throw 'Unexpected PostgreSQL fixture'}
    $binding=$item.HostConfig.PortBindings.'5432/tcp'
    if($binding.Count -ne 1 -or $binding[0].HostIp -ne '127.0.0.1' -or $binding[0].HostPort -ne '25432'){throw 'Unexpected PostgreSQL binding'}
}
AssertPostgres
if($Action -eq 'Prepare') {
    DockerChecked @('start','ifm-eventlog-benchmark-20260919')
    $existing=DockerChecked @('exec','ifm-eventlog-benchmark-20260919','psql','-U','postgres','-Atc',"SELECT datname FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres';")
    if($existing){throw 'Dedicated PostgreSQL fixture must be empty before Prepare'}
    DockerChecked @('exec','ifm-eventlog-benchmark-20260919','psql','-U','postgres','-v','ON_ERROR_STOP=1','-c',"CREATE DATABASE $database;")
    DockerChecked @('run','-d','--name',$containers[0],'--label',"ifm.qualification.run=$RunId",'-p','127.0.0.1:29042:9042','scylladb/scylla:6.2.2','--smp','1','--memory','1G','--overprovisioned','1','--developer-mode','1')
    DockerChecked @('run','-d','--name',$containers[1],'--label',"ifm.qualification.run=$RunId",'-p','127.0.0.1:26379:6379','redis:latest')
    DockerChecked @('run','-d','--name',$containers[2],'--label',"ifm.qualification.run=$RunId",'-p','127.0.0.1:24223:4222','nats:2.12.0-alpine','-js')
}
if($Action -eq 'Prepare' -or $Action -eq 'InitializeStores') {
    foreach($name in $containers){AssertOwned $name}
    $deadline=[DateTime]::UtcNow.AddSeconds(180)
    do {
        $scyllaLog=DockerChecked @('logs','--tail','20',$containers[0])
        if($scyllaLog -match 'does not satisfy minimum AIO requirements'){
            throw 'Shared Docker host AIO capacity is insufficient. No host kernel setting was changed.'
        }
        $ErrorActionPreference='Continue'
        & docker exec $containers[0] cqlsh -e 'SELECT release_version FROM system.local;' 2>$null
        $ErrorActionPreference='Stop'
        if($LASTEXITCODE -eq 0){break}
        if([DateTime]::UtcNow -gt $deadline){throw 'Scylla readiness timeout; owned fixtures retained for diagnosis'}
        Start-Sleep -Seconds 2
    } while($true)
    foreach($store in @('trade','fund','reference','optionpricer','marketdata','securities')){
        $keyspace="ifm_synthetic_${RunId}_$store"
        DockerChecked @('exec',$containers[0],'cqlsh','-e',"CREATE KEYSPACE IF NOT EXISTS $keyspace WITH replication = {'class':'SimpleStrategy','replication_factor':1};")
    }
    Write-Output 'Isolated stores prepared. Use Verify before Run.'
    exit
}
if($Action -eq 'Cleanup') {
    foreach($name in $containers){AssertOwned $name}
    DockerChecked @('exec','ifm-eventlog-benchmark-20260919','psql','-U','postgres','-v','ON_ERROR_STOP=1','-c',"DROP DATABASE IF EXISTS $database WITH (FORCE);")
    foreach($name in $containers){DockerChecked @('stop',$name); DockerChecked @('rm','-v',$name)}
    DockerChecked @('stop','ifm-eventlog-benchmark-20260919')
    Write-Output 'Owned synthetic stores removed without backup. Reports retained.'
    exit
}
$env:DOTNET_ENVIRONMENT='Test'
$env:ASPNETCORE_ENVIRONMENT='Test'
$env:DATABENTO_API_KEY=''
$env:FMP_API_KEY=''
$env:POSTGRES_TEST_KEY='{"userid":"postgres","password":"ifm-benchmark-only"}'
$env:SCYLLADB_TEST_KEY='{"userid":"cassandra","password":"cassandra"}'
$env:IFM_NATS_URL='nats://127.0.0.1:24223'
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION="Host=127.0.0.1;Port=25432;Database=$database"
$env:IFM_PORTFOLIO_LIVE_ID='1909202601'
if($AcceptanceSuite -eq 'WorkflowRestart'){
    $checkpoint=Get-Content -LiteralPath (Join-Path $artifactDirectory 'workflow-checkpoint.json') -Raw | ConvertFrom-Json
    $env:IFM_PORTFOLIO_LIVE_ID=[string]$checkpoint.PortfolioId
    $env:IFM_PORTFOLIO_FUND_LIVE_ID=[string]$checkpoint.FundId
    $env:IFM_PORTFOLIO_LIVE_WORKFLOW_ID=[string]$checkpoint.WorkflowId
}
$env:IFM_QUALIFICATION_ARTIFACT_DIRECTORY=$artifactDirectory
$flag="--event-log-qualification=$RunId"
if($Action -eq 'Verify'){
    Push-Location $hostDirectory
    try { & dotnet TomasAI.IFM.Application.Api.Server.dll $flag --verify-startup-only; if($LASTEXITCODE -ne 0){throw 'Qualification composition failed'} }
    finally { Pop-Location }
    exit
}
foreach($name in $containers){AssertOwned $name}
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
if($AcceptanceSuite -ne 'None'){
    $testProject=Join-Path $repoRoot 'TomasAI.IFM.Domain.Portfolio.IntegrationTests\TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj'
    & dotnet build $testProject -c Release --no-restore --verbosity quiet
    if($LASTEXITCODE -ne 0){throw 'Acceptance build failed before API launch'}
}
$stdout=Join-Path $artifactDirectory ("api-"+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')+'.log')
$stderr=$stdout+'.err'
$process=Start-Process dotnet -ArgumentList @('TomasAI.IFM.Application.Api.Server.dll',$flag) -WorkingDirectory $hostDirectory -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    $deadline=[DateTime]::UtcNow.AddSeconds($StartupSeconds)
    do {
        if($process.HasExited){Get-Content $stdout -Tail 35;Get-Content $stderr -Tail 15;throw "Qualification API exited: $($process.ExitCode)"}
        try {
            $health=Invoke-RestMethod 'http://127.0.0.1:25443/health/bootstrap' -TimeoutSec 2
            if($health.status -eq 'Healthy'){
                Write-Output 'Qualification bootstrap healthy; this is not UI/workflow acceptance.'
                $health|ConvertTo-Json -Depth 8
                if($AcceptanceSuite -ne 'None'){
                    $testProject=Join-Path $repoRoot 'TomasAI.IFM.Domain.Portfolio.IntegrationTests\TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj'
                    $filter=switch($AcceptanceSuite){
                        'Basic' {'FullyQualifiedName~EventLogQualificationCatalogTests|FullyQualifiedName~Production_NATS_actor_allocates_all_typed_business_identities'}
                        'PortfolioWrite' {'FullyQualifiedName~Production_NATS_actors_execute_create_read_update_read_with_real_projection'}
                        'PortfolioRestart' {'FullyQualifiedName~Production_NATS_query_and_authority_retain_state_after_host_restart'}
                        'Load' {'FullyQualifiedName~Concurrent_creates_replays_and_conflicts_preserve_exact_authority_and_projections'}
                        'LoadRestart' {'FullyQualifiedName~Retries_after_API_restart_preserve_one_business_event_per_portfolio'}
                        'Workflow' {'FullyQualifiedName~Production_NATS_actors_execute_configuration_resolution_reservation_composition_and_risk'}
                        'WorkflowRestart' {'FullyQualifiedName~Production_pipeline_configuration_and_composition_retain_state_after_host_restart'}
                    }
                    & dotnet test $testProject -c Release --no-build --no-restore --filter $filter --logger "trx;LogFileName=$AcceptanceSuite-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss')).trx" --results-directory $artifactDirectory
                    if($LASTEXITCODE -ne 0){throw "Isolated live-host $AcceptanceSuite acceptance failed"}
                }
                exit
            }
        } catch [System.Net.WebException] {}
        if([DateTime]::UtcNow -gt $deadline){Get-Content $stdout -Tail 50;throw 'Qualification bootstrap deadline exceeded'}
        Start-Sleep -Seconds 2
    } while($true)
} finally {
    if(!$process.HasExited){Stop-Process -Id $process.Id; $process.WaitForExit()}
    Write-Output "Owned API child stopped. Logs: $stdout; stores retained for explicit Cleanup."
}
