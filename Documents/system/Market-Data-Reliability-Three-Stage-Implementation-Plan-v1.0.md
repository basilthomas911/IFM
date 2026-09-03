# Market Data Reliability Three-Stage Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Plan ID | `MDR` |
| Status | Stage 1 complete; Stage 2 implementation complete, elapsed soak and acceptance pending |
| Date | 2026-09-01 |
| Design authority | `Documents/system/Databento-Market-Data-Service-Resiliency-System-Design-v0.1.md` |
| Stage 1 | Local Market Outlook update processor |
| Stage 2 | Databento lifecycle and resiliency refactor |
| Stage 3 | Central market-data operations-health service and dataset process containment |
| Deployment now | API Server hosted |
| Deployment later | Dedicated Aspire Market Data service |

## 1. Objective

Implement market-data reliability in three independently reviewable stages. Each stage must be
completed, tested and accepted before the next stage begins. The sequence provides immediate Market
Outlook single-writer correctness, then resilient Databento lifecycle ownership, and finally one
central end-to-end operational-health view with supervised per-dataset process containment.

The stages are intentionally ordered:

1. create the local Market Outlook processing boundary without changing Databento lifecycle;
2. create the authoritative Databento lifecycle, watchdog, persistence and recovery boundary; and
3. aggregate every stage's already-instrumented measurements into the central operations-health
   service and UI, then contain hard-reset escalation within the affected dataset worker process.

## 2. Binding implementation rules

1. `MarketOutlookUpdate` and every derived update are local in-process objects. They are not actor
   messages and never traverse NATS.
2. One `Channel<MarketOutlookUpdate>` has multiple writers and exactly one reader.
3. `MarketOutlookUpdateProcessor` is the only production cache writer and Market Outlook snapshot
   publisher.
4. Market Outlook queries read complete immutable snapshots without an application-level lock.
5. Source actors and historical loaders are adapters/producers; none directly mutate the cache.
6. Market-data and analytics producers never wait for Market Outlook composition or UI delivery.
7. Queue saturation, coalescing, processing failure and publication failure are explicit; no loss is
   silent.
8. Databento lifecycle remains unchanged during Stage 1.
9. `DatabentoMarketDataWatchdogService` becomes the only Databento lifecycle owner during Stage 2.
10. C++ and Rust remain equal implementations of one native ABI and must change together.
11. A minimal operational recording contract is defined in Stage 1. Stage 1 records Market Outlook
    measurements and Stage 2 instruments Databento through that boundary. Stage 3 supplies the full
    central aggregation, evaluation, queries, UI and supervised dataset-process containment.
12. Metric dimensions are bounded. Update, command and correlation GUIDs belong in diagnostic
    records/traces and never become metric labels.
13. Operational metric recording cannot throw into or synchronously block the market-data path.
14. On-demand refresh delegates to the owning stage. The health service observes and routes refresh;
    it never steals lifecycle or calculation ownership.
15. Market Outlook and current operational metrics remain process-local and rebuildable. The derived
    Market Outlook snapshot is not persisted.
16. Existing Market Outlook latest-arrival, unconditional partial-write, OR-composition and
    whole-snapshot-read semantics remain authoritative.

## 3. Execution controls

- Only one stage may be `In progress` at a time.
- No Stage 2 production change begins until all Stage 1 exit criteria pass.
- No Stage 3 production change begins until all Stage 2 exit criteria pass.
- A stage may add compatibility interfaces needed by a later stage, but it must not partially enable
  later-stage runtime behavior.
- Every gate uses failing-first characterization where behavior is being changed.
- Every stage ends with a clean build, focused regression suites, full affected-project suites,
  runtime verification, documentation update and explicit completion evidence.
- Unrelated baseline failures must be identified with evidence; they cannot be counted as successful
  stage tests.

## 4. Verified starting baseline

The current implementation has these relevant properties:

- `MarketOutlookSnapshotRealtimeActor` directly mutates the cache for component, EOD and ES-trade
  updates.
- `FuturesEmaBbHistoricalDailyReplayPublisher` independently performs a direct cache write.
- `MarketOutlookHotCache` serializes partial writes with one application `lock` and publishes
  immutable snapshot references for lock-free reads.
- Current Market Outlook metrics are aggregate totals and cannot distinguish RSI, TDI, ITI, EMA,
  Bollinger, ES, VX, EOD or trade-signal progress.
