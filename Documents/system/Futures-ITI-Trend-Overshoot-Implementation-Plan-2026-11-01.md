# Futures ITI trend overshoot implementation plan

Date: 2026-11-01  
Status: Implementation and affected-suite qualification complete. The original BDD and ITI shutdown blockers were resolved by the follow-up repairs below.

Add `TrendOvershootExtended` and `TrendOvershootReversal` to `IntrinsicTimeModeType`. Emit them through the existing durable Futures ITI signal flow when a qualifying extreme or reversal movement produces a signal with `BandLevel > 1.0`. Resume ordinary mode classification when the evaluated level returns to `1.0` or below. Preserve existing direction changes, trend identity, threshold calculations, event ownership and workflow deduplication.

## Signal rules

One accepted observation produces at most one existing `FuturesItiSignalGeneratedEvent`, with the applicable mode in its signal snapshot. The two additions are mode values within the existing message family. They do not require parallel copies of the ordinary and overshoot event.

| Existing structural condition | Evaluated candidate BandLevel | Resulting mode |
| --- | --- | --- |
| True trend-direction trigger crossed | Any | `TrendDirectionChanged` |
| New extreme advances by at least the active BandSize | Greater than 1.0 | `TrendOvershootExtended` |
| Reversal advances against the trend by at least the active BandSize | Greater than 1.0 | `TrendOvershootReversal` |
| New extreme advances by at least the active BandSize | At or below 1.0 | `TrendExtremeChanged` |
| Reversal advances against the trend by at least the active BandSize | At or below 1.0 | `TrendReversalChanged` |
| Existing generic band-anchor movement qualifies without a new extreme or reversal | Any | `Trending` |
| No existing movement condition qualifies | Any | No generated signal |

In an uptrend, extreme movement is `price >= prior.TrendExtreme + priorBandSize`, and reversal movement is `price <= prior.TrendReversal - priorBandSize`. In a downtrend, extreme movement is `price <= prior.TrendExtreme - priorBandSize`, and reversal movement is `price >= prior.TrendReversal + priorBandSize`. Existing direction-change checks retain first priority.

Use the current evaluator's persisted BandSize and existing fallback rules to qualify movement. Enrich the candidate using the existing current observation, VX input and threshold calculation, then use that candidate's resulting BandLevel for overshoot classification. A previous signal's BandLevel must not delay entry or exit by one observation. The final published snapshot must still satisfy the existing BandLevel and ReversalLevel formulas.

The strict comparison is `BandLevel > 1.0`; exactly `1.0` uses ordinary processing. Do not add a tolerance that silently moves this boundary. Reaching the threshold without qualifying movement creates no extra event. There is no latched overshoot flag: dropping below the threshold preserves the current trend, group, extreme/reversal history and hold/trade state, then applies ordinary classification to the next qualifying result. Re-entering overshoot can occur again in the same trend group.

An extreme event continues to set both TrendExtreme and TrendReversal to the observed price and to update the directional trailing triggers. An overshoot reversal retains TrendExtreme, updates TrendReversal and preserves the existing reversal trigger behavior. A price gap spanning several bands produces the existing single signal at the observed price; it does not manufacture intermediate prices or duplicate workflow starts.

Reversal tracking is relative to the latest established extreme, both above and below BandLevel 1.0. In an uptrend, each qualifying higher extreme resets TrendReversal upward to that new extreme; subsequent qualifying pullbacks move TrendReversal downward until another higher extreme resets it again. In a downtrend, each qualifying lower extreme resets TrendReversal downward; subsequent qualifying rebounds move it upward. TrendReversal is therefore not a permanent group-wide low or high. Whipsawing across BandLevel 1.0 alone does not reset either anchor or generate an event.

## Existing implementation and constraints

