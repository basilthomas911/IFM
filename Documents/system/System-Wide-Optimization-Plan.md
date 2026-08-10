# IFM System-Wide Optimization Plan

**Document type:** Living optimization specification and execution plan  
**Status:** Active; execution may be paused for specialized optimization work  
**Created:** 2026-08-07  
**Last updated:** 2026-08-09
**Owner:** IFM engineering  

## 1. Purpose

This document is the persistent system-wide optimization plan for IFM. It is not a fixed-time project plan or a promise to implement every item in its current order. It records the best-known optimization priorities, their design constraints, evidence requirements, implementation status, and decisions so work can pause for specialized investigations and later resume without losing context.

The plan must be updated whenever measurements, architecture changes, paper-trading results, or completed work change the priority or scope of an item. Historical decisions should remain visible in the revision and decision logs.

## 2. Current position

The root domain-actor optimization passes are substantially complete for:

- Domain.Application;
- Domain.Fund;
- Domain.MarketData;
- Domain.MarketData.Analytics;
- Domain.MarketData.Feed;
- Domain.MarketData.Securities;
- Domain.OptionPricer;
- Domain.Reference;
- Domain.SystemAdmin; and
- Domain.Trade.

The shared actor runtime, NATS messaging, application storage, event-source replay, and solution-wide graceful cancellation have also received structural optimization passes. Current work has moved beyond dictionary-versus-switch and async-wrapper micro-optimizations. The remaining high-value work is concentrated in production observability, aggregate backpressure, runtime readiness, the Databento data plane, query-shaped storage, projection recovery, bounded reconstruction, and a small number of measured allocation hot spots.

All ten domain integration projects now form the shared actor-runtime integration gate. The 2026-08-09 SWO-01 confirmation passed 193 of 193 tests. This does not replace component-specific tests, benchmarks, Databento qualification, or production-like performance testing.

## 3. Binding architectural principles

All work under this plan must preserve the following rules.

1. Event history remains immutable and may remain unbounded. Optimization should bound reconstruction and query work through snapshots, typed ranges, query projections, or indexes rather than deleting history.
2. A successful command and a state change are separate concepts. A command can succeed without producing a state change or event.
3. Empty same-domain event actors are valid default publication targets and must not be removed merely because their handlers currently do nothing.
4. Missing snapshots or requested event types should return the best available, possibly empty, reconstructed state. Application code must not manufacture replay exceptions solely because data is absent.
5. Cancellation is honored while work can safely be abandoned. Once durable persistence or another mutation boundary has been crossed, required publication, denormalization, projection, and cleanup must reach a terminal outcome without caller cancellation.
6. Actor-owned state remains protected by mailbox serialization. Locks are introduced only for genuinely shared mutable resources.
7. Independent I/O may run concurrently only when context ownership, connection-pool limits, ordering, and mutation boundaries remain safe.
8. Optimizations require measurements proportional to their risk. BenchmarkDotNet is used for deterministic CPU and allocation paths; integrated or paper-trading measurements are used for storage, network, scheduling, and end-to-end latency.
9. Improvements smaller than measurement variance are treated as neutral and do not justify additional code complexity.
10. Graceful shutdown stops intake, drains accepted work, and then releases actors and their resources. Any force-stop behavior must be a separately named and explicitly lossy operation.

## 4. Scope boundaries

### Included

- shared actor scheduling, lifecycle, mailbox, cancellation, and telemetry infrastructure;
- active domain command, query, event, repository, projection, and storage paths;
- NATS Core and JetStream messaging used by the actor system;
- Databento native-to-managed ingestion and its future actor/event persistence pipeline;
- PostgreSQL and ScyllaDB access used by active application paths;
- event-projector durability, recovery, idempotency, and throughput;
- production-like benchmarking, soak testing, and paper-trading evidence.

### Explicitly excluded

- the current Interactive Brokers market-data feed implementation and its service host;
- queueing code used only by the legacy Interactive Brokers feed;
- removal of intentionally empty event actors;
- speculative jump tables or dispatch rewrites without profiles showing a material dispatch cost;
- removal of explicit `async`/`await` forwarding around storage I/O solely to save an insignificant state machine;
- dormant Trade `AlgorithmBuilder` work unless that workflow is approved for reactivation;
- unrelated product features that do not change the active performance path.

Databento is the primary market-data implementation. A future Interactive Brokers implementation should mirror the proven Databento architecture rather than extend the legacy feed.

