[CmdletBinding()]
param()

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$actorFiles = @(Get-ChildItem -LiteralPath $repositoryRoot -Directory -Filter 'TomasAI.IFM.Domain.*' |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter '*QueryActor.cs' -File |
            Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }
    })
$actorFiles += Get-Item -LiteralPath (
    Join-Path $repositoryRoot 'TomasAI.IFM.Shared.EventModelActor.Templates/QueryActorTemplate.cs')

$violations = [System.Collections.Generic.List[string]]::new()
$domainActorCount = 0
$concreteActorCount = 0

foreach ($actorFile in $actorFiles) {
    $source = [IO.File]::ReadAllText($actorFile.FullName)
    if ($source -notmatch 'BaseQueryActor\s*<') {
        continue
    }

    $concreteActorCount++
    $relativePath = $actorFile.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    $isTemplate = $actorFile.Name -eq 'QueryActorTemplate.cs'
    if (-not $isTemplate) {
        $domainActorCount++
    }

    $parseMapType = if ($isTemplate) { '(?:IReadOnlyDictionary|Dictionary)' } else { 'IReadOnlyDictionary' }
    $receiveMapType = if ($isTemplate) { '(?:IReadOnlyDictionary|Dictionary)' } else { 'IReadOnlyDictionary' }
    if ($source -notmatch "$parseMapType<string,\s*Func<IActorMessage,\s*IQuery>>\s+_parseMap") {
        $violations.Add("$relativePath does not expose a verb-keyed _parseMap.")
    }
    if ($source -notmatch "$receiveMapType<Type,[\s\S]{0,500}?_receiveMap") {
        $violations.Add("$relativePath does not expose an exact-Type _receiveMap.")
    }
    if ($source -notmatch 'IReadOnlyDictionary<Type,\s*QueryExceptionHandler>\s+_exceptionMap') {
        $violations.Add("$relativePath does not expose an exact-Type _exceptionMap.")
    }
    if ($source -notmatch 'CreateQueryExceptionMap\(\s*_receiveMap\.Keys') {
        $violations.Add("$relativePath does not derive _exceptionMap from the receive-map manifest.")
    }
    if ($source -notmatch 'ParseMappedQuery\(\s*context,\s*message,\s*_parseMap\s*\)') {
        $violations.Add("$relativePath does not delegate parsing to ParseMappedQuery.")
    }
    if ($source -notmatch 'ResolveMappedQueryHandler\(\s*query,\s*_receiveMap\s*\)') {
        $violations.Add("$relativePath does not delegate receive dispatch to ResolveMappedQueryHandler.")
    }
    if ($source -notmatch 'ExceptionMappedQueryAsync\(\s*context,\s*threadId,\s*query,\s*verb,\s*exception,\s*_exceptionMap\s*\)') {
        $violations.Add("$relativePath does not delegate exception dispatch to ExceptionMappedQueryAsync.")
    }
    if ($source -match '_receiveMap\.TryGetValue' -or $source -match 'switch\s*\(\s*query\s*\)') {
        $violations.Add("$relativePath retains actor-owned receive routing.")
    }
    if ($source -match '_receiveMap[\s\S]{0,200}?Dictionary<string') {
        $violations.Add("$relativePath retains a string-keyed receive map.")
    }

    if (-not $isTemplate) {
        if ($actorFile.Name -eq 'DatabaseBackupQueryActor.cs') {
            $routeBlock = [regex]::Match(
                $source,
                'static readonly \(Type QueryType, Type ResultType\)\[\] QueryRoutes\s*=\s*\[(?<routes>[\s\S]*?)\];').Groups['routes'].Value
            $parseTypes = @([regex]::Matches($routeBlock, 'typeof\(([^\)]+Query)\)') |
                ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        }
        else {
            $parseTypes = @([regex]::Matches($source, 'AsQuery<\s*([^,>]+Query)\s*,') |
                ForEach-Object { $_.Groups[1].Value.Trim() } | Sort-Object -Unique)
        }
        $receiveTypes = @([regex]::Matches($source, '\[typeof\(([^\)]+Query)\)\]\s*=') |
            ForEach-Object { $_.Groups[1].Value.Trim() } | Sort-Object -Unique)

        if ($parseTypes.Count -eq 0) {
            $violations.Add("$relativePath does not expose any concrete parsed query types.")
        }
        if (Compare-Object $parseTypes $receiveTypes) {
            $violations.Add("$relativePath parse and receive query sets differ.")
        }
    }
}

if ($domainActorCount -ne 36) {
    $violations.Add("Expected 36 domain QueryActors but discovered $domainActorCount.")
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "QueryActor convention verification failed with $($violations.Count) violation(s)."
}

Write-Host "QueryActor convention verification passed for all $domainActorCount domain actors ($concreteActorCount domain/template actors inspected)."