- [FuturesItiSignalCompute](../../TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Command/Model/FuturesItiSignalCompute.cs) owns the structural decisions, enrichment and formulas. Production currently sets `DefaultBandPercentage = 0.20`. Its summary still says ten percent; some tests and validation rungs refer to fifteen percent. Preserve the production 20% policy and correct identified stale descriptions/fixtures separately from overshoot assertions.
- [IntrinsicTimeModeType](../../TomasAI.IFM.Application.Contracts.MarketData.Analytics/IntrinsicTimeModeType.cs) currently assigns values 0–6 implicitly. Append explicit values `TrendOvershootExtended = 7` and `TrendOvershootReversal = 8`, preserving all existing values. Update [ToStringFast](../../TomasAI.IFM.Application.Contracts.MarketData.Analytics/IntrinsicTimeModeTypeExtensions.cs).
- Enrichment currently updates trailing triggers only for `TrendDirectionChanged` and `TrendExtremeChanged`. Simply renaming an extreme before enrichment would omit a necessary trigger update. Keep structural action and final mode classification separate, or explicitly include the new extreme mode in the same enrichment semantics.
- [Storage query code](../../TomasAI.IFM.Application.Storage/MarketDataDb/MarketDataDbContext.cs) and [mode lists](../../TomasAI.IFM.Application.Storage/MarketDataDb/MarketDataDbContextExtensions.cs) explicitly enumerate ordinary structural modes. Existing latest-extreme and latest-reversal queries must recognize their overshoot counterparts.
- [FuturesItiSignalDataReadModel](../../TomasAI.IFM.Application.Contracts.MarketData.Analytics/ViewModels/FuturesItiSignalDataReadModel.cs) has direction, extreme and reversal slots. Populate extreme/reversal slots with the latest applicable ordinary or overshoot snapshot, retaining its actual mode. Separate new aggregate fields are unnecessary for these semantics.
- [Generated completion handling](../../TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Event/FuturesItiSignalGeneratedComplete.cs) already publishes UI/Market Outlook updates, requests derived periods and starts the workflow. Retain that one durable completion path and its identities.
- The [Rust UI contract](../../TomasAI.IFM.UI.Rust.eGui/src/models/trade_contracts.rs) pins integer decoding and text labels. Both mappings and their fixtures need the new values.

## Implementation sequence

| Phase | Work | Exit evidence |
| --- | --- | --- |
| 1 Baseline and contracts | Capture current focused test results and relevant source status; distinguish existing band-percentage fixture mismatches. Append enum values and string mappings; inventory mode switches, filters and contract fixtures. | Existing enum values remain 0–6; new values roundtrip as 7/8 with exact names. Baseline failures are recorded with causes. |
| 2 Evaluator | Retain direction/extreme/reversal/trending priority and movement anchors. Apply new mode classification from the enriched candidate BandLevel. Preserve trailing-trigger updates and full snapshot contents. | Boundary, direction, anchor and state-transition tests pass for both trends and all three timeframes. |
| 3 Persistence and retrieval | Keep the existing event family and projection identities. Include new modes in mode lists, MDI/history queries and latest structural-category retrieval. Merge ordinary/overshoot rows using existing sequence ordering and trend-group boundaries. | Real-store roundtrip, latest-result and mixed-history tests return the correct new modes without hiding or duplicating signals. |
| 4 Workflow and timeframe propagation | Verify UI notifications, Market Outlook/cache updates, strategy admission and Daily-to-Weekly/Monthly fan-out accept both modes. Derived frames calculate their own thresholds and modes from the persisted source inputs. | One workflow admission per eligible completion; deterministic child command identities and no recursive fan-out. |
| 5 UI and wire compatibility | Update C# and Rust mode decoding, labels, serialization and affected fixture/generation inputs. Keep MessagePack field keys and historical event identities unchanged. | Old and new enum fixtures, signal snapshots and mixed-history display pass without fallback numeric labels for 7/8. |
| 6 Combined qualification | Run focused tests first, then affected broader suites. Run isolated actor/store/NATS cycles, replay/hydration checks and development build checks. Update the existing ITI specification with accepted semantics and record results. | All acceptance criteria below pass; package/build validation uses the development profile and full debug information. |

Storage columns inspected for the ITI modes use text. No table redesign is anticipated, but implementation must check active schema constraints, indexes, parameter mappings and any supported-provider differences before declaring that no migration is needed. Preserve the exact persisted mode names and typed parameter conventions.

## Test matrix

Run applicable evaluator cases for **UpTrend and DownTrend**, across **Daily, Weekly and Monthly**. Assert the actual output BandLevel and structural state, not just the enum name or a price derived from the previous threshold.

