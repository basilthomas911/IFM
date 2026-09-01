# Databento Market Data Service Resiliency System Design v0.1

**Status:** Proposed for review  
**Date:** 2026-09-01  
**Scope:** System-wide detailed design; implementation is not authorized by this document  
**Current deployment:** API-server-hosted market-data runtime  
**Target deployment:** Dedicated Databento Market Data service orchestrated by .NET Aspire
**Related projection authority:** `TomasAI.IFM.Domain.MarketData.Analytics/Docs/Market-Outlook-Simple-Hot-Cache-Correction-Implementation-Plan-v1.0.md`

> The `MOSC` correction supersedes every Market Outlook activation, native-generation admission
> fence, source-order rejection, generation-driven cache clear and dedicated hot-cache worker
> statement retained later in this historical design. Databento generation fencing remains valid
> for native lifecycle ownership and feed diagnostics, but it never gates a routed Market Outlook
> partial write.

## 1. Purpose

This design makes the Databento market-data service resilient, observable, transactionally controlled, and fail-closed for the minimum futures market-data set required by IFM.

The design establishes one lifecycle owner for:

- authoritative current-contract assignment;
- value-date startup and rollover;
- native C++ or Rust feed startup;
- currently traded feed activation;
- one-minute watchdog observations;
- automatic three-attempt recovery;
- actor-requested full reset;
- terminal feed shutdown and UI readiness fencing;
- persisted watchdog history; and
- lifecycle coordination for the versionless Market Outlook hot-cache worker.

The initial implementation remains hosted by the existing API server. The public contracts and component boundaries must allow the same service to move into a dedicated Aspire-hosted process without redesigning callers.

## 2. Binding decisions

The following decisions are authoritative for this design.

1. The watchdog runs throughout every active futures value-date session, including live-trading and off-trading hours.
2. The active value-date session is 18:00 through 17:00 Eastern. The scheduled 17:00-18:00 maintenance closure and weekend closure are intentional inactivity.
3. Live trading remains 03:00 inclusive through 16:00 exclusive Eastern. Off-trading is the remainder of an active value-date session.
4. Feed silence changes operational health; it does not independently relinquish feed ownership.
5. One C# background service is the only component permitted to start, stop, reset, replace, or roll the Databento runtime.
6. One bulk P/Invoke call obtains a point-in-time native status snapshot for every feed that has reached startup and has not yet completed terminal capture and destruction.
7. Native major feed status is `Up`, `Resetting`, or `Down`.
8. The minimum authoritative current-contract set contains exactly these three required roles:

   - quarterly ES futures;
   - front/current-month VX futures;
   - second/next-month VX futures.

9. All three minimum contract roles are core and required. Failure of any one after recovery is exhausted shuts down the complete Databento runtime.
10. Option and future auxiliary feeds are noncritical to global availability. Their exhausted failure produces Orange health without stopping healthy core feeds.
11. PostgreSQL `MarketDataServiceDbContext` is authoritative for current-contract assignments and watchdog history.
12. The existing complete futures-contract catalog is read-only to the Market Data Service.
13. Every current-contract create, update, or rollover requires a matching complete source contract. It never inserts, updates, deletes, or changes flags on that source contract.
14. Watchdog history is persisted in the initial implementation. Production retention and archival policy can be added later.
15. During expected market-data operating hours, failure to establish all three core contracts and feeds restricts UI navigation to the System menu.
16. The future Aspire Market Data service communicates with clients exclusively through typed actor commands, events, and queries.
17. C++ and Rust remain equal, supported native implementations of one managed P/Invoke contract. This design does not select a preferred production backend.
18. Every native feed or watchdog API change must be implemented in both backends and pass the same ABI, behavioral, failure, and performance qualification before the managed application may consume it.
19. A deployment selects exactly one native backend before process startup. Managed market-data code uses the same exported functions, structs, status values, and normalized record layout regardless of that selection.
20. The Market Data Service boundary hosts the versionless Market Outlook singleton because the projection is assembled from current market-data and analytics hot caches. Contract ID and value date select an identity; feed generation never controls write admission.
21. `DatabentoMarketDataWatchdogService` remains the sole Databento lifecycle owner. Market Outlook is an immediately writable singleton projection/cache used by the realtime actor and query actor, not a second lifecycle worker, and it cannot start, stop, reset, replace, or roll Databento.
22. The watchdog reports explicit `Up`, `Resetting`, or `Down` feed truth and its reason as an independent partial input. That status cannot clear analytics, reject a cache write or roll back a published snapshot. Market Outlook calculation or notification failure never initiates a Databento reset.
23. Market Outlook is non-authoritative, versionless, process-local and rebuildable. It is not persisted in `MarketDataServiceDbContext`, `MarketDataDbContext`, or any other database. Its authoritative inputs retain their existing owners and persistence rules.

## 3. Verified baseline

### 3.1 Current ownership is distributed

Market-data lifecycle ownership is currently split among:

- `FuturesContractRolloverStartupService`, which supervises value-date startup;
- `MarketDataFeedEventActor`, which directly performs start, stop, and reset operations;
- individual futures and option streaming actors, which can start the API and acquire routes;
- `DatabentoMarketDataApi`, which treats an existing same-value-date epoch as already started even if its aggregation worker has completed.

This distribution permits an epoch object to remain present while its native reader or managed aggregation worker is no longer running. The new service removes direct lifecycle mutation from these callers.

### 3.2 Native health exists but is incomplete at the ABI boundary

Both native implementations expose feed lifecycle and terminal status. The C++ implementation also tracks heartbeat messages and the last provider-message monotonic timestamp internally, but its current statistics ABI does not export those fields. The Rust implementation does not yet maintain equivalent heartbeat counters.

