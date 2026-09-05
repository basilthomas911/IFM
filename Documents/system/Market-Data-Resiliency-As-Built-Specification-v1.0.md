# Market Data Resiliency As-Built Specification v1.0

| Item | Value |
| --- | --- |
| Specification ID | `MDR-AB` |
| Status | Stage 1 and Stage 2 as-built and accepted |
| Version | 1.0 |
| As-built date | 2026-09-04 |
| Runtime deployment | `TomasAI.IFM.Application.Api.Server` |
| Native backends | C++ default; Rust parity implementation |
| Scope | Consolidated current implementation of Market Data Resiliency Stages 1 and 2 |
| Excluded | Stage 3 central health/process containment and Stage 4 resilient option-chain ownership |
| Stage roadmap | `Documents/system/Market-Data-Reliability-Three-Stage-Implementation-Plan-v1.0.md` |

## 1. Purpose

This is the single consolidated as-built specification for the Market Data Resiliency work that is
implemented through Stage 2. It gives the Stage 3 specification one stable description of the
system it will extend.

This document answers four questions:

1. what is running now;
2. who owns each lifecycle and mutation boundary;
3. what recovery guarantees exist inside the current API Server process; and
4. which remaining limitations belong to Stage 3 rather than Stage 2.

It describes implementation truth, not the earlier design target. A feature described only in the
Stage 3 or Stage 4 roadmap is not current behavior.

Post-baseline shared observability now records Market Outlook cache/composition/publication through
the composite recorder and exposes a read-only central health panel. This does not change Stage 2
lifecycle ownership, its publisher policy, or latest-snapshot restart hydration. The new bounded
publisher is injected only with Stage 3 enabled. See the separate
[Stage 3 as-built subset and remaining gates](Market-Data-Resiliency-Stage-3-As-Built-Specification-v1.0.md).

## 2. Authority and document precedence

For Stage 1 and Stage 2 current behavior, use the following precedence:

1. executable source and tests are runtime truth;
2. this specification is the authoritative consolidated human-readable as-built description;
3. the four-stage implementation plan controls stage gates and future-stage requirements;
4. focused correction/design documents remain supporting rationale and detailed historical
   evidence; and
5. the original v0.1 system design remains architectural background where it does not conflict
   with a later document.

This specification supersedes conflicting current-behavior statements in:

- `Databento-Market-Data-Service-Resiliency-System-Design-v0.1.md`;
- `Databento-Absolute-Per-Dataset-Watchdog-and-Reset-Design-v1.0.md`;
- `Market-Outlook-Simple-Hot-Cache-Correction-Implementation-Plan-v1.0.md`;
- `Market-Outlook-Durable-Snapshot-Implementation-Plan-v1.0.md`; and
- current-behavior portions of the four-stage implementation plan.

Those documents are not deleted or made invalid. They retain their design rationale, execution
records and future requirements. When implementation changes, this as-built specification and the
applicable tests must be updated together.

Two known documentation conflicts are explicitly resolved here:

- Market Outlook now persists exactly one latest eligible snapshot per entity for restart
  hydration. It does not persist or replay the stream of intermediate display snapshots.
- The Stage 2 watchdog currently polls every 15 seconds with a five-minute causal-stall threshold.
  The one-minute/five-minute live-session policy is a Stage 3 target, not current behavior.

## 3. Scope and maturity

### 3.1 Included

- Stage 1 local Market Outlook update ingestion, single-writer composition, immutable hot cache,
  realtime notification, latest-only restart persistence, hydration and measurements;
- Stage 2 authoritative futures session and contract selection;
- Stage 2 Databento lifecycle ownership, native/managed health capture, per-dataset causal health
  evaluation, in-process generation reset, startup qualification, rollover and shutdown;
- C++ and Rust native ABI parity relevant to lifecycle and health;
- persisted rollover assignments and watchdog observations;
- readiness, status, history, refresh and UI operational boundaries already implemented; and
- current configuration, verification evidence and limitations.

### 3.2 Excluded

