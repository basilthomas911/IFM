# Futures Contract On-The-Run and Rollover Set Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Plan ID | `FCR` |
| Status | Complete - all FCR gates qualified |
| Date | 2026-09-02 |
| Primary design authority | `Documents/system/Futures-Contract-Rollover-Startup.md` |
| Related lifecycle authority | `Documents/system/Futures-Value-Date-and-Live-Trading-Hours-Policy.md` |
| Scope | Futures contract schema, rollover preparation, value-date activation, persistence, runtime registration and qualification |
| Deferred | Automated exchange-holiday ingestion, historical on-the-run history, operator rollover UI and Market Data Aspire extraction |

## 1. Objective

Replace the ambiguous `CurrentlyTraded` futures-contract flag with two explicit operational
classifications:

- `OnTheRun`: the single primary contract for a futures root on the authoritative value date;
- `Rollover`: a contract admitted to the active rollover/trading set, including the on-the-run
  contract and any additional maturity required to conduct rollover or term-structure trading.

Implement rollover as a prepared, atomic transition. The replacement set is resolved and persisted
during the 17:00-18:00 Eastern closed interval on the preceding exchange business day. No feed is
active during that interval. The replacement `OnTheRun` contract is first consumed when the
rollover value date begins at 18:00 Eastern.

The minimum active assignments are:

| Root | Contract role | `OnTheRun` | `Rollover` |
| --- | --- | ---: | ---: |
| ES | Active quarterly maturity | `true` | `true` |
| VX | Front month | `true` | `true` |
| VX | Back/next month | `false` | `true` |
| Any | Inactive reference contract | `false` | `false` |

## 2. Binding semantics and invariants

1. `OnTheRun` is value-date operational state, not a synonym for provider activation, an
   unexpired contract or the furthest maturity.
2. Exactly one contract per required futures root is `OnTheRun=true`.
3. `OnTheRun=true` implies `Rollover=true`.
4. Multiple contracts for a root may be `Rollover=true`.
5. `Rollover=true` means the contract is eligible for active subscription and rollover-related
   trading. It does not mean that a rollover is currently due or has completed.
6. The `futures_contract_rollover.contractId` pointer identifies the on-the-run contract.
7. `futures_contract_rollover.nextRolloverDate` is the effective value date on which the next
   on-the-run assignment must be used. It is not the preparation timestamp.
8. The preparation date is the preceding valid exchange business day. Calendar-day subtraction is
   prohibited.
9. Preparation normally executes after the 17:00 close and before the 18:00 market open. The new
   set cannot be consumed by a live feed before the effective value date.
10. ES normally has one active rollover-set contract. The model permits a future ES successor to
    be pre-admitted when the rollover policy requires it.
11. VX has exactly two distinct, expiry-ordered rollover contracts for v1: front and back. The front
    is on the run.
12. A rollover is all-or-nothing across canonical rows, query projection, rollover pointer and
    durable operation log. Failure retains the last complete assignment.
13. Runtime registration changes only after durable persistence and verification succeed.
14. Repeated preparation, duplicate lifecycle commands and startup reconciliation are idempotent.
15. If the application missed the preparation window, startup reconciles the authoritative value
    date before admitting the affected Databento feeds.
16. Provider/catalog availability and operational selection remain separate concepts. A provider
    contract being active or unexpired does not make it on the run.
17. The two flags store the current operational snapshot. Historical on-the-run state by past value
    date is outside this schema and requires a future history/event design.

## 3. Rollover procedures

### 3.1 ES transition

1. Determine that the next market-open value date equals or exceeds `nextRolloverDate`.
2. Resolve the next eligible quarterly ES maturity after the existing front contract.
3. Verify the full contract exists in the canonical futures catalog and is valid for the target
   value date.
4. Prepare one replacement ES row with `OnTheRun=true` and `Rollover=true`.
5. Retire the previous active ES row to `OnTheRun=false`, `Rollover=false`.
6. Move the rollover pointer to the replacement contract and calculate/persist its next effective
   rollover date under the configured policy.
7. Verify persistence, then publish one immutable runtime registration snapshot.
8. The new ES registration is first used by the epoch opened for the rollover value date.

### 3.2 VX transition