| Test | Required outcome |
| --- | --- |
| Qualifying new extreme below 1.0 | Ordinary `TrendExtremeChanged`; no overshoot mode. |
| Qualifying reversal below 1.0 | Ordinary `TrendReversalChanged`; no overshoot mode. |
| Exactly 1.0 | Both structural categories remain ordinary. |
| Representable values immediately below/above 1.0 | Classifier tests use `Math.BitDecrement(1d)`, `1d`, and `Math.BitIncrement(1d)`; only the value above 1 selects overshoot. |
| First qualifying extreme crosses from below to above 1.0 | That same event is `TrendOvershootExtended`, with final BandLevel strictly greater than 1.0. |
| Further extension above 1.0 | Each qualifying extreme advances the extreme and reversal anchors, preserves group identity and correctly updates trailing triggers. |
| Qualifying pullback while still above 1.0 | `TrendOvershootReversal`; extreme remains unchanged and reversal advances by the required band. |
| New extreme after an earlier reversal | Reset TrendReversal to the new extreme in both ordinary and overshoot modes. A subsequent band-qualified pullback/rebound can emit another reversal even if it has not passed the previous extreme's reversal value. Mirror for both trends and verify group identity is unchanged. |
| Prior level above 1.0, candidate below 1.0 | First qualifying result returns immediately to ordinary mode; no reset, extra direction change or one-event delay. |
| Prior level above 1.0, candidate exactly 1.0 | Ordinary processing on that event. |
| Return to overshoot in the same trend | Overshoot mode resumes when the new candidate qualifies above 1.0; group remains unchanged. |
| Movement just short of one band | No extreme/reversal event unless another existing higher-priority or generic movement condition independently qualifies. Configure fixtures so this distinction is explicit. |
| Movement exactly one band | Qualifies using the existing inclusive movement comparison; mode follows final BandLevel. |
| Price gap spanning multiple bands | One generated event at the observed price, correct mode, no synthetic intermediate observations. |
| Direction-change trigger reached during overshoot | `TrendDirectionChanged` wins and starts the next group with existing initialization behavior. |
| Rebound within the established extreme | Existing `Trending` behavior remains; a rebound alone must not be mislabeled as a new overshoot extreme. |
| VX/threshold changes | Use the newly calculated threshold to classify the candidate. Crossing a level due to threshold changes alone does not bypass the existing movement gate. |
| Hold, clear-hold and trade-state behavior | Existing dedicated hold events and trade-state transitions remain intact; new structural modes preserve the applicable state. |
| Frame rollover and independent periods | New frame follows existing initialization. Daily, Weekly and Monthly modes are independently calculated and correctly partitioned. |
| Durable persistence and retrieval | Both new modes roundtrip with exact snapshot values and are returned by applicable history, MDI, aggregate and latest-category queries. |
| Mixed ordinary/overshoot history | Latest extreme/reversal category is selected by sequence within the existing group boundary, not by a preferred enum or numeric enum order. |
| Replay and restart | Rehydrating a stream ending in either new mode yields the same next decision as uninterrupted execution. Projection replay does not duplicate records or relabel historical modes. |
| Completion redelivery and derived periods | Existing command/completion deduplication is preserved. Daily child commands remain deterministic; Weekly/Monthly do not recurse. |
| C# and Rust compatibility | Old values 0–6 keep their meaning; 7/8 carry exact new labels; MessagePack and supported JSON forms roundtrip; unrelated unknown-enum handling remains intact. |

Exact-boundary classifier tests should use deliberately constructed candidate values. Full evaluator/command tests must also validate real threshold calculations. Since threshold depends on price, VX and direction, multiplying a **previous** threshold by a desired level is not sufficient proof that the resulting candidate actually equals or exceeds 1.0. Include both deterministic near-boundary cases and realistic calculated command sequences.

## Test locations and execution

Extend these existing tests and add narrowly scoped overshoot fixtures where needed:

- [FuturesItiSignalCommandTests](../../TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesItiSignal/FuturesItiSignalCommandTests.cs): structural decisions, complete Up/Down cycles, band boundaries, hold behavior and all timeframes.
- [FuturesItiSignalQueryTests](../../TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesItiSignal/FuturesItiSignalQueryTests.cs): structural-category selection and mixed-mode results.
- [FuturesItiSignalLevelCycleIntegrationTests](../../TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalLevelCycleIntegrationTests.cs): actual durable Up/Down/Up cycle, overshoot entry/reversal/exit, stored rows and final timeframe state. Replace obsolete fixed 15% validation rungs with fixtures consistent with the unchanged 20% production policy.
- [Daily pipeline](../../TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalDailyPipelineIntegrationTests.cs), [command API](../../TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalCommandApiTests.cs), [query API](../../TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalQueryApiTests.cs) and [realtime actor](../../TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalRealtimeActorIntegrationTests.cs): event flow, period fan-out, no-change ingress behavior and API visibility.
- [MarketDataDbTests](../../TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/MarketDataDbTests.cs): both modes in persistence, history and latest-category retrieval, preserving ordering and projection identities.
- Existing intrinsic-time workflow tests in `TomasAI.IFM.Domain.Trade.IntegratedTests` and `TomasAI.IFM.Domain.Trade.VerificationTests`: both new modes reach the workflow and regime inputs through the existing completion boundary.
- [typed_trade_contracts](../../TomasAI.IFM.UI.Rust.eGui/tests/typed_trade_contracts.rs) and affected strategy display/history fixtures: integer/name decoding and visible labels for both modes.

