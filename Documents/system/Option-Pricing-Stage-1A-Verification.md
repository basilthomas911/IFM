# Stage 1A: Fast Calculation Tiers - Implementation and Verification

Date: 2026-09-19. Status: Stage 1A implementation and verification gate PASSED. This record supplements, rather than overwrites, the [original Stage 1 evidence](Option-Pricing-Stage-1-Implementation-and-Verification.md).

Scope: the required APIs and gates from the [S1A addendum](Option-Pricing-Calculation-Tiers-and-Stage-1-Addendum.md). No Stage 2 scheduling/cache/metadata integration, Stage 3 blotter changes or Stage 4 IV Rank/Percentile implementation is included.

## Delivered APIs

All APIs are on Framework.OptionPricer.Pricing.OptionCalculator.

| API | Outputs | Work performed |
| --- | --- | --- |
| PriceAndDelta | PriceDeltaResult: theoretical price, volatility used, Delta | One American lattice/FD evaluation; no IV solve or Vega/Rho/Theta bumps |
| PriceAndDeltaBatch | Caller-owned span of PriceDeltaResult | Same calculation, bounded ordered batch |
| SolveImpliedVolatility | ImpliedVolatilityResult: IV, repriced price, signed residual, bisection iterations | Shared model-consistent IV solve only; no full-Greek postpass |
| SolveImpliedVolatilityBatch | Caller-owned span of ImpliedVolatilityResult | Same solve, bounded ordered batch |
| Existing Price / ImpliedVolatility and batches | Existing full-Greek results | Preserved; ImpliedVolatility uses the shared IV-only solve before explicitly calculating Greeks |

New result types retain the numerical request, engine identifier, steps, numerical settings and policy version. Failures have no numeric payload. Price/Delta has no Gamma/Vega/Theta/Rho placeholder fields; IV-only has no Greek placeholder fields. Residual means repriced price minus input market price; Iterations counts bisection iterations, excluding bracket construction.

European managed pricing has a direct price/Delta kernel using the existing normal-CDF implementation and correct forward-to-spot Delta conversion for equities. It does not expose the private price evaluator's zero Delta placeholder. European futures configured for Rust reuse the existing fused analytic native price/Greek call and expose only price/Delta; this preserves native selection without extending the ABI. That Rust path still computes inexpensive analytic Greeks internally, but performs no extra numerical trees or bumps. Equity and American calculations remain managed.

Numerical tolerances, model limits, default steps and PricingPolicy/v2 are unchanged. No reduced-step approximation, warm-start cache or alternative IV solver was introduced.

## Usage and responsibility boundaries

Solve IV from a qualified market snapshot when an IV refresh is required. Pass that volatility explicitly to PriceAndDelta for selection refreshes. Changing Delta targets should select from an existing qualified snapshot; the numerical facade does not own target selection, quote subscriptions or freshness scheduling.

These results contain framework numerical inputs, not feed identity/calendar/source-time wrappers. Stage 2 must preserve the original market mark and source evidence, identify reused IV separately from newly solved IV, enforce age/skew/generation policy, and reject stale out-of-order publication. The facade does not claim an IV input is fresh merely because its value is numerically valid.

Scalar and batch APIs preserve cancellation and typed validation. Batch size is at most 2048 with matching spans. New batches check cancellation even when empty. A cancellation during a nonempty batch can leave a completed prefix in the caller's buffer; callers must not publish that buffer as a complete snapshot after an exception.

Zero-volatility/expiry price-only behavior remains in TheoreticalPrice. PriceAndDelta rejects these boundaries as UndefinedGreeks rather than inventing a convention for boundary Delta. Existing model/domain safety limits continue to apply.

An IV-only solve or price/Delta result may succeed at a model boundary where full Greeks fail because a bumped input is outside the qualified domain. This is intentional: success certifies only the requested outputs. It does not authorize using unavailable full-risk sensitivities.

## Verification mapping

- PricingTierTests: 20 model/convention cases cover both rights, European/American futures with paid-upfront/futures-style premium, and European/American equities with none/continuous/cash dividends. Fast price/Delta matches the full result to 10 decimal places on these fixtures. IV matches the shared full API and reprices within the unchanged 1e-8 tolerance.
- Independent European Delta fixture rejects accidental zero placeholders and verifies the managed equity engine identifier.
- No-postpass regression: at a cash model total-volatility boundary, price/Delta succeeds while the full Vega bump fails; at the carry boundary, IV-only succeeds while the full Rho bump fails. Accidentally calling the full path makes these tests fail.
- Invalid inputs, unsupported metadata, undefined boundaries, solver limits, overflow guard, nullable failure payloads, scalar/batch parity, input ordering, capacity, empty batches, cancellation and concurrent calls are covered.
- Warmed allocation regression measures repeated price/Delta and IV-only calls on the current thread after pooling/JIT warmup.
- BDD covers qualified IV reuse for refreshed underlying/selection inputs and unidentifiable IV failures across all four asset/style combinations.
- Integration covers new result JSON round trips, cash-schedule preservation, numerical-policy/engine evidence, exact replay and failure payloads, plus the existing actor/storage integration suite.
- Frozen market-data consumer tests compare new tiers with existing qualified Black-76 output and retain stale-quote rejection.
- Separate Rust-configured process verifies the new tiers alongside existing facade behavior; full normal tests retain independent managed/native regression coverage.

Final regression results: 238 passing test executions, zero failures and zero skipped tests. The Rust run repeats a focused subset under the alternate backend; these are executions, not 238 distinct tests.

| Suite | Passed |
| --- | ---: |
| Full option-pricer unit suite | 122 |
| Full option-pricer BDD suite | 16 |
| Full option-pricer integration suite | 16 |
| Affected market-data consumer regressions | 43 |
| Separate Rust-configured facade/tier regressions | 41 |