1. Determine that the next market-open value date equals or exceeds `nextRolloverDate`.
2. Promote the existing VX back month to the new front month.
3. Resolve and validate the next maturity after the promoted front.
4. Persist this exact replacement set:
   - old front: `OnTheRun=false`, `Rollover=false`;
   - old back/new front: `OnTheRun=true`, `Rollover=true`;
   - newly resolved back: `OnTheRun=false`, `Rollover=true`.
5. Move the rollover pointer to the new front and persist its next effective rollover date.
6. Verify the exact two-contract rollover set and its order before replacing runtime state.
7. Start the rollover value-date epoch with both VX contracts subscribed and the front contract as
   the singular on-the-run route.

### 3.3 Failure and missed-window behavior

- A missing, duplicate, expired, wrong-root or otherwise invalid replacement blocks the mutation.
- A provider, storage or verification failure leaves the last coherent set active and emits a
  structured degraded/failed activity result to logs and the Status Console.
- No partial runtime registration is published.
- During the closed preparation window, the lifecycle coordinator retries according to its bounded
  startup policy.
- On a later API start, reconciliation derives the current authoritative value date and catches up
  before starting any feed whose assignment is stale.

## 4. Schema and contract direction

### 4.1 Shared DTO

Evolve `FuturesContractV2ReadModel` into a new explicitly versioned contract model:

- replace serialized key 9 `CurrentlyTraded` with `OnTheRun`;
- add serialized key 10 `Rollover`;
- update validation so `OnTheRun && !Rollover` is invalid;
- update command models, mapping code, fixtures and actor/API response contracts together.

No legacy semantic compatibility is required. Numeric serialization keys remain explicit and the
new shape receives serialization round-trip qualification before transport cutover.

### 4.2 Scylla canonical and query tables

The canonical `futures_contract` table receives `onTheRun` and `rollover` columns and stops reading
or writing `currentlyTraded` after cutover.

Because `currentlyTraded` is currently a clustering-key component of
`futures_contract_by_symbol_v2`, the projection cannot be safely renamed in place. Create and
backfill `futures_contract_by_symbol_v3` with an access pattern equivalent to:

```text
PRIMARY KEY ((symbol), rollover, onTheRun, lastTradeDate, contractId)
```

This supports:

- rollover-set query: `symbol + rollover=true`;
- singular on-the-run query: `symbol + rollover=true + onTheRun=true`;
- complete symbol inventory: the `symbol` partition.

Clustering order must return active rows first and maturities in ascending order where the
rollover algorithm requires front-before-back ordering. The existing projection-operation log,
hash verification and resumable backfill conventions remain mandatory.

### 4.3 Query and service contracts

Replace ambiguous APIs with precise operations:

- `GetOnTheRunFuturesContractAsync(symbol)` returns one contract or no result;
- `GetRolloverFuturesContractsAsync(symbol)` returns the ordered active set;
- `ReplaceFuturesRolloverSetAsync(rolloverPointer, contracts)` validates and atomically replaces a
  complete root assignment;
- runtime lookup exposes singular on-the-run identity separately from the subscribed rollover set.

## 5. Execution controls

- Only one gate may be `In progress` at a time.
- Each gate requires its implementation, focused tests and recorded exit evidence.
- Existing uncommitted startup/hosted-service work must be preserved and treated as the working
  baseline; this plan must not overwrite or revert it.
- Tests use `TimeProvider` and deterministic value dates. They cannot depend on the workstation
  clock or a live provider unless explicitly classified as live verification.
- Rollover mutations for the same root are serialized. Concurrency tests must prove that duplicate
  preparation or startup requests converge to one state.
- No production feed may start from an invalid or partially migrated assignment.
- Expected provider/storage unavailability is represented by typed results and operational status,
  without deliberate first-chance exceptions in long-running hosted-service loops.
- Existing unrelated test failures are reported separately and cannot count as gate evidence.

## 6. Implementation gates

### FCR-01 - Baseline inventory and failing-first characterization

**Work**

- Inventory every `CurrentlyTraded` property, CQL column, key, query, command, resolver, registry,
  rollover check, fixture and document.
- Classify each occurrence as provider availability, on-the-run identity or rollover-set
  membership.
