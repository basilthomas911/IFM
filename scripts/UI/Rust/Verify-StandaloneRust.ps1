param([switch]$SkipNats,[string[]]$NativeTests,[string]$NatsFilter)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$testWorker = $null
$containerName = 'ifm-standalone-protocol-' + [Guid]::NewGuid().ToString('N')
$containerStarted = $false
$priorTestUrl = $env:IFM_STANDALONE_TEST_NATS_URL
$priorOrderJournal = $env:IFM_STANDALONE_ORDER_JOURNAL
$env:IFM_STANDALONE_ORDER_JOURNAL = Join-Path $repoRoot (".artifacts/native-order-journals/" + [Guid]::NewGuid().ToString("N"))
$clientKeys=@('DOTNET_ENVIRONMENT','IFM_UI_CONFIG_PATH','IFM_UI_NATS_URL','IFM_UI_NATS_TOKEN','IFM_UI_NATS_CREDENTIALS','IFM_UI_MARKETDATA_NATS_URL','IFM_UI_MARKETDATA_SCOPE','IFM_UI_BROKER_ACCOUNT_ALIAS','IFM_UI_BROKER_ENVIRONMENT','TradeBroker__AccountAlias','TradeBroker__Environment','TradeBroker__Emulator__AccountAlias','IFM_UI_ACTOR_HEALTH_URL','IFM_UI_OPERATIONS_HEALTH_URL','IFM_UI_LIVE_PIPELINE_HEALTH_URL')
$priorClientSettings=@{};foreach($key in $clientKeys){$priorClientSettings[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
$profileDirectory=Join-Path $repoRoot ('.artifacts/standalone-verification-profiles/'+[Guid]::NewGuid().ToString('N'))
Push-Location $repoRoot
try {
    foreach($key in $clientKeys){[Environment]::SetEnvironmentVariable($key,$null,'Process')}
    New-Item -ItemType Directory -Force -Path $profileDirectory | Out-Null
    $env:IFM_UI_CONFIG_PATH=Join-Path $profileDirectory 'ui-client.Development.json'
    '{}' | Set-Content -LiteralPath $env:IFM_UI_CONFIG_PATH -Encoding UTF8
    $env:DOTNET_ENVIRONMENT='Development';$env:IFM_UI_BROKER_ACCOUNT_ALIAS='IFM-EMULATOR-PAPER';$env:IFM_UI_BROKER_ENVIRONMENT='Emulator';$env:IFM_UI_MARKETDATA_SCOPE='nativecompat'
    function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            & $Executable @Arguments 2>&1 | ForEach-Object { $_.ToString() }
            $nativeExitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $previousPreference }
        if ($nativeExitCode -ne 0) { throw "$Executable failed with exit code $nativeExitCode" }
    }
    $project = 'TomasAI.IFM.UI.Rust.eGui/tools/ContractFixtures/ContractFixtures.csproj'
    $fixtures = 'TomasAI.IFM.UI.Rust.eGui/tests/fixtures/backend'
    $artifacts = '.artifacts/standalone-contract-tool'
    Invoke-Checked -Executable dotnet -Arguments @('build', $project, '--artifacts-path', $artifacts)
    $oracle = Join-Path $repoRoot "$artifacts/bin/ContractFixtures/debug/ContractFixtures.dll"
    Invoke-Checked -Executable dotnet -Arguments @($oracle, $fixtures)
    Invoke-Checked -Executable python -Arguments @('TomasAI.IFM.UI.Rust.eGui/tools/audit_native_ownership.py')
    # Serialize MSVC links: parallel debug/PDB links intermittently fail on this
    # Windows workstation even when the same target succeeds in isolation.
    # Service/controller tests do not launch the desktop. Avoid relinking the
    # GUI executable for each isolated protocol gate; normal/release builds
    # retain the default desktop feature and are verified at packaging.
    $testArguments=@('test','--jobs','1','--locked','--no-default-features','--manifest-path','TomasAI.IFM.UI.Rust.eGui/Cargo.toml')
    if($NativeTests) {
        foreach($test in (@('backend_codec')+$NativeTests|Select-Object -Unique)) {
            if($test -notmatch '^[A-Za-z0-9_]+$'){throw 'Invalid native test name'}
            $testArguments+=@('--test',$test)
        }
    } else {$testArguments+='--tests'}
    Invoke-Checked -Executable cargo -Arguments $testArguments
    Invoke-Checked -Executable dotnet -Arguments @($oracle, $fixtures, 'TomasAI.IFM.UI.Rust.eGui/target/cross-language')
    if (-not $SkipNats) {
        $natsArguments=@('test', '--jobs', '1', '--locked', '--no-default-features', '--manifest-path', 'TomasAI.IFM.UI.Rust.eGui/Cargo.toml', '--test', 'backend_nats')
        if($NatsFilter){if($NatsFilter -notmatch '^[A-Za-z0-9_]+$'){throw 'Invalid integration filter'};$natsArguments+=$NatsFilter}
        # Compile/link before starting the finite-lived responder. A slow MSVC
        # link must not consume the integration fixture's five-minute lifetime.
        Invoke-Checked -Executable cargo -Arguments ($natsArguments+@('--no-run'))
        Invoke-Checked -Executable docker -Arguments @('run', '-d', '--name', $containerName, '-p', '127.0.0.1::4222', 'nats:2.12.0-alpine')
        $containerStarted = $true
        $port = (& docker port $containerName 4222/tcp).Trim().Split(':')[-1]
        if ($LASTEXITCODE -ne 0 -or $port -eq '4222') { throw 'Could not allocate isolated NATS port' }
        $env:IFM_STANDALONE_TEST_NATS_URL = "nats://127.0.0.1:$port"
        $env:IFM_UI_NATS_URL=$env:IFM_STANDALONE_TEST_NATS_URL
        $log = Join-Path $repoRoot '.artifacts/standalone-responder.log'
        $errorLog = Join-Path $repoRoot '.artifacts/standalone-responder-error.log'
        $workerArguments = '"' + $oracle + '" "' + (Join-Path $repoRoot $fixtures) + '" --serve ' + $env:IFM_STANDALONE_TEST_NATS_URL
        $testWorker = Start-Process -FilePath dotnet -ArgumentList $workerArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $log -RedirectStandardError $errorLog
        $readyDeadline = [DateTime]::UtcNow.AddSeconds(30)
        while (-not (Test-Path $log) -or -not (Select-String -LiteralPath $log -Pattern '^READY:' -Quiet)) {
            if ($testWorker.HasExited -or [DateTime]::UtcNow -ge $readyDeadline) { throw "Compatibility responder did not become ready; inspect $errorLog" }
            Start-Sleep -Milliseconds 100
        }
        $natsArguments+=@('--', '--ignored', '--nocapture', '--test-threads=1')
        Invoke-Checked -Executable cargo -Arguments $natsArguments | Tee-Object -Variable integrationOutput
        if(-not($integrationOutput -match 'test result: ok\. [1-9][0-9]* passed')){throw 'Integration filter matched no executed passing tests; acceptance was not verified.'}
        # Integration operations persist their receipts after the initial native
        # tests. Re-open those exact files through the unchanged managed stores.
        Invoke-Checked -Executable dotnet -Arguments @($oracle, $fixtures, 'TomasAI.IFM.UI.Rust.eGui/target/cross-language')
    }
} finally {
    if ($testWorker -and -not $testWorker.HasExited) { Stop-Process -Id $testWorker.Id -Force }
    if ($containerStarted) { & docker rm -f $containerName | Out-Null }
    $env:IFM_STANDALONE_TEST_NATS_URL = $priorTestUrl
    $env:IFM_STANDALONE_ORDER_JOURNAL = $priorOrderJournal
    foreach($key in $clientKeys){[Environment]::SetEnvironmentVariable($key,$priorClientSettings[$key],'Process')}
    Pop-Location
}
