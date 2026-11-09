# Application Backtesting System Implementation Plan

**Version:** 0.1  
**Date:** 2026-11-09  
**Status:** Planned; implementation not started

Implement an isolated historical execution mode using the normal trading application, a file-backed market-data emulator and the existing broker emulator. The first release proves functional correctness over historical time while retaining profitability outputs. It must preserve live trading defaults, financial rules and existing UI layout.

This plan follows the [consolidated design](Application-Backtesting-System-Draft-Design-v0.1.md), [functional specification](Application-Backtesting-System-Specification-v0.1.md) and [shared-code inventories](Application-Backtesting-Shared-Code-Inventories-2026-11-09.md). Creating this plan does not start implementation, reset storage, acquire data or restart Development.

## 1. Scope and completion criteria

Each accepted Start creates a fresh run: fence old workers, clear all prior mutable backtesting state, recreate stores, initialize the selected baseline/capital, reimport the date range and warm-up, then replay. Reusing immutable verified Databento files is allowed; reusing prior aggregate snapshots, imported working data or financial state is not. Duplicate delivery of the same Start operation must not reset twice.

Per-run Input includes dates, capital/currency/Fund allocation, immutable configuration baseline, strategy horizons, historical source manifest, execution profile, commissions/fees/slippage, simulated response latencies and acquisition authorization. Show resolved effective values without credentials. No numeric capital/cost/latency defaults are assumed approved.

Output retains decisions, errors, trades/fills, settlement, performance/data evidence and daily/monthly/whole-run results split Long/Short, including points, gains/losses, commissions, realized/unrealized P&L and drawdown. A profitable result is not a release criterion. Unexplained errors, lost required inputs, unknown financial outcomes, duplicates, cross-run leakage and unreconciled accounting are failures. Valid NoTrade decisions remain valid.

V1 excludes research optimization, a previous-run history browser, whole-run resume, live/Paper brokerage, Redis/Blackboard reintroduction and Aspire migration. Stop preserves the current partial result; normal range end marks open positions without forced liquidation.

## 2. Architecture and ownership

| Component | Implementation boundary |
| --- | --- |
| Backtesting controller | Separate long-lived control process owns current Start receipt, worker generation, validation/reset/import lifecycle and Stop. Its minimal current receipt survives mutable-store reset; no prior trading data/results survive there. |
| Run workers | Fresh API/domain, Rust replay-host and broker-emulator instances per accepted run. Do not rewind static state in retained workers. UI/control processes may remain alive and reconnect to the new generation. |
| Run lifecycle state | Use current command/function actor conventions: producer preparation, concrete lifecycle commands/events, durable post-commit projections and query handlers reading projected models. Keep operational lifecycle ownership under SystemAdmin; keep financial ownership in existing aggregates. A full-state lifecycle snapshot must recreate current state and tail replay. |
| Historical provider | Dedicated file-backed `IMarketDataApi`, using the existing Rust decoder where compatible. Implement the full required contract and coherent read mirror; never select a live provider as fallback. |
| Time authority | One run clock controls .NET TimeProvider, native controlled UTC, due business timers and simulated broker responses. Physical I/O, worker leases, stop deadlines and stall watchdogs remain independent. |
| Completion coordinator | Track causal work, durable commits, projections/publications and scheduled future work before advancing logical time. Native readiness or empty queues alone do not prove completion. |
| Trading and ledger | Reuse indicators, ITI, workflows, orders, Portfolio, positions, Fund and financial event sourcing. Do not create a second authoritative backtesting ledger. |
| UI | Add Backtesting after System; selection adds/focuses one tab in the existing center-pane host. Input above resizable Output. Preserve every other pane, tab, splitter and graph. |

