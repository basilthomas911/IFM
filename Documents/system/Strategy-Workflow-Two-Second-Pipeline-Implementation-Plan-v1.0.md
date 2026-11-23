# Strategy Workflow Two Second Pipeline Implementation Plan

**Status:** Decision-runtime implementation and automated qualification complete; the native futures metadata publication fix is implemented and packaged. WP7/WP8 live entry/UI acceptance is **blocked** by the closed market and Windows input/capture helper, as detailed below. The overall plan is not complete.

**Date:** 2026-11-20

## Current implementation checkpoint — 2026-11-20, evening

Continued after the EOD repair. Corrected Rust's configured futures DTO publication so complete ES/MES provider definitions expose schema, exact dataset/publisher/instrument, original definition timestamp and SHA-256, expiry/last trading, monetary multiplier and tick size. Native evidence remains **Draft**, never fabricated Reviewed evidence. USD/point economics are derived from source unit quantity and verified against display factor, tick size and tick value using checked integer arithmetic; the provider's deliverable-count multiplier is not substituted. Missing/conflicting economics retain unqualified metadata and composition remains unavailable. No decision operator, strategy rule, storage fallback or retry policy changed.

Qualification: **16 native tests passed**, **2 existing tests ignored**, including the new ES/MES metadata and conflicting/missing-source cases, existing 48 option/catalog oracle cases, 90 current-future selection cases, bootstrap and repository/query checks. Targeted Rust formatting checks passed. The separate development native Host package built successfully at `.artifacts/strategy-workflow-2s/native-futures-package`; activation uses the actual API/Rust eGui/Rust MarketData development profile.

A read-only audit of **286 persisted runtime-v1 workflows** since the corrected 15:05 activation found **zero processing workflows** and **zero recorded stages >= 2,000 ms**:

| Operator | Samples | p50 | p95 | Maximum |
|---|---:|---:|---:|---:|
| Regime Discovery | 286 | 53.411 ms | 84.920 ms | 353.932 ms |
| Market Condition | 284 | 4.528 ms | 9.008 ms | 1,413.132 ms |
| Trade Selection | 112 | 26.985 ms | 49.783 ms | 253.802 ms |
| Order Composition | 4 | 50.748 ms | 357.175 ms | 357.175 ms |
| Risk Management | 0 live | — | — | — |

These are persisted stage timestamps, not paired benchmarks or successful all-five-stage live entry evidence. Risk/entry safety remains covered by the automated ready-input and disposable PostgreSQL qualification above/below. Cache failure and the legacy timeout label in the earlier sample are not actual two-second overruns.

**Concrete acceptance blockers:**

1. The Friday market session is closed. Real Host queries for both `ES20261218` and `ES20270319` return **“The market-data API does not have a running value-date epoch.”** Therefore native metadata in the active feed registry, producer cache readiness, a fresh genuine eligible signal and live Risk/entry completion cannot be qualified now. No artificial value-date epoch, stale-signal replay or financial entry was created. The next qualification must use the next genuine session and preserve all current eligibility rules.
2. Windows accessibility can read the running eGui tree: exact ES contract, Daily timeframe, matching fund/family filters and ordered saved workflow rows are present. Screenshot capture still returns `window capture timed out: timed out waiting on channel`; attempting the saved-row accessibility action returns `coordinate input geometry is unavailable`. Activation plus refreshed capture also fails. Actual filter/detail navigation therefore remains unverified; no guessed coordinates or financial actions were used.

Activation completed in the real development profile. The MarketData process loads `.artifacts/strategy-workflow-2s/native-futures-package/ifm_market_data_native.dll`, SHA-256 `6854f954004d3430f54fdd5b1fe2e1ace1edeebac807749210c9f182b99a62dc`, matching the package manifest. **`/health/ready` returned HTTP 200 at 19:53 ET** after the restart, with all components Healthy, including the five-position EOD audit/recovery. Both pre/post-activation ES queries confirm the closed-session epoch blocker. Overall acceptance remains open; no fresh live Risk/entry success is claimed.

Evidence: `.artifacts/strategy-workflow-2s/native-futures-metadata-tests.log`, `native-futures-regression.log`, `native-futures-package.log`, `native-futures-profile-*.log`, `native-futures-loaded.json`, `native-futures-health.json`, `native-futures-checkpoint.json`, `live-qualification/summary.json`, and `native-metadata-probe/before.json` / `after.json`.

## Prior implementation checkpoint — 2026-11-20, 15:12 ET

