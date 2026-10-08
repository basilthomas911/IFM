# Iron Condor Trade Plan Monitoring Implementation Plan

Status: Calculation and storage implementation in progress; live qualification pending. Updated: 2026-10-07.

Implement the [monitoring design](Iron-Condor-Trade-Plan-Monitoring-Design.md) from owned Databento leg connections through IronCondorTradePlanSnapshot calculation and TradePlanDb persistence. Follow [actor conventions](Actor-Implementation-Conventions.md) and [event modeling conventions](Actor-Event-Modeling-Conventions.md) throughout. This plan does not authorize a production deployment or a live broker test.

## Scope and completed foundation

Keep FuturesTickTradeDataChangedEvent as the position input and OptionTradeTickPriceDataUpdatedEvent Notify only. Use IronCondorTrade.Legs in the established trade view. IronCondorTradeView is read-only. Live Feed On activates monitoring; Off deactivates its monitoring ownership and retains the last values with inactive status. Its only monitoring listeners are option leg, Iron Condor position and trade plan notifications. Each accepted position update plots the actual backend combined spread price with its observation timestamp, updates the current marker and keeps the latest point centered. No order-execution listener or order action is part of this view.

Exit execution is a separate future design. Monitoring may calculate and display an exit recommendation, but must not start an exit order workflow. Remove the current automatic exit-workflow dispatch from this monitoring path when wiring the new handler. Preserve financial order execution durability and atomic spread fills outside this scope.

Completed: recovered the pre-removal legacy calculator from Git; added separate XML-documented pure functions for forward price, forward-loss ratio, MScore, trade-risk classification, MDI warning/limit/price thresholds, gamma risk and trailing-stop values. Twenty focused regression tests passed. These functions are now connected to immutable monitoring inputs, the full source snapshot and its single-attempt Scylla projection.

The design's unresolved formula statements are superseded for those recovered functions. Forward-loss ratio uses the absolute combined forward spread divided by the configured price limit. MScore uses square-root ratios, a 60-day historical sample plus the current observation, and the legacy lower-middle median/MAD. Implementation must retain these definitions rather than use the previously proposed monetary-loss ratio. Preserve the legacy lossProbability calculation/version unless a separately named model is deliberately introduced; do not silently replace its meaning with scenario frequency.

## Stage 1 Verify inputs and legacy parity

Dependencies: completed calculation foundation.

- Recover the remaining latest legacy plan initializer, rule engine, MDI map, limit configuration and distribution dependencies from Git. Record commit IDs and exact formulas in the design.
- Build a 48-column mapping with the exact provider, units, calculation version, freshness and required/optional status for each field.
- Resolve opening cash flow signs, multiplier/quantity scaling, gross versus net PnL, loss-limit signs, probability versus MAD score, and median sample inclusion.
- Load MScore history once per session from persisted ratios, keep a bounded live baseline, and define deduplication/expiry without per-tick queries. Capture baseline identity for reproducibility.
- Verify long-condor and unequal-wing behavior against reference fixtures; do not assume short-condor gamma classifications apply to long strategies.

Acceptance: every legacy field has an identified source/calculation; parity fixtures cover formulas and rule precedence; missing inputs produce explicit unavailable/degraded values. No invented defaults.

## Stage 2 Define the snapshot and immutable inputs

Dependencies: Stage 1.

- Introduce IronCondorTradePlanInputs and IronCondorTradePlanSnapshot in the owning mirrored Shared hierarchy.
- Include all 48 legacy business fields, full position identity, exchange ValueDate, revision, source event, route generation, captured leg/market observations, thresholds, versions, completeness and hash.
- Give new contracts explicit permanent MessagePack keys. Keep existing generic contracts readable and provide explicit adapters for activity and exit consumers.
- Capture underlying EOD hot-cache inputs, option quotes/Greeks, security metadata, risk settings and seeded analytics once per evaluation. Queries continue reading persisted Scylla projections.
- Restore the revision from only the last full source snapshot when loading a plan stream; retain no resident plan history. Any authoritative state mutation requires a concrete command and event application; function compute models remain pure.

Acceptance: serialization round trips; identity and calendar guards; deterministic fingerprints; no command-state read store shortcut; old contracts still deserialize.

## Stage 3 Implement per-leg feed ownership

Dependencies: Stage 2 identity contracts; may proceed independently of Stage 4 calculations.