The managed wrapper can retrieve a terminal error, but epoch and API health currently discard important native transport details. Native terminal completion can therefore stop a worker without leaving enough application-level evidence to identify the terminal cause.

### 3.3 Implemented interim synchronous up/down probe

Before the full persisted watchdog is implemented, the current API exposes one deliberately small
connection/runtime liveness query:

```csharp
bool IsDatabentoFeedUp(TimeSpan? timeout = null);
```

The default timeout is one second. The result is `true` only when an epoch exists, every configured
native feed reports `Running` with terminal status `Ok`, every associated managed aggregation worker
is still running, and the complete enumeration finishes within the supplied timeout. A connected
feed remains up during a quiet market because record freshness is explicitly outside this probe.

The probe uses the existing `dbf_feed_get_stats` operation implemented by both C++ and Rust. It does
not add a new P/Invoke operation, perform network I/O, reset a feed, persist an observation, or replace
the future bulk watchdog snapshot. Expected lifecycle, disposed-handle, native-status and timeout
conditions fail closed as `false` rather than escaping as operational exceptions. Both C++ and Rust
now advertise canonical ABI version 3. A shared capability manifest and binary comparison suite
qualify exports, layouts, validation/status behavior, deterministic lifecycle/record/statistics
vectors, latest-price behavior and historical operations. Every future public C++ change must
include its Rust equivalent and parity evidence in the same change set.

Persistent live sessions treat the provider `SlowReaderWarning` as an advisory pressure signal, not
as terminal feed completion. Both C++ and Rust increment the observable slow-reader counter and keep
the session open while the managed consumer drains. Development and Paper/Production profiles permit
up to 30 seconds for a full native ring to regain capacity during the startup/session replay burst;
the deterministic Synthetic CI profile retains its two-millisecond bound. Exhausting the configured
ring wait or receiving a different terminal system code remains a real feed failure. The future bulk
watchdog snapshot must expose the warning count, ring occupancy/high-water data and terminal reason
so this behavior is visible without changing its lifecycle meaning.

### 3.4 Existing recovery utilities are not wired to production

`DatabentoFeedMonitor` and `DatabentoRecoveryOrchestrator` exist in the framework but are only used by tests. The existing orchestrator uses five attempts and synchronous callbacks. This design requires an asynchronous, production-wired coordinator with exactly three attempts.

### 3.5 Current contract storage location

The complete `FuturesContractV2ReadModel` catalog is currently stored through `SecuritiesDbContext` in the `futures_contract` source table and its controlled projections. `MarketDataDbContext` stores market observations and analytics rather than the complete futures-contract master.

For this design, references to the existing Market Data contract catalog mean the existing `SecuritiesDbContext` futures-contract catalog. Moving or duplicating that master into `MarketDataDbContext` is not required, and the new service must not mutate it.

The existing Scylla `futures_contract_rollover` row is no longer authoritative after this design is implemented. It may remain for compatibility or later removal, but the new service must not read it as an operational decision source.

## 4. System context

### 4.1 Current deployment

```text
UI and actor clients
        |
        | NATS commands, events, and queries
        v
API Server
  +-- DatabentoMarketDataWatchdogService (single lifecycle owner)
  +-- Market Outlook singleton cache (realtime/query actor projection)
  +-- MarketDataServiceDbContext (PostgreSQL)
  +-- Existing contract catalog reader (read-only)
  +-- Databento managed application/framework layer
        |
        | one bulk watchdog P/Invoke per minute
        v
  C++ or Rust Databento native runtime
```

### 4.2 Future Aspire deployment

```text
UI / strategy / trading / administration services
                        |
              NATS commands, events, queries
                        v
Dedicated Databento Market Data service
  +-- lifecycle/watchdog coordinator
  +-- Market Outlook singleton cache
  +-- PostgreSQL authority and history
  +-- contract-catalog adapter
  +-- managed aggregation and hot caches
  +-- native C++ or Rust feed runtime
                        |
                        v
              .NET Aspire AppHost
     configuration, secrets, health, deployment,
       dependencies, telemetry, and orchestration
```

The current implementation must use interfaces for lifecycle control, contract authority, watchdog persistence, Market Outlook hot-cache projection, notification publishing, and time/session authority so those components can move into the dedicated service without changing actor payloads.

### 4.3 Market Outlook hosting boundary

Market Outlook belongs inside the Market Data Service hosting boundary, but it is not part of the
native Databento lifecycle state machine. The boundary contains one lifecycle worker and one
immediately available cache service:

- `DatabentoMarketDataWatchdogService` owns contracts, startup, native feeds, managed aggregation,
  watchdog polling, reset, rollover, readiness and shutdown; and
- `MarketOutlookHotCache` consumes routed market-data/analytics events through its realtime actor, maintains
  immutable latest-value input and display caches, answers typed queries and publishes UI updates.

The cache is writable before, during and after Databento startup/reset. A routed update always
overwrites only the fields it owns. `(contract ID, value date)` prevents cross-instrument/session
mixing; source sequence, event time, stream epoch and native generation are diagnostics only. The
watchdog may publish feed health, but cannot activate, fence, clear or reject Market Outlook state.

This is hosting and lifecycle coordination, not shared calculation ownership. The watchdog never
calculates RSI, TDI, ITI, EMA, Bollinger, MDI or a Futures Trade Signal. The Market Outlook realtime
actor and cache never interpret transport heartbeat as market data and never mutate Databento lifecycle state.
Market Outlook cache failure is a derived-view degradation; Databento recovery continues to depend
only on authoritative native/managed feed evidence.