- All five decision operators use resident inputs, one attempt and a two-second deadline including preparation/queue residence. New workflow decisions have a ten-second ceiling. Capacity is bounded; overload, missing inputs and late results terminate explicitly.
- Configuration, calendar, market/history, composition routes/quotes and financial state are prepared by background owners. Decisions cannot load caches, query storage/providers, poll quotes or retry business logic.
- Local continuations run under the assignment owner between durable admission and one final evidence transaction, preserving original command receipts and causation. Admission/persistence and atomic entry commitment are separate from calculation.
- Risk calculation has no financial effects. Only acknowledged durable approval can reach the atomic revision/cash/expiry/idempotency-checked entry transaction. Uncertain writes or financial outcomes reconcile original receipts outside decisions; no calculator is replayed.
- Terminal owners remain resident for subsequent stream/arming state. Startup scanning is bounded; stopped-new-intake controls preserve reconciliation. Runtime/session/entry phases remain readable in Rust; legacy workflow fixtures are retained.
- Real ready-input qualification uncovered the old 64 KiB approval hash buffer limit. Pooled streaming preserves the exact v1 MessagePack digest and supports bounded large evidence without changing stored hashes.
- Initial live activation uncovered unassigned generic Regime scopes. Existing authored legacy profiles are now validated/published by the background producer and consumed from memory. Explicitly disabled or missing assigned versions cannot fall back. Market Condition publication checks the exact Regime profile binding in the producer.

Qualification:

| Check | Result |
|---|---|
| Final full Trade unit suite, including assigned/unassigned ready five-operator paths | **2,011 passed, zero failures/skips** |
| Portfolio unit suite | **555 passed** |
| Disposable PostgreSQL financial commitment | **5 passed**: duplicate approvals, revision conflict, No Trade, expiry and actual lost COMMIT reply |
| Isolated real API + disposable PostgreSQL | **4 passed**: real DI, two evidence transactions, lost-reply reconciliation, startup SQL and scoped large risk projection/hash |
| Rust strategy tests | **21 passed**, one existing ignored test; legacy workflow and current metadata compatibility |
| Executor burst guard | 256 requests, 4 workers/16 waiting slots: 20 completed, 236 rejected as overloaded; no retries; successful p99/max **6.453 ms** |

The burst measurement qualifies executor overload handling, not full-workflow throughput. Noncooperative work retains its worker; queue expiry and late-result rejection are covered separately.

BenchmarkDotNet ShortRun, five measured iterations, in-process toolchain on the development workstation. Measurements cover prepared binding/calculation, excluding admission, persistence and entry commitment. Confidence intervals are noisy; these are not whole-workflow guarantees.

| Strategy | Selection before → prepared | Composition binding + calculation before → prepared | Composition allocation before → prepared |
|---|---:|---:|---:|
| Long future | 5.815 → 0.290 ms | 20.35 → 14.81 ms | 6.10 → 3.38 MB |
| Bear put debit vertical | 18.018 → 0.542 ms | 73.17 → 22.99 ms | 6.96 → 4.20 MB |
| Short balanced iron condor | 5.600 → 0.302 ms | 32.85 → 28.21 ms | 9.89 → 7.14 MB |

Selection allocations fell from approximately 1.1 MB to 77–102 KiB. Composition still allocates legacy evidence/hash data; its in-process Gen 2 measurements did not establish a collection reduction. No live GC improvement is claimed.

The pre-activation latest-100-workflow sample contained RD/MC maximum durations of 121.751/117.255 seconds and TS maximum 2.376 seconds. This is observational baseline evidence, not paired BenchmarkDotNet input. Fresh activation evidence is recorded separately below when available.

Corrected Development activation completed at 15:05 ET. `/health/ready` returned **200** after indicator warm-up; deployment identity and all five open-position EOD checks are healthy. eGui launched automatically. The initial activation's generic-scope failure was repaired before this observation window.

Fresh persisted sample after the corrected restart (14 workflows, ending approximately 15:11 ET):

| Operator | Samples | p50 | p95 / maximum |
|---|---:|---:|---:|
| Regime Discovery | 14 | 61.153 ms | 353.932 ms |
| Market Condition | 12 | 5.665 ms | 1,413.131 ms |
| Trade Selection | 2 | 46.712 ms | 253.803 ms |
| Order Composition | 1 | 357.175 ms | 357.175 ms |
| Risk Management | 0 live | — | — |

