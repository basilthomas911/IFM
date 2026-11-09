[CmdletBinding()]
param(
    [string]$ResultsDirectory = 'E:\IFM\RedisRemoval\20261108\tests\livepipeline-final',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$container = 'ifm-redisfree-notifications-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$previous = $env:IFM_RECOVERY_TEST_NATS_URL
try {
    & docker run --detach --rm --pull never --name $container --label 'ifm.test=redisfree-notifications' `
        --publish '127.0.0.1::4222' nats:2.12.0-alpine --jetstream | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Isolated notification broker startup failed.' }
    $address = (& docker port $container 4222/tcp).Trim()
    if ($address -notmatch '^127[.]0[.]0[.]1:\d+$' -or $address.EndsWith(':4222')) { throw 'Unexpected isolated broker endpoint.' }
    $env:IFM_RECOVERY_TEST_NATS_URL = 'nats://' + $address
    $arguments = @('test',(Join-Path $repository 'TomasAI.IFM.LivePipeline.IntegrationTests'),'-m:2','-v:q',
        '-p:NuGetAudit=false','--logger','trx;LogFileName=LivePipeline.IntegrationTests.trx',
        '--results-directory',$ResultsDirectory)
    if ($NoBuild) { $arguments += '--no-build' }
    & dotnet @arguments *> (Join-Path $ResultsDirectory 'integration.log')
    if ($LASTEXITCODE -ne 0) { throw 'Redis-free LivePipeline verification failed; see integration.log.' }
    Write-Output 'LivePipeline verified with isolated JetStream, including durable UI recovery notifications.'
} finally {
    $env:IFM_RECOVERY_TEST_NATS_URL = $previous
    & docker stop $container *> (Join-Path $ResultsDirectory 'cleanup.log')
}