## 5. Status and priority definitions

### Status

| Status | Meaning |
| --- | --- |
| Proposed | Identified but not yet designed in sufficient detail. |
| Ready | Scope, dependencies, tests, and acceptance criteria are understood. |
| In progress | Implementation or measurement is active. |
| Paused | Intentionally suspended while specialized work takes priority. |
| Blocked | Cannot proceed without a named external decision or dependency. |
| Measuring | Code is complete and awaiting benchmark, soak, or paper-trading evidence. |
| Complete | Acceptance criteria are satisfied and results are documented. |
| Rejected | Measured or reviewed and deliberately not adopted. |

### Priority

| Priority | Meaning |
| --- | --- |
| P0 | Correctness, loss prevention, or production-readiness issue. |
| P1 | High expected effect on latency, throughput, memory, or operability. |
| P2 | Valuable improvement after P0/P1 evidence is available. |
| P3 | Telemetry-gated micro-optimization or cleanup. |

## 6. Prioritized work register

| Order | Work package | Priority | Status | Primary outcome |
| ---: | --- | --- | --- | --- |
| 1 | Operational metrics export and stage timing | P0 | Measuring | Make system bottlenecks and regressions observable in paper trading. |
| 2 | Aggregate actor backlog and overload control | P0 | In progress | Bound total actor memory and provide explicit system-wide backpressure. |
| 3 | Actor startup readiness gate | P0 | Complete | Prevent intake before every actor-owned dependency is ready. |
| 4 | Databento tick-price actor pipeline | P0 | Paused | Convert the proven feed into the production actor/event persistence path. |
| 5 | ScyllaDB analytics query projections | P1 | Complete | Removed the remaining active `ALLOW FILTERING` signal reads. |
| 6 | Event-projector recovery and idempotency | P1 | Proposed | Reduce duplicate side effects and make replay backlog predictable. |
| 7 | Fund compact snapshots | P1 | Proposed | Keep immutable history while bounding Fund reconstruction cost. |
| 8 | OptionPricer QLNet allocation isolation | P1 | Proposed | Reduce the measured large allocation graph and global lock pressure. |
| 9 | Event-context cancellation completion | P2 | Proposed | Gracefully stop cancellable event-initiated work without violating commit boundaries. |
| 10 | Production performance and reliability gates | P0 | In progress | Turn benchmarks, integration tests, and soaks into repeatable release evidence. |

The order is advisory. Specialized work may temporarily supersede it. When that happens, update the status and active-work sections instead of deleting or silently reordering unfinished work.

## 7. Work-package specifications

### SWO-01: Operational metrics export and stage timing

**Priority:** P0  
**Status:** Measuring

#### Objective

Expose low-overhead runtime evidence that identifies the real limiting stage during paper trading and production-like tests.

#### Current evidence

- NATS counters cover publishing, receiving, dispatch failures, duplicate suppression, listener-only events, operation latency, and operation failures.
- Actor instruments cover lifecycle outcomes, mailbox and ready-queue depth, active mailboxes, logical worker capacity/busy/available/utilization, queue/handler timing, normal outcomes, and bounded processing stages.
- The API server registers an OpenTelemetry metrics pipeline with a production OTLP exporter and .NET runtime/host meters.
- The dormant actor mailbox measurement path reports no per-operation allocation; an active listener adds approximately 124.671 ns per measured mailbox operation on the 2026-08-09 benchmark host.
- The full domain integration gate passes 193 of 193 tests.
- Production-like collector visibility and paper-trading p95/p99 attribution remain outstanding.

#### Deliverables

1. Register the actor and NATS meters with one host-level metrics pipeline.
2. Provide a production-supported exporter, initially OpenTelemetry/OTLP or Prometheus according to deployment requirements.
3. Add low-cardinality instruments for:
   - actor mailbox depth and oldest-message age;
   - accepted, processed, failed, and canceled messages;
   - handler queue time and execution time;
   - command validation, replay, persistence, denormalization, and publication duration;
   - query execution and reply duration;
   - NATS publish, request/reply, dispatch, redelivery, and acknowledgement latency;
   - storage request count, latency, retries, timeouts, and pool pressure;
   - process allocation rate, GC collections and pauses, working set, CPU, and thread-pool queue length.
