[CmdletBinding()]
param()

$root = Split-Path -Parent $PSScriptRoot
$roles = 'Command', 'Query', 'Event', 'Realtime', 'Function'
$violations = [System.Collections.Generic.List[string]]::new()
$actors = 0
$handlers = 0

foreach ($project in @(Get-ChildItem -LiteralPath $root -Directory -Filter 'TomasAI.IFM.Domain.*')) {
    foreach ($file in @(Get-ChildItem -LiteralPath $project.FullName -Recurse -Filter '*Actor.cs' -File)) {
        if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
        $actorDirectory = Split-Path -Parent $file.FullName
        if ((Split-Path -Leaf $actorDirectory) -ne 'Actor') { continue }
        $roleDirectory = Split-Path -Parent $actorDirectory
        if ((Split-Path -Leaf $roleDirectory) -notin $roles) { continue }
        $source = [IO.File]::ReadAllText($file.FullName)
        $relativeDirectory = $actorDirectory.Substring($project.FullName.Length).TrimStart('\', '/')
        $expectedNamespace = $project.Name + '.' + $relativeDirectory.Replace('\', '.')
        $namespace = [regex]::Match($source, '(?m)^namespace\s+(?<name>[^;]+);')
        if (-not $namespace.Success -or $namespace.Groups['name'].Value.Trim() -ne $expectedNamespace) {
            $violations.Add("$($file.FullName): actor namespace must be $expectedNamespace.")
        }
        $mapStart = $source.IndexOf('_receiveMap', [StringComparison]::Ordinal)
        if ($mapStart -lt 0) {
            $mapStart = $source.IndexOf('receiveMap', [StringComparison]::Ordinal)
        }
        if ($mapStart -lt 0) {
            $mapStart = $source.IndexOf('ReceiveMap', [StringComparison]::Ordinal)
        }
        if ($mapStart -lt 0) { continue }
        $open = $source.IndexOf('{', $mapStart)
        if ($open -lt 0) {
            $violations.Add("$($file.FullName): _receiveMap has no initializer.")
            continue
        }
        $depth = 0
        $close = -1
        for ($i = $open; $i -lt $source.Length; $i++) {
            if ($source[$i] -eq '{') { $depth++ }
            elseif ($source[$i] -eq '}') {
                $depth--
                if ($depth -eq 0) { $close = $i; break }
            }
        }
        if ($close -lt 0) {
            $violations.Add("$($file.FullName): _receiveMap initializer is unbalanced.")
            continue
        }
        $actors++
        $map = $source.Substring($open, $close - $open + 1)
        $types = @([regex]::Matches($map, '\[typeof\((?<name>\w+)\)\]') |
            ForEach-Object { $_.Groups['name'].Value } | Sort-Object -Unique)
        foreach ($type in $types) {
            $handler = $type -replace '(RealtimeEvent|Command|Query|Event)$', ''
            $path = Join-Path $roleDirectory "$handler.cs"
            $handlers++
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                $violations.Add("$($file.FullName): $type requires $path.")
                continue
            }
            $handlerSource = [IO.File]::ReadAllText($path)
            if ($handlerSource -notmatch "\bclass\s+$([regex]::Escape($handler))\b") {
                $violations.Add("$path does not declare handler class $handler.")
            }
            $handlerNamespace = [regex]::Match($handlerSource, '(?m)^namespace\s+(?<name>[^;]+);')
            $relativeRole = $roleDirectory.Substring($project.FullName.Length).TrimStart('\', '/')
            $expectedHandlerNamespace = $project.Name + '.' + $relativeRole.Replace('\', '.')
            if (-not $handlerNamespace.Success -or
                $handlerNamespace.Groups['name'].Value.Trim() -ne $expectedHandlerNamespace) {
                $violations.Add("$path handler namespace must be $expectedHandlerNamespace.")
            }
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "Mapped handler ownership failed with $($violations.Count) violation(s)."
}
Write-Host "Mapped handler ownership passed for $handlers handlers in $actors domain actors."