- Databento exposes an interim synchronous up/down probe and aggregate runtime counters, but the full
  persisted watchdog, three-attempt recovery and single lifecycle owner are not implemented.
- Operational measurements are fragmented; no single query explains progress from native receipt
  through UI publication.

## 5. Stage 1 — Local Market Outlook update processor

### Stage 1 scope

Replace every direct Market Outlook cache write with a strongly typed local update sent through one
MPSC channel. Add the single-reader hosted processor, lock-free immutable cache reads, independently
addressable Market Outlook metrics and on-demand recompose/republish. Do not change native
Databento lifecycle, watchdog persistence, current-contract authority or recovery behavior.

### MOUP-01 — Baseline and architecture enforcement

Deliverables:

- inventory every production cache writer, query reader and notification publisher;
- capture current synchronous and concurrency behavior with characterization tests;
- add an architecture test that will prohibit direct production cache mutation outside the new
  processor; and
- record current throughput, allocation and notification behavior for comparison.

Exit verification:

- all existing writers and readers are accounted for;
- failing-first tests demonstrate that more than one production path currently owns cache writes;
- existing Market Outlook unit, BDD, integration and UI baselines are recorded.

### MOUP-02 — Typed local update contracts

Deliverables:

- add `MarketOutlookUpdateKind`;
- add abstract `MarketOutlookUpdate` with update ID, entity ID, receipt time, market-data-as-of time
  and diagnostic correlation context;
- add strongly typed derived updates for RSI, TDI, ITI, EMA, Bollinger, ES trade, VX, EOD, Futures
  Trade Signal, feed health, historical warmup and recompose/republish; and
- ensure the contracts implement no command, event, query or actor-message interface.

Exit verification:

- every derived update maps to one stable kind;
- every payload owns an explicit, tested set of Market Outlook input fields;
- compile-time tests verify strong payload typing;
- architecture tests prove local updates do not enter NATS serialization/registration catalogs.

### MOUP-03 — Channel and writer boundary

Deliverables:

- add `IMarketOutlookUpdateWriter` and one singleton channel implementation;
- configure multiple writers and one reader;
- define bounded capacity and explicit overload behavior;
- expose readiness, depth, oldest-item age and accepted/saturation counters; and
- prevent producer-facing APIs from exposing the channel reader.

Exit verification:

- concurrent producer tests preserve every accepted update;
- saturation is reported without silently losing the latest state;
- cancellation and shutdown do not manufacture operational exceptions;
- producer calls do not wait for composition or UI publication.

### MOUP-04 — Single-reader update processor

Deliverables:

- add API-hosted `MarketOutlookUpdateProcessor`;
- pattern-match and apply every derived update;
- isolate one malformed update without terminating the reader loop;
- establish processor Ready before releasing producers; and
- implement bounded graceful drain with explicit undrained count at shutdown.

Exit verification:

- only one update is inside merge/compose/publication sequencing at a time;
- mixed concurrent producers converge to the expected complete state;
- fault injection proves processing continues after a failed update;
- startup and shutdown ordering are deterministic under `FakeTimeProvider` or controlled fixtures.

### MOUP-05 — Sole-writer immutable cache

Deliverables:

- remove the application cache-write lock after all writes are processor-owned;
- make the mutation API internal to the processor boundary;
- atomically replace complete immutable input and display references;
- retain lock-free whole-snapshot reads by contract/value-date identity; and
- preserve latest-arrival and OR-composition behavior without generation or source-order admission
  fences.

Exit verification:

- no production caller except the processor can mutate cache state;
- readers observe a complete before-or-after snapshot and never torn state;
- every non-empty reasonable component-availability combination remains valid;
- repeated, older-timestamp and cross-generation arrivals remain accepted as routed latest arrivals.

### MOUP-06 — Producer migration

Deliverables:

- convert component, EOD and ES-trade paths in `MarketOutlookSnapshotRealtimeActor` into local update
  producers;
- convert historical EMA/Bollinger warmup into a local update producer;
- add feed-health and future local-producer adapters without direct cache access; and
- preserve actor subjects and external notification/query DTOs.

Exit verification:

- RSI, TDI, all four ITI modes, EMA, Bollinger, ES, VX, EOD, Futures Trade Signal and warmup each
  reach the processor independently;
- missing siblings never block a valid update;
- historical warmup works before, during and after live-feed startup;
- no update is serialized through NATS merely to cross the local channel.