- Implement a separately owned Databento transport connection for each distinct leg contract using the supported worker lifecycle.
- Validate symbol/instrument mapping and subscription entitlement. Verify actual available connection limits before declaring this topology supported.
- Track owners, partial acquisition rollback, observed readiness, stop/release and reconnect generations. Multiple trades owning the same contract must not stop one another.
- Wire IronCondorTradeView Live Feed to the new model's four legs, remove its legacy-model dependency for this path, and expose per-leg status.
- Maintain backend monitoring ownership independently when configured; closing the view releases only view ownership.
- Keep raw trade routing, source sequence/generation checks, tick insertion and Notify-only option updates distinct and correctly mapped.

Acceptance: four observed active subscriptions; exact contract records reach the expected position; duplicate/out-of-order and retired-generation ticks are rejected; rollback/release tests pass. Unsupported worker capabilities or provider connection limits are concrete blockers to this stage.

## Stage 4 Implement the complete calculation pipeline

Dependencies: Stages 1 and 2; captured records allow testing before Stage 3 is live.

- Adapt valuation and expiry payoff calculations to established trades and current signed remaining quantities, including commissions and realized PnL.
- Adapt existing spread distribution jobs and forward-pricing providers to the new input model. Use four qualified OptionCalculator price calls outside the tick handler; record the exact inputs, engine and numerical-policy versions. The existing forward provider supplies one deterministic observation, not Monte Carlo.
- Call the completed legacy initializer functions for forward loss, MScore, risk, MDI and trailing-stop calculations.
- Populate underlying statistics, daily moving averages, five-minute RSI/TDI and market/VIX classifications from their documented sources.
- Preserve configured targets, loss warnings, gamma guards, trailing stops, MDI warning progression and action precedence from the recovered rule engine.
- Trigger evaluation on leg, underlying, analytics, distribution and risk-setting changes, plus bounded freshness checks.
- Guard invalid/incomplete calculations before accepting a snapshot. Record unavailable reasons and degraded monitoring state.

Acceptance: reference parity and all 48 mapped outputs; short/long, quantity, expiry, stale inputs and invalid denominators tested; no database/network/simulation in pure Compute; no silent fallback to normal risk.

## Stage 5 Connect actors and committed plan events

Dependencies: Stages 2 and 4.

- Preserve SendTradeLegUpdateAsync -> ChangeTradeLegDataCommand -> position state event flow.
- Update IronCondorPositionChanged and UpdateIronCondorTradePlan to capture inputs and produce IronCondorTradePlanSnapshot.
- Route any authoritative plan acceptance/trailing-stop changes through the owning command, with Compute, failure guard switch arms, one default state.Update expression and standard ServiceResult return.
- Events carry domain-named payloads and originating CommandId. Commit the exact decision inputs with the accepted event.
- Serialize revision allocation; persist the latest generated plan once, without replay or recalculation of failed plans.
- Adapt IronCondorTradePlanUpdatedEvent consumers and the existing exit workflow to the new snapshot; distinguish evaluation, exit acceptance and execution completion.

Acceptance: actor maps and command acknowledgement/source commit tests; loading restores only the latest source snapshot; duplicate current requests do not retry dropped projections; source and Scylla history may differ; full event correlation is preserved.

## Stage 6 Implement TradePlanDb schema and projections

Dependencies: Stages 2 and 5; prepare migration inventory earlier.

- Replace the TradeDb connection alias with an independently configured TradePlanDbConnection and keyspace initialization.
- Preserve all legacy column names/types in iron_condor_trade_plan and add provenance/snapshot fields. Use full position/session partition identity and descending revision sequence per the design.
- Inventory existing history, prepare a versioned migration/staging strategy and verify readback before canonical table cutover. Do not attempt a primary-key migration with CREATE TABLE IF NOT EXISTS or erase history.
- Write scalar fields and the immutable snapshot payload. Append every accepted revision; use MaterialChange only for alert/notification filtering.
- Skip duplicate revisions; log persistence exceptions and drop failed plans without retries, replay or secondary-write repair.
- Update current/history queries and adapters to read persisted IronCondorTradePlanSnapshot results from TradePlanDb.

Acceptance: real Scylla tests verify resolved keyspace, every column/type, append ordering, latest/history results, duplicate writes, conflicts and migration counts. A migration requiring destructive changes must stop for a concrete reviewed cutover.