No observed stage reached two seconds. These are a small mixed-input observational sample, not paired benchmarks or proof of all-five-stage live entry performance. Nine workflows rejected triggering events older than 30 seconds, two encountered startup EMA unavailability, one stopped at the options-only VX backwardation veto, one stopped at the existing consumed-DC rule, and one selected a Daily future but could not compose it. The latter's top-level legacy stop label is `PipelineTimedOut`; its actual recorded failure is `OC.CONFIG.CACHE_NOT_READY`, not a two-second overrun. Input rejection did not retry or query missing data in the decision.

### Historical live qualification blockers at 15:12 ET

1. **Native futures metadata publication:** the live background producer repeatedly reports `OC.INPUT.FUTURE_METADATA_NOT_READY`. `TomasAI.IFM.Application.MarketData.Rust/src/catalog/option_models.rs`, function `future`, explicitly emits default zero/nil values for extended futures DTO fields 11–31. The composition cache requires native/reviewed schema, dataset, last-trading time, positive multiplier/tick size and a definition digest. The legacy trading contract/quote is sufficient for feed readiness but does not meet this composition contract. It would be incorrect to invent these fields, guess last-trading time, loosen the requirement or fetch them during decisions. Complete live entry qualification requires an authoritative metadata publication path, followed by a fresh genuine selected signal. Risk/entry behavior remains covered by automated ready-input and disposable-PostgreSQL tests, not a new live order.
2. **Windows visual verification:** the Windows computer-use helper repeatedly timed out capturing the eGui window, including after reset (`window capture timed out` / `FrameArrived timed out`). Automatic launch and 21 passing Rust tests are verified; actual filter/detail navigation is not. No financial UI actions were attempted.

Early Weekly/Monthly activation-missing messages occurred before Development portfolio provisioning completed; later workflows reached their prepared stages. They are not asserted to be a persistent activation blocker.

The runtime remains fail-closed on missing metadata. No Trade decisions create no financial effects. No legacy querying fallback has been restored. New-start shutdown must retain `LocalDecisionRuntimeEnabled=true` with `Enabled=false` so reconciliation of existing original operation identities remains active; do not revert to old binaries with unresolved approvals.

Evidence directory: `.artifacts/strategy-workflow-2s/`. Current reports include `results/full-trade-legacy-cache-final.trx`, `results/full-portfolio-streaming-approval.trx`, `results/prepared-runtime-risk-projection.trx`, `results/prepared-entry-streaming-hash-postgres.trx`, `executor-burst-report.json`, both benchmark result directories, `rust-strategy-batched-v2.log`, and `live-baseline-summary.json`.

WP7/WP8 final live acceptance remains open. Historical checkpoints below describe earlier states and are superseded by this checkpoint.

## Market-close regression repair — 2026-11-20, 17:50 ET

The initial local-decision path committed admission evidence but returned before publishing it to the conventional projector. Later final evidence was queued and permanently fenced by those unpublished earlier stream versions. At 17:01 ET, market close waited two minutes for 686 replay acknowledgements, then failed. This was a regression in the optimization implementation, not a reason to purge unresolved messages or relax stream ordering.

The source owner now publishes admission and final evidence in order after calculation, keeping projector I/O outside the decision boundary. A final-reply failure still publishes the already-committed admission; original source identities remain recoverable. The stronger PostgreSQL regression test initially exposed a replay delay when final evidence was queued first; ordered publication fixes that test without changing its deadline.

After deployment/restart, original source recovery completed all pending workflow projections. Both workflow PROCESS and REPLAY queues reached zero messages and zero acknowledgements pending. Retrying today's API market close returned HTTP 200 in **1,533.5 ms**, with durable run `807afa34-da00-4e0c-85aa-b7200a24d0e4` recorded **Succeeded / MarketClosed / exit 0** at 17:49 ET. This retry finalized zero additional positions because their source EOD seals had already committed in the failed earlier close. Trade 4601 remains closed; the other five position sequences and the sampled original source-event payload checksum are unchanged.

A separate closing-book boundary bug rejected valid receive-filtered full-book updates whose exchange timestamp was just before the range start. It now accepts only the existing bounded 15-second late allowance, retaining explicit snapshots for older anchors, exact record counts, publisher/instrument checks, gap flags and final-clear invalidation. Tests: **2 disposable PostgreSQL**, **18 acquisition**, **12 closing-book**, zero failures. A real 9,345-record archive replays successfully with original timestamps and correctly yields no valid closing book after its final clear.

**EOD follow-up:** all five older open test positions contain Nov20-expiry options. The sampled authentic archive ends in an empty book before 16:00 ET, correctly preventing quote-based recovery. The expiry valuation repair below addresses this separately. Source recovery also logged durable business-subscription CapacityExceeded quarantines; no new entry was authorized by the transport fix, and those quarantines are not claimed as resolved by it.

