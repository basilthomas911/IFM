# Market Outlook Hot-Cache Refactor Implementation Plan v1.0

> **Superseded cache-lifecycle record.** `MOSC` replaces this document's generation activation,
> source-order admission, stale-input rejection, activation polling, generation-driven clearing
> and dedicated `MarketOutlookHotCacheService` requirements. The retained design is a
> non-authoritative, process-local, immutable whole-snapshot projection with independent component
> refresh, ES-trade recalculation, typed NATS query/notification contracts and no persistence.

| Item | Value |
| --- | --- |
| Plan ID | `MOHC` |
| Status | Partially superseded by `Market-Outlook-Simple-Hot-Cache-Correction-Implementation-Plan-v1.0.md` |
| Date | 2026-09-01 |
| Scope | Replace the versioned, event-sourced Market Outlook projection with a versionless, process-local hot-cache projection |
| Initial host | Separate `MarketOutlookHotCacheService` worker hosted by `TomasAI.IFM.Application.Api.Server` |
| Target host | The same worker inside the future Aspire-orchestrated dedicated Market Data Service |
| Supersedes | The Market Outlook command-state, event-projector, persisted-snapshot and revision workflow described by the `MOR` and `LMO` plans |
| Retains | Historical indicator ownership, canonical market-data admission, source ordering, NATS UI notification and independent OR-component admission |
| Explicitly out of scope | Implementing the Databento native watchdog, reset orchestration and feed-service persistence; the lifecycle-coordination interface used by Market Outlook is in scope |

## 1. Objective

Market Outlook shall be a non-authoritative, latest-value display projection assembled from the
authoritative market-data and analytics hot caches. It shall not be an event-sourced aggregate,
carry an aggregate revision, or persist its own working state or display snapshot.

The refactor shall implement both approved refresh behaviours:

1. **Independent component refresh:** every valid RSI, TDI, ITI, VX, EOD/session, EMA, Bollinger or
   Futures Trade Signal component update shall update its cache slot, atomically refresh the
   Market Outlook projection, and publish a UI notification without waiting for another component.
2. **ES-trade full refresh:** every accepted normalized ES `New` trade shall capture the latest
   component inputs, recalculate all price-derived values, atomically replace the current Market
   Outlook projection, and publish a UI notification. No minimum price-change threshold applies.

Missing, warming, stale or invalid components shall never block valid siblings. Expected
unavailability is represented as typed availability and `N/A`, not as an exception.

## 2. Binding architectural decisions

1. The Market Outlook runtime owner is one singleton `MarketOutlookHotCacheService` hosted worker
   in the current API process. It is not part of the UI process.
2. Cache identity is `(ES contract ID, value date)`; value-date and contract rollover boundaries
   cannot reuse an entry from the prior identity.
3. The public Market Outlook DTO has no aggregate `Revision` and no collecting/published lifecycle.
4. `UpdatedAtUtc` identifies when the projection was replaced. `MarketDataAsOfUtc` identifies the
   newest accepted market observation represented by it. Neither field is a version.
5. Individual cache slots retain only the source identity, source sequence/event time, stream epoch
   and freshness required to reject duplicate, delayed or regressive input. This is source ordering,
   not Market Outlook aggregate versioning.
6. Cache entries are immutable. Readers receive one atomic reference and can never observe an
   object being mutated in place.
7. Market Outlook does not own durable signal calculations. RSI, TDI, ITI, EOD/session state,
   EMA/Bollinger baselines, VX and Futures Trade Signal remain owned by their existing operators.
8. Price-derived live previews may reuse their owners' pure calculators, but Market Outlook cannot
   append completed observations or become an alternative authoritative signal store.
9. The initial Market Outlook query reads the owning hot cache. It does not fall back to a stale
   persisted Market Outlook snapshot.
10. Existing authoritative component stores may be used to warm their respective caches at
    startup. Market Outlook itself is rebuilt rather than restored.
11. NATS remains the transport for actor input, typed query and UI notification. Internal
    component-command/event/projector round trips are removed.