## Stage 7 Wire monitoring UI and operational behavior

Dependencies: Stages 3, 5 and 6.

- Display latest plan, all available legacy risk/market values, action/reason, per-leg freshness and historical revisions in IronCondorTradeView.
- Deliver calculated updates promptly and show persistence-pending or degraded status explicitly. Emit persistence completion only after successful writes.
- Never replace a newer live revision with an older query result. Missing values show unavailable reasons.
- Preserve immediate disposable mailbox generation replacement and fence detached handlers. Restore routes and observations with their original timestamps.
- Add bounded structured logging and metrics for source-to-position, compute, commit and projection latency; mailbox age; data freshness; subscription faults; storage lag; and dropped plan writes.

Acceptance: UI on/off, four-leg updates, plans/history and recovery validated with FlaUI; transient projection failures do not stop calculated monitoring; stale inputs remain visible; exit workflow changes are visible through normal events.

## Stage 8 Complete integration qualification

Dependencies: all preceding stages.

- Rebuild affected projects and the full solution; run targeted suites and relevant architecture gates.
- Replay captured real option trades with dataset, contract and source timestamps recorded, through aggregation, actor commands, calculation, committed event, real Scylla projection and query readback.
- Test session rollover, application restart, reconnect/reset, storage outage/drop, distribution refresh and analytics-only changes.
- Use the emulator to verify atomic spread partial batches, update, cancel and close workflows and the resulting plan state. No live broker orders.
- Run a representative load test and report measured p50/p95/p99 latency, mailbox backlog and persistence lag. Set acceptance budgets from the measured baseline and trading requirement before sign-off; do not claim unmeasured guarantees.
- Publish evidence paths, test counts, observed gaps and migration state.

Acceptance: every required field has a valid provider; reproducible snapshots persist in the correct keyspace; all lifecycle/fault tests pass; monitoring remains responsive within agreed measured budgets. Any blocked gate is reported specifically rather than marked complete.

## Execution order and checkpoints

Stages 1 -> 2 -> 4 -> 5 -> 6 -> 7 -> 8 form the main dependency chain. Stage 3 runs after identity/input design and must complete before live UI qualification. Implementation can proceed continuously through the stages until a concrete provider, configuration, migration or test blocker prevents progress. A completed helper library does not count as completed monitoring integration.

Reviewable checkpoints: legacy parity evidence; stable snapshot contracts; four-leg subscription evidence; complete calculator tests; committed actor flow; real database readback; FlaUI monitoring and emulator evidence; final rebuild and latency report.

## Earlier implementation checkpoints

The missing legacy limit rows are an initialization gap, not a request for invented development constants. The recovered XML-documented initializer calculates limits from spread prices, contract multiplier, quantity, available Fund cash, risk margin, opening commissions and days to expiry. They are now connected through InitializeIronCondorMonitoringCommand, its source event, State.Apply and the ordinary Scylla projector.

Every committed Iron Condor plan now reaches the projector, including nonmaterial observations. Monitoring no longer dispatches automatic exit workflows. Real Scylla storage regression tests passed (5 tests) in iron_condor_monitoring_verification.

TradePlanDbConnection is independently registered in API and integration hosts. Development history was copied without deleting source rows into trade_plan_test_db: 37 Iron Condor plans, 7 activity rows and 3 exit workflow rows; complete CSV rows including serialized payloads matched exactly. Evidence: .artifacts/monitoring-keyspace-migration.json. Development hosts select the independent keyspace on their next start. Production retains its existing keyspace explicitly pending a separate production migration. No production cutover is claimed.

The worker now supports an explicit SeparateContractConnection request flag (new MessagePack key 8). Each isolated option has its own physical session key; exact-contract release and disposal preserve other sessions. Existing chain requests retain their previous behavior. Six session-manager tests pass, including four distinct transports and full disposal. Host discovery/worker/retention/generation regression tests: 43 passed, one skipped. This verifies synthetic transport behavior, not provider entitlement or observed live subscriptions.

Retained option trade ingress now forwards FuturesTickTradeDataChangedEvent after successful source retention with the lasting generation cancellation token. Event identity is derived from immutable source identity, so retransmission preserves the position command identity. Native source timestamps and source sequence are preserved. Side/action/header flags are unavailable in the retained evidence and are explicitly represented as unavailable metadata; this route is a position price observation, never a broker fill.