4. Define tag-cardinality rules. Entity IDs, command IDs, event IDs, contract IDs, and stream IDs must not be metric tags.
5. Document dashboards and paper-trading capture procedures.

#### Acceptance criteria

- Metrics are visible from the production-like host without changing actor behavior.
- Normal message processing adds no per-message heap allocation when no listener is attached.
- Enabled instrumentation overhead is benchmarked and remains within an agreed threshold.
- A paper-trading run can attribute p95/p99 latency to queueing, actor execution, messaging, or storage.

Implementation, benchmark, validation, dashboard guidance, and remaining evidence are recorded in `Documents/system/System-Wide-Optimization-Results.md`.

### SWO-02: Aggregate actor backlog and overload control

**Priority:** P0  
**Status:** In progress; Tranches A through D implemented locally, production remains ObserveOnly

The detailed design, implementation tranches, overload contracts, test matrix, benchmark gates, rollout, and review decisions are defined in `Documents/system/Aggregate-Actor-Backlog-Overload-Control-Implementation-Plan.md`. Tranche A contracts, configuration, observe-only accounting, and the capacity worksheet are implemented. Tranche B adds allocation-free runtime enforcement and deterministic capacity ownership. Tranche C adds explicit Core request/reply failures, classified optional drops, delayed JetStream NAK/redelivery, and transport metrics. Tranche D migrates production Command sends to request/reply, removes the unused Supervisor consumer, and proves local enforced saturation and transport recovery. Production enforcement remains blocked pending production-like measurements, approved limits, and paper-trading rollout evidence.

#### Objective

Bound total actor-runtime memory and make overload behavior explicit across many active entity mailboxes.

#### Current evidence

Individual V2 entity queues previously had bounded slot accounting without a process-wide bound. Tranche B now supports atomic global and actor-type message/byte limits plus non-waiting per-entity enforcement while keeping the ready-mailbox channel physically unbounded and logically bounded by admitted non-empty mailboxes. Tranche C proves typed Core overload replies and delayed durable redelivery over a real NATS server. Tranche D removes the audited required-non-durable Command/Supervisor blocker and proves local limits remain bounded and drain to zero. Production remains in observe-only mode until the worksheet supplies approved capacities and a paper-trading run validates them.

#### Design requirements

1. Measure active entity queues, total queued messages, ready-queue length, queue wait time, and eviction rate before selecting limits.
2. Add configurable process, actor-type, or mailbox-level admission limits without breaking per-entity ordering.
3. Define overload behavior separately for:
   - command requests requiring an explicit failure response;
   - queries requiring a bounded failure response;
   - durable events that must remain replayable;
   - non-durable diagnostics or optional traffic.
4. Avoid silent drops.
5. Preserve graceful drain semantics and payload ownership on rejected or canceled writes.
6. Stress high-cardinality and hot-entity scenarios independently.

#### Acceptance criteria

- Worst-case queued memory is calculable from configuration.
- No accepted message or owned payload is leaked during overload, cancellation, or shutdown.
- Per-entity ordering remains deterministic.
- Sustained overload produces measurable backpressure or explicit failures instead of unbounded growth.

### SWO-03: Actor startup readiness gate

**Priority:** P0  
**Status:** Complete

#### Objective

Prevent consumers from accepting actor traffic until all actors, producers, projectors, and required startup recovery operations are ready.

#### Current evidence

The centralized startup coordinator now registers the complete runtime, awaits every actor's `StartAsync`, and only then opens Core NATS and JetStream consumer intake. Runtime readiness remains false through registration, actor initialization, projector recovery, and consumer startup. Startup cancellation or failure leaves readiness false and invokes the existing non-cancelable supervisor rollback path. The API host exposes the low-cardinality readiness result at `/health/ready`, and graceful supervisor shutdown clears readiness before stopping intake.

#### Design requirements

1. Choose and document one startup contract:
   - initialize actors completely before opening consumer intake; or
   - keep consumers connected behind a closed readiness gate until actors are ready.
2. Preserve rollback-safe startup and the shared supervisor cleanup path.
3. Confirm that JetStream durable delivery and Core NATS request/reply behave correctly during the closed interval.
4. Publish readiness state to host health checks.
5. Add deterministic tests for messages arriving at every startup boundary.

The selected contract is actor-first initialization. Consumers are registered early but are not connected until all actor startup contracts complete. This avoids buffering Core request/reply traffic inside an application that cannot yet process it, while JetStream retains durable events at the server until its consumer starts.