12. UI publication may coalesce a burst to the newest cache value, but backend ES recalculation and
    cache replacement occur for every accepted trade.
13. Feed health is not inferred from Market Outlook activity. The canonical Databento health path
    supplies feed state and age. A stopped ES feed leaves the last display values visible but marks
    them stale/red and never advances their timestamps.
14. No migration or compatibility support is required for legacy persisted Market Outlook rows.
    Physical legacy data may remain untouched until an explicitly approved cleanup operation.
15. `MarketOutlookHotCacheService` and `DatabentoMarketDataWatchdogService` are separate hosted
    workers inside the same Market Data Service boundary. Only the watchdog owns Databento start,
    stop, reset, recovery and rollover.
16. The watchdog coordinates Market Outlook activation, value-date/contract/native-generation
    fencing and shutdown through `IMarketDataGenerationAuthority`; Market Outlook cannot invoke a
    Databento lifecycle mutation.
17. Until the watchdog implementation is available, an API-server adapter over the existing
    session/epoch authority supplies the same generation contract. Replacing that adapter with the
    watchdog authority cannot change Market Outlook or UI actor contracts.
18. The API Server is only the initial executable host. The worker's implementation cannot depend
    on API endpoints, WinForms, or API-server-specific state, allowing it to move unchanged into the
    dedicated Aspire Market Data Service.

## 3. Target runtime flow

```text
RSI / TDI / ITI / VX / session / EMA-BB / trade-signal component event
  -> component-specific validation
  -> per-slot duplicate and stale-input rejection
  -> atomic MarketOutlookInputState replacement
  -> compose current MarketOutlookReadModel
  -> atomic MarketOutlookHotCache replacement
  -> latest-value NATS UI notification

accepted normalized ES New trade
  -> canonical ES last-price cache already accepted
  -> update ES price/session slot
  -> atomically capture all current component slots
  -> calculate current price and open-to-current percentage
  -> recalculate provisional Daily EMA and Bollinger values
  -> calculate live MDI presentation from the same Bollinger result
  -> compose current MarketOutlookReadModel with partial availability
  -> atomic MarketOutlookHotCache replacement
  -> latest-value NATS UI notification

initial UI/API query
  -> typed NATS query to Market Outlook owner
  -> current hot-cache entry or typed unavailable result
```

### 3.1 Current and future hosting

```text
Initial deployment

TomasAI.IFM.Application.Api.Server process
  +-- DatabentoMarketDataWatchdogService / interim generation authority
  +-- MarketOutlookHotCacheService
  +-- NATS actor query/event adapters

Future deployment

.NET Aspire AppHost
  +-- Dedicated Market Data Service process
        +-- DatabentoMarketDataWatchdogService
        +-- MarketOutlookHotCacheService
        +-- NATS actor query/event adapters

API Server and UI
  +-- typed NATS clients only
```

The move to Aspire is a hosting/configuration change. The shared DTOs, actor subjects, calculation
rules, cache implementation and UI integration remain unchanged.

## 4. Target contracts and ownership

### 4.1 Public display contract

Replace `MarketOutlookSnapshotReadModel` with a versionless `MarketOutlookReadModel` containing at
least:

- contract ID and value date;
- `UpdatedAtUtc`, `MarketDataAsOfUtc` and the refresh trigger;
- ES session open/high/low/current price/volume and open-to-current percentage;
- Futures Trade Signal/MDI display values;
- RSI and TDI values;
- the three ITI milestone values and latest accepted four-mode ITI trend value;
- current VX value;
- provisional EMA and Bollinger values with baseline and live-price provenance;
- per-component availability/freshness and aggregate feed health; and
- `MissingInputs` as presentation information only, never a composite admission gate.

### 4.2 Internal cache contracts

Introduce:

- `IMarketOutlookHotCache` for atomic current-projection reads and replacements;
- `IMarketOutlookInputCache` for immutable component-slot updates and captures;
- `MarketOutlookInputState` containing the latest eligible component slots;
- `MarketOutlookSourcePosition` containing only source-ordering/freshness metadata;
- `MarketOutlookComposer` as a pure projection function;
- `MarketOutlookRefreshTrigger` identifying component and ES-trade refreshes; and
- typed cache/query results that distinguish `Available`, `Warming`, `Stale`, `Invalid` and
  `Unavailable` without exceptions.