Proposed new application projects are `TomasAI.IFM.Application.Contracts.Backtesting`, `TomasAI.IFM.Application.Backtesting` and `TomasAI.IFM.Application.Backtesting.Host`, with UnitTests, BDDTests and IntegrationTests projects following existing repository conventions. Confirm dependencies before adding them; avoid a dependency cycle or duplicate domain abstractions. Native file-replay support stays within a separate Rust backend module/composition, reusing the existing decoder and explicit clock APIs.

## 3. Delivery stages and gates

All stages below are pending. Stages establish dependencies and evidence, not permission to stop after routine milestones once implementation is requested. A gate failure requires diagnosis; only an actual blocker should halt authorized implementation.

| Stage | Deliverable | Depends on | Required exit evidence |
| --- | --- | --- | --- |
| P0 | Baselines, executable test fixture and per-site inventory disposition | None | Existing suite status recorded; available real data covers the proposed proof; clock/message candidates classified |
| P1 | Isolated launcher, current run receipt and complete fresh reset | P0 | Wrong-target cleanup denied; Start twice and interrupted reset verified |
| P2 | Shared clocks and durable message-origin foundation | P0, P1 test environment | Old/new wire and persistence fixtures pass; live defaults unchanged |
| P3 | File-fed API and deterministic scheduler foundation | P1, P2 | Required proof capabilities work without network fallback or look-ahead |
| P4 | Small end-to-end feasibility proof | P1–P3 proof slice | Real historical signal/workflow, broker lifecycle/settlement and second fresh Start pass |
| P5 | Complete clock/message migration and all required API capabilities | P4 | Every inventoried candidate disposed; reachable paths qualified; all 76 API members have explicit behavior |
| P6 | All indicators, horizons, sessions and operational health | P5 | Warm-up/freshness, rollover, five-minute checks and actual workflow coverage pass |
| P7 | Broker edge cases, financial output and terminal lifecycle | P5, P6 | Order states, fills, positions and Fund reconcile; Stop/EOF deterministic |
| P8 | Backtesting center-pane UI and VS Code profile | Contracts from P1/P2; final integration after P7 | Inputs/Output, Start/Stop/reconnect and unchanged surrounding layout verified |
| P9 | Full qualification, measurements, live regression and final report | P1–P8 | Required suites, repeatability, isolation, Development smoke and evidence report pass |

P0–P3 build only the minimum shared slice needed for P4. Broader mechanical migration waits for that proof. UI scaffolding may proceed after stable contracts, but must not be presented as a functioning backtester before the backend is qualified.

### P0 Establish baselines and resolve source scope

1. Record repository revision, dirty changes, current Development startup settings, active services and storage identities. Capture baseline suite outcomes and timings before edits; do not attribute existing failures to the migration without evidence.
2. Turn the inventory CSVs into a disposition register: source/member, active consumer, business/physical/source time, required change or retained behavior, tests and completion status. Resolve syntax/name-only ambiguities and client/test examples. The 2,933 C# candidate sites are not a blanket replacement count.
3. Review all 158 computed origins and 60 other direct message timestamp properties, their actual consumers and inherited/transport defaults. Classify legacy UI/provider paths explicitly; they must either remain outside the Backtesting dependency graph or be migrated if reached.
4. Select the smallest available real Databento fixture covering ES, required VX front/back, required options, definitions and reference publications. Calculate warm-up from the full indicator dependency graph. Identify a qualifying trigger for each intended path; absence of a trigger is not test coverage.
5. Validate schema/data rights and coverage locally. Missing paid history is a visible acquisition dependency, not authority to purchase. Keep approval separate from the configured budget.

### P1 Isolate startup and guarantee a fresh run