## 5. PostgreSQL MarketDataServiceDbContext

### 5.1 Database boundary

Add a dedicated PostgreSQL context and connection:

- context: `MarketDataServiceDbContext`;
- connection key: `MarketDataServiceDbConnection`;
- schema owner: Market Data Service;
- transaction provider: Npgsql/PostgreSQL;
- startup policy: the schema and both required tables must be available before core feed readiness can be established.

PostgreSQL is chosen for row locking, transactional replacement of coupled VX roles, uniqueness constraints, optimistic concurrency, JSONB snapshots, and reliable ordered history.

### 5.2 `currently_traded_futures_contract`

This table stores a complete copied contract for each required operational role. The role, not the root symbol alone, is the stable key because VX requires two simultaneous rows.

Minimum roles:

| Contract role | Root symbol | Meaning | Critical |
|---|---|---|---:|
| `EsQuarterly` | ES | Active quarterly ES futures contract | Yes |
| `VxFrontMonth` | VX | Active current/front-month VX contract | Yes |
| `VxSecondMonth` | VX | Active next/second-month VX contract | Yes |

Recommended columns:

| Column | Type | Purpose |
|---|---|---|
| `contract_role` | text | Stable primary key |
| `root_symbol` | text | ES or VX |
| `contract_id` | text | Canonical contract ID |
| `description` | text | Source contract copy |
| `local_symbol` | text | Provider/local symbol copy |
| `security_type` | text | Contract security type |
| `currency` | text | Contract currency |
| `exchange` | text | Contract exchange |
| `multiplier` | text | Contract multiplier |
| `last_trade_date` | date | Contract expiration/last trade date |
| `next_rollover_date` | date | Next mandatory evaluation date |
| `source_contract_hash` | text | Deterministic fingerprint of the source copy |
| `row_version` | bigint | Optimistic concurrency token |
| `created_on_utc` | timestamptz | Audit timestamp |
| `created_by` | text | Audit identity |
| `updated_on_utc` | timestamptz | Audit timestamp |
| `updated_by` | text | Audit identity |

Required constraints:

- primary key on `contract_role`;
- unique `contract_id` across active roles;
- check constraint for supported role/root-symbol combinations;
- non-empty identifiers and audit identities;
- positive row version;
- second-month VX must not equal front-month VX;
- second-month VX maturity must be later than front-month VX maturity;
- both VX assignments must be replaced in one transaction when their ordering changes.

The table's membership is the authority for current status. A legacy `CurrentlyTraded` flag on the source contract is not a second authority. Runtime models derived from these rows are marked currently traded by the Market Data Service.

### 5.3 Current-contract CRUD

All create and update requests accept an assignment identity, contract ID, rollover date, expected row version, actor identity, and correlation identity. The service performs the following transaction boundary:

1. Load the complete source contract from the existing catalog.
2. Reject the request if the source contract does not exist.
3. Validate its root symbol against the requested role.
4. Validate cross-role ordering and uniqueness.
5. Copy the complete source contract into the PostgreSQL row.
6. Increment `row_version` and commit.
7. Refresh the immutable in-memory registry only from committed rows.

The operation never modifies the source catalog. A cross-database foreign key is impossible because the source catalog is not PostgreSQL; source existence and fingerprint consistency are application-enforced.

Deleting any of the three minimum roles is permitted by the full CRUD contract but immediately makes core readiness false. No feed startup is permitted until all three valid assignments exist again.

Required typed actor APIs:

- create current-contract assignment;
- update current-contract assignment;
- delete current-contract assignment;
- get assignment by role;
- list all assignments;
- validate the complete minimum assignment set.

Commands must use optimistic concurrency and idempotent command identity. Queries return the complete copied DTO and validation state.

### 5.4 `watchdog_status_log`

One row represents one atomic watchdog observation or lifecycle observation. It stores the complete result of the single bulk native watchdog call together with the managed interpretation.

Recommended columns:

| Column | Type | Purpose |
|---|---|---|
| `watchdog_status_log_id` | bigint | Sequence-generated primary key |
| `observation_id` | uuid | Globally unique observation identity |
| `correlation_id` | uuid | Startup/reset/recovery correlation |
| `value_date` | date | Active or attempted value date |
| `observed_on_utc` | timestamptz | Observation timestamp |
| `operation_reason` | text | Startup, poll, reset, recovery, rollover, shutdown |
| `major_status` | text | Up, Resetting, or Down |
| `display_health` | text | Green, Yellow, Orange, Red, or Inactive |
| `core_contracts_ready` | boolean | All three core roles are valid and running |
| `recovery_attempt` | integer | Zero or attempt one through three |
| `native_backend` | text | C++ or Rust |
| `native_abi_version` | integer | Snapshot ABI version |
| `native_generation` | uuid | Active native generation identity |
| `failure_stage` | text | Optional startup/recovery failure stage |
| `failure_detail` | text | Optional bounded failure detail |
| `feed_status_details` | jsonb | Complete bulk native and managed feed snapshot |
| `row_version` | bigint | Optimistic concurrency token |
| `created_on_utc` | timestamptz | Audit timestamp |
| `created_by` | text | Audit identity |
| `updated_on_utc` | timestamptz | Audit timestamp |
| `updated_by` | text | Audit identity |

`watchdog_status_log_id` is generated by the existing sequence-ID service. It is never entered manually.

Recommended indexes:

- unique `observation_id`;
- `observed_on_utc DESC`;
- `(value_date, observed_on_utc DESC)`;
- `(major_status, observed_on_utc DESC)`;
- `(core_contracts_ready, observed_on_utc DESC)`;
- GIN on `feed_status_details` only when production query requirements justify it.