#### Acceptance criteria

- No message reaches an actor before its startup contract completes.
- Startup failure leaves the host unready and rolls back every registered resource.
- The host does not advertise readiness until actor intake is safe.

All acceptance criteria are satisfied. Deterministic lifecycle tests hold an actor startup operation open and prove consumer startup and readiness publication cannot occur, then prove actor-start failure never opens intake and performs rollback. Shutdown coverage proves readiness is cleared before cleanup. The complete Shared unit suite passes 105/105, the API server builds with zero warnings and errors, and the ten-domain sequential Release integration gate passes 193/193.

### SWO-04: Databento tick-price actor pipeline

**Priority:** P0  
**Status:** Paused pending specialized design and schema decisions  

#### Objective

Build the production market-data path from the Databento managed drain to bounded actor messages and ScyllaDB persistence while preserving every tick and reducing message count for unchanged prices.

#### Source specification

Use `TomasAI.IFM.Framework.MarketData.DataBento/Docs/Tick_Price_Event_Pipeline_Specification_v0.1.md` as the detailed subordinate specification. Its readiness gates remain binding.

#### Required phases

1. Resolve comparable-price, `PriceKind`, capacity, actor-message, routing, and persistence-schema decisions.
2. Implement the pure price selector and per-instrument manager contracts.
3. Implement fixed pooled buffers and the bounded single-reader ticker channel.
4. Implement `TickPriceChangedManager` and its ordering/state-machine invariants.
5. Integrate after the managed Databento drain and before source-batch release.
6. Add BenchmarkDotNet coverage for changed/unchanged mixes, ticker cardinality, and buffer sizes.
7. Implement actor messages and the actor-event producer.
8. Implement the tick manager and tick aggregator actors.
9. Implement query-shaped ScyllaDB persistence and recovery/idempotency behavior.

#### Acceptance criteria

- Every source tick appears exactly once in a changed event or unchanged-data batch at the manager boundary.
- Per-instrument source order is preserved.
- A full unchanged buffer is emitted even when the price never changes.
- Managed memory is bounded and buffers have explicit single-owner lifetime.
- Backpressure is visible and never concealed as loss.
- The production qualification gates in SWO-10 pass.

### SWO-05: ScyllaDB analytics query projections

**Priority:** P1  
**Status:** Complete

#### Objective

Replace the intentionally deferred ITI/RSI and related active signal `ALLOW FILTERING` reads with query-shaped tables.

#### Design requirements

1. Inventory every remaining active filtered query and its caller.
2. Group queries by actual access pattern: contract, symbol, date range, trend, signal type, and ordering requirement.
3. Design partition and clustering keys from those access patterns and expected partition sizes.
4. Use additive schemas, dual writes, idempotent backfill, reconciliation, readiness markers, and canonical fallback.
5. Do not remove canonical data during the rollback window.
6. Include real Scylla query plans, round trips, scanned rows, latency percentiles, and allocation results.

#### Acceptance criteria

- Active application paths execute without `ALLOW FILTERING`.
- Reconciliation proves the new projections match canonical data.
- Negative lookups and empty partitions do not fall back to full scans indefinitely.
- Deployment and rollback are documented and repeatable.

All acceptance criteria are satisfied. Three bounded ITI projections cover contract/day, contract/month, and
contract/trend/mode/month access patterns. Maintained writes and exact deletes use the scoped mutation protocol;
idempotent backfill reconciles independent identity fingerprints before readiness. Production application-storage CQL
contains zero `ALLOW FILTERING` statements. Real-Scylla tracing proves the legacy lookup reads 4,096 or 32,768 rows
while the projection lookup reads one row from one partition. The final sequential Release gate passes all 193 domain
integration tests. Detailed measurements and validation are recorded in `System-Wide-Optimization-Results.md`.

### SWO-06: Event-projector recovery and idempotency

**Priority:** P1  
**Status:** Proposed  

#### Objective

Make projector recovery efficient and ensure retry behavior cannot create uncontrolled duplicate side effects.

#### Current evidence

The projector workflow provides at-least-once side-effect delivery. A process failure after an external action and before its next checkpoint can repeat publication or projection. Stream-aware stale-event supersession is intentionally deferred, and the maximum-attempt path does not currently publish the typed failure event.

#### Design requirements