1. Create the explicit Backtesting environment and VS Code preparation/start/stop tasks with full source-level debugging. Keep working/source artifacts on E: and current Docker data placement unchanged. Development and Backtesting run one at a time; a conflicting process produces an actionable refusal, not an automatic kill.
2. Validate resolved PostgreSQL database identities, Scylla tick-only keyspaces, restricted principals, NATS account/scope/streams/durables, ports, cache roots and process ownership before enabling any destructive operation. No connection setting may fall back to Development.
3. Implement a single-owner Start receipt outside reset-owned stores. Include operation ID, RunId, generation, target identities and lifecycle stage. Persist acceptance before destructive work; duplicate Start returns the same attempt. Validate settings and acquisition authorization before accepting a destructive Start.
4. Fence old producers/consumers, broker/replay/scheduler workers and in-flight messages. Require quiescence before clearing run-owned PostgreSQL/Scylla data, NATS state, sequence allocation, outboxes, leases, emulator journal, caches, offsets, timers and mutable files. Revoke old-generation access. Reject late old-generation mutation.
5. Recreate/migrate stores, verify emptiness, initialize immutable baseline and configurable capital using owning commands/audited bootstrap, and import history again. Canonical immutable archive/config files remain intact. Retain only the current reset receipt across teardown.
6. On interrupted reset, continue the same cleanup attempt idempotently or fail with its precise stage. Never resume a prior trading run or use partially initialized data. Verify previous query endpoints and UI caches cannot expose prior results.

### P2 Introduce compatible shared time and origin seams

**Clocks.** Change API `RegisterActorGenericTypes.cs` and the Backtesting Host composition so an explicit run TimeProvider reaches SimpleInjector, Microsoft DI and all worker factories. Keep live System bindings and `NativeBackendServices` live-only guard. Introduce a separately named physical-time dependency in mixed services; do not inject a virtual provider into socket/database watchdogs by accident.

Reuse `FuturesValueDateProvider` as a fresh worker-local singleton, refreshed on every read. Remove static `.System` fallback from the qualified replay path. Preserve calendars, holiday/DST behavior and ValueDate advancement as the last successful Market Close step.

Reuse native `ConfigureLive`/`UpdateLiveClock` in controlled mode where these APIs do not start source sockets. Coordinate explicit UTC units/domain/generation and acknowledgements. Live advancing mode remains unchanged; replay source selection is separate from the clock configuration method name.

Split business expiry from physical waits in `QualifiedCompositionDiscovery`, managed native pricing/scope services, function-stage execution policy and Trade Selection. Logical waits register future due work and yield; physical cancellation/stall bounds remain effective while simulation time is frozen. Keep Rust `availability.rs` and transport/recovery timers monotonic: its 30-second required-output diagnostic is not quote freshness.

**Origin contract.** Add immutable, versioned origin metadata with `OriginatedAtUtc`, `ClockDomainId`, optional RunId, generation and causation identity, retaining existing message IDs. Capture once at creation from the declared clock. Pre-run administrative messages use the control-plane clock; trading messages use the run clock. New derived messages/replies capture their own origin and preserve causation; retries preserve the original origin.

**Transport/context.** Extend `IActorMessage`, `ActorMessageInfo`, contexts and receive paths, `NatsActorProducer`, `NatsEventProducer`, owned/legacy/pooled/shared message paths and replies. Use additive headers independent of tracing, preserving existing body shapes and numeric keys. Include market-data RPCs, native publication frames, retained snapshots, recovery/evidence queues and UI bridge paths that bypass actor producers. A disabled Activity must not remove origin metadata.

**Durability.** Extend event/command audit and projector outbox write/read models, schema, serializer/dispatcher and both event appenders with per-message origin metadata. Stage it atomically before first persistence; restore it rather than restamping on delivery. Keep physical CreatedAt/lease/retry timestamps distinct from logical business EventTimestamp. Test all production append modes and latest-snapshot-plus-tail reads.

**Compatibility.** Add nullable storage metadata first, then readers and writers; preserve old payload decoding. Mark absent old origin explicitly legacy/unknown or derive from a documented stored fact with provenance. Do not use today's date to fill historical unknown origin. New replay messages require complete domain/generation metadata. Existing serialized OriginatedOn/ReceivedOn fields keep their meaning and keys; preserve supplied facts. No global base-class key insertion or historical payload rewriting.

