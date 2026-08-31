# Portfolio and Fund Implementation Plan v1.1

| Item | Value |
| --- | --- |
| Status | Draft extension prepared for review and execution approval |
| Created | 2026-08-29 |
| Prior v1.0 baseline approved | 2026-08-29 |
| Revised | 2026-08-30 |
| Source | [Portfolio-Fund-Specification-v1.0.md](./Portfolio-Fund-Specification-v1.0.md), revised internally to v1.1 |
| Authoritative design | [Portfolio-Fund-High-Level-Design-v0.1.md](../../Documents/system/Portfolio-Fund-High-Level-Design-v0.1.md) |
| Scope | Historical PF-01 through PF-20 plus new PF-21 through PF-30 extension |
| New-gate initial state | Not Started |

## 1. Purpose

Once approved, this plan converts the revised Portfolio/Fund specification into an ordered, independently reviewable implementation sequence. A gate is complete only when its production deliverables, documentation, and all five required test dispositions are recorded: BDD, unit, integration, verification, and UI system tests.

The implementation stops after Portfolio/Fund configuration, planned composition identity, accepted OrderComposition references, and Risk outcome references. It does not authorize broker submission, fills, live positions, or an execution-facing TradeDb redesign.

PF-01 through PF-20 retain their recorded evidence and status. The approved design changes are implemented by PF-21 through PF-30. A historical gate is not rewritten as though the new requirement existed at the time; the new gate explicitly supersedes affected behavior and requalifies it.

## 2. Execution rules

1. Gates execute in dependency order unless this plan explicitly permits parallel work.
2. Every gate starts from a recorded clean targeted-test baseline.
3. A gate may add tests to more than one project, but test ownership must follow the layer definitions in section 5.
4. `Not applicable` is not an omitted test. It requires an executable architecture, boundary, or unchanged-behavior assertion identified in the gate.
5. A later test cannot excuse a missing test at the layer where a defect is cheapest to detect.
6. Deterministic business rules receive unit and BDD coverage before infrastructure integration.
7. Real NATS, PostgreSQL, and ScyllaDB paths receive integration coverage before a gate closes.
8. Verification tests use representative production-shaped scenarios, not an uncontrolled Cartesian product.
9. UI system tests use public actor/NATS APIs. The UI must not read Portfolio storage directly.
10. Every asynchronous wait is bounded and reports the last observed state on timeout.
11. Every test owns unique Portfolio, Fund, workflow, command, idempotency, and file identities as applicable.
12. Tests clean temporary files and test-owned projection data. Durable event history may use isolated test databases or unique stream namespaces instead of destructive shared cleanup.
13. Existing unrelated failures are recorded before a gate begins and cannot be reclassified as gate success.
14. Production code, test code, schema, and governing documentation are committed together per completed gate or coherent gate group.
15. A gate reopens if a later change invalidates its acceptance evidence.

## 3. Fixed boundaries

- `PortfolioCommandActor` owns Portfolio versions, state, membership, allocations, and delegated Fund risk envelopes.
- `PortfolioFundCommandActor` owns Fund mandates, assignments, planned composition identities, and accepted downstream result references.
- `PortfolioQueryActor` serves typed reads over rebuildable ScyllaDB Portfolio projections.
- PostgreSQL EventSourceDb is authoritative for aggregate history.
- PostgreSQL SequenceIdDb allocates positive integer PortfolioId, FundId, OrderId, and TradeId values.
- PostgreSQL SequenceIdDb also allocates PolicyId and TradeStrategyFamilyId; no operator enters an integer business ID.
- ReferenceDb owns exactly three read-only v1 TradeStrategyFamily definitions: Futures, Vertical Spread, and Iron Condor.
- PortfolioFinancialPolicy owns immutable global and per-family hard limits and atomic activation/assignment.
- ScyllaDB `PortfolioDbContext` is a query projection, never write authority.
- All application and UI commands/queries use typed NATS messaging.
- Existing Fund actors, Fund data, and Funds UI are legacy and remain isolated.
- Portfolio Administration exposes a compact command bar and Risk Policy modal, with no Planned Compositions action.
- Trade Orders is the sole manual/StrategyWorkflow composition view and selects Portfolio before Fund.
- TradeSelection selects a permitted template; OrderComposition constructs an exact non-executable candidate.
- No PF gate performs OrderExecution, broker effects, fills, or live-position creation.

## 4. Required project and test topology

```text
TomasAI.IFM.Domain.Portfolio
TomasAI.IFM.Domain.Portfolio.Shared
TomasAI.IFM.Domain.Portfolio.UnitTests
TomasAI.IFM.Domain.Portfolio.BDDTests
TomasAI.IFM.Domain.Portfolio.IntegrationTests
TomasAI.IFM.Domain.Portfolio.VerificationTests

TomasAI.IFM.Application.Storage/PortfolioDb
TomasAI.IFM.Application.Storage/FundLegacyDb
TomasAI.IFM.Application.Storage/ReferenceDb
TomasAI.IFM.Domain.Reference.Shared

TomasAI.IFM.UI.Net.Presentation.UnitTests
TomasAI.IFM.UI.Net.SystemTests
```

The implementation must register new projects in the solution without renaming the legacy Fund projects. Shared test utilities may be placed in an existing repository-approved test-infrastructure project, but no production project may reference a test project and no test project may depend on another test project merely to access internal fixtures.

## 5. Test-layer contract

| Layer | Responsibility | Infrastructure policy |
| --- | --- | --- |
| BDD | Business behavior and acceptance language across commands, policies, and state changes | In-memory/fake boundaries are allowed when the scenario is about domain behavior |
| Unit | Identities, serialization, validation, mapping, state transitions, algorithms, hashes, and deterministic resolution | No external services |
| Integration | Actor routing, typed NATS APIs, PostgreSQL event/sequence persistence, Scylla schemas/projections, replay, restart, and concurrency | Real containerized or configured repository infrastructure |
| Verification | Representative end-to-end production paths and decision/configuration combinations | Production actors and clients with real infrastructure; external broker effects prohibited |
| UI system | Navigation, user workflows, public API binding, filtering, error display, identity search, and legacy coexistence | Real UI host or repository-standard UI harness over public APIs |

Each test is tagged/category-filterable as `Portfolio`, its gate ID, and its layer where the framework supports traits.

## 6. Gate dependency sequence

```text
PF-01
  -> PF-02
  -> PF-03 -> PF-04 -> PF-05 -> PF-06
  -> PF-07 -> PF-08 -> PF-09 -> PF-10
  -> PF-11 -> PF-12 -> PF-13 -> PF-14 -> PF-15
  -> PF-16 -> PF-17
  -> PF-18 -> PF-19 -> PF-20
```

PF-03 and PF-04 may be implemented in parallel after PF-02 if shared-contract changes remain coordinated. PF-07 and PF-08 may be prepared in parallel after aggregate contracts stabilize, but PF-09 cannot close until both are complete. PF-16 UI shell work may begin after PF-10, but cannot close before PF-11 and PF-12 provide usable query data.

The v1.1 extension executes as:

```text
PF-21
  -> PF-22
  -> PF-23
  -> PF-24
  -> PF-25
  -> PF-26
      +-> PF-27 -+
      `-> PF-28 -+-> PF-29 -> PF-30