1. Define durable idempotency keys for every projection and downstream event publication.
2. Add stream version information and projector-specific supersession rules where an older event can be proven stale.
3. Preserve immutable source history and never infer supersession only from missing projection state.
4. Decide whether a terminal maximum-attempt failure must publish a typed failure event.
5. Bound and measure startup recovery batches, replay concurrency, retry delay, and backlog age.
6. Validate crash points between every side effect and checkpoint transition.

#### Acceptance criteria

- Reprocessing the same event produces the same durable projection without duplicate business effects.
- Supersession decisions are deterministic, durable, and reversible through replay.
- Recovery latency and memory are bounded for a defined backlog size.
- Terminal failures are operationally visible and cannot retry forever silently.

### SWO-07: Fund compact snapshots

**Priority:** P1  
**Status:** Proposed  

#### Objective

Preserve unbounded Fund and FundTransaction history while bounding reconstruction cost using complete compatible snapshots and short replay tails.

#### Design requirements

1. Introduce explicit versioned snapshot event types for Fund and FundTransaction state.
2. Store the complete current state required by future commands; do not use an arbitrary last-N slice as a substitute for a complete snapshot.
3. Load the newest compatible snapshot and replay only later events in ascending order.
4. Fall back to the existing creation snapshot or full historical replay for streams written before the new snapshot type.
5. Preserve best-effort empty-state behavior for absent data.
6. Define snapshot cadence from event-tail benchmarks and paper-trading distribution data.
7. Provide schema-version migration and rollback rules.

#### Acceptance criteria

- Reconstruction cost is bounded by snapshot size plus the configured maximum expected tail.
- Historical streams remain readable without rewriting or deleting events.
- State reconstructed before and after snapshotting is behaviorally identical.
- Benchmarks cover at least 32, 256, 2,048, and production-representative tail sizes.

### SWO-08: OptionPricer QLNet allocation isolation

**Priority:** P1  
**Status:** Proposed  

#### Objective

Reduce the approximately 102 MB measured allocation for a four-leg QLNet pricing workflow and reduce the effect of QLNet global-settings serialization.

#### Design requirements

1. Profile the retained and transient QLNet graph by type and allocation source.
2. Benchmark reuse of immutable curves, calendars, handles, engines, and instrument templates independently.
3. Do not share mutable pricing objects across concurrent calculations unless QLNet thread safety is proven.
4. Evaluate isolated pricing-worker or process partitioning if the global `Settings` object prevents safe in-process concurrency.
5. Compare every optimization against the existing numerical outputs across representative expiries, strikes, volatility regimes, and option structures.

#### Acceptance criteria

- Allocation and GC improvements are material and repeatable.
- Pricing results remain within an explicitly approved numerical tolerance.
- Concurrent calculations cannot contaminate evaluation dates, curves, or global settings.
- Additional caching has a bounded lifetime and clear invalidation ownership.

### SWO-09: Event-context cancellation completion

**Priority:** P2  
**Status:** Proposed  

#### Objective

Allow the supervisor to stop genuinely cancellable event-initiated work gracefully without canceling required post-commit work halfway through.

#### Current evidence

Command, query, repository, transport, and storage paths broadly support cancellation. `IEventActorContext` and denormalizer context operations still expose tokenless send/request methods, and some long-running cross-domain event workflows therefore cannot receive an owned shutdown token.

#### Design requirements

1. Classify event-handler work as required post-commit completion or optional/cancellable downstream work.
2. Add token-aware context/API overloads without serializing tokens across NATS.
3. Use the receiving actor's supervisor token for local work; cancellation of a remote requester must not revoke already accepted remote work.
4. Preserve non-cancelable persistence/publication after the documented commit boundary.
5. If required, introduce separately named background-operation ownership rather than changing the meaning of graceful `ShutdownAsync`.
6. Decide separately whether an explicit force-stop operation is required and document its loss semantics.

#### Acceptance criteria

- Long-running cancellable event work stops within a measured shutdown budget.
- Required post-commit effects still reach a terminal state.
- `OperationCanceledException` remains cancellation and is not converted into a domain failure event.
- No cancellation token is serialized or treated as a distributed revocation token.

### SWO-10: Production performance and reliability gates

**Priority:** P0  
**Status:** In progress  

#### Objective

Make optimization results repeatable and prevent regressions across actor, storage, messaging, and Databento changes.

#### Required gates

