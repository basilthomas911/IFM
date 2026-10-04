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
        if ($source -match 'CaptureCommandValidation\s*\(') {
            $violations.Add("$relativePath wraps domain validation in exception capture; use aggregate list extensions.")
        }
        if ($source -match 'static\s+void\s+Validate(?:Identity|Common|Create|Load|TradeEntityId)\s*\(') {
            $violations.Add("$relativePath owns domain validation; move checks into Command/Validation extensions.")
        }

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

            # The map is a manifest: each entry must show its CommandId validation.
            $mapAssignment = [regex]::Match($source, '_validationMap\s*=')
            if ($mapAssignment.Success) {
                $mapStart = $source.IndexOf('{', $mapAssignment.Index + $mapAssignment.Length)
                $depth = 1
                $mapEnd = $mapStart + 1
                while ($depth -gt 0 -and $mapEnd -lt $source.Length) {
                    if ($source[$mapEnd] -eq '{') { $depth++ }
                    elseif ($source[$mapEnd] -eq '}') { $depth-- }
                    $mapEnd++
                }
                $manifest = $source.Substring($mapStart, $mapEnd - $mapStart)
                $entryCount = [regex]::Matches($manifest, '\[typeof\(').Count
                $commandIdCount = [regex]::Matches($manifest, '\.ValidateCommandId\(').Count
                if ($entryCount -ne $commandIdCount) {
                    $violations.Add("$relativePath hides CommandId validation in one or more map entries.")
                }
            }
            # Runtime regression tests verify aggregation and null payload behavior.
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "CommandActor convention verification failed with $($violations.Count) violation(s)."
}

Write-Host "CommandActor convention verification passed for all $domainActorCount domain actors ($concreteActorCount concrete/template actors inspected)."