For the P4 slice, migrate actual consumers of computed OriginatedOn to the immutable context/captured value. Do not leave business readers using a dynamic getter while declaring envelope migration complete. Capture missing ReceivedOn once at the proper creation boundary; classify ErrorDate as diagnostic or business before changing it. Keep semantic request hashes, deterministic projector identities and retry keys unchanged.

### P3 Implement historical API and causal replay

1. Implement manifest parsing/checksums, schema/instrument indexes and bounded DBN file readers. Resolve historical definitions, listings, multipliers, roll rules, Treasury and economic publication availability. Merge futures, VX and option sources deterministically by availability/source order with documented tie-breaks.
2. Provide the P4 subset of `IMarketDataApi`: prepare/start/stop, subscriptions, reference/contract discovery, seeds, quotes/readers/mirror, composition and health as reached by the chosen real fixture. Add explicit Replay recovery ownership and update all recovery/health consumers; never impersonate a live Process/Tokio recovery owner.
3. Implement event-driven replay scheduling for source records, bar/session boundaries, due broker responses, health checks and logical timeouts. Retain original event/receive timestamps, even when event time precedes delivery time. No extra timestamp admission window may suppress a valid ITI signal.
4. Track causally admitted work across actors, persistence, projections/outboxes, downstream workflow commands and broker effects. Register children before acknowledging parents. Durable retry queues remain unfinished work until delivered or terminally failed; retries reuse IDs and origin. Keep accounting bounded rather than retaining every historical message.
5. Advance only when current due work is complete or explicitly yielded on a future timer, then advance to the next eligible input/timer. Update .NET/native clocks coherently before releasing work. A future wait cannot hold the barrier that must advance time to satisfy it.
6. Add real progress-stall detection and responsive Stop. No record dropping, mailbox-depth-only completion, freshness weakening or synthetic broker outcomes to force advancement.

### P4 Prove the architecture before broad conversion

Run a small real historical interval plus genuine warm-up through isolated PostgreSQL, Scylla and NATS. Demonstrate bars, indicators, an actual ITI-driven workflow and expected stage evidence; exercise a broker-emulator open/close through the normal order/Portfolio/Fund paths using executable historical quotes. If a real strategy validly selects NoTrade, retain that decision and choose an additional suitable fixture for the unexercised order path; separately labeled fault/control scenarios may test mechanics without claiming a historical trading signal occurred.

Start the same interval again and prove full reset, reimport, no retained singleton state, no old query/history visibility and equivalent normalized results. Test a frozen-time stall and cancellation. Run shared live-default/wire regression tests.

If this proof needs weakened trading rules, an unbounded completion mechanism or major actor-runtime replacement, stop expansion and revise the architecture. Missing required historical inputs are a visible blocker; do not manufacture quotes or purchase data silently.

### P5 Complete the inventoried shared migration and API surface

1. Finish all reachable clock/origin consumer changes using small reviewed groups with existing domain tests. Preserve supplied source/valuation timestamps and physical diagnostics. Every inventory entry needs a retained/migrated/excluded disposition; unclassified entries cannot be called complete.
2. Remove dynamic getter behavior from the qualified contract surface. Where an old source API must remain temporarily, provide a captured/restored value with explicit provenance, not a hidden system-clock read. Keep excluded legacy paths unable to enter replay.
3. Complete behavior for all 76 API members using the capability CSV. Required paths must not fall through unsupported defaults. Historical availability/estimate/load operate during preparation; development worker-fault injection returns explicit unsupported V1. No network acquisition occurs during replay.
4. Complete saved-trade subscriptions, releases, recovery, quote/risk/composition snapshots, chains, Treasury/calendar, term structure, seed histories and coherent mirrors. Preserve last-known prices versus qualified executable OrderPrice. Quiet subscriptions can be ready without fresh quotes; EOF is not a connection outage.
5. Add a source-level regression guard for new direct business clock reads/computed origins, using an explicit reviewed allowlist for physical time, test code and opaque identifier generation. It must not force blanket removal of correct Stopwatch/Instant/timer usage.