- an independent central market-data operations-health service;
- a separate OS process for each dataset;
- forced termination and replacement of a hung dataset process;
- the Stage 3 session-aware one-minute/five-minute watchdog policy;
- resilient option-chain sessions and strategy-owned option ticker leases; and
- order-execution behavior.

### 3.3 Stage status

| Stage | Implementation | Acceptance |
| --- | --- | --- |
| Stage 1 — Market Outlook single-writer boundary | Complete | Complete |
| Stage 2 — Databento lifecycle and in-process resiliency | Complete | Accepted by the owner on 2026-09-04; elapsed provider-connected soak retained as a non-blocking operational follow-up |
| Stage 3 — central health and dataset process containment | Not implemented | Not started |
| Stage 4 — resilient options market data | Not implemented | Not started |

Stage 2 was explicitly accepted by the owner on 2026-09-04 as working according to design. The
owner accepted the remaining elapsed provider-connected soak as a documented gate waiver so Stage 3
specification and implementation can proceed. The soak remains recommended evidence and must not be
misrepresented as having run.

## 4. Runtime terminology and identity

| Term | Meaning |
| --- | --- |
| Entity | A Market Outlook identity: futures contract plus value date |
| Dataset | A Databento dataset such as `GLBX.MDP3` or `XCBF.PITCH` |
| Epoch | The API-owned active futures value-date runtime |
| Generation | One replaceable dataset runtime inside an epoch, identified by a GUID |
| Contract role | One authoritative role: ES quarterly, VX front month or VX second month |
| Operational value date | The value date exposed even when the market is closed |
| Active value date | The value date for which a Databento epoch must be running; null when closed |
| Core feed | A feed required for overall readiness |
| Optional feed | A feed whose failure degrades its capability without stopping healthy core data |
| Cooperative reset | Bounded in-process stop, state clear, reconstruction and qualification |
| Process replacement | Stage 3-only termination and restart of one dataset worker process |

Identity is hierarchical:

```text
API Server process
  -> active value-date epoch
       -> dataset
            -> generation
                 -> native feed/ring + managed drain + channels + aggregation worker
```

A generation GUID prevents an old or duplicate reset request from replacing a newer owner.

## 5. Current end-to-end architecture

```text
Databento transport
  -> native producer
  -> native ring
  -> dedicated managed drain thread
  -> bounded per-instrument channels
  -> multiplexed dataset reader
  -> dataset TickAggregation worker
  -> latest-price store / analytics events
  -> Market Outlook typed update producers
  -> bounded local MPSC update channel
  -> sole MarketOutlookUpdateProcessor
  -> immutable process-local Market Outlook hot cache
       +-> immediate realtime snapshot event -> UI
       \-> latest-only periodic DB upsert -> next-process hydration

Watchdog control plane
  authoritative futures session + PostgreSQL contract roles
  -> serialized lifecycle owner
  -> native registry + managed dataset/worker/cache snapshot
  -> per-dataset causal evaluator
  -> cooperative dataset-generation reset or whole-epoch recovery
  -> persisted observation + status/readiness/UI
```

The Market Outlook composition queue is local memory. It is not a JetStream durable replay queue.
Realtime delivery and restart hydration are separate concerns: realtime consumers receive the
latest composed event immediately, while the database periodically replaces one latest row per
entity.

## 6. Cross-stage invariants

1. Market-data producers do not mutate the Market Outlook cache directly.
2. Market Outlook has one production cache writer and publishes whole immutable snapshots.
3. A slow Market Outlook consumer cannot block the Databento producer path.
4. Queue saturation and coalescing are measured; replacement is not silent.
5. Databento lifecycle requests are serialized through one owner.
6. Each active dataset has exactly one admitted generation.
7. A dataset reset is scoped to the failed dataset; unaffected datasets remain admitted.
8. The old generation must quiesce before its replacement is admitted.
9. Reset clears failed-generation price and aggregation state before accepting new observations.
10. Health collection cannot synchronously perform network or database I/O on the market-data hot
    path.
