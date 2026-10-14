param([switch]$Desktop,[switch]$Visual,[switch]$Live)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
& "$PSScriptRoot/Generate-UiCatalog.ps1" -Check
if($Live){ & "$repoRoot/.vscode/Wait-IFMDevelopmentApi.ps1"; if($LASTEXITCODE -ne 0){throw "Development API readiness failed"} }
Push-Location "$repoRoot/TomasAI.IFM.UI.Rust"
try{$ErrorActionPreference='Continue';if($Visual){cargo test --features visual-tests 2>&1 | ForEach-Object { $_.ToString() }}else{cargo test 2>&1 | ForEach-Object { $_.ToString() }};if($LASTEXITCODE -ne 0){throw 'Rust tests failed'}}finally{Pop-Location}
Push-Location $repoRoot
try{
  Remove-Item Env:IFM_RUST_UI_MODE -ErrorAction SilentlyContinue
 $env:IFM_RUST_REPO_ROOT=$repoRoot
 $env:IFM_RUST_UI_EXE=Join-Path $repoRoot '.artifacts/rust-ui/app/ifm-ui-rust.exe'
 $env:IFM_RUST_TEST_ARTIFACTS=Join-Path $repoRoot '.artifacts/rust-ui/tests'
 $filter=if($Live){'FullyQualifiedName~Live'}elseif($Desktop){'FullyQualifiedName~PilotTests|FullyQualifiedName~AbiTests|FullyQualifiedName~StrategyDetailsFlaUiTests|FullyQualifiedName~EditorActionTests|FullyQualifiedName~ClientCompositionTests'}else{'FullyQualifiedName~Projection_|FullyQualifiedName~AbiTests|FullyQualifiedName~EditorActionTests|FullyQualifiedName~ClientCompositionTests'}
 $filter+='|FullyQualifiedName~InspectionTests&FullyQualifiedName!~Live|FullyQualifiedName~ContractEditorTests&FullyQualifiedName!~Live|FullyQualifiedName~ReferenceReviewMappingTests|FullyQualifiedName~ProviderReferenceMappingTests'
 $filter+='|FullyQualifiedName~ScheduledTaskBridgeTests|FullyQualifiedName~OperationsHealthBridgeTests|FullyQualifiedName~ActorHealthBridgeTests|FullyQualifiedName~ParameterSetBridgeTests|FullyQualifiedName~ParameterSetEditorTests|FullyQualifiedName~StrategyCatalogBridgeTests|FullyQualifiedName~ConfigurationStrategyCatalogUiTests'
 if($Desktop -or $Live){$filter+='|FullyQualifiedName~ScheduledTasksViewTests|FullyQualifiedName~MarketDataOperationsHealthRenderingTests|FullyQualifiedName~ActorHealthRenderingTests'}
 $ErrorActionPreference='Continue'
 dotnet test 'TomasAI.IFM.UI.Rust.SystemTests/TomasAI.IFM.UI.Rust.SystemTests.csproj' -c Release -o '.artifacts/rust-ui/test-bin' --filter $filter --blame-hang-timeout 3m --logger 'trx;LogFileName=pilot.trx' --results-directory '.artifacts/rust-ui/tests'
 if($LASTEXITCODE -ne 0){throw 'Managed/desktop tests failed'}
}finally{Pop-Location}
