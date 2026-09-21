# Stage 1 Option Pricing: Implementation and Verification Record

Status: **Stage 1 implementation gate PASSED, 2026-09-19**, within the numerical capability limits below. Stage 2 has not started. This is not live-trading activation.

Subsequent scope addition: dedicated price/Delta and IV-only scalar/batch APIs required by the chain-selection policy are now implemented and separately verified in the [S1A verification record](Option-Pricing-Stage-1A-Verification.md). See the [S1A calculation-tier addendum](Option-Pricing-Calculation-Tiers-and-Stage-1-Addendum.md). The evidence below remains the original completed baseline, not the evidence for these additions.

## Scope and delivered capability

This record implements Stage 1 of [the three-stage plan](ES-Trade-Blotter-Three-Stage-Implementation-Plan-v1.0.md). The framework facade is `TomasAI.IFM.Framework.OptionPricer.Pricing.OptionCalculator`; existing `Black76.OptionCalculator` callers remain compatible.

| Underlying / exercise | Engine | Carry / premium |
| --- | --- | --- |
| European futures | Existing Black-76 managed/Rust selection | Zero futures carry; paid-upfront discounting or futures-style zero discounting |
| European equity | Managed Black-Scholes-Merton, using an equivalent forward transformation | Rate minus continuous yield; paid-upfront only |
| American futures | Managed CRR lattice | Zero futures carry; premium-specific discounting |
| American equity | Managed CRR lattice | Rate minus continuous yield; paid-upfront only |
| Equity with discrete cash dividends, either exercise style | Managed implicit finite differences, exact-time dividend jumps; American projected SOR obstacle solve | Explicit dated cash amounts, not a continuous-yield or escrow approximation |

American and equity requests never enter the existing Black-76-only native ABI. European futures retain the existing process-pinned backend choice. Equity pricing is framework capability only: equity trading remains excluded from V1.

Delivered APIs: theoretical price, price plus all Greeks, model-consistent IV plus Greeks, bounded price/Greek and IV batches, cancellation, typed failures without numeric payloads, immutable request/settings evidence and versioned engine identifiers. The calculator reads no clock and owns no mutable evaluation date.

No reference schemas, Databento selectors, UI controls, startup behavior, broker adapters or live trading workflows were changed. Existing market-data pricing qualification remains in place. Frozen consumer fixtures verify compatibility; Stage 2 owns replacing European-only context checks and routing market-data consumers to the new models.

## Units and numerical policy v2

- Inputs: positive underlying and strike; qualified year fractions; annual decimal rates, yield and volatility.
- Outputs are per option, before position quantities and contract multipliers. Delta is per underlying-price unit, Gamma per squared unit, Vega/Rho per unit decimal volatility/rate, Theta per year. UI conversions belong outside the kernels.
- Default CRR/FD time steps: 801; permitted 32–4096. Default IV iterations: 100; permitted 1–256. Configured volatility ceiling defaults to 4 and cannot exceed 10. IV uses expanding brackets and bisection, with absolute repricing tolerance 1e-8. Default batch cap: 2048.
- FD spatial steps: 800, permitted 64–4096. American FD SOR limit: 2000, permitted 1–10000; update tolerance 1e-9.
- American CRR Delta/Gamma use the first two lattice layers. FD Delta/Gamma use central spatial derivatives and interpolation.
- Numerical Vega uses min(1e-4, sigma/2); Rho uses 1e-5; calendar Theta uses min(1e-5, T/2), further limited by the first dividend time. Cash-dividend Theta shifts expiry and every dividend date together and freezes segment step counts to avoid discretization jumps.
- Price-only supports zero volatility and expiry. A complete smooth Greek vector at these boundaries is rejected as `UndefinedGreeks`. Expired, invalid, unsupported, unidentifiable IV, unbracketed IV, nonconvergence and numerical failures remain distinct statuses.
- CRR uses one pooled flat buffer; FD uses one pooled lease sliced into eight work arrays. No per-node objects or mutable global dates. Non-dividend price/Greek calls have no measured per-call allocations after warmup. Immutable cash schedules are copied for calendar-theta shifts.

These are bounded implementation/verification defaults, not blanket financial approval over every mathematical input or production trading configuration. Product-specific accuracy and streaming latency remain deployment qualifications.

### Explicit capability limits

Nonpositive underlying/strike, nonfinite values, unknown metadata and incompatible premium/dividend conventions fail. Equity futures-style premium is unsupported. Futures dividend inputs are unsupported. CRR rejects invalid transition probabilities and overflow/underflow-risk terminal ranges rather than clamping probabilities.

Cash schedules are immutable, strictly increasing, positive amounts, with 0 < dividend time < expiry, at most 64 entries and no simultaneous yield input. An empty cash schedule is supported and tested against an independent American put reference. Cash paid is capped at stock value: S_after = max(S_before - D, 0); this limited-liability convention must be appropriate for any future product using it.