### MOUP-07 — Snapshot publication and failure isolation

Deliverables:

- add `IMarketOutlookSnapshotPublisher` around the existing UI notification mechanism;
- publish only after atomic cache replacement;
- distinguish cache commit from notification success;
- retain the committed snapshot after publication failure; and
- preserve update/correlation identity through diagnostics.

Exit verification:

- cache queries equal the snapshot carried by successful notifications;
- notification failure increments failure metrics and leaves the cache readable;
- a slow or failed UI publication cannot stop input production or the processor loop;
- publication recovery requires no cache rebuild.

### MOUP-08 — Market Outlook measurements and refresh

Deliverables:

- define the shared minimal `IMarketDataOperationsRecorder` contract required by all three stages;
- implement Stage 1 Market Outlook received, enqueued, applied, changed, composed, published, failed
  and coalesced counters by update kind;
- record last activity, market-data-as-of time, queue depth, queue/processing/publication latency and
  last update ID as bounded diagnostic detail;
- add a local metrics snapshot for Stage 1 verification; and
- implement recompose/republish from current inputs without fabricating source values.

Exit verification:

- counters reconcile exactly across success, no-change, failure and publication-failure cases;
- per-kind metrics identify a deliberately stopped TDI producer while other kinds remain healthy;
- metric recording cannot throw into the producer/processor;
- on-demand refresh republishes current values and retains missing/stale availability accurately.

### MOUP-09 — Qualification suites

Required tests:

- unit tests for contracts, payload ownership, merge, composition, metrics and failure isolation;
- BDD tests for partial updates, OR semantics, refresh and unavailable/stale behavior;
- integration tests for actor-to-local-channel-to-cache-to-notification flow;
- concurrency tests with simultaneous component, ES and warmup writers;
- backlog/saturation and sustained burst tests;
- runtime restart, graceful drain and publication-failure tests;
- UI/system tests proving every component independently refreshes its displayed values; and
- architecture tests proving sole-writer and local-only update rules.

Exit verification:

- all affected test projects pass;
- no first-chance exception is used for expected missing, duplicate, delayed, shutdown or saturation
  behavior;
- throughput and allocation remain within the recorded baseline budget;
- live ES processing updates Market Outlook and all available composite values.

### MOUP-10 — Stage 1 acceptance boundary

Stage 1 is complete only when:

1. the processor is the only production cache writer and publisher;
2. the cache has no application-level write lock and queries remain lock-free;
3. every production input uses a derived local update;
4. per-kind counters and latency are queryable in the Stage 1 snapshot;
5. on-demand recompose/republish works;
6. Databento lifecycle behavior is unchanged;
7. all Stage 1 qualification suites pass; and
8. the user accepts Stage 1 evidence before Stage 2 begins.

## 6. Stage 2 — Databento lifecycle and resiliency refactor

### Stage 2 entry criteria

- MOUP-01 through MOUP-10 are complete and accepted.
- The local update processor is stable under live and burst verification.
- The Stage 1 operations recorder contract is frozen for compatible Stage 2 instrumentation.

### DBR-01 — Resiliency baseline and lifecycle inventory

- map every current start, stop, reset, rollover, feed acquisition and worker ownership path;
- add architecture tests identifying unauthorized direct lifecycle mutation;
- capture C++/Rust ABI, live-session and failure-injection baselines.

### DBR-02 — PostgreSQL service authority

- add `MarketDataServiceDbContext` and migrations;
- implement full CRUD for `futures_rollover_contract_assignment` and `watchdog_status_log`;
- require ES quarterly, VX front and VX second roles;
- validate copied contracts against the read-only source catalog;
- use sequence-generated integer IDs where required and transactional VX-pair updates.

### DBR-03 — Current-contract startup and rollover

- make PostgreSQL assignments authoritative;
- implement startup rollover and validation;
- preserve the source futures-contract catalog unchanged;
- define the future/on-demand rollover adapter without bypassing lifecycle serialization.

### DBR-04 — Native C++ bulk health ABI

- implement one-call enumeration of every started/current feed;
- expose Up/Resetting/Down, heartbeat, terminal state, subscriptions, counters, ring occupancy and
  failure detail;
- guarantee destruction/enumeration safety and bounded buffers.

### DBR-05 — Native Rust ABI and behavioral parity

- implement the identical ABI, structs, enums, exports and semantics in Rust;
- update the shared capability manifest and managed comparison suite;
- require deterministic lifecycle, record, failure and performance parity evidence.