Operational inserts are append-oriented. Full CRUD actor APIs are nevertheless required as approved. Update and delete require optimistic concurrency and an administrative actor identity. Production authorization and retention policy are future administrative work.

### 5.5 Transactional behavior

- Current-contract role replacement uses a PostgreSQL transaction with row locks and serializable behavior where cross-role ordering is affected.
- The two VX role changes commit atomically.
- Watchdog observation insertion is one transaction containing the whole bulk snapshot.
- A contract rollover observation may be committed in the same PostgreSQL transaction as the assignment replacement when correlation must be exact.
- PostgreSQL unavailability during startup prevents authoritative current-contract establishment and fails startup closed.
- PostgreSQL watchdog-log failure after feeds are already Up produces Orange observability degradation and bounded retry; it does not falsely report a native feed as Down.

## 6. Native bulk watchdog ABI

### 6.1 One-call contract

Both C++ and Rust expose the same ABI version and function, conceptually:

```text
dbf_get_watchdog_snapshot_v1(
    dbf_watchdog_snapshot_v1* snapshot,
    dbf_watchdog_feed_status_v1* entries,
    uint32_t entry_capacity)
```

The managed caller rents or reuses a fixed-capacity buffer sized to the configured maximum feed count. One P/Invoke transition returns the snapshot header and all active entries. Buffer exhaustion is an explicit incomplete-snapshot failure; it must never silently truncate a feed.

### 6.2 Native registry

The selected native library maintains a process-wide registry of feed instances:

- register when a feed reaches startup admission;
- retain while Starting, Running, Resetting, Stopping, Stopped, or Faulted until terminal capture;
- unregister only during explicit destruction after the managed service has captured terminal health;
- protect enumeration and destruction with a safe lifetime lease;
- use per-feed atomics for hot counters and bounded locks for terminal error text;
- never invoke a managed callback from native watchdog code.

Only one backend is loaded in one process. The current Windows API runtime uses the C++ live DLL; Rust remains an ABI-compatible alternative.

No application service may reference a C++-specific or Rust-specific feed interface. The selected library supplies the same stable exported C ABI beneath the existing managed interop layer. Data waiting, batch reads, lifecycle operations, terminal-error reads, statistics, and the bulk watchdog snapshot therefore remain single P/Invoke API calls from the managed application's perspective. The application does not call both backends for one live feed and does not branch its market-data behavior by backend.

Backend selection is a deployment/startup decision and is immutable for the lifetime of the process. It must be explicit in configuration and included in every watchdog observation. Automatic in-process failover from C++ to Rust, or Rust to C++, is prohibited because it would obscure failure attribution and create an unqualified mixed execution path. A backend change requires a controlled service restart and a new native generation.

### 6.3 Native entry fields

Each native entry includes:

- stable native feed instance ID;
- native generation ID;
- dataset identity;
- feed kind;
- major status `Up`, `Resetting`, or `Down`;
- detailed native `FeedState`;
- terminal status;
- bounded terminal error;
- producer-thread alive/completed state;
- transport authenticated/running state;
- expected and received subscription acknowledgements;
- heartbeat count;
- provider-message count;
- age of last heartbeat/provider message;
- records produced and consumed;
- native ring capacity, use, high-water and overruns;
- snapshot observation timestamp/monotonic reference.

Native `Up` requires a Running transport, OK terminal status, live producer, acknowledged subscriptions, and a current provider heartbeat. Native `Resetting` covers native Starting, ConsumerSetup, Stopping, or an explicitly supplied recovery transition. Native `Down` covers Stopped, Faulted, terminal failure, dead producer, or failed health enumeration.

The managed service retains `Resetting` across the brief interval in which the old handle has been disposed and a replacement handle does not yet exist.

## 7. Feed registry and criticality

The managed registry enriches native feed instances with domain metadata:

- value date;
- dataset;
- subscribed contract IDs;
- contract roles;
- feed type;
- criticality;
- aggregation worker state;
- route ownership;
- hot-cache status;
- generation fencing token.

Criticality rules:

| Feed content | Classification | Exhausted failure |
|---|---|---|
| ES quarterly | Core | Stop all Databento feeds; Red |
| VX front month | Core | Stop all Databento feeds; Red |
| VX second month | Core | Stop all Databento feeds; Red |
| Option chain or individual option | Noncritical | Isolate failed feed; Orange |
| Future auxiliary feed | Configured policy | Default noncritical until explicitly promoted |

If one native dataset connection contains any core role, that connection is core. A failure of the shared native connection affects every role carried by it and follows the core rule.

## 8. DatabentoMarketDataWatchdogService

### 8.1 Ownership invariant

Only `DatabentoMarketDataWatchdogService` may mutate the Databento lifecycle. Existing startup services and actor handlers become request adapters. Direct calls to start, stop, or reset from other production components are prohibited by architecture tests.

The process-local `MarketOutlookHotCache` is available immediately and is not lifecycle-coordinated
by the watchdog. The watchdog may write feed-health facts as one independent partial input, but it
cannot activate, clear, fence or reject Market Outlook state. The cache has no Databento start,
stop, reset or rollover capability.

### 8.2 Serialized operation queue

The service owns one serialized lifecycle queue for:

- scheduled session start;
- startup qualification;
- value-date rollover;
- one-minute watchdog poll;
- automatic recovery;
- manual reset event;
- requested stop;
- application shutdown.

This prevents overlapping native connections, duplicate epochs, reset/poll races, rollover/reset races, and stale-generation publication.