Evidence: `.artifacts/market-close-20261120/incident-summary.json`, `close-retry-result.json`, `durable-after.txt`, `nats-after.json`, `health-after.json`, `real-book-replay.log` and `results/`.

## Verified expiry EOD recovery — 2026-11-20

Added a distinct, versioned expiry-fixing evidence record for reviewed European ES Friday weekly options. Recovery obtains the exact underlying instrument's exchange fixing (statistics type 10), checks the original record, date, instrument, publisher, published contract mapping and hashes, and records intrinsic expiry values separately from tradable bid/ask. Preliminary option settlement records are not accepted. Normal closing-book gap and final-clear checks remain intact. Unsupported expiry conventions, missing fixing evidence, partial expiries and nonzero net futures delivery remain blocked.

The authenticated Nov20 ES fixing is **6,935.07**, published at `1795208401040290345` ns UTC, sequence `131798823`, exact underlying instrument `10252`, publisher `1`. Its archive was acquired at zero cost. All five positions (1001, 2204, 2302, 2406, 2502) now have qualified Nov20 EOD seals. Original leg observation timestamps/sequences, position quantities and open status are preserved; this is valuation recovery, not exercise/account settlement or a closing execution. Trade 4601 remains closed at sequence 170272. The repair also carries definitive expiry values into an already-open later session without retiming quotes or introducing false daily gains.

Qualification: **2,024 Trade unit tests**, **18 disposable PostgreSQL recovery tests**, **24 quote acquisition tests**, and **9 planned-closure health tests**, all passing. PostgreSQL verifies invalid reference rejection, audited correction, retry/lost-reply safety and restart recovery. Existing quote hashes retain their original 13-field encoding. Live verification rechecks each fixing archive/hash and compares retained observation times and quantities with the original source event.

The post-repair restart preserved all five qualified seals and 4601's closed state. All five recovery jobs completed normally by 18:31 ET after their pre-deployment leases/backoff expired, with no further corrections required. Also fixed a separate readiness bug: a planned closed-session pipeline reported `Inactive` and was always treated as degraded despite healthy required checks. Readiness now accepts this state only without an active session, with verified session authority and all required checks healthy/inactive; invalid, missing, degraded and unknown evidence still fails. After deploying the health fix and restarting the API/Rust eGui/Rust MarketData development profile, **`/health/ready` returned HTTP 200 at 18:34 ET**, with every component Healthy. Final verification again confirmed five qualified seals, five Complete recovery jobs and trade 4601 still closed.

Evidence: `.artifacts/expiry-eod-20261120/repair-live.json`, `verify-live.json`, `final-restart-verify.log`, `health-ready-final.json`, `incident-summary.json`, `es-statistics.csv`, `results/` and profile logs.

## Execution checkpoint — 2026-11-20

The restored pre-optimization workflow suite ran 1,255 tests: **1,076 passed and 179 failed**. The isolated test command used `--artifacts-path .artifacts/strategy-workflow-2s/build` and the `FullyQualifiedName~Strategy.Workflow.IntrinsicTime` filter. The live API was not rebuilt or restarted.

Failure groups:

- 173 fingerprint/envelope cases fail while constructing `IntrinsicTimeFundAssignment`: the reflection fixture assumes a parameterless constructor, but the assignment is a positional record. These cases do not reach the intended fingerprint assertions.
- Three message-contract cases assume every event has an explicit serialization constructor. `IntrinsicTimeStrategySignalQueuedEvent` uses initialized keyed properties instead.
- The event inventory omits `IntrinsicTimeStrategySignalQueuedEvent`.
- The legacy workflow compatibility test expects 40 current fields; the current contract has 42. This assertion fails before checking the shortened legacy payload.
- The selection initialization ordering assertion expects an options lookup that the current path no longer performs.

These failures initially prevented reliable regression qualification. The constructor and inventory failures were test-fixture defects; repairing them exposed a real assignment-fingerprint omission, addressed below. They did not establish that production serialization itself was broken.

Evidence:

- [Restored baseline results](../../.artifacts/strategy-workflow-2s/results/baseline-restored.trx)
- [Failure details](../../.artifacts/strategy-workflow-2s/baseline-failures.json)
- [Restored baseline log](../../.artifacts/strategy-workflow-2s-restored-baseline.log)
- [Unactivated deadline proposal](../../.artifacts/strategy-workflow-2s/proposed/)