### DBR-06 — Managed wrapper and runtime registry

- consume the bulk ABI through one selected backend;
- join native status with contract roles and managed workers;
- expose non-throwing typed status and record native/interop/aggregation measurements through the
  shared recorder contract.

### DBR-07 — Single lifecycle owner and startup qualification

- implement `DatabentoMarketDataWatchdogService` as the only lifecycle owner;
- convert existing callers to request adapters;
- qualify contracts, native feeds, subscriptions, workers, routes and hot caches before Ready;
- run the one-minute watchdog throughout active value-date hours.

### DBR-08 — Recovery, reset and failure policy

- implement Orange Resetting and exactly three bounded recovery attempts;
- stop all feeds and latch Red after exhausted core failure;
- isolate exhausted optional failure and retain Orange with core feeds running;
- serialize scheduled start, rollover, poll, recovery, manual reset, stop and shutdown;
- persist and publish every recovery transition.

### DBR-09 — Readiness, actor contracts and watchdog history

- implement typed commands/events/queries for contracts, lifecycle, readiness and history;
- keep native polling out of query handlers;
- expose clickable persisted observation/detail data;
- enforce System-only navigation for unexpected core NotReady while preserving planned-closure
  read-only behavior.

### DBR-10 — Refresh ownership

- register immediate native watchdog probe and managed worker/route re-evaluation handlers;
- keep feed reset as an explicit authorized lifecycle operation;
- report refresh requested, started, completed and failed through the shared recorder;
- prohibit Market Outlook refresh from invoking Databento recovery.

### DBR-11 — Qualification and soak

Required evidence:

- unit, PostgreSQL integration, actor/NATS integration and UI tests;
- C++ and Rust native unit, ABI, parity and fault-injection suites;
- connection loss, heartbeat timeout, slow reader, terminal fault and worker-completion recovery;
- exactly three recovery attempts under deterministic time;
- rollover/reset/poll concurrency and idempotency;
- complete active-session, overnight and repeated restart runs;
- CPU, allocation, managed/native memory, handle, ring, backlog and P/Invoke latency measurements.

### DBR-12 — Stage 2 acceptance boundary

Stage 2 is complete only when:

1. PostgreSQL authoritatively supplies all three required current contracts;
2. only the watchdog service mutates Databento lifecycle;
3. one bulk P/Invoke reports every feed for either native backend;
4. C++ and Rust parity passes;
5. the watchdog persists every active-session minute and reacts immediately to known terminal
   faults;
6. three-attempt core recovery and optional-feed isolation behave exactly as designed;
7. Stage 2 instrumentation is visible through its local recorder snapshots;
8. all qualification and soak evidence passes; and
9. the user accepts Stage 2 evidence before Stage 3 begins.

## 7. Stage 3 — Central market-data operations-health service and dataset process containment

### Stage 3 entry criteria

- MOUP and DBR stages are complete and accepted.
- Market Outlook and Databento stages already report through the shared recording contract.
- Stage/contract/update-kind metric dimensions are known and bounded.

### MDOH-01 — Central contracts and status model

- finalize `MarketDataOperationStage`, update-kind, contract/feed identity and refresh-target enums;
- define immutable current snapshot and per-stage detail DTOs;
- define Green, Yellow, Orange, Red and Inactive evaluation inputs and bounded reasons.

### MDOH-02 — Independent central registry

- implement `MarketDataOperationsHealthService` independently of the watchdog and Market Outlook
  processors;
- use atomic counters and immutable snapshot publication;
- perform no database/network I/O while recording;
- remain queryable when any monitored worker is paused or failed.

### MDOH-03 — End-to-end instrumentation adapters

- connect native connection/heartbeat, interop, normalization, aggregation and event publication;
- connect RSI, TDI, ITI, EMA, Bollinger, VX, EOD and Futures Trade Signal;
- connect Market Outlook queue, processor, cache and notification;
- connect UI delivery acknowledgements when available;
- verify counters reconcile from source receipt to UI publication.

### MDOH-04 — Health evaluation and latency

- calculate freshness, backlog, failure and recovery status per stage and active contract;
- retain session-aware market-data thresholds;
- distinguish quiet markets from failed transport;
- expose queue, processing, publication and end-to-end latency distributions.

### MDOH-05 — Refresh coordinator