- Capture the existing exact one-ES/two-VX durable and runtime baseline.
- Add failing-first tests demonstrating that the current model incorrectly labels VX back month as
  the current contract.

**Exit evidence**

- No production occurrence remains unclassified.
- Tests expose the present ambiguity without changing behavior.

### FCR-02 - Domain model and transport contract split

**Work**

- Introduce `OnTheRun` and `Rollover` in the versioned futures-contract model.
- Add invariant validation and descriptive factory/builder methods.
- Remove operational selection from the catalog's provider-active/unexpired calculation.
- Update command and MessagePack/NATS models without retaining `CurrentlyTraded` aliases.

**Tests**

- Constructor/default/value equality tests.
- `OnTheRun => Rollover` validation tests.
- MessagePack and JSON round-trip tests.
- Provider-active-but-not-selected characterization tests.

**Exit evidence**

- The shared contract cannot represent on-the-run outside the rollover set.
- Provider catalog mapping cannot independently declare a contract on the run.

### FCR-03 - Scylla schema v3 and controlled backfill

**Work**

- Add canonical columns and create `futures_contract_by_symbol_v3`.
- Add prepared statements and row mapping for both flags.
- Implement resumable projection backfill, per-symbol validation, count/hash comparison and rollback
  controls using existing securities projection conventions.
- Convert the known operational assignment during cutover:
  - ES front `true/true`;
  - VX front `true/true`;
  - VX back `false/true`.
- Retain the v2 projection only as rollback evidence until qualification; production reads switch
  entirely to v3.

**Tests**

- Schema creation and idempotent upgrade tests.
- Empty, partially populated and already-upgraded database tests.
- Backfill interruption/resume and hash verification integration tests.
- Invalid duplicate-on-the-run data rejection tests.

**Exit evidence**

- V3 contains exactly the expected flags and hashes for every migrated symbol.
- No production query reads the v2 `currentlyTraded` key.

### FCR-04 - Typed storage queries and atomic rollover-set replacement

**Work**

- Add singular on-the-run and collection rollover-set queries.
- Implement validated set replacement with one durable projection mutation and rollover-pointer
  update.
- Enforce root, uniqueness, ordering, exact cardinality and pointer invariants.
- Preserve the previous coherent assignment when any queued mutation or post-write check fails.

**Tests**

- Unit tests for every invariant and precise no-result behavior.
- Scylla integration tests for insert, replace, retire, retry and concurrent same-root commands.
- Injected partial-failure tests at canonical, projection, pointer and verification stages.

**Exit evidence**

- Readers can never observe a committed pointer that identifies a non-on-the-run contract.
- Duplicate commands converge to one exact set.

### FCR-05 - Exchange-business-day and effective-value-date policy

**Work**

- Introduce a deterministic futures exchange-business-calendar abstraction.
- Support weekends and explicitly configured exchange closures in v1; automated holiday ingestion
  remains deferred.
- Calculate the preparation date as the business day immediately preceding
  `nextRolloverDate`.
- Treat `nextRolloverDate` as an effective value date and prohibit use of the new assignment before
  that value-date epoch.
- Define startup catch-up behavior when preparation time was missed.

**Tests**

- Monday rollover prepared on the preceding Friday.
- Configured holiday and consecutive-closure cases.
- DST-safe 17:00, 18:00 and process-restart boundary cases.
- Early preparation cannot create an early live epoch.

**Exit evidence**

- No rollover date is calculated through calendar-day subtraction.
- The assignment first affects feed use on its effective value date.

### FCR-06 - Resolver and ES/VX rollover procedures

**Work**

- Resolve the next maturity strictly after the current front contract.
- Implement the ES one-contract replacement procedure.
- Implement the VX front-promotion/new-back sliding-window procedure.
- Separate provider candidate availability from operational flag assignment.
- Calculate and persist the subsequent effective rollover date through the configured policy.

**Tests**

- Nearest eligible successor selection with unordered and duplicate provider results.
- ES quarterly successor selection.
- VX old-front retirement, back promotion and new-back selection.
- Missing successor, wrong root, expired candidate and insufficient VX pair tests.

**Exit evidence**

- ES produces exactly one on-the-run rollover contract.
- VX produces exactly one on-the-run and exactly two rollover contracts in expiry order.