The draft's first test run had three additional deadline-related failures. All 14 previously edited application/configuration files were restored byte-for-byte from the pre-change copies; the added deadline helper was preserved with the draft and removed from application source. Existing unrelated workspace changes were retained. WP1–WP8 remain incomplete, and no BenchmarkDotNet, disposable-PostgreSQL qualification or live activation result is claimed.

### Baseline repair qualification

- Repaired reflection fixtures to populate positional records and nullable values, preserving every-field fingerprint mutation coverage.
- Included the durable queued-signal event in contract inventory and exercised its initialized-property serialization contract and populated round trip.
- Verified the 35-field historical workflow payload still decodes with its original identity/revision and without newer risk fields, independently of the current contract's field count.
- Replaced the obsolete Market Condition selection-lookup expectation with assertions that it requests the correct workflow snapshot without accessing selection options, configuration storage or deployment readers.
- Fixed the uncovered production defect: Regime Discovery's v1 content fingerprint omitted the assignment. New typed envelopes use a v2 fingerprint covering every assignment and deployment field. Existing v1 typed hashes and opaque payloads remain readable, with no persisted-data migration. Both storage projection paths recognize v1 and v2.
- Added a pinned pre-change v1 hash, v1/v2/opaque projection checks, assignment-tampering and version-downgrade rejection, and out-of-order JSON-property coverage.
- Workflow qualification: **1,258 passed, 0 failed, 0 skipped**, compared with the original **1,076 passed / 179 failed**. The three additional tests cover compatibility and integrity.
- Broadened verification to the entire Trade unit-test project. Repaired two further obsolete fixtures: the pre-monitoring-limit payload test assumed 18 current fields instead of 19, and the open-position monitoring fixture left `IsOpen` false. Preserved the closing guard and added coverage that refreshed monitoring inputs do not dispatch a plan while closing.
- Final full Trade unit-test qualification: **1,960 passed, 0 failed, 0 skipped**.

[Passing workflow test report](../../.artifacts/strategy-workflow-test-repair/results/repaired-v2.trx) · [Test log](../../.artifacts/strategy-workflow-test-repair/tests-v2.log)

[Passing full Trade unit-test report](../../.artifacts/strategy-workflow-test-repair/results/all-trade-unit-tests-final.trx) · [Full test log](../../.artifacts/strategy-workflow-test-repair/all-trade-unit-tests-final.log)

The live API has not been restarted. This clears the baseline blocker; it does not activate the two-second runner or complete WP1–WP8.

## 1. Objective and agreed scope

Run each of the five strategy decision stages in one attempt, with a maximum budget of 2,000 milliseconds including scheduling delay. Operators consume trusted, ready, in-memory snapshots. They perform no database queries, external data requests, cache population, quote polling or business retries.

Validate data at its producer and configuration publication boundaries. Cache owners maintain readiness and invalidate expired or inconsistent generations. Operators apply trading rules to supplied inputs without repeating schema validation, configuration hashing or source verification. The workflow manager owns workflow identity and revision checks.

Retain the current strategy rules for Daily futures, Weekly verticals and Monthly iron condors. Research backtest settings are not introduced by this work. Capital reservation and order submission occur only after a complete entry proposal passes every decision stage.

The deadline is per stage, not a claim that all five stages together take two seconds. Use a shared workflow ceiling of 10 seconds, further restricted by the accepted signal and market snapshot validity deadlines. The existing 30-second signal-age policy remains an upper eligibility boundary, not permission to wait in a queue for 30 seconds.

## 2. Execution and persistence boundaries

1. Match the exact ITI contract, timeframe, fund and strategy-family assignment using ready assignment state.
2. Assign a stable workflow ID and accept the signal through the existing durable admission boundary. Bound admission separately; unknown persistence outcomes do not create another workflow identity.
3. Run the five decision stages through the workflow manager and local calculation runner. Pass immutable results and ready snapshots directly between stages.
4. Record the final decision and its stage evidence durably. No Trade outcomes create no financial reservation.
5. Only a durably approved, unexpired entry proposal can cross the entry-commit boundary.
6. Atomically commit the capital reservation and unique order intent, then dispatch order execution.

Workflow admission, decision computation, result persistence, entry commit and broker execution have distinct timings and states. Moving persistence outside a calculation must not hide delayed durability or permit execution before commitment.

Existing Function actor APIs and historical queries remain compatible while the live decision path reuses their calculation models without per-stage transport round trips or repository checkpoint reads. Direct model calls do not change the established ActorType transport mapping.

