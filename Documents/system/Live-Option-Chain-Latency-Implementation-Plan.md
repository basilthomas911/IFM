# Live Option Chain Latency Implementation Plan

Status: Core optimizations implemented; full acceptance blocked by intermittent quote-age failures. Date: October 9, 2026.

## Objective and acceptance targets

Reduce the time from enabling Live Feed or selecting a maturity to a usable four-leg Iron Condor. Target the 95th percentile at two seconds for a reused subscription and five seconds for a cold subscription under healthy provider conditions. Measure both four selected identities and four fresh quotes with the required selection Greeks; displaying placeholders does not satisfy the usable-leg target.

Live prices must remain live. Preserve quote freshness, contract mapping validation, worker generation fencing, execution eligibility, and independent ownership of trade-monitoring subscriptions. User Live Feed off must release the view's subscription ownership.

Related specification: [Contract Reference and Option Pricing Specification v3](ES-Trade-Blotter-Contract-Reference-and-Option-Pricing-Specification-v3.md).

## Verified baseline

The October 9 API log contains an evaluated-chain request starting at 12:04:22 Eastern that took 11.76 seconds for 160 contracts. Preparation through reference publication took approximately 5.87 seconds, reference qualification 4.50 seconds, context preparation 0.39 seconds, and the first acquisition 0.51 seconds. The remaining processing took approximately 0.49 seconds. The 5.87-second interval includes several operations and needs finer instrumentation before assigning its cost to any one operation.

Later requests repeatedly qualified alternating 106 and 108 contract scopes. Requests reusing an existing chain completed in approximately 20 to 25 milliseconds. These are backend request timings, not measured UI completion times.

The current lease comparison requires an identical contract set. A changed window releases the previous lease before acquiring a replacement. Default Iron Condor selection assigns four identities together, but a wing selected from definitions may be absent from the returned evaluated rows, and the preview builds legs only from those rows.

## Stage 1 Measure the complete selection path

- Carry a correlation ID from the UI request through definition loading, window calculation, reference publication, qualification, session acquisition, capture, pricing readiness, and UI binding.
- Log maturity, underlying, worker generation, scope identity, cache hits, contract count, subscription reuse or replacement reason, and selected contract IDs.
- Record elapsed time for four identities selected and for each leg's first fresh quote and required Greeks. Record cancellation, unavailable inputs, and timeouts as outcomes.
- Measure automated callers through the same readiness boundary so UI improvements cannot conceal backend latency.

Acceptance: one trace explains every delay between Live Feed activation and a usable four-leg selection, without high-volume per-tick logging.

## Stage 2 Cache maturity metadata and qualification

Primary components: SecuritiesDbContext, QualifiedEvaluatedOptionChain, EuropeanOptionUniverse, and OptionChainWindowInputs.

- Share one definition load across provider roots for a maturity rather than repeatedly reading the same published partition.
- Cache immutable definitions and qualified reference data by published generation, contract identity, and mapping version. Preserve policy dependencies in cache keys or invalidation.
- Prepare metadata asynchronously when a maturity is selected. Metadata preparation must not silently start a live subscription while Live Feed is off.
- Coalesce identical cache loads, remove failed loads, and bound cache size. Invalidate relevant entries after publication changes and worker replacement where runtime state is involved.
- Batch convention reads where supported. Keep bounded concurrency for remaining reads.
- Reuse volatility and window inputs across requests with explicit freshness limits. Run independent reads concurrently; eliminate duplicate reads between definition and evaluated-chain queries.

Acceptance: repeated requests for unchanged published metadata perform no repeated per-contract database qualification. Changed mappings and generations cannot reuse stale qualification.

## Stage 3 Stabilize subscription ownership and strike coverage

Primary components: QualifiedEvaluatedOptionChain, QualifiedCompositionDiscovery, and WorkerOptionChainRuntime.

- Separate the displayed strike window from the subscribed strike scope. Retain a bounded coverage buffer and recenter only when coverage becomes insufficient, using hysteresis to avoid boundary oscillation.
- Include the configured spread widths when choosing coverage. Once selected, include all four required leg IDs explicitly.
- Reuse an existing lease when it covers the requested contracts and satisfies generation and lifetime requirements, even if the display requests a smaller subset.
- Correct acquisition coalescing so incompatible required-leg sets or owners cannot share an incorrectly scoped result. Merge compatible requests under serialized scope management.
- Review worker physical-session constraints before changing replacement ordering. Stage a replacement before release only where supported; otherwise perform one coordinated replacement and make the transition observable.
- Preserve reference-counted ownership and release on cancellation, maturity change, disposal, and Live Feed off. A view release must not stop subscriptions owned by trade monitoring.

