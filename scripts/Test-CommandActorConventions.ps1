[CmdletBinding()]
param()

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$actorFiles = @(Get-ChildItem -LiteralPath $repositoryRoot -Recurse -Filter '*CommandActor.cs' -File |
    Where-Object {
        $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' -and
        $_.Name -ne 'BaseEventSourceCommandActor.cs' -and
        $_.FullName -notmatch '[\\/]Contracts[\\/]'
    })
$actorFiles += Get-Item -LiteralPath (
    Join-Path $repositoryRoot 'TomasAI.IFM.Shared.EventModelActor.Templates/CommandActorTemplate.cs')

$violations = [System.Collections.Generic.List[string]]::new()
$concreteActorCount = 0
$domainActorCount = 0

foreach ($actorFile in $actorFiles) {
    $source = [IO.File]::ReadAllText($actorFile.FullName)
    if ($source -notmatch 'BaseEventSourceCommandActor\s*<') {
        continue
    }

    $concreteActorCount++
    $relativePath = $actorFile.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')

    if ($source -notmatch '_parseMap') {
        $violations.Add("$relativePath does not declare _parseMap.")
    }
    if ($source -notmatch 'IReadOnlyDictionary<string, Func<IActorMessage, ICommand>>\s+_parseMap') {
        $violations.Add("$relativePath does not expose a read-only parse map.")
    }
    if ($source -notmatch '=>\s*ParseMappedCommand\(context, message, _parseMap\);') {
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
        if ($source -notmatch 'IReadOnlyDictionary<Type,\s*Func<ICommand,\s*List<ValidationError>>>\s+_validationMap') {
            $violations.Add("$relativePath does not expose an exact-type read-only validation map.")
        }
        if ($source -match 'Dictionary<string,\s*(?:Action|Func)<ICommand[^\r\n]*>\s+_validationMap') {
            $violations.Add("$relativePath retains a string-keyed or action validation map.")
        }
        if ($source -notmatch 'ValidateMappedCommand\(\s*(?:cmd|command),\s*_validationMap\s*\)') {
            $violations.Add("$relativePath does not delegate validation dispatch to ValidateMappedCommand.")
        }
        if ($source -notmatch 'IReadOnlyDictionary<Type,[\s\S]{0,700}?_receiveMap') {
            $violations.Add("$relativePath does not expose an exact-type read-only receive map.")
        }
        if ($source -notmatch 'ResolveMappedCommandHandler\(\s*(?:cmd|command),\s*_receiveMap\s*\)') {
            $violations.Add("$relativePath does not delegate receive dispatch to ResolveMappedCommandHandler.")
        }

        if (-not $isTemplate) {
            $isDatabaseBackupActor = $actorFile.Name -eq 'DatabaseBackupCommandActor.cs'
            if ($isDatabaseBackupActor) {
                $commandTypeBlock = [regex]::Match(
                    $source,
                    'static readonly Type\[\] CommandTypes\s*=\s*\[(?<types>[\s\S]*?)\];').Groups['types'].Value
                $parseTypes = @([regex]::Matches($commandTypeBlock, 'typeof\(([^\)]+Command)\)') |
                    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            }
            else {
                $parseTypes = @([regex]::Matches($source, 'AsCommand<([^>]+)>') |
                    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            }
            $validationTypes = @([regex]::Matches($source, '\[typeof\(([^\)]+Command)\)\]\s*=') |
                ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            $receiveTypes = if ($isDatabaseBackupActor) {
                $parseTypes
            }
            else {
                @([regex]::Matches(
                    $source,
                    '\[typeof\((?<type>[^\)]+Command)\)\]\s*=') |
                    ForEach-Object { $_.Groups['type'].Value } | Sort-Object -Unique)
            }
            $parseTypes = @($parseTypes)
            $validationTypes = @($validationTypes)
            $receiveTypes = @($receiveTypes)

            if (Compare-Object $parseTypes $validationTypes) {
                $violations.Add("$relativePath parse and validation command sets differ.")
            }
            if (Compare-Object $parseTypes $receiveTypes) {
                $violations.Add("$relativePath parse and receive command sets differ.")
            }

            $commandIdCalls = [regex]::Matches($source, '\.ValidateCommandId\s*\(').Count
            if ($commandIdCalls -lt $parseTypes.Count) {
                $violations.Add("$relativePath does not visibly validate CommandId for every command.")
            }
            $entityIdCalls = [regex]::Matches(
                $source,
                '\.Validate[A-Za-z0-9_]*Id\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\.EntityId').Count
            if (-not $isDatabaseBackupActor -and $entityIdCalls -lt $parseTypes.Count) {
                $violations.Add("$relativePath does not visibly validate EntityId for every command.")
            }
        }
    }
}

if ($domainActorCount -ne 37) {
    $violations.Add("Expected 37 domain CommandActors but discovered $domainActorCount.")
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "CommandActor convention verification failed with $($violations.Count) violation(s)."
}

Write-Host "CommandActor convention verification passed for all $domainActorCount domain actors ($concreteActorCount concrete/template actors inspected)."
