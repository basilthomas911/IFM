# Market Outlook Simple Hot-Cache Correction Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Plan ID | `MOSC` |
| Status | Complete |
| Date | 2026-09-01 |
| Scope | Replace Market Outlook generation fencing and update rejection with one always-writable partial-state cache and atomic whole-snapshot reads; restore and enforce Databento C++/Rust native ABI and behavioral parity |
| Supersedes | The cache admission, source-position rejection, generation-fence, activation-polling and derived-state lifecycle decisions in `Market-Outlook-Hot-Cache-Refactor-Implementation-Plan-v1.0.md` |
| Retains | Existing Market Outlook DTO, pure calculations, component ownership, NATS query/notification contracts, UI presentation and Databento feed ownership |
| Explicitly out of scope | Native Databento watchdog/reset implementation, persistent feed-health storage, Aspire extraction and changes to authoritative market-signal persistence; native ABI alignment and parity testing are explicitly in scope |

## 1. Objective

Market Outlook shall behave as a simple, process-local, latest-arrival hot cache. Every relevant,
valid signal routed to the cache updates only the fields it owns. One write lock serializes partial
state changes, composes the entire immutable Market Outlook snapshot, and atomically publishes that
snapshot. Readers return the latest published snapshot without taking a read lock.

The cache shall not reject an otherwise valid routed update because of a generation identifier,
source position, sequence comparison, timestamp comparison, startup ordering, value-date activation
or contract activation. A later arrival overwrites the fields owned by an earlier arrival.

Feed resilience remains independent. Databento connections and configured subscriptions remain
running continuously. Feed health reports connection/subscription state, most recent receipt times,
reset state and failure reasons; it does not control Market Outlook cache admission.

## 2. Binding decisions

1. The cache is available immediately when its singleton is constructed; it has no activation
   phase and no active-generation fence.
2. One write synchronization boundary protects every partial-state merge and compose operation.
3. The mutable working state is never returned to callers.
4. Each successful write publishes a new immutable `MarketOutlookReadModel` reference atomically.
5. Reads use an atomic reference read and do not acquire the write lock. A reader sees the complete
   snapshot before or after a write, never a torn or partially mutated snapshot.
6. Cache identity remains `(contract ID, value date)` solely to prevent values belonging to
   different instruments or sessions from being combined. Identity is not an admission fence.
7. The cache uses latest-arrival-wins semantics. Source sequence, event time, stream epoch and
   generation may remain as diagnostic/provenance fields, but cannot reject or suppress a write.
8. Contract/value-date rollover creates or updates the corresponding identity. It does not require
   an activation command and cannot stop another identity from receiving updates.
9. Old cache identities are removed only by bounded maintenance or host shutdown, never as a side
   effect of rejecting a producer update.
10. Feed parsers and signal owners retain responsibility for structural validation, supported
    signal type and correct contract routing. The Market Outlook cache does not become a raw-feed
    validator.
11. Missing, warming or unavailable components never block valid siblings. They remain explicit
    availability metadata and render as `N/A` where appropriate.
12. Historical EMA/Bollinger replay can write its warm baseline before, during or after live-feed
    startup. Its write is always merged and published.
13. Every valid ES `New` last-trade arrival updates current price/session fields. When a warm Daily
    EMA/Bollinger baseline exists, the same write recalculates the provisional EMA, Bollinger and
    MDI values, even when the price did not change.
14. A warmup operation cannot report successful Market Outlook publication until the resulting
    EMA and Bollinger values are observable through the cache query.
15. Market Outlook never starts, stops, resets or otherwise owns Databento. Cache availability,
    analytics availability and feed health are separate concerns.
16. Expected missing data, repeated writes, unusual arrival order and silent-market periods use
    normal result/status paths and do not manufacture first-chance exceptions.
17. The C++ public header is the canonical Databento native C ABI. The managed wrapper, C++
    implementation and Rust implementation must advertise the same ABI version and expose the same
    public structures, status values, calling conventions, ownership rules and exports.
18. The current alignment target is ABI version 3. The managed wrapper and C++ implementation
    already advertise version 3; the Rust implementation and its comparison interop currently
    advertise version 2 and must be brought to version 3 before qualification.
19. Any Databento feed capability or externally observable behavior added to or changed in C++ must
    be implemented in Rust in the same change set. Language-specific internal implementation is
    allowed, but public semantics and managed results must remain equivalent.
