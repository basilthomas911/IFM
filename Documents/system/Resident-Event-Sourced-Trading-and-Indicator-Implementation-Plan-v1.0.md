# Resident event-sourced trading and indicator implementation plan

Version: 1.0

Date: 2026-11-08

Status: first five pilots implemented and tested; primary 70-case BenchmarkDotNet matrix, focused repeats and 30-minute soak complete. Development processes restored with the five independent policies enabled. Live acceptance is blocked by stale server ValueDate November 6 and provider replay-window rejection at the November 8 opening; see the verification report. Remaining five actors are deferred until the user reviews pilot timings and this gate is resolved.

Implementation evidence and current acceptance status: [First five pilot verification](Resident-Actor-Pilot-Verification-2026-11-08.md). Full before/after timing, allocation, GC and locking tables are linked there. This plan's later waves are not authorized for automatic rollout before that review.

Implement the strongest candidates first, preserving existing event-sourcing, actor and query conventions. Scope is ten actors: seven Command actors and three Function actors. New residency remains independently configurable by actor. Financial, broker-order and order-execution actors are excluded.

## Selected actors and order

| Wave | Actor | Why selected | Main correctness condition |
|---|---|---|---|
| 2 | `FuturesVwapSignalCommandActor` | Repeated updates reuse one contract/session/configuration checkpoint; avoid repeated snapshot reads and reconstruction. | Preserve cumulative source identity, corrections, duplicates and recovery semantics. |
| 2 | `MarketOutlookSnapshotCommandActor` | Successive snapshots reuse a small previous state; repeated PostgreSQL loading is avoidable. | Preserve revision, provenance, contract and ValueDate boundaries, and committed publication. |
| 3 | `FuturesTradePlanFunctionActor` | Repeated monitoring requests reuse the latest plan for one position/session. | Preserve plan revisions, accepted thresholds, request matching and material-change publication. |
| 3 | `VerticalSpreadTradePlanFunctionActor` | Same reusable position/session state with frequent monitoring changes. | Preserve full plan state and source/projection atomicity. |
| 3 | `IronCondorTradePlanFunctionActor` | Repeated requests currently restore creation/latest-checkpoint state for the same position/date. | Preserve creation metadata, accepted stop/watermark state, revisions and required plan projections. |
| 4 | `FuturesAtrSignalCommandActor` | Reuse recurring smoothing/checkpoint state, especially short-interval indicators. | Preserve historical initialization and separate Daily/intraday identities. |
| 4 | `FuturesRsiSignalCommandActor` | Avoid reconstructing rolling prices and RSI history for every observation. | Cached capacity must satisfy the requested retained window; reload when insufficient. |
| 4 | `FuturesAdxSignalCommandActor` | Reuse directional-movement, smoothing and retained-history state. | Preserve wide-history reconstruction and bar sequence/duplicate handling. |
| 4 | `FuturesMacdSignalCommandActor` | Reuse EMA/signal-line state and retained history. | Preserve history limits, checkpoint eligibility and every persisted stream-version increment. |
| 5 | `FuturesItiSignalCommandActor` | Frequent evaluations reuse trend/extreme/reversal/overshoot state on stable timeframe streams. | Preserve hold-trade controls, exact signal generation and durable workflow triggering; highest business sensitivity. |

The first rollout consists of the first five actors. The following five proceed only after pilot correctness and performance acceptance. Ranking is based on inspected code and expected state reuse, not newly measured speedup percentages.

## Verified starting point