Start with filtered Futures ITI BDD tests, then the ITI integration collection and relevant storage/workflow tests. Finish with the affected project suites and Rust contract/display tests. Build Rust UI verification artifacts with `--profile development`; development debug information remains at the project's configured setting and must not be reduced for testing. Reuse baseline timing methodology only if profiling identifies a material added cost; this feature does not require a new optimization experiment.

ITI integration tests explicitly disable parallelization because their hosts share durable consumers and integration databases. Keep that collection serial, use isolated test identities and supported fixtures, and verify required local services before running. If a required service or fixture cannot be isolated from a running application, record the blocker and continue independent unit/contract work. Do not treat skipped integration cases as passed.

## Acceptance and handoff

1. Every generated `TrendOvershootExtended` or `TrendOvershootReversal` has a finite **BandLevel strictly greater than 1.0** and the corresponding band-qualified structural movement.
2. Tests positively prove **both** new modes are generated above 1.0, in both trend directions and all supported timeframes. Tests also prove they are absent at or below 1.0.
3. Crossing back below the threshold resumes ordinary processing without a trend reset; direction-change priority, trailing triggers, anchors, formulas and state behavior remain correct.
   Every qualifying extreme extension resets TrendReversal to the new extreme, so reversal events remain relative to that latest extreme throughout the group.
4. The first qualifying crossing is classified from the current candidate, with no one-event delay. At most one event is generated per observation.
5. Source events, projection, storage queries, replay, workflow inputs and UI contract decoding all preserve and expose the new modes. Existing numeric values and wire fields remain compatible.
6. Required tests pass, and any service-dependent blocker is explicitly identified with the missing validation. Record commands, results and relevant fixtures in the completion report.
7. Deliver the code/test diff, an updated [ITI timeframe specification](Market-Data-ITI-Completion-Driven-Timeframe-Specification.md) and development build verification. Deployment, application restarts and committing/pushing changes are separate actions.

The strict boundary, candidate-based classification, one-event behavior and ordinary handling at exactly 1.0 are the working interpretations from the preceding confirmation. No change to band percentage, threshold mathematics, trading policy or automatic order execution is included.

## Initial implementation and validation — 2026-11-01

Phases 1–5 were implemented; the initial phase 6 run found the qualification blockers below. The follow-up section records their resolution. Both new modes use the existing generated-event family,
with candidate-based classification after trigger/anchor enrichment. C# and
Rust preserve numeric values 0–6 and append 7/8. Storage category queries merge
ordinary and overshoot records by sequence. Market Outlook eligibility,
latest/extreme/reversal cache positions, and the .NET status console accept the
new structural modes; Rust uses their exact typed labels. No schema migration
is required for the existing text columns.

All ITI calculation fixtures use the **20% threshold band policy**. Tests retain
zero/omitted-band input only where explicitly exercising historical snapshot
fallback; restored values must equal 20%. The storage fallback for omitted
historical band fields was corrected from 15% to 20%; explicitly persisted band
values remain unchanged. The production evaluator already used 20%, and its
threshold formula and Daily/Weekly/Monthly scales (1/20/60) were preserved.
Stale documentation and test expectations were aligned with those defaults.

The original focused BDD baseline had 49 passing and 18 failing tests out of
67. The failures included stale 15%/timeframe expectations and mocks configured
for obsolete query overloads. The repaired focused suite passes all 67.

| Validation | Result | Evidence |
| --- | --- | --- |
| Entire Analytics unit suite, including 38 dedicated overshoot cases, workflow admission and both Market Outlook cache categories | 1,253 passed | `.artifacts/iti-overshoot/20261101/unit-qualified.log` |
| Focused ITI BDD suite | 67 passed | `.artifacts/iti-overshoot/20261101/bdd-qualified.log` |
| PostgreSQL overshoot persistence, sequence/category ordering, MDI/history, idempotent projection and missing-band fallback | 1 passed against owned isolated containers | `.artifacts/iti-overshoot/20261101/postgres-overshoot.log` |
| Rust typed mode, registry display and numeric/name compatibility | 7 passed, locked development profile | `.artifacts/iti-overshoot/20261101/rust-qualified.log` |
| .NET status console and overshoot extreme notification | 5 passed, Debug | `.artifacts/iti-overshoot/20261101/ui-status.log` |
| ITI API, realtime, durable Up/Down/Up cycle and Daily fan-out | 12 test cases passed; overall run failed during shared-host shutdown | `.artifacts/iti-overshoot/20261101/pipeline-qualified.log` |
| Entire Analytics BDD project (wider diagnostic run) | 432 passed; 45 failed outside ITI: ADX 7, ATR 10, TDI 7, Trade Signal 21 | `.artifacts/iti-overshoot/20261101/bdd-all.log` |

