# Futures ITI In Memory Event Sourcing Implementation Plan

**Date:** 2026-11-23  
**Status:** Source/ownership blocker resolved and the development fast path activated on 2026-11-23. Disposable PostgreSQL qualification passes for all three timeframes; live source continuity, restart recovery and Rust eGui compatibility are verified. Overall `/health/ready` is still blocked by existing ES Daily indicator projections. Representative cash-open burst/tail-latency acceptance remains unqualified; the implementation must not be described as a hard real-time guarantee. The broader raw tick-storage investigation is separate.

Move Futures ITI generation onto the existing resident event-sourced actor framework. Process ordered trades against committed state held in memory, remove storage and command request/reply work from ordinary no-change ticks, and preserve durable signal history, source identity, publication recovery and restart behaviour.

This extends the Strategy Workflow Two Second Pipeline Implementation Plan. The downstream local decision runtime remains in place; this work addresses the price-to-signal path before a workflow starts.

## Measured problem

The 2026-11-23 capture of Daily workflows generated between 09:30 and approximately 10:14 ET contained 199 workflows, including 192 Market Condition stale-signal rejections. Median underlying market observation age at workflow start was 64.798 seconds. Median generated-event-to-workflow-start time was 145.162 ms, and median workflow execution time was 60.091 ms. The longest captured workflow execution was 273.483 ms.

The ES ITI realtime mailbox was observed at 2,047 of 2,048 slots with 8,718 rejected messages in its current generation. Average realtime handler duration was 31.563 ms; average Daily generation-command duration was 25.652 ms. EOD realtime traffic also occupied approximately 5,000 queued messages in another capture.

The current generator reconstructs state through PostgreSQL for each generation command and reserves a durable command audit before calculation, even when the tick produces no new signal. The realtime adapter awaits the whole command before accepting the next eligible trade.

Evidence: `.artifacts/strategy-queue-delay-20261123/evidence.json`, `workflow-timings.json`, `actors-all.json`, `diagnostics.json` and `recent-delay-log.txt`.

## Intended behaviour

| Boundary | Behaviour |
| --- | --- |
| Owner startup | Hydrate the committed ITI checkpoint, stream version and source cursor before opening intake. |
| Ordinary trade without a signal | Deduplicate and calculate in memory, in source order; zero database queries, audit writes or synchronous NATS command request/reply calls. |
| Trade producing a signal | Atomically append the event, persist required signal projections and recovery/publication evidence, and commit the relevant source identity. Publish after commit. |
| Control command | Apply hold/clear or other state mutations through the same owner; preserve their durable command audit. |
| Failed or uncertain commit | Stop that owner's admission/publication, invalidate working state, and reconcile the durable outcome before recovery. |
| Restart or handover | Rehydrate committed state, replay the authentic unprocessed source tail and resume without duplicate signals or workflows. |
| Overload or source gap | Preserve or recover the ordered source tail; expose the gap and block fresh-entry eligibility until caught up. Do not discard intermediate trades to appear current. |

Use one state owner per exact futures contract, ITI timeframe and timeframe-start value date. Preserve Daily-derived Weekly and Monthly processing and their existing boundaries. Funds consume the resulting signal stream; they do not create separate ITI calculators for the same stream.

Signal thresholds, ATR selection, event cadence and classifications, TrendVelocity, workflow gates, trading hours, order sizing and position exits remain unchanged. The performance change must reproduce the existing evaluator's output for identical ordered inputs.

## WP1 Source identity and baseline

1. Capture a reproducible stored-trade replay covering the cash-open burst, an ordinary session interval and a contract/session boundary. Include original producer identity, receive/event timestamps and all available per-trade ordering fields.
2. Define a source cursor using stream/epoch, exact dataset/publisher/instrument identity and trade ordinal or another verified unique per-trade discriminator. Provider sequence and timestamp alone are insufficient when several trades share them.
3. Trace the cursor through native publication, API ingestion, the generation input and generated signal. Add optional contract fields at new MessagePack slots; keep legacy fixtures and historical events readable.
4. Record the causal ATR and VX inputs used by evaluation. Recovery must obtain the same completed-bar/input context for a replayed trade, not substitute today's current cache value.
5. Capture baseline input throughput, no-change/change ratios, per-command database work, allocations, queue depth, oldest market-observation age, rejection counts and signal-to-workflow timing. Keep transport arrival time distinct from the original market timestamp.
6. Verify that the source journal retains every trade needed for restart and overload recovery. Identify exactly where today's optional actor admission can drop a publication relative to durable raw-trade storage.