11. Telemetry failure cannot throw into market-data ingestion.
12. C++ and Rust implement one native ABI and the same wake/health semantics.
13. Scheduled closure is an inactive state, not a feed incident.
14. UI or downstream analytics failure alone does not declare a progressing dataset Down.
15. Current-process recovery is cooperative and bounded. Unrecoverable native hangs require the
    Stage 3 process boundary.

## 7. Stage 1 as-built — Market Outlook processing

### 7.1 Ownership

`MarketOutlookUpdateProcessor` is the only production owner of Market Outlook input mutation,
composition and snapshot submission. Source actors, historical warmup and other producers translate
their input into a typed `MarketOutlookUpdate` and submit it through `IMarketOutlookUpdateWriter`.

The update contracts are local objects. They are not actor messages and do not traverse NATS.
Actor/event boundaries exist before some producers and after snapshot publication, not inside the
single-writer composition boundary.

### 7.2 Typed updates

The current update kinds are:

- RSI, TDI and ITI;
- EMA and Bollinger Band;
- ES trade and VIX price;
- EOD and trade signal;
- feed health and historical warmup; and
- hydration and explicit recompose.

Every update carries entity identity, update identity, received time and source provenance needed
for diagnostics. Source positions are diagnostic. They do not reject a valid late-arriving update;
the cache retains latest-arrival semantics.

### 7.3 Bounded ingress and overload behavior

`MarketOutlookUpdateChannel` is a bounded multi-producer/single-consumer channel.

| Property | Current value |
| --- | --- |
| Default primary capacity | 8,192 updates |
| Readers | Exactly one |
| Writers | Multiple |
| Producer operation | Non-blocking `TryWrite` |
| Primary drain batch | At most 64 before overflow scan |
| Overflow key | Entity plus update kind |
| Overflow behavior | Retain the newest pending value for that key |

When the primary lane is full, submission stores the update in an explicitly measured coalescing
slot. Replacing an older overflow item records `Coalesced` and removes the old pending-age entry.
Producers do not wait for capacity.

This is deliberate latest-state load shedding, not durable history. Signals that require historical
event fidelity must use their own durable domain storage and must not depend on this display queue.

### 7.4 Sequential application and isolation

The hosted processor reads updates sequentially. For each update it:

1. applies the typed partial update;
2. composes a complete `MarketOutlookReadModel`;
3. atomically replaces the cache cell's immutable published state; and
4. if the snapshot has a valid positive and internally consistent ES OHLC baseline, submits it to
   the snapshot publisher.

Hydration updates rebuild the local cache but are not republished. A recompose update uses current
inputs without pretending a new component arrived.

An exception while applying or publishing one update is measured and logged. It does not terminate
the reader loop. Publication failure increments the cache notification-failure metric; the committed
local snapshot remains available.

### 7.5 Hot-cache semantics

`MarketOutlookHotCache` stores one cell per entity. The cell contains immutable input state and its
current composed snapshot. The processor writes a newly composed `PublishedState` with a volatile
reference exchange. Readers take that reference without an application-level lock and therefore see
either the complete old state or the complete new state.

Composition remains unconditional latest-arrival partial-write plus whole-snapshot OR composition.
Missing optional inputs are represented in the read model rather than causing torn or partial reads.

The cache is a process-local live workspace. It is rebuilt by hydration and new input after process
restart.

### 7.6 Realtime publication and latest-only persistence

`LatestMarketOutlookSnapshotPublisher` has two independent outputs:

1. it sends a `MarketOutlookSnapshotInsertedEvent` to realtime consumers immediately; and
2. it queues only the newest snapshot per entity for periodic database upsert.

The default persistence cadence is one second. Intermediate snapshots within that interval are
coalesced and never replayed. A database failure requeues the newest failed item unless a newer
sequence is already pending. Realtime publication is not delayed until the periodic database flush.

The persisted row is a restart/display hydration checkpoint, not an event history and not a
downstream work queue. Startup queries obtain the latest eligible snapshot and synthetic/live source
policy prevents synthetic state from being treated as live state.

### 7.7 Shutdown

The update processor gets a bounded five-second opportunity to finish accepted work. The latest
snapshot publisher then gets a bounded five-second final persistence flush. Exhausting either
deadline is logged and does not make application shutdown unbounded.