### P6 Qualify indicators and strategy horizons

Size warm-up for all active indicators and their dependencies, including Daily EMA20/50/200 and required 15-second, one-minute, 5/15-minute, hourly/four-hour and Daily/Weekly/Monthly views. Optional inactive metrics remain inactive. Seed via normal paths without placing warm-up orders; shared seed replay is performed once where supported and is bounded by the current replay instant.

Verify per-horizon dependencies do not block unrelated workflows. Run indicator health every five simulated minutes and enforce initial qualification within the specified five-minute initialization budget, with a separate real startup/stall bound. Report indicator/timeframe, missing versus stale cause, source time and required boundary. The clock must be allowed to advance while waiting for genuinely future bars; readiness cannot require future data before trading Start.

Dispatch normal Market Open/Close business actions at historical session boundaries through the coordinator, keeping Windows scheduler/process supervision physical and single-owned. Preserve final-step EOD ValueDate advancement, rollover, early closes, weekends and DST. Verify real historical ITI triggers drive fresh Daily, Weekly and Monthly workflows; preserve legitimate input restrictions and NoTrade reasons at Regime Discovery, Market Condition and Trade Selection.

### P7 Qualify execution, financial output and lifecycle

1. Adapt `IEmulatorClock` to run time only in Backtesting. Freeze effective per-run execution costs/latency; use qualified historical executable bid/ask and V1 atomic combo rules. Do not treat indicative midpoint or stale last-known quotes as executable prices.
   Validate expiry/assignment support before admitting a range containing affected exposure. Implement the required behavior explicitly or block that range with a clear unsupported-capability reason; never carry an expired contract through replay as if it remained tradable.
2. Verify pending Place/Change Limit/Cancel, timeout, rejection, lost reply/reconcile, partial fills, cancellation of remainder and late fills. Simulate broker faults only in labeled tests. Broker accepted/fill evidence determines final state, not UI assumptions.
3. Keep Portfolio/Fund/position accounting authoritative. Reconcile quantities, actual per-fill prices, contract multipliers, fees counted once, cash/reservations and settlement. Expose any unresolved outcome as an error requiring reconciliation.
4. Build projected current-run results from ledger/decision evidence. Provide all specified Output views, preserving Long/Short and instrument/quantity/point units; mark missing range-end prices unavailable rather than zero. Do not synthesize EOD rows without a valid source/policy.
5. Implement Draining/Completed and Stopping/Stopped/Failed. Stop admission first, drain already-admitted effects within physical bounds, record unresolved work explicitly and retain partial current results. EOF marks remaining positions; it does not force liquidation or restart feeds.
6. UI reconnection never restarts work. Process failure never silently resumes the historical run: expose Failed/Stopped with durable evidence and allow a new fresh Start. Order reconciliation tests may rebuild durable state without resuming replay or resubmitting orders.

### P8 Deliver the workspace and debugger profile

Add only the Backtesting menu after System and a singleton Backtesting tab in the existing center-pane host. Preserve left/right panes and Strategy/Operations/Actors placement. Use existing typography, calendar conventions, baseline alignment and 20 px label-to-control spacing.

Expose every per-run setting in Input; show derived boundaries/warm-up/storage aliases without secrets. Center Start/Stop buttons, lock inputs while active, display the mandatory fresh-reset notice and prevent closing an active run without stopping. Do not introduce a separate modal/window for the workspace.

Output shows lifecycle/progress, simulated and physical duration, current error once with persistent inline detail, decisions, financial results, data/performance and settlement. Refresh through projected queries, load large details on demand, discard old-generation responses and reconnect to the current attempt. Switching tabs does not affect execution.

Add the API + Rust eGui + Rust MarketData Backtesting debug profile and deterministic preparation tasks, with Rust debug information level 2 and .NET source debugging. It must select replay/emulator endpoints explicitly, preserve Development launch behavior and refuse simultaneous ownership. Verify the actual window and debugger startup; report any UI verification blocker rather than substituting controller-only evidence.