```

PF-27 and PF-28 may proceed in parallel only after PF-26 supplies stable typed APIs. PF-29 cannot begin until both UI gates close. PF-30 cannot close while any historical Partial item materially affects the revised path; superseded missing evidence is replaced by the named PF-21+ evidence rather than silently waived.

## 7. Standard gate evidence

Every gate record contains:

- gate ID, owner, start/completion dates, commit IDs, and affected files;
- baseline and final targeted commands with pass/fail/skip counts and duration;
- named BDD, unit, integration, verification, and UI test evidence;
- schema or serialized-contract compatibility evidence when applicable;
- trace/log examples for new actor or projector behavior;
- known failures proven unrelated to the gate;
- deferred items and the gate that owns them; and
- reviewer approval.

No gate is `Complete` while a required test is skipped, flaky, quarantined, timing-dependent without a bound, or dependent on undeclared local state.

## 8. Implementation gates

### PF-01 — Topology, identities, enums, and serialization contracts

**Depends on:** approved specification.

**Implementation:**

1. Record solution build and relevant test baselines; inventory overlapping user changes.
2. Create/register the Portfolio production, shared, unit, BDD, integration, and verification projects.
3. Add `PortfolioId`, `PortfolioFundId`, `PortfolioFundOrderId`, and `PortfolioFundOrderTradeId` with stable MessagePack keys and dot-separated formats.
4. Add explicitly numbered Portfolio, Fund, capacity, composition, and origin enums without renumbering existing contracts.
5. Establish command-envelope key inheritance: base keys 0–5 and payload keys beginning at 6.
6. Audit the repository and reserve error codes `34000-34299`; document any collision before code merges.
7. Add shared reason-code and validation-result foundations without implementing later gate behavior.
8. Register serialization/source-generation metadata required by repository conventions.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Examples reject zero/negative identities and preserve the business-readable Portfolio/Fund/Order/Trade hierarchy. |
| Unit | Constructor validation, equality, hash behavior, exact `Format()`, enum numeric values, MessagePack round trips, key positions, and unknown enum handling. |
| Integration | Solution/test discovery plus serialization through the actual typed NATS serializer for one identity and one command envelope. |
| Verification | A production-contract smoke scenario serializes and deserializes every new identity and confirms integer values remain unchanged. |
| UI system | UI contract smoke test binds integer identity DTOs and renders/searches their operator-facing formats without direct storage access. |

**Exit:** all projects build and are independently test-discoverable; contract snapshots are approved; error-code audit is clean; no legacy project was renamed.

### PF-02 — PostgreSQL Portfolio sequence and allocation

**Depends on:** PF-01.

**Implementation:**

1. Add `Portfolio_PortfolioId` to `SequenceName` and `ToStringFast`.
2. Update SequenceIdDb initialization/cutover scripts and documentation.
3. Add a typed allocation service/client used by Portfolio creation.
4. Reuse `Fund_FundId`, `Trade_OrderId`, and `Trade_TradeId` without creating competing sequences.
5. Enforce checked `long`-to-`int` conversion, positive values, no reuse, and allowed gaps.
6. Prevent callers from treating a sequence high watermark as an allocated ID.
7. Prohibit hand-entered or client-generated integer IDs in UI, console, API, import, and test-support creation paths; allocation failure must stop creation without a fallback ID.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Creating identities yields positive operator-facing integers; consumed-but-uncommitted IDs may gap and are never reused. |
| Unit | Sequence-name mapping, checked conversion, zero/negative rejection, `Int32.MaxValue` boundary, and overflow failure. |
| Integration | Real PostgreSQL allocation proves uniqueness under concurrency, block allocation, restart continuity, and correct four-sequence names. |
| Verification | Allocate representative Portfolio, Fund, Order, and Trade IDs through production services and prove unchanged round trip through DTOs. |
| UI system | Create-Portfolio and Create-Fund UI obtain/display read-only allocated integer IDs, expose no editable ID input, and present bounded allocation failure without fabricating an ID. |

**Exit:** schema initialization is repeatable; allocation tests pass under concurrency; cutover documentation includes the new sequence.

### PF-03 — Portfolio aggregate

**Depends on:** PF-02.

**Implementation:**

1. Implement Portfolio state, complete immutable views, commands, events, validators, and mapping.
2. Implement create, add-version, operating-state change, Fund membership, risk-envelope delegation hook, retirement transitions, and audited terminal deletion of a never-activated Draft.
3. Enforce expected versions, effective dates, authenticated principal attribution, and command idempotency.
4. Reject commands that would place exact composition or execution data in Portfolio state.
5. Register `PortfolioCommandActor` routes without enabling UI writes yet.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Create/version/activate/pause/reduce-only/retire scenarios, Draft-only deletion, duplicate create, invalid transitions, membership rules, and retired/deleted immutability. |
| Unit | Reducers, validators, version increments, effective-time boundaries, expected-version conflicts, idempotent replay, and immutable snapshot copies. |
| Integration | Real actor command routing and PostgreSQL event append/reload for success, validation failure, concurrency conflict, and actor restart. |
| Verification | Production path builds representative deletion decisions for Draft/Active/Paused/Disabled/Retired, reloads identical tombstone state, and proves no broker/execution records or messages are produced. |
| UI system | Portfolio form contracts display lifecycle state/reasons, require exact Portfolio-code confirmation plus deletion reason, and disable deletion outside actor-authoritative Draft state. |

**Exit:** every Portfolio command has deterministic state/event behavior, typed errors, durable replay evidence, and no execution side effect.

### PF-04 — PortfolioFund mandate aggregate

**Depends on:** PF-02 and compatible PF-03 membership contracts.

**Implementation:**

1. Implement Fund mandate state, complete immutable views, commands, events, validators, and mapping under `PortfolioFundId`.
2. Implement create mandate, add version, state change, and expiry/effective-date rules.
3. Enforce exactly one Portfolio parent per Fund version and reject legacy Fund actor routing.
4. Model investment intent, eligible assets, horizon, objectives, and composition policy references without Portfolio-wide capital authority.
5. Register `PortfolioFundCommandActor` routes.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Create/version/activate/pause/disable/expire mandate scenarios; parent immutability; invalid asset/horizon/effective-window behavior. |
| Unit | Mandate validation, transition table, expected versions, date boundaries, immutable collections, and Portfolio/Fund route formatting. |
| Integration | Real NATS command to PortfolioFund actor, PostgreSQL append/reload/restart, concurrency conflict, and rejection by the legacy Fund actor. |
| Verification | Daily, Weekly, and Monthly ES mandates retain exact identities/versions across production serialization and replay. |
| UI system | Fund editor binds only beneath a selected Portfolio and displays actor validation for invalid mandate/state operations. |

**Exit:** Fund mandate history is replayable, parent ownership is invariant, and legacy routing cannot mutate new Fund state.

### PF-05 — Template and profile assignments

**Depends on:** PF-04.

**Implementation:**

1. Implement versioned TradeTemplate, TradeSelectionHintProfile, and OrderCompositionProfile assignment records.
2. Enforce enabled/effective windows, allowed horizons/assets, immutable referenced versions, and non-overlapping uniqueness rules.
3. Support assignment replacement by appending state rather than mutating historical versions.
4. Preserve the distinction between reusable definitions, assignments, and instantiated FundOrder composition records.
5. Add initial Daily directional future, Weekly vertical, and Monthly Iron Condor assignment fixtures as configuration, not hard-coded selection truth.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Assign/replace/disable template and profiles; reject incompatible asset/horizon, missing version, expired assignment, and duplicate active configuration. |
| Unit | Assignment validator, effective-window comparison, stable hashing, deterministic ordering, overlap detection, and mapping. |
| Integration | Commands persist/replay assignments; projected reads retain exact template/profile IDs and versions after actor restart. |
| Verification | Representative Daily/Weekly/Monthly configurations resolve the expected assignment set, including bullish/bearish/neutral variants. |
| UI system | Assignment editor lists versioned definitions, shows effective/enabled state, prevents incompatible selection, and displays duplicate errors. |

**Exit:** assignment history is immutable and deterministic; no assignment constructs exact legs or execution fields.

### PF-06 — Fund allocation and FundRiskEnvelope delegation

**Depends on:** PF-03 and PF-04.

**Implementation:**

1. Implement versioned Fund allocation and FundRiskEnvelope records under Portfolio authority.
2. Validate allocation, reserves, currency, effective windows, hard limits, and Portfolio/Fund membership.
3. Enforce that Fund envelopes cannot exceed Portfolio authority and cannot silently widen a prior hard limit.
4. Model active, constrained, blocked, and expired permission inputs without implementing the RiskManagement calculation.
5. Freeze complete envelope identity/version/hash references for downstream snapshots.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Delegate/replace/expire allocations and envelopes; reject missing membership, negative capital, over-allocation, widening, overlap, and blocked new exposure. |
| Unit | Money/percentage boundaries, aggregate allocation totals, constraint intersection, effective-window rules, hash determinism, and version checks. |
| Integration | Portfolio actor persists and replays allocation/envelope changes; concurrency and cross-Portfolio Fund misuse fail through real NATS. |
| Verification | Green, constrained, blocked, and expired representative envelopes produce exact immutable references and permissions for each horizon. |
| UI system | Allocation/risk editor shows Portfolio totals and effective constraints, blocks invalid delegation, and never exposes broker credentials. |

**Exit:** Portfolio remains sole financial authority; frozen envelope references are deterministic; no RiskManagement decision is calculated here.

### PF-07 — PostgreSQL event-source repositories, replay, and snapshots

**Depends on:** PF-03 through PF-06.

**Implementation:**

1. Implement Portfolio and PortfolioFund event stream naming, repositories, expected-version append, and complete replay.
2. Add snapshot support only where consistent with repository conventions; snapshots are accelerators, not authority.
3. Add idempotency result retention sufficient to return committed command/reservation outcomes.
4. Store authenticated principal, command, correlation, causation, and origin timestamps required by the specification.
5. Reject legacy streams that cannot produce a valid new-domain snapshot instead of treating them as empty.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Historical versions remain observable and current state is identical with or without an acceleration snapshot. |
| Unit | Stream-name composition, event-fold ordering, snapshot selection, unknown-event/version handling, and metadata mapping. |
| Integration | Real PostgreSQL append, optimistic concurrency, multi-event atomicity, replay, snapshot restore, corruption/unknown-contract failure, and process restart. |
| Verification | Rebuild representative Portfolio/Fund histories from events only and byte/hash-compare final immutable views. |
| UI system | Restart/replay is transparent to open Portfolio/Fund detail workflows; stale UI versions receive a conflict and safe refresh action. |

**Exit:** PostgreSQL history alone reconstructs authoritative state; optimistic concurrency and metadata attribution are proven.

### PF-08 — PortfolioDb ScyllaDB schema and contexts

**Depends on:** stable PF-03 through PF-06 read models; may progress with PF-07.

**Implementation:**

1. Add `IPortfolioDbReadContext`, `IPortfolioDbWriteContext`, `IPortfolioDbContext`, `PortfolioDbContext`, CQL, parameters, and schema initialization.
2. Implement the specification’s point, state, membership, active-Fund, assignment, envelope, order, trade, and workflow-composition tables.
3. Ensure partition keys serve intended queries without `ALLOW FILTERING` or unbounded scans.
4. Add bounded paging/cursors and explicit consistency/idempotent write behavior.
5. Keep `FundLegacyDbContext` physically/logically separate; do not dual read or dual write.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Query-language examples define Portfolio-to-Fund navigation, lifecycle filtering, and composition lookup from an operator perspective. |
| Unit | CQL/parameter mapping, partition-key construction, cursor validation, null/optional mapping, and bounded page-size rules. |
| Integration | Real Scylla schema create/reapply, CRUD by each intended access path, paging, no-`ALLOW FILTERING` audit, and isolated teardown. |
| Verification | Seed representative projections and retrieve identical Portfolio/Fund/order/trade relationships through every supported typed access path. |
| UI system | Query DTO fixtures bind to Portfolio/Fund selectors and paged grids without storage-specific fields or direct database access. |

**Exit:** schema initialization is idempotent; every query has an intended partition path; legacy storage remains isolated.

### PF-09 — Durable projectors and idempotent projections

**Depends on:** PF-07 and PF-08.

**Implementation:**

1. Implement durable Portfolio and PortfolioFund projector descriptors and handlers.
2. Project committed events into all required Scylla tables using idempotent mutations.
3. Persist fenced PostgreSQL checkpoints and prevent checkpoint advancement before successful projection.
4. Handle duplicate delivery, partial batch failure, poison events, bounded retry, and controlled rebuild.
5. Publish terminal completion/failure only according to repository projector conventions.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | A committed business change becomes query-visible once; duplicate event delivery does not duplicate business state. |
| Unit | Event-to-row mapping, idempotency keys, checkpoint monotonicity, batch boundaries, retry classification, and tombstone/current-row behavior. |
| Integration | Real PostgreSQL-to-Scylla projection, duplicate/reordered delivery handling, restart recovery, failed mutation checkpoint fencing, and full rebuild. |
| Verification | Rebuild the representative catalog from empty ScyllaDB and compare every query result/hash with the original projection. |
| UI system | UI eventually observes committed changes, presents bounded pending/error state, and does not show duplicate rows after projector replay. |

**Exit:** projections are rebuildable and idempotent; no checkpoint can skip an unprojected authoritative event.

### PF-10 — Typed NATS command/query APIs and clients

**Depends on:** PF-07 through PF-09.

**Implementation:**

1. Define shared service APIs, subjects, request/response DTOs, error envelopes, and typed clients for Portfolio and PortfolioFund commands and queries.
2. Route mutations to the correct command actor and reads to `PortfolioQueryActor`.
3. Implement bounded request timeouts, cancellation, serialization errors, not-found/conflict/validation mapping, and correlation propagation.
4. Add point and paged queries for Portfolio, Fund, assignments, envelopes, FundOrders, FundOrderTrades, and workflow references.
5. Prohibit UI/console references to storage contexts.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Client-visible commands and queries express successful, validation, conflict, not-found, timeout, and unavailable outcomes consistently. |
| Unit | Subject construction, DTO mapping, error mapping, cancellation propagation, page-token validation, and MessagePack key snapshots. |
| Integration | Real NATS server routes every typed API, survives actor restart, enforces timeout/cancellation, correlates traces, and queries real projections. |
| Verification | Production clients execute an end-to-end Portfolio/Fund create-read-update-read path with exact identities and versions. |
| UI system | UI harness uses only typed NATS clients; architecture test rejects references from UI assemblies to PortfolioDb contexts. |

**Exit:** all public operations are available through typed NATS; no application consumer requires direct Portfolio storage access.

### PF-11 — Active Fund resolution and frozen strategy snapshot

**Depends on:** PF-05, PF-06, PF-09, and PF-10.

**Implementation:**

1. Implement deterministic active-Fund resolution by Portfolio, trading year, decision horizon, eligible asset, and evaluation time.
2. Fail safely for zero or multiple matches and for inactive, disabled, expired, blocked, or version-inconsistent configuration.
3. Construct `PortfolioFundStrategySnapshot` containing exact Portfolio/Fund/assignment/envelope identities, versions, hashes, and effective times.
4. Canonically order collections and calculate a deterministic snapshot hash.
5. Ensure later configuration changes cannot mutate an already frozen workflow snapshot.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Resolve Daily/Weekly/Monthly active Funds; reject missing, duplicate, paused, disabled, retired, expired, ineligible, and blocked configurations. |
| Unit | Precedence, date boundaries, horizon/asset matching, deterministic ordering/hash, immutable copies, and reason-code ordering. |
| Integration | Typed NATS strategy-reference query reads real projections and remains identical across actor/projector restart and replay. |
| Verification | Representative minimum catalog resolves Daily future, Weekly bullish/bearish vertical, and Monthly neutral/bias Iron Condor snapshots. |
| UI system | Portfolio/horizon selectors show the single resolved Fund or a precise configuration error without guessing a default. |

**Exit:** resolution is deterministic and fail-closed; accepted workflows retain immutable version/hash-complete snapshots.

### PF-12 — FundOrder/FundOrderTrade reservation and integer retention

**Depends on:** PF-02, PF-07, PF-10, and PF-11.

**Implementation:**

1. Implement the complete reservation request/response contracts from the specification.
2. Atomically reserve one OrderId and required TradeId values through the PortfolioFund command path.
3. Persist the selected TradeSelection result/template/profile references and initial composition state.
4. Return original committed IDs for an identical idempotent replay; reject key reuse with different canonical payload.
5. Preserve integer IDs through events, state, projections, queries, logs, traces, and downstream contracts.
6. Handle allocation-success/event-commit-failure gaps without reuse.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | First reservation succeeds; identical duplicate returns the same IDs; changed-payload reuse conflicts; invalid snapshot/template/state is rejected. |
| Unit | Canonical payload hash, idempotency comparison, leg/trade-count validation, checked ID conversion, state creation, and response mapping. |
| Integration | Concurrent duplicate requests over real NATS/PostgreSQL yield one committed reservation; restart/replay and Scylla queries retain exact IDs. |
| Verification | Reserve futures, two-leg vertical, and four-leg Iron Condor compositions and prove every integer ID is unchanged end to end. |
| UI system | Composition view displays/searches reserved integer OrderId/TradeId values and does not present them as broker orders or filled trades. |

**Exit:** reservation is concurrency-safe and idempotent; no committed retry can return different IDs; gaps are tolerated but reuse is impossible.

### PF-13 — TradeSelection reservation handoff

**Depends on:** PF-11 and PF-12 plus the approved TradeSelection contract.

**Implementation:**

1. Update TradeSelection continuation to submit a reservation only for an accepted current `Selected` result.
2. Pass the frozen PortfolioFund snapshot and exact TradeSelection result/template/profile IDs, versions, hashes, workflow, invocation, and idempotency identity.
3. Treat `NoTrade`, failed, stale, expired, mismatched, or untradeable inputs as no reservation.
4. Make lost replies/retries safe through deterministic idempotency keys.
5. Persist the reservation reference in Strategy Workflow according to workflow authority; Portfolio retains navigation references only.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Selected reserves; NoTrade/failure/stale/expired/mismatch do not reserve; retry returns the original composition identity. |
| Unit | Continuation guard, identity/version/hash matching, idempotency-key derivation, request mapping, and error classification. |
| Integration | Real TradeSelection result through NATS reserves once, commits workflow reference, handles lost reply/retry, and emits no OrderComposition prematurely. |
| Verification | Daily future, Weekly bullish/bearish vertical, and Monthly neutral/bias Iron Condor selections bind the correct frozen assignment and IDs. |
| UI system | Strategy/Portfolio status shows Selected-to-Reserved linkage and precise stop reasons for NoTrade or invalid selection. |

**Exit:** only an accepted TradeSelection result can reserve; the workflow and Portfolio references agree; no later stage is dispatched on failure.

### PF-14 — OrderComposition result-reference handoff

**Depends on:** PF-13 and the approved OrderComposition boundary contract.

**Implementation:**

1. Add transitions from Reserved to Composing and then Composed/RiskPending, CompositionFailed, Cancelled, or Expired as permitted.
2. Send OrderComposition the reserved integer identities, selected immutable template/profile references, workflow attribution, and allowed fresh-data contract.
3. Record only the accepted result ID/hash/evaluation/expiry/reference; Strategy Workflow remains result authority.
4. Reject stale, expired, mismatched, duplicate-different, or invalid-state results.
5. Prohibit broker calls, fills, live TradeDb writes, and execution-state fabrication.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Reserved composition succeeds/fails/expires/cancels; stale or mismatched result cannot advance; accepted result retains reserved IDs. |
| Unit | Composition state table, result acceptance guard, version/hash/expiry checks, idempotent terminal replay, and mapping. |
| Integration | Real NATS handoff records accepted reference after restart/replay, fences concurrent terminal updates, and produces no broker/TradeDb effects. |
| Verification | Futures, vertical, and Iron Condor candidates return exact reserved identities and permitted result references across production actors. |
| UI system | Composition details show lifecycle, accepted result reference, reason/error, and exact legs when supplied by the query contract—never fill status. |

**Exit:** accepted OrderComposition references are durable and immutable; all invalid paths fail closed; execution boundaries remain untouched.

### PF-15 — Risk outcome reference and boundary fencing

**Depends on:** PF-14 and the RiskManagement reference contract.

**Implementation:**

1. Record accepted Risk result identity/hash/outcome against the correct FundOrder version.
2. Implement RiskPending to RiskApproved/RiskRejected transitions and idempotent terminal replay.
3. Validate Portfolio/Fund/workflow/order identity, candidate hash, envelope reference, currentness, and expiry.
4. Ensure approval is a recorded decision reference only and cannot invoke OrderExecution.
5. Add architecture/runtime fences against broker clients, execution subjects, fills, and live-position writes from Portfolio code.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Approve/reject valid candidate; reject stale/mismatched/expired/duplicate-different outcome; no outcome submits an order. |
| Unit | Risk transition rules, identity/hash/envelope matching, idempotency, expiry boundary, reason mapping, and prohibited dependency rules. |
| Integration | Real NATS records approved/rejected reference exactly once; restart/replay is stable; probes prove zero OrderExecution/TradeDb side effects. |
| Verification | Green/constrained/blocked envelope references preserve their accepted Risk outcomes and halt at the documented implementation boundary. |
| UI system | UI presents RiskPending/Approved/Rejected as decision status and offers no execute control introduced by this scope. |

**Exit:** Risk references are attributable and immutable; no PF actor can dispatch execution or create live trading state.

### PF-16 — Portfolio UI and legacy navigation coexistence

**Depends on:** PF-10 through PF-12; may use PF-13 through PF-15 status contracts when available.

**Implementation:**

1. Baseline the current UI framework and the existing `TomasAI.IFM.UI.Net.SystemTests` host.
2. Add a separate Portfolio navigation entry without removing or repurposing Funds.
3. Implement Portfolio list/detail/create/version/state, child Fund list/detail/create/version/state, assignments, allocations, and envelope views through typed APIs.
4. Display loading, empty, pending-projection, validation, conflict, timeout, unavailable, and unauthorized states.
5. Preserve operator-facing integer identities and accessible keyboard/search behavior.
6. Keep the existing Funds UI operational and clearly labeled as legacy where appropriate.
7. Treat every integer identity as sequence-allocated, read-only UI state: create actions allocate before opening the editor, version actions preserve identity, and allocation failure prevents the editor from opening.
8. Add `Delete Draft` only for the selected Draft Portfolio; require exact-code confirmation and reason, call the typed NATS command with current aggregate revision, refresh the Draft list, and never offer deletion for other states.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Administrator creates/configures and deletes a Draft Portfolio; reader cannot mutate; Active/Paused/Disabled/Retired states cannot delete and present correct permitted actions. |
| Unit | View-model state, validation mapping, command enablement, selector/filter logic, cancellation, and stale-response suppression. |
| Integration | UI service/view-model layer uses real typed NATS APIs for create/update/query/conflict/timeout/authorization paths. |
| Verification | Production UI host completes one Portfolio plus Daily/Weekly/Monthly Fund configuration journey against real infrastructure. |
| UI system | Navigation, accessibility smoke, CRUD/version/state flows, errors, refresh, integer display/search, and continued Funds navigation all pass. |

**Exit:** the new Portfolio UI is usable through actor APIs and legacy Funds remains operational; no direct storage dependency exists.

### PF-17 — Portfolio/Fund Trade composition views

**Depends on:** PF-12 through PF-16.

**Implementation:**

1. Change Trade composition filtering to Portfolio then Fund while preserving the existing manual-blotter interaction style.
2. Remove Create Fund from the new Trade composition workflow.
3. Add FundOrder/FundOrderTrade lists, integer OrderId/TradeId search, selection, and detail navigation.
4. Distinguish planned composition, OrderComposition result, Risk result, and future execution truth in labels and view models.
5. Preserve the legacy/manual blotter until separately retired; do not reinterpret current TradeDb records as new Portfolio records.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Operator selects Portfolio/Fund, views planned compositions, finds Order/Trade IDs, and cannot create a Fund from the Trade screen. |
| Unit | Cascading selector state, paging/filter/search, identity parsing, detail mapping, empty/error state, and stale selection reset. |
| Integration | Real typed queries return Portfolio-scoped composition pages/details and enforce cross-Portfolio isolation and bounded paging. |
| Verification | Futures, vertical, and Iron Condor reservations navigate from Portfolio/Fund to exact order/trade detail without execution-state confusion. |
| UI system | End-to-end selectors, grids, search, detail selection, back/refresh behavior, absent Create Fund, and legacy blotter coexistence pass. |

**Exit:** composition navigation is Portfolio-centric, identity-stable, and clearly separated from broker/live TradeDb semantics.

### PF-18 — Full acceptance and regression qualification

**Depends on:** PF-01 through PF-17.

**Implementation:**

1. Inventory every normative specification requirement and map it to one or more executable tests.
2. Close coverage gaps without duplicating the same assertion across layers without purpose.
3. Run all Portfolio BDD, unit, integration, verification, and UI system suites independently and together.
4. Run affected solution-wide regression suites for Sequence, actor/event projector, messaging, storage, Strategy Workflow, TradeSelection, and UI.
5. Add bounded concurrency/restart/rebuild/timeout qualification and eliminate flakes.
6. Record representative combination coverage rather than claiming exhaustive market-strategy completeness.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Complete feature suite covers lifecycle, assignment, resolution, reservation, composition, Risk reference, cancellation/expiry, and boundary prohibitions. |
| Unit | All deterministic contracts/rules achieve agreed critical-path coverage; mutation/branch review confirms transition and validation edges are exercised. |
| Integration | Full real-infrastructure suite covers NATS, PostgreSQL, ScyllaDB, replay/rebuild, restart, concurrency, paging, timeout, and cleanup. |
| Verification | Entire representative catalog plus invalid configuration, duplicate reservation, failure, stale, expiry, and no-execution cases passes. |
| UI system | Full Portfolio and composition user journeys, authorization/error/accessibility smoke, legacy coexistence, and lifecycle cleanup pass. |

**Exit:** requirement-to-test traceability is complete; all targeted and affected regression suites are green with zero unexplained skips/flakes.

### PF-19 — Legacy Fund read-only isolation and no-dual-write audit

**Depends on:** PF-18.

**Implementation:**

1. Introduce/finish `FundLegacyDbContext` and only the read-only interfaces needed to preserve historical access.
2. Prove new Portfolio/Fund actors, APIs, projectors, and UI never write legacy tables or route new commands to legacy Fund actors.
3. Prove legacy UI does not write new Portfolio projections or event streams.
4. Audit dependency graphs, registrations, connection configuration, CQL, NATS subjects, and runtime traces for cross-boundary writes.
5. Retain legacy data and UI; perform no migration or deletion.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | New Portfolio workflows leave legacy history unchanged; legacy viewing remains possible; no compatibility migration is implied. |
| Unit | Architecture tests reject forbidden project/type dependencies and mutation interfaces on `FundLegacyDbContext`. |
| Integration | Database/NATS probes fingerprint legacy and new stores before/after representative commands and prove no cross-write or cross-route. |
| Verification | Run the representative new workflow beside legacy read scenarios and compare store/event/subject audit evidence. |
| UI system | Both menu paths remain available; actions in one context do not create/update records in the other; legacy removal is absent. |

**Exit:** static and runtime audits prove isolation; legacy data is preserved; no dual-read/dual-write behavior exists.

### PF-20 — Operational qualification and release evidence

**Depends on:** PF-19.

**Implementation:**

1. Complete API, schema, actor, UI, operations, recovery/rebuild, and deferred-work documentation.
2. Verify structured traces/logs include Portfolio/Fund/Order/Trade/workflow/command/correlation/causation identities, versions, result hashes, and reason codes without secrets.
3. Add metrics and alerts for command outcomes, conflicts, resolution failures, projection lag/failure, reservation latency/idempotent replay, and query latency.
4. Enforce authorization policies for Portfolio, Fund, allocation/envelope, assignment, manual composition, read-only, and explicitly absent execution authority.
5. Establish performance baselines for active-Fund resolution, composition reservation, paged queries, projection rebuild, and concurrent distinct-Fund commands.
6. Produce final gate ledger, test reports, schema/version manifest, known-deferred register, rollback/disable procedure, and release recommendation.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Authorized/unauthorized operational personas receive correct outcomes and prohibited execution remains impossible. |
| Unit | Telemetry field mapping/redaction, authorization policy mapping, metric labels/cardinality guards, configuration validation, and health evaluation. |
| Integration | Trace/metric capture, auth enforcement, health checks, projector recovery, service restart, and bounded load baselines over real infrastructure. |
| Verification | Complete production-shaped success/failure run produces correlatable operational evidence from trigger through Risk reference and stops before execution. |
| UI system | Operations-visible status, permission-driven controls, correlation/error details, service-unavailable recovery, and acceptable baseline responsiveness pass. |

**Exit:** PF-01 through PF-20 evidence is approved; documentation and observability are operationally usable; performance has recorded baselines; release scope contains no deferred execution work.

### PF-21 — Revised contract baseline and obsolete-surface removal

**Depends on:** approved v1.1 specification.

**Implementation:**

1. Inventory current PortfolioCode, Guid PolicyId, fabricated policy fallback, Planned Compositions, and direct/legacy Trade Orders dependencies.
2. Remove PortfolioCode from the authoritative Portfolio contract and editors; reserve MessagePack key 1 and advance Portfolio SchemaVersion without renumbering later keys.
3. Change Portfolio policy reference to positive integer ActivePolicyId/ActivePolicyVersion and prohibit fallback identities.
4. Mark `PortfolioCompositionForm` and its view model/tests for removal in PF-28; remove the Portfolio Administration navigation requirement immediately.
5. Record the deliberate no-migration/no-backward-compatibility decision for prior Portfolio/Fund data.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Draft Portfolio needs no policy; Active requires a real integer policy reference; PortfolioCode is not a business requirement. |
| Unit | Reserved MessagePack key, schema version, positive policy reference, and no-fallback validation. |
| Integration | Revised Portfolio DTO traverses real NATS and persists/projects without PortfolioCode or Guid policy identity. |
| Verification | Create/version/query representative Portfolio and prove exact integer identity chain with no legacy-data dependency. |
| UI system | Portfolio dialogs contain no PortfolioCode/raw policy input and Portfolio Administration exposes no Planned Compositions action. |

**Exit:** contradictory v1.0 surfaces are removed or explicitly fenced; serialized keys are approved; no compatibility adapter was introduced.

### PF-22 — ReferenceDb TradeStrategyFamily catalog

**Depends on:** PF-21.

**Implementation:**

1. Add TradeStrategyFamilyId/DefinitionVersion contracts and `Reference_TradeStrategyFamilyId` sequence mapping.
2. Add query-shaped `trade_strategy_family_v2` CQL/schema keyed by stable SystemKey/DefinitionVersion, typed read context, point/list query DTOs, and Reference NATS client/API.
3. Implement concurrency-safe idempotent bootstrap by stable key for exactly FUTURES, VERTICAL_SPREAD, and IRON_CONDOR version 1 Active rows.
4. Expose the three rows in the existing Reference screen read-only with no mutation controls.
5. Register no public TradeStrategyFamily command API; defer management and variants to v1.x.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | The catalog contains exactly the three approved broad families and excludes Long/Short/directional variants. |
| Unit | Stable keys, DTO validation/serialization, sequence mapping, seed definitions, ordering, and duplicate detection. |
| Integration | Real PostgreSQL sequence + ReferenceDb + NATS bootstrap/query; repeated, concurrent, and restart bootstrap produces exactly three unique rows. |
| Verification | Production-shaped Reference query returns exact IDs/versions/names in deterministic order and remains read-only. |
| UI system | Reference screen lists three definitions and exposes no Add/Edit/Retire/Delete controls under authorized and read-only personas. |

**Exit:** schema/bootstrap/query/UI evidence passes with exactly three active immutable definitions and no public write path.

### PF-23 — Risk Policy identities, DTOs, validation, and allocation

**Depends on:** PF-22.

**Implementation:**

1. Add PortfolioFinancialPolicyId, state enum, PortfolioFinancialPolicyReadModel, and TradeFamilyRiskLimitReadModel with stable MessagePack keys.
2. Add `PortfolioPolicy_PolicyId` sequence and typed allocation API.
3. Implement global and per-family validation, decimal base-currency semantics, exact family version validation, and zero-means-blocked behavior.
4. Add stable policy/family reason codes and canonical defensive-copy/hash behavior.
5. Extend Portfolio/Fund/template/snapshot contracts to carry exact family and policy identities/versions without display-string inference.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Valid global/family policy, disabled family, zero capacity, family-over-global rejection, and sequence-gap behavior. |
| Unit | MessagePack keys/round trips, enum values, decimal boundaries, every invariant, family uniqueness/versioning, hash determinism, and overflow. |
| Integration | Real PostgreSQL policy allocation and raw/typed NATS serialization retain exact IDs/versions under concurrency/restart. |
| Verification | Futures, Vertical Spread, and Iron Condor representative DTOs compute the most restrictive configured caps. |
| UI system | Binding contract renders read-only PolicyId/base currency and independent editable rows for the selected family. |

**Exit:** contracts are frozen; validators and sequence allocation pass; no integer or family identity can be fabricated.

### PF-24 — PortfolioFinancialPolicy aggregate and lifecycle

**Depends on:** PF-23.

**Implementation:**

1. Implement event-sourced policy aggregate and immutable saved Draft/Active versions.
2. Implement Create, AddVersion, ActivateAndAssign, Retire, and DeleteDraft commands/events with expected revisions and audit reasons.
3. Coordinate activation/assignment idempotently across policy and Portfolio so partial failure preserves the prior valid assignment.
4. Enforce effective-now activation, one Active selection, supersession, reference-safe retirement, and never-active/unreferenced Draft deletion.
5. Preserve consumed IDs and authoritative tombstones.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Complete create/version/activate/assign/supersede/retire/Delete-Draft lifecycle for Draft and Active Portfolios. |
| Unit | Transition matrix, expected revisions, validation, idempotent replay/conflict, retirement/reference checks, and deletion eligibility. |
| Integration | Real NATS/PostgreSQL concurrent activation/retry/restart proves one logical assignment and no partial state. |
| Verification | Valid and invalid global/family policies across all lifecycle states fail closed with stable reason codes. |
| UI system | Public command result contracts expose the states/errors required for later modal behavior without direct storage access. |

**Exit:** aggregate history is deterministic and recoverable; atomic policy replacement and reference safety are proven.

### PF-25 — Policy persistence, projections, replay, and deletion fences

**Depends on:** PF-24.

**Implementation:**

1. Add EventSourceDb repositories/snapshots for policy streams and coordinated command outcomes.
2. Add PortfolioDb `portfolio_policy_by_id`, `portfolio_policy_by_portfolio`, and `active_portfolio_policy` tables and typed read/write contexts.
3. Implement durable idempotent projectors with source EventId monotonic write/delete fences.
4. Implement exact point/list/current queries, paging, tombstone cleanup, rebuild, reconciliation, and restart recovery.
5. Prove delayed delivery cannot resurrect a deleted Draft or regress an assigned version.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Committed history remains visible while eligible Draft deletion removes operational projections only. |
| Unit | Projection mapping, timestamp/event fences, paging tokens, tombstone ordering, and rebuild hashes. |
| Integration | Real PostgreSQL/Scylla live projection, duplicate/out-of-order delivery, deletion, two rebuilds, and full-host restart. |
| Verification | Query catalog before/after rebuild is hash-equivalent and deleted Draft cannot reappear. |
| UI system | Projection-pending and refreshed-policy DTO behavior is deterministic under bounded polling. |

**Exit:** EventSourceDb is authoritative; all policy query shapes rebuild exactly and deletion fencing passes.

### PF-26 — Typed policy/reference APIs and frozen pipeline propagation

**Depends on:** PF-25.

**Implementation:**

1. Register policy command/query actors, typed clients, subjects, DI, authorization hooks, timeout/cancellation mapping, and observability.
2. Register read-only TradeStrategyFamily Reference queries and clients without exposing a mutation verb.
3. Resolve exact assigned policy plus full family limits into PortfolioFundStrategySnapshot and canonical hash.
4. Require Fund mandate/template family references to match the frozen enabled family definition.
5. Carry global/family limits into TradeSelection eligibility and RiskManagement input; no stage resolves latest configuration mid-workflow.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Missing/disabled/stale family or policy stops safely; valid family proceeds without widening limits. |
| Unit | Subject/entity mapping, error mapping, cancellation, snapshot immutability/hash, and exact version matching. |
| Integration | Real NATS command/query/cancellation/restart routes over PostgreSQL, ReferenceDb, and PortfolioDb. |
| Verification | Three-family catalog flows through resolution, selection eligibility, composition identity, and Risk input unchanged. |
| UI system | Public APIs provide all policy/reference/loading/error states required by Portfolio and Reference screens. |

**Exit:** stable public APIs and frozen snapshot propagation pass; no UI/pipeline direct database access exists.

### PF-27 — Compact Portfolio Administration and Risk Policy modal

**Depends on:** PF-26.

**Implementation:**

1. Replace the six-button Portfolio bar with Refresh, New Portfolio, Risk Policy, and Portfolio Actions; label Show State as a filter.
2. Put New Version, Change State, and conditional Delete Draft in Portfolio Actions; remove Planned Compositions.
3. Implement the modal header, bounded policy/version list, global-limit groups, Reference-backed family selector/limits, effective/audit detail, and status area.
4. Implement New Policy allocation, immutable New Version, Save/Cancel/unsaved confirmation, Activate & Assign preview, Retire, typed Delete Draft, and permission-driven controls.
5. Implement validation, zero-blocking display, pending projection, conflict refresh/review, timeout, unavailable, unauthorized, and accessibility behavior.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Operator creates and assigns a valid family-limited policy and cannot perform invalid lifecycle actions. |
| Unit | View-model state/actions, field/summary validation, family selection isolation, dirty state, action eligibility, and error mapping. |
| Integration | UI service layer uses real typed NATS for allocation/create/version/activate/query/delete and projection refresh. |
| Verification | Draft and Active Portfolio operator journeys cover all three families, conflicts, restart, and no direct storage. |
| UI system | Exact command bar/modal layout, accessibility, read-only IDs, validations, confirmations, role states, and lifecycle journeys. |

**Exit:** the compact Portfolio/Risk Policy UI passes automated and real-host operator qualification with no obsolete action.

### PF-28 — Unified Portfolio-to-Fund Trade Orders UI

**Depends on:** PF-26.

**Implementation:**

1. Add Portfolio selection before Fund and clear/cancel every dependent load when scope changes.
2. Query Funds only from the selected Portfolio and query canonical manual/StrategyWorkflow FundOrders from the new authority.
3. Add Source/status columns and All/Manual/Strategy Workflow filtering while preserving integer OrderId/TradeId selection and detail behavior.
4. Retain eligible manual Create Order/Add Trade, remove Create Fund, and make automated/accepted compositions read-only.
5. Display workflow/template/profile/composition/risk provenance in the existing detail area.
6. Fence submit/fill/live-feed/End-of-Day/position actions for new pre-execution records.
7. Remove `PortfolioCompositionForm`, its view model/navigation/tests, and all dual-write/direct-storage paths.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Manual and automated compositions appear in one Fund-scoped flow with correct editability and no execution side effect. |
| Unit | Cascading selection, stale-load generation, source filtering, action eligibility, mapping, and provenance display state. |
| Integration | Real NATS loads and mutates canonical manual orders; automated orders arrive in the same queries; no legacy cross-write. |
| Verification | Portfolio A/B and Fund A/B switching, manual/automated sources, restart, integer lookup, and pre-execution fencing. |
| UI system | End-to-end Portfolio→Fund→Order→Trade interaction, source filters, removed controls/viewer, read-only automated state, and accessibility. |

**Exit:** Trade Orders is the sole composition UI; new operations use one authority; the separate viewer and Planned Compositions path are absent.

### PF-29 — Cross-pipeline qualification and regression

**Depends on:** PF-27 and PF-28.

**Implementation:**

1. Execute all Portfolio, Reference, Sequence, NATS, storage, workflow, TradeSelection, Risk boundary, and UI suites independently and together.
2. Qualify pairwise global/family/Fund-envelope cases without uncontrolled Cartesian expansion.
3. Exercise concurrency, restart, rebuild, cancellation, timeout, authorization, cleanup, and UI stale-response behavior.
4. Prove no broker, fill, live-position, or legacy dual-write effect.
5. Produce requirement-to-test traceability for PF-21 through PF-29.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Full revised lifecycle and manual/automated composition feature suite passes. |
| Unit | All revised contracts, rules, transitions, UI state, and architecture tests pass with reviewed critical branches. |
| Integration | Full real NATS/PostgreSQL/ReferenceDb/PortfolioDb suite passes with restart/rebuild/concurrency and zero unexplained skips. |
| Verification | Representative three-family/global/envelope/manual/automated catalog passes with exact identity/version propagation. |
| UI system | Complete Reference, Portfolio/Risk Policy, and Trade Orders journeys pass with cleanup and accessibility. |

**Exit:** all revised requirements have green five-layer evidence and affected regressions contain no unexplained failure, skip, or flake.

### PF-30 — Operational qualification and release approval

**Depends on:** PF-29.

**Implementation:**

1. Reconcile HLD, specification, implementation plan, schemas, APIs, operations, recovery, and deferred-work documents.
2. Capture traces/metrics for family bootstrap/query, policy lifecycle/assignment, projection lag/rebuild, and Trade Orders queries without high-cardinality labels or secrets.
3. Qualify authorization policies, service health, performance/load baselines, rollback/disable behavior, and full-host operator journeys.
4. Audit all historical Partial gates; link remaining applicable evidence to PF-21+ or keep a concrete release blocker.
5. Produce final manifest, commands/results, commit IDs, known deferrals, and release recommendation.

**Test obligations:**

| Layer | Required evidence |
| --- | --- |
| BDD | Authorized/unauthorized personas and prohibited deferred operations retain correct business outcomes. |
| Unit | Telemetry/redaction, authorization, health/configuration, metric-cardinality, and rollback feature-switch behavior. |
| Integration | Real trace/metric/auth/health/restart/rebuild/load qualification with captured bounded results. |
| Verification | Production-shaped Reference→Policy→Portfolio/Fund→TradeSelection→Composition→Risk and manual Trade Orders paths stop before execution. |
| UI system | Real-host Reference, Portfolio/Risk Policy, and Trade Orders operator acceptance with responsive recovery/error behavior. |

**Exit:** PF-21 through PF-30 are Complete, applicable historical gaps are resolved, documents are reconciled, and the release recommendation is evidence-backed.

## 9. Representative verification catalog

The verification suite must include at least the following pairwise representative cases. More cases are added when a new rule creates a distinct outcome; cases are not multiplied merely because fields can be permuted.

| Case | Portfolio/Fund configuration | Selection/composition | Expected Portfolio/Fund outcome |
| --- | --- | --- | --- |
| V01 | Active / Daily ES | Directional future | One FundOrder and one FundOrderTrade with stable integer IDs |
| V02 | Active / Weekly ES | Bullish vertical | Matching template/profile; one OrderId and required TradeIds |
| V03 | Active / Weekly ES | Bearish vertical | Matching template/profile; stable replay |
| V04 | Active / Monthly ES | Neutral Iron Condor | Matching neutral configuration and stable multi-trade IDs |
| V05 | Active / Monthly ES | Bullish-bias Iron Condor | Matching biased configuration |
| V06 | Active / Monthly ES | Bearish-bias Iron Condor | Matching biased configuration |
| V07 | Paused/ReduceOnly/Retired Portfolio | New exposure | Safe configuration/permission stop as defined by state/policy |
| V08 | Paused/Disabled/Expired Fund | Any | Configuration failure; no reservation |
| V09 | Missing active Fund | Any | Deterministic configuration failure |
| V10 | Duplicate active Fund | Any | Deterministic ambiguity failure |
| V11 | Constrained envelope | Compatible candidate reference | Constraints remain frozen for Risk; no widening |
| V12 | Blocked/expired envelope | New exposure | No new-exposure permission |
| V13 | Identical reservation retry | Same key and canonical payload | Original OrderId/TradeIds returned |
| V14 | Conflicting reservation retry | Same key, changed canonical payload | `IdempotencyConflict` |
| V15 | Stale/expired downstream result | Any | Result rejected; no forward transition |
| V16 | Risk Approved/Rejected | Valid result reference | Outcome recorded; zero OrderExecution effects |
| V17 | Projector rebuild | Representative catalog | Query results/hash-equivalent before and after rebuild |
| V18 | Legacy coexistence | New plus legacy reads | No cross-write; both UI navigation paths work |
| V19 | Repeated/concurrent Reference bootstrap | Three required stable keys | Exactly three unique Active definitions with retained sequence IDs |
| V20 | Futures enabled below global caps | Candidate inside family/global/envelope | Most restrictive remaining capacity is applied |
| V21 | Vertical Spread disabled | Matching template exists | Eligibility stops before composition |
| V22 | Iron Condor enabled with zero family risk | Matching template exists | Family remains configured but new exposure is blocked |
| V23 | Family cap exceeds global cap | Draft policy | Activation rejected with stable family-limit reason |
| V24 | Active policy replacement retry | Same idempotency key/payload | One policy activation and one exact Portfolio version assignment |
| V25 | Trade Orders manual plus StrategyWorkflow | Same Portfolio/Fund/month | Both sources visible; automated record read-only; no execution action |
| V26 | Rapid Portfolio/Fund selection change | Delayed earlier response | Only the latest selected scope is rendered |

## 10. Cross-gate non-functional test matrix

| Concern | First proving gate | Final qualification |
| --- | --- | --- |
| MessagePack compatibility | PF-01 | PF-18 |
| Positive integer identity allocation | PF-02 | PF-12/PF-18 |
| Optimistic concurrency | PF-03/PF-04 | PF-18 |
| Event replay/snapshots | PF-07 | PF-18 |
| Projection idempotency/rebuild | PF-09 | PF-18/PF-20 |
| NATS timeout/cancellation/restart | PF-10 | PF-18/PF-20 |
| Deterministic active Fund resolution | PF-11 | PF-18 |
| Reservation idempotency/concurrency | PF-12 | PF-18/PF-20 |
| Pipeline boundary failure closure | PF-13/PF-14/PF-15 | PF-18 |
| No broker/live-position effect | PF-03 onward | PF-15/PF-18/PF-19 |
| UI accessibility/error handling | PF-16 | PF-18/PF-20 |
| Reference catalog/bootstrap/read-only UI | PF-22 | PF-29/PF-30 |
| Policy global/family validation and lifecycle | PF-23/PF-24 | PF-29/PF-30 |
| Policy persistence/rebuild/atomic assignment | PF-25/PF-26 | PF-29/PF-30 |
| Compact Portfolio/Risk Policy UI | PF-27 | PF-29/PF-30 |
| Unified Trade Orders and stale-load fencing | PF-28 | PF-29/PF-30 |
| Legacy isolation | PF-01 onward | PF-19 |
| Authorization/redaction | PF-03 onward | PF-20 |
| Performance/load baseline | PF-09 onward | PF-20 |

## 11. Qualification commands

Exact filters and UI commands are finalized when PF-01 confirms the test framework and PF-16 confirms the UI harness. The intended independently runnable command shape is:

```powershell
dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.Domain.Portfolio.BDDTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.Domain.Portfolio.VerificationTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.UI.Net.SystemTests --filter "Category=Portfolio"
```

PF-16 reuses the repository’s active `TomasAI.IFM.UI.Net.SystemTests` harness rather than creating a competing UI test stack. UI presentation-unit coverage belongs in `TomasAI.IFM.UI.Net.Presentation.UnitTests`; the UI system evidence named by every gate belongs in `TomasAI.IFM.UI.Net.SystemTests`.

Final qualification also runs affected existing SequenceId, EventProjector, NATS messaging, storage, Strategy Workflow, TradeSelection, OrderComposition-boundary, and UI suites.

## 12. Gate-status ledger

| Gate | Current status | Completion evidence location |
| --- | --- | --- |
| PF-01 | Complete | 2026-08-29: BDD 2, unit 6, integration 1, verification 1, UI system 1; all passed |
| PF-02 | Complete | 2026-08-30: typed NATS allocation covers Portfolio/Fund/Policy/Order/Trade; the production API actor host allocated every identity as a positive integer and editors expose allocated values without hand entry or fallback |
| PF-03 | Complete | 2026-08-30: production Portfolio actor create/state route, PostgreSQL authority, Scylla projection, full host restart, durable idempotent create replay/conflict, lifecycle tests, and no-execution evidence pass |
| PF-04 | Complete | 2026-08-30: production PortfolioFund actor creates and activates an owned mandate through real NATS/PostgreSQL, resolves it from Scylla, and reloads identical active state after full API-host restart; BDD/unit/catalog/UI contracts pass |
| PF-05 | Complete | 2026-08-30: versioned template/hint/composition-profile assignment is committed through the production actor, projected, frozen into a snapshot, and retained after host restart; Daily/Weekly/Monthly catalog tests pass |
| PF-06 | Complete | 2026-08-30: membership-bound allocation and FundRiskEnvelope are committed through real NATS, projected, frozen into strategy resolution, and retained after restart; available/constrained/blocked/expired catalog passes |
| PF-07 | Complete | 2026-08-29: native EventSourceDb events, expected-version append, committed-command lookup, separate non-authoritative snapshots, strict unknown-contract rejection; BDD 2, unit 3, real-PostgreSQL integration 2, verification 2, UI system 2; all passed |
| PF-08 | Complete | 2026-08-30: 13-table PortfolioDb schema including `fund_allocation`, split typed read/write contexts, bounded partition queries, monotonic EventId Scylla timestamps, no-ALLOW-FILTERING/legacy audit and teardown; real-Scylla integration passed |
| PF-09 | Complete | 2026-08-30: durable descriptors cover every event; failed target mutation cannot report success, retry replays the authoritative event, and a representative Portfolio/Fund/allocation/envelope/assignment/order/trade/workflow catalog rebuilds twice from PostgreSQL into empty Scylla with identical hashes. Duplicate/old delivery, live projection, and full-host restart evidence also pass |
| PF-10 | Partial | 2026-08-30: production command/query actors, stable typed clients, typed aggregate-revision queries, raw real-NATS serialization, live configure/resolve/reserve/compose/Risk/query routes, cancellation and correlation tests, and prior full-host restart pass. Exhaustive live failure mapping for every API plus the new identity/revision route qualification remain to run with the API actor host online |
| PF-11 | Complete | 2026-08-30: deterministic fail-closed resolver, canonical immutable snapshot/hash, and representative catalog pass; production NATS resolves real Scylla configuration and returns the same version-complete configuration after full host restart |
| PF-12 | Complete | 2026-08-30: concurrent identical production NATS reservations yield one committed OrderId/TradeId set, replay returns the exact IDs, PostgreSQL/Scylla/restart retain them, and conflict plus futures/vertical/Iron-Condor cases pass |
| PF-13 | Blocked | Portfolio-side accepted-selection guards/contracts/idempotency and representative tests are complete. Production continuation cannot be wired until the currently skeletal TradeSelection pipeline actor/result contract is implemented |
| PF-14 | Blocked | Portfolio-side fail-closed transitions, immutable result reference, real NATS acceptance, replay/projection, and restart are complete. Production dispatch/return fencing awaits an implemented OrderComposition actor |
| PF-15 | Blocked | Portfolio-side candidate-hash/envelope/currentness validation, Approved/Rejected recording, real NATS/restart, and no-execution fence pass. An actual RiskManagement actor and runtime side-effect probe do not yet exist |
| PF-16 | Partial | 2026-08-30: the Funds-style Portfolio administration UI now implements Portfolio and child-Fund list/detail/create/version/state, allocation, risk-envelope and assignment editors, typed sequence allocation, correct aggregate concurrency revisions, loading/error/role states, accessible controls, and separate legacy Funds navigation. Automated UI tests pass; the real-host operator journey and user review remain |
| PF-17 | Partial | 2026-08-30: the planned-composition UI implements Portfolio/Fund cascading scope, month lists, integer OrderId/TradeId search, refresh/close/detail behavior and non-execution semantics without Create Fund; automated UI contracts pass. The real-host operator journey and user review remain |
| PF-18 | Partial | 2026-08-30: Portfolio unit 58, BDD 17, default integration 18, verification 22, full UI-presentation 244, and full UI-system 50 pass with zero skips; the Portfolio UI application graph builds with 0 warnings/errors. Host-qualified tests correctly report no responders while the API actor host is offline. Remaining partial/blocked gates and affected solution-wide qualification remain |
| PF-19 | Partial | 2026-08-30: read-only `FundLegacyDbContext`, no-mutation/no-execution architecture tests, preserved legacy UI and no migration/deletion implemented; runtime cross-store fingerprint/subject audit remains |
| PF-20 | Partial | 2026-08-30: operations/recovery document, bounded authorization/telemetry contracts, hash redaction and tests implemented; middleware enforcement, captured telemetry/health and performance/load baselines remain |
| PF-21 | Complete | 2026-08-30: PortfolioCode and Guid/fallback policy identities are absent from production/tests; MessagePack key 1 is reserved, schema v2 uses positive policy identity/version, obsolete Planned Compositions navigation/types are removed, and revised DTOs pass real NATS plus UI contracts |
| PF-22 | Complete | 2026-08-30: sequence-backed exact three-family catalog, stable-key/version Scylla LWT bootstrap, typed Reference NATS list query, startup bootstrap, and read-only Reference UI pass. Eight simultaneous independent bootstrap processes plus restart produce exactly three stable unique rows with sequence IDs; the focused process integration test passes 1/1 |
| PF-23 | Complete | 2026-08-30: positive policy identity allocation, versioned DTOs, MessagePack contracts, stable validators/reason codes, canonical hashing, zero-blocking semantics, and most-restrictive global/family/envelope caps are implemented and pass BDD/unit/verification/UI plus live allocation evidence |
| PF-24 | Complete | 2026-08-30: event-sourced lifecycle, expected revisions, replay/conflict and reference fences pass. Injected failure after policy append/before Portfolio assignment is healed idempotently on retry, including missed projections; concurrent replacements serialize and exactly one matching expected revision succeeds. Focused integration tests pass 3/3 |
| PF-25 | Complete | 2026-08-30: authoritative policy events/snapshots, Scylla query projections, delete tombstones, projector registration and rebuild pass. A delayed pre-delete event cannot resurrect policy state, and two empty-store full rebuilds produce identical query/catalog hashes. Focused real-storage tests pass 3/3 |
| PF-26 | Complete | 2026-08-30: policy command/query actors and clients, read-only Reference query actor/client, split read/write DI, cancellation-aware APIs, exact assigned policy/family snapshot propagation and effective caps are implemented. Production host tests pass typed Reference query and Policy create/activate/assign through frozen workflow resolution |
| PF-27 | Partial | 2026-08-30: compact command bar and complete Risk Policy modal are implemented. Automated rendered-form journeys cover create/edit/save, dirty-close rejection, seven disabled mutation actions for an unauthorized persona, accessibility and layout; focused UI system tests pass 4/4. A real desktop-control run remains because the Windows automation helper failed initialization twice with `failed to write kernel assets: path not found`; operator acceptance is not claimed |
| PF-28 | Complete | 2026-08-30: Trade Orders queries Portfolio then Fund and canonical Manual/StrategyWorkflow orders, explicit origin/status filtering, integer IDs and pre-execution fences. Manual Create Order now sends a typed Portfolio/Fund command; the actor validates active current versions, sequence-allocates OrderId, commits/projects an idempotent non-executable Draft and performs no legacy write. Generation/identity fencing rejects delayed scope responses. BDD 1, unit 1, integration 2 (including production-host NATS), verification 1 and UI system 3 pass |
| PF-29 | Partial | 2026-08-30: final-code Portfolio unit 57, BDD 18, real integration 29, verification 28, Portfolio UI system 17 and UI presentation 4 pass with zero failures/skips. PF-22/24/25/28 explicit gaps are closed and the production-host PF-28 manual/automated NATS journey passes. PF-27 hands-on acceptance plus broader authorization/load evidence still prevent PF-29 closure |
| PF-30 | Partial | 2026-08-30: HLD/specification/plan/schema terminology are reconciled; the full solution serial build passes with 0 warnings/errors (the parallel build exposes an existing native Databento build-directory lock); the production API host creates/bootstraps the v2 Reference catalog, starts Reference/Portfolio/Policy actors, serves live typed tests, tolerates unavailable external market data, and shuts down cleanly. Authorization enforcement, captured trace/metric baselines, load/rollback qualification and interactive operator approval remain release blockers |

Allowed statuses are `Not Started`, `In Progress`, `Partial`, `Blocked`, and `Complete`. `Partial` must identify the missing deliverable or test evidence; it cannot be used as a permanent closure state.

## 13. Explicit deferred register

The following remain outside every PF gate:

1. Broker OrderExecution, submission, acknowledgement, replace, cancel, and reconciliation.
2. Broker order identifiers, fills, partial fills, overfills, and fill correction.
3. Live TradeDb order/trade/position creation and market-feed position updates.
4. RiskManagement calculation internals beyond its accepted result-reference contract.
5. OrderComposition algorithms beyond their boundary contract.
6. Migration/deletion of legacy Fund, order, trade, or position history.
7. Removal of the legacy Funds UI or manual blotter.
8. Multi-asset/unrestricted template ranking and advanced Portfolio optimization.
9. High-throughput ScyllaDB sequence/tick identity redesign.
10. Expansion of current operator-facing integer IDs beyond checked Int32 contracts.
11. TradeStrategyFamily mutation commands and management UI.
12. Strategy-family variants/subtypes including Long, Short, bullish, bearish, neutral, debit, and credit.
13. Scheduled policy activation and generic expression/rule engines.

## 14. Plan definition of done

This implementation plan is complete only when:

- PF-21 through PF-30 are `Complete` with five-layer evidence and every still-applicable historical Partial item is resolved or remains an explicit release blocker;
- all approved specification requirements map to implementation and tests;
- real NATS, PostgreSQL, ScyllaDB, actor restart, replay, rebuild, concurrency, and failure paths pass;
- representative Daily/Weekly/Monthly configurations and invalid variants pass verification;
- Portfolio and composition UI system journeys pass while legacy navigation remains operational;
- ReferenceDb contains exactly the three read-only v1 families after repeated/restart bootstrap;
- Risk Policy global and per-family limits are immutable, versioned, and atomically assigned;
- Trade Orders is the sole manual/StrategyWorkflow composition view with Portfolio-to-Fund scoping;
- OrderId and TradeId values remain unchanged through all implemented stages;
- no actor, projector, API, UI, or test path performs an OrderExecution or live-position side effect;
- legacy Fund data has neither migrated nor received new-domain writes;
- all temporary files and test-owned ephemeral data are cleaned;
- final operational/performance evidence is recorded; and
- deferred work remains explicitly deferred.