### 7.8 Stage 1 observability

The local recorder captures bounded-dimension counts and latency for received, enqueued, coalesced,
applied, changed, composed, published and failed outcomes by update kind and processing stage.
Correlation/update GUIDs are diagnostic fields and are not metric labels.

## 8. Stage 2 as-built — Databento lifecycle and recovery

### 8.1 Sole lifecycle owner

`DatabentoMarketDataWatchdogService` is the only production owner of Databento start, stop, poll,
rollover, refresh, reset and application-shutdown operations. A semaphore serializes manual,
scheduled, terminal-fault and watchdog paths so two lifecycle mutations cannot race.

Refresh requests run an immediate serialized probe and qualification. Refresh does not imply a
reset when health is good and is independent from Market Outlook recompose.

### 8.2 Authoritative futures session

The shared Eastern-time session authority exposes `Closed`, `OffTrading` and `LiveTrading`:

| State | Current definition | Lifecycle behavior |
| --- | --- | --- |
| `LiveTrading` | Weekdays 03:00 inclusive to 16:00 exclusive ET within an active value-date session | Runtime active; causal health and 5/15-minute provider freshness display thresholds apply |
| `OffTrading` | Remaining active portion of the 18:00-17:00 ET futures value-date session | Runtime active; causal health applies; provider-message freshness alone does not degrade a quiet feed |
| `Closed` | No active futures value date | Runtime stopped; status is scheduled/inactive |

At an active value-date change, the lifecycle owner reconciles the new contracts and replaces the
old epoch through its serialized rollover path.

### 8.3 Contract authority

`MarketDataServiceDbContext` in PostgreSQL is the authority for three required roles:

- ES quarterly;
- VX front month; and
- VX second month.

The authority reconciles these assignments with the read-only securities/current-futures catalog.
It validates dataset eligibility and date ordering, stores the source-catalog fingerprint and uses
optimistic row versions. VX front/second replacement is atomic. The market-data service does not
mutate the source securities catalog.

Only a complete committed role set is published into the runtime registry and admitted at startup.

### 8.4 Epoch and dataset topology

One active epoch owns the value-date catalog, shared routing/publication services and latest-price
store. Inside it, each dataset owns a replaceable `TickAggregationService` generation containing:

- one native ticker feed, handle and ring;
- one dedicated managed drain thread;
- pooled batches and bounded per-instrument channels;
- one multiplexed reader;
- one aggregation worker and its price/session/sequence state; and
- a generation cancellation/fencing boundary.

Queries resolve through dataset-specific clients/runners. Stream-owner intent is captured before a
dataset reset and restored onto its replacement.

### 8.5 Native ABI and wake correctness

The default production configuration selects the C++ backend. Rust is supported only as an explicit
alternative with ABI and behavioral parity. The native registry exposes bounded bulk health data,
including lifecycle/terminal state, provider activity, subscription counts, produced/consumed
counters, ring occupancy and bounded failure detail.

Both backends implement a consumer-waiting handshake. Before sleeping, the consumer marks its wait
intent and rechecks the ring. The producer signals when it transitions an empty ring or observes a
waiting consumer. This closes the lost-wake race where records could remain in the ring while the
managed drain reported `WaitingForNativeSignal`.

### 8.6 Runtime health snapshot

`DatabentoLifecycleRuntime` joins the native bulk registry with current committed roles and managed
epoch state, including aggregation worker and cache evidence. A complete registry snapshot means
enumeration completed; it does not itself mean every dataset is healthy.

The per-dataset evaluator uses progress between observations. A provider can legitimately be quiet:
no upstream or buffered work means no causal stall.

Immediate Down conditions are:

- native terminal/major failure;
- producer or transport stopped;
- ring overrun;
- aggregation worker stopped;
- received subscriptions below expected subscriptions; or
- one aggregation record in flight for at least the hard-stall timeout.

Causal Suspect conditions include:

- produced records or buffered ring data without native-consumer progress;
- ring data while the drain waits for a native signal;
- managed batch publication or full channels without aggregation progress; or
- published batches without completed aggregation records.