## 4. Unit test coverage

Extend existing owning projects and add focused Backtesting suites; do not hide application tests inside unrelated domain projects.

| Area | Required unit tests |
| --- | --- |
| Inputs and lifecycle | Date ordering/session conversion, capital/allocation reconciliation, explicit costs/latency units, immutable baseline hash, Start idempotency, generation fencing and valid lifecycle transitions |
| Clock and dates | Frozen/advancing run clock; equivalent native/.NET UTC units; monotonic in-run advancement; fresh earlier-date worker; DST/weekend/holiday/18:00/EOD behavior; source timestamps never overwritten |
| Timers | Future timers yield to coordinator; due-now tie ordering; frozen-market Stop and real watchdog; physical polling cannot be trapped behind a logical barrier |
| Origin and serialization | Stable repeated reads; new message versus retry origin; tracing disabled; typed/pooled/fanout/reply paths; old/new JSON/MessagePack; legacy unknown provenance; unchanged keys/hashes/IDs |
| Historical provider | Real small DBN decode, deterministic merge, duplicate/gap policy, historical listings/rolls, future records hidden from seed/quote/discovery, last-known/fresh qualification, quiet/EOF/failure distinction and required/unsupported API defaults |
| Completion | Parent/child admission ordering, durable outbox pending acknowledgement, future waits, cancellation, double acknowledgement, bounded retained bookkeeping and terminal failure propagation |
| Execution/results | Place/change/cancel scheduling; partial/late fills; costs once; multipliers; Long/Short aggregation, calendar buckets, missing marks, Fund/reservation reconciliation and terminal partial results |
| UI/controller | Singleton center tab, all inputs mapped/validated, active edit lock, Start retry/reconnect, old-generation response rejection, details on demand and one dialog per incident |

Use existing NATS unit/integration, Storage integration, MarketData/Analytics/Feed/Securities unit/BDD/integration, Rust Host tests, native Rust tests, SystemAdmin lifecycle and Trade/Portfolio/broker suites for shared changes. Verify actual project naming (`IntegratedTests` versus `IntegrationTests`) before adding solution/test-run entries. Add lifecycle snapshot and projected-query convention audits to the affected SystemAdmin suites.

## 5. BDD scenarios

Implement feature files and bindings in the new Backtesting BDD project and relevant existing domain BDD projects. Bindings must exercise the application contracts, not a separate test-only trading engine.

| Scenario | Given / When / Then evidence |
| --- | --- |
| Fresh Start twice | Same manifest and settings / two deliberate Starts / both import anew, second has no first-run financial rows or history, normalized results agree |
| Duplicate Start delivery | One accepted operation / repeated request / same receipt, one reset, one worker generation |
| Interrupted reset | Reset stops after fencing or partial cleanup / controller restarts / same reset finishes safely or fails explicitly; old run never resumes |
| Unsafe target | Development database/keyspace/NATS identity supplied / Start / refusal before deletion or publication; sentinel unchanged |
| Stable origin | A logical-time command and staged effect / read, retry and restart delivery / original origins/source facts/IDs persist |
| Historical availability | File contains future quote/definition/publication / query before availability / unavailable until the appropriate replay boundary |
| Warm-up and independent horizons | Eligible prehistory / initialization and replay / indicators warm, no pre-start orders, one horizon cannot block another unnecessarily |
| Five-minute health | Required active indicator becomes stale / next due audit / one explicit error with evidence; rules are not bypassed |
| Valid NoTrade | Complete inputs but a trading restriction / normal workflow / NoTrade with correct reason and no order |
| Broker round trip | Historical executable quotes and configured fees/latency / open and close / fills, position and Fund reconcile without duplicates |
| Lost broker replies | Place/change/cancel reply missing, including partial/late fills / reconcile durable broker evidence / known correct final state, no resubmission or duplicate fill |
| Stop while time is frozen | Replay waits on a future event / Stop or real stall bound / responsive terminal/partial result with unresolved effects reported |
| Quiet options and EOF | Available subscription without a new tick / quiet interval then end of files / readiness distinct from freshness, no live replacement loop |
| Configuration and UI | Existing shell / open Backtesting / only the new center tab appears; settings and Output stay in that tab |
| No paid-data fallback | Missing history and no authorization / preparation / visible acquisition requirement, no charge/download/live substitution |

