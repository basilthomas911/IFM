[CmdletBinding()]
param()

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$actorFiles = @(Get-ChildItem -LiteralPath $repositoryRoot -Directory -Filter 'TomasAI.IFM.Domain.*' |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter '*FunctionActor.cs' -File |
            Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }
    })

$violations = [System.Collections.Generic.List[string]]::new()
foreach ($actorFile in $actorFiles) {
    $source = [IO.File]::ReadAllText($actorFile.FullName)
    $compact = $source -replace '\s+', ''
    $relativePath = $actorFile.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')

    if ($compact -notmatch ':BaseEventSourceFunctionActor<') {
        $violations.Add("$relativePath does not inherit BaseEventSourceFunctionActor directly.")
        continue
    }

    foreach ($helper in @(
        'ParseMappedFunction',
        'ValidateMappedCommand',
        'ResolveMappedFunctionHandler',
        'DispatchMappedFunctionEvent',
        'DispatchMappedExecutionPolicy')) {
        if ($compact -notmatch "\b$helper\(") {
            $violations.Add("$relativePath does not use $helper.")
        }
    }

    $mapNames = @(
        'ParseMap', 'ValidationMap', 'ReceiveMap', 'EventMap', 'ExecutionPolicyMap'
    )
    $helpers = @(
        'ParseMappedFunction', 'ValidateMappedCommand', 'ResolveMappedFunctionHandler',
        'DispatchMappedFunctionEvent', 'DispatchMappedExecutionPolicy'
    )
    for ($index = 0; $index -lt $helpers.Count; $index++) {
        $call = [regex]::Match($compact, "\b$($helpers[$index])\([^;]*?\)")
        if (-not $call.Success) { continue }
        $map = [regex]::Match($call.Value, ',(?<name>[A-Za-z_][A-Za-z0-9_]*)\)$')
        if (-not $map.Success) {
            $violations.Add("$relativePath has no explicit map argument for $($helpers[$index]).")
            continue
        }
        $name = $map.Groups['name'].Value
        if ($compact -notmatch "\b$([regex]::Escape($name))=newDictionary<") {
            $violations.Add("$relativePath does not declare the $($mapNames[$index]) map as an explicit dictionary.")
        }
    }
    if ([regex]::Matches($compact, '\.ToFrozenDictionary\(').Count -lt 5) {
        $violations.Add("$relativePath does not freeze all five FunctionActor maps.")
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "FunctionActor convention verification failed with $($violations.Count) violation(s)."
}

Write-Host "FunctionActor convention verification passed for all $($actorFiles.Count) domain actors."