- `BaseInMemoryEventSourceCommandActor<TActor,TState>` currently serves the three position Command actors. Exact selected command types retain resident state; other commands drain and reload through the normal path.
- Its existing switch is named `OptionTradeLegDataEnabled`, with defaults of 4,096 resident streams and 64 commands/window. New actors must not silently inherit an option-specific enable switch or a 64-command speculative window.
- Current Command repositories already use snapshot loading. Residency targets repeated database reads, event deserialization and reconstruction; it does not justify weakening or removing durable snapshots.
- VWAP and Market Outlook repositories append source facts and required MarketDataDb projections in the same PostgreSQL transaction. Their resident adapters must preserve the entire transaction and post-commit behavior.
- The Function base is `BaseEventSourceFunctionActor`, with completed-only persisted state, typed Complete/Fail replies, exact request matching, deadline/cancellation policies, projection policies and commit-ambiguity handling. Function contexts and state contracts differ from Command contexts and state contracts.
- Trade Plan Functions use stable position/date identities and prepare loaded state for each request. The cache must retain committed business state, not a mutable object carrying another request's active command marker.
- RSI/ADX/MACD have history-sensitive state and periodic snapshot behavior. Some saves append a snapshot in addition to business events, so `businessEvents.Count` alone is not always the next committed stream version.
- The older September in-memory actor plan describes a previous OptionTrade pilot. Preserve that historical record; use current source, current actor names and fresh measurements for this rollout.

## Invariants

1. PostgreSQL events remain authoritative. Warm memory is disposable and recoverable from existing event contracts.
2. Cold loads, eviction and restart restore the latest qualifying creation/full-state checkpoint plus its required suffix, using existing aliases and compatibility rules. A creation-only event is insufficient when it does not recreate current state.
3. Preserve exact per-stream ordering, audit/payload-conflict checks, expected versions, no-op semantics and durable duplicate handling.
4. Preserve source events, required projections, checkpoints and command-audit participation under the owning repository's existing atomic policy. Do not replace a specialized transaction with a generic event-only append.
5. Reply successfully only after the current persistence/projection policy has been satisfied. Post-commit notification failure must not imply a rolled-back source commit or trigger duplicate persistence.
6. New Command pilots initially use one outstanding durable operation per stream. Reuse state without advancing speculative state across multiple new commands. Preserve the existing position actors' established behavior and configuration.
7. Functions initially cache committed state only. Preserve typed failures and retries; never cache failed outcomes, negative readiness or an unconfirmed completion as durable truth.
8. Do not reuse a mutable Function state across an unfinished/timed-out request. Late work needs isolated request state and ownership/generation checks; ambiguous commits require authoritative reconciliation.
9. Query handlers continue to read projected read models. They must not read resident Command/Function state directly.
10. Preserve market-input freshness, ValueDate, configuration versions, three independent strategy timeframes, quiet-feed readiness and the existing persistent four-hour seed cache.

## Wave 0 — inventory, baseline and acceptance fixtures

- Record Git revision plus working-tree changes, source/fixture hashes, runtime/SDK, PostgreSQL/NATS settings and hardware.
- Map the exact stream identity, eligible command types, state load, persistence policy, snapshot event(s), projection writes and post-commit publications for each selected actor.
- Measure actual same-stream reuse, state-load SQL counts, replay rows/bytes, state size and load/compute/save durations. Record when upstream code awaits each reply; do not infer production batching from an artificial queued burst.
- Capture representative valid deterministic fixtures: successive/no-change/duplicate VWAP checkpoints, Outlook revisions, all three position-plan types, short-interval indicator bars and ITI transitions.
- Preserve an immutable benchmark reference from the reviewed current tree. Do not reset Development or conflate earlier API performance results with this new baseline.
- Verify how repositories advance `CommittedStreamVersion`, including periodic snapshots, before defining resident commit receipts/adapters.

Exit: baseline and equivalence fixtures cover every selected actor; unknown ownership or persistence behavior is resolved before conversion.

## Wave 1 — shared residency foundations

### Command support

- Introduce explicit per-actor eligibility and configuration without changing existing position-actor defaults. New actor switches start disabled.
- Keep the current parser/validator/receive maps and command extension handlers. Residency is a state/persistence optimization, not a replacement actor convention.
- Add typed persistence adapters where needed. Capture immutable pending event/checkpoint data at the correct transition, and report the actual committed version; never capture a later mutable working state from a deferred callback.
- Support history-capacity checks, explicit invalidation, expiry, failure eviction and a safe fallback to standard loading after draining the affected stream.
- Enforce an admission limit on resident keys. Busy/pinned entries must not cause uncontrolled growth: use a bounded wait or the safe standard path after the necessary barrier. Do not evict an owner with unfinished work.
- Bound retained histories and idle residency separately from key count. Report estimated retained bytes and sampled process memory; a count limit or serialized-size estimate is not a hard managed-heap byte bound.
- Expose actor-scoped hits, misses, invalidations, evictions, active/pinned keys, pending work, actual replay/SQL counts and commit failures. Avoid option-position-only metric labels for new actors.