Except for the already-over-time in-flight aggregation record, the same causal reason must remain
continuous for the five-minute hard-stall timeout before the evaluator declares the dataset Down.
A reason change or restored progress restarts/clears the confirmation episode.

### 8.7 Display health and readiness

Core readiness requires the committed roles, required feeds, subscriptions, running aggregation
workers and current cache/runtime evidence to qualify.

| Condition | Display/lifecycle result |
| --- | --- |
| Closed by session policy | Inactive / `ScheduledStopped` |
| All required data operational | Green / `Healthy` |
| Optional feed failed | Orange / `Degraded`; healthy core datasets remain running |
| Causal stall is still within confirmation window | Yellow / dataset `Suspect` |
| Live provider-message age over 5 minutes | Yellow |
| Live provider-message age over 15 minutes | Red/not ready |
| Core dataset Down or recovery exhausted | Red / `Failed` |
| Reset in progress | Orange / `Resetting` |

Provider freshness is a live-session display/readiness rule. Causal pipeline evidence remains the
reset trigger in both LiveTrading and OffTrading. During OffTrading, a quiet but operational feed is
not reset merely because no provider message arrived recently.

### 8.8 Current watchdog scheduling

The hosted watchdog waits for either:

- the configured 15-second poll interval; or
- a terminal worker fault signal.

A terminal signal causes an immediate out-of-cycle probe. Every resulting operation still enters
the same serialized lifecycle boundary.

This is not yet the Stage 3 session-aware schedule. The current service continues polling at the
same configured interval in all states; a Closed probe ensures the runtime is stopped and records
inactive status.

### 8.9 Cooperative dataset-generation reset

When the evaluator declares one or more ticker datasets Down, the watchdog diagnoses and resets only
those datasets. For each failed dataset, the epoch performs this sequence under its lifecycle lock:

1. reject a mismatched value date;
2. ignore the request successfully if a newer generation already owns the route;
3. capture the affected contracts and current stream owners;
4. cancel/fence and stop the old aggregation generation within the teardown timeout;
5. dispose the old generation;
6. remove its dataset and contract routes from API admission;
7. clear latest trade, quote, Greeks and sequence values for affected contracts while preserving
   stable epoch reader handles;
8. allocate a new generation GUID and construct new native, drain, channel and aggregation state;
9. restore captured stream owners;
10. qualify the replacement with three observations, rejecting stopped feeds and repeated
    produced-without-drain progress; and
11. atomically admit the replacement dataset and contract routes.

Old-generation state is never copied into the replacement. A consumer holding a stable latest-price
reader sees no value after reset until the new generation accepts a new observation.

The current watchdog makes one cooperative reset call for a dataset already confirmed Down. If
teardown or replacement qualification fails, lifecycle state becomes `Failed`. It cannot forcibly
kill a stuck unmanaged thread because the dataset is still inside the API Server process.

### 8.10 Whole-epoch recovery

Startup failure, rollover failure and a core qualification failure outside the per-dataset Down path
use bounded whole-epoch recovery. This performs exactly three serialized stop/start/qualify attempts:

- attempt 1 immediately;
- attempt 2 after the configured five-second delay; and
- attempt 3 after the configured fifteen-second delay.

After exhaustion, the runtime receives a final stop attempt and latches `Failed`/Red. Recovery does
not loop forever.

### 8.11 Shutdown and generation fencing

Dataset stop cancels the generation, clears routes, asks the feed to stop with forced wake/unblock,
waits for the aggregation worker, then disposes its reader and reference-counted publisher. Whole
epoch shutdown stops dataset aggregation services and invalidates the epoch latest-price store.

The application host invokes the same serialized lifecycle owner on shutdown. No UI, actor or
secondary hosted service owns a competing Databento stop/start implementation.

### 8.12 Persistence and operational surfaces

PostgreSQL persists authoritative contract assignments and append/update watchdog observations.
Observation persistence is retried at most three times with the configured retry delay, and status
is published through the watchdog publisher boundary.