**Completion:** A saved input manifest and baseline report can reproduce event ordering and distinguish market-to-signal latency from workflow duration. If authentic source continuity cannot be established, record it as a recovery blocker rather than infer missing prices or ordering.

## WP2 Resident ITI state

1. Adapt `FuturesItiSignalCommandActor` to `BaseInMemoryEventSourceCommandActor` with an explicit ITI residency policy. Reuse the committed-state ownership/cache machinery rather than introduce an unrelated global calculator cache.
2. Keep `FuturesItiSignalCompute` as the evaluator. Retain state by exact owner key and hydrate before live intake; use the existing repository for cold restoration and durable changes.
3. Make committed state, expected stream version and last committed source identity explicit. Keep the processed no-change cursor in memory; it may advance beyond the durable cursor until the next genuine commit/checkpoint.
4. Serialize generation and control changes through that same owner. Prevent concurrent writers across API instances or ownership handovers. Do not permit an out-of-band hold/clear writer to silently invalidate memory.
5. On failure, discard uncommitted candidate state. Evict only idle owners without pending work or unresolved commits. Handle Daily session boundaries separately from Weekly/Monthly timeframe boundaries.
6. Keep legacy generation callers without verified source metadata on the existing compatible durable path. Only the trusted ordered live feed uses the new fast admission path.

**Completion:** Warm processing retains state across inputs, loads it once per owner activation and reproduces legacy signal content. Multiple contracts cannot interfere with each other's state.

## WP3 Zero storage on no-change inputs

1. Remove pre-calculation database command reservations for eligible raw-trade generation inputs. Use their source identity and owner-local cursor for in-process duplicate handling; do not treat raw ticks as financial or administrative commands.
2. On a no-change result, perform no snapshot replay, stream-head query, event-name lookup, command-audit insert or projection query.
3. Specialize the existing framework's no-change ownership guard for the verified single-owner feed. Do not replace a database check with an unchecked always-successful override: ownership transitions must revoke admission and invalidate the owner.
4. When a signal changes, enlist any required generation-command idempotency/audit record in the same transaction as the source event and projection, with optimistic stream-version enforcement.
5. Preserve auditing and deduplication semantics for control, recovery and other command families. Do not change generic actors' auditing globally to optimize this one feed path.

**Completion:** Storage instrumentation and a database-access spy prove zero database calls after hydration for an extended no-change input sequence. A changed signal remains recoverable and idempotent after a lost acknowledgement.

## WP4 Ordered ingress and traffic isolation

1. Filter quote-only updates, non-current contracts and unrelated instruments before they occupy the ITI trade queue. Continue to prepare required VX context through its producer cache.
2. Replace synchronous command request/reply per raw trade with bounded ordered delivery to the resident owner. Use an internal local adapter or bounded ordered batches through the actor, preserving every individual trade's identity and evaluation position.
3. Process every price in a batch in source order. Batching must not average prices, retain only the last price or change ITI cadence. Do not parallelize evaluation of the same owner's prices.
4. Separate admission/capacity for the ITI trade path from general optional realtime notifications and EOD/statistics work. EOD pressure must not consume the ITI allowance.
5. Keep bounded memory and explicit source lag. If capacity is exceeded, retain a recoverable durable source range and stop fresh-entry eligibility while it is replayed by recovery. No silent drops, timestamp replacement or unlimited queue enlargement.
6. Keep no-change input handling lightweight; reduce per-tick informational logs in favour of counters and sampled diagnostics. Continue logging signal changes and failures with their source identities.

**Completion:** Cash-open replay preserves the complete input order with zero unrecovered loss, no per-trade network roundtrip and no dependence on EOD queue capacity. Different owners can proceed concurrently.

## WP5 Durable events and recovery

