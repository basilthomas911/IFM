param([string]$Destination=".artifacts/rust-ui/package",[switch]$Debug)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
& "$PSScriptRoot/Build-RustUi.ps1" -Release:(!$Debug)
Push-Location $repoRoot
try {
 $target=[IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
 if(-not $target.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package destination must be inside repository'}
 if(Test-Path $target){throw 'Use a new empty package destination to avoid mixing versions'}
 New-Item -ItemType Directory -Path $target | Out-Null
 Copy-Item '.artifacts/rust-ui/app/*' $target -Recurse
 Copy-Item 'TomasAI.IFM.UI.Rust/README.md' $target
 $files=Get-ChildItem $target -Recurse -File | ForEach-Object {[pscustomobject]@{Path=$_.FullName.Substring($target.Length+1);Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}
 $files | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $target 'manifest.json') -Encoding UTF8
 Write-Output "Packaged host at $target; .NET 10 x64 runtime and installed Windows Microsoft Sans Serif are required."
}finally{Pop-Location}