### FCR-07 - Atomic runtime registry and Databento subscription cutover

**Work**

- Replace the split current-contract/term-structure registry updates with one per-root immutable
  rollover-set publication.
- Register every `Rollover=true` contract for the appropriate DataBento dataset.
- Route default/current lookups only to `OnTheRun=true`.
- Derive the VX front/back term structure from the same immutable snapshot.
- Fence an in-flight epoch so it cannot observe half of a rollover.

**Tests**

- Registry snapshot and concurrent-read tests.
- ES subscribes only the active quarterly contract.
- VX subscribes front and back while singular lookup returns only front.
- Old epoch stability and new epoch replacement tests.

**Exit evidence**

- Durable and runtime views produce the same contract IDs, flags and ordering.
- No registry transition temporarily drops the VX back month or selects it as front.

### FCR-08 - Startup preparation and value-date lifecycle integration

**Work**

- Add the rollover preparation activity to actor-owned application/market-data startup sequencing.
- During 17:00-18:00 Closed, prepare an assignment whose effective date is the next market-open
  value date.
- At 18:00, verify the prepared assignment before starting the new epoch.
- On mid-session API startup, catch up stale assignments before feed admission.
- Make cancellation, repeated startup and degraded provider/storage paths safe for the hosted
  service exception boundary.

**Tests**

- Manual-time lifecycle tests spanning 16:59, 17:00, preparation, 17:59 and 18:00.
- Friday-to-Sunday/Monday value-date scenario.
- Missed-window, API restart, duplicate command and delayed provider response tests.
- Status Console and structured activity-result tests.

**Exit evidence**

- A feed cannot start for a rollover value date with the preceding assignment.
- Preparation failure does not terminate API startup or expose partial runtime state.

### FCR-09 - Actor/API query and operational verification surface

**Work**

- Add typed NATS queries for on-the-run identity and rollover-set membership.
- Return the effective rollover date, preparation status and source value-date revision where
  operational status already exposes contract readiness.
- Update diagnostics and logs to use exact `OnTheRun` and `Rollover` language.
- Remove ambiguous plural/singular `CurrentlyTraded` query and exception names.

**Tests**

- Actor unit and serialization tests.
- NATS request/reply integration tests for ES and VX.
- Stale response/revision fencing tests.
- Unauthorized/invalid root and unavailable-state behavior tests where applicable.

**Exit evidence**

- Operators and consumers can independently verify the primary contract and complete subscribed
  rollover set.

### FCR-10 - BDD, end-to-end verification, documentation and cutover

**Work**

- Add BDD scenarios for normal ES rollover, VX pair rotation, Monday/holiday preparation, missed
  startup recovery, missing successor, partial storage failure and duplicate lifecycle delivery.
- Run full unit, storage integration, actor/NATS integration, API startup and MarketData analytics
  regression suites.
- Run a deterministic verification journey across at least two consecutive rollover cycles.
- Optionally run a separately identified live-provider verification without making it a repeatable
  test prerequisite.
- Update rollover startup, Databento resiliency, startup actor and value-date documentation.
- Remove v2 reads and obsolete `CurrentlyTraded` production vocabulary after rollback evidence is
  accepted.

**Exit evidence**

- All reasonable minimum ES/VX decision combinations pass.
- The exact durable, query, runtime and feed-registration sets agree at every verified stage.
- Test results, schema/hash evidence and any unrelated baseline failures are recorded in this
  document.

## 7. Minimum BDD decision matrix

| Scenario | Expected result |
| --- | --- |
| ES before preparation day | Existing ES remains `true/true` |
| ES preparation during prior-business-day close | Successor becomes prepared `true/true`; no feed runs before 18:00 |
| ES rollover value date opens | New ES is the singular runtime/current route |
| VX before preparation day | Front is `true/true`; back is `false/true` |
| VX preparation | Back is promoted; next maturity becomes rollover back |
| VX rollover value date opens | Both new VX contracts are subscribed; only front is returned as on the run |
| Monday effective rollover | Preparation uses preceding exchange business day; activation uses Sunday 18:00 market open |
| Configured exchange holiday | Preparation skips the closure to the preceding valid business day |
| Provider cannot resolve successor | Existing complete assignment remains; activity reports failure/degraded state |
| Storage fails after partial queued work | Operation recovery restores/verifies one coherent set; runtime is unchanged |
| Duplicate preparation/startup commands | One idempotent durable and runtime result |
| API starts after missed rollover | Catch-up completes before the affected feed starts |
| Catalog contract is merely active/unexpired | It remains `false/false` until selected by rollover policy |
| Invalid `OnTheRun=true, Rollover=false` | Validation rejects the model and persistence operation |