1. Verify whether existing analytic projector/outbox evidence covers a crash between event commit and publication. Reuse that mechanism; if it does not cover this path, enlist recoverable publication intent in the signal transaction.
2. Publish only committed signals. Recover committed-but-unpublished signals after restart and rely on stable event identity plus existing workflow queue deduplication to prevent duplicate Strategy Updates executions.
3. Restore the last committed source cursor and replay from the next authentic source record. Replaying no-change ticks after a crash is acceptable; silently skipping them without a durable cursor is not.
4. Persist source progress with signal commits. Add a bounded background checkpoint policy for long no-change periods only if replay cost requires it; it must not introduce database work per no-change input.
5. Test a crash before commit, after commit before acknowledgement, after commit before publication, and during recovery. Reconcile uncertain outcomes in recovery, not by rerunning trading decisions or retrying workflows.
6. Preserve original event, receive and processing timestamps. A recovered old signal must remain old and subject to the existing freshness policy.
7. If stored source continuity or causal indicator inputs are missing, report the exact gap and keep the relevant owner unready. Recovery may use an explicitly authorized authentic provider backfill; it must not fabricate prices or timestamps.

**Completion:** Replaying the same input, duplicates and restart windows produces the same committed signal identities/order and one workflow execution per matching assignment.

## WP6 Benchmarks and correctness qualification

Use BenchmarkDotNet for the evaluator/resident command path and disposable PostgreSQL for commit/recovery measurements. Compare the current implementation with the resident version on the same immutable inputs.

| Scenario | Evidence required |
| --- | --- |
| Warm no-change ticks | Time/input, inputs/sec, allocations, Gen0/1/2, LOH allocations and verified zero database calls. |
| Signal-changing ticks | Time to acknowledged commit, audit/projection calls, ordered publication and expected stream version. |
| Cash-open burst | Original arrival pacing plus a faster stress replay, throughput headroom, maximum lag, bounded queue/memory and no lost intermediate trades. |
| Background EOD pressure | The same ordered ITI output and isolated ITI admission under concurrent EOD/statistics load. |
| Duplicate and reordered records | Deterministic duplicate suppression; gaps/reordering detected rather than silently accepted. |
| Recovery and rollover | Correct source resumption, Daily reset, Weekly/Monthly continuation, next-contract isolation and failure invalidation. |
| Algorithm parity | Ordered event types, trend/group/band, prices, thresholds, frozen ATR provenance and TrendVelocity match the reference. Compare deterministic business content separately from processing-time metadata. |
| Workflow handoff | Stable committed signal identity creates one execution for every matching fund assignment; all observed decision stages retain their two-second budget. |
| Compatibility | Old MessagePack payloads and Rust inspection fixtures remain readable; control commands retain audit behaviour. |

Report median, p95, p99 and maximum for paced replay timings, not just averages. Distinguish in-process benchmark time, durable commit time, source backlog and signal-to-UI time.

**Qualification targets:** no-change warm processing performs zero database operations; paced cash-open input produces no unrecovered drops; sampled market-observation-to-committed-signal lag remains below two seconds in the agreed representative workload; downstream accepted decision stages remain below two seconds. These are measured acceptance targets, not a hard real-time guarantee under arbitrary failures or process pauses. If the two-second ingestion target fails, keep the work open and profile the remaining constraint.

## WP7 Diagnostics and live activation

1. Expose committed/processed source cursors, current market-event lag, hydrate count, no-change count, generated/committed/published counts, duplicate/gap counts, capacity/rejections, resident cache state and commit failures.
2. Report market-to-signal age separately from generated-event-to-workflow delay. Do not use a newly created event timestamp to make an old market observation appear fresh.
3. Treat ITI overload, a source gap or overdue input processing as degraded/unready for entry even if the feed connection and Market Outlook remain healthy. Keep the existing stale-input veto.
4. In Rust eGui, verify graph/history sequence, new signal details, workflow ordering and stale-input explanations for each exact contract/timeframe. Preserve the Daily graph event filter and show all events in Weekly/Monthly.
5. Rebuild and activate through the API + Rust eGui + Rust MarketData development profile after automated qualification. Observe genuine signals and perform a restart and session/contract-boundary qualification.
6. Validate the existing actor/cache policies with production-size owner counts. Keep a compatible fallback for unqualified callers, but do not silently fall back to per-tick database reads while claiming fast-path health.

**Completion:** Live and recorded-burst evidence establishes the input-lag and decision targets, signal completeness and restart safety. Do not mark live market-open acceptance complete based only on quiet-session benchmarks or configuration changes.

## Main implementation locations

