$ErrorActionPreference = 'Stop'
$Filter = if ($args.Count -gt 0) { $args[0] } else { 'FullyQualifiedName~CommandAuditPersistenceTests' }
$container = docker inspect ifm_db | ConvertFrom-Json
$environment = $container[0].Config.Env
$passwordEntry = $environment | Where-Object { $_ -like 'POSTGRES_PASSWORD=*' } | Select-Object -First 1
$userEntry = $environment | Where-Object { $_ -like 'POSTGRES_USER=*' } | Select-Object -First 1
if (-not $passwordEntry) { throw 'The ifm_db container does not expose POSTGRES_PASSWORD.' }
$password = $passwordEntry.Substring('POSTGRES_PASSWORD='.Length)
$username = if ($userEntry) { $userEntry.Substring('POSTGRES_USER='.Length) } else { 'postgres' }
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = "Host=localhost;Port=5432;Database=event-source-test-db;Username=$username;Password=$password"
$env:DOTNET_ENVIRONMENT = 'Test'
$env:ASPNETCORE_ENVIRONMENT = 'Test'
$env:POSTGRES_TEST_KEY = @{ userid = $username; password = $password } | ConvertTo-Json -Compress
Write-Output "Running command-audit integration tests with filter: $Filter"
$resultsDirectory = Join-Path $PSScriptRoot '..\.test-results'
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$stdout = Join-Path $resultsDirectory 'command-audit.stdout.txt'
$stderr = Join-Path $resultsDirectory 'command-audit.stderr.txt'
$testAssembly = Join-Path $PSScriptRoot '..\TomasAI.IFM.Application.Storage.IntegrationTests\bin\Release\net10.0\TomasAI.IFM.Application.Storage.IntegrationTests.dll'
$testProcess = Start-Process -FilePath $dotnet -NoNewWindow -Wait -PassThru `
    -ArgumentList @('vstest', $testAssembly, "--TestCaseFilter:$Filter", '--Logger:trx;LogFileName=command-audit.trx', "--ResultsDirectory:$resultsDirectory") `
    -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$testExitCode = $testProcess.ExitCode
Get-Content $stdout
if ((Get-Item $stderr).Length -gt 0) { Get-Content $stderr }
[xml]$testResult = Get-Content (Join-Path $resultsDirectory 'command-audit.trx')
$counters = $testResult.TestRun.ResultSummary.Counters
Write-Output "Command-audit tests: total=$($counters.total), passed=$($counters.passed), failed=$($counters.failed)"
Write-Output "Command-audit integration test exit code: $testExitCode"
exit $testExitCode
