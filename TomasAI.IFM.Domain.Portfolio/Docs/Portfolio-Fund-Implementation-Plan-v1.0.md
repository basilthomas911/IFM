# Portfolio and Fund Implementation Plan v1.2

> **Emulator scope correction (2026-09-08):** The IBKR emulator design has not started; its implementation is a future delivery after design approval. Do not list emulator fills, fees, settlement, cancellation or reconciliation as unfinished emulator work in the current Portfolio delivery. Current scope covers Portfolio accounting/capacity contracts, consumers and financial integrity tests using explicitly labelled execution-fact fixtures. Those tests do not qualify an emulator. Existing local submission/admission scaffolding is not a functioning emulator or evidence of broker acceptance. Full emulator integration is deferred; it is not a blocker for completing the current Portfolio development scope.

> **Development scope decision (2026-09-08):** The system is strictly in development while the complete trading system is being built. Production security implementation and qualification are deferred until immediately before production deployment. They do not block current development implementation or PF-FIN gate completion. Retain existing role/scope checks and all financial integrity rules; development principal/role metadata is not authenticated identity. Opening capital remains development-only. Completing development gates does not authorize production deployment.

> **Actor responsibility revision (2026-09-08):** The financial phase uses exactly two Function actors: `CapacityReservationFunctionActor` and `CapacityConsumptionFunctionActor`. `GeneralLedgerCommandActor` handles single/batch posting; `CapacityReservationCommandActor` handles subsequent lifecycle changes. Configuration uses Command actors and reads use Query actors. Both financial Command and Function writes require atomic PostgreSQL business/receipt/event persistence; changing actor type does not permit eventual financial authority. This supersedes the earlier four-Function proposal. The initial status was Not Started; section 22 records the current In Progress status.

> **Financial implementation revision (2026-09-08):** Sections 15–22 specify the new `GeneralLedger` and `CapacityReservation` delivery, including the shared transactional Function lifecycle, Risk/Fund/workflow integration, reconciled legacy transaction migration and Portfolio financial UI. They implement the requirements of specification v1.2 sections 37–46 and design v0.3 sections 27–34. All seven PF-FIN gates were initially **Not Started**; section 22 records subsequent implementation and test evidence. Sections 6–12 and 14 preserve the earlier configuration-phase sequence and evidence. Their no-financial-migration/no-execution restrictions do not exclude the explicitly required financial and emulator work below.

> **Current catalog and pipeline baseline:** ConfigurationDb owns active strategy catalog authoring. Reference Data Manager edits all seven catalog sections, including balanced/directional variants; Portfolio mandates, assignments and policy limits use exact deployment GUID/version references. Existing family records are imported as Drafts without automatic permissions. The old family UI/write path is Legacy; historical contracts remain readable. [Integration details](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md) and [UI guide](../../TomasAI.IFM.UI.Net/Docs/Strategy-Catalog-Reference-UI.md) supersede the older family-authoring descriptions below. Regime Discovery, Market Condition, Trade Selection and Order Composition have since been implemented. Old PF-13/PF-14 blocked descriptions are historical evidence, not a current claim that those actors are absent. Risk Manager and financial admission require the new integrated qualification.