Typed lifecycle commands and status/readiness/current-contract/history queries expose the current
state without allowing UI code to poll the native layer directly. The operational UI can navigate to
detail, treats planned closure as inactive and gates dependent capability when a required core feed
is unavailable.

The Stage 1 `IMarketDataOperationsRecorder` is the compatibility boundary for bounded Stage 2
measurements. The full independent aggregation, retention and operational-health UI are Stage 3.

## 9. Current state ownership and reset effects

| State | Scope/owner now | Durable | Effect of cooperative dataset reset |
| --- | --- | --- | --- |
| Native feed, handle and ring | Dataset generation | No | Destroyed and recreated |
| Drain thread, pools and bounded channels | Dataset generation | No | Stopped/disposed and recreated |
| Aggregation price/session/sequence state | Dataset generation | No | Discarded and recreated empty |
| Dataset generation GUID | Dataset generation | No | Replaced |
| Stream-owner registrations | Captured above generation | No | Restored to replacement |
| Contract-to-dataset catalog | Value-date epoch | No | Preserved |
| Latest-price reader handles | Value-date epoch | No | Identity preserved; affected values cleared |
| Other dataset generations | Value-date epoch | No | Unchanged |
| Contract-role assignments | Market-data service/PostgreSQL | Yes | Preserved |
| Watchdog observations | Market-data service/PostgreSQL | Yes | New correlated evidence appended/updated |
| Market Outlook inputs/current snapshot | API process/local cache | No | Not directly reset by a Databento dataset reset; refreshed by new downstream updates |
| Latest eligible Market Outlook checkpoint | Market data DB, one row per entity | Yes | Preserved for restart hydration; no intermediate replay |
| Watchdog causal episode | Watchdog process, keyed by dataset generation | No | Forgotten after successful replacement |

## 10. Current configuration baseline

The API Server binds these values from `MarketDataRecovery`; the table shows checked-in defaults.

| Setting | Current value | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Automatic recovery enabled |
| `NativeBackend` | `Cpp` | Selected native implementation |
| `PollInterval` | 15 seconds | Recurring Stage 2 probe cadence |
| `ProbeTimeout` | 1 second | Native/runtime snapshot deadline |
| `AttemptTwoDelay` | 5 seconds | Whole-epoch retry delay before attempt 2 |
| `AttemptThreeDelay` | 15 seconds | Whole-epoch retry delay before attempt 3 |
| `PersistenceRetryDelay` | 100 milliseconds | Watchdog observation retry delay |
| `YellowFreshnessAge` | 5 minutes | Live-session freshness warning default |
| `RedFreshnessAge` | 15 minutes | Live-session freshness failure default |
| `HardStallTimeout` | 5 minutes | Continuous causal-stall confirmation |
| `DatasetTeardownTimeout` | 10 seconds | Cooperative old-generation stop budget |
| `DatasetQualificationTimeout` | 30 seconds | Replacement construction/qualification budget |
| Market Outlook channel capacity | 8,192 | Local primary update lane default |
| Market Outlook persistence interval | 1 second | Latest-only checkpoint cadence |
| Market Outlook shutdown drain | 5 seconds | Bounded processor drain |
| Latest snapshot shutdown flush | 5 seconds | Bounded final DB flush |

`YellowFreshnessAge` and `RedFreshnessAge` are contract defaults even though Startup does not
currently override them from configuration. Validation requires positive timeouts and Red greater
than Yellow.

## 11. Fault and recovery matrix

