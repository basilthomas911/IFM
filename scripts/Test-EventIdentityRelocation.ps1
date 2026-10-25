# Uses separate processes so the relocated contract cannot accidentally resolve the old assembly.
[CmdletBinding()]
param([string]$ArtifactsPath = "$PSScriptRoot/../.artifacts/event-identity-relocation")
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = [System.IO.Path]::GetFullPath($ArtifactsPath)
foreach ($name in @('Original','Relocated')) {
    dotnet build "$repo/test-support/EventIdentityRelocation/$name/$name.csproj" --artifacts-path $artifacts
    if ($LASTEXITCODE -ne 0) { throw "Fixture build failed: $name" }
}
$original = "$artifacts/bin/Original/debug/IdentityFixture.Original.dll"
$relocated = "$artifacts/bin/Relocated/debug/IdentityFixture.Relocated.dll"
foreach ($compressed in @('False','True')) {
    $before = "$artifacts/before-$compressed.bin"
    $after = "$artifacts/after-$compressed.bin"
    dotnet $original write $before $compressed
    if ($LASTEXITCODE -ne 0) { throw 'Original writer failed' }
    dotnet $relocated $before $after $compressed
    if ($LASTEXITCODE -ne 0) { throw 'Relocated reader/writer failed' }
    dotnet $original read $after $compressed
    if ($LASTEXITCODE -ne 0) { throw 'Old reader could not read the relocated writer payload' }
    Write-Output "PASS: actual assembly/namespace relocation; legacy/stable/current reads; unchanged identity/bytes; old-reader rollback; LZ4=$compressed"
}