### 8.3 Initial startup

When the API starts:

1. Start the reset-event listener so requests can be queued.
2. Read the authoritative market-session snapshot.
3. If the session is intentionally Closed, record Inactive and wait for 18:00.
4. If a value-date session is active, execute the rollover process.
5. Require committed rows for `EsQuarterly`, `VxFrontMonth`, and `VxSecondMonth`.
6. Validate every copied contract against the read-only source catalog.
7. Build the immutable runtime contract registry from committed PostgreSQL rows.
8. Create the Databento epoch and native feeds.
9. Subscribe all three core contracts and required data schemas.
10. Start native transport and managed aggregation.
11. Acquire stable watchdog/service route ownership for all three core roles.
12. Obtain one bulk native watchdog snapshot.
13. Confirm native transport, subscription acknowledgements, provider heartbeat, workers, routes, and hot-cache authority.
14. Persist the startup observation.
15. Publish authoritative readiness.
16. Publish the active value date, currently traded contracts and explicit feed-health facts to
    their consumers; native generation remains feed-lifecycle diagnostics only.
17. Confirm the immediately available Market Outlook cache can answer typed current or unavailable
    queries. No cache activation or current-generation admission check exists.
18. Begin the one-minute timer after startup reaches Up or a terminal Down result.

Market records are not required to prove connectivity during quiet periods. A valid provider heartbeat plus acknowledged subscriptions proves transport health. Live-data freshness remains a separate managed signal.

If startup fails before a native handle exists, the service persists a synthetic Down detail containing the failing stage, intended dataset/contracts, exception, value date, and attempt. If a handle exists, it first captures the native bulk snapshot and terminal detail, then disposes the generation.

### 8.4 One-minute watchdog tick

For every tick during an active value-date session:

1. Enter the serialized queue.
2. Execute one bulk native watchdog P/Invoke.
3. Join native entries to managed feed metadata.
4. Evaluate all three core roles and every optional feed.
5. Evaluate live-data freshness when in LiveTrading.
6. Determine effective major status and display health.
7. Persist one `watchdog_status_log` row.
8. Publish the typed watchdog observation event.
9. Trigger recovery when required.

Native terminal signaling may request an immediate out-of-cycle watchdog evaluation. The one-minute cadence is the durable heartbeat, not a requirement to delay known terminal recovery for up to 60 seconds.

### 8.5 Status and color model

| Effective state | Display | Meaning |
|---|---|---|
| Planned closed | Inactive | Scheduled 17:00-18:00/weekend inactivity |
| All core feeds Up and live data current | Green | Fully operational |
| Connections Up; live accepted data older than 5 and no more than 15 minutes | Yellow | Freshness intermittent |
| Recovery active | Orange | Resetting |
| Core feeds Up; optional feed exhausted and Down | Orange | Degraded optional capability |
| Watchdog persistence temporarily unavailable after readiness | Orange | Observability degraded |
| Any core feed exhausted recovery | Red | Complete Databento runtime Down |

During OffTrading, absent market records do not trigger recovery while provider heartbeats remain current. During LiveTrading, a core route with no accepted current data for more than 15 minutes is a recovery trigger even if the transport appears open. A native Stopped/Faulted state or missing heartbeat initiates Orange recovery immediately in any active value-date period.

### 8.6 Market Outlook cache coordination

The Market Outlook realtime actor and immediately available singleton cache implement the two
refresh behaviours defined by the `MOSC` correction plan:

1. every eligible component event updates its input slot, atomically refreshes the projection and
   publishes a UI notification; and
2. every structurally valid, correctly routed ES `New` trade captures all current input slots, recalculates all
   price-derived values, atomically replaces the projection and publishes a UI notification.

The cache is available as soon as the Market Data Service process constructs its singleton and remains
available for typed queries even when the Databento runtime is Resetting or Down. In those states it returns
the last immutable value with explicit stale/red health, or a typed unavailable result if no value
exists. A timer or watchdog observation never advances `UpdatedAtUtc` or `MarketDataAsOfUtc`.

Coordination rules are:

- startup/value-date/rollover/reset changes native lifecycle and health diagnostics only;
- every structurally valid, correctly routed partial update is latest-arrival-wins;
- contract/value-date identities remain isolated without activation or eviction side effects;
- cache replacement is process-local and is not written to either PostgreSQL or ScyllaDB;
- the first component may rebuild partial output under OR semantics;
- notification failure leaves the committed cache readable and does not affect feed readiness; and
- projection/calculator failure reports Market Outlook degradation but cannot request or trigger a
  Databento reset.

## 9. Rollover design

### 9.1 Startup rollover

Every background-service session startup performs rollover before starting Databento.

For each role:

1. Lock the authoritative PostgreSQL assignment rows.
2. Validate the current complete contract in the source catalog.
3. Reconcile copied descriptive fields and source fingerprint.
4. Determine whether rollover evaluation is due.
5. Resolve the eligible replacement according to the role:

   - ES selects the eligible quarterly contract;
   - VX front selects the eligible current/front month;
   - VX second selects the immediately following eligible VX month.

6. Require every replacement contract to exist completely in the source catalog.
7. Reject missing, duplicate, symbol-mismatched, or improperly ordered candidates.
8. Replace affected PostgreSQL assignments atomically.
9. Persist the correlated rollover observation.
10. Commit before updating the runtime registry or starting a feed.

The source contract catalog is never changed. If a required candidate is absent, the transaction is rolled back and startup fails closed.

VX front/second rollover is one coupled transaction. It is invalid for the front and second roles to identify the same contract or for the second maturity not to follow the front maturity.

