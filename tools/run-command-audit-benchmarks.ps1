$ErrorActionPreference = 'Stop'
$Filter = if ($args.Count -gt 0) { $args[0] } else { '*CommandAuditSerializationBenchmarks*' }
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
$env:NUGET_PACKAGES = 'C:\Users\basil\.nuget\packages'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '..\.test-results\dotnet-cli-home'
dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks/TomasAI.IFM.Application.Storage.Benchmarks.csproj -c Release --no-build -- --filter $Filter --artifacts BenchmarkDotNet.Artifacts/command-log-messagepack-gates
exit $LASTEXITCODE