Full solution build passed with zero warnings and errors: .artifacts/monitoring-solution-build.log. Domain formula/plan tests: 36 passed. Evidence: .artifacts/monitoring-plan-regressions.log, .artifacts/monitoring-worker-tests.log, .artifacts/monitoring-physical-leg-tests.log.

At that earlier checkpoint the snapshot/input integration, host/UI acquisition, scalar projection and three-listener UI were unfinished. Later sections record their implementation and the remaining live qualification gates.

## Snapshot and scalar projection implementation ? 2026-10-07

Added `IronCondorTradePlanInputs` and `IronCondorTradePlanSnapshot` with explicit permanent MessagePack keys and all 48 legacy column names. Existing generic payload keys remain intact; key 19 carries the strategy-specific snapshot. The function commits this snapshot in the plan output. Captured legacy forward observations and MScore baseline use the recovered functions; unavailable distribution, limits, analytics and Greeks remain null with explicit reasons. No unavailable value is treated as a normal risk classification. This is partial input integration, not complete monitoring qualification.

Command fingerprints include captured legacy inputs; content hashes include every strategy-specific snapshot value. The compute copies the baseline before commit. Old nineteen-member generic payloads still deserialize. Monitoring/formula/algorithm tests: 37 passed; build zero warnings/errors. Evidence: `.artifacts/monitoring-snapshot-tests.log`.

Added individual non-destructive ALTER migrations for the 45 non-key legacy business columns and six provenance/payload columns. The original full identity/revision primary key is preserved; sequenceId is the legacy alias of planRevision, not a primary-key migration. Scalar projection follows the immutable plan write; Duplicate same-hash rows are skipped. Failed writes are logged and dropped without replay or repair. Old snapshots remain readable and do not fabricate scalar values. Real Scylla and storage architecture tests: 13 passed in `iron_condor_monitoring_verification`, including scalar and snapshot readback. Evidence: `.artifacts/monitoring-scalar-storage-tests.log`.

Those input, ownership and UI items were open at this earlier checkpoint. Subsequent sections record implementation; live provider, usable MScore baseline, recovery and UI qualification still require evidence.


## Continuation evidence and qualification blockers ? 2026-10-07

Implemented the host's asynchronous persisted-input cache, matching established trade/expiry/strategy identity, current hot EOD input capture, and a preceding sixty-day baseline load. Storage work runs outside the tick handler. Recovered short/long legacy rule priority, strict trailing-stop boundaries and MDI thresholds are individual documented computations. Current calculations retain no rolling plan or profit collection; source loading restores only the last full snapshot. Missing inputs produce an explicit unavailable monitoring result and cannot inherit a generic exit recommendation.

The established read-only UI has three owner-specific listeners. Provider acknowledgements for all four exact legs are required before Live Feed becomes enabled; startup failures release submitted legs. Cleanup attempts every release and aggregates failures. Stopping one view does not stop another owner's listeners. Current database queries use the same identity/revision guard as live notifications. The grid renders all 48 nullable specific snapshot fields, action/reasons and missing-input explanations; it does not substitute legacy numeric defaults. The graph uses backend signed spread values.

Verification: full solution rebuilt with zero warnings/errors (`.artifacts/monitoring-continuation-final-build.log`); 55 focused domain tests (`.artifacts/monitoring-final-domain-tests.log`); 13 UI service/view model tests (`.artifacts/monitoring-continuation-final-ui-tests.log`); 13 real Scylla/storage convention tests (`.artifacts/monitoring-continuation-storage-tests.log`). Scylla tests used the disposable `iron_condor_monitoring_verification` keyspace. These tests do not constitute observed live four-leg subscriptions or FlaUI qualification.

Actual development database inspection found one legacy limit row (trade 2001, LongIronCondor) with positive MaxLoss, three forward-loss observations all dated 2025-03-15, eight old Open spread-distribution rows, no current-session IntraDay distribution pair, and zero forward-loss classification rows. The recovered model requires negative MaxLoss, matching current distributions and a usable prior baseline. These are concrete data qualification blockers; old rows must not be relabelled as fresh inputs or invented to pass a gate.