Fault scenarios must be distinguishable from real historical replay results. Do not label a synthetic test-generated ITI event as historical workflow evidence.

## 6. Integration and end to end qualification

Use real isolated PostgreSQL, ScyllaDB and NATS plus actual small DBN fixtures. Mocks are insufficient for the following acceptance checks:

1. **Storage/reset:** verify actual identities and restricted permissions; schema migrations; all reset-owned targets; interrupted reset; stale generation attempts; unchanged Development sentinels and immutable source checksums. Run the same range twice.
2. **Message durability:** persist origin through both event appenders, command audit, committed event reads, snapshots/tails, projector outbox crash/retry, native evidence/current-value replay and NATS round trips. Include multiple origins in one batch and old rows without metadata.
3. **Latest snapshots:** write creation, several state changes, a full-state snapshot and tail events for new lifecycle state; verify latest eligible snapshot plus tail equals full replay. Repeat for affected existing repositories/read models to ensure metadata additions do not change stream selection or wider-history behavior.
4. **Causal replay:** slow/delay/fail a projection or publication and prove time does not pass unfinished due work. Let future timers yield. Verify no deadlock, dropped inputs, duplicate effects, future-data leakage or unbounded bookkeeping.
5. **Historical trading:** run normal API/domain/Rust processes through warm-up and actual qualifying Daily/Weekly/Monthly ITI triggers, stage evidence and valid decisions. Verify all active indicators and five-minute health checks. Use additional verified intervals when one fixture lacks a qualifying trigger.
6. **Broker/financial:** actual emulator Place/change/cancel/reconcile, partial/late fill combinations, trade open/close, Fund settlement and final marks. Restart/rebuild financial state in the isolated environment without restarting replay or duplicating orders.
7. **UI:** run the Backtesting debugger profile, use the actual Rust eGui tab, Start/Stop/reconnect, inspect Input/Output and verify no other pane/tab moved. Check current-run filtering, calendar boundaries, large result paging and one-incident reporting.
8. **Repeatability:** compare normalized decisions, source identities, fills, prices, quantity, P&L and settlement under identical manifest/config/order policy. Exclude random operational IDs and physical timing from equality; retain their diagnostic traceability.
9. **Live regression:** System clock/live provider defaults, quiet-options readiness, quote freshness, recovery, independent timeframe startup, market-open/close ordering, broker pending/reconcile and latest-snapshot persistence remain correct. Perform the controlled Development smoke only after preserving/restoring its settings and data.

Archive machine-readable results, exact commands, code/config/data hashes, failing evidence and normalized comparison outputs. A test failure must be fixed or recorded as a concrete blocker; an unrelated failure must not be silently excluded from a claimed full pass.

## 7. Measurements before and after

Capture the same fixture, hardware, storage, build profile and configuration before/after each shared migration group. For newly introduced replay functionality, establish its first qualified baseline; do not invent a pre-existing backtester comparison.

