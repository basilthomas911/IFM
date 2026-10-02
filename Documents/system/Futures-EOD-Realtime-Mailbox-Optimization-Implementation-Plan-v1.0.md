# Futures EOD realtime mailbox optimization — implementation plan v1.0

Date: 2026-10-02
Status: Experimental mailbox handoff implemented for basic testing; durable ingress and snapshot coalescing deferred

## Goal and evidence

Keep ES and VX rolling EOD values current without making the EOD actor mailbox wait for a database query, database write, and multiple NATS publications on every trade. Preserve contract and value-date isolation, trade ordering, official session statistics, and the existing persisted EOD contract.

In the 2026-10-02 API run, 225 ES trade-handler exits had a 552 ms median and a 38.9 s maximum; 64 exceeded one second and five exceeded five seconds. The mailbox processes one message at a time and has a configured 2,048-message limit. These measurements demonstrate mailbox exposure, but do not yet identify which awaited dependency dominates. The same run also had wider actor-type admission pressure and a separate fatal NATS publication stall; this plan does not claim EOD processing was the sole cause of that shutdown.

Implementation discovery: `MarketDataDbContext.InsertFuturesEodDataAsync` appends a distinct `futures_intra_day_data` row on **every** trade in addition to updating the current EOD row and maintained projections. The user requires preserving every history row. Consequently, latest-only coalescing of calls to this method is invalid; the storage operations must be split before a coalescing writer can be used. The 2026-10-02 instrumentation change adds source IDs and slow-stage timing without changing trade semantics.

Admission decision: when the bounded durable history queue is full, pause and backpressure the feed until storage catches up. Do not drop or replace a historical observation.

## Implementation checkpoint and blocker

The EOD actor now logs source-correlated handler and slow-stage durations. Its ES path caches the last successfully projected EOD value per contract and value date, skips duplicate or older positive tick sequences, and invalidates the cache on session-statistics/EOD updates, failed projection, exception, and shutdown. Failed current/previous-row queries now throw instead of being mistaken for absent data. The focused `FuturesTickDataEventActorTests` suite passes (12 tests). This removes steady-state ES EOD reads but still awaits the existing projector on each changed trade; it does not meet the mailbox latency target.

The requested no-loss queue cannot safely be added behind the current EOD mailbox. `FuturesTickTradeDataInsertedEvent` is broadcast over fire-and-forget Core NATS. `NatsTransportOverload.SettleCoreRejectionAsync` disposes rejected optional traffic, and there is no acknowledgement or flow-control path from this mailbox to the Databento publisher. Upstream, `BoundedRealtimeTickPublisher.PublishAsync` returns after admitting a tick to its in-memory queue, before transport publication. It rejects when full, and its processing loop can discard accepted items on expiry, cancellation, or shutdown. A mailbox-local pause would let upstream continue publishing and cause admission drops before durable enqueue. The generic `IDurableReplayQueue` also has no bounded stream-capacity/backpressure contract, and its JetStream transport currently creates streams without a configured maximum. Enqueueing only after actor delivery cannot establish the requested preservation guarantee.

To resume Stage 3, first establish an acknowledged, bounded trade ingress before Core NATS fan-out, or replay from the already persisted normalized tick table with a proven complete ordering and catch-up protocol. Wire queue saturation to the feed's existing pause/recovery gate and prove the source stops before acknowledging further ticks. Then give each intraday observation a stable source-trade key so a retried write cannot append a second history row. Keep the current per-trade projector path until this admission contract is tested end to end; reducing its write cadence beforehand would discard history.

## Basic testing path authorized on 2026-10-02

The user explicitly deferred source pause/replay to measure handler throughput first. In Development, `MarketData:FuturesEodAsyncTradeWorker` now enables a bounded 2,048-item, single-consumer, process-local queue **per contract** (maximum 16 contracts). The EOD mailbox enqueues each admitted trade and returns; that contract's worker invokes the existing per-trade handler and projector in order, preserving the intraday row per successful projection. Separate contracts can progress independently. A full contract queue waits instead of silently dropping within this handoff. Session-statistics work and EOD cache invalidations enter the same contract queue in mailbox order; they do not drain all workers from the mailbox. Shutdown drains queued work before stopping the projector. Worker failure is logged and closes that contract's admission; no retry or restart replay is claimed. Production configuration leaves this experimental path disabled.

The mailbox log `Futures EOD trade handler exit` reports sampled handoff duration with outcome `Queued`; `Futures EOD queued trade exit` reports sampled processing duration and per-contract queue pending count. Slow handoffs (10 ms) and slow worker executions (250 ms) are always logged. Compare these with the projector `database_apply`, `source_publish`, and `completion_publish` stage logs. A deliberately blocked-projector test proves mailbox return before processing completes, a capacity-one test proves ordered processing and waiting at saturation, and a two-contract test proves independent progress without an update-triggered mailbox drain. The focused actor suite passes 15 tests. This is a latency experiment, not completion of the durable history/snapshot design; upstream Core NATS may still drop messages and any worker failure can leave process-local backlog unfinished.

## Current flow

`FuturesEodDataRealtimeActor` routes each admitted `FuturesTickTradeDataInsertedEvent` into `FuturesTickTradeDataInserted.ExecuteAsync`. ES processing resolves the contract, requests today's persisted EOD row, may request the previous row, applies session statistics, computes a replacement value, then awaits `BaseRealtimeProjector.ProcessRealtimeEventAsync`. The projector publishes a source event, awaits a ScyllaDB insert, and publishes a completion event. VX skips the EOD read but still awaits its projector's publication and insert. The actor cannot consume its next mailbox message until these awaits complete.

