[CmdletBinding()]
param()

$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$targets = @(
    @{
        Name = "TomasAI.IFM.UI.Net"
        Root = [System.IO.Path]::GetFullPath(
            (Join-Path $workspace "TomasAI.IFM.UI.Net\bin\Debug"))
    },
    @{
        Name = "TomasAI.IFM.Application.Api.Server"
        Root = [System.IO.Path]::GetFullPath(
            (Join-Path $workspace "TomasAI.IFM.Application.Api.Server\bin\Debug"))
    }
)

foreach ($target in $targets) {
    $processes = Get-Process -Name $target.Name -ErrorAction SilentlyContinue
    foreach ($process in $processes) {
        $path = $process.Path
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        $resolvedPath = [System.IO.Path]::GetFullPath($path)
        if (!$resolvedPath.StartsWith(
                $target.Root,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "Skipping $($process.ProcessName) ($($process.Id)); it is not a repository Debug process."
            continue
        }

        Write-Host "Stopping $($process.ProcessName) ($($process.Id)) from $resolvedPath"
        Stop-Process -Id $process.Id
    }
}