- `TomasAI.IFM.Application.EventModelActor/BaseInMemoryEventSourceCommandActor.cs` and `ResidentActorCache.cs`: reuse committed residency and specialize the verified ITI policy without weakening unrelated actors.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Realtime/FuturesMarketPriceUpdated.cs`: remove per-trade request/reply and move eligibility filtering before ITI admission.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Realtime/Actor/`: wire the ordered producer-to-owner path and diagnostics.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Command/Actor/FuturesItiSignalCommandActor.cs`: resident ownership, command compatibility and control-command serialization.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Command/State/`: checkpoint hydration, atomic signal persistence and recovery cursor.
- `TomasAI.IFM.Application.Contracts.MarketData.Analytics/Commands/GenerateFuturesItiSignalCommand.cs` and generated-signal contracts: append backward-compatible source identity/provenance fields where needed.
- Feed/remote ingestion, storage projections and publisher recovery: establish source continuity and post-commit publication, including additive schema changes if needed.
- Analytics actor tests, disposable PostgreSQL integration tests, benchmarks and Rust contract/strategy tests: qualify parity, durability, performance and UI compatibility.

## Execution order and completion record

| Work package | Depends on | Current status |
| --- | --- | --- |
| WP1 Source identity and baseline | Existing read-only capture | Implemented: native cursor plus frozen ATR/VX in a dedicated journal before optional fan-out |
| WP2 Resident ITI state | WP1 identity and ownership contract | Implemented: committed cache, real PostgreSQL session fence, serialized generation/control commands |
| WP3 Zero storage on no-change inputs | WP2 | Qualified: warm no-change decisions make zero audit, restoration, head-check or transaction calls |
| WP4 Ordered ingress and isolation | WP1; integrated with WP2/WP3 | Implemented: local bounded channel, WAL, batches, direct actor delivery; independent of optional actor admission |
| WP5 Durable recovery/publication | WP1–WP4 | Qualified: atomic event/projection/source checkpoint, durable projector markers, WAL/inbox recovery and sealed epoch bridges |
| WP6 Benchmarks and correctness | WP2–WP5 | Twelve BenchmarkDotNet cases completed; all three timeframe recovery fixtures and publisher integration pass |
| WP7 Live activation and acceptance | WP6 passes | Development enabled; clean source-epoch restarts and Rust eGui checked. Overall readiness remains blocked by indicator projections; cash-open tail-latency acceptance remains open |

Implement WP1 first, then the resident actor and its storage-free no-change boundary. Integrate ordered ingress before claiming that the cash-open bottleneck is removed. Keep a dated checkpoint with changed files, test/benchmark artifacts, measured results and any concrete blocker. No changes to trading strategy rules are part of this plan.

## 2026-11-23 initial guarded-cache checkpoint (superseded)

### Implemented and verified

- Adapted `FuturesItiSignalCommandActor` to `BaseInMemoryEventSourceCommandActor` using an independent `ResidentActorPilots.FuturesItiSignal` policy. It is disabled by default; development configuration has not enabled it.
- Warm generation reuses committed state rather than reconstructing the checkpoint and event tail for every command. Hold/clear commands use the compatible durable path and evict cached state before later generation. Failed persistence invalidates the candidate.
- Added `IResidentCommandStateRepository` support to the ITI repository. A cached no-change result checks the authoritative committed stream version; a stale result fails and evicts the cached state. Startup rejects an enabled cache without this guard. Existing command auditing remains in place.
- Qualified Daily, Weekly and Monthly with caching off/on on disposable PostgreSQL: business-signal MessagePack parity against the unchanged evaluator, duplicate suppression, control eviction, restart hydration, rollback of injected commit failures and stale no-change rejection after an external writer. Six lifecycle fixtures passed.
- All 67 ITI command/query/ingress tests and 23 resident actor/cache tests passed. Updated three obsolete traversal assertions to the already-existing Daily 10-point fallback and Daily-only Trending cadence; no evaluator or strategy rules were changed.
- Release build completed with zero warnings and errors. Other pre-existing workflow and Rust UI edits were preserved.

### Earlier recovery blocker and scope boundary (superseded by the dedicated intake below)

The native trade snapshot carries `StreamEpochId` and `TradeOrdinal`. The normalized stored trade shape and the inspected ScyllaDB `tick_trade_data` table do not retain those fields. Provider sequence/timestamps and the publication sequence do not establish the same unique contiguous live cursor. The captured Core NATS fan-out also admits tick storage as optional traffic: rejected source requests can precede their write, while rejected completion notifications can concern an already-saved tick. The 12,125 rejected-message total is therefore **not a count of missing trades**.