The uniform FD grid uses Smax = 4 max(spot,strike) + total cash. It rejects unresolved spot locations, total volatility sigma sqrt(T) > 0.5, and |rT| > 0.25. Schedule validation additionally limits total cash to 4 max(spot,strike) and spot/max(spot,strike) to at least 0.05. IV's FD bracket respects the same total-volatility ceiling; it cannot silently extrapolate. Numerical Greek bumps can fail at a domain boundary even where price-only succeeds. Resolution checks below qualify the tested fixtures, not every schedule admitted by these safety guards.

Greeks around American exercise boundaries are grid-dependent sensitivities, not a guarantee of a smooth economic surface. An immediate-exercise plateau is explicitly tested for intrinsic price, Delta -1 and approximately zero Gamma/Vega/Theta/Rho. Live product activation must review exercise/assignment semantics separately.

## Numerical verification and acceptance policy

| Verification | Acceptance limit | Observed final-fixture evidence |
| --- | --- | --- |
| European equity S=K=100, r=.05, sigma=.2, T=1 | Price 10.45057–10.45060; independent Greek ranges in unit fixture | Passed |
| American put S=36,K=40,r=.06,sigma=.2,T=1 | Price 4.47–4.50; 801 vs 1601 difference <= .005 | CRR and independent FD fixtures passed |
| Six-case American CRR vs separate log-price moment-matched trinomial | Absolute price difference <= .015 | Maximum .004080724 |
| Same American matrix, 801 vs 1601 CRR steps | Absolute price difference <= .008 | Maximum .001445747 |
| European cash dividend, conditional lognormal integration followed by closed-form continuation | FD at 1600 time/spatial steps: error <= .006 | Call .000689904; put .000614448 |
| American call before large dividend | Early-exercise premium and 800 vs 1600 resolution difference <= .025 | Passed |
| Cash Theta at 800/801 segment thresholds | Annual Theta difference <= .02 | Passed |
| Identifiable IV from model-generated prices | Repricing <= 1e-8; volatility within 1e-6 | Passed across four combinations and cash models |
| European price-bump Greek verification | Delta/Gamma 1e-5; per-unit Vega/Rho 1e-3 | Passed |
| American six-case Greek resolution checks | Delta/Gamma .003; Vega .15; Theta .1; Rho .2 | Passed |

The European derivative tolerance accounts for the inherited normal-CDF approximation (documented absolute error about 1.5e-7). The kernel was not changed. The trinomial reference has an independent recurrence and buffers; it does not call the production CRR implementation. Cash integration uses a different numerical method with 4000 Simpson intervals on normal [-8,8], using the separately checked European kernel for conditional continuation.

Additional coverage: both option rights; negative rates; continuous yield; paid-upfront and futures-style premium; early exercise; near expiry; deep moneyness; zero volatility; price bounds; IV non-identifiability and iteration limits; malformed cash schedules; FD solver exhaustion/range rejection; cancellation; batch length/capacity/order; concurrent calls; serialization of schedule and numerical evidence; frozen application-context compatibility; stale quote rejection; unchanged managed/native Black-76 behavior.

## Actor integration regression resolved

The original two spread-distribution integration tests timed out because `SpreadDistributionCommandState.Apply` always returned false. The base state only enqueues accepted events; consequently insert/delete events were neither persisted nor projected despite successful command responses.

The handler now accepts exactly `SpreadDistributionInsertedEvent` and `SpreadDistributionDeletedEvent`. No projector retry settings, timeouts or startup behavior were changed. Unit tests verify pending-event capture, accept-changes and replay behavior; the full integration suite verifies insert/delete completion events and projected storage. Both previously failing tests pass.

## Test execution and reproducibility

Windows DEV-SERVER, .NET SDK 10.0.302 / runtime 10.0.10. Integration projects ran sequentially, per repository policy. This is the option-pricer stage suite plus affected pricing-consumer regressions, not a claim that every solution integration project ran.

| Final-source check | Passed | Failed / skipped |
| --- | --- | --- |
| Full option-pricer unit suite | 97 | 0 / 0 |
| Full option-pricer BDD suite | 11 | 0 / 0 |
| Full option-pricer integration suite | 12 | 0 / 0 |
| Affected market-data pricing consumer regressions | 43 | 0 / 0 |
| Separate process with Rust backend: unified routing/regression cases | 16 | 0 / 0 |

Total: 179 passing test executions, including the 16 repeated under Rust. All 16 final-source Release benchmark cases also completed successfully.

Commands:

