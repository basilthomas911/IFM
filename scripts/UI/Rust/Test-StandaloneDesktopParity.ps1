param(
 [Parameter(Mandatory=$true)][string]$Scenario,
 [ValidateSet('Both','Hybrid','Standalone')][string]$Variant='Both',
 [string]$StandaloneExe='.artifacts/standalone-rust-ui/app/TomasAI.IFM.UI.Rust.eGui.exe',
 [string]$FixtureDirectory='TomasAI.IFM.UI.Rust.eGui/tests/fixtures/backend',
 [string]$ContractTool='.artifacts/standalone-contract-tool/bin/ContractFixtures/debug/ContractFixtures.dll',
 [switch]$PrepareHybrid
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Push-Location $repoRoot
$container='ifm-desktop-parity-'+[Guid]::NewGuid().ToString('N')
$worker=$null;$containerStarted=$false
$keys=@('IFM_UI_NATS_URL','IFM_UI_NATS_CREDENTIALS','IFM_UI_NATS_TOKEN','IFM_UI_REQUEST_TIMEOUT_SECONDS','IFM_UI_MARKETDATA_NATS_URL','IFM_UI_MARKETDATA_SCOPE','IFM_UI_ACTOR_HEALTH_URL','IFM_UI_OPERATIONS_HEALTH_URL','IFM_RUST_UI_MODE','DOTNET_ENVIRONMENT','IFM_STANDALONE_DESKTOP_HTTP_URL','IFM_STANDALONE_ORDER_JOURNAL','SupervisorControl__Enabled','SupervisorControl__Requester','SupervisorControl__Timeout','LOCALAPPDATA','IFM_UI_BROKER_ACCOUNT_ALIAS','IFM_UI_BROKER_ENVIRONMENT','IFM_UI_OPERATOR','IFM_STANDALONE_DESKTOP_EXPECT_NATIVE')
$keys+=@('IFM_STANDALONE_DESKTOP_CONTROL_ORACLE','IFM_STANDALONE_DESKTOP_OPERATIONAL_CLOCK','IFM_UI_CONFIG_PATH','IFM_STANDALONE_DESKTOP_CONFIG_ONLY')
$keys+='IFM_STANDALONE_DESKTOP_STRATEGY_CLOCK_UTC'
$prior=@{};foreach($key in $keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
function Invoke-Checked([string]$Program,[string[]]$Arguments){
 $previous=$ErrorActionPreference;$ErrorActionPreference='Continue'
 try{& $Program @Arguments 2>&1 | ForEach-Object {$_.ToString()};$code=$LASTEXITCODE}finally{$ErrorActionPreference=$previous}
 if($code -ne 0){throw "$Program failed with exit code $code"}
}
try{
 $spec=Get-Content -LiteralPath $Scenario -Raw -Encoding UTF8 | ConvertFrom-Json
 $env:IFM_STANDALONE_DESKTOP_CONFIG_ONLY=if($spec.clientConfigurationOnly){'1'}else{'0'}
 $env:IFM_STANDALONE_DESKTOP_OPERATIONAL_CLOCK=if($spec.operationalClock){'1'}else{'0'}
 $env:IFM_STANDALONE_DESKTOP_STRATEGY_CLOCK_UTC=if($spec.strategyClock){[DateTime]::UtcNow.AddMinutes(-2).ToString('O')}else{$null}
 $scenarioName=[IO.Path]::GetFileNameWithoutExtension($Scenario)
 if($scenarioName -notmatch '^[A-Za-z0-9_-]+$'){throw 'Invalid scenario name'}
 $run=Join-Path $repoRoot ('.artifacts/standalone-desktop-parity/'+$scenarioName+'-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $run -Force | Out-Null
 $steps=Join-Path $run 'steps.json';ConvertTo-Json -InputObject @($spec.steps) -Depth 20 | Set-Content -LiteralPath $steps -Encoding UTF8
 $oracle=(Resolve-Path -LiteralPath $ContractTool).Path
 $env:IFM_STANDALONE_DESKTOP_CONTROL_ORACLE=$oracle
 $driver=Join-Path $repoRoot '.artifacts/standalone-desktop-tool/bin/DesktopAcceptance/debug/DesktopAcceptance.dll'
 foreach($path in @($oracle,$driver)){if(-not(Test-Path -LiteralPath $path)){throw "Build the acceptance tools first: $path"}}
 $hybridManaged=Join-Path $repoRoot '.artifacts/standalone-parity-hybrid-build/managed'
 if($Variant -ne 'Standalone' -and ($PrepareHybrid -or -not(Test-Path -LiteralPath (Join-Path $hybridManaged 'TomasAI.IFM.UI.Rust.Bridge.Net.dll')))){
  Invoke-Checked dotnet @('build','TomasAI.IFM.UI.Rust.Bridge.Net/TomasAI.IFM.UI.Rust.Bridge.Net.csproj','-c','Release','-m:1','-o',$hybridManaged)
 }
 Invoke-Checked docker @('run','-d','--name',$container,'-p','127.0.0.1::4222','nats:2.12.0-alpine');$containerStarted=$true
 $port=(& docker port $container 4222/tcp).Trim().Split(':')[-1];if($LASTEXITCODE -ne 0 -or $port -eq '4222'){throw 'Isolated broker allocation failed'}
 $env:IFM_UI_NATS_URL="nats://127.0.0.1:$port";$env:IFM_UI_MARKETDATA_NATS_URL=$env:IFM_UI_NATS_URL;$env:IFM_UI_MARKETDATA_SCOPE='nativecompat'
 Remove-Item Env:IFM_UI_NATS_CREDENTIALS -ErrorAction SilentlyContinue;Remove-Item Env:IFM_UI_NATS_TOKEN -ErrorAction SilentlyContinue
 $env:IFM_UI_BROKER_ACCOUNT_ALIAS='IFM-EMULATOR-PAPER';$env:IFM_UI_BROKER_ENVIRONMENT='Emulator';$env:IFM_UI_OPERATOR=[Environment]::UserName
 $env:IFM_UI_REQUEST_TIMEOUT_SECONDS='10';$env:IFM_RUST_UI_MODE='live';$env:DOTNET_ENVIRONMENT='Development'
 $env:SupervisorControl__Enabled='true';$env:SupervisorControl__Requester='Fixture';$env:SupervisorControl__Timeout='00:02:00'
 $variants=if($Variant -eq 'Both'){@('Hybrid','Standalone')}else{@($Variant)}
 foreach($flavor in $variants){
  $folder=Join-Path $run $flavor;New-Item -ItemType Directory -Path $folder -Force | Out-Null
  $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$listener.Start();$httpPort=([Net.IPEndPoint]$listener.LocalEndpoint).Port;$listener.Stop()
  $http="http://127.0.0.1:$httpPort/";$env:IFM_STANDALONE_DESKTOP_HTTP_URL=$http
  $env:IFM_UI_ACTOR_HEALTH_URL=$http+'api/actor-health';$env:IFM_UI_OPERATIONS_HEALTH_URL=$http+'api/market-data/operations-health'
  $env:IFM_UI_CONFIG_PATH=Join-Path $folder 'ui-client.Development.json'
  @{AppSettings=@{NatsServerUri=$env:IFM_UI_NATS_URL};MarketDataHost=@{NatsUrl=$env:IFM_UI_NATS_URL;Scope='nativecompat'};TradeBroker=@{AccountAlias='IFM-EMULATOR-PAPER';Environment='Emulator'};MarketDataOperationsHealth=@{Endpoint=$env:IFM_UI_OPERATIONS_HEALTH_URL};SupervisorControl=@{Enabled=$true;Requester='Fixture';Timeout='00:02:00'}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $env:IFM_UI_CONFIG_PATH -Encoding UTF8
  $env:IFM_STANDALONE_ORDER_JOURNAL=Join-Path $folder 'order-journals'
  $env:LOCALAPPDATA=Join-Path $folder 'native-local-data'
  if($flavor -eq 'Hybrid'){
   $appFolder=Join-Path $folder 'app';New-Item -ItemType Directory -Path $appFolder -Force | Out-Null
   Copy-Item -LiteralPath '.artifacts/rust-ui/app/ifm-ui-rust.exe' -Destination (Join-Path $appFolder 'ifm-ui-rust.exe')
   Copy-Item -LiteralPath $hybridManaged -Destination (Join-Path $appFolder 'managed') -Recurse
   @{AppSettings=@{NatsServerUri=$env:IFM_UI_NATS_URL};MarketDataHost=@{NatsUrl=$env:IFM_UI_NATS_URL;Scope='nativecompat'};TradeBroker=@{AccountAlias='IFM-EMULATOR-PAPER';Environment='Emulator'};MarketDataOperationsHealth=@{Endpoint=$env:IFM_UI_OPERATIONS_HEALTH_URL};SupervisorControl=@{Enabled=$true;Requester='Fixture';Timeout='00:02:00'}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $appFolder 'managed/ui-client.Development.json') -Encoding UTF8
   $exe=Join-Path $appFolder 'ifm-ui-rust.exe'
  }else{$exe=(Resolve-Path -LiteralPath $StandaloneExe).Path}
  $fixtures=(Resolve-Path -LiteralPath $FixtureDirectory).Path
  $stdout=Join-Path $folder 'responder.log';$stderr=Join-Path $folder 'responder-error.log'
  $arguments='"'+$oracle+'" "'+$fixtures+'" --desktop-serve '+$env:IFM_UI_NATS_URL
  $worker=Start-Process dotnet -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
  $until=[DateTime]::UtcNow.AddSeconds(30)
  while(-not(Test-Path -LiteralPath $stdout) -or -not(Select-String -LiteralPath $stdout -Pattern '^READY:' -Quiet)){
   if($worker.HasExited -or [DateTime]::UtcNow -ge $until){throw "Desktop responder did not start: $stderr"};Start-Sleep -Milliseconds 100
  }
  foreach($control in @($spec.controls)){Invoke-Checked dotnet @($oracle,'--control',$env:IFM_UI_NATS_URL,[string]$control.verb,[string]$control.value)}
  $env:IFM_STANDALONE_DESKTOP_EXPECT_NATIVE=if($flavor -eq 'Standalone'){'1'}else{'0'}
  Invoke-Checked dotnet @($driver,$exe,$steps,$folder)
  if($worker.HasExited){throw "Desktop responder exited during the scenario; inspect $stderr"}
  if((Get-Item -LiteralPath $stderr).Length -gt 0){throw "Desktop fixture reported an unsupported request or exception; inspect $stderr"}
  if(-not $worker.HasExited){Stop-Process -Id $worker.Id -Force};$worker=$null
 }
 if($Variant -eq 'Both'){Invoke-Checked python @('TomasAI.IFM.UI.Rust.eGui/tools/compare_desktop.py',$run,(Resolve-Path -LiteralPath $Scenario).Path)}
 Write-Output "Desktop evidence: $run"
}finally{
 if($worker -and -not $worker.HasExited){Stop-Process -Id $worker.Id -Force}
 if($containerStarted){& docker rm -f $container | Out-Null}
 foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$prior[$key],'Process')}
 Pop-Location
}