The existing capture cannot establish a complete authentic cash-open source tail, nor a durable association of every no-change input with the exact ATR/VX context used at the time. A new field cannot retroactively recover an unrecorded identity or dropped source observation. This prevents the required restart/gap-recovery proof. It is not evidence that ScyllaDB itself is the throughput bottleneck.

The user deferred the detailed tick-storage correction. This checkpoint makes no changes to raw tick storage, its admission policy or its transport. It also does not replace the no-change head guard with an unconditional success, skip audits without a verified owner, infer missing trades, or publish uncommitted signals. The guarded cache is an intermediate implementation, **not** the storage-free fast path and **not** a fix for cash-open queue delay.

Unblocking the remaining packages requires the agreed source/ownership contract: recoverable ordered intake with a durable source cursor and causal ATR/VX inputs, ownership revocation/fencing, and an authentic capture/backfill against which tail replay can be qualified. A separate ITI input journal could provide this boundary; the storage design must determine where it is captured before loss can occur. No historical recovery or live completeness is claimed from synthetic benchmark inputs.

### Reproduction and evidence

- Benchmark source: `TomasAI.IFM.Application.Api.Server.Benchmarks/FuturesItiStorageBaselineBenchmarks.cs`.
- Disposable qualification: run the benchmark executable with `--verify-iti-residency`.
- Paired BenchmarkDotNet run: `--filter '*FuturesItiStorageBaselineBenchmarks*' --artifacts .artifacts/iti-resident-20261123/bdn-paired`.
- Release build root: `E:/IFM/BenchmarkArtifacts/ItiResident/20261123/build`.
- Logs and source/schema evidence: `.artifacts/iti-resident-20261123/` (`qualification.log`, `iti-tests.log`, `resident-tests.log`, `paired.log`, `source-continuity-evidence.json`, `live-tick-trade-schema.txt`).
- Measurements cover the real Daily actor, command audit, PostgreSQL restoration/guard, evaluation and atomic source/projection writes. NATS network latency, real market pacing, real projector delivery and workflow/UI processing are excluded; the projector is a test substitute. MemoryDiagnoser measures fixture-inclusive allocations under concurrent workstation GC. These measurements do not establish live LOH/Gen2 improvement or the end-to-end two-second target.

### Paired BenchmarkDotNet results

Same executable, identical deterministic price/ATR/VX inputs, independent disposable PostgreSQL fixture per case, residency disabled versus enabled. Sixteen sequential inputs per invocation, three warm-ups and twelve measured iterations; results below are normalized per input. No compilation or fixture startup is included. The benchmark's automatically generated Ratio column groups by parameters and does not compare the residency modes; the percentages below are calculated directly from the paired means.

| Scenario | Before mean | Guarded resident mean | Time reduction | Before allocated/input | Guarded resident allocated/input |
| --- | ---: | ---: | ---: | ---: | ---: |
| Warm no-change trade | 28.06 ms | 19.66 ms | 29.9% | 46.12 KiB | 27.72 KiB |
| Signal-changing trade and commit | 46.37 ms | 39.63 ms | 14.5% | 106.83 KiB | 72.06 KiB |

Across 272 processed inputs per case, disabled residency performed 272 state restorations. Enabled residency performed zero additional restorations after hydration, with 272 cache hits. The no-change resident case still performed **272 authoritative head guards**, and command audit reservations remained on both paths. No-change cases persisted zero additional signal events; each changing case persisted exactly 272 additional signal events. Allocation reductions were 39.9% and 32.5%, respectively.

The separate pre-implementation baseline measured 28.65 ms/no-change input and 48.86 ms/changing input. The paired table is the controlled comparison for this cache change. The guarded resident case is not an after measurement of the unimplemented zero-storage/ordered-ingress path. The workload does not provide valid live LOH/Gen2 or paced cash-open p95/p99 results.

Generated report: `.artifacts/iti-resident-20261123/bdn-paired/results/TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks-report-github.md`.

## 2026-11-23 source recovery unblock

The previous blocker is resolved for the new intake boundary. `TickAggregationEventPublisher` delivers current ES trade observations to the local ITI consumer before optional actor fan-out. The consumer freezes original source identity, timestamps, completed ATR provenance and VX input in a checksummed binary WAL before queue admission. The bounded worker imports inputs into PostgreSQL in batches, primes cold state outside decision processing and invokes the existing evaluator through the resident command actor. It does not average, coalesce away or re-time trades.