## 3. Current implementation findings

| Area | Verified current behavior | Required change |
|---|---|---|
| Deadlines | Regime Discovery options allow a two-minute workflow duration; Function callers add five-second reply grace periods. | Replace live stage waits with the shared deadline contract and remove grace periods extending decision eligibility. |
| Workflow delivery | Critical workflow transitions currently use optional Realtime delivery and have been dropped under actor-type admission overload. | Protect admission during migration and remove dependence on optional Realtime messages for local stage continuation. |
| Current workflow state | `ReadActiveWorkflowAsync` reads the PostgreSQL active-workflow projection and deserializes it. | Use manager-owned state with workflow generation and revision ownership. |
| Configuration | The selection configuration cache can await a cold load or reload after its one-minute lease; other initializers retain direct configuration queries. | Publish fully prepared configuration generations before they become eligible for live use. |
| Market Condition | Daily futures uses cached prices and ATR, but calendar cache misses load coverage and events. The shared options observation path reads one-minute ATR and historical VX bars. | Publish ready calendar, indicator and VX context snapshots in background services. |
| Composition | Preparation checks read stored preparations. Futures preparation can poll for quotes for up to ten seconds. | Acquire an immediately available trusted market snapshot or return No Trade. |
| Risk and financial state | Portfolio evaluation reads/locks financial authority and materializes financial and order/capacity effects in its transaction. | Separate pure sizing and eligibility from the final atomic entry commitment. |
| Function persistence | Function repositories load checkpoint state and commit outcomes around calculation. | Keep live decision state in its owning manager; retain durable admission/result boundaries and historical compatibility. |

Relevant source locations:

- [Workflow realtime context](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Realtime/Actor/IntrinsicTimeStrategyWorkflowRealtimeContext.cs)
- [Workflow dispatch](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Realtime/Actor/IntrinsicTimeStrategyWorkflowRealtimeActor.cs)
- [Selection configuration cache](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/WorkflowSelectionConfigurationCache.cs)
- [Market Condition snapshot provider](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/MarketCondition/Model/MarketConditionAssessmentSnapshotProvider.cs)
- [Calendar cache](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/MarketCondition/Model/MarketConditionEventRiskAdapter.cs)
- [Composition preparation](../../TomasAI.IFM.Application.MarketData/Pricing/CompositionMarketPreparation.cs)
- [Portfolio composition transaction](../../TomasAI.IFM.Domain.Portfolio/OrderComposition/Repository/PortfolioOrderCompositionStore.cs)

## 4. Implementation work packages

### WP0 Record the baseline and dependency map

- Trace admission, queue delay, stage scheduling, calculation, persistence and financial commitment separately for each strategy family.
- Identify the exact database and external calls reached by current live bindings, distinguishing current schema paths from historical compatibility paths.
- Capture the existing reason codes, decisions, workflow IDs and stage outputs for deterministic regression fixtures.
- Inventory every timeout, reply grace period, preparation wait and synchronous cache loader on those paths.

**Completion:** reproducible baseline and an enumerated list of allowed background/commit I/O versus prohibited decision-path I/O. No trading-policy changes.

### WP1 Enforce deadlines and protect workflow scheduling

- Introduce one stage budget carried from the time a stage becomes ready; queue residence consumes that budget.
- Use a monotonic clock for elapsed execution and preserve UTC deadlines for persisted evidence and cross-process boundaries.
- Stop expired work with a specific reason; do not retry, extend the deadline or promote a late result into another stage.
- Make long calculations cooperate with cancellation and bound all candidate loops. Bound executor concurrency and queued memory.
- Preserve critical workflow admission and control under quote/EOD traffic saturation. Reuse the existing admission controller rather than creating an unbounded priority queue.
- Keep different assignment queues concurrent and preserve order within the exact contract/timeframe/fund/family queue.

**Completion:** forced scheduling delay, lost control delivery and slow-stage fixtures cannot hold a decision lane for two minutes or authorize an expired result. Record worker completion separately if cleanup outlives a rejected decision.

### WP2 Publish trusted ready snapshots

- Assign one owner to each snapshot and its readiness lifecycle.
- Validate content once at production/publication: schema, numeric validity, identity, parameter bounds and hashes.
- Preload and compile all five stages' configuration before exposing a new assignment or configuration generation.
- Replace awaited lazy cache loading with a non-blocking ready/unavailable lookup. Load and refresh only in background services.
- Carry immutable snapshot references and existing provenance rather than repeatedly copying and hashing complete catalogs.
- Retain original market timestamps. Cache updates must not relabel old observations as fresh.
- Invalidate readiness on feed loss, contract rollover, expiry, source discontinuity or configuration replacement. Perform only a lightweight validity-deadline comparison when acquiring a snapshot.
- Keep readiness independent per contract/timeframe/family. Weekly four-hour EMA200 warm-up must not block ready Daily or Monthly assignments.