### 9.2 Manual contract maintenance

The initial operator workflow is:

1. Manually create or update the complete futures contract in the existing source catalog.
2. Use the Market Data Service current-contract CRUD API to assign it explicitly, or allow startup rollover to select it.
3. The service validates and copies it into PostgreSQL.
4. The existing source contract remains unchanged.

### 9.3 Future on-demand rollover event

Document, but do not initially implement, a typed `MarketDataFuturesRolloverEvent` carrying:

- correlation and command identity;
- requested value date;
- requested roles/symbols;
- force-evaluation flag;
- requested-on and requested-by;
- reason.

The event listener submits the request to the same serialized lifecycle queue and invokes the same rollover transaction used at startup. Correlated complete/fail events report the result. NATS redelivery is idempotent by command/event identity.

An eventual command may be preferable as the external request, with `MarketDataFuturesRolloverEvent` representing the accepted fact. The implementation specification must preserve the repository's command/event conventions when this capability is scheduled.

## 10. Recovery and reset

### 10.1 Automatic recovery trigger

Recovery starts when:

- native state is Stopped or Faulted;
- terminal status is not OK;
- producer thread has completed unexpectedly;
- provider heartbeat is not current;
- bulk watchdog enumeration fails or is incomplete;
- a core managed aggregation worker has completed;
- a core live route has received no accepted current input for more than 15 minutes during LiveTrading.

### 10.2 Recovery budget

Each recovery attempt has these bounds:

- fence and graceful stop: up to 5 seconds;
- recreate, authenticate, subscribe, and start: up to 30 seconds;
- verify heartbeat, native state, workers, routes, and cache: up to 10 seconds.

Maximum attempt duration is 45 seconds. Wait 5 seconds before attempt two and 15 seconds before attempt three. The maximum three-attempt recovery phase is approximately 155 seconds.

Every attempt produces and persists a Resetting observation. Success returns the replacement generation to Up. No old generation may publish after its fence is installed.

### 10.3 Exhausted recovery

- Exhausted core failure: stop and dispose all Databento feeds, invalidate live hot-cache authority, latch aggregate Down/Red, and keep API/UI processes running.
- Exhausted optional failure: stop and isolate that feed, keep core feeds running, and publish Orange.
- The same failed epoch is not silently restarted by a legacy supervisor.
- A latched core Down state is cleared only by an explicit reset, application/session startup, or future authorized administrative action.

### 10.4 MarketDataFeedResetEvent

The background service owns an actor-listener adapter for `MarketDataFeedResetEvent`. The existing event actor delegates execution instead of directly calling `IMarketDataApi.StopAsync` and `StartAsync`.

Full reset behavior:

1. Deduplicate by command/event identity.
2. Queue behind any active lifecycle operation.
3. Persist and publish Resetting.
4. Fence all current publication.
5. Capture final bulk native health.
6. Stop and dispose all feeds and invalidate old cache authority.
7. Re-read authoritative value date.
8. Rerun startup rollover.
9. Rerun the complete initial startup process.
10. Restore core feeds first.
11. Restore previously active optional feed intents best-effort.
12. Persist and publish the terminal result.
13. Emit the existing correlated reset complete or fail event.

Event-carried contracts are audit inputs, not authority. PostgreSQL committed assignments and the current value-date authority determine the reset startup.

## 11. Readiness and UI behavior

### 11.1 Core readiness

`CurrentlyTradedMarketDataReady` is true only when:

- all three required role rows exist and validate;
- ES quarterly, VX front, and VX second source contracts exist;
- their native core feed connection is Up;
- the managed aggregation worker is running;
- required routes are actively owned;
- the last-price hot cache is active;
- the generation is not fenced or Resetting.

An optional-feed failure does not make core readiness false.

### 11.2 Navigation gate

While the service is expected to be operational, core readiness false causes the UI to:

- hide all application menus except System;
- suppress entry into market-dependent views;
- keep the process, status console, and System menu available;
- restore menus automatically only after a later authoritative Ready result.

The UI starts fail-closed until its first readiness query completes. It does not reconstruct readiness from local time or local feed events.

This gate is an unexpected critical-service readiness gate, not a market-hours gate. During intentional `Closed/Inactive` periods, existing read-only navigation remains available, preserving the established rule that normal market closure does not disable the application. If future policy explicitly requires System-only navigation during planned closures, that is a separate policy change.

### 11.3 Clickable watchdog history

The shell feed-health indicator becomes clickable. It opens a watchdog dialog backed by authoritative queries.

Upper observation list:

- observation time;
- operation reason;
- Up/Resetting/Down;
- Green/Yellow/Orange/Red/Inactive;
- core readiness;
- recovery attempt;
- counts of feeds by status;
- affected core roles.

Lower selected-observation detail:

- feed ID and generation;
- dataset and feed type;
- subscribed contract roles/IDs;
- core/optional classification;
- native and effective status;
- heartbeat/provider-message age;
- subscription acknowledgements;
- producer and aggregation-worker state;
- ring/counter information;
- terminal status/error;
- failure stage and detail.

The UI initially queries PostgreSQL-backed history through a typed NATS query and then consumes observation events for live updates. Missing events are reconciled by query.

## 12. Actor contracts

The detailed specification must define stable typed contracts for:

### Commands

- create/update/delete current-contract assignment;
- request market-data start/stop/reset;
- future request futures rollover;
- create/update/delete watchdog log administration.

### Events

