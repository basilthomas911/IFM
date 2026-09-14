[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceFiles = Get-ChildItem -LiteralPath $repositoryRoot -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.test-results|\.artifacts)[\\/]' }

$matches = foreach ($file in $sourceFiles) {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        if ($line -match 'LoggerMessage\(EventId\s*=\s*(23(?:8|9)\d{2})') {
            [pscustomobject]@{
                EventId = [int]$Matches[1]
                File = $file.FullName
                Line = $lineNumber
            }
        }
    }
}

$duplicates = $matches | Group-Object EventId | Where-Object Count -gt 1
if ($duplicates) {
    $details = $duplicates | ForEach-Object {
        "$($_.Name): " + (($_.Group | ForEach-Object { "$($_.File):$($_.Line)" }) -join ', ')
    }
    throw "Duplicate Futures ITI/workflow LoggerMessage EventIds found: $($details -join '; ')"
}

$expected = @(23805, 23807, 23808, 23809) + @(23810..23814) + @(23820..23828) + @(23900..23910)
$actual = @($matches.EventId | Sort-Object -Unique)
$missing = @($expected | Where-Object { $_ -notin $actual })
if ($missing.Count -ne 0) {
    throw "Missing reserved Futures ITI/workflow LoggerMessage EventIds: $($missing -join ', ')"
}

$itiRoot = Join-Path $repositoryRoot 'TomasAI.IFM.Domain.MarketData.Analytics\FuturesItiSignal'
$retiredTypes = @(
    'FuturesItiSignalRealtimeState',
    'FuturesItiSignalRealtimeProjector',
    'FuturesItiSignalStreamOwnership'
)
$itiText = (Get-ChildItem -LiteralPath $itiRoot -Recurse -Filter '*.cs' -File |
    ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }) -join "`n"
foreach ($retiredType in $retiredTypes) {
    if ($itiText.IndexOf($retiredType, [StringComparison]::Ordinal) -ge 0) {
        throw "Retired Futures ITI realtime dependency remains referenced: $retiredType"
    }
}

$actorFile = Join-Path $itiRoot 'Realtime\Actor\FuturesItiSignalRealtimeActor.cs'
$actorText = [System.IO.File]::ReadAllText($actorFile)
if (([regex]::Matches($actorText, 'typeof\(FuturesMarketPriceUpdatedRealtimeEvent\)')).Count -ne 1) {
    throw 'The Futures ITI realtime receive map must contain exactly one market-price event mapping.'
}
if ($actorText -match 'GeneratedComplete|Weekly|Monthly|DbContext|Projector|Repository|Hydrat') {
    throw 'The thin Futures ITI realtime actor contains a prohibited durable or timeframe responsibility.'
}

Write-Host "Futures ITI/workflow observability verification passed ($($actual.Count) reserved EventIds)."
