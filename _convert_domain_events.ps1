param([switch]$DryRun)

# Convert DomainEvent subclasses to IEvent pattern
$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }

$files = Get-ChildItem -Path (Join-Path $root "TomasAI.IFM.Shared") -Recurse -Filter "*.cs" |
    Select-String -Pattern "class \w+ : DomainEvent" |
    Select-Object -ExpandProperty Path -Unique |
    Sort-Object

Write-Host "Found $($files.Count) files with DomainEvent inheritance" -ForegroundColor Cyan

# Known verbs to split class names: ordered longest first to avoid partial matches
$knownVerbs = @(
    "DistributionStatisticsChanged","DistributionStatisticsUpdated","LegDataChanged","LegDataUpdated",
    "DailyProfitTargetUpdated","SpreadBarDataDeleted","SpreadBarDataInserted","SpreadDataInserted",
    "ForwardLossLimitCleared","ForwardLossLimitReachedUpdated","ForwardLossLimitUpdated",
    "ForwardLossLimitWarningUpdated","ActionUpdated","StatusUpdated",
    "EndOfDayProcessed","PositionClosed","PositionOpened","OrderPlaced",
    "ModelBuildStarted","ModelDataLoaded","ModelTrained","ModelLoaded",
    "ClassModelDataLoaded","ClassModelTrained","DeltaModelDataLoaded","DeltaModelTrained",
    "AlgorithmExecuted","ClosePosition","OpenPosition",
    "Cancelled","Executed","Snapshot","Rangebound","AddedToFund","HeldOver",
    "Streaming","Opened","Closed","Filled","Placed","Updated","Started","Stopped",
    "Changed","Removed","Deleted","Added","Hedged","Cleared","Waited","Set",
    "ToClose","ToOpen","Wait"
)

function Get-ActorVerb($className) {
    $base = $className -replace 'Event$', ''
    foreach ($verb in $knownVerbs) {
        if ($base.EndsWith($verb)) {
            $actor = $base.Substring(0, $base.Length - $verb.Length) + "Event"
            return @{ Actor = $actor; Verb = $verb }
        }
    }
    return @{ Actor = "${base}Event"; Verb = "Unknown" }
}

function Get-CamelCase($name) {
    if ($name.Length -eq 0) { return $name }
    return $name.Substring(0,1).ToLower() + $name.Substring(1)
}

function Get-CtorAssignment($type, $camelName, $pascalName, $indent) {
    if ($type -eq "string" -or $type -eq "string?") {
        return "${indent}        $pascalName = $camelName ?? string.Empty;"
    }
    return "${indent}        $pascalName = $camelName;"
}