Acceptance: small underlying-price changes within coverage do not restart sessions or requalify contracts. Required wings remain covered, concurrent callers remain isolated, and recovery invalidates old-generation leases.

## Stage 4 Make four-leg selection complete and responsive

Primary component: BrokerTradeBlotterView.

- Bind selected leg identities from definitions, including wings outside the first evaluated response. Display pending quote and Greek fields until fresh values arrive.
- Prioritize selected-leg quote processing and required selection Greeks. Keep nonessential chain analytics outside the selection completion path.
- Push readiness updates through existing actor messaging and UI event infrastructure, with bounded fallback polling if needed. Correlate every update with maturity, request, subscription, and generation.
- Cancel superseded work and prevent late responses from replacing the current selection. Keep manual choices stable during automatic refresh.
- Perform data preparation asynchronously and apply compact UI updates on the UI thread.

Acceptance: four distinct selected legs are always represented. Pending, stale, or missing quotes remain visibly unavailable and cannot be treated as executable prices. Switching maturity or disabling Live Feed cannot apply stale results.

## Stage 5 Verify backend behavior and latency

- Test versioned cache invalidation, concurrent requests with different required legs, failure eviction, and generation replacement.
- Test buffered scope reuse, movement beyond coverage, lease renewal, multiple owners, and owner release.
- Test a wing outside the initial displayed window, missing quotes, partial Greek readiness, manual selection, and superseded maturity requests.
- Run integration tests through the actual selection and pricing path with deterministic quote delivery; do not mock away database qualification or lease transitions in the performance benchmark.
- Separately measure live Databento cold startup and reused sessions. Use at least 30 samples for each benchmark category and report p50, p95, maximum, and failure counts. Avoid repeated paid session churn solely to collect samples.

Acceptance: correctness tests pass; warm p95 is at most two seconds and cold p95 at most five seconds under documented healthy-provider conditions. Report provider or illiquid-contract waits separately, retaining them in total elapsed-time results.

## Stage 6 Verify the UI and complete documentation

- Use FlaUI to select an Iron Condor maturity, enable Live Feed, and verify all four roles and their readiness.
- Exercise maturity changes, Live Feed off and on, missing wing quotes, manual leg selection, and worker recovery. Verify responsiveness and clean subscription release.
- Confirm execution controls remain unavailable until the existing execution requirements are met.
- Update the specification and implementation record with measured results, cache limits, coverage policy, latency traces, and remaining external constraints.
- Build the affected projects and solution, and run the focused regression and integration suites.

Acceptance: both automated and UI evidence meet the readiness targets, with no subscription leak or stale-generation update. Completion requires measured results; lower timeout values alone do not qualify as a performance improvement.

## Delivery sequence

Implement stages 1 through 4 in order, adding focused tests with each change. Complete stages 5 and 6 against the integrated path. Keep metadata caching, subscription coverage, and selected-leg readiness as separately reviewable changes so regressions can be isolated.

A provider outage or a selected option that has not supplied a fresh quote cannot be solved by caching an old price. In that case, keep the four identities visible, identify the waiting leg and stage, and report the unavailable outcome.


## Implementation record ? October 9, 2026

### Delivered changes

- Added bounded, expiring async metadata caches with shared loads, caller cancellation isolation, and failed/missing-load eviction. Immutable published references use contract/version keys (8,192 entries, one hour); definition partitions use symbol/generation/expiry keys (64 entries, ten minutes). Publication state is still checked before partition reuse. Stored window metadata has a 30-second lifetime; the current futures price is read separately.
- Serialized acquisition changes per underlying/maturity. Stable view owners share a covered lease; releasing one owner preserves another owner's subscription. Owners retain required-leg identities and expire after 90 seconds without renewal.
- Buffered subscribed coverage by the largest of 25% of the display range, configured wing width, and five price points. A scope exceeding 2,048 contracts falls back to the requested scope rather than silently clipping required contracts. Coverage reuse requires matching definition digests and mapping versions. Worker generation checks remain active.
- Deferred reference publication and qualification until a new or changed scope requires them. Independent metadata reads run concurrently.
- Added a browsing capture that reuses qualified IV/delta and computes full Greeks for explicitly selected risk contracts. Execution captures retain their strict validation path.
- Selected wings from definitions remain visible as pending rows when quotes are absent. Four-leg readiness requires four fresh quotes and qualified deltas; placeholders do not count. Manual/default identities remain stable during refresh.
- Retained bounded polling because the current worker interface provides capture requests rather than a readiness notification subscription: 100 milliseconds for the first five seconds, then one second. A new event transport was not introduced.
- Added owner/scope/cache/generation timing logs and a four-leg UI readiness measurement. Unavailable browsing quotes carry rejection reasons and last observed event/receive timestamps; those diagnostic timestamps never qualify a price for execution.