- watchdog observation recorded;
- startup started/completed/failed;
- recovery started/attempted/completed/exhausted;
- current-contract assignment changed/deleted;
- reset started/completed/failed;
- future futures rollover started/completed/failed;
- core readiness changed;
- market-data generation activated/fenced; and
- versionless Market Outlook updated or became unavailable/stale.

### Queries

- get/list current-contract assignments;
- validate minimum current-contract set;
- get current Databento readiness;
- get latest watchdog observation;
- get watchdog observation by ID;
- list watchdog history by time/value date/status;
- get complete feed detail for one observation; and
- get current versionless Market Outlook hot-cache value/availability for a contract and value
  date.

All commands are idempotent and correlated. Queries do not call native code directly; they read the latest committed service state/history.

## 13. Observability and persistence

Each persisted observation identifies one of:

- `InitialStartup`;
- `ScheduledSessionStart`;
- `WatchdogPoll`;
- `AutomaticRecovery`;
- `ManualReset`;
- `ValueDateRollover`;
- `RequestedStop`;
- `ApplicationShutdown`.

The latest in-memory service diagnostics additionally expose Market Outlook cache availability,
last component refresh, last ES full refresh, received/written/composed/query counts and notification
failure counts. No accepted/rejected or active-generation cache counters exist. These fields help
correlate feed health with the derived UI projection but are not
persisted as Market Outlook state. Watchdog history may record the worker's health/status as
diagnostic detail; it must not serialize the Market Outlook payload.

Status console and structured logs include observation/correlation IDs rather than duplicating the entire JSON detail. Distributed trace context is propagated through actor requests and lifecycle operations.

Initial persistence has no automatic deletion. Production work must add retention, partitioning/archive strategy, administrative authorization, and capacity alerts. At one row per minute plus lifecycle transitions, ordinary volume is modest, but JSONB size and option-feed count must be measured before setting retention.

## 14. Security and operational controls

- Databento API credentials remain native/service secrets and are never stored in watchdog JSON.
- Terminal error text is bounded and scrubbed before persistence.
- CRUD commands record actor identity and correlation.
- Current-contract mutations use optimistic concurrency.
- Administrative watchdog-log update/delete operations are auditable even though the production authorization UI is out of scope.
- P/Invoke buffers are fixed, bounds-checked, ABI-versioned, and fail closed on truncation or mismatch.
- Native registry enumeration cannot race handle destruction.

## 15. Testing and qualification requirements

### 15.1 Native C++ and Rust parity

- ABI layout, packing, size, version, and enum parity.
- Identical exported function names and calling conventions for every managed P/Invoke operation.
- Identical normalized quote, trade, statistics, replay-marker, lifecycle, terminal-error, statistics, and bulk-watchdog semantics.
- Empty, one-feed, maximum-capacity, and overflow snapshots.
- Concurrent create/start/stop/fault/destroy during enumeration.
- Heartbeat, terminal error, producer completion, and counter accuracy.
- No use-after-free and no silent truncation.
- C++ and Rust return semantically identical fixture snapshots.

### 15.2 Backend selection and soak qualification

No preferred native implementation is chosen by this design. C++ and Rust must be qualified independently with the same managed binaries and test workloads.

Qualification evidence for each backend must include:

- deterministic native unit and ABI conformance suites;
- managed/native integration suites with no backend-specific expectations;
- connection loss, heartbeat timeout, slow-reader, terminal fault, reset, and three-attempt recovery injection;
- full value-date-session runs spanning OffTrading and LiveTrading boundaries;
- repeated 18:00 startup, 03:00 live-health reset, 16:00 off-trading transition, and 17:00 shutdown boundaries;
- sustained dev and paper-trading observation over many complete market-data sessions;
- record counts, sequence continuity, unconditional cache writes, and watchdog snapshots reconciled against the provider stream;
- CPU, allocation, native/managed memory growth, handle growth, ring occupancy, backlog, and P/Invoke latency measurements;
- reset recovery time and post-recovery continuity evidence;
- clean shutdown, value-date rollover, and restart evidence;
- zero unexplained native termination, silent worker completion, missing feed registry entry, or incomplete watchdog snapshot.

Performance comparison must use equivalent release builds, machine placement, Databento subscriptions, market hours, datasets, contract roles, affinity policy, buffers, and managed application version. Debug-build results cannot select the production backend.

Development and paper-trading soak evidence informs the eventual backend decision; it does not automatically promote either implementation. Production selection requires an explicit later approval recorded with the tested native artifact version and qualification evidence.

### 15.3 Unit tests

- Three-role validation and missing-role rejection.
- ES quarterly and VX front/second ordering.
- Complete source-contract requirement.
- Source catalog remains unchanged for every CRUD/rollover operation.
- Optimistic concurrency and idempotency.
- Up/Resetting/Down mapping and display precedence.
- Core versus optional exhausted-failure decisions.
- Exact three-attempt recovery timing under `FakeTimeProvider`.
- Menu readiness policy, including planned Closed versus unexpected Down.

### 15.4 PostgreSQL integration tests

- Schema creation and migrations.
- Full CRUD for both required tables.
- Sequence-generated watchdog IDs.
- Serializable VX paired rollover under concurrent writers.
- Rollback on missing source contract, duplicate contract, invalid ordering, or stale row version.
- Atomic watchdog JSONB insertion and query ordering.
- Persistence failure classification and retry.
- Randomized isolated test rows with complete cleanup.

### 15.5 Actor and NATS integration tests

- Commands/events/queries round-trip typed DTOs.
- Duplicate and delayed reset events are idempotent.
- Reset completion/failure correlates with persisted observations.
- Lost UI observation event reconciles from history query.
- Future rollover-event contract can be added without bypassing lifecycle serialization.
- A component-only update refreshes the Market Outlook cache and publishes a typed UI notification.
- Every accepted ES trade performs a full Market Outlook refresh and publishes the current cached
  DTO.