The actor holds a real PostgreSQL advisory-lock session shared with the repository's legacy writer fence. A lost/expired owner stops decisions; a local success flag cannot replace ownership. Genuine events, their source checkpoints, accepted analytic projections and required projector markers commit atomically. A no-change decision advances only resident progress and performs no database work. Background heartbeat and durable intake are separate from that decision boundary. Control commands retain auditing and use the same owner. Legacy callers without the local durable-admission capability retain auditing and stream-head protection.

Startup first verifies the original WAL root, imports its tail into the shared journal, restores checkpoints and replays pending inputs in logical source order. Recovery retains the original serialized envelope, timestamps and causal inputs. Gaps, changed payloads, missing WAL roots and corrupt/torn records fail closed. Authentic late arrivals can repair a gap. A completed native replay checkpoint plus a drained/sealed predecessor permits an explicit source-epoch bridge; an unresolved gap cannot be cleared by simply starting another epoch. Rust emits replay/first-live checkpoints without the ordinary two-second throttle so this boundary is visible.

The realtime actor explicitly joins/starts its command owner through the supervisor's per-actor lifecycle gate before attaching intake. This handles the supervisor's concurrent managed-actor startup without polling, a decision retry or opening intake ahead of recovery. The live readiness route recognizes the local consumer rather than requiring the removed optional NATS route.

This does not recover previously unrecorded ScyllaDB source ordinals or claim completeness for the old cash-open capture. It also does not change general tick storage or its shared admission budget. New source history is owned by ITI rather than inferred from that incomplete older table.

### Final automated qualification

- `--verify-iti-fast`: Daily, Weekly and Monthly; evaluator MessagePack parity, zero no-change storage/audits/head checks, duplicate suppression, exclusive competing owner, killed PostgreSQL owner session, rejected commit rollback, shared-inbox restart, WAL-only restart, authentic late gap repair, missing input context, sealed epoch handover, original timestamps/IDs and frozen ATR despite a changed current cache, torn WAL rejection.
- `--verify-iti-ingress`: real `TickAggregationEventPublisher` and ordered runtime with disposable PostgreSQL; pre-fanout delivery, duplicate/quote exclusion, drain/seal, atomic durable projector-state markers, restart and native-checkpoint epoch handover. The fixture substitutes the downstream projector; marker persistence and live delivery are distinct checks.
- 67 ITI tests; 32 shared resident/cache/framework tests; native library 119 passed, 11 ignored.
- 224 ITI actor/event/handoff unit tests now pass with normal warnings-as-errors. Their protected test adapters were updated to the resident store hooks and cancellation-aware repository overloads; original argument guards were retained. A named internal MessagePack resolver avoids exporting a conflicting default resolver from the new domain envelope.
- 22 remote-admission/current-value/receiver tests pass, including the new subscription and exact-manifest startup barriers.
- Release build: zero warnings/errors. Qualification logs: `fast-v4-qualification.log`, `ingress-qualification.log`, `fast-iti-tests.log`, `fast-framework-tests.log`, `native-tests.log`, `final-build.log` under `.artifacts/iti-resident-20261123/`.

### Before/after BenchmarkDotNet

Same executable and immutable synthetic ordered prices, 16 inputs/invocation, three warmups and twelve measured iterations, real disposable PostgreSQL. Values are normalized per input. Projector dispatch, NATS network latency and live market pacing are excluded. Background development/build activity introduces timing variance; durable ingress is reported independently instead of labeling decision-only timing as end-to-end latency. Both modes include the common required projector marker; Before selects the original stateless load/audit path. Boundaries are independent cases: do not subtract their means to infer the incremental cost of journaling.

| Measured boundary | Before | Fast | Before allocated/input | Fast allocated/input |
| --- | ---: | ---: | ---: | ---: |
| Warm no-change actor decision | 24.680 ms | 0.009156 ms | 46.32 KiB | 1.13 KiB |
| Signal-changing actor + atomic commit | 43.696 ms | 16.093 ms | 114.50 KiB | 47.47 KiB |
| No-change including WAL flush + PostgreSQL intake | 24.753 ms | 2.081 ms | 46.72 KiB | 4.64 KiB |
| Signal-changing including durable intake | 44.131 ms | 14.593 ms | 113.95 KiB | 50.99 KiB |