The cache owner may use an entity-keyed lock or compare/exchange loop. The correctness requirement
is atomic immutable replacement, not a particular synchronization primitive.

### 4.3 Physical code placement and dependency direction

| Project/location | Responsibility |
| --- | --- |
| `TomasAI.IFM.Domain.MarketData.Analytics.Shared/MarketOutlook` | Versionless DTOs, availability/provenance contracts, NATS events and typed queries |
| `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlook` | Pure composer, component eligibility and ES price-derived calculations |
| `TomasAI.IFM.Application.MarketData/MarketOutlook` | `MarketOutlookHotCacheService`, immutable caches, generation fencing and actor adapters |
| `TomasAI.IFM.Application.Api.Server` | Initial hosted-service registration, configuration and interim/watchdog authority binding only |
| Future dedicated Market Data Service executable | Aspire-era registration and configuration using the same application/domain components |
| `TomasAI.IFM.UI.EventConsumer` and `TomasAI.IFM.UI.Net` | Typed query/notification consumption and presentation only |

The dependency direction is host -> application worker -> domain composer -> shared contracts. The
domain and application implementations cannot reference the API Server or WinForms projects.

### 4.4 Hosted-worker lifecycle

On API Server startup, `MarketOutlookHotCacheService` shall:

1. start its typed component, ES-price, generation-fence and query listeners;
2. read `IMarketDataGenerationAuthority` without starting or changing Databento;
3. activate the current `(value date, ES contract, native generation)` fence when available, or
   answer queries as unavailable while waiting;
4. accept only inputs matching the active fence;
5. rebuild partial/current output from authoritative input caches; and
6. remain available for stale/unavailable queries while the feed owner is Resetting or Down.

On a generation/value-date/contract transition, it atomically fences the old identity before
accepting the replacement identity. On host shutdown, it stops its listeners, rejects further
writes and clears only its process-local derived state. It never calls feed lifecycle APIs during
startup, transition or shutdown.

## 5. Calculation rules preserved by the refactor

1. ES percentage change is stored as `(current trade - session open) / session open`; the UI's
   percentage formatter applies the display multiplication by 100.
2. Every accepted ES `New` trade recalculates the live preview, including same-price trades and the
   smallest supported price increment.
3. Daily EMA and Bollinger previews use the last committed completed-session baseline and replace
   the provisional current-session close. They never advance the completed Daily observation count.
4. MDI uses the approved Bollinger-position calculation and approved 30/60 presentation limits.
5. RSI uses the latest warm, valid `FifteenSeconds` period-13 cache value.
6. TDI uses the approved standard configuration and remains optional while warming.
7. ITI accepts exactly `Trending`, `TrendDirectionChanged`, `TrendExtremeChanged` and
   `TrendReversalChanged`; all four can update the latest trend and Trend Delta.
8. VX and every other optional component progress independently under OR admission.
9. A component update cannot manufacture or advance an unrelated component.
10. No expected missing/stale/warming input throws a runtime exception.

## 6. Implementation gates

### MOHC-00 - Baseline characterization and documentation freeze

Deliverables:

- map the current component-event, command, PostgreSQL event-state, Scylla projection, realtime
  completion, query and UI notification path;
- capture current public DTO fields, MessagePack contracts, query URI and UI mappings;
- identify every producer of Market Outlook component changes;
- capture the current API Server hosted-service registration and define the future Aspire extraction
  dependency boundary;
- add failing-first characterizations for the two target refresh behaviours; and
- mark the superseded Market Outlook sections of prior documents without changing unrelated signal
  or Databento ownership.

Exit tests:

- architecture test enumerates every current Market Outlook producer and consumer;
- characterization tests demonstrate the current persistence/revision dependency; and
- documentation cross-reference validation passes.

### MOHC-01 - Versionless contracts and availability model