| Fault/evidence | Current detector | Current action | Stage 2 limit |
| --- | --- | --- | --- |
| Quiet provider, no buffered work | Causal evaluator | Remain Up | None; quiet is healthy |
| Live message age 5-15 minutes | Freshness evaluation | Yellow/not fresh | Does not itself prove a causal pipeline stall |
| Live message age over 15 minutes | Freshness evaluation | Red/not ready, bounded recovery path if core readiness fails | Uses whole-epoch recovery when not a confirmed dataset Down |
| Ring has records while drain sleeps | Per-dataset causal evaluator | Suspect; Down after continuous timeout | Requires cooperative stop to return |
| Producer advances, consumer does not | Per-dataset causal evaluator | Suspect; Down after continuous timeout | Same |
| Managed backlog without aggregation progress | Per-dataset causal evaluator | Suspect; Down after continuous timeout | Same |
| Aggregation record in flight at least 5 minutes | Immediate evaluator rule | Dataset Down and cooperative reset | Cannot preempt arbitrary in-process native/unmanaged work |
| Native terminal/producer stopped/ring overrun | Immediate evaluator rule or terminal signal | Immediate probe and dataset reset | Reset failure latches Failed |
| One optional dataset fails | Criticality evaluation | Orange; isolate failed capability | No OS process containment yet |
| Startup/rollover/core qualification failure | Lifecycle qualification | Three whole-epoch recovery attempts | Final state latches Failed |
| Market Outlook queue full | Channel | Coalesce newest by entity/kind | Intermediate display updates intentionally dropped |
| Market Outlook publication fails | Processor/publisher | Record/log and continue local composition | Consumer misses that notification |
| Market Outlook DB upsert fails | Latest publisher | Requeue newest snapshot | No intermediate snapshot replay |

## 12. Source map

| Responsibility | Primary implementation |
| --- | --- |
| Stage 1 update contracts/channel/metrics | `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookUpdateChannel.cs` |
| Stage 1 immutable hot cache | `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookHotCache.cs` |
| Typed application updates and sole processor | `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Model/Processing/MarketOutlookUpdates.cs` |
| Immediate realtime/latest-only persistence | `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Model/Processing/LatestMarketOutlookSnapshotPublisher.cs` |
| Snapshot persistence policy | `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Model/Processing/MarketOutlookSnapshotPersistencePolicy.cs` |
| API Server dependency/lifetime wiring | `TomasAI.IFM.Application.Api.Server/Startup.cs` |
| Session state/policy | `TomasAI.IFM.Domain.MarketData.Shared/FuturesMarketState.cs` |
| Stage 2 contracts/options | `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoResiliencyContracts.cs` |
| Sole lifecycle/watchdog owner | `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoMarketDataWatchdogService.cs` |
| Per-dataset causal evaluator | `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDatasetHealthEvaluator.cs` |
| Native/managed snapshot join | `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoLifecycleRuntime.cs` |
| Contract reconciliation authority | `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoContractAuthority.cs` |
| Epoch and dataset reset | `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataEpoch.cs` |
| Dataset managed worker | `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/TickAggregationService.cs` |
| Latest-price reset semantics | `TomasAI.IFM.Framework.MarketData.DataBento/LastPrice/DatabentoLastPriceStore.cs` |
| Managed native feed/drain wrapper | `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/SyntheticTickerFeed.cs` |
| C++ native backend | `native/DatabentoFeed.Native/src/databento_feed_native.cpp` |
| Rust native backend | `native.rust/DatabentoFeed.Rust/src/engine.rs`, `src/lib.rs` |
| Contract/watchdog PostgreSQL store | `TomasAI.IFM.Application.Storage/MarketDataServiceDb/MarketDataServiceDbContext.cs` |
| Checked-in Stage 2 defaults | `TomasAI.IFM.Application.Api.Server/appsettings.json` |

## 13. Verification baseline

The latest focused verification recorded while consolidating the implementation is:

| Suite | Result |
| --- | --- |
| `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests` | 136 passed |
| `TomasAI.IFM.Application.MarketData.UnitTests` | 142 passed |
| C++ synthetic native CTest | Passed |
| C++ live-enabled build and CTest | Passed |
| Rust native tests | 6 passed |

Rust tests retain an existing unused-`historical_inputs` warning. `cargo fmt --check` also reports
pre-existing formatting drift in untouched Rust blocks; that is not represented as a clean format
gate.

The broader Stage 1 and Stage 2 qualification history remains in sections 11 and 12 of the
four-stage implementation plan. The owner accepted Stage 2 on 2026-09-04 with the real elapsed
active-session/overnight provider-connected soak retained as non-blocking follow-up evidence. The
acceptance does not claim that soak occurred.

## 14. Known limits and deferred work

### 14.1 In-process containment ceiling