| Measurement | Method and reported evidence |
| --- | --- |
| Shared message overhead | BenchmarkDotNet for .NET origin creation, header handling, serialization, append preparation and pooled receive paths: latency distributions, allocations, GC counts and relevant contention; native benchmarks for frame/clock operations |
| Live-equivalent regressions | Fixed representative actor/publication workload before/after: throughput, CPU, memory, backlog and tail latency; investigate regression before expanding |
| Preparation | Fresh reset, schema/bootstrap and import/warm-up times, logical records, compressed/imported bytes and repeated shared-seed reads |
| Replay | Records/second, simulated/wall duration, causal barrier/stage latency, queue pressure, outstanding effects, memory high-water mark and storage use |
| Correctness under load | Delivered versus eligible records, duplicates, repaired/declared gaps, stale indicator counts, workflow outcomes and settled quantities; faster execution never excuses missing records |
| Stop and UI | Physical Stop/cancel/drain duration while market time is frozen; UI query latency and responsiveness with large outputs |

Use full-debug builds for functional development. Run optimized measurements separately with source symbols and explicit profile labels; never compare Debug before with optimized after or silently replace the Development binaries with stripped builds. Set performance budgets from P0 evidence and document them before judging results.

## 8. Live trading safeguards and rollback

| Safeguard | Enforcement |
| --- | --- |
| Explicit mode | Separate Backtesting composition; System/live provider bindings remain defaults for Development/live. No mutable process-wide mode or clock switch. |
| No real orders or feeds | No live/Paper broker credentials or provider source sockets in replay; reject wrong capabilities and endpoints before starting workers. Historical acquisition is preparation-only with explicit spending authorization. |
| Restricted reset | Resolve/verify exact owned target identities using restricted credentials; fence workers first; never delete shared Docker volumes, Development stores or immutable archives. |
| Compatible shared deployment | Add nullable metadata/read compatibility before writing new metadata; do not rewrite existing event payloads or reindex numeric keys. Test old readers and rollback binaries against additive schema before rollout. |
| Financial invariants | Preserve trading rules, risk/data restrictions, pending/rejection/cancel/late-fill/reconcile semantics and owning ledger. No forced success, fabricated price or silent unknown outcome. |
| Controlled rollout | Complete P4 before broad migration. Keep changes independently reviewable by shared seam/domain group, with regression tests and evidence. Run one environment at a time. |
| Rollback | Stop/fence replay workers, retain current diagnostic/financial evidence, restore the verified prior application build/configuration and confirm Development health. Leave backward-compatible added metadata columns in place; do not use destructive schema rollback as a recovery shortcut. |

Rollback cannot discard an unresolved broker/financial effect or resume a historical run with different code/configuration silently. Restore prior live behavior only after reconciling any in-flight Development activity affected by deployment; Backtesting must never have touched that activity in the first place.

## 9. Blockers and final acceptance

Potential blockers are required point-in-time history missing without affordable/authorized acquisition, an unsafe storage/transport boundary, an unbounded/deadlocking causal barrier, incompatible persisted contracts or a requirement to rewrite trading/risk rules to make replay work. Diagnose the exact dependency and retain reproducible evidence. Missing ticks off-hours, valid NoTrade, a losing result or an unavailable optional inactive metric are not automatically blockers.

Complete only when P0–P9 evidence is recorded: all required unit/BDD/integration suites pass, all inventory candidates have dispositions, all 76 API members are accounted for, real-data workflows and broker/Fund paths are exercised, fresh reset/import twice is proven, and Development is restored and verified. Publish the final report with test results, actual before/after measurements, fixture limitations, any remaining explicitly accepted noncritical limitations and links to the delivered UI/profile.

## 10. Specification traceability

| Specification requirements | Delivery stages |
| --- | --- |
| ENV-01–06 | P0, P1, P8, P9 |
| UI-01–09 | P8, P9 |
| RESET-01–07 | P1, P4, P9 |
| DATA-01–06 | P0, P1, P3, P6 |
| MD-01–04 | P3, P4, P5 |
| TIME-01–04; MSG-01–07 | P2, P4, P5, P9 |
| ORDER-01–04 | P3, P4, P7, P9 |
| TRADE-01–06 and financial behavior | P4, P6, P7, P9 |
| OUT-01–05 | P7, P8, P9 |
| Lifecycle, tests and feasibility gates | P1–P9 |