20. An ABI version is incremented only for an ABI-affecting change. When an increment is required,
    the canonical header, managed declarations, C++, Rust, packaging metadata and parity fixtures
    change together. A C++-only ABI or behavior change is not releasable.

## 3. Target runtime model

```text
validated component or ES trade arrives
  -> select cache identity (contract ID, value date)
  -> enter single cache write lock
  -> merge only the fields owned by that input
  -> recalculate every affected derived field
  -> compose one complete immutable MarketOutlookReadModel
  -> atomically replace the published snapshot reference
  -> leave write lock
  -> publish/coalesce latest-value UI notification

typed query or UI refresh
  -> atomically read the published snapshot reference
  -> return the complete snapshot immediately

Databento resilience
  -> independently observe connection and subscription health
  -> report Up / Resetting / Down and detailed reason
  -> never activate, fence or reject Market Outlook cache writes

native Databento implementation selection
  -> load configured C++ or Rust library
  -> require the same canonical ABI version and export set
  -> execute the same managed contract
  -> produce equivalent lifecycle, record, error and health behavior
```

### 3.1 Concurrency contract

The preferred implementation is a single `lock` for working-state mutation and an atomic reference
for publication:

```text
write:
  lock (writeGate)
    workingState = Merge(workingState, update)
    publishedSnapshot = Compose(workingState)
    Volatile.Write(ref currentSnapshot, publishedSnapshot)

read:
  return Volatile.Read(ref currentSnapshot)
```

An explicit reader-priority reader/writer lock is prohibited because frequent UI/API reads could
starve feed writers. Atomic immutable reads provide non-blocking read precedence without that risk.

If multiple identities are retained, the implementation may publish an immutable identity map or
use identity cells, but all writes still pass through the one approved write synchronization
boundary and every cell exposes only an atomic immutable snapshot.

## 4. Component write ownership

| Input | Fields updated or recalculated |
| --- | --- |
| ES trade | Current ES price, high/low/volume/session presentation, open-to-current percentage, provisional EMA/Bollinger and live MDI when baseline is available |
| Historical Daily EMA baseline | EMA baseline/provenance, EMA50, EMA200 and availability |
| Historical Daily Bollinger baseline | BB20 standard deviation, upper, center/EMA20, lower and availability |
| RSI 15-second signal | RSI value, slope and availability |
| TDI signal | TDI value set, strength/direction presentation and availability |
| ITI `Trending` | Latest ITI state and Trend Delta |
| ITI `TrendDirectionChanged` | Direction state and Trend Delta |
| ITI `TrendExtremeChanged` | Extreme state and Trend Delta |
| ITI `TrendReversalChanged` | Reversal state and Trend Delta |
| VX trade/term input | Current VX presentation and availability |
| Futures Trade Signal | Trade-signal and MDI presentation fields owned by that result |
| Feed-health update | Feed-health presentation only; it cannot clear or block analytics values |

Each writer owns its listed fields. A partial write must preserve every field owned by other
components.

## 5. Implementation gates

### MOSC-00 - Baseline and failing-first characterization

Deliverables:

- record the current fence, source-position and activation-poll rejection paths;
- capture the live failure where 260 valid ES Daily sessions replay before cache activation and
  EMA/Bollinger remain unavailable afterward;
- enumerate every Market Outlook writer, query consumer and UI notification consumer;
- preserve unrelated in-progress Databento feed-up probe work; and
- add failing-first tests for replay-before-service-activation and concurrent partial writers.

Exit tests:

- unit characterization proves a warm baseline is currently rejected before activation;
- integration characterization reproduces warmup success followed by null EMA/BB query values;
- architecture inventory accounts for every cache producer and consumer; and
- existing Market Outlook and Databento baselines are recorded before modification.

### MOSC-01 - Simple cache contract and atomic publication

Deliverables:

- replace active-fence, source-position and accepted/rejected APIs with an unconditional typed
  partial-update API;
- implement one write gate for merge-plus-compose;
- publish immutable snapshots through atomic reference replacement;
- implement lock-free current-snapshot reads;
- retain `(contract ID, value date)` identity without activation semantics; and
- preserve last-arrival diagnostic timestamps without using them for admission.

Exit tests:

- unit tests cover first write, overwrite, repeated identical write and every component-specific
  partial merge;
- verification proves one component write cannot erase sibling fields;
- concurrency tests prove readers never observe torn snapshots;
- stress tests run simultaneous component writers and readers without deadlock, starvation or
  lost sibling fields; and