- register typed handlers for every operational stage;
- route refresh to the existing owner without duplicating lifecycle/calculation logic;
- observe requested, started, completed and failed outcomes;
- keep requery, recompose, recalculate, worker re-evaluation and authorized reset semantics distinct.

### MDOH-06 — Queries, API and actor boundary

- add current combined-health and stage-detail queries;
- add typed refresh commands and completion/failure events where an external boundary is required;
- ensure queries read immutable current state and never call native code;
- retain local refresh/update objects inside the process.

### MDOH-07 — Operational UI

- make feed/operations health clickable;
- show overall status, per-stage/component rows, counts, timestamps, freshness, backlog and latency;
- show selected-stage diagnostic detail and last bounded correlation/update identity;
- support status refresh and authorized stage refresh separately;
- preserve existing market-hours navigation rules.

### MDOH-08 — Persistence boundary

- keep high-frequency current metrics process-local;
- include only bounded operational summaries in persisted watchdog observations;
- define, but do not automatically enable, future sampled time-series retention/export;
- prevent unbounded labels, payloads or errors from entering PostgreSQL or telemetry exporters.

### MDOH-09 — Supervised per-dataset process containment

- host each active Databento dataset generation in its own supervised worker process; the process
  owns that generation's native handle, producer, managed drain, channels, aggregation worker and
  cancellation source;
- keep `DatabentoMarketDataWatchdogService` as the sole health and reset authority; the process
  supervisor executes its escalation decision but does not independently decide to reset data;
- attempt the bounded cooperative Stage 2 teardown first, then terminate only the affected dataset
  worker when it fails to quiesce within the hard-reset deadline;
- fence every worker generation at publication and hot-cache ingress so a terminated or stale
  generation cannot publish, mutate current state or overlap its replacement;
- expose process identity, generation, exit reason, graceful-stop outcome, forced-termination count,
  restart count and post-restart qualification through the central operations-health snapshot;
- prove that terminating one dataset worker leaves the API/Core host, the health authority and every
  healthy dataset worker running; and
- treat later Aspire Market Data Feed extraction as orchestration of this established boundary, not
  as a second lifecycle or reset authority.

### MDOH-10 — End-to-end qualification

Required evidence:

- unit tests for every counter, latency, status and refresh outcome;
- integration tests reconciling native-to-UI stage progress;
- failure injection at every monitored boundary;
- proof that the health service remains queryable when each worker is independently stopped;
- saturation, high-cardinality guard, metrics-recorder failure and exporter failure tests;
- graceful-stop timeout and forced process-termination tests for each dataset, including proof that
  no stale generation publishes after replacement and that unaffected datasets do not restart;
- runtime and UI journeys identifying a deliberately stopped RSI, TDI, aggregation, publication or
  native stage without log reconstruction.

### MDOH-11 — Stage 3 and plan acceptance

The complete plan is accepted only when:

1. one combined immutable snapshot describes native-to-UI operational health;
2. every composite signal is individually addressable by counters, freshness and latency;
3. the system identifies the exact stopped or backlogged stage;
4. monitoring remains operational when the Market Outlook processor or watchdog is faulted;
5. every registered stage is refreshable through its authoritative owner;
6. metric collection cannot block or fail the market-data path;
7. no loss, saturation, coalescing or recovery is silent;
8. an unresponsive dataset generation is forcibly terminated and replaced without restarting the
   API/Core host or an unaffected dataset;
9. no old and replacement dataset generations can concurrently publish or mutate current state;
10. all BDD, unit, integration, verification, runtime, UI and soak suites pass; and
11. documentation contains final evidence and no partial gates remain.

## 8. Cross-stage verification matrix

| Verification | Stage 1 | Stage 2 | Stage 3 |
| --- | ---: | ---: | ---: |
| Unit tests | Required | Required | Required |
| BDD/behavior tests | Required | Required | Required |
| Storage integration | Existing regression | Required | Watchdog-summary regression |
| Actor/NATS integration | Adapter/notification | Lifecycle/readiness/history | Health queries/refresh |
| Native C++ tests | Baseline only | Required | Regression |
| Native Rust tests | Baseline only | Required | Regression |
| C++/Rust parity | Baseline only | Required | Regression |
| Concurrency/backlog | Required | Required | Required |
| Failure injection | Required | Required | Required |
| Runtime/live verification | Required | Required plus soak | Required end-to-end |
| UI/system verification | Market Outlook refresh | Readiness/history | Operations dashboard |
| Architecture tests | Sole cache writer | Sole lifecycle owner | Independent health authority |