foreach ($filePath in $files) {
    $relPath = $filePath.Replace("$root\", "")
    Write-Host "`nProcessing: $relPath" -ForegroundColor Yellow

    $content = Get-Content $filePath -Raw
    $lines = Get-Content $filePath

    # Extract namespace
    $nsMatch = [regex]::Match($content, 'namespace\s+([\w.]+)\s*;')
    $fileScopedNs = $false
    if ($nsMatch.Success) {
        $namespace = $nsMatch.Groups[1].Value
        $fileScopedNs = $true
    } else {
        $nsMatch = [regex]::Match($content, 'namespace\s+([\w.]+)\s*\{')
        if ($nsMatch.Success) { $namespace = $nsMatch.Groups[1].Value }
        else { Write-Host "  SKIP: Cannot find namespace" -ForegroundColor Red; continue }
    }

    # Extract usings
    $usings = @()
    foreach ($line in $lines) {
        if ($line -match '^\s*using\s+[\w.]+;') {
            $usings += $line.Trim()
        }
    }

    # Find the main DomainEvent class declaration
    $classMatch = [regex]::Match($content, 'public\s+class\s+(\w+)\s*:\s*DomainEvent\s*(,\s*([^\r\n{]+))?')
    if (-not $classMatch.Success) {
        Write-Host "  SKIP: Cannot parse class declaration" -ForegroundColor Red
        continue
    }
    $className = $classMatch.Groups[1].Value
    $extraInterfaces = ""
    if ($classMatch.Groups[3].Success) {
        $extraInterfaces = $classMatch.Groups[3].Value.Trim()
        $extraInterfaces = $extraInterfaces -replace ',?\s*IDenormalizerEvent\s*,?', ''
        $extraInterfaces = $extraInterfaces.Trim().Trim(',').Trim()
    }

    $av = Get-ActorVerb $className
    $actorName = $av.Actor
    $verbName = $av.Verb

    Write-Host "  Class: $className, Actor: $actorName, Verb: $verbName"

    # Find class body - locate opening brace after class declaration
    $classStartIdx = $classMatch.Index + $classMatch.Length
    $braceStart = $content.IndexOf('{', $classStartIdx)
    if ($braceStart -lt 0) {
        Write-Host "  SKIP: Cannot find class opening brace" -ForegroundColor Red
        continue
    }

    # Find matching closing brace
    $depth = 1
    $pos = $braceStart + 1
    while ($depth -gt 0 -and $pos -lt $content.Length) {
        if ($content[$pos] -eq '{') { $depth++ }
        elseif ($content[$pos] -eq '}') { $depth-- }
        $pos++
    }
    $braceEnd = $pos - 1

    $classBody = $content.Substring($braceStart + 1, $braceEnd - $braceStart - 1)

    # Content BEFORE the DomainEvent class (for files with multiple classes)
    $beforeClass = $content.Substring(0, $classMatch.Index)
    # Content AFTER the DomainEvent class closing brace
    $afterClass = $content.Substring($braceEnd + 1)

    # Check if there are other classes before the DomainEvent class
    $hasClassesBefore = $beforeClass -match 'public\s+class\s+'

    # Parse ErrorCode
    $errorCodeValue = "0"
    $errorCodeMatch = [regex]::Match($classBody, '(?:public\s+)?(?:const\s+)?int\s+ErrorCode\s*=\s*(\d+)\s*;')
    if ($errorCodeMatch.Success) {
        $errorCodeValue = $errorCodeMatch.Groups[1].Value
    }

    # Parse payload properties
    $payloadProps = @()
    $hasTypedEntityId = $false
    $typedEntityIdType = "string"

    $propMatches = [regex]::Matches($classBody, 'public\s+(new\s+)?(\S+\??\s*\[?\]?)\s+(\w+)\s*\{[^}]*get;[^}]*set;[^}]*\}(?:\s*=\s*[^;]+;)?')
    foreach ($pm in $propMatches) {
        $propType = $pm.Groups[2].Value.Trim()
        $propName = $pm.Groups[3].Value.Trim()

        if ($propName -eq "EntityId") {
            $hasTypedEntityId = $true
            $typedEntityIdType = $propType
            continue
        }

        $payloadProps += @{ Type = $propType; Name = $propName }
    }

    # Parse methods
    $methods = @()
    $methodPattern = '(?:public\s+(?:(?:virtual|override|new|static)\s+)*)(?:ICompleteEvent|IErrorEvent|void)\s+\w+\s*\([^)]*\)\s*(?:=>|{)'
    $methodMatches = [regex]::Matches($classBody, $methodPattern)
    foreach ($mm in $methodMatches) {
        $methodStart = $mm.Index
        $methodText = $classBody.Substring($methodStart)
        if ($mm.Value -match '=>') {
            $semiIdx = 0
            $mDepth = 0
            for ($i = 0; $i -lt $methodText.Length; $i++) {
                if ($methodText[$i] -eq '{') { $mDepth++ }
                elseif ($methodText[$i] -eq '}') { $mDepth-- }
                elseif ($methodText[$i] -eq ';' -and $mDepth -eq 0) { $semiIdx = $i; break }
            }
            $fullMethod = $methodText.Substring(0, $semiIdx + 1).Trim()
        } else {
            $bStart = $methodText.IndexOf('{')
            if ($bStart -ge 0) {
                $mDepth = 1
                $i = $bStart + 1
                while ($mDepth -gt 0 -and $i -lt $methodText.Length) {
                    if ($methodText[$i] -eq '{') { $mDepth++ }
                    elseif ($methodText[$i] -eq '}') { $mDepth-- }
                    $i++
                }
                $fullMethod = $methodText.Substring(0, $i).Trim()
            } else {
                $fullMethod = $mm.Value.Trim()
            }
        }
        $methods += "    $fullMethod"
    }

    # Build required usings
    $requiredUsings = @(
        "using MessagePack;",
        "using TomasAI.IFM.Shared.EventModelActor;",
        "using TomasAI.IFM.Shared.EventModelActor.Contracts;",
        "using TomasAI.IFM.Shared.EventSourcing;"
    )

    $allUsings = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($u in $usings) { [void]$allUsings.Add($u) }
    foreach ($u in $requiredUsings) { [void]$allUsings.Add($u) }
    $removeUsings = @("using System;", "using System.Collections.Generic;", "using System.Text;", "using System.Linq;", "using System.Threading.Tasks;")
    foreach ($r in $removeUsings) { [void]$allUsings.Remove($r) }
    $sortedUsings = $allUsings | Sort-Object

    # Build interface list
    $interfaceList = "IEvent"
    if ($extraInterfaces) { $interfaceList = "IEvent, $extraInterfaces" }

    $keyIdx = 8
    $indent = if ($fileScopedNs) { "" } else { "    " }
    $cachedUserStr = '    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";'

    # Build the new class content only
    $sb = [System.Text.StringBuilder]::new()

    if (-not $hasClassesBefore) {
        # Normal case: rewrite the entire file
        foreach ($u in $sortedUsings) { [void]$sb.AppendLine($u) }
        [void]$sb.AppendLine()

        if ($fileScopedNs) {
            [void]$sb.AppendLine("namespace $namespace;")
        } else {
            [void]$sb.AppendLine("namespace $namespace")
            [void]$sb.AppendLine("{")
        }
        [void]$sb.AppendLine()
    } else {
        # Edge case: preserve content before the DomainEvent class, but update usings
        # Re-inject updated usings at the top, then everything else before the class
        $beforeNoUsings = $beforeClass -replace '^\s*using\s+[\w.]+;\s*\r?\n', ''
        foreach ($u in $sortedUsings) { [void]$sb.AppendLine($u) }
        [void]$sb.Append($beforeNoUsings)
    }

    # Class declaration
    [void]$sb.AppendLine("${indent}[MessagePackObject(AllowPrivate = true)]")
    [void]$sb.AppendLine("${indent}public class $className : $interfaceList")
    [void]$sb.AppendLine("${indent}{")

    # Constants
    [void]$sb.AppendLine("${indent}    [IgnoreMember] public const string Actor = `"$actorName`";")
    [void]$sb.AppendLine("${indent}    [IgnoreMember] public const string Verb = `"$verbName`";")
    [void]$sb.AppendLine("${indent}    [IgnoreMember] public const int ErrorCode = $errorCodeValue;")
    [void]$sb.AppendLine("${indent}$cachedUserStr")
    [void]$sb.AppendLine()

    # Base metadata properties
    [void]$sb.AppendLine("${indent}    [Key(0)] public ActorSubject Subject { get; set; }")
    [void]$sb.AppendLine("${indent}    [Key(1)] public Guid Id { get; set; }")
    if ($hasTypedEntityId) {
        [void]$sb.AppendLine("${indent}    [Key(2)] public $typedEntityIdType EntityId { get; set; }")
    } else {
        [void]$sb.AppendLine("${indent}    [Key(2)] public string EntityId { get; set; }")
    }
    [void]$sb.AppendLine("${indent}    [Key(3)] public long EventId { get; set; }")
    [void]$sb.AppendLine("${indent}    [Key(4)] public Guid CommandId { get; set; }")
    [void]$sb.AppendLine("${indent}    [Key(5)] public string AggregateId { get; set; }")
    [void]$sb.AppendLine("${indent}    [Key(6)] public string EventSource { get; set; }")
    [void]$sb.AppendLine("${indent}    [Key(7)] public DateTime ReceivedOn { get; set; }")
    [void]$sb.AppendLine()

    # Payload properties
    if ($payloadProps.Count -gt 0) {
        [void]$sb.AppendLine("${indent}    // payload (keys 8..)")
        foreach ($pp in $payloadProps) {
            [void]$sb.AppendLine("${indent}    [Key($keyIdx)] public $($pp.Type) $($pp.Name) { get; set; }")
            $keyIdx++
        }
        [void]$sb.AppendLine()
    }

    # IgnoreMember computed properties
    [void]$sb.AppendLine("${indent}    [IgnoreMember] public string UserName => CachedUserName;")
    [void]$sb.AppendLine("${indent}    [IgnoreMember] public string EventName => nameof($className);")
    [void]$sb.AppendLine("${indent}    [IgnoreMember] public EventType EventType => EventType.DomainEvent;")
    [void]$sb.AppendLine()

    # Parameterless constructor
    [void]$sb.AppendLine("${indent}    public $className() { }")
    [void]$sb.AppendLine()

    # Serialization constructor
    [void]$sb.AppendLine("${indent}    [SerializationConstructor]")
    $ctorParams = @()
    $ctorParams += "${indent}        ActorSubject subject"
    $ctorParams += "${indent}        Guid id"
    if ($hasTypedEntityId) {
        $ctorParams += "${indent}        $typedEntityIdType entityId"
    } else {
        $ctorParams += "${indent}        string entityId"
    }
    $ctorParams += "${indent}        long eventId"
    $ctorParams += "${indent}        Guid commandId"
    $ctorParams += "${indent}        string aggregateId"
    $ctorParams += "${indent}        string eventSource"
    $ctorParams += "${indent}        DateTime receivedOn"
    foreach ($pp in $payloadProps) {
        $camel = Get-CamelCase $pp.Name
        $ctorParams += "${indent}        $($pp.Type) $camel"
    }

    [void]$sb.AppendLine("${indent}    public $className(")
    [void]$sb.AppendLine(($ctorParams -join ",`n") + ")")
    [void]$sb.AppendLine("${indent}    {")
    [void]$sb.AppendLine("${indent}        Subject = subject;")
    [void]$sb.AppendLine("${indent}        Id = id;")
    [void]$sb.AppendLine("${indent}        EntityId = entityId;")
    [void]$sb.AppendLine("${indent}        EventId = eventId;")
    [void]$sb.AppendLine("${indent}        CommandId = commandId;")
    [void]$sb.AppendLine("${indent}        AggregateId = aggregateId ?? string.Empty;")
    [void]$sb.AppendLine("${indent}        EventSource = eventSource ?? string.Empty;")
    [void]$sb.AppendLine("${indent}        ReceivedOn = receivedOn;")
    foreach ($pp in $payloadProps) {
        $camel = Get-CamelCase $pp.Name
        $assignment = Get-CtorAssignment $pp.Type $camel $pp.Name $indent
        [void]$sb.AppendLine($assignment)
    }
    [void]$sb.AppendLine("${indent}    }")

    # Methods
    if ($methods.Count -gt 0) {
        [void]$sb.AppendLine()
        foreach ($m in $methods) {
            [void]$sb.AppendLine("${indent}$m")
        }
    }

    [void]$sb.AppendLine("${indent}}")

    # Companion types and closing
    if ($afterClass.Trim()) {
        $companionText = $afterClass -replace ',?\s*IDenormalizerEvent', ''
        [void]$sb.Append($companionText)
    } elseif (-not $fileScopedNs) {
        [void]$sb.AppendLine("}")
        [void]$sb.AppendLine()
    }

    $newContent = $sb.ToString().TrimEnd() + "`n"

    if ($DryRun) {
        Write-Host "  [DRY RUN] Would write $($newContent.Length) chars" -ForegroundColor Green
    } else {
        Set-Content -Path $filePath -Value $newContent -NoNewline -Encoding UTF8
        Write-Host "  Written: $($newContent.Length) chars" -ForegroundColor Green
    }
}

Write-Host "`nDone! Processed $($files.Count) files." -ForegroundColor Cyan