The plan remains incomplete. At this checkpoint Fund cash/limit initialization, distribution generation and input-only triggers were still implementation tasks; the OptionCalculator section below records their completion. Remaining qualification includes current Greeks/probabilities/daily inputs, bounded persisted history display, rollover/reconnect and measured live/FlaUI/load evidence. The generic compatibility valuation still has a separate extrapolated forward price; it must not be presented as the recovered distribution calculation. Plan history failures remain single-attempt logged drops with no replay or repair. No extra plan-history queue is introduced; failed plan projection remains a logged drop.

## Source snapshot and projection correction - 2026-10-07

Source snapshot persistence remains required. The function restores only the latest `IronCondorTradePlanUpdatedEvent` from PostgreSQL (a typed last-N query with N=1), commits each new full snapshot, and then submits one Scylla projection attempt. It keeps no resident plan dictionary, loads no Scylla baseline plan and does not replay historical source events. The committed stream version is used for source append; it is independent of the global event ID.

The one-attempt projection consumes the already committed payload without a source readback or validity check. Failed target writes or failed admission are logged and dropped. Source snapshots and Scylla history do not need to match; loading the latest source snapshot does not repair or resubmit dropped writes. Readers continue using the latest successfully stored Scylla snapshot.
Verification for this correction: 62 focused domain tests passed, followed by a final seven-test snapshot regression run; the real PostgreSQL latest-source-snapshot test passed; and six real Scylla storage tests passed in the disposable `iron_condor_monitoring_verification` keyspace. The first PostgreSQL fixture run could not start its unrelated Scylla container; rerunning with the fixture's standard CQL setup passed. The full solution build reported zero warnings and errors. Evidence: `.artifacts/monitoring-latest-source-tests.log`, `.artifacts/monitoring-latest-source-final-tests.log`, `.artifacts/monitoring-latest-source-postgres-final-tests.log`, `.artifacts/monitoring-latest-source-scylla-tests.log`, and `.artifacts/monitoring-latest-source-build.log`. These results verify this source/projection correction; the separate live-provider and FlaUI monitoring gates are not claimed complete.

## Approved current-input definitions - 2026-10-07

`FiveDayXMA` uses completed daily futures closes ordered oldest to newest. Seed the EMA with the arithmetic mean of the first five closes; subsequent updates use `EMA = close/3 + 2*previousEMA/3`. The open exchange session is excluded. The background reader loads at most 60 closes from the preceding 120 calendar days once per underlying contract/session; insufficient data remains unavailable and can be refreshed after provider initialization. This caches a market input, not prior trade plans.

`ForwardDelta` is `sum(sign(signedLegQuantity) * qualifiedOptionDelta)` for the four matched option contracts, per strategy unit. All four absolute quantities must be equal and nonzero. Increasing strategy quantity from one to ten does not scale this value. The reader uses the four current qualified scopes and clears the value when their risk evidence expires. No trade-plan history is read by either calculation.

Verification: all 69 focused domain tests passed, including the approved formula examples, missing/invalid input guards, quantity invariance, reversed provider scope order, completed-session filtering and daily-input query reuse. Evidence: `.artifacts/monitoring-inputs-final-tests.log`. The current database still lacks a preceding sixty-day MScore sample and matching current-session spread distributions; those observations remain explicitly unavailable. These data gaps do not impose replay, store reconciliation or source-event readback on snapshot persistence.

Final solution rebuild after the approved input changes passed with zero warnings and errors: `.artifacts/monitoring-source-and-inputs-build.log`.


## Current OptionCalculator integration - 2026-10-07

Implemented the four-leg `IronCondorOptionCalculator` adapter. Every option uses the exact qualified contract exercise/premium conventions, midpoint of its underlying bid/ask, current IV, expiry year fraction and converted rate. It calls the existing calculator price-only API once per leg and persists all inputs, outputs and engine versions. The signed strategy price is the signed four-leg sum; legacy `netPrice` is the sum of the put/call spread magnitudes. Quantity scaling does not alter the unit price. Actual observed spread prices continue to drive the read-only graph.

Reference refresh and calculator refresh run separately from tick processing. Current pricing is published before any forward observation history request. A single pending request bounds persistence concurrency and does not block newer prices. Background input/freshness changes trigger the same plan function without waiting for a leg trade; retired-generation and superseded observations cannot replace a newer source snapshot.

