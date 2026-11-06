$ErrorActionPreference = 'Stop'

$root = 'C:\repos\IFM\.artifacts'
$csv = Join-Path $env:LOCALAPPDATA 'Temp\IFM-artifact-cleanup-candidates.csv'
$rows = @(Import-Csv -LiteralPath $csv)
$prefix = $root + '\'

if ($rows.Count -ne 382 -or
    @($rows.FullPath | Sort-Object -Unique).Count -ne 382) {
    throw 'Expected exactly 382 unique folders in the reviewed CSV.'
}

$rootItem = Get-Item -LiteralPath $root -Force
if (-not $rootItem.PSIsContainer -or
    ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw "Expected a regular directory at $root"
}

$buildNames = @{
    'MSBuild bin' = @('bin')
    'MSBuild obj' = @('obj')
    'Cargo build' = @('debug', 'release', 'development')
}
$running = @(Get-CimInstance Win32_Process |
    Where-Object ExecutablePath |
    Select-Object -ExpandProperty ExecutablePath)

$activity = 'Clean up IFM build artifacts'
$validated = 0
Write-Host "Validating $($rows.Count) build folders before cleanup."
$targets = @(foreach ($row in $rows) {
    $validated++
    Write-Progress -Activity $activity -Status "Validating $validated of $($rows.Count)" `
        -CurrentOperation $row.FullPath -PercentComplete (100 * ($validated - 1) / $rows.Count)
    if ($validated -eq 1 -or $validated % 10 -eq 0) {
        Write-Host "[$validated/$($rows.Count)] Validating $($row.FullPath)"
    }
    $path = [IO.Path]::GetFullPath($row.FullPath)
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not $buildNames.ContainsKey($row.type) -or
        (Split-Path -Path $path -Leaf) -notin $buildNames[$row.type]) {
        throw "Unexpected cleanup target $path"
    }

    if (-not (Test-Path -LiteralPath $path)) { continue }

    $item = Get-Item -LiteralPath $path -Force
    if (-not $item.PSIsContainer) { throw "Expected a directory at $path" }

    for ($dir = $item; $dir.FullName -ne $root; $dir = $dir.Parent) {
        if ($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Directory link in cleanup path $($dir.FullName)"
        }
    }

    $link = Get-ChildItem -LiteralPath $path -Recurse -Force -Attributes ReparsePoint |
        Select-Object -First 1
    if ($link) { throw "Linked item inside cleanup target $($link.FullName)" }

    if ($running | Where-Object {
        $_.StartsWith($path + '\', [StringComparison]::OrdinalIgnoreCase)
    }) {
        throw "A running process uses $path"
    }

    $path
})

$before = (Get-PSDrive C).Free
Write-Host "Validation complete. $($targets.Count) folders remain to remove."
try {
    for ($index = 0; $index -lt $targets.Count; $index++) {
        $path = $targets[$index]
        $number = $index + 1
        Write-Progress -Activity $activity -Status "Removing $number of $($targets.Count)" `
            -CurrentOperation $path -PercentComplete (100 * $index / $targets.Count)
        Write-Host "[$number/$($targets.Count)] Removing $path"
        Remove-Item -LiteralPath $path -Recurse -Force
    }
} finally {
    Write-Progress -Activity $activity -Completed
}
$after = (Get-PSDrive C).Free

'Free space on C: {0:N1} GiB before, {1:N1} GiB after' -f ($before / 1GB), ($after / 1GB)