Snapshot ownership and contents:

| Owner | Ready state |
|---|---|
| Assignment service | Active funds, exact contract/timeframe/family/deployment associations and their versions. |
| Configuration publisher | Decoded and validated regime, condition, selection, composition and risk policies and catalog templates. |
| Indicator producers | Completed EMA and ATR results, warm state, calculation/source identity and original timestamps. |
| Market-data owner | Quotes, trades, feed health, eligible contracts, VX pair and precomputed market context. |
| Calendar service | Trading sessions, economic-event rows and verified calendar coverage. |
| Option-chain owner | Ready strategy scopes, eligible contracts, coherent quotes and required pricing context. |
| Financial authority | Current fund/portfolio balances, exposure, positions, working orders, committed reservations and revisions. |
| Workflow manager | Current workflow stage/revision and strategy-specific ITI arming/consumption state. |

**Completion:** missing, loading or invalidated snapshots are unavailable immediately; no lookup performs I/O or waits for refresh.

### WP3 Run decisions through the owning workflow manager

- Hydrate manager state before enabling live starts; use its current state for dispatch ownership rather than querying projections.
- Invoke existing calculation models through a local runner with typed immutable stage inputs and outputs.
- Reuse prevalidated policy instances. Preserve business-rule evaluation while removing repeated schema/hash checks from operator execution.
- Keep database dependencies and provider clients out of decision-operator interfaces; guard against transitive query access in qualification tests.
- Collect stage evidence and commit the final workflow decision without per-stage Function checkpoint reads or transport hops.
- Keep one durable workflow identity and Strategy Updates entry per accepted signal/assignment. Terminal outcomes include explicit unavailability, overload, deadline and interruption reasons.

**Completion:** control fixtures produce the same trading decisions for the same accepted input snapshots, with zero data queries from the decision runner.

### WP4 Migrate the five operators

| Operator | Work to retain | Read and wait removal |
|---|---|---|
| Regime Discovery | EMA20/50/200 classification using five-minute bars for Daily, four-hour bars for Weekly and daily bars for Monthly. | Use a ready EMA snapshot and configuration generation; remove effective-parameter database fallback and revalidation. |
| Market Condition | Ready market context and the options-only VX backwardation veto. | Replace ATR/VX history reads and calendar cold loads with producer-maintained snapshots. Preserve the agreed separation from strategy eligibility. |
| Trade Selection | Existing family-specific ITI, direction, hours, event and selection rules. | Consume pinned compiled catalog/policies and manager-owned arming state. No fund or catalog query and no capital reservation. |
| Order Composition | Construct and price one bounded futures/options proposal using existing strategy policies. | Remove preparation-store reads, cold subscriptions and quote polling. A missing or incoherent quote snapshot ends the stage immediately. |
| Risk Management | Calculate allowable size and check existing exposure and limits. | Use financial-authority memory and pinned risk policies. Remove queries and financial effects from the calculation. |

Each stage captures its own newly required ready inputs once. A market snapshot that has expired cannot be refreshed and recalculated inside the same stage. Its workflow ends with the appropriate reason.

**Completion:** all five operators obey the same budget and one-attempt contract across all three families. Strategy rules and exit workflows remain unchanged.

### WP5 Separate entry commitment from evaluation

- Refactor portfolio evaluation so the decision portion does not lock/read ledger tables or create orders/capacity reservations.
- Maintain financial decision state under the financial authority's ownership. Update it from committed operations and invalidate it when state continuity is lost.
- After a complete approved proposal is durably accepted, perform one bounded atomic entry-commit operation using a unique operation/order identity.
- Within this separate boundary, verify the expected financial revision and unexpired proposal, confirm capital, and commit the reservation and order intent atomically. Database reads needed by that transaction do not run inside decision operators.
- Coordinate shared portfolio resources when multiple funds approve entries concurrently. Reject revision conflicts without rerunning the decision workflow.
- Only acknowledged committed order intent can proceed to submission. Track order fills asynchronously; a decision deadline is not an order-fill deadline.
- Reuse existing financial contracts and idempotency semantics wherever possible. Update contract versions explicitly where splitting evaluation from commitment requires it.

