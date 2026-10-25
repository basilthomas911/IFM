param([Parameter(Mandatory=$true)][string]$StandaloneExe)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$exe=(Resolve-Path -LiteralPath $StandaloneExe).Path
$identity=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$output=Join-Path $repoRoot ('.artifacts/standalone-desktop-suite/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$fixtureSnapshot=Join-Path $output 'fixtures'
# Keep paired runs independent of an overlapping contract/test export.
Copy-Item -LiteralPath (Join-Path $repoRoot 'TomasAI.IFM.UI.Rust.eGui/tests/fixtures/backend') -Destination $fixtureSnapshot -Recurse
$cases=@('reference','portfolio','portfolio-editors','system','recovery-notification','market-data','trade-workspace','broker-workspace','broker-working-orders','actor-health','operations-health','financial-reads','financial-drafts','parameter-inspection','iron-condor-monitor','client-configuration','market-outlook','strategy-dashboard','iron-condor-populated')
$results=@()
Push-Location $repoRoot
try {
 foreach($case in $cases){
  if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $identity){throw 'The executable changed during desktop acceptance'}
  $log=Join-Path $output ($case+'.log')
  $status='Passed';$errorText=''
  try { & (Join-Path $PSScriptRoot 'Test-StandaloneDesktopParity.ps1') -Scenario ('TomasAI.IFM.UI.Rust.eGui/tests/desktop/'+$case+'.json') -StandaloneExe $exe -FixtureDirectory $fixtureSnapshot *> $log }
  catch {$status='Failed';$errorText=$_.Exception.Message}
  $evidence=Select-String -LiteralPath $log -Pattern '^Desktop evidence: (.+)$' | Select-Object -Last 1
  $results+=@([ordered]@{scenario=$case;status=$status;log=$log;evidence=if($evidence){$evidence.Matches[0].Groups[1].Value}else{$null};error=$errorText})
  # A loaded trade must remain visible beneath independent health dialogs.
 $case='health-navigation-loaded-trade';$log=Join-Path $output ($case+'.log');$status='Passed';$errorText=''
 try {& (Join-Path $PSScriptRoot 'Test-StandaloneDesktopParity.ps1') -Variant Standalone -Scenario ('TomasAI.IFM.UI.Rust.eGui/tests/desktop/'+$case+'.json') -StandaloneExe $exe -FixtureDirectory $fixtureSnapshot *> $log}
 catch {$status='Failed';$errorText=$_.Exception.Message}
 $evidence=Select-String -LiteralPath $log -Pattern '^Desktop evidence: (.+)$' | Select-Object -Last 1
 $results+=@([ordered]@{scenario=$case;variant='Standalone';status=$status;log=$log;evidence=if($evidence){$evidence.Matches[0].Groups[1].Value}else{$null};error=$errorText;basis='Independent health dialogs above an existing live trade; closing a dialog does not reload the trade.'})
 [ordered]@{executable=$exe;sha256=$identity;cases=$results;qualification='Paired FlaUI against isolated synthetic backend services; no live broker or market qualification.'} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'suite.json') -Encoding UTF8
  Write-Output ($case+': '+$status)
 }
 # This correctness case deliberately does not reproduce the unchanged hybrid's
 # shared date-picker identity defect. Its baseline failure is documented.
 $case='financial-distinct-dates';$log=Join-Path $output ($case+'.log');$status='Passed';$errorText=''
 try {& (Join-Path $PSScriptRoot 'Test-StandaloneDesktopParity.ps1') -Variant Standalone -Scenario ('TomasAI.IFM.UI.Rust.eGui/tests/desktop/'+$case+'.json') -StandaloneExe $exe -FixtureDirectory $fixtureSnapshot *> $log}
 catch {$status='Failed';$errorText=$_.Exception.Message}
 $evidence=Select-String -LiteralPath $log -Pattern '^Desktop evidence: (.+)$' | Select-Object -Last 1
 $results+=@([ordered]@{scenario=$case;variant='Standalone';status=$status;log=$log;evidence=if($evidence){$evidence.Matches[0].Groups[1].Value}else{$null};error=$errorText;basis='Independent date selections; unchanged hybrid baseline fails this correctness case.'})
 [ordered]@{executable=$exe;sha256=$identity;cases=$results;qualification='Paired FlaUI plus explicit native correctness checks against isolated synthetic services; no live broker or market qualification.'} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'suite.json') -Encoding UTF8
 Write-Output ('Desktop suite evidence: '+$output)
 if(@($results|Where-Object status -eq 'Failed').Count){throw 'One or more desktop comparisons failed; inspect suite.json before acceptance'}
} finally {Pop-Location}