```powershell
dotnet test TomasAI.IFM.Domain.OptionPricer.UnitTests --no-restore --logger "trx;LogFileName=stage1-complete.trx" --verbosity quiet
dotnet test TomasAI.IFM.Domain.OptionPricer.BDDTests --no-restore --logger "trx;LogFileName=stage1-complete.trx" --verbosity quiet
dotnet test TomasAI.IFM.Domain.OptionPricer.IntegrationTests --no-restore --logger "trx;LogFileName=stage1-complete.trx" --verbosity quiet
dotnet test TomasAI.IFM.Application.MarketData.UnitTests --no-restore --filter "FullyQualifiedName~UnifiedPricingConsumerCompatibilityTests|FullyQualifiedName~OrderCompositionPricingPrerequisiteTests|FullyQualifiedName~OrderCompositionSnapshotTests" --logger "trx;LogFileName=stage1-consumer.trx" --verbosity quiet
# In a separate test process, with the environment restored afterward:
$env:IFM_OPTION_PRICER_IMPLEMENTATION = "Rust"
dotnet test TomasAI.IFM.Domain.OptionPricer.UnitTests --no-build --filter "FullyQualifiedName~UnifiedOptionCalculatorTests" --logger "trx;LogFileName=stage1-rust-routing.trx" --verbosity quiet
# Restore the prior environment value before benchmarks.
dotnet run -c Release --project TomasAI.IFM.Domain.OptionPricer.Benchmarks --no-restore -- --filter "*UnifiedPricingBenchmarks*" --job short --inProcess --unrollFactor 1
```

TRX artifacts reside under each project's `TestResults`; benchmark reports under `BenchmarkDotNet.Artifacts/results/*UnifiedPricingBenchmarks*`. Generated artifacts can be Git-ignored; this repository document preserves the outcome and acceptance policy.

Worktree base: `8c36fce5902aa91708c86774911034137393bb82`, with existing unrelated user changes preserved. No commit was created. SHA-256 fingerprints of the final numerical source:

- CashDividendModel.cs: `820C3F70EB6BBF45DCB3C21E23D86C474BE7BC93C3B751F48DF8B8B091DF2065`
- OptionCalculator.cs: `4E4443786B4FDC679B803824B31FFA275D3CCAC0BD1D93DEFBC03353A1493F94`
- OptionPricingContracts.cs: `3B7DEC66F032684B15E289EACC37D598ADA851957CC3BACB823AD7A801843024`

## Final Release performance baseline

BenchmarkDotNet 0.15.8, Windows 10 22H2, AMD Ryzen Threadripper 1950X (16 physical / 32 logical cores), .NET 10.0.10, Concurrent Workstation GC. ShortRun uses one in-process launch, three warmup and three measured iterations, unroll factor 1. Tests and benchmarks ran sequentially. These are futures batches, default 801 steps, not equity cash-dividend benchmarks.

| Options per batch | European price + Greeks (us) | European IV + Greeks (us) | American price + Greeks (ms) | American IV + Greeks (ms) |
| --- | --- | --- | --- | --- |
| 1 | 0.1745 | 3.458 | 6.147 | 36.307 |
| 2 | 0.3443 | 6.979 | 12.421 | 71.744 |
| 4 | 0.6867 | 13.976 | 24.699 | 143.777 |
| 64 | 10.949 | 219.839 | 393.108 | 2307.121 |

The memory diagnostic observed no Gen 0/1/2 collections in the measured workloads. European cases reported no measured allocation. American per-batch reported allocations for counts 1/2/4/64 were 26/36/0/0 bytes for price-plus-Greeks and 237/48/0/492 bytes for IV-plus-Greeks. Do not interpret this short in-process result as proof of zero whole-process allocation. The separate warmed synchronous pricing regression measured zero current-thread bytes over 100 calls in both managed and Rust-configured test processes.

This supersedes the earlier Dry-run timing table. ShortRun provides a reproducible baseline, not p99 latency, a production SLA or market-hours load qualification. Some three-sample confidence intervals are wide; the complete report retains them. The 64-option American IV workload is approximately 2.3 seconds, so full-chain recalculation per raw tick is not an acceptable integration strategy. Coalesced/background scheduling and product-specific workload qualification remain required.

## Deployment distinction

Stage 1 delivers numerical capability and compatibility, not live activation. Stage 2 still owns reference models, reviewed conventions, exact source-time/context-digest wrapping, Databento selection and quote/trade enrichment. Stage 3 owns the blotter and emulator workflow. No equity trading is enabled.

American lattices remain O(N²), with multiple repricings for Greeks and IV. Full-chain calls must not run synchronously on the UI thread or once for every raw tick. Stage 2 must preserve coalescing, bounded work and cancellation; market-hours load qualification remains separate. No exchange/provider or live broker qualification is claimed.

## Primary reference basis

- [Longstaff and Schwartz, 2001](https://escholarship.org/uc/item/43n1k4jb): independent American put reference family.
- [QuantLib trinomial implementation](https://github.com/lballabio/QuantLib/blob/master/ql/methods/lattices/trinomialtree.cpp): moment-matching reference basis; the test recurrence is separate from production CRR.
- [QuantLib finite-difference equity engine](https://github.com/lballabio/QuantLib/blob/master/ql/pricingengines/vanilla/fdblackscholesvanillaengine.cpp): dividend-event and American finite-difference modeling reference.
- [Manchester American-option finite-difference laboratory](https://personalpages.manchester.ac.uk/staff/paul.johnson-2/resources/math60082/lab-math60082-8.pdf): projected obstacle-solve methodology.
- [CME premium margining conventions](https://www.cmegroup.com/education/articles-and-reports/a-primer-on-margining-styles-for-options): paid premium versus futures-style margining.