### Verification completed

The final solution build passed with zero warnings and errors. Focused verification passed: three async-cache tests, 18 option-chain domain tests, 95 pricing tests (one existing skip), and six WinForms behavior tests. After the diagnostic changes, 50 pricing tests passed again with the same existing skipped stress test. Diagnostic wire round-tripping retains unavailable status and never restores a suppressed price. Optional null diagnostics are excluded from semantic JSON hashing so existing valid snapshot content does not acquire new null fields. The final UI regression run passed eight tests; its two opt-in live methods return without contacting the provider when live testing is disabled, so those two results are not live evidence.

BenchmarkDotNet 0.15.8 ShortRun completed all eight measurements:

| Operation | 160 contracts | 512 contracts |
| --- | ---: | ---: |
| Previous sorted identity comparison | 21.33 microseconds / 3,440 bytes | 69.59 microseconds / 9,776 bytes |
| Coverage check with an existing index | 0.95 microseconds / 32 bytes | 3.26 microseconds / 32 bytes |
| Build buffered scope | 44.42 microseconds / 42,456 bytes | 119.50 microseconds / 128,584 bytes |
| Immutable reference cache hit | 53.95 nanoseconds / 48 bytes | 54.65 nanoseconds / 48 bytes |

These are local operations, not Databento startup or complete query timings. The coverage microbenchmark starts with an existing index; production acquisition also constructs that index. Sorted equality and coverage intentionally answer different ownership questions. ShortRun uses three measured iterations and has wide confidence intervals; do not infer production latency from these means.

### Live evidence and acceptance status

Tests use the running development API over NATS, real database qualification, and GLBX.MDP3 subscriptions. No orders are submitted. The selected maturity is November 20, 2026, on ES20261218 with 50-point wings and approximately 16-delta shorts. Each warm run captures 30 samples and verifies that releasing the first owner does not stop the second owner.

| Run | Initial four-leg readiness | Warm p50 | Warm p95 | Warm maximum | Unavailable samples |
| --- | ---: | ---: | ---: | ---: | ---: |
| Initial diagnostic run | 4,434 ms | 32.86 ms | 91.34 ms | 98.19 ms | 1/30 |
| Bounded readiness polling | 1,733 ms | 27.43 ms | 2,003.57 ms | 2,018.92 ms | 2/30 |
| Final check before rejection diagnostics | 6,349 ms | 29.54 ms | 171.10 ms | 2,085.23 ms | 1/30 |
| Diagnostic capture run | 3,280 ms | 37.78 ms | 178.81 ms | 233.63 ms | 0/30 |
| Extended stability check | 2,425 ms | 30.63 ms | 81.52 ms | 1,006.59 ms | 0/120 |
| Longer diagnostic stability run | 2,428 ms | 32.65 ms | 302.38 ms | 2,094.60 ms | 5/300 |

The initial diagnostic run measured one capture per sample and did not include a readiness retry wait. Later runs include up to two seconds waiting for all four quotes. Failure counts are retained. Initial readiness observations reuse published metadata between runs and are not 30 independent cold-start samples, so they do not establish cold p95.

FlaUI selected the intended maturity and verified the selected-leg grid/status against the live backend. One run reached four-leg readiness in 2,049.96 milliseconds, excluding the later UI Automation inspection overhead. The final build's UI run also displayed four usable legs, but needed 8,563.47 milliseconds and correctly failed the five-second assertion. The UI gate is therefore not passed consistently. The harness uses the native combo popup's UI Automation selection pattern and asserts the selected expiry; earlier rapid keyboard navigation was unreliable. An earlier UI measurement used the first expiry because automated selection had not reached the requested maturity; it is excluded as an invalid measurement. The harness now asserts the selected maturity before enabling live mode.