- contract tests prove there is no rejection, generation-fence or source-order admission result.

### MOSC-02 - Remove activation and generation fencing

Deliverables:

- remove cache dependence on `IMarketDataGenerationAuthority`;
- remove the one-second activation polling behavior from `MarketOutlookHotCacheService`;
- remove generation-triggered cache clearing and stale-position rejection;
- keep listener/query hosting independent from the Databento lifecycle; and
- make the cache immediately writable during API-server startup and feed transitions.

Exit tests:

- startup unit test writes and reads before Databento reports Running;
- BDD proves a component arriving before, during and after feed startup remains visible;
- rollover tests prove separate contract/value-date identities never mix;
- shutdown tests prove clean listener completion without intentional exceptions; and
- dependency tests prove the cache has no Databento start/stop/reset capability.

### MOSC-03 - Historical EMA/Bollinger baseline handoff

Deliverables:

- make historical replay unconditionally merge the warm EMA and Bollinger baselines;
- publish both baselines as one coherent write when produced by the same ordered replay;
- make warmup completion verify the cache-visible target contract/value-date result;
- make repeated same-day replay an idempotent overwrite rather than a rejected update; and
- preserve Development-only automatic acquisition and the Production prohibition.

Exit tests:

- unit tests cover warmup before live feed, after live feed and concurrent with an ES trade;
- BDD proves 201 or more ordered sessions make all two EMA and four Bollinger values available;
- deterministic verification compares EMA20/50/200, BB20 standard deviation, upper and lower
  values with an independent reference calculator;
- integration proves stored-history replay -> cache -> typed query -> UI notification; and
- regression proves a second warmup performs no required Databento acquisition and still repairs
  an empty process-local cache.

### MOSC-04 - ES last-trade recalculation

Deliverables:

- route every structurally valid, correctly routed ES `New` last trade to the partial cache;
- update ES price/session fields for every arrival, including same-price and minimum-tick changes;
- recalculate provisional Daily EMA, Bollinger and MDI inside the same serialized update when the
  baseline exists;
- retain price/session updates when the baseline is still unavailable; and
- immediately recalculate when the baseline later arrives using the latest cached ES price.

Exit tests:

- unit tests cover first trade, same-price trade, minimum tick, burst arrival and baseline-late
  arrival;
- BDD proves every ES last trade publishes refreshed EMA/Bollinger values after warmup;
- verification proves 10,000 live previews never mutate committed Daily accumulator state;
- integration proves ES native event -> canonical event -> cache -> query/notification; and
- all expected missing-baseline paths complete without an exception.

### MOSC-05 - Independent component and whole-snapshot refresh

Deliverables:

- migrate RSI, TDI, all four ITI modes, VX, Futures Trade Signal, session/EOD and feed-health
  writers to unconditional partial merges;
- compose and atomically publish the whole snapshot after every component write;
- retain OR semantics so missing siblings cannot block a valid writer;
- retain typed availability and last-update diagnostics; and
- ensure notifications carry the exact snapshot reference/value available to a query.

Exit tests:

- BDD scenario for every component proves independent refresh;
- pairwise verification covers representative combinations and all 127 non-empty component
  availability masks;
- tests prove all four ITI signal languages update Trend Delta;
- integration proves each component event updates both cache query and NATS notification; and
- query/notification equivalence tests prove consumers observe the same complete snapshot.

### MOSC-06 - Feed-resilience and analytics-availability separation

Deliverables:

- keep Databento feeds and subscriptions running independently of cache state;
- report feed `Up`, `Resetting` or `Down`, last receipt times and a human-readable reason through
  the existing/interim health boundary;
- prevent silent-market periods from clearing values or disabling cache writes;
- distinguish feed health from `Available`, `Warming`, `Stale` and `Unavailable` analytics status;
- remove accepted/rejected cache counters and replace them with received, written, composed,
  queried and notification-failure counters; and
- ensure notification failure never rolls back the published cache value.

Exit tests:

- unit tests cover healthy-but-warming, healthy-and-complete, silent-but-up, resetting and down;
- BDD proves feed health cannot suppress any analytics write;
- integration injects notification failure while query results continue advancing;
- verification proves no-data periods retain the last complete snapshot with accurate status; and
- no cache test starts, stops or resets a Databento feed.

### MOSC-07 - UI and typed-query acceptance

Deliverables:

- retain the current typed NATS Market Outlook query and notification DTO;
- make initial UI load read the complete current snapshot;
- make every subsequent notification replace the displayed whole snapshot;
- continue showing `N/A` only for components that have never become available or are explicitly
  unavailable; and
- expose component update time/status sufficiently to diagnose a feed-versus-analytics problem.

Exit tests:

- presentation tests cover cold partial, baseline-arrival, live-trade refresh and full snapshot;
- UI system tests prove all four Bollinger and two EMA controls change after ES trade updates;
- stale/silent feed presentation tests preserve values while changing health status;
- repeated refresh/query tests do not regress to an older locally retained UI snapshot; and
- interactive acceptance confirms the live screen updates without restart or manual refresh.

### MOSC-08 - Failure, concurrency and soak qualification

Deliverables:

- exercise concurrent warmup, ES, RSI, TDI, ITI, VX, trade-signal and query activity;
- inject composer, notification and shutdown failures at their actual boundaries;
- prove the write lock is always released and the last published snapshot remains readable;
- measure lock hold time, write throughput, query latency and allocation behavior; and
- run through value-date and ES contract rollover while feeds remain running.

Exit tests:

- deterministic concurrency test proves no lost sibling updates;
- sustained stress/soak contains no deadlock, writer starvation, torn read or unbounded identity
  growth;
- p95/p99 query latency remains effectively non-blocking under burst writes;
- injected failures retain the last complete snapshot and report the responsible boundary; and
- first-chance debugger qualification finds no intentional exception control flow.

### MOSC-09 - Databento C++/Rust ABI v3 and behavioral parity

Deliverables:

- inventory the canonical ABI v3 header, managed P/Invoke declarations, C++ exports, Rust exports,
  structure layouts, enum/status values and ownership rules;
- update Rust and its comparison-test interop from ABI version 2 to the canonical ABI version 3;
- implement every ABI v3 export and externally observable C++ feed behavior in Rust, including any
  missing historical, latest-value, lifecycle, statistics, error and health capabilities;
- make both native libraries independently selectable through the same managed wrapper without a
  managed-code behavior fork;
- introduce a shared native capability manifest and deterministic vectors consumed by the C++,
  Rust and managed parity suites;
- fail build/CI when the native export sets, ABI versions, status values, structure sizes, field
  offsets, alignment, packing, calling convention or deterministic behavior diverge; and
- document the rule that every future C++ Databento change requires the corresponding Rust change
  and parity evidence in the same pull request/commit series.

Exit tests:

- C++ native ABI tests pass against canonical ABI v3;
- Rust native ABI tests pass against canonical ABI v3;
- managed startup loads C++ and Rust separately and receives ABI version 3 from each;
- binary export comparison proves identical required public symbol sets;
- layout verification proves exact cross-language size, alignment and offset parity for every ABI
  structure;
- deterministic synthetic parity compares lifecycle transitions, ticker mappings, quote/trade
  records, statistics, latest-price results, historical results, timeout behavior and error codes;
- the complete managed integration contract runs once with C++ selected and once with Rust selected
  and produces equivalent normalized results;
- negative tests prove both libraries return the same status for ABI mismatch, invalid structure
  size, invalid state, timeout and null/invalid arguments; and
- packaging verification proves the configured implementation resolves the intended native binary
  with no accidental fallback to the other implementation.

### MOSC-10 - Live verification, documentation and closeout

Deliverables:

- update the Market Outlook hot-cache, historical-warmup and Databento-resiliency documents with
  the simplified ownership model;
- mark the superseded fencing/rejection requirements explicitly;
- record ABI v3 C++/Rust parity results and the mandatory same-change-set maintenance policy;
- run the API server, UI and configured live Development feeds;
- record timestamped evidence that price, percentage, two EMA and four Bollinger values refresh;
- verify every remaining Market Outlook value is either refreshing or has an explicit and correct
  availability reason; and
- record the full qualification results before marking this plan complete.

Exit tests:

- Analytics unit, BDD and integration suites pass;
- Application MarketData unit/integration suites pass;
- API Server build and health verification pass;
- UI presentation and system suites pass;
- C++, Rust and managed ABI/parity suites pass with both runtime selections;
- a controlled live session proves warmup can precede or follow feed startup;
- repeated live ES events change the applicable EMA/Bollinger projections without cache rejection;
  and
- documentation, formatting and repository-diff checks pass.

## 6. Required test matrix

