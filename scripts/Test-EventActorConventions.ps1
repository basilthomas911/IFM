[CmdletBinding()]
param()

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$actorFiles = Get-ChildItem -LiteralPath $repositoryRoot -Directory -Filter 'TomasAI.IFM.Domain.*' |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter '*EventActor.cs' -File |
            Where-Object { $_.FullName -notmatch '[\/](?:bin|obj)[\/]' }
    }

$violations = [System.Collections.Generic.List[string]]::new()
$domainActorCount = 0

foreach ($actorFile in $actorFiles) {
    $source = [IO.File]::ReadAllText($actorFile.FullName)
    if ($source -notmatch 'BaseEventActor\s*<') {
        continue
    }

    $domainActorCount++
    $relativePath = $actorFile.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')

    if ($source -notmatch 'IReadOnlyDictionary<string,\s*Func<IActorMessage,\s*IEvent>>\s+_parseMap') {
        $violations.Add("$relativePath does not expose a read-only verb-keyed parse map.")
    }
    if ($source -notmatch 'IReadOnlyDictionary<Type,[\s\S]{0,900}?_receiveMap') {
        $violations.Add("$relativePath does not expose an exact-type read-only receive map.")
    }
    if ($source -notmatch 'ParseMappedEvent\(\s*(?:context|actorContext),\s*message,\s*_parseMap\s*\)') {
        $violations.Add("$relativePath does not delegate parsing to ParseMappedEvent.")
    }
    if ($source -notmatch 'ResolveMappedEventHandler\(\s*@event,\s*_receiveMap\s*\)') {
        $violations.Add("$relativePath does not delegate receive dispatch to ResolveMappedEventHandler.")
    }
    if ($source -match '_receiveMap\.TryGetValue\([^\r\n]*GetType\(\)\.Name') {
        $violations.Add("$relativePath retains type-name receive dispatch.")
    }

    $isDatabaseBackupActor = $actorFile.Name -eq 'DatabaseBackupEventActor.cs'
    if ($isDatabaseBackupActor) {
        $eventTypeBlock = [regex]::Match(
            $source,
            'static readonly Type\[\] ServiceEventTypes\s*=\s*\[(?<types>[\s\S]*?)\];').Groups['types'].Value
        $parseTypes = @([regex]::Matches($eventTypeBlock, 'typeof\(([^\)]+Event)\)') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        $receiveTypes = $parseTypes
    }
    else {
        $parseTypes = @(
            @([regex]::Matches($source, 'AsEvent<([^>]+Event)>') |
                ForEach-Object { $_.Groups[1].Value }) +
            @([regex]::Matches($source, 'Parse[A-Za-z0-9_]*Event<([^>]+Event)>') |
                ForEach-Object { $_.Groups[1].Value }) |
            Where-Object { $_ -ne 'TEvent' } |
            Sort-Object -Unique)
        $receiveTypes = @([regex]::Matches($source, '\[typeof\(([^\)]+Event)\)\]\s*=') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    }

    if (Compare-Object @($parseTypes) @($receiveTypes)) {
        $violations.Add("$relativePath parse and receive event sets differ.")
    }
}

if ($domainActorCount -ne 31) {
    $violations.Add("Expected 31 domain EventActors but discovered $domainActorCount.")
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "EventActor convention verification failed with $($violations.Count) violation(s)."
}

Write-Host "EventActor convention verification passed for all $domainActorCount domain actors."