1. Keep focused BenchmarkDotNet suites for deterministic CPU, allocation, dispatch, replay, and buffer hot paths.
2. Run database benchmarks against the same topology and durability settings used for the comparison baseline.
3. Run all ten domain integration projects as the shared actor-runtime gate.
4. Replace placeholder Application BDD/integration tests with real startup, shutdown, routing, persistence, and rollback cases.
5. Repair infrastructure-dependent domain integration suites so environmental failure is distinguishable from regression.
6. Complete Databento qualification evidence:
   - 1 million records/second sustained;
   - 5 million records/second sustained;
   - 10 million records/second burst;
   - 2x replay load;
   - 30-minute strict pre-production run;
   - 24-hour production soak;
   - zero unexplained loss, ordering errors, handle growth, or post-warm-up allocation on the qualified hot path.
7. Store benchmark environment, commit, configuration, input shape, absolute values, allocation results, and variance with every result set.

#### Acceptance criteria

- Every accepted optimization has before/after evidence or a documented correctness justification.
- CI or the release procedure detects material performance regressions against approved thresholds.
- Paper-trading reports include p50/p95/p99/max latency, throughput, allocation, GC, backlog, storage, and NATS evidence.
- Failed environmental prerequisites are reported separately from product failures.

## 8. Lower-priority and activation-gated work

The following items remain visible but should not displace the prioritized work without new evidence.

### NATS compatibility consumers

The primary JetStream actor-event path uses shared pooled ownership for fan-out. Legacy compatibility event listeners still use `byte[]` payloads. Migrate them only if profiles show meaningful traffic or allocation, or if they become part of the Databento production topology.

### Reference cold lookup load

Normal cached reads are lock-free, but the shared reference lookup interface retains a guarded synchronous cold load. A fully asynchronous redesign requires a coordinated interface and validator-consumer migration. Perform it only if cold-load latency appears in startup or refresh evidence.

### Dormant Trade algorithm path

The inactive `AlgorithmBuilder` now awaits remote inputs before constructing its rule engine. Its dependency-injection registration remains disabled because cache ownership and the incomplete algorithms still require functional work before reactivation.

### Obsolete trade-plan API route

The actor-only trade-plan summary operation is deliberately unsupported pending UI cleanup while a legacy API route remains mapped. Remove or replace that contract during the UI/API refactor rather than optimizing an obsolete path.

### Documentation drift

Some implementation documents still describe issues already fixed by later cancellation, startup, storage, or NATS work. Update affected documents when their component is next changed; do not treat stale narrative as runtime evidence.

## 9. Measurement standards

### BenchmarkDotNet

Use BenchmarkDotNet for isolated CPU and allocation comparisons. Every report must include:

- baseline commit and candidate commit;
- runtime, SDK, operating system, processor, architecture, and GC mode;
- warmup and measurement counts;
- parameter values and input distribution;
- mean plus an appropriate spread/error measure;
- allocated bytes and observed GC generations;
- threading or contention results when relevant;
- absolute results as well as percentage change.

Do not infer database or network improvements from a benchmark that excludes those dependencies.

### Integrated measurements

Use component integration tests for database query plans, transport behavior, cancellation, durability, and recovery. Record topology, consistency level, connection-pool configuration, batch size, dataset size, and environmental failures.

### Paper trading

Paper trading is the authority for end-to-end tuning. Capture normal flow, opening/closing bursts, reconnect and replay, storage latency, external-service delay, and graceful shutdown. Optimize p95/p99 and bounded resource use rather than only average microbenchmark latency.

### Acceptance rule

An optimization is accepted when it provides a material measured benefit, fixes a demonstrated correctness/resource-bound issue, or supplies required operational evidence without unacceptable complexity. A result within benchmark variance is neutral. Rejected experiments remain documented so they are not repeated without new evidence.

## 10. Execution and update workflow

For each work package:

1. Confirm the active baseline and update this document's date.
2. Inspect current code and existing reports; do not assume an older TODO is still valid.
3. Write or update the component-specific implementation plan before high-risk changes.
4. Capture a reproducible baseline before editing production code.
5. Implement one coherent tranche at a time.
6. Add deterministic correctness, concurrency, cancellation, ownership, and failure-path tests as appropriate.
7. Run proportional validation, including the Fund integration gate for shared actor-runtime changes.
8. Capture before/after metrics on the same environment.
9. Update the work-package status, results, unresolved risks, and decision log.
10. Commit with a summarized message only when explicitly requested.