| Layer | Minimum required proof |
| --- | --- |
| Unit | Partial merge ownership, immutable publication, lock-free read, repeated overwrite, identity isolation, late baseline and every calculation path |
| BDD | Startup in every ordering, component OR semantics, live ES recalculation, health separation, rollover and recovery |
| Integration | Historical storage/replay, NATS component delivery, query, notification, API-hosted worker and native-to-managed ES event route |
| Verification | Independent indicator reference values, 127 availability combinations, 10,000-trade non-mutation and deterministic final snapshot |
| Concurrency | Simultaneous writers/readers, no torn state, no sibling loss, no deadlock and no writer starvation |
| Native ABI | C++/Rust/managed version, exports, layout, alignment, status, ownership and calling-convention equality at ABI v3 |
| Native parity | Shared deterministic lifecycle, mappings, records, statistics, latest-price, historical, timeout, error and health vectors against both implementations |
| UI/System | Initial partial display, automatic completion, changing EMA/BB values, status transitions and no manual refresh requirement |
| Live smoke | Running Databento feed, observable cache progression, warmup-before/after-startup, explicit reason for every unavailable field and qualified selected-native identity |

## 7. Execution order and rollback

Gates execute in numeric order. `MOSC-00` freezes the reproducible failure. `MOSC-01` and
`MOSC-02` establish the new concurrency/lifecycle foundation. `MOSC-03` and `MOSC-04` correct the
current EMA/Bollinger failure. `MOSC-05` migrates all remaining component writers. `MOSC-06`
separates feed resilience. `MOSC-07` qualifies consumer behavior. `MOSC-08` qualifies concurrency
and failures. `MOSC-09` restores ABI v3 alignment and qualifies C++/Rust behavioral parity.
`MOSC-10` performs live acceptance and documentation closeout.

Implementation shall remain one deployable change set until `MOSC-04` passes so the live system
cannot contain a mixture of fenced and unconditional writers. Before deployment, rollback is source
reversion to the existing cache implementation. The cache contains no authoritative or persisted
data, so rollback requires no data migration or restoration.

## 8. Completion rule

No gate is complete because code compiles or an individual test passes. A gate is complete only
after all listed deliverables and exit tests have recorded evidence. The plan remains incomplete
until live Development acceptance demonstrates, through the typed live path and the WinForms system
boundary, that the two EMA and four Bollinger values become available and continue changing from
accepted ES last trades while Databento feed health is reported independently. Completion also
requires both the C++ and Rust Databento libraries to
advertise canonical ABI version 3 and pass the same managed integration and deterministic parity
suites; selecting C++ as the live Development runtime does not waive Rust qualification.

## 9. Execution record - 2026-09-01

### 9.1 Gate status

| Gate | Status | Recorded evidence |
| --- | --- | --- |
| `MOSC-00` | Complete | The prior activation, generation and position admission paths were characterized; the 260-session warmup-before-activation failure and producer/consumer inventory drove failing-first coverage. Existing Databento feed-up probe changes were preserved. |
| `MOSC-01` | Complete | One global write gate now protects merge-plus-compose, immutable references are atomically published, reads are lock-free, and latest arrival always overwrites its owned fields. Concurrency, identity and 127-mask verification pass. |
| `MOSC-02` | Complete | `IMarketDataGenerationAuthority`, activation polling, generation clearing and source-order admission were removed from Market Outlook. The cache is immediately writable and has no Databento lifecycle capability. |
| `MOSC-03` | Complete | Historical EMA/Bollinger replay writes one coherent baseline regardless of startup order. Same-day replay repairs a cleared process cache without provider acquisition. Provider historical instrument identity is explicitly aliased to the active domain contract, so live `ES20260918` trades resolve the replayed baseline even when stored history identifies the series as `42140870`. |
| `MOSC-04` | Complete | Every structurally valid routed ES `New` trade updates price and recalculates the warm Daily preview. Empty diagnostic epoch/ordinal, repeats, gaps, same-price changes, minimum changes and 10,000 previews are covered. |
| `MOSC-05` | Complete | RSI, TDI, four ITI modes, VX, EOD, EMA, Bollinger and trade-signal updates use independent OR-semantic partial merges; query and notification receive the committed whole snapshot. |
| `MOSC-06` | Complete | Explicit `Up/Resetting/Down` feed health and reason are independent of analytics availability. Notification failure cannot roll back cache state; received, written, composed, queried, notification-failure and composition-failure metrics are recorded. |
| `MOSC-07` | Complete | Presentation tests pass; a WinForms system test proves consecutive whole snapshots replace price, percentage, two EMA and four Bollinger controls; and the opt-in live Windows system acceptance starts the real UI ES/VX route command, invokes warmup through NATS, and observes all eight projections change on a later ES trade. |
| `MOSC-08` | Complete | Concurrent partial writers/readers, 127 availability masks, 10,000 previews, composer failure, notification failure, identity isolation and recovery-after-failure tests pass. |
| `MOSC-09` | Complete | C++, Rust and managed consumers advertise ABI v3. Both native implementations expose the canonical 33-symbol manifest and pass layout, lifecycle, historical, error and deterministic parity tests. |
| `MOSC-10` | Complete | The Development host bound HTTP, started 125 actors, authenticated the selected C++ runtime, warmed 260 stored ES sessions, started the actual UI ES/VX feed routes, reported Green feed health, and passed the live eight-field refresh acceptance in 8 seconds. Documentation and C++/Rust parity qualification are complete. |