The `FuturesEodDataUpdatedEvent` and routed market-price `Updated` payloads share a verb; dispatch must continue using the source subject to distinguish them. The ES chart is fed by the separate 15-second futures-bar pipeline and is outside this change.

## Stage 1 — isolate the latency

1. Add structured durations around contract lookup, today's EOD query, fallback query, calculation, source publication, database apply, and completion publication. Use `source.Id` or `TickDataId` as the correlation key; the current `EventId` log field was zero for all sampled events. Include contract, value date, and outcome.
2. Record handler duration and mailbox depth/queue wait in metrics, with bounded or sampled information logs during normal tick rates. Count skipped trades, persistence attempts, failures, and coalesced updates.
3. Capture a representative ES/VX run. Use p50/p95/p99 and maximum by stage, plus incoming trade rate, queue depth, and drop rate. Confirm that the planned removal of per-trade I/O targets the measured bottleneck.

## Stage 2 — actor-owned rolling state

1. Add one state slot per `(ContractId, ValueDate)` owned by the EOD actor. Store the latest calculated EOD read model, last applied trade sequence or source timestamp, latest official session-statistics version, dirty version, and last persisted version.
2. Hydrate a slot once on first eligible trade or actor startup: read today's row and, only when needed, the previous row; use the existing official-statistics baseline rules. Keep the slot uninitialized on a transient query failure and retry safely. Never treat a failed query as an empty row.
3. On each later trade, check the slot and monotonic trade identity and apply the existing OHLC and volume rules in memory. Expose the latest in-memory value through an actor-owned read boundary; update the external blackboard at a bounded cadence unless a consumer requires every trade. Preserve the existing behavior for unchanged prices, incomplete statistics, VX-specific calculations, and value-date rollover.
4. Feed `FuturesSessionStatisticsUpdatedRealtimeEvent` through the same slot so a later trade cannot overwrite newer official OHLC/volume with stale values. Clear or rehydrate slots on actor restart, value-date transition, contract rollover, and feed reset.

Exit condition: steady-state trade handling performs no EOD query and no synchronous database or NATS publication. State transitions are deterministic under duplicate and out-of-order trades.

## Stage 3 — bounded, ordered persistence

1. Split `InsertFuturesEodDataAsync` into an append of **every** immutable intraday observation and an upsert of the latest EOD snapshot plus maintained projections. Preserve the existing public method for other callers. Define an idempotent observation key based on source trade identity, so retries cannot create duplicate history rows. Migrate existing readers only if required by that key change.
2. Place every admitted history observation on a bounded, durable ordered work stream before acknowledging it. The persistence worker appends each observation in order. Queue saturation must produce explicit backpressure or a terminal preservation fault; it must never silently coalesce or drop history.
3. In the same worker, coalesce only the latest current-EOD snapshot into a bounded flush interval (initial target: one second). Track monotonically increasing state versions. Mark a version persisted only after its database write succeeds; never let an older completion overwrite a newer value.
4. Publish the existing completion notification only after the corresponding current-EOD snapshot is persisted. Review consumers of source and completion events before reducing their frequency; retain any every-trade contract explicitly. Emit failure telemetry and retry with bounded backoff. Surface durable-queue depth and oldest unpersisted age as degraded health.
5. During shutdown or recovery, stop admitting new work, drain to a deadline, and retain the durable backlog for restart if the deadline expires. Rehydrate actor state from the last persisted row and replay preserved observations in source order before accepting new trades.

Exit condition: no unbounded in-memory per-trade queue; every admitted history observation is durably retained, writes and completion notifications are ordered, and a slow ScyllaDB write does not hold the trade mailbox while durable admission has capacity.

## Stage 4 — qualify and roll out

1. Unit-test baseline hydration, duplicate and out-of-order trades, same-price/no-volume change, official-statistics corrections, VX behavior, value-date and contract rollover, failed queries, failed writes, retries, and stale write completions.
2. Run integration tests with delayed ScyllaDB and NATS publication. Verify that thousands of rapid ES trades leave one bounded pending snapshot per key, that persisted values remain monotonic, and that completion events describe persisted versions only.
3. Compare a live-like run with the 2026-10-02 baseline: handler p95 under 10 ms after hydration, zero steady-state EOD queries per trade, bounded persistence lag (target under 2 seconds in a healthy system), and no EOD mailbox admission drops under the same input rate. Treat these as qualification targets, not promises until measured.
4. Roll out behind a configuration switch for the ES path first, retaining the current path as rollback. Include mailbox depth, persistence age, failure count, and last persisted version in operational health. Expand to VX after ES qualification; remove the old path after sustained verification.

## Implementation boundaries

- Keep the EOD mailbox as the sole owner of rolling state mutations. The writer receives immutable snapshots and cannot mutate actor state directly.
- Do not fire-and-forget projector calls from `ExecuteAsync`: that would lose ordering, error visibility, and shutdown guarantees.
- Avoid using the Redis-backed blackboard as a substitute for the actor-owned rolling slot until its read/write latency and consistency are measured.
- Preserve the current `FuturesEodDataV2ReadModel` serialization and `Updated` source-subject dispatch fix.