## 9. Stage hand-off evidence

At each stage boundary record:

- commit ID and affected projects;
- completed gate list;
- build results;
- test projects, filters, pass/fail/skip counts and durations;
- runtime/live scenarios and timestamps;
- throughput, latency, backlog and resource measurements;
- expected unavailable external dependencies;
- unrelated baseline failures with evidence;
- documentation changes; and
- explicit user acceptance before starting the next stage.

## 10. Stage 1 execution record - 2026-09-01

### 10.1 Gate status

| Gate | Status | Recorded evidence |
| --- | --- | --- |
| `MOUP-01` | Complete | The two production writers (realtime actor and historical EMA/BB replay), three direct query locations and the notification owner were inventoried. Characterization, atomic-reader and architecture tests preserve the baseline behavior while prohibiting a second production mutation owner. |
| `MOUP-02` | Complete | Twelve strongly typed local update records cover RSI, TDI, ITI, EMA, Bollinger, ES trade, VX, EOD, trade signal, feed health, warmup and recompose. Reflection tests prove they implement no actor command/event/query/message contract. |
| `MOUP-03` | Complete | One singleton bounded `Channel<MarketOutlookUpdate>` is MPSC/single-reader. Capacity is 8,192; overflow explicitly retains and measures the latest update per entity/kind. Readiness, pending depth, oldest pending time, receipt, enqueue and coalescing are queryable. Telemetry failure cannot escape submission. |
| `MOUP-04` | Complete | `MarketOutlookUpdateProcessor` is API-hosted, processes one update at a time, isolates malformed inputs, becomes Ready on execution, accounts for in-flight work and performs a bounded five-second graceful drain before host cancellation. |
| `MOUP-05` | Complete | The application write lock was removed. The processor is the sole production `IMarketOutlookHotCacheWriter`; each identity atomically replaces one immutable input/display pair and queries use lock-free whole-reference reads. Latest-arrival and all 127 non-empty availability combinations remain qualified. |
| `MOUP-06` | Complete | Component, EOD and ES-trade actor paths and historical EMA/Bollinger warmup now only submit typed local updates. Actor subjects and public NATS DTOs are unchanged; all four ITI languages and missing-sibling OR behavior remain covered. |
| `MOUP-07` | Complete | `IMarketOutlookSnapshotPublisher` wraps the existing actor notification. Publication occurs after cache commit; injected publication failure increments metrics, retains queryable state and does not stop the processor. |
| `MOUP-08` | Complete | The minimal shared operations-recorder boundary and per-kind received/enqueued/applied/changed/composed/published/failed/coalesced counters are implemented with last activity, market-data-as-of, update ID, queue depth/age and queue/processing/publication latency. Recompose republishes current state without fabricating source time or a changed count. |
| `MOUP-09` | Complete | Unit, BDD, integration, concurrency, saturation, graceful-shutdown, publication-failure, architecture, UI presentation/system and runtime-host qualification all pass. |
| `MOUP-10` | Complete | The technical acceptance boundary is satisfied and this evidence is recorded. Databento lifecycle code was not changed. Stage 2 remains unstarted and requires explicit user direction. |

### 10.2 Automated qualification

| Suite | Result |
| --- | --- |
| MarketData Analytics unit | 988 passed after Stage 1 additions |
| MarketData Analytics BDD | 478 passed |
| MarketData Analytics integration | 50 passed |
| Application MarketData unit | 93 passed |
| Market Outlook UI presentation | 3 passed |
| Market Outlook UI system | 6 passed |
| API Server build | succeeded with 0 warnings and 0 errors |
| Focused processor/warmup unit qualification | 46 passed |
| Focused channel-to-cache-to-notification integration | 2 passed |

The first complete Analytics integration attempt exposed a missing Stage 1 registration in its
embedded API host. The fixture was corrected to use the same singleton channel, processor, cache,
publisher, metrics and hosted-service wiring as production; the complete suite then passed 50/50.

### 10.3 Concurrency and resource evidence

- Two simultaneous producer tasks submitted 1,000 accepted updates; all 1,000 were applied and
  published with sibling state retained.
- The capacity-one saturation test retained the bounded-lane update plus the latest overflow value,
  explicitly reporting both diverted submissions.
- The isolated sustained-burst test applied and published 10,000/10,000 updates in 160.8 ms,
  approximately 62,198 updates/second, with 29,703,464 bytes allocated and zero pending work.