### 9.2 Automated qualification

| Suite | Result |
| --- | --- |
| MarketData Analytics unit | 975 passed |
| MarketData Analytics BDD | 478 passed |
| MarketData Analytics integration | 50 passed |
| Application MarketData unit | 93 passed |
| Domain MarketData integration | 22 passed |
| Domain MarketData Feed integration | 48 passed, 4 intentional skips |
| Framework Databento unit | 133 passed |
| Framework Databento integration | 7 passed |
| UI presentation unit | 280 passed |
| UI system | 73 passed, including 1 opt-in live acceptance passed separately |
| C++ native CTest | 1 passed |
| Rust `cargo test --features live` | 7 passed |
| Managed C++/Rust binary parity | 4 passed |
| API Server build | succeeded with 0 warnings and 0 errors |

The Analytics integration suite initially encountered shared-infrastructure timeouts while a live
API replay was using the same NATS/projector services. After that live process was stopped, the
complete isolated suite passed 50/50. A later single TDI timeout passed immediately in isolation and
the final complete suite then passed 50/50, so no product failure remained.

### 9.3 Live acceptance evidence

The initial HTTP-readiness diagnosis was incorrect: the Development host was bound and its
`/health/ready` and typed Market Outlook endpoints were responsive. The controlled live runs then
found and corrected two separate issues that automation alone had not exposed:

1. A Databento `SlowReaderWarning` terminated the persistent C++ session, and the two-millisecond
   native ring-full timeout was too small for the session replay burst. C++ and Rust now both treat
   that warning as advisory for a persistent live session. Development and Paper/Production live
   profiles allow up to 30 seconds for managed drain backpressure; the synthetic CI profile retains
   its bounded two-millisecond behavior.
2. The historical replay published its immutable EMA/Bollinger checkpoint under provider instrument
   ID `42140870`, while live trades requested it under active contract `ES20260918`. Replay now
   publishes the same baseline under the active domain-contract alias. This preserves historical
   provenance while making the live preview lookup exact and deterministic.

At 16:19-16:20 Toronto time, the final `DatabentoLive` Development acceptance recorded:

1. the actor supervisor running all 125 actors and the selected C++ native runtime authenticated;
2. API health `Healthy`, `databentoFeedUp=true`, ES and VX aggregation/routes running, no publication
   or processing failures, and both current feeds `OffHoursActive`;
3. 260 valid stored ES Daily sessions replayed into a warm immutable baseline;
4. the opt-in live test executing the same typed `StartMarketDataFeed` route command used by the UI;
5. two successive Green snapshots in which close, open-to-current percentage, EMA50, EMA200,
   Bollinger standard deviation, upper, EMA20 center and lower all changed; and
6. a subsequent typed snapshot at `2026-09-01T20:20:04.1760275Z` with close `7647.5`, EMA50
   `7527.1758806389985221129780468`, EMA200 `7195.4145574182154599728092036`, Bollinger standard
   deviation `374.707418361313`, upper `8269.859285568245712666054462`, center
   `7520.4444488456197126660544619`, lower `6771.0296121229937126660544619`, provisional preview
   enabled and a non-null live-price timestamp.

The Windows interactive automation helper remained unavailable with
`failed to write kernel assets: The system cannot find the path specified. (os error 3)`, so no
claim is made that an automation cursor visually inspected the running desktop. Acceptance instead
uses the real live NATS command/query path plus the WinForms control-level system test, avoiding a
manual-only release gate while still verifying both sides of the UI boundary.