Stage 2 can reconstruct a dataset only after its old generation cooperatively stops. If native or
unmanaged work cannot be interrupted within the teardown deadline, the API Server cannot safely
kill only that thread. Restarting the API Server is the present external recovery ceiling.

Stage 3 removes that ceiling by moving each dataset into a replaceable worker process while keeping
supervision and incident ownership above it.

### 14.2 Current watchdog policy is not the Stage 3 policy

Current Stage 2 behavior is 15-second polling, five-minute causal confirmation, one cooperative
reset for a confirmed Down dataset, and three attempts for whole-epoch startup/rollover recovery.
Do not describe the following as implemented until Stage 3 is complete:

- LiveTrading probes once per minute;
- at most one cooperative reset attempt per unhealthy scheduled probe;
- five continuously unhealthy minutes or five failed attempts trigger dataset-process replacement;
- one full healthy live minute closes an incident;
- OffTrading probes every five minutes and resets after fifteen unhealthy minutes;
- failed OffTrading cooperative reset triggers dataset-process replacement; or
- Closed stops workers and performs no scheduled health probe.

### 14.3 Central operations health

Stages 1 and 2 expose measurements and typed status boundaries, but their measurements remain local
to the API process. Stage 3 owns independent central aggregation, retention, policy evaluation,
queries, operational UI and process-supervisor evidence.

### 14.4 Options

Historical loading and option strategy analysis designs do not create resilient live option ticker
ownership. Option-chain session recovery, lease reconciliation and selected-leg reconstruction are
Stage 4 work after Stage 3 containment is accepted.

## 15. Binding handoff to the Stage 3 specification

The Stage 3 specification must preserve these Stage 1/2 behaviors unless it explicitly replaces a
named boundary with migration and regression evidence:

1. Market Outlook remains local MPSC/single-writer and lock-free for whole-snapshot readers.
2. Market Outlook persistence remains latest-only hydration, never a display-event replay backlog.
3. The authoritative session policy and PostgreSQL contract-role authority remain above dataset
   generations.
4. Dataset health remains causal and quiet-provider safe.
5. Reset clears failed-generation native, drain, buffer, aggregation and latest-price values.
6. C++ and Rust retain ABI and wake-handshake parity.
7. Lifecycle mutation remains serialized; Stage 3 moves the owner above workers rather than adding
   another competing owner.
8. Dataset failures remain isolated; healthy datasets, API, UI and supervisor continue.
9. Worker intent, contract identity and incident history live above a replaceable worker process.
10. Stage 2 cooperative reset remains the first recovery action; Stage 3 adds an OS-enforceable
    termination/replacement boundary when cooperation fails.

The Stage 3 design must define, test and document:

- worker command/status protocol and versioning;
- generation and incident identity across process replacement;
- supervisor ownership and session-aware scheduling;
- exact live/off-hours escalation timers and healthy qualification;
- Windows and Linux process-tree termination;
- contract and stream-intent reconstruction;
- stale message and old-generation fencing;
- central status persistence and bounded-cardinality metrics;
- crash-loop/backoff policy; and
- fault-injection proving that only the failed dataset process is replaced.

The approved target watchdog policy is recorded under `MDOH-09` in the four-stage implementation
plan. The future Stage 3 specification must copy that policy as a binding requirement and must not
silently reinterpret the current Stage 2 15-second/five-minute behavior as the final design.

## 16. Change control

This document is current only while its source map and tests agree with it. Any change to lifecycle
ownership, session timing, reset scope, generation fencing, Market Outlook durability, queue
semantics, native ABI or recovery thresholds requires:

1. a code and test change;
2. an update to this as-built specification;
3. an update to the roadmap if stage scope or acceptance changes; and
4. new verification evidence proportional to the failure mode.

Stage 3 is defined separately by
`Documents/system/Market-Data-Resiliency-Stage-3-Specification-v1.0.md` and
`Documents/system/Market-Data-Resiliency-Stage-3-Implementation-Plan-v1.0.md`. This file remains the
Stage 1/2 baseline against which Stage 3 deltas and acceptance evidence are judged.