Across each 272-input fast no-change case, instrumentation recorded zero additional state restorations, audit calls, stream-head checks and decision transactions. Signal-changing cases persisted all 272 events. MemoryDiagnoser recorded no Gen 0/1/2 collections in these short iterations; this is not a live LOH/Gen 2 claim. WAL frames are bounded below the LOH threshold and use pooled framing buffers; ordinary journal payloads remain small binary arrays.

Final report: `.artifacts/iti-resident-20261123/bdn-final/results/TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks-report-github.md`. All twelve final cases completed; `final-fast-qualification.log`, `final-ingress-qualification.log` and `final-prefix-qualification.log` also passed with the final serializer/build.

### Upstream handover found and repaired during live qualification

The first profile start exposed concurrent actor startup: realtime intake could attach before its command owner completed recovery. It now explicitly starts/joins the command owner through the supervisor lifecycle gate.

The next restart correctly failed closed on a new native source beginning at ordinal 92. `MarketDataClientReceiver` was discarding early frames before its two-second snapshot reconciliation installed the manifest. Feed startup now awaits an acknowledged NATS publication subscription, and the receiver holds the first ordered frame until its exact dataset/worker/generation/date/revision is admitted. Subsequent frames take the completed in-memory admission path. A separately owned data-plane connection prevents bounded publication backpressure from blocking the control reply that establishes admission. These waits occur during source admission, never inside a trading decision.

The affected prefix was recovered through the existing Databento intraday live replay entitlement, without a paid Historical download. Native acceptance order was reconstructed using the actual native `(provider sequence, event time at 100 ns)` rule and verified against **156 captured WAL records**, with exact provider sequence, event time and price matches. The missing **91** authentic records were imported after passing a dedicated disposable PostgreSQL fixture. The original captured records were not modified. Replay receive timestamps were retained for missing records; they differ from the original connection's receive stamps by up to 2.6 microseconds in the matched window. No original receipt time was invented.

The recovered startup prefix explicitly has unavailable causal ATR/VX context, matching the first captured record's unavailable state. It advances continuity without running the evaluator or substituting current context. Existing captured tail records retain their original frozen inputs. The fixture verified exact event timestamps, contiguous ordinals, a sealed predecessor bridge, no synthetic signals and idempotent restart. Evidence: `verified-prefix-proof.json`, `gap-prefix-live-trades.csv`, `final-prefix-qualification.log`, `prefix-production-import.log`.

### Live activation and remaining acceptance

- Profile: VS Code **API + Rust eGui + Rust MarketData**, left running from `.artifacts/development-restart-20261123-iti-final/`. Development enables committed residency and ordered intake; other environments remain opt-in.
- Two subsequent source epochs started at ordinal **1** and remained contiguous through clean restart. The earlier 6,992-record saved tail recovered successfully. No pending journal data was discarded to bypass the gap.
- Latest saved live sample: **7,981 captured = 7,981 processed**, pending **0**, failures **0**; all **7** committed signals projected and handled, with **7** workflow starts in that snapshot. A subsequent SQL query found **zero duplicate `(triggerEventId, workflowEntityId)` executions**.
- The subsequent eight-workflow fresh-epoch SQL sample averaged **337.177 ms** and had a maximum **1,793.520 ms**, with none above two seconds. This small, mostly No Trade sample is not a cash-open stress result. The startup workflow began 6.304 seconds after its market observation; subsequent observations were ahead of the host UTC clock by roughly 0.6–0.8 seconds. Preserve these timestamps and do not present them as precise end-to-end latency percentiles.
- Rust eGui displayed new Daily workflows, opened saved details and loaded Weekly/Monthly histories under the exact ES contract and their specific assignments. Daily retains its filtered graph events; other timeframes show all graph events.
- `/health/launch-ready` **200**; ITI intake and durable completion **Healthy**. `/health/ready` **503** remains due to missing ES Daily projections. Initial 15-minute staleness cleared during observation. Readiness was not weakened. This independent indicator issue and representative cash-open latency acceptance prevent marking the entire live acceptance package complete.
- Final evidence: `final-live-pipeline.json`, `final-live-ready.json`, `final-workflow-timings.txt`, `pre-final-restart-source.txt`, and the final profile logs. No change to signal thresholds, strategy entries, exits or general tick-storage admission is part of this implementation.