### Function support

- Add `BaseInMemoryEventSourceFunctionActor` derived from the existing Function base, retaining its lifecycle and typed contracts.
- Reuse a shared bounded residency component where practical; do not copy the Command message-processing state machine into Functions.
- Cache an immutable committed checkpoint/state representation with its stream identity and committed version. Restore a request-local state and run the existing request preparation and matching rules for every call.
- Populate/advance the cache only after confirmed persistence according to the existing policy. Preserve completion replay, payload conflict detection and projection recovery semantics.
- Use existing load/save hooks for compatible Functions. Do not admit a transactional Function whose direct commit path bypasses the cache's success/invalidation hooks until an explicit lifecycle hook has been qualified.
- Fence late load/calculation/commit work after timeout or cancellation. An unknown commit outcome must poison/invalidate the owner and be reconciled before it can supply authoritative cached state.
- Scope cache keys to actor and complete stream identity, including position/date and relevant configuration/history capacity. In-process residency is not distributed ownership; retain optimistic concurrency and invalidate after another writer wins.

Exit: shared unit tests pass; no behavior regression in the three existing resident position actors; new policies can be independently disabled and drained.

## Wave 2 — first Command pilots

1. Convert only live `UpdateFuturesVwapSignalCommand` to resident eligibility. `RecoverFuturesVwapSignalCommand` drains/invalidates and uses authoritative loading. Preserve session/configuration isolation.
2. Convert `InsertMarketOutlookSnapshotCommand`. Preserve existing no-change behavior and committed publication. The separate Outlook read cache does not replace event-sourced command state or its projection.
3. Await each new durable operation before the next command advances the stream. Keep atomic source/projection writes and existing notification classification.
4. Run equivalence, PostgreSQL and NATS tests, then paired actor-level measurements. Enable only qualified actors in Development.

Exit: equivalent event/state/read-model results; observed warm hits avoid repeated state-load queries; restart, disable and failure recovery pass; no material unexplained latency or memory regression.

## Wave 3 — Trade Plan Function pilots

1. Adopt the new Function base for Futures, Vertical Spread and Iron Condor Trade Plan Functions.
2. Preserve `Prepare(request)`, fingerprints, latest accepted plan revision, obsolete-source guards and exact plan threshold/watermark calculations.
3. Preserve each repository's PostgreSQL transaction and material-change/publication behavior; do not make the three repositories artificially identical.
4. Reconstruct an independent working state per request from the retained committed representation. Preserve creation metadata where required by the current reducer.
5. Verify failed/late requests cannot modify another request's state, and that notification failures do not invalidate a confirmed durable completion as though it had rolled back.

Exit: all three plan outputs match the baseline, duplicate/conflicting requests behave identically, and measured same-position reuse demonstrates a worthwhile benefit.

## Wave 4 — short-interval indicator residency

- Convert ATR, RSI, ADX and MACD incrementally, retaining the exact contract/timeframe/period/configuration identity.
- Select recurring generation commands explicitly. Startup, stop, recovery, reseeding and incompatible history/configuration changes use a barrier and authoritative loading unless independently qualified for residency.
- Preserve Daily-baseline behavior, independent short-interval startup and historical initialization.
- Check resident history capacity before reuse. A wider request must hydrate/reload from a qualifying snapshot/history; never return a smaller cached history merely because the key exists.
- Preserve periodic snapshot cadence, full reconstruction data and the extra event/version advances. Cached state and subsequent cold replay must agree at snapshot boundaries.

Exit: each indicator passes its owning suites, wider-history cases where applicable and PostgreSQL latest-snapshot/restart tests before the next actor is enabled.