The approved trailing-stop input is current net currency PnL plus the single latest source stop ratio. Limit initialization captures actual ledger cash, accepted order capital and established executions, then changes state through its created event. MaxLoss persistence and missing-limit query handling were corrected and verified in a disposable Scylla keyspace.

Verification before the final identity/supersession checks: 73 focused domain tests and 9 real Scylla tests passed. Full Debug solution rebuild: zero warnings/errors (`.artifacts/monitoring-option-calculator-solution-build.log`). Storage evidence: `.artifacts/monitoring-calculator-storage-tests.log`. Final calculator/supersession evidence is recorded in `.artifacts/monitoring-option-calculator-tests.log`.

Open qualification gates: genuine preceding 60-day MScore baseline; observed four-leg provider subscriptions and qualified current reference/risk data; FlaUI read-only monitoring; session rollover/reconnect and accepted-stop carry policy; measured end-to-end/load latency. Old 2025 rows are not fresh monitoring evidence. These gates remain open and the whole monitoring plan is not marked complete.


Final verification after common-underlying/expiry guards and source supersession tests: 77 focused Iron Condor tests passed. The wider Trade suite passed all 1,449 tests after correcting the old fixture's PutCall value (put = 2). UI service/view-model/architecture checks passed 36 tests; real Scylla checks passed 9 tests. Final full solution build passed with zero warnings and errors (`.artifacts/monitoring-option-calculator-final-build.log`). Logs: `.artifacts/monitoring-domain-regressions.log`, `.artifacts/monitoring-calculator-ui-tests.log`, `.artifacts/monitoring-calculator-storage-tests.log`. No live-provider, FlaUI or load qualification is implied by these counts.

Removed the legacy stop-history query from monitoring reference capture. The only accepted stop source is the single latest persisted source snapshot loaded for the function request; a regression test proves that a hung legacy history lookup cannot hold input capture.


## Observed live provider and FlaUI qualification - 2026-10-07

The requested Live Feed -> four Databento legs -> current position -> committed source plan -> TradePlanDb -> original IronCondorTradeView integration gate passed with real development data. This supersedes the earlier unobserved-provider and FlaUI checkpoints, but does not qualify every legacy risk field or production performance.

Test: [IronCondorLiveMonitoringFlaUiTests](../../TomasAI.IFM.UI.Net.SystemTests/Layout/IronCondorLiveMonitoringFlaUiTests.cs). It attaches to the API/UI launched by the development Server Manager used in the VS Code compound, loads an existing Open trade through the Trades list if the view is not already open, registers evidence listeners before enabling Live Feed, and never places or closes an order.

### Verified run

- Run directory: `.artifacts/iron-condor-live-monitoring/20261007-live-20/`; test log: `.artifacts/monitoring-live-test-20.log`.
- Trade `101.701.1701.1101`; exact contracts `ES20261120C8450`, `ES20261120C8400`, `ES20261120P7700`, `ES20261120P7650`.
- Four independent provider subscription acknowledgements between 15:52:24.081 and 15:52:25.051 UTC; genuine two-sided quote notifications for all four contracts.
- First qualified four-leg OptionCalculator source plan: global event ID `395149`, received at 15:52:26.609 UTC. Qualified calculation continued after the original 60-second lease (71.6 seconds observed), proving owner lease renewal.
- Current Scylla snapshot in the independently configured `trade_plan_test_db`: revision `10390`, source event ID `395819`. Its position age at calculation was `0.183` seconds, within the captured 30-second maximum.
- FlaUI matched visible revision `10393` and its calculated net spread `13.7749999882571` to a captured priced source event. It verified all 48 legacy column headers, four live bid/ask pairs, and the graph/central observed signed spread against current position events.
- `live-plan.png` was inspected. `visible-plan.txt`, `events.ndjson` and `timeline.json` record screen identity, stage timestamps, source IDs, calculator inputs, unavailable fields and cleanup.
- Live Feed OFF received all four matching owner release acknowledgements at 15:53:45.819 UTC. API and UI remained running with monitoring OFF.

The PostgreSQL source payload/hash readback and its comparison with Scylla/UI are integration-test assertions only. Runtime monitoring performs no source/target reconciliation, plan replay or repair.

### Issues exposed and corrected

