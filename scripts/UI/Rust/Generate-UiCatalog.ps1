param([switch]$Check)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$catalog=Get-Content -LiteralPath "$repoRoot/TomasAI.IFM.UI.Rust/catalog/strings.en.json" -Raw -Encoding UTF8 | ConvertFrom-Json
if($catalog.version -ne 1){throw 'Unsupported catalog version'}
$keys=@($catalog.strings.PSObject.Properties.Name | Sort-Object)
$rust="// Generated from catalog/strings.en.json.`n" + (($keys | ForEach-Object { 'pub const '+$_.ToUpperInvariant()+': &str = "'+$_+'";' }) -join "`n") + "`n"
$csharp="// Generated from the shared Rust UI string catalog.`nnamespace TomasAI.IFM.UI.Rust.Bridge.Net;`npublic static class StringIds`n{`n" + (($keys | ForEach-Object { $identifier=(($_ -split '_' | ForEach-Object { $_.Substring(0,1).ToUpperInvariant()+$_.Substring(1) }) -join ''); '    public const string '+$identifier+' = "'+$_+'";' }) -join "`n") + "`n}`n"
# Compile the catalog for the managed bridge so startup does not parse a JSON document.
$compiledCatalog="// Generated from catalog/strings.en.json.`nnamespace TomasAI.IFM.UI.Rust.Bridge.Net;`nstatic class BridgeCatalogData`n{`n    public static BridgeStringCatalog Create() => new(1, "+(ConvertTo-Json -InputObject $catalog.locale -Compress)+", new()`n    {`n" + (($keys | ForEach-Object { '        ['+(ConvertTo-Json -InputObject $_ -Compress)+'] = '+(ConvertTo-Json -InputObject $catalog.strings.$_ -Compress)+',' }) -join "`n") + "`n    });`n}`n"
foreach($entry in @(@('TomasAI.IFM.UI.Rust/src/localization/strings.rs',$rust),@('TomasAI.IFM.UI.Rust.Bridge.Net/StringIds.cs',$csharp),@('TomasAI.IFM.UI.Rust.Bridge.Net/BridgeCatalogData.cs',$compiledCatalog))){
 $path=Join-Path $repoRoot $entry[0]
 if($Check){if([IO.File]::ReadAllText($path).Replace("`r`n","`n") -ne $entry[1]){throw "Stale generated catalog: $path"}}
 else{[IO.File]::WriteAllText($path,$entry[1],[Text.UTF8Encoding]::new($false))}
}