### Pause and resume protocol

When specialized optimization work interrupts this plan:

1. Mark the active system-wide work package `Paused` if it is not complete.
2. Record the last completed checkpoint, uncommitted work, measurements, and exact next action in the active-work record.
3. Add the specialized work to the related component document rather than expanding this plan with every local implementation detail.
4. On return, re-audit the repository because specialized changes may have resolved or reordered system-wide items.
5. Update priorities from current evidence before resuming implementation.

## 11. Active-work record

### Current tranche

**Work package:** SWO-05 ScyllaDB analytics query projections
**State:** Complete
**Implemented scope:** bounded futures ITI day/month/trend-mode projections, maintained writes and exact deletes, month inventory, canonical fallback, idempotent backfill/reconciliation/readiness, operator migration support, zero application-storage `ALLOW FILTERING`, and a real-Scylla before/after benchmark
**Validation recorded:** 16/16 CQL/binding/projection policy tests, 58/58 real-Scylla MarketData storage tests, 25/25 Market Data Analytics integration tests, a zero-warning serial Release solution build, and the complete ten-domain sequential Release gate at 193/193; detailed traces and benchmark percentiles are recorded in `System-Wide-Optimization-Results.md`
**Next action:** SWO-04 remains paused pending its recorded Databento schema/design decisions; the next independently actionable package is SWO-06 event-projector recovery and idempotency

This record is intentionally temporary and must be updated after the tranche is committed, revised, or abandoned.

## 12. Decision log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-08-07 | Treat this as a living specification rather than a fixed project plan. | Specialized optimization work will interrupt and inform system-wide priorities. |
| 2026-08-07 | Exclude the legacy Interactive Brokers feed from optimization. | Databento is the replacement and future IBKR work will mirror the proven architecture. |
| 2026-08-07 | Preserve empty event actors. | They are intentional default same-domain event targets. |
| 2026-08-07 | Preserve command-success versus state-change semantics. | They represent different domain concepts. |
| 2026-08-07 | Keep immutable event histories unbounded. | Reconstruction and query cost will be bounded through snapshots and projections instead of event deletion. |
| 2026-08-07 | Defer dispatch jump tables and similar micro-optimizations. | Existing measurements do not justify their complexity. |
| 2026-08-07 | Use Fund integration tests as the current shared-runtime gate. | Other integrated suites are not yet uniformly reliable, but specialized validation remains required. |
| 2026-08-09 | Promote all ten domain integration projects to the shared-runtime gate. | The repaired suites pass 193 of 193 tests in one sequential Release confirmation run. |
| 2026-08-09 | Keep SWO-01 in Measuring after code completion. | OTLP, instrumentation, tests, and the microbenchmark are complete, but paper-trading p95/p99 attribution still requires a production-like collector. |
| 2026-08-09 | Implement SWO-02 Tranche A without enforcement. | Capacity values require production-like traffic and an approved memory budget; observe-only instrumentation gathers that evidence without changing admission behavior. |
| 2026-08-09 | Implement Tranche B runtime enforcement without enabling it in production. | Runtime safety and performance can be proven with deterministic test limits while real capacities and transport-specific overload policies remain pending. |
| 2026-08-09 | Implement Tranche C while keeping production in `ObserveOnly`. | Typed replies and durable redelivery can be proven now, but the audited required non-durable Command/Supervisor traffic must block enforcement until migrated. |
| 2026-08-09 | Complete Tranche D locally without changing production mode. | Required Command/Supervisor routes are resolved and enforced behavior is verified, but capacity selection still requires production-like memory and traffic evidence. |
| 2026-08-09 | Retain Channel as the actor-mailbox default while preparing an optimized MPSC production trial. | The optimized ring is 6.4% faster with one producer, 27.3% faster with eight producers, and 11.8% faster in the scheduled round trip; the four-producer result is 3.5% faster but statistically overlapping. It still allocates 320.94 KB per 8,192-slot mailbox versus 2.13 KB for Channel, so high-cardinality and end-to-end evidence is required before changing production configuration. |
| 2026-08-09 | Recommend the topology-aligned SPSC mailbox for production, but retain Channel and capacity 8,192 in checked-in production configuration until the paper-trading or initial-production review. | SPSC is 72.4% faster than Channel in the scheduled round trip, 71.8% faster in the scheduled burst, and 66.5% faster in the dedicated single-producer batch. The activation gate must confirm the single-producer stripe invariant, full-pipeline latency, retained memory, and high-cardinality mailbox behavior under production-like traffic. Capacity 65,536 remains experimental. |
| 2026-08-09 | Use actor-first startup and open Core/JetStream consumers only after every actor is initialized. | Actor startup has no dependency on external consumer intake; holding intake closed prevents premature request handling and lets JetStream retain durable events until the runtime is safe. |
| 2026-08-09 | Complete SWO-05 with bounded month-based ITI projections while retaining canonical ITI data for fallback and rollback. | Real-Scylla tracing shows one projected row read instead of 4,096 or 32,768 canonical rows, with 88.64% and 98.27% lower measured mean latency; scoped readiness and reconciliation preserve correctness during migration. |