**Completion:** every No Trade result has zero financial effects; concurrent approvals cannot spend the same capacity; expired proposals cannot submit orders.

### WP6 Handle persistence failures and restart outside decisions

- Keep admission, result persistence and entry-commit outcomes visible as separate states and timings.
- Do not execute an approved entry before its durable decision/intent is confirmed.
- On restart, restore trusted snapshots and workflow identities. Record interrupted decision attempts as terminal; do not rerun their calculations.
- Accepted signals that have not begun a decision can receive their first attempt only if their original eligibility and scheduling deadlines still permit it.
- Reconcile uncertain reservations/submissions using their original operation and order identities outside the expired workflow. Do not infer that a timeout means no financial effect occurred.
- Keep any unresolved fund exposure visible to admission/risk state while allowing independent fund queues to continue.
- Preserve existing historical workflow/hash compatibility and the Rust eGui filters for exact contract, timeframe, fund and strategy family.

**Completion:** restart and lost-reply tests preserve one workflow/order identity, do not resurrect expired decisions, and do not create new reservations for rejected signals.

### WP7 Qualify performance and correctness

- Use BenchmarkDotNet for prepared-input calculations, configuration binding, allocations and candidate-count scaling. Keep database/network fixtures out of calculation benchmarks.
- Run end-to-end admission and workflow load tests with realistic ES/VX/EOD traffic. Report queue delay, calculation time, persistence time and entry-commit time independently, including p50/p95/p99/maximum.
- Verify the two-second stage deadline under overload, slow work, cancellation and late completion. A percentile below two seconds alone is not acceptance evidence.
- Install test-time dependency guards that fail if any decision operator invokes PostgreSQL, Scylla, HTTP, broker queries, historical acquisition or an awaited cache loader.
- Verify identical decisions and reasons for identical input snapshots. Test absent caches, stale snapshots, generation changes, rollover, calendar invalidation and independent timeframe readiness.
- On disposable PostgreSQL, verify durable admission/result recording, entry commits, concurrent funds, revision conflicts, failed commits, lost replies, restart recovery and historical queries.
- Verify Rust eGui shows each accepted signal once, accurate stage/queue timings, actual No Trade reasons, and distinct persistence/entry-commit states.

**Completion:** publish stage-by-stage timing, allocation, zero-query, correctness and restart evidence. The approved trading algorithm and financial idempotency guarantees are preserved.

### WP8 Activate in stages

- Deploy the shared cache/readiness and deadline infrastructure first.
- Compare the new decision runner with saved inputs without enabling a second execution route.
- Activate Daily futures, then Weekly verticals and Monthly iron condors after their qualification checks pass.
- Resolve any already-pending financial operations independently of decision-runner activation. Do not duplicate old in-flight orders or restart their trading decisions.
- Keep rollback controls that stop new starts while retaining original financial-operation identities for reconciliation.

**Completion:** live stage metrics meet the agreed budget and no decision-path queries, missing stage continuations or duplicate entries occur during the qualification window.

## 5. Dependencies and first implementation slice

WP0 establishes the baseline. WP1 and WP2 establish the execution and input contracts. WP3 and WP4 depend on those contracts. WP5 and WP6 complete the durable execution boundary before WP7 can qualify the full entry path. WP8 follows passing qualification.

Start with WP0 and WP1: preserve the current behavior as regression fixtures, instrument the complete latency boundary, and remove the dropped-continuation/two-minute-wait failure. Then implement the ready-only configuration/calendar/market snapshots so the first operator migration has no query fallback.

## 6. Final acceptance criteria

- [x] Every decision operator has one attempt and a 2,000 ms budget including scheduling.
- [x] No operator reads a database, requests external data, populates a cache, polls quotes or retries business logic.
- [x] Producers validate once; cache owners maintain readiness; operators do not repeat content validation.
- [x] Missing inputs and expired stages produce explicit terminal reasons and cannot authorize entries.
- [x] All three families preserve their existing strategy and exit rules.
- [x] Assignment queues preserve signal order and allow independent funds to run concurrently.
- [x] No Trade workflows create no financial reservations or orders.
- [x] Entry commitment remains atomic and prevents double spending across shared financial resources.
- [x] Persistence uncertainty and broker recovery cannot restart a decision or duplicate an entry.
- [ ] Strategy Updates retains one identity per accepted signal/assignment and accurate timing/reason evidence.
- [ ] Benchmark, load, disposable PostgreSQL and Rust eGui qualification evidence is recorded before full activation.
