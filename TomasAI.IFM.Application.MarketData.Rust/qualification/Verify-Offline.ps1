param([switch]$IncludeProductionDeadlines,[switch]$IncludeNats,[switch]$IncludePostgres,[switch]$IncludeSustained,[string]$ResumeFrom)

# Reproducible local qualification only. Every transport peer is a localhost
# fixture with fabricated credentials; this script never starts/stops IFM servers.
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repository 'TomasAI.IFM.Application.MarketData.Rust'
$artifacts = Join-Path $repository '.artifacts/rust-marketdata-offline'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
$manifest = Join-Path $project 'Cargo.toml'
$library = Join-Path $project 'target/development/ifm_market_data_native.dll'
$baseline = Join-Path $project 'baseline'
$fixture = Join-Path $artifacts 'live-fixture.dbn'
$scopeFixture = Join-Path $artifacts 'scope-fixture.dbn'
$pricingFixture = Join-Path $artifacts 'pricing-fixture.dbn'
$leaseFixture = Join-Path $artifacts 'lease-fixture.dbn'
$futuresFixture = Join-Path $artifacts 'futures-fixture.dbn'
$vxFixture = Join-Path $artifacts 'vx-fixture.dbn'
$historyFixtures = Join-Path $artifacts 'history'
$historyOracle = Join-Path $artifacts 'HistoricalSdkOracle.json'
$pricingTime = [DateTimeOffset]::new(2026,9,8,16,0,0,[TimeSpan]::Zero)
$pricingNanoseconds = ($pricingTime.ToUnixTimeMilliseconds() * [long]1000000).ToString([Globalization.CultureInfo]::InvariantCulture)
$report = Join-Path $artifacts 'results.json'
$previousRustdoc = $env:RUSTDOCFLAGS

# Resume only a failed recorded stage; earlier successful logs remain evidence.
# Use the same switches and unchanged inputs as the interrupted invocation.
$script:resumePending = ![string]::IsNullOrWhiteSpace($ResumeFrom)
if($script:resumePending){
    if(!(Test-Path -LiteralPath $report)){throw 'No prior qualification report exists.'}
    $prior=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    $failed=@($prior.stages | Where-Object { $_.name -eq $ResumeFrom -and $_.exitCode -ne 0 })
    if($failed.Count -ne 1){throw 'ResumeFrom must identify the recorded failed stage.'}
    foreach($stage in $prior.stages){
        if($stage.name -eq $ResumeFrom){break}
        if($stage.exitCode -ne 0){throw 'Cannot skip an earlier failed stage.'}
        $results.Add($stage)
    }
}

function Invoke-Stage([string]$Name, [string]$Executable, [string[]]$Arguments) {
    if($script:resumePending){
        if($Name -ne $ResumeFrom){return}
        $script:resumePending=$false
    }

    $log = Join-Path $artifacts (($Name -replace '[^A-Za-z0-9]', '-') + '.log')
    Write-Host "Running $Name (log: $log)"
    $elapsed = [System.Diagnostics.Stopwatch]::StartNew()
    Get-Command $Executable -CommandType Application -ErrorAction Stop | Out-Null
    # Windows PowerShell 5.1 represents native stderr as ErrorRecord objects.
    # Cargo writes ordinary progress to stderr; only its exit code is failure.
    $ErrorActionPreference = 'Continue'
    & $Executable @Arguments 2>&1 | ForEach-Object { $_.ToString() } | Out-File -LiteralPath $log -Encoding utf8
    $code = $LASTEXITCODE
    $results.Add([ordered]@{ name = $Name; exitCode = $code; elapsedMilliseconds = $elapsed.ElapsedMilliseconds; log = $log })
    if ($code -ne 0) {
        Get-Content -LiteralPath $log -Tail 60
        throw "Qualification failed: $Name (exit $code)."
    }
}

