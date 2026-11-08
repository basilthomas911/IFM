[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$ProjectDirectory,
    [string]$CacheRoot = 'E:\IFM\RustBuildCache'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\')
$projectPath = (Resolve-Path -LiteralPath $ProjectDirectory).Path.TrimEnd('\')
$repositoryPrefix = $repositoryRoot + '\'
if (!$projectPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    !(Test-Path -LiteralPath (Join-Path $projectPath 'Cargo.toml'))) {
    throw 'A Rust project inside this repository is required.'
}
$cachePath = [IO.Path]::GetFullPath($CacheRoot).TrimEnd('\')
if ([IO.Path]::GetPathRoot($cachePath) -ne 'E:\') { throw 'Rust build caches must reside on E:.' }
$relativeProject = $projectPath.Substring($repositoryPrefix.Length)
$source = [IO.Path]::GetFullPath((Join-Path $projectPath 'target'))
$destination = [IO.Path]::GetFullPath((Join-Path (Join-Path $cachePath $relativeProject) 'target'))
if (!$destination.StartsWith($cachePath + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved target escaped the build-cache root.'
}
if ($env:CARGO_TARGET_DIR -and !([IO.Path]::GetFullPath($env:CARGO_TARGET_DIR)).StartsWith(
    $cachePath + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'CARGO_TARGET_DIR overrides the project target outside the E: build cache. Clear or correct it first.'
}
if (Test-Path -LiteralPath $source) {
    $existing = Get-Item -LiteralPath $source -Force
    if ($existing.LinkType -eq 'Junction' -and [IO.Path]::GetFullPath([string]$existing.Target).TrimEnd('\') -eq $destination) {
        Write-Host "Rust build target on E: $relativeProject"
        return
    }
    throw "Existing target requires an explicit verified migration: $source"
}
if (!$PSCmdlet.ShouldProcess($source, "Create a junction to $destination")) { return }
New-Item -ItemType Directory -Force -Path $destination | Out-Null
New-Item -ItemType Junction -Path $source -Target $destination | Out-Null
Write-Host "Created E: build target for $relativeProject"