> **Historical catalog direction (superseded):** Earlier three-family restrictions describe the original PF scope, not the current catalog taxonomy. Preserve old exact IDs, versions and hashes as compatibility contracts; new variants must not expand Fund permission implicitly. See [ConfigurationDb strategy catalog design](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

| Item | Value |
| --- | --- |
| Status | Financial development implementation and qualification In Progress; production security deferred |
| Created | 2026-08-29 |
| Prior v1.0 baseline approved | 2026-08-29 |
| Revised | 2026-09-08 |
| Source | [Portfolio-Fund-Specification-v1.0.md](./Portfolio-Fund-Specification-v1.0.md), revised internally to v1.2 |
| Authoritative design | [Portfolio-Fund-High-Level-Design-v0.1.md](../../Documents/system/Portfolio-Fund-High-Level-Design-v0.1.md) |
| Scope | Historical PF-01 through PF-31; new PF-FIN-01 through PF-FIN-07 |
| New-gate initial state | Not Started |

## 1. Purpose

Once approved, this plan converts the revised Portfolio/Fund specification into an ordered, independently reviewable implementation sequence. A development gate is complete only when its development-scope deliverables, documentation, and all five required test dispositions are recorded: BDD, unit, integration, verification, and UI system tests.

The original configuration phase stopped after Portfolio/Fund configuration and accepted downstream references. The financial phase adds authoritative accounting, reservations, exact Risk authorization and Portfolio execution-fact boundary qualification. Actual broker connectivity and a general execution-facing TradeDb redesign remain separate deliveries.

PF-01 through PF-31 retain their recorded historical evidence and status. PF-FIN-01 through PF-FIN-07 supersede affected financial boundaries and requalify them. A historical gate is not rewritten as though the new requirement existed at the time.

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

## 3. Configuration boundaries and financial supersession

The following record the original configuration boundaries. Sections 15–22 supersede legacy transaction isolation, financial write authority and emulator restrictions only within the specified financial scope; they do not authorize actual broker effects.

- `PortfolioCommandActor` owns Portfolio versions, state, membership, allocations, and delegated Fund risk envelopes.
- `PortfolioFundCommandActor` owns Fund mandates, assignments, planned composition identities, and accepted downstream result references.
- `PortfolioQueryActor` serves typed reads over rebuildable ScyllaDB Portfolio projections.
- PostgreSQL EventSourceDb is authoritative for aggregate history.
- PostgreSQL SequenceIdDb allocates positive integer PortfolioId, FundId, OrderId, and TradeId values.
- PostgreSQL SequenceIdDb also allocates PolicyId and TradeStrategyFamilyId; no operator enters an integer business ID.
- Original PF scope used three ReferenceDb family seeds. Current reusable families/strategies/structures/variants/deployments belong to ConfigurationDb. Historical PF gates do not qualify that later catalog implementation.
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

**Qualification record:** [`Portfolio-Fund-PF29-Qualification-Evidence-v1.0.md`](Portfolio-Fund-PF29-Qualification-Evidence-v1.0.md) maps PF-21 through PF-29 to executable evidence and records the final focused, full-regression, production-host, concurrency, and adjacent-suite results.

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
| Read-only legacy Fund/Trade history and source isolation | PF-31 | PF-31 live NATS reconciliation |
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

## 12. Historical configuration gate-status ledger

These are the recorded results of the earlier deliveries. Later implementations supersede the old Trade Selection/Order Composition blockers; PF-FIN-05 must qualify their current financial handoff. New financial status is tracked independently in section 22.

| Gate | Current status | Completion evidence location |
| --- | --- | --- |
| PF-01 | Complete | 2026-08-29: BDD 2, unit 6, integration 1, verification 1, UI system 1; all passed |
| PF-02 | Complete | 2026-08-30: typed NATS allocation covers Portfolio/Fund/Policy/Order/Trade; the production API actor host allocated every identity as a positive integer and editors expose allocated values without hand entry or fallback |
| PF-03 | Complete | 2026-08-30: production Portfolio actor create/state route, PostgreSQL authority, Scylla projection, full host restart, durable idempotent create replay/conflict, lifecycle tests, and no-execution evidence pass |
| PF-04 | Complete | 2026-08-30: production PortfolioFund actor creates and activates an owned mandate through real NATS/PostgreSQL, resolves it from Scylla, and reloads identical active state after full API-host restart; BDD/unit/catalog/UI contracts pass |
| PF-05 | Complete | 2026-08-30: versioned template/hint/composition-profile assignment is committed through the production actor, projected, frozen into a snapshot, and retained after host restart; Daily/Weekly/Monthly catalog tests pass |
| PF-06 | Complete | 2026-08-30: membership-bound allocation and FundRiskEnvelope are committed through real NATS, projected, frozen into strategy resolution, and retained after restart; available/constrained/blocked/expired catalog passes |
| PF-07 | Complete | 2026-08-29: native EventSourceDb events, expected-version append, committed-command lookup, separate non-authoritative snapshots, strict unknown-contract rejection; BDD 2, unit 3, real-PostgreSQL integration 2, verification 2, UI system 2; all passed |
| PF-08 | Complete | 2026-08-30: 16-table PortfolioDb schema including policy and `fund_allocation` projections, split typed read/write contexts, bounded partition queries, monotonic EventId Scylla timestamps, no-ALLOW-FILTERING/legacy audit and teardown; real-Scylla integration passed |
| PF-09 | Complete | 2026-08-30: durable descriptors cover every event; failed target mutation cannot report success, retry replays the authoritative event, and a representative Portfolio/Fund/allocation/envelope/assignment/order/trade/workflow catalog rebuilds twice from PostgreSQL into empty Scylla with identical hashes. Duplicate/old delivery, live projection, and full-host restart evidence also pass |
| PF-10 | Complete | 2026-08-30: production command/query actors, stable typed clients, typed identity/aggregate-revision routes, raw real-NATS serialization, live configure/resolve/reserve/compose/Risk/query routes, cancellation/correlation/access metadata, dedicated unauthorized/disabled error mapping, and full-host restart pass. PF-26/PF-29/PF-30 supersede and qualify every revised public route |
| PF-11 | Complete | 2026-08-30: deterministic fail-closed resolver, canonical immutable snapshot/hash, and representative catalog pass; production NATS resolves real Scylla configuration and returns the same version-complete configuration after full host restart |
| PF-12 | Complete | 2026-08-30: concurrent identical production NATS reservations yield one committed OrderId/TradeId set, replay returns the exact IDs, PostgreSQL/Scylla/restart retain them, and conflict plus futures/vertical/Iron-Condor cases pass |
| PF-13 | Blocked | Portfolio-side accepted-selection guards/contracts/idempotency and representative tests are complete. Production continuation cannot be wired until the currently skeletal TradeSelection pipeline actor/result contract is implemented |
| PF-14 | Blocked | Portfolio-side fail-closed transitions, immutable result reference, real NATS acceptance, replay/projection, and restart are complete. Production dispatch/return fencing awaits an implemented OrderComposition actor |
| PF-15 | Blocked | Portfolio-side candidate-hash/envelope/currentness validation, Approved/Rejected recording, real NATS/restart, and no-execution fence pass. An actual RiskManagement actor and runtime side-effect probe do not yet exist |
| PF-16 | Complete | Superseded and requalified by PF-27: the final compact Portfolio/Risk Policy UI passed repository-owned rendered STA message-loop acceptance, dirty-close, unauthorized persona, accessibility, and production-host journeys |
| PF-17 | Complete | Superseded by PF-28: the obsolete Planned Compositions surface was removed and its valid Portfolio/Fund/order visibility requirements were requalified in unified Trade Orders with stale-response fencing and no execution controls |
| PF-18 | Complete | PF-29 full regression plus PF-30 focused and production-host qualification supersede the historical matrix; all applicable Portfolio, Reference, NATS, Trade boundary, UI presentation, and UI system suites passed with no unexplained failure/skip |
| PF-19 | Complete | PF-29 architecture/runtime audits prove read-only `FundLegacyDbContext`, distinct new subjects/stores, no cross-write, no broker/TradeDb/execution dependency, preserved legacy navigation, and no migration/deletion |
| PF-20 | Complete | PF-30 enforces the role map in production actors, captures bounded trace/metric dimensions, reports Portfolio operational health, qualifies NATS load/restart/rebuild, and proves independent query/mutation rollback switches |
| PF-21 | Complete | 2026-08-30: PortfolioCode and Guid/fallback policy identities are absent from production/tests; MessagePack key 1 is reserved, schema v2 uses positive policy identity/version, obsolete Planned Compositions navigation/types are removed, and revised DTOs pass real NATS plus UI contracts |
| PF-22 | Complete | 2026-08-30: sequence-backed exact three-family catalog, stable-key/version Scylla LWT bootstrap, typed Reference NATS list query, startup bootstrap, and read-only Reference UI pass. Eight simultaneous independent bootstrap processes plus restart produce exactly three stable unique rows with sequence IDs; the focused process integration test passes 1/1 |
| PF-23 | Complete | 2026-08-30: positive policy identity allocation, versioned DTOs, MessagePack contracts, stable validators/reason codes, canonical hashing, zero-blocking semantics, and most-restrictive global/family/envelope caps are implemented and pass BDD/unit/verification/UI plus live allocation evidence |
| PF-24 | Complete | 2026-08-30: event-sourced lifecycle, expected revisions, replay/conflict and reference fences pass. Injected failure after policy append/before Portfolio assignment is healed idempotently on retry, including missed projections; concurrent replacements serialize and exactly one matching expected revision succeeds. Focused integration tests pass 3/3 |
| PF-25 | Complete | 2026-08-30: authoritative policy events/snapshots, Scylla query projections, delete tombstones, projector registration and rebuild pass. A delayed pre-delete event cannot resurrect policy state, and two empty-store full rebuilds produce identical query/catalog hashes. Focused real-storage tests pass 3/3 |
| PF-26 | Complete | 2026-08-30: policy command/query actors and clients, read-only Reference query actor/client, split read/write DI, cancellation-aware APIs, exact assigned policy/family snapshot propagation and effective caps are implemented. Production host tests pass typed Reference query and Policy create/activate/assign through frozen workflow resolution |
| PF-27 | Complete | 2026-08-30: compact command bar and Risk Policy modal are implemented and qualified. The unstable nested-splitter first-paint path was replaced by equivalent bounded table panels. A repository-owned STA WinForms message-loop acceptance drives create/edit/save, rejected dirty close, clean teardown, and all seven unauthorized mutation controls on the rendered form; PF-27 BDD 2, unit 6, production-host NATS integration 1, verification 3, focused UI system 5, and broader Portfolio UI system 18 pass with zero failures/skips. The external desktop helper is not a gate dependency; the live message-loop and production-host qualifications provide the required executable evidence |
| PF-28 | Complete | 2026-08-30: Trade Orders queries Portfolio then Fund and canonical Manual/StrategyWorkflow orders, explicit origin/status filtering, integer IDs and pre-execution fences. Manual Create Order now sends a typed Portfolio/Fund command; the actor validates active current versions, sequence-allocates OrderId, commits/projects an idempotent non-executable Draft and performs no legacy write. Generation/identity fencing rejects delayed scope responses. BDD 1, unit 1, integration 2 (including production-host NATS), verification 1 and UI system 3 pass |
| PF-29 | Complete | 2026-08-30: complete persona/operation authorization matrix, unauthorized rendered UI journey, 64-query/8-worker production NATS load qualification (597.2 ms total, 204.6 ms p95), exact three-family results, and no execution/legacy dependency proofs pass. Focused PF-29: unit 44, BDD 7, live integration 2, verification 10, UI system 6. Full Portfolio: unit 93, BDD 22, isolated real integration 29, verification 28, UI system 17, presentation 4. Adjacent Reference 24, NATS 132, Trade unit/BDD/integrated/verification 451 pass with zero failures/skips. The traceability record maps PF-21 through PF-29 and explains the required live-host versus isolated-responder topology. |
| PF-30 | Complete | 2026-08-30: caller principal/roles are carried by additive typed envelopes and enforced per verb at every Portfolio actor; audit events use the caller principal; bounded activities/metrics are captured without principal/hash/ID metric labels; `/health/ready` reports operational switches; production NATS persona, 128-read load (334.5 ms total, 42.9 ms p95), clean restart, deterministic rebuild/hash, and mutation-disabled rollback journeys pass. Historical Partial gates are reconciled above. See `Portfolio-Fund-PF30-Release-Qualification-v1.0.md` |
| PF-31 | Complete | 2026-08-31: sequence-allocated Draft Portfolio 1101 `Legacy Test Portfolio` has 14 permanent-Draft, read-only mappings (new FundIds 5001-5014) to every defined legacy Fund. A repeated importer run skipped all mappings idempotently. Production Portfolio NATS queries reconciled all 1,217 FundOrders and 1,222 FundOrderTrades: 1,206/1,204 belong to mapped Funds and 11/18 under orphan source FundIds 1003/1016 remain separately queryable quarantine. TradeDb hydration demonstrated `NoTradeDbDefinition`, `DefinitionOnly`, `PositionHistory`, and `FillHistory`. Current PortfolioDb order loading remains a separate default mode; legacy mode disables every mutation. Single selection now embeds the original four-leg Iron Condor trade-order editor in the lower Trade Orders region while keeping the selector open; selection replacement is disposed and generation-fenced. The editor consumes the hydrated TradeDb trade and legacy Fund balance without current market-data dependencies, resolves the exact base contract, disables all editing, and fences command/live-feed entry points. Missing, unsupported, or unresolved cases show only a concise unavailable message. Accepted selections still open one reusable graph-enabled middle-screen `OrderId:TradeId` tab under its separate historical read-only boundary. Qualification covers hydrated historical editor loading, identity validation, fail-closed commands, original-editor routing, exact-contract behavior, selection lifecycle, and the existing graph viewer protections. PF-31 passed its focused unit/UI verification and affected UI builds; the broader presentation suite retains six unrelated `TradeOrderEditorViewModel.LoadCoreAsync` fixture failures at the PortfolioQueries setup boundary. |

Allowed statuses are `Not Started`, `In Progress`, `Partial`, `Blocked`, and `Complete`. `Partial` must identify the missing deliverable or test evidence; it cannot be used as a permanent closure state.

## 13. Explicit deferred register

For the current financial phase, the following remain deferred. This register supersedes the original configuration-only exclusions:

1. Actual IBKR/broker connectivity and production account/margin qualification. The IBKR emulator also requires a future approved design and implementation. PF-FIN-05 currently qualifies Portfolio execution-fact boundaries with labelled fixtures, not a working emulator.
2. General broker execution-platform implementation. Financial ingestion of authenticated execution facts, duplicate/corrected facts and uncertain commitments is required here.
3. General live TradeDb redesign and market-feed position processing. Existing order/trade identities must be retained through the financial handoff.
4. Unrelated Risk Manager enhancements. The sized assessment and functioning Risk/Fund/workflow integration required by PF-FIN-05 are delivery dependencies, not optional stubs.
5. Unrelated Order Composition algorithm changes. Its current exact candidates and contracts must be qualified with financial admission.
6. Uncontrolled legacy migration or physical deletion. PF-FIN-06 requires audited transaction migration and explicit disposition of every legacy producer/table; PF-31 mappings alone do not accomplish that migration.
7. Removal of the legacy Funds UI or manual blotter.
8. Multi-asset/unrestricted template ranking and advanced Portfolio optimization.
9. High-throughput ScyllaDB sequence/tick identity redesign.
10. Expansion of current operator-facing integer IDs beyond checked Int32 contracts.
11. External QuickBooks connector implementation. Internal export identity, mappings and failure-isolation contracts/tests are included.
12. New strategy families beyond the current versioned catalog. Long/short futures, credit/debit verticals and long/short iron condors with balanced/directional bias are required compatibility cases, not deferred variants.
13. Scheduled policy activation and generic expression/rule engines.

## 14. Historical configuration-phase definition of done

The original configuration-phase criteria were as follows. They do not establish v1.2 financial completion; section 22 defines that separately:

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

## 15. Financial phase: authority, scope and dependencies

This phase implements [specification v1.2 sections 37–46](./Portfolio-Fund-Specification-v1.0.md), [design v0.3 sections 27–34](../../Documents/system/Portfolio-Fund-High-Level-Design-v0.1.md), the [Risk Manager design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RiskManager/Docs/RiskManagement-High-Level-Design-v0.1.md) and the [system actor conventions](../../Documents/system/Actor-Implementation-Conventions.md). The specification owns financial fields, wire keys, invariants and reason meanings. This plan owns implementation order, artifacts and acceptance evidence. Resolve discrepancies in both documents before enabling affected routes.

The default shared Function lifecycle projects a result and then appends its completed event as separate operations. The implemented opt-in `AtomicBusinessAndEvent` path now enlists financial state, receipt and completed event in one PostgreSQL transaction for the two financial Functions. Targeted tests establish that boundary, not completion of the whole financial phase. The first four workflow actors are regression baselines; a working fifth-stage Risk Manager handoff must be delivered and qualified in PF-FIN-05, coordinated with its own design. Do not close that gate using a fabricated Approved result. See the [implementation manifests](./Portfolio-Financial-Implementation-Manifests-v1.0.md).

### 15.1 Delivery sequence

```text
PF-FIN-01 Contracts, authority and schema manifests
    -> PF-FIN-02 Shared atomic persistence for financial Commands and Functions
        -> PF-FIN-03 GeneralLedger authority and posting
            -> PF-FIN-04 Capacity reservation and lifecycle
                -> PF-FIN-05 Risk/Fund/workflow and emulator handoff
                    -> PF-FIN-06 Reconciled migration and Portfolio financial UI
                        -> PF-FIN-07 Release qualification and operational evidence
```

Migration inventory and UI contract design start in PF-FIN-01. Schema/test-fixture preparation can proceed while the shared lifecycle is developed. No financial mutation route becomes usable before PF-FIN-02 and its own domain gate pass. Legacy cutover follows financial integration qualification, not simply schema creation. Earlier migration dry runs use isolated destinations. Each gate must be independently reviewable; shared work does not substitute for its exit evidence.

### 15.2 Production ownership and placement

| Area | Required implementation |
| --- | --- |
| `Domain.Portfolio/GeneralLedger` | `GeneralLedgerCommandActor` for single/batch posting, mapped Command extensions/validation, Models, configuration Command surface, `GeneralLedgerQueryActor`, projection/reconciliation ownership |
| `Domain.Portfolio/CapacityReservation` | `CapacityReservationFunctionActor` for admission; `CapacityConsumptionFunctionActor` for pre-submission consumption; `CapacityReservationCommandActor` for subsequent lifecycle changes; typed contexts, mapped extensions, Models and `CapacityReservationQueryActor` |
| `Domain.Portfolio.Shared` | Owned commands/events, concrete request/receipt/query DTOs, reasons and append-only wire manifests |
| `Domain.Strategy.Contracts.Shared` | Neutral Portfolio authorization/evidence DTOs needed across domains; no Portfolio-to-Trade implementation dependency cycle |
| `Shared/EventModelActor` and shared event persistence | Opt-in transactional Function completion plus financial Command event/business enlistment using the same request-scoped transaction boundary; existing calculation Functions and unrelated Commands retain their behavior |
| `Application.Storage` | `IPortfolioFinancialDbContext`, `IGeneralLedgerStore`, `ICapacityReservationStore`; PostgreSQL migrations and enlisted EventSourceDb writes; Scylla Portfolio projections |
| Application/API composition root | Typed actor/API registrations, context aliases, schema/readiness verification, permissions and financial operation switches |
| Trade workflow and Fund composition | Exact Risk authorization/receipt acceptance, durable execution intent, consumption and emulator fact handoff |
| UI services/ViewModels/Views | Typed financial APIs, selected-Fund financial views, pending-operation recovery and readonly generated identities |

`ILedgerPostingApi` dispatches single/batch Commands; `ICapacityReservationApi` and new `ICapacityConsumptionApi` invoke the two Functions; `ICapacityLifecycleApi` dispatches lifecycle Commands. `IGeneralLedgerQueryApi` and `ICapacityReservationQueryApi` serve reads. Complete their method/subject/authorization matrix in PF-FIN-01. Financial writes belong in schema `portfolio_financial` in the PostgreSQL database configured by `EventSourceActorDbContext.EventSourceActorDbConnection`, alongside Command/Function events. `PortfolioDbContext` remains a Scylla projection. ConfigurationDb strategy metadata and legacy Fund balances cannot become financial authority fallbacks.

The Function boundary is the workflow dependency: reservation must commit before accepting capacity, and consumption must commit before submission. Ledger posting is an ongoing Command responsibility, as are working/fill/submission-unknown/cancel/release/expiry transitions. Command acceptance or queue delivery is not financial completion: callers that need a committed result await the correlated post-commit Command outcome or query its receipt. The lifecycle Command route must reject `Consume`; only `ConsumeCapacityReservationCommand` through `CapacityConsumptionFunctionActor` can authorize submission. A consumed replay is evidence of the original transition, not permission to submit a duplicate order.

## 16. Non-negotiable financial guardrails

| Guard | Implementation and acceptance requirement |
| --- | --- |
| FIN-G01 Atomic success | Business mutations, typed receipt and authoritative event use the **same PostgreSQL connection and transaction**. Functions append their completed event; Commands append their committed domain outcome at the expected aggregate version. Independent transactions, eventual projection or sequential saves are insufficient. Financial success follows confirmed commit, not queue/command acceptance. |
| FIN-G02 Thin actors | The two Functions use frozen `_parseMap`, `_validationMap`, `_receiveMap`, `_executionPolicyMap`, `_eventMap`, typed context/generic singleton aliases and mapped Execute/Complete/Fail/policy extensions. Commands follow standard mapped Command parsing, `ValidateMappedCommand`, ordered `List<ValidationError>` extensions, receive/event handling and durable publication; they do not inherit completed-only Function semantics. Models own calculations and shared persistence owns transactions. No `Typed()`, actor-specific timer helper or inline domain/SQL override. |
| FIN-G03 Replay and uncertainty | Stable Portfolio/OperationId and canonical input hash identify the attempt. Exact retry returns the original completion. Changed inputs conflict. Unknown COMMIT stays `OutcomeUnknown` until authoritative reconciliation; never issue another identity, release a hold or infer rollback from timeout. Post-commit reply/observer failure cannot relabel the committed result. |
| FIN-G04 Source uniqueness | Enforce source identity separately from command identity. New operation for an already posted source returns `GL.SOURCE.ALREADY_POSTED`, NoNewMutation and original operation reference; changed content conflicts. Batch duplicates cannot cause partial posting or a falsely correlated completion. |
| FIN-G05 Shared admission fence | Lock/recheck one Portfolio authority across all Funds/server instances, then affected scopes in deterministic order. Ledger spending, holds, period/configuration changes and authority revocation participate. Actor serialization and Scylla reads alone cannot enforce admission. |
| FIN-G06 Accounting truth | Balanced USD journals, explicit decimal units/rounding, immutable corrections and authenticated movement evidence. Encumbrances are not cash expenses; valuation is not a deposit; order notional is not automatically settlement cash. Genuine loss/fee/fill facts still post if cash becomes negative, while new discretionary spending blocks. |
| FIN-G07 Exact authorization | Verify current Fund membership/mandate, policy/envelope, exact deployment/strategy/structure/variant/parameter versions, sized order and evidence/environment. Recompute requirements from pinned inputs; do not trust a supplied Approved flag or capacity vector. Missing/stale authority fails closed. |
| FIN-G08 Lifecycle conservation | Consume before external/emulator submission. Filled + cancelled + remaining units equal original units; obligations move from reserved to working/position usage without omission or double/triple counting. Unknown submission/cancel retains obligations. Consumed commitments cannot be freed by blind expiry. |
| FIN-G09 Safe concurrency | Materialize external evidence before locks; recheck fenced revisions/currentness inside. No external HTTP, cross-actor/NATS wait or Scylla I/O inside financial locks. Await cancellation-aware provider calls; use request-local transactions and bounded retries, no `.Result`, `.Wait()` or process-wide financial mutex. |
| FIN-G10 Bounded work | Enforce 1 MiB request, 512 KiB result, 256 lines/journal and 100 journals/batch; byte limits also apply to the whole batch. Loading/replay and new execution budgets are 1 s/2 s, subject to stricter caller expiry. At most three confirmed-rollback transaction retries within the original deadline; unknown commit is reconciliation. |
| FIN-G11 Compatibility | Append-only keys/routes/reasons; preserve historical hashes/IDs. Standard actor MessagePack transport/persistence serialization; explicit canonical domain hashes, no ad hoc MessagePack cloning, nested byte payloads or serialization-dependent normalization. Size checks use the shared serialization boundary. |
| FIN-G12 Read separation | PostgreSQL receipt/current-capacity queries establish financial truth. Scylla history may lag and shows revision/freshness; projection outage/rebuild cannot release money, allocate IDs or repost transactions. UI uses typed APIs. |
| FIN-G13 Controlled cutover | Every legacy producer/writer/table has a disposition. One writer per migrated scope, explicit source cut and reconciliation; no live dual-write, silent currency/sign inference, source deletion or importing both opening capital and the same historical capital movements. |
| FIN-G14 External isolation | Actual IBKR and QuickBooks are later deliveries. Labelled execution-fact fixtures and internal export contracts are required now; a functioning emulator is deferred until its design and implementation. Remote failures/edits cannot alter local authority. No provider secret or full financial payload in unrestricted logs. |

## 17. Financial implementation gates

### PF-FIN-01 — Freeze contracts, authority and storage manifests

**Depends on:** approved design/specification and current code/registry inventory. **Initial status:** Not Started.

**Deliverables**

1. Record baseline commit, existing Function/Command behavior and targeted regressions. Produce traceability for sections 37–46: exactly two financial Functions, the ledger and lifecycle Command actors, configuration/query routes, APIs, principals, terminals and contexts. Pin separate Function execution identities and continuing Command aggregate identities/revisions; operation/source deduplication applies across both. Inventory current Risk/Fund/workflow boundaries and missing PF-FIN-05 production steps.
2. Freeze explicit keys for all top-level/nested DTOs in section 38, enum values, subjects, numeric error IDs, canonical hashes and limits. Include `GL.SOURCE.ALREADY_POSTED` and section 46 reasons. Check the complete registry before append-only allocation; no placeholders survive route enablement. Add neutral shared contracts without circular dependencies.
3. Freeze sequences/identities: existing Portfolio/Fund/Order/Trade Int32 IDs remain; `PortfolioLedger_BookId`, `PortfolioLedger_AccountId`, `PortfolioLedger_JournalId`, `PortfolioLedger_TransactionId` use specified widths; stable reservation/operation GUIDs. Operators never enter business codes.
4. Produce a versioned posting-rule matrix for **every** legacy producer/type/sign: source identity, Portfolio/Fund mapping, dates, accounts, cash/P&L/valuation effects, evidence, corrections and import disposition. Unknown mappings quarantine affected sources; no default deposit/USD assumption.
5. Freeze authority semantics covering policy/envelope activation/revocation, allocation, membership/mandate and admission-relevant configuration. Identify every current conventional Command writer and its participation in the financial fence without relying on delayed projections. Prove revocation wins against stale authority. Unsupported cross-Portfolio shared-account pooling fails explicitly.
6. Publish PostgreSQL migration/constraint and Scylla query/CQL manifests from section 18. Pin scopes, precision, locks/CAS, uniqueness, balance enforcement, replay lookups and pagination. Co-location/enlistment and migration version are readiness requirements.
7. Pin engineering defaults, personas, emulator margin/evidence fixtures and catalog/timeframe matrix. Freeze internal export source inclusion, identity, corrections and mappings; no live connector required.

**Tests and exit evidence**

- **BDD:** authorized transaction kinds, stale authority, unsupported currency and duplicate-source decisions.
- **Unit:** golden key/enum/hash manifests, sequence overflow, culture-independent decimals, payload bounds, route/error uniqueness, two-Function five-map/context assertions, mapped Command conventions and exhaustive legacy-kind classification. Reject Consume on the lifecycle Command route and all actor-type mismatches.
- **Integration:** real NATS DTO/authorization routes; isolated PostgreSQL migration/reapply/constraints; real Scylla schema/query smoke tests. Mutation routes remain disabled pending their implementation gates.
- **Verification:** independent posting examples; every producer/table maps to a rule or named quarantine reason. Automatically detect unclassified kinds rather than asserting coverage from a selected subset.
- **UI system:** generated/readonly identity and typed service-boundary checks; no action bypasses readiness.

Exit requires checked-in manifests, no unallocated public IDs, complete authority-writer and migration inventories. Atomic completion remains PF-FIN-02 work. Covers FIN-T02/13/14/15/18.

### PF-FIN-02 — Shared atomic financial Command and Function persistence

**Depends on:** PF-FIN-01. **Initial status:** Not Started.

**Deliverables**

1. Extend shared typed execution policy/completion for the two capacity Functions. Add shared transactional Command persistence/enlistment for ledger and lifecycle writes; use ordinary mapped Command handling, continuing aggregate history and post-commit durable event delivery. Do not turn these Commands into Function wrappers. Preserve existing calculation Functions and unrelated Commands; no per-actor lifecycle fork or timer helper.
2. Enlist financial stores and EventSourceDb in one request-scoped PostgreSQL transaction. Validate/write business state, operation receipt and authoritative event with expected stream/version checks. Functions finalize completed-only state after commit; Commands advance aggregate state after commit and deduplicate operations/sources within continuing history. Reload after uncertain commit; singleton contexts cannot hold current transactions.
3. Distinguish confirmed rollback, confirmed commit, NoNewMutation and OutcomeUnknown. Reconcile original operation/source identity on reconnect against a matching receipt/event pair. Alert on an impossible partial pair instead of manufacturing success. Cancellation/disconnect during COMMIT follows the same uncertainty rule.
4. Preserve Function Complete/Fail mapping and exact replay. For Commands, persist the domain outcome/receipt atomically and deliver correlated outcomes through the normal durable post-commit publication path; a delivery failure cannot roll back financial truth. Pin subscriber recovery/checkpoint handling so a crash after commit cannot lose notification permanently. Queries resolve original receipts while delivery/projection lags. No financial mutation is performed again by a projector.
5. Add test-controlled failpoints around business mutation, receipt, event append, commit, finalization, projection and reply. Do not expose unauthenticated production mutation switches.

**Tests and exit evidence**

- **BDD:** success returns one durable receipt; pre-commit failure changes nothing; lost acknowledgement resolves original attempt without duplicate money.
- **Unit:** policy dispatch, stage/disposition mappings, deadline/cancellation, context instance equality, single finalization, unchanged default behavior and bounded retry classification.
- **Integration:** exercise both financial actor types with the production provider and real PostgreSQL; rollback at every write, connection loss during COMMIT and response loss afterward. Restart/query original receipt/event. Race identical/conflicting requests across **two independent hosts/connections**. Prove same-transaction enlistment. For Commands, commit multiple operations to the same aggregate, replay an old operation after later writes without rewinding state, and recover notification after commit-before-publication failure.
- **Verification:** each fault leaves no business mutation/receipt/event or one matching committed set. No partial acceptance/double application. Check continuing Command stream versions and Function completed-only replay independently; run existing pipeline Function and conventional Command regressions.
- **UI system:** service timeout/recovery retains original OperationId and cannot display rollback after confirmed commit.

Exit requires atomic rollback/restart/unknown-commit evidence for both actor types, durable Command outcome delivery and unchanged existing Function/Command tests. Covers FIN-T05/06/12/13/14. No downstream financial route bypasses this gate.

### PF-FIN-03 — GeneralLedger posting and authoritative balances

**Depends on:** PF-FIN-01/02. **Initial status:** Not Started.

**Deliverables**

1. Implement `GeneralLedgerCommandActor` with separate mapped handlers for `PostFundTransactionCommand` and `PostFundTransactionsCommand`, standard Command validation/event handling and Models for rules, balances, encumbrances and receipts. Both use the shared atomic persistence boundary. Batch has one operation/financial revision/committed outcome and ordered manifest; any invalid/duplicate item rejects the entire new batch. Persist continuing aggregate history, not one permanently completed Function per ledger.
2. Implement versioned books/accounts/rules and period Commands with financial-fence participation. Posted transactions/journals/entries are immutable. Enforce journal balance at the database boundary; raw adjustment/import lines are privileged.
3. Implement confirmed deposit, withdrawal request/settle/cancel, intra-Portfolio Fund transfer, settlement, commission, realized P&L, valuation/EOD, reversal, adjustment and opening balance. Request/cancel persists a business transaction and obligation without inventing a cash journal; nullable JournalId/JournalHash and required ObligationId follow the specification.
4. Separate cash, pending movement, encumbrance, valuation and trading capacity. Record authenticated losses/fees even when limits/cash are breached and atomically block new admission. Enforce reversal remainder, periods/reopen/late adjustment and source watermarks. Prevent realizing already recognized unrealized P&L twice.
5. Implement source deduplication, receipts, coherent-cut trial balance, reconciliation, typed queries and durable revision-fenced Scylla projections. Rebuild cannot post money. Capture export eligibility with commit or derive it from a durable committed checkpoint.

**Tests and exit evidence**

- **BDD:** balanced/unbalanced posting, request versus settled movement, insufficient withdrawal, negative-cash actual loss, period correction, source replay, atomic batch/transfer.
- **Unit:** independently specified journal lines for every rule, decimal/rounding bounds, encumbrance transitions, net-zero Portfolio transfer, reversal remainder, commission deltas, EOD replay and valuation clearing, batch ordering/limits.
- **Integration:** real NATS/PG/Scylla posting; concurrent withdrawals/transfers/reversals/period closure, crash/restart, new-operation duplicate source, reordered execution facts, projection outage/rebuild; mid-batch fault rolls back every item.
- **Verification:** independently reconstruct balances/trial balance at a pinned revision. USD debit 100/credit 100 commits; credit 99.99 has no mutation. Transfers/negative-cash settlement reconcile; source gaps remain visible.
- **UI system:** typed query binding, readonly history/correction links, period errors and committed-but-history-pending contract.

Exit requires cash, balances, obligations, corrections and reconciliation, not transaction insertion alone. Covers FIN-T01/02/05/06/07/08/09/12/18.

### PF-FIN-04 — Capacity reservation and lifecycle

**Depends on:** PF-FIN-03 and the common authority fence. **Initial status:** Not Started.

**Deliverables**

1. Implement `CapacityReservationFunctionActor` for Reserve and `CapacityConsumptionFunctionActor` for Consume with five maps, typed contexts, extensions/Models and transactional completion. Implement `CapacityReservationCommandActor` for the remaining lifecycle transitions using standard Command conventions and shared atomic persistence. Persist exact requirements/evidence/hashes, receipt and usage; reject Consume on the Command route.
2. Independently derive/check settlement cash, margin funding, fee/variation reserves, loss charge, gross notional/contracts, position slots and exposure units from pinned inputs. Separate funding from risk measures; avoid duplicate charges. Aggregate shared underlying/Portfolio usage across Funds and timeframes.
3. Implement Reserved, Consumed, Working, PartiallyFilled, SubmissionUnknown, CancelPending, Filled, Released and Expired with only specified ChangeKind transitions. Fence reservation version, execution/source sequence and quantities. Command handlers reload authoritative reservation/usage under the common fence, including changes made by either Function; their cached aggregate history cannot override PostgreSQL state. Use explicit remaining-capacity formulas, not blanket release on any terminal notification.
4. Recheck permissions/evidence/expiry on admission and consumption. Historical receipt replay cannot grant execution after release/expiry. Release only unconsumed or authoritatively reconciled obligations. Scheduled expiry uses typed lifecycle commands, never direct table deletion.
5. Implement authoritative reservation queries/history projections and visible uncertain commitments. Timeout does not mean no order exists.

**Tests and exit evidence**

- **BDD:** cash 1000, competing withdrawal 700/hold 700 admits at most one; competing Funds respect Portfolio caps; revoked/expired consumption rejects; partial-fill/cancel retains filled obligation.
- **Unit:** requirements/units/rounding, zero limits, exhaustive transitions and correct Function/Command dispatch, quantity conservation, source ordering, stale evidence and replay versus current authority. Consumption requires the dedicated Function; Command routes cannot bypass submission authorization.
- **Integration:** multi-host admission, ledger-versus-hold fence, policy/allocation/mandate races, bounded deadlock/serialization retries, replay/unknown commit and consume/fill/cancel/expiry races on real NATS/PG. Keep a lifecycle Command actor loaded across a consumption Function commit and prove it rechecks the new authoritative state instead of acting on stale cached status.
- **Verification:** independently reconstruct scope usage; no negative/missing/triple-counted usage. Same underlying across three timeframes shares capacity. Lost consume response before submission retains the obligation until reconciled.
- **UI system:** current version/uncertain state and disabled unauthorized release; history lag cannot imply available cash.

Exit requires real cross-Fund/cross-host admission and lifecycle conservation. Covers FIN-T03/04/05/06/10/11/12.

### PF-FIN-05 — Risk, Fund and workflow authorization with Portfolio execution-fact boundaries

**Depends on:** PF-FIN-04, current four upstream Functions and Risk Manager design. **Initial status:** Not Started.

**Deliverables**

1. Deliver the real fifth-stage Risk Manager calculation/typed result and workflow dispatch/acceptance, following current Function conventions. Coordinate its implementation gates with this gate; a mocked assessment is valid for unit tests, not gate closure. Risk determines final units/requirements; Portfolio owns reservation.
2. Carry one triggering Daily, Weekly or Monthly timeframe and exact upstream/configuration versions: Regime Discovery, Market Condition, Trade Selection, Order Composition, deployment/strategy/structure/variant/parameters, Fund authority and financial evidence. Preserve Portfolio/Fund/Order/Trade IDs. Do not fabricate results for the other two timeframes.
3. Add `FundRiskAuthorizationReference` and the versioned Fund command. Distinguish composition-result, unit-candidate, sized-order and Risk-assessment hashes. Preserve the old `RiskManagementResultReference.CandidateSha256` meaning and its existing comparison to CompositionResultHash; do not silently reinterpret it. New execution acceptance requires the exact typed receipt and units.
4. Implement durable sequence: composed candidate -> Risk assessment -> reservation Function -> Fund accepted outcome -> workflow accepted intent. Qualify consumption at the Portfolio boundary with explicit accepted-execution fixtures. Actual submission requires the future execution/emulator design; local admission scaffolding is not broker acceptance. Await both committed Function results. Lifecycle and ledger updates thereafter use Commands with correlated post-commit outcomes/receipt queries. Fence workflow/execution revisions and intent; recovery resumes missing steps using stable IDs, never a new submission identity after ambiguous acceptance.
5. Define and test the Portfolio-side contracts and consumers for future execution acknowledgements, fills, cancellation/rejection, fees, settlement and reconciliation using explicitly labelled source fixtures. Do not implement a broker emulator in this gate or claim these fixtures are emulator qualification. Production source authentication follows the deferred security phase. Define ordering/recovery across actors: retain conservative obligations until reconciled, with no unsafe release between fill and accounting. Qualify duplicate, reordered and corrected facts.
6. Exercise long/short futures, credit/debit verticals and long/short iron condors with balanced/directional bias against exact authorized catalog versions. Respect upstream European-style option/pricing qualification. An unauthorized variant or stale deployment cannot pass merely because its family matches.

**Tests and exit evidence**

- **BDD:** exact approved/reserved order can consume; rejected, unsized, stale, expired, mismatched, unreserved or wrong-environment order cannot submit. Actual loss posts while further admission blocks.
- **Unit:** hash-field separation, legacy compatibility, deterministic sizing/requirements, units, acceptance guards and workflow recovery transitions.
- **Integration:** five application pipeline actors plus Fund/financial APIs on real NATS/PG/Scylla with a labelled execution-boundary fixture. Full emulator integration is a future joint qualification. Restart/fault after every handoff, including consume-before-submit and submit-before-ack; stable execution/source IDs throughout. No actual broker connection.
- **Verification:** representative catalog/timeframe matrix with independently expected units/journals/usage; at least one full five-stage path per triggering timeframe, all required variants and negative authority cases. Final cash, working/filled obligations, reservations and journals reconcile after settlement/cancel.
- **UI system:** accepted/rejected/pending financial status, stale Portfolio/Fund response fencing; old opaque Approved reference cannot enable execution.

Current exit requires a functioning fifth-stage financial handoff and real Portfolio ledger/capacity processing of labelled execution-fact fixtures, not just DTO acceptance. Emulator order behaviour and end-to-end reconciliation require its future design, implementation and separate integration qualification. Covers FIN-T04/09/10/11/12/14/17. Actual IBKR qualification remains outside this evidence.

### PF-FIN-06 — Legacy migration, one-writer cutover and Portfolio financial UI

**Depends on:** PF-FIN-05; inventory/dry runs start earlier. **Initial status:** Not Started.

**Deliverables**

1. Implement complete producer/table disposition and migration in section 20. Move active Fund transaction responsibilities under GeneralLedger, reusing qualified legacy behaviors/tests. Do not copy old balance arithmetic as financial authority without rule qualification.
2. Produce deterministic manifests for counts, source IDs/hashes, scope mappings, dates, signs/currencies, accounts, opening balances and exceptions. Preserve source IDs; PF-31 permanent-Draft historical mappings remain non-trading unless separately authorized. Quarantine orphans/ambiguous facts visibly.
   Opening capital is development-only: require a Development API host, an Emulator book in its unqualified import phase, and `DevelopmentOpeningCapital` provenance. Do not treat the legacy `OpeningTrade` balance snapshot as funding. No production funding or automatic amount is required to qualify development behavior; production opening capital is disabled.
3. Reconcile, fence old writers/producers, drain their pending durable events to a recorded source cut, import once, verify totals and switch qualified scopes. No dual-write or legacy balance fallback. Retain readonly source history; no deletion.
4. Add selected-Fund Transactions, Balances, Reservations, journal/correction and reconciliation views/actions in Portfolio Administration. Preserve equal three sections, bottom metrics and Dark Trading Theme. Use generated readonly IDs, permission-controlled selectors and typed APIs. Metrics identify method/freshness and are not spendable-cash authority.
5. Implement pending-operation services/ViewModels: await the correlated ledger/lifecycle Command outcome or reconcile its receipt. Command accepted/queued is not Committed. Prevent duplicate submission, retain OperationId across navigation/restart, distinguish Committed/History updating from OutcomeUnknown, and generation-fence Fund changes. Do not automatically give an uncertain operation a new identity or expose unconditional release.
6. Make scoped legacy transaction paths readonly after cutover; redirect all active transaction/EOD producers and UI commands. Recovery reconciles new writes before rollback; toggling a feature must not restore an old writer against stale balances.

**Tests and exit evidence**

- **BDD:** idempotent import, quarantined ambiguity, opening balance without duplicate capital, one active writer and pending UI retry with original identity.
- **Unit:** all category/sign/date mappings, manifest hashing, source deduplication, cutover states and ViewModel operation/selection generations.
- **Integration:** isolated real source/destination, interrupted/resumed/repeated import, producer drain, writer fence, replay across cut, projection rebuild and unknown commit across UI/API restart.
- **Verification:** full qualified source/destination counts and totals at pinned watermarks, quarantines and both allowed import modes. Recovery after new posting and consumed reservation; routing disable alone cannot pass rollback verification.
- **UI system:** rendered typed-service journeys for deposit, withdrawal request/confirmation, correction/reversal/period errors, journal drilldown, readonly legacy, pending/unknown recovery, stale Fund, small-window layout and theme.

Exit requires every producer/table classified, reconciled scoped cutover and usable financial UI. PF-31 order-history counts are not financial migration evidence. Covers FIN-T02/06/07/12/15/16.

### PF-FIN-07 — Qualification, operations and release evidence

**Depends on:** PF-FIN-01 through PF-FIN-06. **Initial status:** Not Started.

**Deliverables and acceptance**

1. Run section 19 cases and relevant Portfolio, shared Function, storage, NATS, Trade and UI regressions. Report exact passed/failed/skipped counts, commit/configuration/environment and reproducible commands. Mandatory cases cannot be replaced by scans, mocked integration, unrelated historical counts or unobserved manual claims.
2. Prove startup/readiness, schema rejection, financial store outage, multi-host contention, full restart, unknown-commit recovery, projection lag/outage/rebuild, replay after cutover and evidence expiry. Preserve admission fences while authenticated financial facts/reconciliation recover overdrawn or uncertain accounts.
3. Measure representative/maximal batches/lines, concurrent same-Portfolio admission and independent Portfolios. Record workload, count, elapsed time, p50/p95/p99, throughput, allocations/memory and retry/lock-wait/timeout counts. Establish a hardware-specific release budget before load qualification; do not invent latency claims or weaken money invariants for throughput.
4. Deliver section 21 runbooks, schema/contract manifests, migration reconciliation, test artifacts and emulator-versus-production limitations. Internal export/outage fixtures pass; no live QuickBooks claim.
5. Re-run guardrail assertions and independently reconstruct committed totals/usage. Update design/specification/conventions to distinguish implemented behavior from proposals. Only then mark gates Complete with linked evidence.

All five layers remain mandatory: BDD acceptance, pure unit rules, real infrastructure integration, independent numerical/transaction verification and rendered UI journeys. Exit is section 22; environmental/test failures remain incomplete work, not silent waivers.

## 18. Storage and compatibility checklist

PF-FIN-01 freezes exact DDL/CQL/type manifests; PF-FIN-02/03/04 implement them. These are required tables, not a claim they currently exist:

| PostgreSQL table in `portfolio_financial` | Required guard |
| --- | --- |
| `ledger_book`, `ledger_account`, `ledger_posting_rule` | Versioned USD authority, ownership/activation and generated IDs |
| `ledger_transaction` | Immutable business/source record including journal-free encumbrance changes |
| `ledger_journal`, `ledger_entry` | Unique transaction-to-journal reference, balanced complete journal, legal accounts/periods and immutable correction chain |
| `ledger_account_balance` | One balance per normalized book/Fund/account/currency scope; nullable Fund cannot bypass uniqueness |
| `ledger_posting_receipt` | Unique Portfolio/OperationId, canonical input hash and typed single/batch manifest |
| `financial_source_receipt` | Unique book/source-system/source-event-key/posting-purpose; changed content conflicts |
| `financial_encumbrance` | Identified pending obligation, authenticated source and controlled lifecycle |
| `financial_authority` | One Portfolio revision/fence covers admission-relevant writers |
| `capacity_reservation`, `capacity_usage`, `capacity_lifecycle` | Accepted-order uniqueness, scoped/typed measures, versions, unique lifecycle source and quantity conservation |
| `ledger_period`, `ledger_reconciliation` | Non-overlapping periods, controlled reopen and pinned watermarks |
| `ledger_migration`, `accounting_export` | Deterministic import/export identity, source inclusion, corrections and restartable reconciliation |
| Existing Command/Function event storage | Command domain outcome at expected aggregate revision, or Function completed event, in the enlisted transaction; no second independent save after commit |

The database rejects an unbalanced posted journal even through an alternate authorized write path: use a deferred constraint/trigger or exclusively controlled posting procedure as specified. Restrict direct writes accordingly. Test legitimate complete posting and attempted partial/invalid writes.

Scylla models include `ledger_journal_by_id`, monthly `fund_transaction_history`, `fund_balance_snapshot`, `fund_reservation_history` and explicitly enumerated filter indexes. Maximum page size 100, initial window 366 days; stable scoped cursors with revision/freshness. No `ALLOW FILTERING`, unbounded load-all or balance authority computed from history paging. Trial balance uses a coherent cut. Uncertain receipt and current capacity queries use PostgreSQL through actors.

Schema deployment is additive/versioned and idempotent. Verify co-location, enlistment, indexes/constraints and compatibility before writes; failure cannot fall back to legacy FundDb or ConfigurationDb. Preserve old readers/hashes; never renumber historical keys or overload a field with a different financial identity.

## 19. Test traceability and execution evidence

The scenarios below are requirements, not implemented test claims. Add filterable `PortfolioFinancial` coverage to existing projects and map each case to concrete names/source files in release evidence. Retain gate traits using the repository framework; zero discovered tests is a qualification failure.

| Spec case | BDD / unit obligation | Real integration and independent verification | Gate |
| --- | --- | --- | --- |
| FIN-T01 | Balanced journal, rounding/imbalance rejection | 100/100 commits; 100/99.99 leaves no effect; DB bypass rejected | 03 |
| FIN-T02 | Every legacy category/sign, fees, EOD and realization | Independent journals/balances, duplicate cut and complete producer coverage | 01,03,06 |
| FIN-T03 | Cash/hold admission | Two hosts race withdrawal 700/hold 700 against 1000; at most one succeeds | 04 |
| FIN-T04 | Restrictive scope/authority/current version | Competing Funds and policy/mandate revocation under shared fence | 04,05 |
| FIN-T05 | Commit disposition/deadline/finalization for both actor types | Every write/commit/reply/publication failpoint; continuing Command state versus completed Function state; reconnect/restart | 02,03,04 |
| FIN-T06 | Replay, changed hash/source conflict, batch duplicate | New operation returns original source reference without mutation; concurrent replay | 02,03,04,06 |
| FIN-T07 | Period, reversal remainder, late correction/source gap | Concurrent reversal/close, immutable history and bounded correction | 03,06 |
| FIN-T08 | Transfer ownership/net-zero capital | Atomic two-sided posting/failure; foreign Fund/currency rejected | 03 |
| FIN-T09 | Financial facts versus discretionary admission | Negative-cash loss posts once and blocks new holds/withdrawals | 03,05 |
| FIN-T10 | Complete transition/quantity/usage conservation | Fill/cancel/expiry/unknown race, independent usage reconstruction | 04,05 |
| FIN-T11 | Historic replay cannot renew authority | Released/expired receipt cannot execute; consumed timeout retains obligation | 04,05 |
| FIN-T12 | Transport/query/projection boundaries | Production NATS/PG/Scylla, authorization, full restart/outage/rebuild | 02–07 |
| FIN-T13 | Exactly two capacity Functions with five maps; standard ledger/lifecycle Command maps and list validation | Correct actor-type routes, Consume bypass rejection, typed contexts, existing Command and four calculation Function regressions | 01,02 |
| FIN-T14 | Golden keys/hashes, cultures/bounds/overflow, old readers | Raw typed NATS; new versus legacy Risk authorization | 01,02,05 |
| FIN-T15 | Mappings, manifests, quarantine/import mode | Full cut reconciliation, interrupted/repeated import, fenced cutover/recovery | 01,06 |
| FIN-T16 | UI operation/selection/identity/permissions | Rendered commit/history-lag, unknown retry, stale Fund, period and legacy journeys | 03–07 |
| FIN-T17 | Sizing, versions, units and acceptance | Five production stages plus financial actors/emulator; no unreserved dispatch | 05 |
| FIN-T18 | Export mapping/identity/source inclusion/corrections | Durable checkpoint/outage/retry fixtures preserve journals/no duplicate inclusion | 01,03,07 |

Expected financial values must be independent of the implementation under test. Do not call the same Model to compute expected and actual values. Use pinned decimal examples, golden fixtures, database invariants and source-to-journal/usage reconstruction. Include invalid, duplicate, stale, reordered, concurrent and faulted paths, not only successes.

Use existing `TomasAI.IFM.Domain.Portfolio.UnitTests`, `.BDDTests`, `.IntegrationTests`, `.VerificationTests`; `TomasAI.IFM.Shared.UnitTests` for lifecycle rules; `TomasAI.IFM.Application.Storage.IntegrationTests` for providers/schema/enlistment; `TomasAI.IFM.Domain.Trade.UnitTests`, `.BDDTests`, `.IntegratedTests`, `.VerificationTests` for Risk/workflow; and `TomasAI.IFM.UI.Net.Presentation.UnitTests`/`TomasAI.IFM.UI.Net.SystemTests` for presentation/rendered journeys. Do not introduce a competing UI/test stack.

Once the category is implemented, the repeatable command shape is:

```powershell
dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests --filter "Category=PortfolioFinancial" --logger "trx;LogFileName=financial-unit.trx"
dotnet test TomasAI.IFM.Domain.Portfolio.BDDTests --filter "Category=PortfolioFinancial" --logger "trx;LogFileName=financial-bdd.trx"
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests --filter "Category=PortfolioFinancial" --logger "trx;LogFileName=financial-integration.trx"
dotnet test TomasAI.IFM.Domain.Portfolio.VerificationTests --filter "Category=PortfolioFinancial" --logger "trx;LogFileName=financial-verification.trx"
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests --filter "Category=PortfolioFinancial" --logger "trx;LogFileName=financial-presentation.trx"
dotnet test TomasAI.IFM.UI.Net.SystemTests --filter "Category=PortfolioFinancial" --logger "trx;LogFileName=financial-ui.trx"
```

These are planned filters, not reports of existing tests/runs. PF-FIN-01 pins actual framework trait syntax and project paths; final evidence includes shared/storage/Trade regressions too. Restore/build normally; do not reuse stale `--no-build` binaries. Record runtime/framework, schema/server versions, emulator fixtures, source cut, commit, command, counts and artifacts. Unavailable infrastructure is an incomplete run, not a skip counted as pass. Use isolated stores or uniquely owned scopes; never delete shared financial history for cleanup. Bounded polling reports last state/operation; no sleep-and-assume verification.

## 20. Migration inventory and cutover requirements

Every legacy table requires source authority, destination/disposition, reader/writer inventory and reconciliation method. Related `fund.balance` is a reconciliation source, not a blindly trusted balance or new write target.

| Legacy source | Required disposition |
| --- | --- |
| `fund_transaction` | Source/history to ledger transactions/journals or labelled historical-only records according to import mode |
| `fund_transaction_identity_v4` | Preserve/reconcile source identity mappings; no repeated money effect |
| `fund_transaction_timeline_v3` | Reconcile dated history; rebuild bounded projection from committed authority |
| `fund_balance_by_status_day_v3` | Reconcile daily/status/valuation summaries; do not import summaries and contributing postings as separate cash |
| `fund_transaction_amount_v3` | Verify amount/sign/source totals without duplicating postings |
| `fund_transaction_projection_state_v3` | Record projector cut/status and reconcile pending work |
| `fund_transaction_projection_mutation_v3` | Account for pending/replayed projections; retries cannot become new transactions |
| `fund_transaction_write_mutation_v3` | Drain/reconcile pending writes before ownership changes |
| `fund_transaction_write_ownership_v3` | Fence old ownership; establish one writer per migrated scope |

Inventory single/batch commands, EOD, execution/fill/commission/settlement producers, handlers/projectors, APIs, legacy cash UI, reports and balance/metrics consumers. Each is replaced, redirected, readonly historical or explicitly unsupported/quarantined; no unclassified writer. Historical Order/Trade IDs remain unchanged and source lineage is queryable.

Executable sequence: inventory/rule qualification -> isolated dry run -> reconciliation -> select **full posted history** or **opening balance plus labelled pre-cut history** per scope -> fence legacy entry points/background producers -> drain/reconcile at stable source watermark -> idempotent import -> reconcile counts/hashes/cash/journals/obligations -> enable new routes for qualified scopes -> monitor/retain readonly history. Never mix both capital recognition modes for the same money. Partial/failed imports keep new spending disabled, not half-active in both stores.

Manifests include run/version, source/destination environment, scope/source mapping, mode, cut/watermarks, rules/accounts, included/excluded/quarantined counts/reasons, hashes, currency/date totals, journals/opening balances, obligations, reconciliation and activation state. Same-manifest repeat is idempotent; changed source content requires explicit reconciliation.

Before new financial writes, rollback may restore only a proven unchanged old scope after checking ownership. After a posting, reservation, consumption or execution fact, rollback requires reconciled forward recovery or explicitly reconciled reverse migration retaining all obligations. Never toggle back to stale balances. No physical deletion or automatic activation of historical test Funds.

## 21. Operational readiness and recovery

Provide separately scoped controls for new spending/admission, authenticated financial posting, consumption, reconciliation, queries and legacy ownership. Fail closed for new admission if schema, authority or evidence is unavailable. Do not suppress actual settlements because a Fund is overdrawn; unavailable storage requires durable ingestion retry without claiming the fact was posted.

Readiness checks schema/constraints, event/business co-location, enlistment, typed registration, stable registry and permissions. Report Scylla history readiness separately from PostgreSQL financial authority. Route enablement requires its completed gate and qualified scope; startup schema initialization must not automatically migrate capital, activate Funds or switch writers.

Runbooks must cover:

- Unknown COMMIT: retain OperationId, reconnect/query receipt and completion, retry only after confirmed no-commit under stable identity; reconcile inconsistent pairs.
- Unknown submission/cancel: retain conservative capacity, resolve execution/source facts, release only through validated lifecycle.
- Projection outage/rebuild: committed receipt remains authoritative, display lag, replay with revision fences and verify reconstructed totals/hashes.
- Period/reopen, source gaps, late settlement, duplicate/corrected facts, negative cash and revocation: explicit reasons/permissions and immutable history.
- Interrupted migration/source changes/post-cutover recovery: manifest/fence procedure, never legacy fallback.
- Deployment/shutdown: drain or classify in-flight requests; process exit does not prove rollback. Reconcile before accepting conflicting new attempts.
- Export outage: stable export IDs/checkpoints without balance changes; actual connector remains disabled until separately qualified.

Telemetry covers commit disposition, transaction/lock latency, retries, unknown outcomes, oldest unresolved commitments, projection/source lag, reconciliation differences and admission reasons. IDs belong in access-controlled traces, not unbounded metric labels. Redact secrets/full messages. Retain principal, source/evidence, rule/configuration versions and revision in decision audit.

## 22. Financial status ledger and definition of done

| Gate | Status at this revision | Evidence required before completion |
| --- | --- | --- |
| PF-FIN-01 | In Progress | Versioned contracts, numeric keys/reasons and additive schema implemented; complete legacy/authority inventories and five-layer dispositions remain |
| PF-FIN-02 | In Progress | Atomic Function completion, enlisted event/business writes, rollback/race, unknown COMMIT fault injection and targeted recovery pass; full multi-host delivery/restart/failpoint qualification remains |
| PF-FIN-03 | In Progress | Single/batch posting store and Command actor, configured accounting Model, encumbrances and durable history projector implemented; full accounting/configuration/query/reconciliation qualification remains |
| PF-FIN-04 | In Progress | Two mapped Functions, lifecycle Command, closed-position accounting and durable unconsumed-expiry dispatcher implemented; full multi-host and Portfolio lifecycle qualification remains; emulator integration is deferred |
| PF-FIN-05 | In Progress | Mapped Risk Function, exact Fund authorization, persisted Reserve/Fund requests and Authorized checkpoint implemented; bounded committed-snapshot recovery added. Full recovery qualification, Portfolio fact-consumer and five-stage matrix remain; execution/emulator dispatch is deferred |
| PF-FIN-06 | In Progress | Producer/table/type inventory, canonical streaming dry-run archive, financial viewer, posting/reversal, book setup and ledger control UI with persisted recovery implemented; fresh development qualification/fencing and authority review now implemented; existing-source import/cutover and account/rule editing journeys remain |
| PF-FIN-07 | In Progress | Targeted real transaction/actor/UI verification and internal export recovery pass; four real-store load workloads and recovery runbook now verified; full five-stage/multi-host test matrix and final release evidence remain |

Completion requires all seven gates with implemented deliverables and passing evidence. Business balance/usage changes and completed events commit atomically; unknown outcomes reconcile without duplicate money; every writer uses the shared fence; current exact receipts gate execution; source/quantity/accounting invariants survive concurrency/restart; migrated scopes have one writer; and Portfolio UI exposes usable verifiable financial state through APIs.

Record actual test names, counts and artifacts for FIN-T01–18 and FIN-G01–14. Unit success cannot substitute for real transactions/transport or rendered UI verification. No unexplained mandatory failure/skip or migration mismatch in an enabled scope; clearly identify emulator-only and external-connector limitations. Documentation completion, folder creation and old PF counts are never completion of this financial implementation.

### 22.1 Implementation evidence in progress — 2026-09-08

This is an active implementation record, not release sign-off. No PF-FIN gate is closed yet. Changes are uncommitted against baseline `fd066b9b93aaf12a208bf8c0d2f5bfc18ccba26e`.

- `PostgresEventTransaction` uses the existing event-store database/connection settings and expected-version append. Ledger, receipt, financial revision and initial Command projector state commit in that transaction. Business changes roll back when append fails. Only the two financial Functions opt into `AtomicBusinessAndEvent`; other calculation Functions retain their existing completion mode.
- `CapacityReservationFunctionActor` and `CapacityConsumptionFunctionActor` use frozen parse/validation/receive/event/policy maps, typed contexts aliased to their generic singleton registrations, separate Execute/Complete/Fail/policy extensions and transactional repositories. Their state contains the original immutable completion. Current capacity is read separately; replay of a consumed receipt is not fresh submission authority.
- `GeneralLedgerCommandActor` and `CapacityReservationCommandActor` retain continuing aggregate streams. Their database stores load authority under the shared Portfolio row lock. Command projector state is inserted atomically, and post-commit notification failure remains pending delivery rather than a financial failure.
- The additive schema includes immutable journals/entries/transactions, balanced deferred constraints, unique operation/source receipts, configuration version protection and non-overlapping periods. `ledger_journal.created_transaction_id` prevents adding new lines after the original transaction committed. Startup creates schema only; it does not import capital or activate/migrate any Fund.
- Capacity admission reads an `ICapacityAssessmentCompletedEvent` from the committed event log; consumption reads `ICapacityExecutionAcceptedEvent`. These are boundary contracts for the production Risk/workflow producers still required in PF-FIN-05. The PostgreSQL tests use explicitly labelled integration evidence events and do **not** qualify the fifth pipeline actor.
- Gross usage retains absolute contributions and moves held/working/position amounts using differences between quantized absolute contributions, avoiding accumulating fractional-fill rounding residue. Unknown submission/cancel retains obligations.

Executed evidence to this point (SDK 10.0.302, .NET 10.0.10):

| Command/filter | Actual result | Scope |
| --- | --- | --- |
| `dotnet test TomasAI.IFM.Shared.UnitTests --no-restore --filter FullyQualifiedName~FunctionActorLifecycleTests --verbosity quiet` | 31 passed | Shared default/atomic lifecycle, canonical completion/replay and failure handling |
| `dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests --no-restore --filter Category=PortfolioFinancial --verbosity quiet` | 16 passed | Initial pure journal/admission/lifecycle arithmetic; later changes require regression |
| `dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests --no-restore --filter Category=PortfolioFinancial --logger 'trx;LogFileName=financial-integration.trx' --verbosity quiet` | 18 passed, 0 failed/skipped | Real PostgreSQL atomic rollback/race, ledger posting/replay/withdrawal/negative cash, capacity admission/consumption/lifecycle and actual reservation Function maps/reconstruction |
| `dotnet build TomasAI.IFM.Application.Api.Server --no-restore --verbosity quiet` | 0 warnings/errors | New actor/storage/registration code compiles; not a runtime/NATS registration qualification |

The integration artifact is `TomasAI.IFM.Domain.Portfolio.IntegrationTests/TestResults/financial-integration.trx` (local generated output). Tests use uniquely owned scopes in `event-source-test-db`; no production capital, legacy cutover, actual broker submission or external accounting export has occurred. Dedicated schema tests serialize DDL initialization; race cases inside tests still use independent PostgreSQL connections.

### 22.2 Subsequent verified implementation — 2026-09-08

The earlier counts above record an earlier execution. Subsequent executed evidence:

| Test/build | Actual result | Scope and limits |
|---|---|---|
| All Portfolio UnitTests | 167 passed, 0 failed/skipped | Includes legacy classification and additional ledger arithmetic; later timestamp regression still requires rerun |
| Portfolio VerificationTests, Category=PortfolioFinancial | 19 passed, 0 failed/skipped | Six command contracts, six completion contracts, five failure contracts and nested standard MessagePack transport |
| Portfolio IntegrationTests, Category=PortfolioFinancial | 38 passed, 0 failed/skipped | PostgreSQL stores/actors/source fences/configuration, TCP COMMIT-response loss and real Scylla recovery; before subsequent retry tests/hash fixes |
| FinancialRetryIntegrationTests | 5 passed, 0 failed/skipped | Real PostgreSQL error responses verify bounded confirmed-rollback retries; lock timeout/unique conflicts do not retry |
| FinancialRealNatsActorTests, Category=PortfolioFinancialNats | 2 passed, 0 failed/skipped | Actual typed NATS client/production message adapter/Function and Command handlers plus PostgreSQL; isolated NATS 2.12.0 container |
| API Server build | 0 warnings/errors | Includes configuration actor registration and authority validation |
| API `--verify-startup-only` | Passed | Actual composition root; starts no schemas, actors, feeds or HTTP listeners |

NATS qualification uses `IFM_FINANCIAL_TEST_NATS_URL` pointing at an isolated broker, never the application's wildcard subscriptions. Tests directly dispatch received messages to production actor handlers; this qualifies the transport/actor/database boundary, not multi-process actor-pool recovery. The ledger case pre-reserves the real command audit, resumes the uncommitted operation, simulates post-commit notification outage, replays its receipt, and rejects conflicting input. The test found and fixed audit-only duplicate success and UTC timestamp fingerprint instability.

Configuration now has a mapped Command actor and durable projector; versioned account/rule changes, controlled periods, source-validated refresh and journal-derived reconciliation commit with receipts. New books are unqualified and cannot spend. Exact per-deployment authorities share Portfolio/Fund limits. Import, adjustment, reversal and period reopen have separate permissions. Unchanged valuations retain a source fact without zero-value journal entries; reversal can reference retired original account versions. Complete migration, live Risk/emulator handoff, UI journeys and final qualification remain open.

### 22.3 Risk calculation and financial snapshot implementation — 2026-09-08

`RiskManagementFunctionActor` now uses the five frozen maps, typed context alias, separate Execute/Complete/Fail and execution-policy extensions, and a completed-only PostgreSQL repository. Loading permits historical receipt replay; new calculation keeps the original expiry and a 250 ms execution budget. There is no separate Risk projection yet. Its completed event implements `ICapacityAssessmentCompletedEvent`, so capacity admission can read an actual committed Risk result rather than only test evidence contracts.

The pure Models independently reprice the composer's exact frozen contracts, validate European ES option conventions, calculate every payoff breakpoint and outer slope, evaluate the 36-scenario Black-76 grid, normalize Greek units, and enumerate quantities against exact margin quotes and all configured capacity scopes. Futures retain unbounded maximum-loss classification. Whole strategy units preserve leg ratios; one order consumes one position slot. Fees already in composer loss are charged once; a larger quantity-specific fee quote adds only its shortfall. Unknown inputs fail; hard market restrictions or no feasible quantity produce a completed rejection. These calculations are not a reservation or execution authorization.

`GetFinancialAdmissionSnapshot` is a mapped typed query returning available cash, exact deployment authority, relevant Portfolio/Fund/deployment/underlying usage and limits under one PostgreSQL financial revision. Stale committed Portfolio/Fund/policy sources cannot produce a ready snapshot. `FinancialCanonicalHash` resides in the neutral financial contracts assembly so clients, Risk and admission share the same semantic requirement hash.

Partial fills now preserve an entire position slot while any position remains. `capacity_funding_receipt` links an actual posted cash outflow to the exact consumed execution and named unpaid funding component; paid fees/settlement/margin do not reduce available cash twice. Filled loss/position capacity remains held independently of that funding adjustment.

Latest executed checks (uncommitted working tree; earlier counts remain historical):

| Check | Actual result | Qualification boundary |
|---|---|---|
| All Portfolio UnitTests | 168 passed, 0 failed/skipped | Includes semantic timestamp regression after moving the shared hash |
| Portfolio IntegrationTests, Category=PortfolioFinancial | 52 passed, 0 failed/skipped | Includes retries, full accounting cases, partial-fill position slots, fee funding and coherent admission snapshots |
| Risk calculation tests before fee-shortfall addition | 35 passed, 0 failed/skipped | Twelve independent variant cases plus actual composer candidates, tamper detection, sizing, units and cancellation |
| Risk Function tests | 3 passed, 0 failed/skipped | Typed request/result round-trip, persistence/replay/conflict and upstream lineage |
| RiskManager plus composer envelope/contract regression before latest fee changes | 43 passed, 0 failed/skipped | Appended envelope key 12, standard transport and legacy envelope compatibility |
| Trade IntegratedTests, Category=PortfolioFinancialRuntime | 1 passed, 0 failed/skipped | Actual application actor pool, isolated NATS and PostgreSQL completion/reconstruction/replay/conflict; numerical authority/margin are labelled fixtures |
| API Server build | 0 warnings/errors | Includes Risk singleton context alias and the admission snapshot API/query |

Artifacts: `Domain.Portfolio.IntegrationTests/TestResults/financial-integration.trx` and `Domain.Trade.IntegratedTests/TestResults/financial-risk-runtime.trx` (project names have the `TomasAI.IFM.` prefix). Runtime verification requires an isolated `IFM_FINANCIAL_TEST_NATS_URL`.

No gate is closed by these checks. Production Risk policy publication/preparation, durable workflow dispatch/acceptance and Fund authorization, consumption/emulator reconciliation, full migration/cutover, Portfolio financial UI, export, load and release qualification are still required. The existing workflow Risk route has not been switched to the new Function yet. No production capital or writer ownership has been changed.

### 22.4 Subsequent workflow, UI and export implementation — 2026-09-08

This section supersedes the implementation status in 22.3; its earlier test counts remain historical. No financial gate is closed yet.

- Risk preparation now resolves the selected deployment's exact published RiskManagement policy, validates its hash/effective interval and reads an authoritative Portfolio financial snapshot through the typed API. The workflow commits the complete RiskExecution before the Realtime extension dispatches the Function. The legacy Start route is no longer used. Stable preparation/terminal identities survive redispatch.
- PrepareRiskManagement opts into duplicate-audit recovery through a mapped extension: an audit reservation without a committed invocation is retried; committed revision/identity guards suppress repeated transitions. A real actor-pool/NATS/PostgreSQL test pre-reserves the audit and verifies the handler commits its outcome instead of returning an audit-only acknowledgment.
- Risk completion revalidates the saved invocation and independently recomputes its result. Altered quantities, sides, hashes, source identities, expired evidence and generic legacy completions cannot authorize execution. Rejection completes NoTrade. Approval remains Started with no Proceed decision, pending the still-required reservation/Fund/execution handoff.
- RiskParameterSet provides three stable Daily/Weekly/Monthly draft identities and an explicit Emulator-only gross-contract margin schedule. Draft insertion, owning schema validation and exact catalog policy lookup are implemented. Publication/assignment and full production preparation qualification remain required; no profiles or capital were automatically activated.
- Portfolio Administration now opens a read-only Financials dialog with balances, available cash, pending withdrawals, bounded transaction/reservation pages and journal details. Each panel exposes its own financial revision. Selection generations discard stale responses; missing books/outages display unavailable state instead of zero cash. Dark Trading Theme, minimum-size layout, enabled/disabled button colors and money formatting were rendered and checked. Financial write/configuration/migration actions and durable pending-operation UI recovery remain required.
- AccountingExportModel/Store implement the internal PostgreSQL outbox with exact account-version mappings, bounded source revision/journal sets, immutable payloads, unique source inclusion and durable idempotent delivery attempts. Unknown/failed delivery retains the original pending payload. A correction exports separately with original-journal linkage. No remote accounting connector or UI export action is enabled.
- Reconciliation of a replenished overdrawn book moves it to NeedsRefresh, not directly Active; fresh authority is still required. An unresolved cash shortfall remains Overdrawn. History pages now enforce the specified maximum of 100 rows. Capacity admission reads only its bounded requested scopes; upstream evidence loading rejects more than 64 command events.

Executed evidence in this revision:

| Check | Actual result | Scope |
|---|---|---|
| All Trade UnitTests | 959 passed, 0 failed/skipped | Includes Risk preparation/sizing/acceptance, final timeout classification and updated five-actor/wire compatibility inventory |
| All Portfolio UnitTests | 175 passed, 0 failed/skipped | Includes seven independent export mapping/accounting cases |
| Portfolio financial IntegrationTests | 62 passed, 0 failed/skipped | Includes export replay/race/outage/correction/immutability, page bounds, replenished-book recovery and bounded admission reads |
| Portfolio financial VerificationTests | 20 passed, 0 failed/skipped | Standard MessagePack admission/export plus financial mutation/terminal contracts |
| Risk actual actor-pool integration | 2 passed, 0 failed/skipped | Isolated NATS/PostgreSQL Function replay/conflict plus audited-but-uncommitted workflow preparation recovery; authority/margin remain labelled fixtures |
| Financial direct-handler NATS integration | 2 passed, 0 failed/skipped | Standard typed API/message adapter, actual financial Command/Function handlers, PostgreSQL and receipt replay |
| Financial UI plus existing Portfolio layout/startup checks | 4 passed, 0 failed/skipped | Actual WinForms rendering with service fixtures, minimum/default sizes, original three-section layout and composition root |
| Financial presentation UnitTests | 4 passed, 0 failed/skipped | Stale selection/cursor handling and missing/outage state |
| All Portfolio BDDTests | 28 passed, 0 failed/skipped | Includes financial posting scenarios; not the complete financial BDD release matrix |
| Shared Function lifecycle regression | 31 passed, 0 failed/skipped | Default and atomic completion modes |
| API build and --verify-startup-only | Build: 0 warnings/errors; startup: passed | Includes export/recovery/preparation changes; actual composition root, no schemas/actors/feeds/listeners started |

Remaining release work includes full production Risk-to-capacity-to-Fund/Consume/emulator ownership and reconciliation, risk catalog capability publication, financial authorization binding, closed-position lifecycle, all legacy source extraction/import/fencing/cutover/recovery, financial write UI and persisted uncertain-operation journeys, complete multi-host/restart/BDD/verification coverage, operational metrics, load measurements and release runbooks. The rendered viewer and internal export checks do not close those obligations. No production capital import, writer switch, broker submission or external export has occurred.


### 22.5 Financial handoff, posting UI and expiry continuation - 2026-09-08

This is an implementation/evidence update, not a gate-completion claim. It supersedes the open-work descriptions in 22.4 where functionality is listed below.

- `AuthorizeFundOrderRisk` validates the exact typed `FundRiskAuthorizationReference` against the committed reservation under the shared PostgreSQL financial fence. The Fund must still be Active and its order RiskPending. Fund/order/workflow IDs, version, units, requirements, environment, deadlines and four distinct hashes must match. The committed Fund event can be queried without waiting for Scylla. An unfenced Fund event store refuses this authorization. The legacy CandidateSha256 meaning remains CompositionResultHash.
- `AdvanceRiskFinancialHandoffCommand` and its mapped extension persist the exact next request before Realtime dispatch. Stable identities cover Reserve, Fund authorization, workflow execution intent, Consume and internal-emulator submission. The accepted workflow event exposes typed execution evidence; only the exact accepted executable order can pass emulator admission. Queue acceptance alone does not advance a checkpoint: authoritative receipts are checked first. A submitted internal emulator order is immutable and deduplicated across connections/restarts.
- Preliminary local submission/admission scaffolding exists; the broker emulator design and implementation have not started. Portfolio ledger/lifecycle consumers still need qualification with labelled execution-fact fixtures. Emulator fill/fee/settlement/cancel producers and their end-to-end reconciliation belong to the future emulator delivery. The submitted receipt is not evidence of a fill or a live broker order. Full five-stage/financial handoff recovery across timeout/cancellation and every restart boundary also remains open.
- `RecordPositionClose` appends cumulative ClosedUnits while preserving original FilledUnits. Only committed matching execution/financial reconciliation can reduce open position usage. A partial close retains the remaining position and funding commitment; a full close releases it. Unit and PostgreSQL tests independently verify 700 -> 420 -> 0 exposure for 10 filled units with 4 then 10 closed.
- `CapacityExpiryService` discovers at most 32 expired unconsumed reservations and persists exact lifecycle Commands in `capacity_expiry_dispatch`. Two dispatchers share one pending request per reservation. The service reconciles the original operation under the financial fence before replacing an expired attempt; a missing reply never proves release. Consumed/working/filled/unknown execution obligations are excluded. Actual usage mutations remain in `CapacityReservationCommandActor`, not the maintenance service or projector.
- The posting-configuration query supplies effective versioned rules and period state at one financial cut. The Fund Financials transaction dialog supports confirmed deposits, withdrawal requests/confirmation/cancellation and reversal of the selected journal. It uses generated identities, selected server rules, the dark calendar control and aligned multiline description. Post starts disabled until configuration loads. Closed periods cannot dispatch.
- The per-user pending journal writes the complete request before transport and survives navigation/restart. It retains the original OperationId/hash, queries before retry, distinguishes Committed from OutcomeUnknown and never downgrades a committed operation. An expired no-posting status requires an authoritative fenced receipt query. Unexpected/foreign receipts cannot trigger a resend. Pending operations remain visible even when more than 100 newer terminal operations exist. The UI services use labelled API fixtures in rendered tests; they are not proof of deployed authentication or end-to-end financial transport.

Executed checks (latest result per suite; overlapping runs are not additive):

| Check | Actual result | Artifact |
|---|---|---|
| All Trade UnitTests | 969 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Trade.UnitTests/TestResults/financial-handoff-trade.trx` |
| All Portfolio UnitTests | 184 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Portfolio.UnitTests/TestResults/financial-portfolio-unit.trx` |
| Financial PostgreSQL integration including expiry | 83 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Portfolio.IntegrationTests/TestResults/financial-handoff-integration.trx` |
| Expiry PostgreSQL integration | 3 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Portfolio.IntegrationTests/TestResults/financial-expiry.trx` |
| Financial standard transport verification | 23 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Portfolio.VerificationTests/TestResults/financial-verification.trx` |
| Financial NATS Command/Function tests | 2 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Portfolio.IntegrationTests/TestResults/financial-nats.trx` |
| Actual Risk Function/workflow actor-pool integration | 2 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Trade.IntegratedTests/TestResults/financial-risk-runtime.trx`; upstream authority fixtures remain labelled |
| API Development startup verification | Passed | `DOTNET_ENVIRONMENT=Development` and `--verify-startup-only`; no schemas, actors, hosted services, feeds or listeners started |
| Financial presentation tests | 12 passed, 0 failed/skipped | `TomasAI.IFM.UI.Net.Presentation.UnitTests/TestResults/financial-presentation.trx` |
| Financial rendered WinForms journeys | 8 passed, 0 failed/skipped | `TomasAI.IFM.UI.Net.SystemTests/TestResults/financial-ui.trx`; images in `financial-render` |
| All Portfolio BDDTests | 28 passed, 0 failed/skipped | `TomasAI.IFM.Domain.Portfolio.BDDTests/TestResults/financial-bdd.trx`; full FIN-T/FIN-G BDD matrix still incomplete |

The current workspace remains uncommitted. The isolated broker was stopped after messaging/runtime checks. An initial startup check without the Development environment correctly failed the existing synthetic-persistence isolation guard; the explicit Development configuration passed without changing that guard. Checks use .NET SDK 10.0.302/runtime 10.0.10, real local PostgreSQL/Scylla test stores and an isolated NATS 2.12.0 broker on localhost:14222 for financial messaging. Tests use uniquely owned financial books and preserve shared history. Failed intermediate UI/contract runs were corrected and rerun; their earlier failures must not be reported as current passes without the linked rerun.

Requirement dependencies awaiting decisions/evidence:

1. Owner clarification (2026-09-08) resolves the opening-capital scope: **development only**. Use explicitly labelled development opening capital for development qualification; verified production funding is not a prerequisite for that work. The prior general opening-balance migration alternative does not authorize production synthetic funding. Legacy DTOs still lack currency and reliable correction links. The inspected `fund_test_db` contains 15 legacy Fund rows, including zero/unnamed and test Funds. Its one observed canonical transaction is Fund 1, Transaction 1, OpeningTrade, dated 2026-08-29: an opening-position snapshot, not confirmed deposited capital. Observed zero pending write/ownership/projection-mutation counts were not a fenced cut. Preserve source history and classify/quarantine ambiguous rows; never use their balances as a new deposit. Historical migration scope, reconciliation and writer fencing remain required before cutover; no balance import or writer switch has occurred.
2. Production security is explicitly deferred by the owner until just before production deployment. Authentication, transport credentials/subject ACLs and binding permissions to verified caller identity are not prerequisites for the current development gates. The previous single-Windows-versus-multiple-host identity question is no longer a development blocker. Current requests carry development principal/role metadata; their validation tests do not prove authentication. Preserve the metadata/contracts and existing checks so the later production security phase can bind verified identity without redesigning accounting.

The scope decisions above do not imply that all remaining code is finished. Still open for current Portfolio development: execution-fact accounting/capacity consumers and fixture-based reconciliation; complete financial handoff timeout/restart recovery; catalog publication/capability and timeframe/variant qualification; financial migration/fencing/producer redirection; remaining financial configuration/reconciliation UI; exhaustive failpoint/multi-host tests; load budgets/measurements, operational metrics and release runbooks. All seven gates remain In Progress until their stated exit evidence exists.

### 22.6 Development-only opening capital - 2026-09-08

The owner clarified that all opening capital is for development mode only. The API root now registers `FinancialDevelopmentPolicy` using the actual `IHostEnvironment.IsDevelopment()` result; callers cannot enable it through request metadata. An omitted policy denies new opening-capital postings. `GeneralLedgerStore` requires that policy, a stored Emulator book, Importing/unqualified authority and `DevelopmentOpeningCapital` source provenance inside the same financial transaction used for money, receipt and event persistence. Existing import permission, rules, periods, source deduplication and optimistic revision checks remain in force. Both single and batch commands use this store.

No automatic capital amount, existing Fund funding, book activation, qualification bypass or production import was added. An already committed operation can still return its original receipt after development funding is disabled; that recovery does not create new capital. The current work does not require verified production opening funds. Legacy source qualification, scoped writer fencing and the other gate deliverables remain separate requirements.

Verification: **91 financial PostgreSQL integration tests passed, 0 failed/skipped**, including **8 new development-opening-capital cases**. Cases cover missing/disabled host policy, live/incorrect book environment, qualified book, incorrect source provenance, rollback of a preceding valid batch deposit, and an audited development posting followed by replay with development funding disabled. Rejections preserve balances/revision and leave no new source or operation receipt. Evidence: `TomasAI.IFM.Domain.Portfolio.IntegrationTests/TestResults/financial-development-capital-regression.trx`. The earlier focused run passed 7 cases before the batch rollback case was added; it is not additive to the final 91.

API verification also passed with `DOTNET_ENVIRONMENT=Development` and `--verify-startup-only`, including the new policy's composition-root registration. That check started no schemas, actors, feeds or HTTP listeners. `git diff --check` passed. These checks qualify this restriction, not the outstanding financial gates.

### 22.7 Production security deferred; development continues - 2026-09-08

The owner confirmed that the entire trading system is strictly in development and security will be implemented immediately before production. This supersedes earlier authentication requirements as development exit blockers throughout this plan and its source specification/design. No security bypass or removal of existing runtime checks is implemented by this documentation change.

PF-FIN-01 through PF-FIN-07 continue against their functional, financial correctness, recovery, concurrency, migration and UI requirements using explicitly identified development callers and labelled execution-fact fixtures; no functioning emulator is assumed. Real infrastructure tests remain required where specified. Multi-host tests qualify distributed correctness; they do not require selecting a production identity provider now. Preserve atomic commits, authoritative Portfolio/Fund membership and deployment permissions, exact source/receipt validation, idempotency, immutable audit, writer fencing, reservation limits and development-only opening capital. None of these business invariants is deferred.

Before any production deployment, implement and qualify authenticated caller/service identity, transport access controls, permissions bound to that verified identity, credential/secret management, and denial tests for missing, forged, foreign-scope and insufficient-permission callers. Choose the production deployment/identity architecture during that phase. Record separate production security sign-off; development completion is not production readiness.

Documentation-only update: design, specification, plan and implementation manifests now share this scope. No runtime code, infrastructure security configuration or test result was changed. All seven development gates remain In Progress because their outstanding functional and verification deliverables remain; security deferral alone does not close them.

### 22.8 Emulator is a future design and delivery - 2026-09-08

The owner corrected the status: broker emulator design has not started, so emulator implementation is not an outstanding task inside the current Portfolio scope. The earlier list of emulator fills, fees, settlement, cancellation and reconciliation incorrectly expanded that scope. An approved future emulator design must establish order behaviour, execution facts and integration ownership before its implementation.

Repository inspection found preliminary `EmulatorExecutionCommandActor` / `EmulatorExecutionStore` admission scaffolding. It checks exact consumed capacity and commits a local `emulator_order` row, receipt and event; it does not simulate broker behaviour, produce fills/fees or confirm external acceptance. Existing Submitted naming and workflow checkpoints are implementation facts, not proof of an emulator. Preserve this inventory for review against the future design; this documentation correction changes no runtime code or routing.

Current Portfolio gates still require real ledger/capacity processing, conservative obligations, stable identities, recovery and accounting reconciliation. Exercise those consumers through explicitly labelled fact fixtures and real storage/actor tests. Never mark full emulator integration as passed from these fixtures, or fabricate missing execution facts in a running workflow. When the emulator has been designed and implemented, qualify the actual producer-to-consumer integration separately. No additional Function actor or emulator engine is authorized by this correction. All seven gates retain their current In Progress status for remaining Portfolio implementation and tests.


### 22.9 Financial authorization recovery, ledger administration and canonical inventory - 2026-09-09

This continuation implements additional development requirements; none of the seven gates is yet closed. It supersedes earlier descriptions of automatic Consume/emulator submission.

- The workflow now stops at `RiskFinancialHandoffPhase.Authorized=6` after the committed Portfolio reservation and exact Fund authorization. It persists the sized execution intent and completes the workflow without consuming capacity or calling submission scaffolding. Earlier phase numbers and DTO keys are preserved. Legacy ConsumePending/Consumed/Submitted states are not automatically advanced. The future execution owner must obtain current consumption authority before submission; the workflow's calculation/authorization result is not a fill or broker acceptance.
- `FinancialWorkflowRecoveryJournal` reads pages of 32 latest committed workflow snapshots directly from PostgreSQL. Repeated keyset passes also find transactions committed out of allocation order. The hosted recovery service dispatches mapped redispatch/timeout Commands through `IActorService`, retaining saved financial identities. It runs independently of the switch for new ITI triggers. A malformed latest snapshot is reported by stream ID without blocking unrelated streams or falling back to older state. Timeout handling checks the fixed deadline and uses the Risk timeout classification.
- `GetFinancialLedgerConfiguration` returns periods, latest configured account/rule versions and the latest independent ledger reconciliation under one financial revision. Portfolio-wide scope is explicit; selectors are bounded to 256 entries per kind. Closing a period pins its version, reconciliation identity and source cut; storage rejects reconciliation predating a later journal. Reconciliation is of local journal/balance consistency, not external broker reconciliation.
- Portfolio Financials now opens Ledger administration even when no book exists. The screen provides period/account/rule/reconciliation/pending views, reconcile/open/close/reopen/retirement controls and aligned dark-theme fields/buttons. Commands are retained under `PendingFinancialConfigurations`, separate from the posting journal, before dispatch. Recovery checks the original receipt, preserves identity/hash and cannot downgrade committed results.
- Development book setup reads committed current Fund membership and Portfolio execution-account choices. It allocates Book/Account IDs through named sequences and generates other business identities; operators enter no business codes. Prepared configuration contains six account categories and ten initial rules. It deliberately has no generic settlement/notional or arbitrary-adjustment rule. Saving rechecks exact source revisions and creates an Importing, unqualified book with all CanSpend flags false. It adds no capital. Production preparation is not implemented by this development-only flow.
- `StreamCanonicalFundTransactionsAsync` streams canonical Scylla rows for an explicit Fund/date range. `LegacyFinancialInventory` records immutable source payloads, natural-key/content hashes, classifications and amounts into the PostgreSQL inventory tables. Row persistence is resumable; changed scope/content, added rows after completion and missing/duplicate source counts are rejected. Hashing reads 128-row pages. Original precision is retained even for quarantined amounts. The result is explicitly `UnfencedInventory`: it cannot authorize import, activate spending or replace writer fencing. Legacy rows without currency/movement/correction evidence remain quarantined. No source deletion, capital conversion or writer switch occurs in this utility.

Executed evidence at this checkpoint (overlapping runs are not additive):

| Check | Actual result | Artifact / boundary |
|---|---|---|
| All Trade UnitTests after workflow recovery | 985 passed | `financial-recovery-trade-regression.trx`; before subsequent recovery scan error-isolation change |
| Financial workflow recovery plus handoff units | 26 passed | `financial-recovery-unit.trx` |
| PostgreSQL book preparation and workflow recovery | 9 passed | `financial-setup-recovery.trx`; source revisions, no-money book setup, late commit, rollback and malformed latest snapshot |
| Financial presentation tests, FullyQualifiedName~Financial | 27 passed | `financial-controls-presentation.trx`; includes both durable operation journals |
| Financial WinForms system tests | 12 passed | `financial-controls-ui.trx`; real STA forms with labelled API fixtures, minimum-size layout and generated-ID review; subsequent caption-width adjustment requires rerun |
| Financial wire contracts before book-setup addition | 24 passed | `financial-controls-verification.trx`; the new preparation DTO is awaiting the final rerun |
| Financial BDD fixtures | 8 passed | `financial-lifecycle-bdd.trx`; conservative partial-fill/cancel/close accounting, not an emulator |

The canonical inventory test initially exposed a PostgreSQL text/jsonb parameter mismatch; the source payload insert now casts explicitly to jsonb. A repeated whole-suite run also exposed a reused fixture execution-account name; the test now uses a unique account per owned Portfolio, preserving the production exclusive-account constraint. Final regression evidence will replace these pending notes after execution. Remaining work still includes writer fencing/cutover/import qualification, full configuration/authority journeys, comprehensive fact-consumer/multi-host/five-stage tests, load measurements and operational runbooks. These are implementation/qualification tasks, not production-security or emulator blockers.


### 22.10 Fresh development qualification, authority review and measured load - 2026-09-09 UTC

This checkpoint supersedes the pending rerun notes in 22.9. All seven gates remain In Progress; targeted successful runs are not a substitute for their remaining exit matrix.

Implemented in this continuation:

- Fresh development book qualification is a mapped configuration Command (action 11). It demands trusted Development policy, an unqualified nonspending Emulator book, a matched current ledger reconciliation and exact source versions. It installs the per-Fund legacy event/write fence, then checks real Scylla legacy state after releasing PostgreSQL locks. Existing legacy writes/history refuse the fresh path. Successful qualification atomically saves an immutable Qualified migration manifest, receipt and event and moves to NeedsRefresh with CanSpend still false. No application book or capital was activated.
- FundDb transaction insert/delete/backfill writes now persist a PostgreSQL write intent before Scylla mutation; only confirmed completion retires the intent. Unknown completion blocks fresh qualification. The API composition root verifies that its actual FundDb instance carries this fence. This is a fresh-scope boundary, not qualified migration/draining of an existing legacy scope or a guarantee against old unupgraded direct-Scylla writers.
- The posting query exposes development opening capital only for the trusted Development/unqualified Emulator condition. The UI uses the explicit development source and no confirmed external-movement claim. Separate journal reconciliation and qualification actions are available in Ledger administration.
- PrepareFinancialAuthority is wired through the typed API and GeneralLedgerQuery maps. It resolves current committed Portfolio/Fund mandates, policy/envelope versions, effective deployment assignments and exact published ConfigurationDb products. It returns a revision-bound review. Saving checks current source revisions/epoch and cannot change qualified Fund membership. Missing, inactive, unqualified, expired or product-ineligible input cannot grant spending. The UI starts with permission for new spending unchecked and preserves the exact pending request through uncertain outcomes.
- MaximumRiskPerTrade is distinct from aggregate loss. It is appended to deployment authority and admission snapshots and enforced by Risk preparation and reservation admission; missing older fields deny new admission. Shared underlying scope uses published symbol/exchange/currency across expiries/timeframes. Prepared shared Greek caps use the strictest enabled Fund constraint and explicit units. Refresh blocks unreconciled old contract-specific usage.
- Bounded financial telemetry reports attempt outcomes/duration, confirmed rollback retries and successful authority-lock acquisition time. No identifiers, SQL or financial payloads are metric labels; a diagnostic listener cannot alter a commit. Four real-store load workloads were run against budgets declared before execution, and independent journals/cash were reconciled afterward. See [development qualification and operator recovery](Portfolio-Financial-Development-Qualification-v1.0.md).

Latest executed checks (overlapping focused runs are not additive):

| Check | Actual result | Evidence / limits |
|---|---|---|
| Portfolio unit suite | 193 passed | `TestResults/financial-final-TomasAI.IFM.Domain.Portfolio.UnitTests`; includes seven authority preparation/denial cases and scope normalization |
| Trade unit suite | 987 passed | `TestResults/financial-final-TomasAI.IFM.Domain.Trade.UnitTests`; includes separate per-trade budget and missing-cap denial |
| Financial PG/Scylla integration | 116 passed | `TestResults/financial-completion-regression`; includes current source preparation, stale financial revision, immutable qualification, 256/257-line and whole-batch byte boundaries, observer failure isolation, and existing posting/reservation/recovery cases |
| Qualification/authority focused follow-up | 7 passed | `TestResults/financial-qualified-manifest`; corrected test-only migration lookup and database-enforced Qualified-manifest immutability |
| Portfolio BDD suite | 34 passed | `TestResults/financial-final-TomasAI.IFM.Domain.Portfolio.BDDTests`; complete suite count, not proof of the entire remaining release matrix |
| Financial wire verification | 26 passed | `TestResults/financial-current-verification`; standard MessagePack and appended keys |
| Financial presentation | 27 passed | `TestResults/financial-final-TomasAI.IFM.UI.Net.Presentation.UnitTests`; includes durable posting/configuration recovery |
| Financial rendered WinForms | 16 passed | `TestResults/financial-current-ui`; minimum layouts, fresh qualification/opening capital and authority review; API fixtures explicitly labelled |
| Shared Function lifecycle | 31 passed | `TestResults/financial-final-TomasAI.IFM.Shared.UnitTests` |
| Real-store load qualification | 4 passed | `TestResults/financial-load`; single/independent postings, 16 contending reservations and five 100-journal batches; zero unknown outcomes/timeouts/retries |
| API build/composition root | Build passed; Development startup verified | Prepared authority/book services and actual FundDb writer-fence registration; verification starts no schemas, actors, feeds or listeners |

Load observations on Windows 10.0.19045/.NET 10.0.10/32 logical processors: single-post p95 32.5 ms; independent-Portfolio p95 41.1 ms; 16 competing reservations completed by 112.3 ms with one commit and 15 expected revision conflicts; maximum observed 100-journal batch 1,042.0 ms. These are local current-implementation measurements, not before/after optimization claims or production SLAs. The qualification document contains counts, throughput, allocation and lock samples.

Outstanding requirement boundary: legacy source records do not contain currency, confirmed cash-movement provenance, commission-sign qualification or correction/journal relationships. Existing-source migration must select its allowed mode and source/destination mapping and retain explicit quarantine where evidence is absent. The owner has been asked whether development should retain this as read-only history with separately entered development capital or reconstruct postings after qualified mappings are supplied. No history has been silently relabelled as USD cash or used to activate existing Funds.

Remaining implementation/evidence includes existing-scope drain/import/cutover and source disposition, complete account/rule editing journeys, the full five-stage/timeframe/variant matrix, expanded two-host/fault/restart coverage and final gate traceability. These remain open work; production security and the future broker emulator are not being used as development blockers. The unresolved migration decision prevents final migration/release sign-off, not a claim that all other work is complete.


Final follow-up at this checkpoint: the two typed financial NATS Command/Function tests passed using the isolated broker at `nats://127.0.0.1:14222` (`TestResults/financial-final-nats-ipv4`). The initial localhost connection failed; the loopback IPv4 endpoint matched the Docker binding. The two actual Risk/workflow actor-pool tests also passed (`TestResults/financial-final-runtime-owned`) after registering the writer fence in the integration composition root and giving each fixture its own complete contract/workflow stream identity. Repeated qualification never deletes shared workflow history to make a fixture pass. These two tests use explicitly labelled upstream/numerical fixtures and are not the full five-stage matrix. The isolated broker was stopped afterward.

Final API build completed with zero warnings/errors and the rebuilt Development `--verify-startup-only` composition root passed. The 116-test financial regression includes the final writer/manifest/payload/telemetry changes. The observer-failure case proves that a throwing metric listener cannot alter a confirmed posting/receipt/balance. `git diff --check` passed. No commit/push, application capital import, existing-scope cutover, live broker submission or external accounting delivery was performed.