1. Old exact-contract reference tables lacked the current definitions. Exact lookup now uses the published expiry-calendar definition fallback when the primary table is empty.
2. Native source timestamps were ahead of the host clock. Local receipt timestamps now use the actual receiver clock; the worker chooses valuation time after freezing observations. A 2,000 ms source-clock lead allowance is configured only for Development; local freshness and skew guards remain active.
3. Owned individual subscriptions expired after 60 seconds. Explicitly owned monitoring leases now renew with bounded requests; release removes their ownership and renewal registration.
4. Size/depth-only quote records repeatedly generated identical position marks. Changed bid/ask prices always publish; continuing unchanged quotes publish at most once per second to preserve freshness. Raw provider quote capture and actual trade retention remain independent.
5. Slow/replayed position history projections delayed current notifications and generated obsolete plans. After the position source commit, resident market marks now publish directly to the current UI and plan routes. History projection is independent and does not republish old market marks. Financial opening, closing and correction boundaries keep their established persistence/projection lifecycle.
6. Market-mark source events had empty UUIDs before projection. Their command factories now assign a deterministic source UUID from the originating CommandId and event type before State.Update and source persistence. Retry identity is stable.
7. Full live grid traversal was an unreliable and expensive UI Automation assertion. The test reads bounded recent identity/price cells, compares them with captured source events after the read, and records the matching row rather than traversing 200 x 48 moving cells.

### Regression evidence

| Gate | Result | Evidence |
| --- | --- | --- |
| Real Databento/API/Scylla/FlaUI monitoring test | 1 passed | `.artifacts/monitoring-live-test-20.log` |
| Full Trade unit suite including live-publication/source-identity regressions | 1,462 passed | `.artifacts/monitoring-final-trade-suite-identity.log` |
| Focused MarketData observation, qualification and renewal tests | 72 passed, 1 skipped | `.artifacts/monitoring-final-live-marketdata-tests.log` |
| Focused Iron Condor final domain tests | 90 passed | `.artifacts/monitoring-final-live-domain-tests.log` |
| Full solution build after publication separation | 0 warnings, 0 errors | `.artifacts/monitoring-live-final-solution-build.log` |
| Final API build including deterministic source UUIDs | 0 warnings, 0 errors | `.artifacts/monitoring-api-build-18.log` |
| Final FlaUI test build | 0 warnings, 0 errors | `.artifacts/monitoring-test-build-20.log` |

### Reproduction

Start `IFM: API + UI (Development)` from VS Code. The real-data test is intentionally skipped unless explicitly enabled. Discover the running UI PID with `scripts/Development/Get-IFMDevelopmentProcess.ps1`, then run from the repository root:

```powershell
$env:IFM_RUN_IRON_CONDOR_LIVE_MONITORING = '1'
$env:IFM_LIVE_MONITORING_UI_PID = '<running UI PID>'
$env:IFM_LIVE_MONITORING_TRADE = '101.701.1701.1101'
$env:IFM_LIVE_MONITORING_SETUP_TRADE = '101'
$env:IFM_LIVE_MONITORING_EVIDENCE = 'C:\repos\IFM\.artifacts\iron-condor-live-monitoring\<new run>'
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-build --no-restore --filter FullyQualifiedName~IronCondorLiveMonitoringFlaUiTests --logger 'console;verbosity=detailed'
```

The selected trade must exist, be Open, have four exact legs, and have Live Feed OFF initially. The test uses current Databento entitlement, real NATS/PostgreSQL/Scylla services and an interactive Windows desktop. It reports the failing stage and always attempts OFF/release cleanup after attaching to the view.

### Remaining qualification limits

MScore and dependent legacy recommendations are blocked by genuine reference data: `trade_test_db.trade_plan_forward_loss_ratio` contains only three rows dated 2025-03-15 and no sample in the required preceding 60-day window for 2026-10-07. The observed plan therefore correctly records `IsComplete=false` and explains unavailable MScore/rules. Current four-leg calculator prices, forward values, probabilities and qualified risk values were observed in persisted snapshots; they do not justify inventing a historical MScore sample. Quotes/Greeks can also become temporarily unqualified as their freshness deadline expires; those fields stay explicitly unavailable.

The observed live transport/persistence/display gate is complete. Whole-design sign-off still requires the genuine MScore baseline, separate session-rollover/reconnect qualification and a representative latency/load budget. The single observed position age above is evidence for this run, not a p95/p99 guarantee. Exit workflow remains deferred by the read-only monitoring scope.
