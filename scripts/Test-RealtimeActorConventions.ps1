[CmdletBinding()]
param()

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$actorFiles = @(Get-ChildItem -LiteralPath $repositoryRoot -Directory -Filter 'TomasAI.IFM.Domain.*' |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter '*RealtimeActor.cs' -File |
            Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }
    })
$actorFiles += Get-Item -LiteralPath (
    Join-Path $repositoryRoot 'TomasAI.IFM.Shared.EventModelActor.Templates/RealtimeActorTemplate.cs')

$violations = [System.Collections.Generic.List[string]]::new()
$domainActorCount = 0
$concreteActorCount = 0

foreach ($actorFile in $actorFiles) {
    $source = [IO.File]::ReadAllText($actorFile.FullName)
    if ($source -notmatch 'BaseEventActor\s*<') {
        continue
    }

    $concreteActorCount++
    $relativePath = $actorFile.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    $isTemplate = $actorFile.Name -eq 'RealtimeActorTemplate.cs'
    if (-not $isTemplate) {
        $domainActorCount++
    }

    if ($source -notmatch '(?:IReadOnlyDictionary|Dictionary)<string,\s*Func<IActorMessage,\s*IEvent>>\s+_parseMap') {
        $violations.Add("$relativePath does not expose a verb-keyed _parseMap.")
    }
    if ($source -notmatch '(?:IReadOnlyDictionary|Dictionary)<Type,[\s\S]{0,700}?_receiveMap') {
        $violations.Add("$relativePath does not expose an exact-Type _receiveMap.")
    }
    if ($source -notmatch 'ParseMappedRealtimeEvent\(\s*(?:context|actorContext),\s*message,\s*_parseMap\s*\)') {
        $violations.Add("$relativePath does not delegate parsing to ParseMappedRealtimeEvent.")
    }
    if ($source -notmatch 'ResolveMappedEventHandler\(\s*(?:@event|domainEvent),\s*_receiveMap\s*\)') {
        $violations.Add("$relativePath does not delegate receive dispatch to ResolveMappedEventHandler.")
    }
    if (($source -match '_receiveMap\.TryGetValue') -or
        ($source -match 'switch\s*\(\s*(?:@event|domainEvent)\s*\)')) {
        $violations.Add("$relativePath retains actor-owned receive routing.")
    }
    if (($source -match '_receiveMap[\s\S]{0,250}?Dictionary<string') -or
        ($source -match '_receiveMap\.TryGetValue\([^\r\n]*GetType\(\)\.Name')) {
        $violations.Add("$relativePath retains string-keyed receive dispatch.")
    }

    if (-not $isTemplate) {
        $parseTypes = @([regex]::Matches($source, 'AsEvent<\s*([^>]+)\s*>') |
            ForEach-Object { $_.Groups[1].Value.Trim() } | Sort-Object -Unique)
        $receiveTypes = @([regex]::Matches($source, '\[typeof\(([^\)]+)\)\]\s*=') |
            ForEach-Object { $_.Groups[1].Value.Trim() } | Sort-Object -Unique)

        if ($parseTypes.Count -eq 0) {
            $violations.Add("$relativePath does not expose any concrete parsed realtime event types.")
        }
        if (Compare-Object $parseTypes $receiveTypes) {
            $violations.Add("$relativePath parse and receive realtime event sets differ.")
        }
    }
}

if ($domainActorCount -ne 16) {
    $violations.Add("Expected 16 domain RealtimeActors but discovered $domainActorCount.")
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "RealtimeActor convention verification failed with $($violations.Count) violation(s)."
}

Write-Host "RealtimeActor convention verification passed for all $domainActorCount domain actors ($concreteActorCount domain/template actors inspected)."