- Concurrent lock-free readers observed only complete immutable snapshots during 2,001 sequential
  writes, and the 10,000-live-preview verification retained immutable Daily accumulator state.

### 10.4 Runtime and live evidence

At 18:32-18:34 Toronto time, the real Development API Server was started on an isolated port with
the Synthetic data source and historical acquisition disabled. Readiness was `Healthy`, all 125
actors started, both configured market routes were running and publication/processing failures were
zero. Two typed Market Outlook queries 500 ms apart advanced `UpdatedAtUtc` and ES close from
`100.775` to `100.781`, proving the hosted actor-to-local-channel-to-processor-to-query path was
live. The host then shut down through its normal cancellation path.

At 18:47-18:48 Toronto time, a second isolated host used the configured `DatabentoLive` source.
The native feed was up and running, the host had received 1,784 source trade records, ES source and
accepted-cache timestamps were current, and both processing and publication failures were zero.
The typed Market Outlook remained valid, its ES close advanced from `7644.25` to `7644.5`, and its
`UpdatedAtUtc` continued advancing through `2026-09-01T22:48:31.9849576Z`. Overall readiness was
`Degraded` only because the current VX contract had no accepted off-hours update within fifteen
minutes; the ES live path and the Stage 1 processor were operating. The isolated live host was then
shut down normally.

## 11. Stage 2 execution record - 2026-09-02

### 11.1 Gate status

| Gate | Status | Recorded evidence |
| --- | --- | --- |
| `DBR-01` | Complete | Lifecycle start, stop, reset, rollover, feed acquisition and worker ownership were inventoried. `Test-DatabentoLifecycleOwnership.ps1` passes and prohibits direct production lifecycle mutation outside the watchdog/runtime boundary. Synthetic, live-feature-build and fault-injection baselines are recorded below. |
| `DBR-02` | Complete | `MarketDataServiceDbContext` and schema-managed migrations provide full assignment and observation CRUD. PostgreSQL integration proves source-catalog fingerprint validation, all three required roles, sequence IDs, JSONB history, optimistic concurrency and atomic VX pair rollback. The read-only futures catalog is not mutated. |
| `DBR-03` | Complete | Startup reconciles PostgreSQL-authoritative ES quarterly, VX front and VX second assignments, publishes only the committed role set into the runtime registry, is idempotent and performs value-date rollover through the serialized lifecycle owner. |
| `DBR-04` | Complete | C++ ABI v3 exposes a bounded one-call process registry snapshot containing lifecycle state, heartbeat/provider activity, subscriptions, terminal state, counters, ring occupancy and bounded failure detail. Registry enumeration and destruction are synchronized. Native tests pass in synthetic and live-enabled builds. |
| `DBR-05` | Complete | Rust implements the same ABI, export set, layout and behavioral semantics. The capability manifest and managed comparison suite validate the frozen surface, lifecycle, records, historical data, watchdog results and bounded repeated polling/restarts. Synthetic and live-enabled Rust tests pass. |
| `DBR-06` | Complete | The managed wrapper uses one explicitly configured `Cpp` or `Rust` backend, joins the native registry with committed contract roles and managed epoch/worker/cache state, returns non-throwing typed snapshots, and records distinct native, interop and aggregation stages. |
| `DBR-07` | Complete | `DatabentoMarketDataWatchdogService` is the sole lifecycle owner. Contracts, native feeds, subscriptions, aggregation workers, configured/running contracts and the last-price cache must qualify before Ready. A hosted one-minute poll runs for active value dates. |
| `DBR-08` | Complete | Resetting is Orange, core recovery is exactly three serialized attempts followed by stop-and-latched Red, optional failures remain isolated Orange, terminal worker completion signals an immediate out-of-cycle probe, and transition observations are retried, persisted and published. |
| `DBR-09` | Complete | Existing typed lifecycle commands/events are routed into the lifecycle request boundary. New typed readiness, current-contract and history queries work over NATS and REST without native polling. The UI exposes clickable detail, fences navigation to System for unexpected core failure, and preserves planned-closure read-only access. |
| `DBR-10` | Complete | Refresh performs an immediate serialized probe and managed qualification without an implicit reset, records requested/started/completed/failed outcomes, and remains independent from Market Outlook recompose. |
| `DBR-11` | Pending elapsed run | All automated, PostgreSQL, native, parity, fault, concurrency, UI and accelerated 24-hour/restart qualifications pass. The repository contains valid C++ and Rust market-close soak launchers, but the checked-in 2026-08-15 artifacts are preflight-only. A real complete active-session/overnight run has not yet elapsed and is not represented as passed. |
| `DBR-12` | Pending | Technical criteria 1-7 pass. Criterion 8 awaits the real elapsed DBR-11 run, and criterion 9 requires explicit user acceptance. Stage 3 must not begin yet. |

