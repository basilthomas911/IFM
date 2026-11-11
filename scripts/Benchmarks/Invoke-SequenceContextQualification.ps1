[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Before','After')][string]$Arm, [switch]$NoBuild, [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunLabel)
$ErrorActionPreference='Stop'
$workingRepository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$repository=if($Arm -eq 'Before'){'E:\IFM\DbContextConventions\20261110\baseline\source'}else{$workingRepository}
if(!(Test-Path -LiteralPath (Join-Path $repository 'TomasAI.IFM.sln'))){throw 'The frozen baseline source is required before qualification.'}
if($Arm -eq 'Before') {
    $sourceManifest=Get-Content -LiteralPath 'E:\IFM\DbContextConventions\20261110\baseline\source-manifest.json' -Raw | ConvertFrom-Json
    $sequenceSource=$sourceManifest | Where-Object path -eq 'TomasAI.IFM.Application.Storage/SequenceIdDb/SequenceIdDbContext.cs'
    if((Get-FileHash -LiteralPath (Join-Path $repository $sequenceSource.path)).Hash -ne $sequenceSource.sha256){throw 'Frozen SequenceIdDb baseline source hash mismatch.'}
}
$run=[Guid]::NewGuid().ToString('N')
$container="ifm-dbcontext-sequence-$run"
$password=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$previous=@{}
foreach($key in @('IFM_DBCONTEXT_BENCHMARK_CONNECTION','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','POSTGRES_TEST_KEY')) {
    $previous[$key]=[Environment]::GetEnvironmentVariable($key)
}
$output="E:\IFM\DbContextConventions\20261110\timings\$Arm\SequenceIdDb"
if($RunLabel){$output=Join-Path $output $RunLabel}
try {
    & docker run --detach --rm --name $container --label "ifm.dbcontext.qualification=$run" --cpus 2 --memory 2g --shm-size 512m -p '127.0.0.1:58231:5432' -e 'POSTGRES_DB=ifm_dbcontext_benchmark' -e 'POSTGRES_USER=benchmark' -e "POSTGRES_PASSWORD=$password" postgres:17.2 | Out-Null
    if($LASTEXITCODE){throw 'Could not start the dedicated benchmark database.'}
    $ready=$false
    for($attempt=0;$attempt -lt 60;$attempt++) {
        & docker exec $container pg_isready -h 127.0.0.1 -U benchmark -d ifm_dbcontext_benchmark *> $null
        if($LASTEXITCODE -eq 0){$ready=$true;break}
        Start-Sleep -Seconds 1
    }
    if(!$ready){throw 'The dedicated benchmark database did not become ready.'}
    $env:IFM_DBCONTEXT_BENCHMARK_CONNECTION="Host=127.0.0.1;Port=58231;Database=ifm_dbcontext_benchmark;Username=benchmark;Password=$password;Pooling=true;Maximum Pool Size=16;Command Timeout=30"
    $env:DOTNET_ENVIRONMENT='Test'
    $env:ASPNETCORE_ENVIRONMENT='Test'
    $env:POSTGRES_TEST_KEY=@{userid='benchmark';password=$password} | ConvertTo-Json -Compress
    Push-Location (Join-Path $repository 'TomasAI.IFM.Application.Storage.Benchmarks')
    try {
        if($NoBuild) {
            $runner=Join-Path $repository '.artifacts/dbcontext-bench/bin/TomasAI.IFM.Application.Storage.Benchmarks/release/TomasAI.IFM.Application.Storage.Benchmarks.dll'
            if(!(Test-Path -LiteralPath $runner)){throw 'Build the qualification runner before requesting NoBuild.'}
            & dotnet $runner --sequence-context --artifacts $output --exporters json
        } else {
        & dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks.csproj -c Release --artifacts-path (Join-Path $repository .artifacts/dbcontext-bench) -- --sequence-context --artifacts $output --exporters json
        }
        if($LASTEXITCODE){throw 'Sequence context qualification failed.'}
    } finally { Pop-Location }
} finally {
    foreach($entry in $previous.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)}
    $owner=& docker inspect --format '{{index .Config.Labels "ifm.dbcontext.qualification"}}' $container 2>$null
    if($LASTEXITCODE -eq 0 -and $owner -eq $run){ & docker rm --force $container | Out-Null }
}