## 13. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-07 | Created the living system-wide optimization plan from the completed domain reviews and remaining infrastructure priorities. |
| 0.2 | 2026-08-09 | Recorded SWO-01 implementation and measurement status, promoted the complete domain integration gate, and linked the optimization results record. |
| 0.3 | 2026-08-09 | Added the detailed SWO-02 aggregate backlog and overload-control plan for review. |
| 0.4 | 2026-08-09 | Recorded SWO-02 Tranche A contracts, observe-only accounting, configurable capacities, benchmark gate, and remaining capacity approval evidence. |
| 0.5 | 2026-08-09 | Recorded SWO-02 Tranche B atomic enforcement, queue lifecycle safety, benchmark results, and the remaining transport-policy gate. |
| 0.6 | 2026-08-09 | Recorded SWO-02 Tranche C Core classification, typed overload replies, delayed JetStream redelivery, transport metrics, and the remaining rollout gate. |
| 0.7 | 2026-08-09 | Recorded SWO-02 Tranche D route migration, local saturation evidence, full regression confirmation, and the remaining production measurement and activation gate. |
| 0.8 | 2026-08-09 | Recorded the pluggable MPSC ring mailbox experiment and the benchmark-supported decision to retain Channel as default. |
| 0.9 | 2026-08-09 | Recorded the optimized MPSC implementation and isolated-process benchmark evidence supporting a future production trial. |
| 0.10 | 2026-08-09 | Recorded the topology-aligned SPSC mailbox, capacity comparison, and production rollout requirements. |
| 0.11 | 2026-08-09 | Marked SPSC as the recommended production implementation while retaining Channel as the active selection until paper-trading or initial-production validation. |
| 0.12 | 2026-08-09 | Added direct logical actor-worker capacity, occupancy, availability, and utilization observability for the paper-trading saturation dashboard. |
| 0.13 | 2026-08-09 | Completed SWO-03 with actor-first initialization, host readiness publication, rollback-safe failure behavior, and deterministic lifecycle verification. |
| 0.14 | 2026-08-09 | Completed SWO-05 with bounded ITI projections, real-Scylla trace and benchmark evidence, migration documentation, and the complete regression gate. |

## 14. Related documents

- `docs/Solution-Wide-Graceful-Cancellation-Implementation-Details.md`
- `Documents/system/System-Wide-Optimization-Results.md`
- `Documents/system/Aggregate-Actor-Backlog-Overload-Control-Implementation-Plan.md`
- `Documents/system/Actor-Backlog-Capacity-Worksheet.md`
- `docs/Domain-MarketData-Actor-Optimization-Report-2026-08.md`
- `TomasAI.IFM.Framework.Messaging.Nats/PERFORMANCE.md`
- `TomasAI.IFM.Framework.Messaging.Nats.Benchmarks/RESULTS.md`
- `TomasAI.IFM.Framework.Storage/Docs/Storage-Performance-Top-10.md`
- `TomasAI.IFM.Application.Storage/Docs/Scylla-Allow-Filtering-Migration.md`
- `TomasAI.IFM.Application.Storage.Benchmarks/RESULTS.md`
- `TomasAI.IFM.Framework.MarketData.DataBento/Docs/Databento_Market_Data_Specification_v1.1.md`
- `TomasAI.IFM.Framework.MarketData.DataBento/Docs/Phase6_Implementation.md`
- `TomasAI.IFM.Framework.MarketData.DataBento/Docs/Tick_Price_Event_Pipeline_Specification_v0.1.md`
- each optimized domain's `Docs/Domain-Actor-Optimization-Details.md` and benchmark `RESULTS.md`.