Push-Location $repository
try {
    Invoke-Stage 'Rust format' 'cargo' @('fmt', '--manifest-path', $manifest, '--check')
    Invoke-Stage 'Rust Clippy' 'cargo' @('clippy', '--locked', '--manifest-path', $manifest, '--all-targets', '--', '-D', 'warnings')
    Invoke-Stage 'Treasury source oracle' 'dotnet' @('run','--project',(Join-Path $project 'qualification/TreasuryOracle'),'--',(Join-Path $artifacts 'TreasuryCurveOracle.json'),(Join-Path $baseline 'TreasuryCurveOracle.json'))
    Invoke-Stage 'Rust regression' 'cargo' @('test', '--locked', '--manifest-path', $manifest, '--tests')
    $env:RUSTDOCFLAGS = '-D warnings'
    Invoke-Stage 'Rust documentation' 'cargo' @('doc', '--locked', '--manifest-path', $manifest, '--no-deps')
    Invoke-Stage 'Development DLL' 'cargo' @('build', '--locked', '--profile', 'development', '--manifest-path', $manifest)
    Invoke-Stage 'SDK wire fixture' 'cargo' @('run', '--locked', '--manifest-path', $manifest, '--example', 'qualification_fixture', '--', $fixture)
    Invoke-Stage 'Scoped SDK wire fixture' 'cargo' @('run', '--locked', '--manifest-path', $manifest, '--example', 'qualification_fixture', '--', $scopeFixture, 'GLBX.MDP3', '2')
    Invoke-Stage 'Priced SDK wire fixture' 'cargo' @('run', '--locked', '--manifest-path', $manifest, '--example', 'qualification_fixture', '--', $pricingFixture, 'GLBX.MDP3', '2','pricing',$pricingNanoseconds)
    Invoke-Stage 'Lease SDK wire fixture' 'cargo' @('run', '--locked', '--manifest-path', $manifest, '--example', 'qualification_fixture', '--', $leaseFixture, 'GLBX.MDP3', '3','pricing',$pricingNanoseconds)
    Invoke-Stage 'Futures publication fixture' 'cargo' @('run','--locked','--manifest-path',$manifest,'--example','qualification_fixture','--',$futuresFixture,'GLBX.MDP3','3','futures',$pricingNanoseconds)
    Invoke-Stage 'VX publication fixture' 'cargo' @('run','--locked','--manifest-path',$manifest,'--example','qualification_fixture','--',$vxFixture,'XCBF.PITCH','3','futures',$pricingNanoseconds)
    Invoke-Stage 'Historical SDK oracle' 'cargo' @('run','--locked','--manifest-path',(Join-Path $project 'qualification/HistoricalSdkOracle/Cargo.toml'),'--',$historyOracle)
    Invoke-Stage 'Historical wire fixtures' 'cargo' @('run','--locked','--manifest-path',$manifest,'--example','history_fixture','--',$historyFixtures)
    Invoke-Stage 'Replay wire fixture' 'cargo' @('run','--locked','--manifest-path',$manifest,'--example','replay_fixture','--',(Join-Path $artifacts 'replay-fixture.dbn'))
    Invoke-Stage 'Managed bridge build' 'dotnet' @('build', (Join-Path $project 'managed'), '-v', 'quiet')
    Invoke-Stage 'VX bootstrap current Host oracle' 'dotnet' @('run','--project',(Join-Path $project 'qualification/VxBootstrapOracle'),'--',(Join-Path $artifacts 'VxBootstrapOracle.json'))
    Invoke-Stage 'VX bootstrap parity' 'dotnet' @('run','--project',(Join-Path $project 'qualification/VxBootstrapBridge'),'--',(Join-Path $artifacts 'VxBootstrapNative.json'),(Join-Path $artifacts 'VxBootstrapOracle.json'))
    Invoke-Stage 'API recovery ownership compatibility' 'dotnet' @('test',(Join-Path $repository 'TomasAI.IFM.LivePipeline.IntegrationTests'),'-c','Release','--no-restore','--filter','FullyQualifiedName~OptionPipelineRecoveryRuntimeTests|FullyQualifiedName~OptionRecoveryReplacementProofTests|FullyQualifiedName~OptionRecoveryWorkloadProofTests','-v','quiet')
    foreach ($entry in @(
        @{Name='FacadeContract'; Inputs=@()},
        @{Name='HostRecoveryBridge'; Inputs=@()},
        @{Name='HostCompositionBridge'; Inputs=@()},
        @{Name='HostStateBridge'; Inputs=@()},
        @{Name='NativeLeaseCancellationBridge'; Inputs=@()},
        @{Name='RuntimeQualification'; Inputs=@($library, (Join-Path $baseline 'FrameworkOracle.json'))},
        @{Name='PricingBridge'; Inputs=@($library, $baseline)},
        @{Name='ApplicationPricingBridge'; Inputs=@($library)},
        @{Name='CatalogBridge'; Inputs=@($library, (Join-Path $baseline 'DefinitionOracle.json'))},
        @{Name='ResolverBridge'; Inputs=@($library, (Join-Path $baseline 'DefinitionOracle.json'))},
        @{Name='DefinitionBridge'; Inputs=@($library, (Join-Path $baseline 'DefinitionOracle.json'))},
        @{Name='TreasuryBridge'; Inputs=@($library,(Join-Path $baseline 'TreasuryCurveOracle.json'))},
        @{Name='FmpBridge'; Inputs=@($library)},
        @{Name='HistoricalBridge'; Inputs=@($library,$historyFixtures,$historyOracle)},
        @{Name='ReplayBridge'; Inputs=@($library,(Join-Path $artifacts 'replay-fixture.dbn'))},
        @{Name='LiveBridge'; Inputs=@($library, $fixture)},
        @{Name='ScopeBridge'; Inputs=@($library, $scopeFixture)},
        @{Name='ScopePricingBridge'; Inputs=@($library, $pricingFixture)},
        @{Name='ScopeLeaseBridge'; Inputs=@($library, $leaseFixture)},
        @{Name='OwnerReplacementBridge'; Inputs=@($library, $leaseFixture)},
        @{Name='DeliveryBridge'; Inputs=@($library, $leaseFixture)},
        @{Name='FuturesPublicationBridge'; Inputs=@($library,$futuresFixture)},
        @{Name='VxPublicationBridge'; Project='FuturesPublicationBridge'; Inputs=@($library,$vxFixture,'VX')},
        @{Name='VxMixedPublisherParity'; Project='FuturesPublicationBridge'; Inputs=@($library,$vxFixture,'VX','mixed')},
        @{Name='FuturesStatisticsReplayBridge'; Project='FuturesPublicationBridge'; Inputs=@($library,$futuresFixture,'ES','stats')},
        @{Name='EpochResourcesBridge'; Inputs=@($library,$futuresFixture,(Join-Path $baseline 'DefinitionOracle.json'))},
        @{Name='OptionUnderlyingDependencies'; Project='EpochResourcesBridge'; Inputs=@($library,$futuresFixture,(Join-Path $baseline 'DefinitionOracle.json'),'--underlying')},
        @{Name='VxBootstrapServiceBridge'; Inputs=@($library,$vxFixture,(Join-Path $baseline 'DefinitionOracle.json'))},
        @{Name='StopStarvation'; Inputs=@($library)}
    )) {
        $entryProject = if ($entry.ContainsKey('Project')) { $entry.Project } else { $entry.Name }
        $arguments = @('run', '--project', (Join-Path $project ('qualification/' + $entryProject)), '--') + $entry.Inputs
        Invoke-Stage $entry.Name 'dotnet' $arguments
    }
    if ($IncludeNats) {
        Invoke-Stage 'NatsPublicationBridge' 'dotnet' @('run','--project',(Join-Path $project 'qualification/NatsPublicationBridge'),'--',$library,$leaseFixture)
        Invoke-Stage 'NatsControlBridge' 'dotnet' @('run','--project',(Join-Path $project 'qualification/NatsControlBridge'))
        Invoke-Stage 'HostExecutableBridge' 'dotnet' @('run','--project',(Join-Path $project 'qualification/HostExecutableBridge'),'--',(Join-Path $project 'host/bin/Debug/net10.0/TomasAI.IFM.Application.MarketData.Host.exe'))
    }
    if ($IncludePostgres) {
        Invoke-Stage 'NativeStoreBridge' 'dotnet' @('run','--project',(Join-Path $project 'qualification/NativeStoreBridge'))
        Invoke-Stage 'HostNativePostgresRecovery' 'dotnet' @('run','--project',(Join-Path $project 'qualification/EpochResourcesBridge'),'--',$library,$futuresFixture,(Join-Path $baseline 'DefinitionOracle.json'),'--host-postgres')
        if($IncludeNats){Invoke-Stage 'NatsPostgresPublication' 'dotnet' @('run','--project',(Join-Path $project 'qualification/NatsPublicationBridge'),'--',$library,$leaseFixture,'--postgres')}
    }
    if($IncludeSustained){Invoke-Stage '1000 joined option owner resets' 'dotnet' @('run','--project',(Join-Path $project 'qualification/OwnerReplacementBridge'),'--',$library,$leaseFixture,'1000')}
    if ($IncludeProductionDeadlines) {
        Invoke-Stage '120-second Host recovery deadline' 'dotnet' @('run','--project',(Join-Path $project 'qualification/HostRecoveryBridge'),'--','--production-deadline')
        Invoke-Stage '120-second SDK deadline' 'cargo' @('test', '--locked', '--manifest-path', $manifest, '--test', 'production_deadline', '--', '--ignored', '--nocapture')
        Invoke-Stage '120-second DNS deadline' 'cargo' @('test', '--locked', '--manifest-path', $manifest, '--test', 'dns_lifecycle', 'production_dns_deadline', '--', '--ignored', '--nocapture')
        Invoke-Stage '120-second metadata deadline' 'cargo' @('test', '--locked', '--manifest-path', $manifest, '--test', 'lane_transport', 'real_120_second_metadata_stall', '--', '--ignored', '--nocapture')
        Invoke-Stage '120-second pricing deadline' 'cargo' @('test', '--locked', '--manifest-path', $manifest, '--test', 'lane_transport', 'real_120_second_pricing_fault', '--', '--ignored', '--nocapture')
    }
    if($script:resumePending){throw 'Requested resume stage was not included by these switches.'}
    Write-Host 'Offline gates passed. These do not establish full Host/actor/UI or live-provider acceptance.'
}
finally {
    $env:RUSTDOCFLAGS = $previousRustdoc
    [ordered]@{ schema = 1; recordedAtUtc = [DateTimeOffset]::UtcNow.ToString('O'); scope = 'Offline component/FFI parity; no production cutover'; stages = $results.ToArray() } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $report -Encoding utf8
    Pop-Location
    Write-Host "Report: $report"
}