The extended warm run passes its latency and readiness assertions, but consistent live acceptance remains **open**. Multiple runs show wing rows becoming unavailable while the short legs remain usable. During the 6,349-millisecond startup outlier, reference qualification completed in under one millisecond, pricing-context preparation in 12 milliseconds, and worker acquisition in 618 milliseconds. The subscription was ready after 621 milliseconds; most of the remaining time was waiting for four usable quotes/selection values. This rules out reference qualification as the main delay in that sample. The 300-sample follow-up reproduced five failures and identified `QuoteAgeExceeded`. For example, sample 25 was captured at 17:14:59.4688886 UTC; P7650's last event was 17:14:56.708261 UTC (2.76 seconds old) and P7700's was 17:14:56.3510691 UTC (3.12 seconds old). Other failing samples show the same rejection. These selected risk contracts use the worker's current raw quote rather than the browsing selection cache. They exceeded the unchanged one-second quote-age policy; the failures were not clock-lead or generation rejections. These diagnostics do not establish why new quotes were absent, so they do not prove provider inactivity rather than ingress/processing delay. The 120-sample passing run demonstrates the improvement when quotes are available, but does not remove this intermittent constraint. Quote-age, clock, generation, and execution checks have not been relaxed to make a test pass.

### Remaining acceptance work

- Resolve or explicitly qualify the repeated selected-option `QuoteAgeExceeded` failures and the observed startup outlier. The current source-age policy requires a quote no older than one second; a two-second readiness guarantee cannot be met when no qualifying quote arrives. Distinguishing an unchanged healthy book from a stale feed requires an explicit selection-validity policy and feed-health evidence, while execution freshness remains separately enforced. Earlier failing runs remain unresolved even when subsequent runs pass; average query speed is insufficient.
- Establish representative cold-start percentiles without unnecessary repeated paid-session churn.
- Complete the full live maturity/off-on/manual-selection/recovery matrix after the readiness gate passes. Focused tests cover missing wings, partial values, coverage/version changes, and shared ownership, but do not constitute that entire live matrix.

### Evidence and reproduction

- Benchmark source: `TomasAI.IFM.Domain.MarketData.Benchmarks/OptionChainLatencyBenchmarks.cs`.
- Benchmark command: `dotnet run --project TomasAI.IFM.Domain.MarketData.Benchmarks -c Release -- --filter '*OptionChainLatencyBenchmarks*' --job short --artifacts .artifacts/option-chain-benchmarks`.
- Live integration test: `OptionChainLatencyLiveTests.Four_live_legs_are_ready_and_owner_release_preserves_other_owner`; enable with `IFM_CHAIN_LATENCY_LIVE=true` and a running development API/feed. `IFM_CHAIN_WARM_SAMPLES` allows 30 to 300 captures on the same subscription.
- FlaUI test: `TradeBlotterLiveChainTests.Live_four_leg_selection_latency_is_verified_with_FlaUI`, with the same opt-in environment variable.
- Reports: `.artifacts/option-chain-live-initial-report.json`, `.artifacts/option-chain-live-report.json`, `.artifacts/option-chain-live-final-report.json`, `.artifacts/option-chain-ui-live-report.json`, `.artifacts/option-chain-ui-live-final-report.json`, `.artifacts/option-chain-live-diagnostic-report.json`, `.artifacts/option-chain-live-extended-report.json`, `.artifacts/option-chain-live-stability-report.json`, and `.artifacts/option-chain-benchmarks/results/`.
- The development apps are restarted through the standard development scripts after builds. Tests release their subscription owners and do not submit financial trades or reset development databases.


### Completion boundary

Implementation of the caches, buffered ownership, selected-leg rendering, pricing separation, diagnostics, and BenchmarkDotNet harness is delivered and builds successfully. Full plan completion is blocked at live readiness acceptance: selected raw quotes sometimes exceed the retained one-second source-age limit, and the final FlaUI startup sample exceeds five seconds. The full live transition/recovery matrix and representative cold p95 qualification remain outstanding. No stale price has been promoted to usable status to hide these failures. Development API, UI, scheduler host, and Server Manager are running after verification.