### 11.2 Automated qualification

| Suite | Result |
| --- | --- |
| Application MarketData unit | 139 passed |
| Framework Databento unit | 133 passed |
| MarketData Feed actor/NATS unit | 505 passed; focused typed NATS round-trip 1 passed |
| REST MarketData Feed integration | 20 passed |
| PostgreSQL Market Data Service integration | 2 passed against the configured test database |
| Databento UI system | 10 passed |
| C++ native synthetic | 1 CTest target passed |
| C++ native live-enabled | 1 CTest target passed |
| Rust native synthetic | 6 passed |
| Rust native live-enabled | 8 passed across unit and FFI suites |
| C++/Rust ABI and behavioral parity | 6 passed |
| API Server build | succeeded with 0 warnings and 0 errors |
| UI build | succeeded with 0 warnings and 0 errors |
| Lifecycle ownership architecture gate | passed |
| Command/Event/Realtime/Query actor convention gates | 39/31/16/36 domain actors passed |

The live-enabled native suites compile and test provider-specific normalization and slow-reader
semantics; they do not claim that a provider-connected overnight run occurred.

### 11.3 Fault, concurrency and persistence evidence

- Connection loss, heartbeat timeout, terminal fault and aggregation-worker completion enter the
  same exactly-three-attempt core recovery policy.
- A terminal worker signal triggers an immediate probe rather than waiting for the next minute.
- Manual reset, watchdog poll, rollover and recovery share one serialized executor; the measured
  maximum concurrent lifecycle mutation is one.
- Optional-feed exhaustion remains Orange with core readiness retained. Core exhaustion stops the
  runtime and latches Red.
- Observation persistence retries exactly three times with one idempotent observation ID.
  Publication failure cannot undo persistence and instead degrades the in-memory status.
- The live PostgreSQL test verifies assignment and observation create/read/update/delete, stale
  row-version rejection, two-row VX atomicity and rollback, source validation, indexes and JSONB
  detail filtering.

### 11.4 Resource and accelerated-soak evidence

- The deterministic managed soak executed 1,440 one-minute-equivalent probes and 50 serialized
  restarts in 81.684 ms, allocated 3,943,872 bytes, grew private memory by 4,096 bytes and added no
  handles. The history query remained bounded to 1,000 rows.
- Each backend completed 20,000 one-call bulk watchdog snapshots and 50 restarts. The measured C++
  mean was 0.598 microseconds per poll and the Rust mean was 1.071 microseconds. Each allocated 40
  managed bytes for the measurement loop, added zero private bytes and zero handles, and reported
  matching ring state: 0/16,384 used, zero high-water backlog and zero overruns.
- Native ring-overrun/fault tests pass for C++ and Rust; live-enabled tests cover slow-reader warning
  behavior without treating that advisory as a terminal connection failure.

These accelerated tests prove deterministic state-machine, allocation, memory, handle, ring,
backlog and P/Invoke-latency bounds. They complement rather than replace the real elapsed run
required by `DBR-11`.

### 11.5 Elapsed soak commands

Run the provider-connected qualification during a representative session with a clean or explicitly
acknowledged dirty tree, retaining each generated manifest, console log, TRX, machine result and
completion record:

```powershell
& scripts/Databento/Run-MarketCloseSoak.ps1 -Implementation Cpp -Scenario Future -DurationMinutes 1440 -StartAt <start> -AllowDirtyWorkingTree
& scripts/Databento/Run-MarketCloseSoak.ps1 -Implementation Rust -Scenario Future -DurationMinutes 1440 -StartAt <start> -AllowDirtyWorkingTree
```

The two implementations should be qualified sequentially unless the Databento entitlement and the
test machine are intentionally approved for concurrent sessions.

## 12. Next action

Complete and review the two elapsed provider-connected Stage 2 soak runs. If they pass, record the
artifacts here and explicitly accept Stage 2. Do not begin MDOH Stage 3 before both conditions are
satisfied.