- Market Outlook queries return the same immutable cache value represented by the latest
  notification.
- Reset and rollover health changes never suppress a correctly routed Market Outlook input.

### 15.6 Runtime/process tests

- API starts during OffTrading and qualifies all three core contracts from provider heartbeat without requiring a trade.
- Process runs overnight while watchdog rows continue once per minute.
- Native heartbeat timeout causes immediate Orange and replacement.
- Native worker completion with epoch object still present is detected and repaired.
- Three failed core recoveries stop all feeds and latch Red.
- Failed option feed leaves core feeds Up and aggregate Orange.
- PostgreSQL unavailable at startup fails closed.
- PostgreSQL watchdog insert failure after startup leaves feeds Up but health Orange.
- Value-date rollover cannot race reset or a watchdog poll.
- Market Outlook cache is immediately available with the Market Data Service process and requires no hosted-worker activation.
- Market Outlook calculator or notification failure does not stop/reset Databento or alter native
  feed status.
- Databento reset changes health independently; correctly routed events continue rebuilding partial
  then complete display state without a generation admission fence.
- API restart rebuilds Market Outlook from authoritative component caches without reading a
  persisted Market Outlook snapshot.

### 15.7 UI verification

- Clickable indicator loads PostgreSQL-backed observation history.
- Selecting an observation renders all native and managed feed details.
- All menus except System are hidden during unexpected core NotReady/Resetting/Down.
- Menus return after authoritative recovery.
- Optional-feed Down remains Orange without hiding menus.
- Planned Closed state preserves established read-only navigation.
- UI restart reconstructs current status and history from queries without waiting for a new event.
- RSI, TDI, ITI, VX and other eligible component updates can refresh Market Outlook without waiting
  for an ES trade.
- Every accepted ES trade refreshes the current price, open-to-current percentage, provisional
  EMA/Bollinger and MDI presentation.
- Feed Resetting/Down leaves the last Market Outlook values visibly stale/orange or stale/red and
  never presents a watchdog tick as a market-data refresh.

## 16. Acceptance criteria

The design is successfully implemented only when:

1. PostgreSQL contains valid authoritative rows for `EsQuarterly`, `VxFrontMonth`, and `VxSecondMonth`.
2. No Market Data Service CRUD or rollover operation mutates the existing source contract catalog.
3. Only the watchdog service mutates Databento lifecycle state.
4. One bulk P/Invoke call returns complete status for every started/current native feed.
5. Every active-session minute produces one persisted watchdog observation, subject to explicit persisted failure reporting and retry.
6. Native terminal faults cause immediate serialized recovery without waiting for the next minute.
7. Recovery attempts exactly three times and never overlaps native generations.
8. Exhausted failure of any of the three core roles stops all Databento feeds and reports Red/Down.
9. Exhausted option/auxiliary failure isolates that feed and reports Orange while core readiness remains true.
10. Full reset reruns authoritative rollover and the complete initial startup process.
11. UI menu availability follows authoritative core readiness and is recoverable without restarting the UI.
12. Watchdog history is clickable, queryable, and detailed enough to identify the failed feed, native reason, recovery attempt, and lifecycle stage.
13. C++ and Rust pass identical native watchdog contract tests.
14. The component can move into a future Aspire-hosted Market Data service without changing actor-facing commands, events, or queries.
15. The same managed market-data binaries operate against either native backend through the same exported ABI without conditional application behavior.
16. No native backend is selected for production until both have completed equivalent dev/paper-trading qualification over many full market-data sessions and a later explicit approval records the selection.
17. The Market Data Service process hosts an immediately writable versionless Market Outlook
    singleton used by its realtime/query actors; watchdog lifecycle state never gates cache writes.
18. Component events and accepted ES trades implement the two approved Market Outlook refresh
    behaviours without PostgreSQL event state or ScyllaDB Market Outlook persistence.
19. Market Outlook failure cannot mutate or reset Databento, and Databento health/readiness never
    depends on successful Market Outlook calculation or UI notification.
20. Reset, rollover and native-generation replacement update explicit feed health independently,
    while typed Market Outlook queries remain available with current, stale or unavailable analytics.

## 17. Explicit non-goals for the initial implementation

- Building the dedicated Aspire AppHost or deploying a separate Market Data process.
- Building Market Data administrative tools under the System menu.
- Automating production watchdog retention or archival.
- Moving or rewriting the existing complete futures-contract catalog.
- Mutating legacy source `CurrentlyTraded` flags from the new service.
- Implementing the on-demand `MarketDataFuturesRolloverEvent` workflow; only its contract and ownership are designed here.
- Making option-feed availability critical to global Databento readiness.
- Persisting raw credentials, secrets, or unbounded native error payloads.
- Persisting the derived Market Outlook cache or making it authoritative for any source signal.
- Allowing Market Outlook calculation, query or notification failures to initiate Databento
  recovery.

## 18. Follow-on documents

After approval, create:

1. a detailed implementation specification defining PostgreSQL DDL, DTOs, actor subjects, P/Invoke ABI structs, service interfaces, state machines, and migrations;
2. a gated implementation plan covering native C++, native Rust, managed storage, lifecycle service, actors, UI, and all qualification suites;
3. retain the completed `Market-Outlook-Simple-Hot-Cache-Correction-Implementation-Plan-v1.0.md`
   model when the cache is moved to this service boundary; and
4. an Aspire extraction specification when the dedicated Market Data service is scheduled.
