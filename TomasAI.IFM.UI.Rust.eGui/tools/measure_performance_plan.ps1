param([string]$Artifacts = '', [switch]$SkipBuild)
$ErrorActionPreference = 'Continue'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$app = Join-Path $repo 'TomasAI.IFM.UI.Rust.eGui'
if (-not $Artifacts) { $Artifacts = Join-Path $repo '.artifacts/egui-performance-plan-20261031' }
$Artifacts = [IO.Path]::GetFullPath($Artifacts)
New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
$keys=@('IFM_PERF_REVERSE','IFM_PERFORMANCE_PLAN_REPORT','IFM_TRADE_FRAME_BENCHMARK','IFM_PORTFOLIO_BENCHMARK')
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
Push-Location $app
try {
 if (-not $SkipBuild) {
  & cargo test --jobs 1 --locked --profile development --test performance_plan --test trade_blotter --test portfolio_timing --no-run --message-format=json 1> (Join-Path $Artifacts 'timing-build.jsonl') 2> (Join-Path $Artifacts 'timing-build.log')
  if ($LASTEXITCODE -ne 0) { throw 'Performance-plan timing build failed' }
  $built = Get-Content (Join-Path $Artifacts 'timing-build.jsonl') | ForEach-Object { if ($_.StartsWith('{')) { $_ | ConvertFrom-Json } }
  foreach ($pair in @(@('performance_plan','plan-after.exe'),@('trade_blotter','frames-after-plan.exe'),@('portfolio_timing','portfolio-after-plan.exe'))) {
   $exe = ($built | Where-Object {$_.reason -eq 'compiler-artifact' -and $_.target.name -eq $pair[0] -and $_.executable} | Select-Object -Last 1).executable
   if (-not $exe) { throw ('Missing executable: ' + $pair[0]) }
   Copy-Item -LiteralPath $exe -Destination (Join-Path $Artifacts $pair[1]) -Force
  }
 }
 foreach ($name in @('plan-after.exe','frames-before-plan.exe','frames-after-plan.exe','portfolio-before-plan.exe','portfolio-after-plan.exe')) {
  if (-not (Test-Path -LiteralPath (Join-Path $Artifacts $name))) { throw ('Required preserved executable is missing: ' + $name) }
 }
 $quietDeadline=[DateTime]::UtcNow.AddMinutes(5)
 while(Get-Process rustc,link -ErrorAction SilentlyContinue){if([DateTime]::UtcNow -gt $quietDeadline){throw 'Compiler work did not quiesce before measurements'};Start-Sleep -Seconds 1}
 & (Join-Path $Artifacts 'plan-after.exe') --skip benchmark_performance_plan --test-threads=1 *> (Join-Path $Artifacts 'optimized-parity.log')
 if ($LASTEXITCODE -ne 0) { throw 'Optimized parity tests failed' }
 for ($i=1; $i -le 5; $i++) {
  if ($i%2 -eq 0) {$env:IFM_PERF_REVERSE='1'} else {[Environment]::SetEnvironmentVariable('IFM_PERF_REVERSE',$null,'Process')}
  $env:IFM_PERFORMANCE_PLAN_REPORT=Join-Path $Artifacts ("models-pair-$i.json")
  & (Join-Path $Artifacts 'plan-after.exe') --ignored --exact benchmark_performance_plan --nocapture --test-threads=1 *> (Join-Path $Artifacts ("models-pair-$i.log"))
  if ($LASTEXITCODE -ne 0) {throw "Model timing or parity failed: pair $i"}
  $order=if($i%2 -eq 1){@('before','after')}else{@('after','before')}
  foreach ($side in $order) {
   $env:IFM_TRADE_FRAME_BENCHMARK=Join-Path $Artifacts ("frames-$side-$i.json")
   & (Join-Path $Artifacts ("frames-$side-plan.exe")) --ignored --exact benchmark_trade_view_mouse_frames --nocapture --test-threads=1 *> (Join-Path $Artifacts ("frames-$side-$i.log"))
   if($LASTEXITCODE -ne 0){throw "Frame timing failed: $side $i"}
   $env:IFM_PORTFOLIO_BENCHMARK=Join-Path $Artifacts ("portfolio-$side-$i.json")
   & (Join-Path $Artifacts ("portfolio-$side-plan.exe")) --ignored --exact benchmark_portfolio_publication --nocapture --test-threads=1 *> (Join-Path $Artifacts ("portfolio-$side-$i.log"))
   if($LASTEXITCODE -ne 0){throw "Portfolio timing failed: $side $i"}
  }
 }
 & python (Join-Path $PSScriptRoot 'summarize_performance_plan.py') --artifacts $Artifacts
 if($LASTEXITCODE -ne 0){throw 'Performance-plan report validation failed'}
} finally {
 foreach ($key in $keys) {[Environment]::SetEnvironmentVariable($key,$prior[$key],'Process')}
 Pop-Location
}