The initial broad ITI integration run uncovered a fixture still declaring the
Scylla provider for a PostgreSQL connection, unspecified DateTime timestamps,
and tests deleting shared stream history while an actor retained its live
state. The fixture now uses PostgreSQL, API observations use explicit UTC,
each API test owns its contract identity, and listener observations are filtered
by that identity. The subsequent 10-case run passed including orderly host
shutdown; final qualification additionally exercises API retrieval of both new
modes. All 12 cases passed, but the overall run failed at teardown because
`FuturesTradeSessionBarSignalRealtimeActor.RunBarrierLoopAsync` accessed an
already disposed `NatsActorProducer`; supervisor shutdown also reported a
disposed semaphore. This is an intermittent shared-host lifecycle blocker
(the earlier 10-case run exited successfully), not a passed integration run.
Production event-stream concurrency enforcement was left intact.

The wider BDD failures are outside the ITI tests and prevent a claim that the
entire Analytics BDD project is green. They include stale query-overload mocks
and logging assertions in those other signal domains. They are recorded,
without expanding this implementation into those separate refactors. No
overshoot calculation/persistence assertion failed. At that point, clean integrated
qualification was blocked by the host teardown failure and the wider BDD
failures. The follow-up repairs below resolve those failures.

.NET validation uses Debug builds. Rust validation uses the existing development
profile without reducing its configured debug information. No optimization
benchmark was required for this mode-classification feature. The application
was not deployed or restarted, and changes were not committed or pushed.

Reproduce from the repository root using `dotnet test <project> -c Debug
--artifacts-path C:/repos/IFM/.artifacts/iti-overshoot/20261101/dotnet`; apply
`--filter FullyQualifiedName~FuturesItiSignal` to the BDD/integration projects,
and `--filter FullyQualifiedName~StatusConsoleViewModelTests` to the .NET UI
project. The PostgreSQL test filter is
`FullyQualifiedName~Iti_overshoot_modes_roundtrip`. Rust verification command:
`cargo test --locked --manifest-path TomasAI.IFM.UI.Rust.eGui/Cargo.toml
--profile development --jobs 4 --test typed_trade_contracts`.

## Follow-up test repairs — 2026-11-01

All 45 previously failing BDD cases now pass. ADX, ATR, TDI and Trade Signal
mock setups and call assertions explicitly target the cancellation-aware
storage overloads. The two Trade Signal logging fixtures enable Error logging,
preserving their exact error-log and retry assertions.

The shared integration fixture now observes hosts started lazily by class
fixtures, requests normal application shutdown, and awaits the real API
lifecycle task before disposing the host or infrastructure. This lets the
application finish actor shutdown and SimpleInjector cleanup while their
Microsoft DI dependencies remain alive. Shared service configuration callbacks
compose, preserving feed/trade overrides alongside lifecycle tracking. No
production lifecycle or ITI calculation implementation was changed.

The Feed fixture explicitly selects its deterministic managed feed instead of
the separate native Market Data Host, matching its existing feed-factory and
watchdog test adapters. The setting is scoped to that test process and restored
with the other fixture environment settings.

| Final verification | Result | Evidence |
| --- | --- | --- |
| Entire Analytics BDD project | **477 passed, 0 failed**, including all original 45 failures | `.artifacts/iti-test-repairs/20261101/bdd-all.log` |
| ITI API, realtime, durable cycle and timeframe fan-out | **12 passed, successful assembly teardown**; no disposed-producer/semaphore failures | `.artifacts/iti-test-repairs/20261101/iti-integration-final.log` |
| Feed early-start host / Core NATS regression | **1 passed**, successful run and no disposed-producer/semaphore failures | `.artifacts/iti-test-repairs/20261101/feed-host-regression-final.log` |

The original BDD and ITI integration blockers are resolved. The extra Feed
regression emitted a separate watchdog shutdown-observation persistence warning:
`SequenceIdDbContext.GetNextSequenceIdAsync` reported a `NullReferenceException`.
The case and assembly cleanup passed; this diagnostic issue was retained in the
log rather than counted as a failed test or silently removed. The full Feed
integration project was not run. The previously qualified unit, PostgreSQL and
Rust checks remain recorded above; these repairs preserve the 20% ITI policy.

The scoped patch, source snapshots/hashes, original-to-passing BDD case mapping
and machine-readable results are in `.artifacts/iti-test-repairs/20261101`.
Changes remain uncommitted and were not deployed.