Deliverables:

- introduce `MarketOutlookReadModel`, input-state, source-position, availability and refresh-trigger
  contracts;
- remove aggregate revision and collecting/published state from the target contract;
- define explicit timestamps and provenance for the assembled view and provisional calculations;
- define typed unavailable query behaviour; and
- update serialization contracts without requiring legacy Market Outlook row compatibility.

Exit tests:

- unit tests cover construction, serialization, defaults and all availability states;
- contract tests prove there is no aggregate revision field;
- BDD verifies missing optional inputs produce partial output rather than failure; and
- API/UI contract compilation succeeds.

### MOHC-02 - API-hosted worker and atomic hot caches

Deliverables:

- implement entity-keyed singleton input and projection caches;
- implement `MarketOutlookHotCacheService` under `TomasAI.IFM.Application.MarketData/MarketOutlook`
  as a separate hosted worker;
- register that worker in the API Server without placing calculation/cache implementation in the
  API Server project;
- use immutable replacement for every write and atomic reference capture for every read;
- enforce per-component source ordering and stream-epoch isolation;
- add explicit value-date eviction/reset; and
- expose cache counters and last-update timestamps without persistence.

Exit tests:

- unit tests cover add, replace, duplicate, delayed, epoch-change, value-date-change and clear;
- concurrency tests prove readers never observe torn component or projection state;
- stress test runs simultaneous writers/readers with deterministic final state; and
- allocation/capacity tests prove bounded state per configured entity;
- dependency-injection validation proves the worker and caches are singleton-owned; and
- architecture tests reject API Server, UI or database dependencies from the application/domain
  cache implementation.

### MOHC-03 - Pure Market Outlook composer

Deliverables:

- extract one pure composer from the current command handlers and realtime preview path;
- compose all valid sibling inputs using OR semantics;
- calculate `MissingInputs`, completeness and component availability as presentation metadata;
- consume Futures Trade Signal from its owner or invoke the same pure calculator without creating a
  second authoritative decision; and
- produce an immutable `MarketOutlookReadModel` without database or messaging dependencies.

Exit tests:

- unit tests cover every individual component and all required calculation boundaries;
- pairwise verification covers representative valid component combinations;
- the existing 127 non-empty component availability masks remain valid where applicable; and
- deterministic fixtures prove the composer does not mutate input objects.

### MOHC-04 - Independent component refresh behaviour

Deliverables:

- route valid RSI, TDI, all four ITI trend modes, VX, EOD/session, EMA, Bollinger and Futures Trade
  Signal updates directly to the input cache owner;
- refresh the assembled projection immediately after each accepted component update;
- notify the UI after the atomic projection replacement;
- ignore an invalid sibling while accepting valid siblings in the same composite event; and
- remove component-to-observe-command request/reply dependencies from this path.

Exit tests:

- BDD scenario for each component proves it refreshes without an ES trade;
- unit tests cover every admitted and excluded ITI mode;
- OR-composite tests prove invalid/missing siblings do not suppress a valid update;
- integration tests prove component event -> cache -> notification; and
- no expected rejection produces a first-chance exception.

### MOHC-05 - ES-trade full-refresh behaviour

Deliverables:

- consume every canonically accepted normalized ES `New` trade event;
- capture all current component slots and ES session state;
- recalculate open-to-current percentage, provisional EMA/Bollinger and live MDI;
- atomically replace the projection for every accepted trade; and
- preserve calculation-per-trade while allowing bounded latest-value UI delivery.

Exit tests:

- unit tests cover first trade, same-price trade, minimum tick, duplicate, stale, correction,
  cancellation, wrong contract, wrong value date and invalid stream epoch;
- reference-calculator tests verify percentage, EMA, Bollinger and MDI boundaries;
- BDD proves every accepted trade triggers calculation and cache replacement;
- verification proves 10,000 intraday trades do not advance committed Daily state; and
- integration proves ES event -> full compose -> hot cache -> NATS notification.

### MOHC-06 - Typed query and UI notification cutover

Deliverables:

- replace the persisted-snapshot query with a typed query to the Market Outlook cache owner;
- return typed unavailable/warming state when no current entry exists;
- retain or rename `MarketOutlookUpdatedNotifyEvent` with the versionless payload;
- make the initial query and live notification use the same DTO and presentation rules;
- update the WinForms view model to stop revision comparisons; and
- marshal notifications safely onto the UI thread while retaining latest-value coalescing.

Exit tests:

- query unit tests cover current, absent, stale and wrong-value-date entries;
- NATS integration verifies request/reply and notification serialization;
- UI binding tests cover both refresh triggers and every partial availability state;
- verification proves query and notification expose the same committed cache value; and
- UI remains usable while feed/component status is degraded.

### MOHC-07 - Startup warmup, value-date transition and contract rollover

Deliverables:

- populate authoritative component caches through their existing startup/warmup owners;
- introduce `IMarketDataGenerationAuthority` and an initial adapter over the current API-hosted
  market-session/epoch state;
- coordinate worker activation, fencing and shutdown without granting it Databento lifecycle
  mutation authority;
- build Market Outlook only from current component caches, never a persisted Market Outlook row;
- reset or create the correct cache identity at value-date transition;
- prevent the prior ES contract/value date from supplying current-session values after rollover;
- allow the first ES trade to build a useful partial projection while optional inputs warm; and
- preserve completed historical EMA/Bollinger baselines across the transient Market Outlook reset.

Exit tests:

- BDD covers cold start, warm start, missed development days, value-date change and ES rollover;
- integration proves restart rebuilds from component owners without Market Outlook persistence;
- integration proves delayed inputs from a fenced native generation cannot update the cache;
- architecture tests prove only the Databento lifecycle owner can start, stop, reset or roll feeds;
- stale prior-session and prior-contract inputs are rejected; and
- first-trade partial output is published without exception.

### MOHC-08 - Remove the event-sourced and persisted Market Outlook path

Deliverables:

- remove the Market Outlook command actor, command context and state repository;
- remove observe/publish commands and Market Outlook-specific state transition events;
- remove the Market Outlook event projector and its registrations;
- remove Market Outlook snapshot reads/upserts from the active `MarketDataDb` path;
- remove obsolete working-state, watermark and revision contracts; and
- leave physical legacy database rows untouched unless separately authorized.

Exit tests:

- architecture tests prove no live Market Outlook dependency on PostgreSQL event state or Scylla;
- dependency-injection validation succeeds without the removed actor/projector services;
- API Server startup validation succeeds with the new application-layer hosted worker registration;
- repository search finds no active observe/publish command route;
- API and all affected projects build with zero errors; and
- no unrelated analytics persistence path is removed.

### MOHC-09 - Freshness, health and diagnostics

Deliverables:

- attach component age/availability and canonical ES/VX feed health to the display projection;
- expose component-update, ES-refresh, stale-rejection, notification-coalescing and query counters;
- distinguish `cache current but source stale` from `cache absent`;
- never advance Market Outlook timestamps from a timer or health poll; and
- prepare a clean health-provider seam for the later Databento watchdog implementation.

Exit tests:

- unit tests cover green/yellow/red/inactive and component freshness boundaries;
- BDD proves a stopped ES source leaves values visible but stale/red;
- verification proves health polling cannot fabricate a data refresh;
- diagnostics identify the last successful trigger and input timestamps; and
- watchdog interfaces are not implemented or coupled in this plan.

### MOHC-10 - Concurrency, throughput and failure qualification

Deliverables:

- inject concurrent component and ES updates, duplicate delivery, reordering and burst traffic;
- verify bounded latest-value UI publication under sustained ES activity;
- ensure a notification failure does not corrupt or roll back the committed hot-cache value;
- ensure a malformed component cannot terminate the cache owner; and
- prove Market Outlook calculation/query/notification failure cannot stop or reset Databento; and
- define explicit resource and latency thresholds for the development workstation profile.

Exit tests:

- sustained and burst performance tests complete without unbounded mailbox growth;
- concurrent stress tests retain atomic valid state and deterministic source ordering;
- fault injection covers calculator, notification and query failures independently;
- lifecycle-spy verification records zero Databento mutations from every Market Outlook failure;
- cache remains readable after a best-effort notification failure; and
- no expected malformed/stale/duplicate input throws outside its actor boundary.

### MOHC-11 - End-to-end and interactive UI acceptance

Deliverables:

- run the API, NATS, component producers and WinForms Market Outlook together;
- run `MarketOutlookHotCacheService` as a separately registered API Server hosted worker;
- verify immediate component-only refresh and ES-triggered full refresh visually and through typed
  probes;
- verify all Market Outlook rows, `N/A` states, timestamps and health indications;
- verify UI open/close/reopen and listener resubscription; and
- capture repeatable acceptance evidence without requiring the native watchdog.

Exit tests:

- integration: component event -> input cache -> projection -> NATS -> UI;
- integration: ES trade -> all live calculations -> projection -> NATS -> UI;
- UI: RSI/TDI/ITI/VX-only changes appear without waiting for an ES event;
- UI: each accepted ES trade refreshes current price, percentage, EMA, Bollinger and MDI;
- UI: stopped/stale ES displays red/stale rather than appearing current; and
- interactive acceptance confirms no clipping, regressions or exception dialogs.

### MOHC-12 - Documentation and final qualification

Deliverables:

- update Market Outlook design/specification documents to state that it is a derived hot cache;
- document the initial API Server host and future Aspire Market Data Service relocation boundary;
- verify the dedicated service can reuse the application/domain assemblies without API Server or
  UI dependencies;
- mark the superseded revision/persistence portions of `MOR` and `LMO` historical plans;
- document cache ownership, rebuild, freshness, query and UI semantics;
- record test counts, commands, timestamps and any unrelated baseline failures; and
- produce a gate matrix with evidence for every `MOHC` gate.

Exit tests:

- all targeted BDD, unit, integration, verification and UI suites pass;
- affected solution projects build with zero errors;
- documentation links and architecture rules pass;
- live verification shows the expected trigger and freshness timestamps; and
- all `MOHC-00` through `MOHC-12` evidence is complete before the plan is marked implemented.

## 7. Test inventory required by the plan

### BDD

- one scenario for every independent component refresh;
- every accepted ES trade causes full recalculation;
- partial inputs progress under OR semantics;
- all four accepted ITI modes update latest trend/Trend Delta;
- cold start, warm start, value-date transition and contract rollover;
- API-hosted worker starts without taking Databento lifecycle ownership;
- stale ES retains visible values with red/stale status; and
- no persisted Market Outlook fallback is used.

### Unit

- cache atomicity and per-slot source ordering;
- pure composition and availability;
- percentage, EMA, Bollinger and MDI calculations;
- RSI/TDI/ITI/VX eligibility;
- query and notification contracts; and
- value-date/contract identity isolation.

### Integration

- NATS component input through cache and UI notification;
- NATS ES price input through complete recalculation and UI notification;
- typed query against the live cache owner;
- component-store warmup without Market Outlook persistence;
- notification failure with cache continuity;
- API dependency-injection/startup after event-sourced path removal;
- separate hosted-worker start, generation activation, fence transition and clean stop;
- lifecycle spy proves the worker never calls Databento start/stop/reset/rollover; and
- architecture test proves future Aspire hosting requires registration changes only.

### Verification

- representative pairwise component combinations and all retained OR availability masks;
- same-price and minimum-price-change ES events;
- deterministic independent calculation comparison;
- source timestamps never regress;
- no Daily persistence mutation during intraday preview; and
- runtime health timestamps distinguish actual ES input from UI/cache activity.

### UI and system

- initial query followed by both notification behaviours;
- every row updates from its correct source;
- partial, warming, stale and unavailable presentation;
- latest-value burst handling;
- open/close/reopen listener lifecycle; and
- supported DPI/layout regression coverage.

## 8. Execution order and cutover strategy

