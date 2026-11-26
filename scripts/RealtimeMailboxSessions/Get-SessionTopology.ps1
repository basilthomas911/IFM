[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$processes = @(& (Join-Path $repository 'scripts/Development/Get-IFMDevelopmentProcess.ps1') -RepositoryRoot $repository)
$ports = @()
foreach ($item in $processes) {
    $ports += @(Get-NetTCPConnection -OwningProcess $item.ProcessId -State Listen -ErrorAction SilentlyContinue |
        Select-Object LocalAddress, LocalPort, OwningProcess)
}
$health = @()
$urls = [Collections.Generic.HashSet[string]]::new()
foreach ($entry in $ports) {
    if (@($processes | Where-Object { $_.ProcessId -eq $entry.OwningProcess -and $_.Role -in @('api','market-data') }).Count -gt 0) {
        [void]$urls.Add("http://127.0.0.1:$($entry.LocalPort)/health/ready")
    }
}
foreach ($url in $urls) {
    try {
        $reply = Invoke-WebRequest -Uri $url -TimeoutSec 5 -UseBasicParsing
        $health += @{ url=$url; status=[int]$reply.StatusCode; observedUtc=[DateTimeOffset]::UtcNow.ToString('o') }
    } catch {
        $response = $_.Exception.PSObject.Properties['Response']
        $status = if ($null -ne $response -and $null -ne $response.Value) { [int]$response.Value.StatusCode } else { 0 }
        $health += @{ url=$url; status=$status; observedUtc=[DateTimeOffset]::UtcNow.ToString('o') }
    }
}
$containers = @()
try {
    $lines = @(& docker ps --format '{{.ID}}|{{.Names}}|{{.Image}}|{{.Ports}}')
    if ($LASTEXITCODE -eq 0) {
        foreach ($line in $lines) {
            $parts = $line.Split('|',4)
            if ($parts.Length -eq 4 -and ($parts[1].StartsWith('ifm-') -or $parts[1].StartsWith('ifm_'))) {
                $containers += @{ id=$parts[0]; name=$parts[1]; image=$parts[2]; ports=$parts[3] }
            }
        }
    }
} catch { }
$hashes = @()
foreach ($relative in @('.vscode/launch.json','TomasAI.IFM.Application.Api.Server/appsettings.json','TomasAI.IFM.Application.Api.Server/appsettings.Development.json')) {
    $path = Join-Path $repository $relative
    if (Test-Path -LiteralPath $path) { $hashes += @{ path=$relative; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } }
}
$result = @{ version=1; observedUtc=[DateTimeOffset]::UtcNow.ToString('o'); kind='ReadOnlyTopologyInventory';
    eligibleSnapshot=$false; processes=$processes; listeners=$ports; health=$health; containers=$containers; configurationHashes=$hashes;
    limitations=@('No writer pause or ownership checkpoint is proved by this inventory.','No credentials, command lines or config values were exported.') }
$absolute = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($absolute)) | Out-Null
[IO.File]::WriteAllText($absolute,($result | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
Write-Output "Read-only session topology saved. No services or configuration changed."
