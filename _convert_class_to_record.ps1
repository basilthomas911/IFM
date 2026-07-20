param([switch]$DryRun)

$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }

$sharedDir = Join-Path $root "TomasAI.IFM.Shared"

# Find all .cs files with classes directly implementing IEvent or IEvent<
$matches = Get-ChildItem -Path $sharedDir -Recurse -Filter "*.cs" |
    Select-String -Pattern 'public class \w+\s*:\s*IEvent\b' |
    Select-Object -ExpandProperty Path -Unique |
    Sort-Object

Write-Host "Found $($matches.Count) files with class : IEvent" -ForegroundColor Cyan

$converted = 0
foreach ($filePath in $matches) {
    $relPath = $filePath -replace [regex]::Escape("$root\"), ""
    $content = Get-Content $filePath -Raw
    $original = $content

    # Replace 'public class XXX : IEvent' with 'public record XXX : IEvent' (only direct IEvent implementors)
    $content = $content -replace '(public\s+)class(\s+\w+\s*:\s*IEvent\b)', '${1}record${2}'

    # Replace { get; set; } with { get; init; } throughout the file
    $content = $content -replace '\{\s*get;\s*set;\s*\}', '{ get; init; }'

    if ($content -ne $original) {
        $converted++
        if ($DryRun) {
            Write-Host "[DRY] Would update: $relPath" -ForegroundColor Yellow
        } else {
            Set-Content -Path $filePath -Value $content -NoNewline
            Write-Host "Updated: $relPath" -ForegroundColor Green
        }
    }
}

Write-Host "`nConverted $converted files" -ForegroundColor Cyan