1. Complete `MOHC-00` through `MOHC-03` without changing the live route.
2. Implement and register the separate API Server-hosted worker and both refresh behaviours behind
   a temporary development-only hot-cache route.
3. Shadow-compose the current and proposed Market Outlook outputs using deterministic fixtures and
   live accepted inputs; compare common values while ignoring the intentionally removed revision.
4. Cut the query, notification and UI to the hot-cache DTO in `MOHC-06`.
5. Qualify startup/value-date behaviour before removing the old path.
6. Remove the old command/event/projector/storage workflow only after the new path passes targeted
   integration and UI tests.
7. Run concurrency, failure, full regression and interactive acceptance qualification.
8. Verify the Aspire extraction dependency boundary, update documentation and close gates only from
   recorded evidence.

Rollback before `MOHC-08` consists of selecting the prior query/notification route. After
`MOHC-08`, rollback is source-code deployment rollback; no Market Outlook data restoration is
required because the projection is rebuildable.

## 9. Gate status

| Gate | Status | Depends on |
| --- | --- | --- |
| MOHC-00 | Complete | Approval |
| MOHC-01 | Complete | MOHC-00 |
| MOHC-02 | Complete | MOHC-01 |
| MOHC-03 | Complete | MOHC-01, MOHC-02 |
| MOHC-04 | Complete | MOHC-02, MOHC-03 |
| MOHC-05 | Complete | MOHC-02, MOHC-03 |
| MOHC-06 | Complete | MOHC-04, MOHC-05 |
| MOHC-07 | Complete | MOHC-04, MOHC-05 |
| MOHC-08 | Complete | MOHC-06, MOHC-07 |
| MOHC-09 | Complete | MOHC-02, MOHC-06 |
| MOHC-10 | Complete | MOHC-04 through MOHC-09 |
| MOHC-11 | Complete | MOHC-06 through MOHC-10 |
| MOHC-12 | Complete | MOHC-00 through MOHC-11 |

No gate is complete merely because code exists. A gate becomes complete only when its deliverables
and exit tests have recorded evidence.

## 10. Completion record

The implemented cutover uses `MarketOutlookReadModel`, `MarketOutlookHotCache`,
`MarketOutlookHotCacheService`, `MarketOutlookComposer`, the existing typed NATS query subject and
the existing UI notification subject. The obsolete Market Outlook command actor, state repository,
event projector, commands, transition events, working state, revision DTO and active Scylla
read/write API were removed. Existing physical database rows and schema declarations were not
altered.

Qualification covers:

- all 127 non-empty seven-component availability masks;
- every independent component family, including all four ITI market languages;
- duplicate, delayed, ordinal-gap, generation, value-date and contract fencing;
- 10,000 accepted ES preview replacements without committed Daily-state mutation;
- notification-failure cache continuity and typed unavailable queries;
- MessagePack query/notification payload equivalence;
- hosted-worker activation and clean derived-state shutdown;
- WinForms partial and complete presentation plus latest-value delivery; and
- zero-error builds for the Analytics, API Server and WinForms application boundaries.

### 10.1 Qualification evidence — 2026-09-01

| Suite or boundary | Result |
| --- | --- |
| `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests` | 969 passed, 0 failed |
| `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests` | 478 passed, 0 failed |
| `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests` | 50 passed, 0 failed |
| `TomasAI.IFM.Application.MarketData.UnitTests` | 89 passed, 0 failed |
| `TomasAI.IFM.UI.Net.Presentation.UnitTests` | 280 passed, 0 failed |
| `TomasAI.IFM.UI.Net.SystemTests` | 71 passed, 0 failed |
| `TomasAI.IFM.Application.Api.Server` build | succeeded, 0 warnings, 0 errors |
| `TomasAI.IFM.UI.Net` build | succeeded, 0 warnings, 0 errors |

The targeted Market Outlook subset contributed 27 unit/verification cases, 13 BDD cases and two
integration tests. Full parent suites were also executed to detect regressions outside the new
files. Repository qualification found no remaining C# reference to the removed command actor,
observe/publish commands, working-state DTO or revision snapshot DTO.
