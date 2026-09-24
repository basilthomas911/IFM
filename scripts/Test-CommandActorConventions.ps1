[CmdletBinding()]
param()

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$actorFiles = @(Get-ChildItem -LiteralPath $repositoryRoot -Directory -Filter 'TomasAI.IFM.Domain.*' |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter '*CommandActor.cs' -File |
            Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }
    })
$actorFiles += Get-Item -LiteralPath (
    Join-Path $repositoryRoot 'TomasAI.IFM.Shared.EventModelActor.Templates/CommandActorTemplate.cs')

$violations = [System.Collections.Generic.List[string]]::new()
$concreteActorCount = 0
$domainActorCount = 0

foreach ($actorFile in $actorFiles) {
    $source = [IO.File]::ReadAllText($actorFile.FullName)
    if ($source -notmatch 'Base(?:InMemory)?EventSourceCommandActor\s*<') {
        continue
    }
    $compact = $source -replace '\s+', ''

    $concreteActorCount++
    $relativePath = $actorFile.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')

    if ($source -notmatch '_parseMap') {
        $violations.Add("$relativePath does not declare _parseMap.")
    }
    if ($compact -notmatch 'IReadOnlyDictionary<string,Func<IActorMessage,ICommand>>_parseMap') {
        $violations.Add("$relativePath does not expose a read-only parse map.")
    }
    if ($compact -notmatch 'ParseMappedCommand\([^;]*,_parseMap\)') {
        $violations.Add("$relativePath does not delegate ParseMessage to ParseMappedCommand.")
    }
    if ($source -match 'CommandAuditTracker') {
        $violations.Add("$relativePath owns a forbidden domain-local CommandAuditTracker.")
    }
    if ($source -match 'InsertCommandLogAsync\s*\(') {
        $violations.Add("$relativePath writes the command audit log directly.")
    }

    $isTemplate = $actorFile.Name -eq 'CommandActorTemplate.cs'
    $isDomainActor = $relativePath -match '^TomasAI\.IFM\.Domain\.'
    if ($isDomainActor) {
        $domainActorCount++
    }
    if ($isDomainActor -or $isTemplate) {
        if ($compact -notmatch 'IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>>_validationMap') {
            $violations.Add("$relativePath does not expose an exact-type read-only validation map.")
        }
        if ($source -match 'Dictionary<string,\s*(?:Action|Func)<ICommand[^\r\n]*>\s+_validationMap') {
            $violations.Add("$relativePath retains a string-keyed or action validation map.")
        }
        if ($compact -notmatch 'ValidateMappedCommand\([^;]*,_validationMap\)') {
            $violations.Add("$relativePath does not delegate validation dispatch to ValidateMappedCommand.")
        }
        if ($compact -notmatch 'IReadOnlyDictionary<Type,.{0,2000}?_receiveMap') {
            $violations.Add("$relativePath does not expose an exact-type read-only receive map.")
        }
        if ($compact -notmatch 'ResolveMappedCommandHandler\([^;]*,_receiveMap\)') {
            $violations.Add("$relativePath does not delegate receive dispatch to ResolveMappedCommandHandler.")
        }

        if (-not $isTemplate) {
            $parseTypes = @([regex]::Matches($source, 'AsCommand<([^>]+)>') |
                ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            $validationTypes = @([regex]::Matches($source, '\[typeof\(([^\)]+Command)\)\]\s*=') |
                ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            $receiveTypes = @([regex]::Matches(
                $source,
                '\[typeof\((?<type>[^\)]+Command)\)\]\s*=') |
                ForEach-Object { $_.Groups['type'].Value } | Sort-Object -Unique)
            $parseTypes = @($parseTypes)
            $validationTypes = @($validationTypes)
            $receiveTypes = @($receiveTypes)

            if (Compare-Object $parseTypes $validationTypes) {
                $violations.Add("$relativePath parse and validation command sets differ.")
            }
            if (Compare-Object $parseTypes $receiveTypes) {
                $violations.Add("$relativePath parse and receive command sets differ.")
            }

            # Validation helpers may check identifiers for an entire command family.
            # Per-entry validation belongs in focused tests, not a call-count regex.
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "CommandActor convention verification failed with $($violations.Count) violation(s)."
}

Write-Host "CommandActor convention verification passed for all $domainActorCount domain actors ($concreteActorCount concrete/template actors inspected)."