## Wave 5 — ITI residency

- Admit `GenerateFuturesItiSignalCommand` only after its state, event and audit/projection behavior is qualified.
- Hold-trade controls and other noneligible operations drain and reload; no administrative transition may be hidden by retained state.
- Compare direction changes, trend extremes, reversal resets, overshoot extension/reversal events and no-change evaluations against the baseline.
- Retain the established 20% threshold in ITI regression fixtures. Exercise BandLevel crossings and repeated movement around 1.0.
- Prove every accepted qualifying committed signal retains its workflow handoff behavior, with no missed or duplicate workflow starts. Preserve committed-event telemetry as part of the resident persistence path.

Exit: exact signal/event ordering and workflow handoff equivalence under duplicates, failures, restart and independent Daily/Weekly/Monthly streams.

## Required tests

| Layer | Required coverage | Owning suites |
|---|---|---|
| Shared unit | Cold/warm hits; strict stream/configuration isolation; admission/idle eviction; all entries pinned; fallback/disable barriers; no-op commands; duplicate/conflicting payloads; persistence failure; unknown commit; shutdown drain and completion cleanup. | Shared actor runtime tests and Domain.Application actor tests, following current ownership. |
| Function unit | Completed-only cache; fresh request-local state; preparation/fingerprint matching; changed inputs; completion replay; typed Fail; deadline/cancellation at every stage; late success/failure; projection/commit policy; no cache promotion before confirmed commit. | Shared Function lifecycle tests plus Domain.Trade unit tests. |
| Command/domain unit | Identical decisions, event payloads and order; VWAP recovery/corrections; Outlook revision/session handling; ATR initialization; RSI/ADX history capacity; MACD snapshot/history boundaries; ITI hold/overshoot/reversal and committed handoff. | Domain.MarketData.Analytics unit tests and existing position-actor regression suites. |
| BDD | Repeated market observations; duplicate/no-change; recovery and resume; repeated position-plan updates; changed parameters; wider indicator history; session rollover; failed persistence followed by authoritative recovery. | Domain.MarketData.Analytics BDD and Domain.Trade BDD projects. |
| PostgreSQL integration | Latest creation/full-state snapshot loading; snapshot plus suffix; periodic checkpoint version accounting; atomic event/audit/projection rollback; optimistic conflict; cross-writer invalidation; cache eviction; new process restart; projection rebuild equivalence. | Storage integration, Analytics integration and existing Trade integration/verification projects as applicable. |
| NATS/actor integration | Real request/reply path; success after required durable work; serial awaited callers and bounded concurrent callers; duplicate/conflicting command IDs; disconnection; restart; in-flight shutdown; no duplicate events or publications beyond the existing contract. | Actor integration and LivePipeline integration projects. |
| System regression | Existing three resident position actors; Daily/Weekly/Monthly independence; historical seed/reconciliation behavior; projected query results; no changes to broker execution or financial settlement. | Relevant existing full suites and five actor-convention audits. |

Use disposable PostgreSQL/NATS fixtures and recorded inputs. Scylla remains raw tick storage; no additional CQL domain persistence is introduced. Do not submit emulator/IBKR orders, alter Development financial records, or acquire paid data to qualify this optimization.

## Before/after measurement plan

Use BenchmarkDotNet with matching .NET/runtime/GC settings, recorded inputs, source hashes, warmup/iteration settings and provider configuration. Keep benchmark outputs/build artifacts on E:. Include stream resolution in both baseline and candidate cold/end-to-end paths.