## 8. Completion definition

This plan is complete only when:

1. `CurrentlyTraded` is absent from production futures selection, persistence and transport code.
2. Canonical and v3 projection data enforce the approved ES/VX flag combinations.
3. The rollover pointer always identifies the singular on-the-run contract.
4. Preparation uses the preceding exchange business day and the new assignment is first consumed
   on the effective rollover value date.
5. ES and VX transitions are atomic, idempotent and startup-recoverable.
6. Databento subscribes the complete rollover set while current/default routing uses only the
   on-the-run contract.
7. Unit, BDD, Scylla integration, actor/NATS integration, startup lifecycle and end-to-end
   verification tests pass with evidence recorded.
8. Documentation consistently distinguishes provider availability, on-the-run identity,
   rollover-set membership, preparation date and effective rollover value date.

## 9. Completion evidence

All gates `FCR-01` through `FCR-10` completed on 2026-09-02.

- Production selection, persistence and transport code contains no `CurrentlyTraded` API,
  property or query. The versioned v3 DTO and CQL projection use `OnTheRun` and `Rollover`.
- The live durable assignment was verified as ES `ES20260918=true/true`, VX front
  `VX20260916=true/true`, and VX back `VX20261021=false/true`.
- Same-root replacements are serialized, verified after persistence and published to the runtime
  registry only after durable success. Injected storage failure leaves runtime state unchanged.
- Deterministic policy tests cover weekends, configured closures, DST-safe preparation boundaries,
  early-preparation fencing and missed-window startup catch-up.
- The verification journey covers two consecutive ES quarterly transitions and two consecutive VX
  front/back rotations, proving durable/query/runtime ordering and flag invariants at each cycle.
- Focused qualification passed: Application MarketData unit 116/116; Securities unit 11/11;
  Securities BDD 2/2; Scylla Securities integration 40/40; Securities NATS integration 14/14;
  API MarketData integration 102/102; application startup/hosted-service unit 20/20;
  MarketData Analytics unit 1002/1002; Analytics integration 50/50; MarketData Feed unit 502/502;
  Feed integration 49/49 with four explicitly skipped environmental cases; UI presentation unit
  289/289; Domain MarketData unit 146/146.
- The serialized whole-solution build passed with zero warnings and zero errors. Native C++
  synthetic qualification passed all 11 scenarios; Rust passed 5/5 tests and advertises ABI v3.
- A live Development run completed all seven application startup activities. API readiness was
  Healthy, both Databento datasets reported native `Running/Ok`, ES/VX routes were green, and
  processing/publication failures remained zero. Live ES samples changed from 7674.25 to 7673.50;
  EMA10, EMA20 and both Bollinger families changed with the trade price, while RSI/TDI advanced on
  their configured cadence.
- Native dataset state, terminal status, warning, ring usage and produced/consumed counts are now
  exposed by the readiness diagnostics so a completed reader cannot fail silently during future
  Databento resiliency work.
- Live qualification exposed downstream Core NATS backpressure stopping the single ES ingestion
  loop while the native ring filled. Tick notifications now cross a non-blocking local MPSC queue;
  one dedicated publisher worker preserves ordered NATS delivery without allowing transport
  latency to block hot-cache ingestion. A slow-delivery burst test verifies 2,048 submissions over
  the former capacity-two boundary complete while delivery is deliberately held.
- Post-correction sustained live verification consumed 296,006/296,006 GLBX records with zero ring
  occupancy, `Running/Ok` native state and zero processing/publication failures. Market Outlook was
  complete and green; ES price, EMA10 and both Bollinger families changed across samples, and RSI
  plus TDI advanced together to the 17:08:00 calculation bucket.

The full Databento watchdog/reset lifecycle remains the separately designed resiliency Stage 2; it
is not required to qualify the FCR schema and rollover cutover.