The framework build passed with zero warnings/errors. The warmed current-thread allocation regression passed with zero allocated bytes for the tested fast calls.

## Comparative performance gate

All 32 benchmarks completed successfully in the final sequential run. BenchmarkDotNet 0.15.8 ShortRun, in-process, three warmups/three measured iterations, Release .NET 10.0.10 / SDK 10.0.302, Windows 10, AMD Threadripper 1950X. Futures, paid-upfront premium, default 801-step American model; caller-owned batch buffers. These are local microbenchmarks, not end-to-end market-hour latency guarantees or equity/cash-dividend performance measurements.

European means in microseconds per batch:

| Contracts | Full price/Greeks | Price/Delta | IV plus Greeks | IV only |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 0.1764 | 0.1201 | 3.4971 | 3.3242 |
| 2 | 0.3483 | 0.2480 | 6.9285 | 6.6005 |
| 4 | 0.6967 | 0.4953 | 13.8403 | 13.1845 |
| 64 | 11.0420 | 7.7876 | 222.6044 | 210.8709 |

American means in milliseconds per batch:

| Contracts | Full price/Greeks | Price/Delta | IV plus Greeks | IV only |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 6.1427 | 0.8826 | 36.4226 | 29.8264 |
| 2 | 12.3517 | 1.7579 | 72.2725 | 59.8435 |
| 4 | 24.6199 | 3.5153 | 148.2688 | 119.4911 |
| 64 | 397.6815 | 56.1254 | 2306.7140 | 1908.8463 |

Measured price/Delta speedup: European 1.40-1.47x; American 6.96-7.09x. IV-only speedup: European 1.05-1.06x; American 1.21-1.24x. ShortRun uncertainty applies, especially the four-contract American IV-plus-Greeks case. The 64-contract American IV-only batch still takes approximately 1.91 seconds: it is not suitable for every chain tick or a UI-thread calculation. Stage 2 must use qualified IV reuse and bounded/coalesced background refresh.

Raw MemoryDiagnoser records report zero Gen 0/1/2 collections in all 32 cases. European allocations were reported as none; American per-batch allocations (full price/Greeks, price/Delta, IV plus Greeks, IV only) were respectively: count 1 = 0/3/0/0 B; count 2 = 11/7/384/373 B; count 4 = 0/3/840/730 B; count 64 = 84/219/504/828 B. These small in-process inclusive measurements differ from the isolated warmed current-thread zero-allocation regression; no claim of zero application-wide allocation or GC is made.

Raw run: BenchmarkDotNet.Artifacts/TomasAI.IFM.Domain.OptionPricer.Benchmarks.UnifiedPricingBenchmarks-20260919-130217.log. The report includes confidence intervals and standard deviations; run exited successfully with 32 executed benchmarks.

## Reproducibility

Tests execute sequentially; no integration projects or benchmarks overlap. Commands:

```powershell
dotnet test TomasAI.IFM.Domain.OptionPricer.UnitTests --no-restore --logger "trx;LogFileName=stage1a-final.trx" --verbosity quiet
dotnet test TomasAI.IFM.Domain.OptionPricer.BDDTests --no-restore --logger "trx;LogFileName=stage1a-final.trx" --verbosity quiet
dotnet test TomasAI.IFM.Domain.OptionPricer.IntegrationTests --no-restore --logger "trx;LogFileName=stage1a-final.trx" --verbosity quiet
dotnet test TomasAI.IFM.Application.MarketData.UnitTests --no-restore --filter "FullyQualifiedName~UnifiedPricingConsumerCompatibilityTests|FullyQualifiedName~OrderCompositionPricingPrerequisiteTests|FullyQualifiedName~OrderCompositionSnapshotTests" --logger "trx;LogFileName=stage1a-consumer.trx" --verbosity quiet
# Separate test process; restore the prior environment value afterward:
$env:IFM_OPTION_PRICER_IMPLEMENTATION = "Rust"
dotnet test TomasAI.IFM.Domain.OptionPricer.UnitTests --no-build --filter "FullyQualifiedName~UnifiedOptionCalculatorTests|FullyQualifiedName~PricingTierTests" --logger "trx;LogFileName=stage1a-rust.trx" --verbosity quiet
# Restore environment before benchmark:
dotnet run -c Release --project TomasAI.IFM.Domain.OptionPricer.Benchmarks --no-restore -- --filter "*UnifiedPricingBenchmarks*" --job short --inProcess --unrollFactor 1
```

TRX files are under each project's TestResults. Benchmark reports are under BenchmarkDotNet.Artifacts/results/*UnifiedPricingBenchmarks*. Generated artifacts may be Git-ignored; this record preserves the final evidence.

Worktree base: 8c36fce5902aa91708c86774911034137393bb82; unrelated existing edits preserved; no commit created. Final-source SHA-256:

- Pricing/OptionCalculator.cs: A71A2A51F1E2BBBFD2BB118DEAA7FA6ADDD24A2880C8ED0C9EF4415B512ACD18
- Pricing/OptionPricingContracts.cs: 5AFF9D35B8DD1ACCC506CA6E8E6836A21EEB117908F97AB5A43C652D4D487594
- Black76/OptionModel.cs: 4EC6A6092F0A19BA0BDEEC51FFC1B907AB184D62739C41FB5ADE086EFD83C241

## Remaining downstream work

Completion of S1A qualifies the framework API separation, not live trading. IV refresh/caching, coherent snapshots, coalesced bounded scheduling, current-position monitoring and source-time/context contracts remain Stage 2 responsibilities. Composer/blotter integration remains Stage 3; IV Rank/Percentile and history remain Stage 4. The original model qualification limits and deployment gates still apply.