| Workload | Comparison and measurements |
|---|---|
| Cold first request | Actual snapshot selection/replay, stream resolution and first persistence. Residency is not expected to eliminate first-load cost. |
| Warm same stream | Repeated real eligible commands; count state-load SQL separately from audit/projection/other SQL. Verify zero additional state loads on a valid hit, not zero total database work. |
| Real caller topology | One caller awaiting every reply, then representative 4/16/64-stream workloads; record achieved concurrency and queue depth. Do not assume a 64-command batch forms in production. |
| Plans | Repeated updates for the same position/date, all three plan types; record full load/calculate/save/reply latency and material-change/no-change cases. |
| Indicators | Fixed valid bar sets across contract/timeframe/period keys; compare generated values, history and snapshots before reporting timings. Include wider-history reloads and snapshot boundaries. |
| Churn and memory | Mixed cold/warm keys, cache-capacity pressure, busy eviction, idle expiry and session/configuration changes. Measure retained/peak memory as well as allocation reduction. |
| Recovery | Duplicate, failure, timeout, disconnect and restart; report recovery latency and committed correctness separately from successful hot-path throughput. |

Publish means with uncertainty, managed allocation/op, Gen0/1/2, completed work/monitor contention, and a separate all-thread CPU/GC-pause/working-set probe. Measure individual request p50/p95/p99 rather than treating normalized batch means as request latency. Record state reads, replay rows/bytes, event/audit/projection counts and cache hit/miss rate.

Run timing with independent Development workloads stopped only during the authorized implementation/measurement phase; restore them afterward. Preserve full source-debugging builds for Development. Do not interpret workstation traces with different workloads as a whole-server speedup, and do not add overlapping gains.

Acceptance: identical domain outcomes and persistence first, measured same-stream read reduction second, reproducible latency/CPU/allocation benefit third. Investigate repeatable latency regressions over 5%; report confidence overlap and memory tradeoffs. Retain the standard path or defer an actor when benefits do not justify complexity.

## Deployment, rollback and final acceptance

1. Enable one new actor at a time in Development; preserve independent switches and actor-specific residency/history limits.
2. Stop admission for a policy change, drain the affected owner and invalidate its cache before standard-path processing. No event rewrite or data migration is required solely for residency.
3. Verify read-only Market Outlook/indicator and Trade Plan views through projected queries and Rust eGui. Keep the Rust Market Data Host as the provider.
4. Run a 30-minute controlled burst/idle soak with bounded keys/memory, followed by an actual market-session acceptance run when inputs are available. Record queue growth, failures, recovery, shutdown and memory retention. A short test or closed-session readiness probe does not satisfy that gate.
5. Restore full-debug API, Rust Host, Rust eGui and scheduler after measurement. Verify manifests, readiness, connections and scheduled triggers.
6. Verify unchanged read-only financial invariants for the settled historical position/Fund/order used by existing regression checks. No financial actor conversion or reconciliation write is part of this plan.
7. Publish a per-actor disposition, raw/summary before-after evidence, tests, snapshot/restart results, failure cases, soak results and any genuine outstanding blocker. Do not mark unexecuted acceptance gates complete.

## Deferred scope

- EMA, Bollinger, TDI, session-bar publication, Trade Signal and VX term structure remain follow-on candidates after the selected ten are measured.
- Forward Price remains conditional on repeated same-position use and the fraction of time attributable to state loading.
- Regime Discovery and Market Condition remain conditional Function candidates: execution-specific identities limit reuse mainly to retries/duplicates. Measure their state-load share and hit rates before adopting the new base; retain fresh-input and atomic-completion rules.
- Strategy/exit workflow Command actors remain secondary; their lower state reuse and handoff/recovery complexity require separate evidence.
- Financial, broker-order, order execution, fills, capacity reservation, ledger and Fund actors are excluded. Mostly stateless ingestion and low-frequency administration/configuration actors are not part of the rollout.

## Existing references

- [Actor implementation conventions](Actor-Implementation-Conventions.md)
- [Actor event-modeling conventions](Actor-Event-Modeling-Conventions.md)
- [Original in-memory Command actor plan, historical](System-Wide-In-Memory-Event-Sourced-Command-Actor-Implementation-Plan-v1.1.md)
- [Original pilot verification, historical](System-Wide-In-Memory-Event-Sourced-Command-Actor-Verification-v1.0.md)
- [Latest API performance qualification and its limits](../../TomasAI.IFM.Application.Api.Server.Core/Docs/Api-Server-Performance-Final-Report-2026-11-08.md)
