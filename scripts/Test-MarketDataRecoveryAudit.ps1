# Isolated recovery-audit verification. Does not use the trading database or running API.
[CmdletBinding()]
param([switch]$SkipNativeBuild)
$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$run=[Guid]::NewGuid().ToString('N').Substring(0,12)
$database="ifm_recovery_audit_$run"
$postgres="ifm-recovery-audit-pg-$run"
$nats="ifm-recovery-audit-nats-$run"
$artifact=Join-Path $workspace '.artifacts/recovery-audit'
New-Item -ItemType Directory -Force -Path $artifact | Out-Null
$variables=@('IFM_RECOVERY_AUDIT_TEST_CONNECTION','IFM_RECOVERY_AUDIT_TEST_NATS','IFM_RECOVERY_AUDIT_NATIVE_TEST_EXE','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','POSTGRES_TEST_KEY','IFM_RECOVERY_AUDIT_EVIDENCE')
$previous=@{}
foreach($variable in $variables){$previous[$variable]=[Environment]::GetEnvironmentVariable($variable)}
Push-Location $workspace
try {
    if(-not $SkipNativeBuild) {
        $ErrorActionPreference='Continue'
        & cargo test --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --offline --lib --no-run *> (Join-Path $artifact 'native-build.log')
        $nativeExitCode=$LASTEXITCODE
        $ErrorActionPreference='Stop'
        if($nativeExitCode -ne 0){throw 'Native qualification build failed; see native-build.log.'}
    }
    $binary=Get-ChildItem (Join-Path $workspace 'TomasAI.IFM.Application.MarketData.Rust/target/debug/deps') -Filter 'ifm_market_data_native-*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if(-not $binary){throw 'Native qualification executable is missing.'}
    & docker run --detach --rm --pull never --name $postgres --label "ifm.test=recovery-audit-$run" --publish '127.0.0.1::5432' --env 'POSTGRES_PASSWORD=ifm-audit-test' --env "POSTGRES_DB=$database" postgres:17.2 | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Isolated PostgreSQL startup failed.'}
    & docker run --detach --rm --pull never --name $nats --label "ifm.test=recovery-audit-$run" --publish '127.0.0.1::4222' nats:2.12.0-alpine --jetstream | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Isolated NATS startup failed.'}
    $ready=$false
    for($attempt=0;$attempt -lt 30;$attempt++) {
        & docker exec $postgres pg_isready --username postgres --dbname $database *> $null
        if($LASTEXITCODE -eq 0){$ready=$true;break}
        Start-Sleep -Seconds 1
    }
    if(-not $ready){throw 'Isolated PostgreSQL readiness failed.'}
    $pgAddress=(& docker port $postgres 5432/tcp).Trim()
    $natsAddress=(& docker port $nats 4222/tcp).Trim()
    if($pgAddress -notmatch '^127[.]0[.]0[.]1:(\d+)$'){throw 'Unexpected PostgreSQL fixture address.'}
    $pgPort=$Matches[1]
    if($natsAddress -notmatch '^127[.]0[.]0[.]1:(\d+)$'){throw 'Unexpected NATS fixture address.'}
    $env:IFM_RECOVERY_AUDIT_TEST_CONNECTION="Host=127.0.0.1;Port=$pgPort;Database=$database;Timeout=120;Command Timeout=120"
    $env:IFM_RECOVERY_AUDIT_TEST_NATS="nats://$natsAddress"
    $env:IFM_RECOVERY_AUDIT_NATIVE_TEST_EXE=$binary.FullName
    $env:IFM_RECOVERY_AUDIT_EVIDENCE=$artifact
    $env:DOTNET_ENVIRONMENT='Test'
    $env:ASPNETCORE_ENVIRONMENT='Test'
    $env:POSTGRES_TEST_KEY='{"userid":"postgres","password":"ifm-audit-test"}'
    $ErrorActionPreference='Continue'
    & dotnet test TomasAI.IFM.Domain.MarketData.Feed.Recovery.IntegrationTests --no-restore -m:2 -nr:false -v:minimal --logger "trx;LogFileName=recovery-audit.trx" --results-directory $artifact *> (Join-Path $artifact 'integration.log')
    $testExitCode=$LASTEXITCODE
    $ErrorActionPreference='Stop'
    if($testExitCode -ne 0){throw 'Recovery audit integration verification failed; see .artifacts/recovery-audit/integration.log.'}
    Write-Output "Recovery audit PostgreSQL/NATS/collector verification passed. Evidence: $artifact"
}
finally {
    $ErrorActionPreference='Continue'
    & docker stop $nats $postgres *> (Join-Path $artifact 'fixture-cleanup.log')
    foreach($variable in $variables){[Environment]::SetEnvironmentVariable($variable,$previous[$variable])}
    Pop-Location
}
