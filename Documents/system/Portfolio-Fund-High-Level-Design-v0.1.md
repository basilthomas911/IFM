# Portfolio and Fund High-Level Design

**Version:** 0.1  
**Status:** Approved design baseline for detailed specification and implementation planning  
**Scope:** New Portfolio-centric ownership, Fund mandates, trade-template assignment, and Fund-to-OrderComposition handoff  
**Prerequisite for:** TradeSelection, OrderComposition, RiskManagement, and the replacement Portfolio UI  
**Explicitly deferred:** Broker OrderExecution, broker fills, live TradeDb positions, and performance-ID redesign for high-throughput ScyllaDB tables  
**Detailed specification:** [Portfolio-Fund-Specification-v1.0.md](../../TomasAI.IFM.Domain.Portfolio/Docs/Portfolio-Fund-Specification-v1.0.md)
**Implementation plan:** [Portfolio-Fund-Implementation-Plan-v1.0.md](../../TomasAI.IFM.Domain.Portfolio/Docs/Portfolio-Fund-Implementation-Plan-v1.0.md)

## 1. Purpose

This document defines the new authoritative Portfolio and Fund model required by the trade-strategy workflow. It replaces the transitional assumption that a Fund independently owns capital and financial risk, while preserving the useful separation already present in the manual trading application:

- Portfolio and Fund describe investment authority and planned trade composition;
- TradeSelection chooses a permitted trade template;
- OrderComposition constructs an exact candidate order;
- RiskManagement approves, adjusts, or rejects the candidate; and
- a later OrderExecution workflow creates and manages broker-facing TradeDb records.

The immediate implementation stops at the Fund-to-OrderComposition boundary. It must not redesign broker execution, fills, live positions, or the existing execution-facing TradeDb tables as part of the Portfolio/Fund work.

## 2. Authoritative decisions

The following decisions are approved and normative:

1. The ownership hierarchy is `Portfolio -> Fund`.
2. A Fund belongs to exactly one Portfolio for one effective Fund version.
3. Portfolio owns capital, allocation, reserves, financial limits, aggregate exposure, and delegated Fund risk envelopes.
4. Fund owns investment intent, mandate, eligible assets, selection guidance, trade-template assignments, composition-policy assignments, and Fund composition instances.
5. `FundOrder` and `FundOrderTrade` remain Fund-owned composition records. They are not broker orders, fills, or live positions.
6. `TradeTemplate` is reusable versioned selection configuration. `FundOrder` is a particular planned composition instance. These concepts must not be conflated.
7. TradeSelection selects a versioned permitted template or returns `NoTrade`. It does not construct exact contracts, legs, quantities, or prices.
8. OrderComposition constructs the exact non-executable candidate order. It does not contact a broker or create a live position.
9. The Fund composition process reserves the integer `OrderId` and `TradeId` values that all downstream stages must retain unchanged.
10. PostgreSQL `ISequenceIdGenerator` remains the allocator for Portfolio, Fund, Order, and Trade business identifiers.
11. Workflow, command, event, trace, and idempotency identities may remain GUID based because they are technical identities, not operator-facing trade identifiers.
12. New Portfolio and Fund data uses `PortfolioDbContext`.
13. Existing Fund data and current Fund actors are legacy. They are preserved through `FundLegacyDbContext` and are not migrated unless a later requirement explicitly authorizes migration.
14. The existing Funds UI remains available during replacement. A separate Portfolio UI is built and tested before legacy removal.
15. The Trade UI must not create a Fund. Portfolio and Fund administration belongs to the Portfolio UI.
16. All application queries and commands reach actors through NATS messaging. UI and console clients do not access the Portfolio database directly.
17. OrderExecution, broker integration, broker fills, live positions, and the execution-facing TradeDb redesign are later work.
18. Sequence-generated identifiers used by high-throughput ScyllaDB tables require a separate performance and identity-semantics review; they are outside this design.

## 3. Relationship to other designs

This document is the authoritative Portfolio/Fund prerequisite for:

- `TradeSelection-High-Level-Design-v0.1.md`;
- `Intrinsic-Time-Strategy-Workflow-Design-v0.2.md`;
- the future TradeSelection detailed specification;
- the future OrderComposition high-level design and detailed specification;
- the future RiskManagement design; and
- the replacement Portfolio and Trade composition UI.

The implementation-grade commands, events, DTOs, state transitions, NATS services, persistence projections, validation codes, test obligations, and delivery gates are defined by [Portfolio-Fund-Specification-v1.0.md](../../TomasAI.IFM.Domain.Portfolio/Docs/Portfolio-Fund-Specification-v1.0.md). The high-level design remains authoritative for domain intent; the detailed specification is authoritative for the initial repository contract. Any conflict must be reconciled in both documents before implementation proceeds.

The OrderExecution specifications remain valid future-direction documents, but their implementation does not begin as part of this design. Nothing in this document authorizes a broker side effect.

If another document describes Portfolio/Fund as a deferred post-paper-trading refactor, this document supersedes that timing. Portfolio/Fund is now a prerequisite for implementing TradeSelection and OrderComposition against the production-shaped domain model.

## 4. Responsibility model

| Component | Authoritative responsibility | Must not own |
| --- | --- | --- |
| Portfolio | Capital, allocation, reserves, financial risk policy, aggregate exposure, operating state, and delegated Fund envelopes | Trade-template compatibility, exact legs, broker execution, or live marks |
| Fund | Mandate, eligible assets, horizon, objectives, template assignments, composition preferences, and planned FundOrder/FundOrderTrade identities | Portfolio-wide capital authority, broker truth, fills, or live positions |
| Strategy Workflow | Stage sequencing, frozen input/result acceptance, timeouts, terminal semantics, and exactly-once logical progression | Reclassifying stage results or directly contacting a broker |
| TradeSelection | Select one permitted versioned template or `NoTrade` using accepted market decisions and the frozen Fund mandate | Exact expiration, strike, contract, quantity, price, or risk approval |
| OrderComposition | Build an exact, immutable candidate from the selected template, composition profile, and permitted fresh market data | Portfolio authorization, broker submission, or fill assumptions |
| RiskManagement | Apply Portfolio hard limits and delegated FundRiskEnvelope constraints to the exact candidate | Changing upstream market classifications or silently changing economics |
| OrderExecution | Future broker submission, acknowledgement, replace/cancel, fill, reconciliation, and execution truth | Selecting a strategy or overriding an approval |
| TradeDb execution model | Future working orders, broker facts, fills, durable trades, and live-position inputs | Portfolio/Fund configuration authority |

The governing principle is:

> **Fund owns investment intent and composition guidance. Portfolio owns capital and financial risk. OrderExecution owns broker effects only after strategy approval.**

## 5. Conceptual model

```text
Portfolio
  +-- PortfolioPolicy versions
  +-- FundAllocation versions
  +-- FundRiskEnvelope versions
  `-- Fund
       +-- FundMandate versions
       +-- TradeTemplate assignments
       +-- TradeSelectionHintProfile assignments
       +-- OrderCompositionProfile assignments
       `-- FundOrder
            `-- FundOrderTrade
                 |
                 | selected and composed by the strategy workflow
                 v
            OrderCompositionResult
                 |
                 | approved by RiskManagement
                 v
            Future OrderExecution
                 |
                 v
            Future TradeDb working order, fills, trade, and position
```

## 6. Terminology

### 6.1 Portfolio

A Portfolio is the top-level financial authority for a broker/account scope. It owns capital and financial-risk policy and delegates bounded authority to Funds.

### 6.2 Fund

A Fund is a versioned investment mandate within one Portfolio. It describes what economic opportunity the Fund pursues and which structures it may use. A Fund does not independently create authoritative capital or risk limits.

### 6.3 TradeTemplate

A `TradeTemplate` is reusable versioned configuration describing a selectable trade structure, such as an ES directional future, ES option vertical, or directionally biased Iron Condor. It supplies compatibility and structural constraints, not current contracts or prices.

### 6.4 OrderCompositionProfile

An `OrderCompositionProfile` is reusable versioned construction policy. It may describe DTE bands, delta or strike-distance targets, width ranges, debit/credit preferences, leg shapes, quantity rules, price rules, liquidity requirements, and calculation versions.

### 6.5 FundOrder

A `FundOrder` is one Fund-owned planned composition instance. It binds a globally unique integer `OrderId` to one Portfolio, Fund, workflow context, selected template, composition policy, underlying intent, and lifecycle state.

It is not the broker order. It is the Fund's durable composition identity and audit record.

### 6.6 FundOrderTrade

A `FundOrderTrade` is one planned trade instruction inside a FundOrder. It binds a globally unique integer `TradeId` to the intended trade family, direction/action, role, and composition context.

It is not a fill and does not prove that a live position exists. A FundOrder may contain multiple FundOrderTrades when a future strategy requires related opening, closing, hedge, roll, or adjustment instructions.

### 6.7 OrderCompositionResult

An `OrderCompositionResult` is the immutable exact candidate produced by OrderComposition. It includes current contracts, expiration, strikes, legs, quantities, prices or price instructions, economics, calculation evidence, and the accepted identity/version chain.

It is not executable until RiskManagement approves it and the Strategy Workflow accepts the approval.

### 6.8 TradeDb trade and position

A TradeDb trade is future execution truth created through OrderExecution. A position exists only from confirmed fills, or from an explicitly approved manual-fill workflow. Current manual-entry behavior is legacy and is not the model for the new automated path.

## 7. Portfolio aggregate

### 7.1 Minimum Portfolio identity and lifecycle

The Portfolio aggregate requires:

| Field | Purpose |
| --- | --- |
| `PortfolioId` | Positive operator-facing integer identity |
| `PortfolioCode` | Stable operator-facing code |
| `Name` | Display name |
| `PortfolioVersion` | Immutable business configuration version |
| `BaseCurrency` | Currency for capital and risk normalization |
| `BrokerAccountRefs` | Versioned references to permitted accounts; no credentials |
| `OperatingState` | Draft, Active, Paused, ReduceOnly, Disabled, or Retired |
| `EffectiveFromUtc` / `EffectiveUntilUtc` | Version validity interval |
| `PolicyVersion` | Active Portfolio financial-policy version |
| `CreatedOnUtc` / `CreatedBy` | Audit provenance |
| `SupersededOnUtc` / `SupersededBy` | Version-retirement provenance |

Physical deletion is not a normal business operation. A Portfolio is superseded or retired so historical workflow attribution remains resolvable.

### 7.2 Portfolio authority

Portfolio policy owns at least:

- approved capital base and protected reserves;
- deployable capital;
- target Fund allocations and permitted allocation ranges;
- Portfolio and per-Fund delegated risk envelopes;
- maximum gross and net notional;
- leverage and margin limits;
- concentration and correlation limits;
- aggregate delta, gamma, vega, and theta limits where applicable;
- risk per candidate and aggregate open/working risk;
- working-order reservations;
- drawdown and loss limits;
- liquidity, exit-capacity, and event-risk constraints;
- Portfolio operating and recovery states; and
- policy version, effective interval, and expiry.

The first implementation may populate only the minimum values required by TradeSelection, OrderComposition, and RiskManagement, but ownership must not be placed back on Fund merely for convenience.

## 8. Fund aggregate and mandate

### 8.1 Minimum Fund identity and lifecycle

| Field | Purpose |
| --- | --- |
| `PortfolioId` | Authoritative parent Portfolio |
| `FundId` | Positive operator-facing integer identity |
| `FundCode` | Stable operator-facing code |
| `Name` | Display name |
| `FundMandateVersion` | Immutable mandate version |
| `TradingYear` | Optional annual configuration boundary |
| `OperatingState` | Draft, Active, Paused, Disabled, or Retired |
| `EffectiveFromUtc` / `EffectiveUntilUtc` | Version validity interval |
| `DecisionHorizon` | Daily, Weekly, Monthly, or later configured horizon |
| `Objective` | Income, directional growth, volatility, mixed, or named objective |
| `UnderlyingUniverse` | Permitted economic exposures and roots |
| `EligibleAssetTypes` | Futures, futures options, or later approved asset types |
| `CreatedOnUtc` / `CreatedBy` | Audit provenance |

### 8.2 Fund mandate

The Fund mandate may define:

- permitted directions and biases;
- permitted RegimeDiscovery and MarketCondition classifications;
- preferred payoff intents;
- permitted strategy and trade families;
- preferred holding-period and entry-frequency ranges;
- minimum market-quality preferences;
- operational-complexity preferences;
- template preference weights where more than one template is later enabled;
- TradeSelection hint-profile references;
- OrderComposition profile references; and
- deterministic summary and reason-code versions.

The mandate may express preferences. It cannot override Portfolio hard constraints or a blocked FundRiskEnvelope.

### 8.3 Fund reporting versus authority

Fund projections should report:

- planned composition instances;
- working orders and positions received from future execution projections;
- realized and unrealized P&L;
- risk and margin utilization;
- performance and expectancy;
- active workflows; and
- incidents and health.

Reporting a value does not make Fund the authority that sets its financial threshold.

## 9. Template and profile assignments

Assignments are versioned relationships rather than mutable fields copied into every Fund row.

### 9.1 TradeTemplate assignment

An assignment contains:

- Portfolio and Fund identity/version;
- TradeTemplate identity/version;
- eligible horizon and underlying universe;
- enabled/paused state;
- effective interval;
- priority or preference weight if applicable;
- TradeSelection hint-profile identity/version; and
- OrderComposition profile identity/version.

### 9.2 Minimum initial catalog

The initial ES catalog remains:

| Horizon | Initial Fund intent | Trade family |
| --- | --- | --- |
| Daily | Directional ES exposure | Directional future |
| Weekly | Defined-risk directional ES exposure | Futures-option vertical spread |
| Monthly | Directionally biased range/income exposure | Futures-option Iron Condor |

These mappings are initial configuration, not permanent domain restrictions. The model must allow later templates and additional Funds without changing actor contracts structurally.

## 10. Fund composition lifecycle

### 10.1 Automated path

The minimum automated sequence is:

1. Strategy Workflow resolves and freezes Portfolio, Fund mandate, template assignments, and required policy versions.
2. RegimeDiscovery and MarketCondition complete normally.
3. TradeSelection evaluates only templates permitted by the frozen Fund mandate and Portfolio permissions.
4. On `Selected`, the workflow requests the Fund composition authority to reserve an integer OrderId and the required integer TradeId values.
5. The Fund actor creates a FundOrder/FundOrderTrade composition record bound to the accepted TradeSelection result.
6. OrderComposition consumes the frozen identity and policy chain and creates an exact candidate.
7. The workflow accepts or rejects the OrderComposition terminal result.
8. RiskManagement evaluates the exact candidate using the frozen Portfolio policy and FundRiskEnvelope.
9. The Strategy Workflow ends successfully, normally with no trade, or as failed according to its existing terminal rules.
10. A later OrderExecution workflow may consume only a committed, risk-approved result.

### 10.2 Manual composition path

A manual composition UI may remain useful, but it must use the same Portfolio/Fund actor commands, template catalog, composition profiles, identity reservation, validation, and candidate contracts as the automated path. Manual operation is an origin type, not permission to bypass TradeSelection, OrderComposition, or risk rules.

### 10.3 Composition states

The design requires stable state concepts. Exact enum names are finalized in the specification, but the minimum semantics are:

- Draft;
- IdentityReserved;
- TemplateSelected;
- Composing;
- Composed;
- CompositionFailed;
- RiskPending;
- RiskRejected;
- RiskApproved;
- Cancelled;
- Expired; and
- future ExecutionRequested, Executing, Executed, and ExecutionFailed states.

Execution states are reserved for compatibility but are not implemented by the Portfolio/Fund phase.

### 10.4 Immutability

Once a workflow accepts a FundOrder identity and version:

- OrderId and TradeId cannot change;
- PortfolioId and FundId cannot change;
- selected template and profile versions cannot change;
- accepted upstream result identities/hashes cannot change;
- material economic changes create a new composition revision; and
- no configuration update may rewrite an in-flight or historical composition.

## 11. Pipeline contracts

### 11.1 Frozen Portfolio/Fund context

The Strategy Workflow should carry a resolved immutable context containing at least:

| Group | Minimum fields |
| --- | --- |
| Workflow | WorkflowId, StageInvocationId, EntityId, revision, trace context, started/evaluated timestamps |
| Portfolio | PortfolioId, PortfolioVersion, operating state, policy identity/version |
| Fund | FundId, FundMandateVersion, operating state, horizon, objective, eligible universe |
| Delegation | FundRiskEnvelope identity/version, capacity state, validity interval |
| Catalog | Eligible TradeTemplate assignments and versions |
| Selection | TradeSelection hint-profile identity/version |
| Composition | OrderComposition profile identity/version |

The snapshot is resolved once for the workflow version. Stages validate it at their trust boundaries but do not silently refresh it mid-workflow.

### 11.2 TradeSelection input and output

TradeSelection consumes:

- accepted RegimeDiscoveryDecision;
- accepted MarketConditionDecision;
- frozen Portfolio/Fund context;
- eligible template assignments; and
- a versioned deterministic selection parameter set.

TradeSelection returns either:

- `Selected`, containing one TradeTemplate identity/version, direction or bias, composition-policy reference, structural constraints, evidence, and validity; or
- `NoTrade`, containing stable incompatibility evidence.

It does not create FundOrder IDs, exact legs, prices, or broker fields.

### 11.3 Composition identity reservation

After `Selected`, the workflow sends a NATS command conceptually equivalent to:

`ReserveFundOrderCompositionCommand`

The command includes the accepted workflow, Portfolio, Fund, template, profile, result identity/hash, and required trade-role count. The Fund actor returns a committed composition reference containing:

- PortfolioId;
- FundId;
- FundOrderVersion;
- integer OrderId;
- one or more integer TradeId values;
- template/profile versions;
- reservation timestamp;
- idempotency identity; and
- composition status.

Retries with the same idempotency identity must return the same reservation. They must not consume or attach different IDs.

### 11.4 OrderComposition input

OrderComposition consumes:

- accepted TradeSelectionResult;
- committed Fund composition reference;
- frozen Portfolio/Fund context;
- exact OrderComposition profile;
- permitted futures/options reference and market data;
- current liquidity and price evidence;
- quantity and capacity boundaries supplied by Portfolio/Risk policy; and
- deterministic calculation versions.

OrderComposition may use additional relevant market data required for exact construction. It must record every input identity, source timestamp, freshness decision, and version used.

### 11.5 OrderComposition output

The immutable result contains at least:

- PortfolioId, FundId, OrderId, and TradeId values;
- FundOrder and composition revision;
- selected template and profile versions;
- underlying and instrument contracts;
- expiration and DTE;
- ordered legs, actions, ratios, and quantities;
- order action and proposed order type;
- price or price-policy result;
- expected debit/credit and commissions;
- payoff, maximum-profit, maximum-loss, breakeven, and stress calculations as applicable;
- liquidity and tradability checks;
- composition evidence, warnings, and stable reason codes;
- result identity/hash, created time, and validity time; and
- `Composed`, `NoCandidate`, or `Failed` terminal semantics.

It contains no broker order ID and causes no external effect.

## 12. Integer identity strategy

### 12.1 Business identifiers

The following remain positive integer business identifiers:

- PortfolioId;
- FundId;
- OrderId; and
- TradeId.

OrderId and TradeId are intended for concise operator communication, blotter use, reconciliation, and institutional trading workflows.

### 12.2 Allocation

`ISequenceIdGenerator` backed by PostgreSQL remains the only application-facing allocator. Named PostgreSQL sequences reserve disjoint blocks for each application instance. The existing `Fund_FundId`, `Trade_OrderId`, and `Trade_TradeId` sequences remain authoritative, and implementation adds a `Portfolio_PortfolioId` sequence before Portfolio creation is enabled. The Fund composition authority requests and commits IDs; downstream actors accept them and never renumber them.

Gaps are valid. Request-completion order is not a business guarantee. A reserved ID is never reused after cancellation, failure, or process termination.

### 12.3 Identity chain

The complete attribution key is:

`PortfolioId + FundId + OrderId + TradeId`

OrderId and TradeId are globally unique under their named sequences, while PortfolioId and FundId preserve explicit ownership and support direct query projections. No historical lookup should infer Portfolio solely from the Fund's current membership.

### 12.4 Capacity and overflow

Current OrderId and TradeId contracts use signed 32-bit integers. Allocation and projection layers must monitor high-watermarks and fail safely before overflow. Changing operator-facing ID width requires a separately versioned contract decision; silent wrapping is forbidden.

### 12.5 Deferred high-throughput ScyllaDB review

The PostgreSQL allocator is approved for low-volume business identifiers. It is not automatically approved for every ScyllaDB `sequenceId` or `tickId`.

Before high-throughput market data or execution tables are redesigned, every generated ID must be classified by purpose:

- human-facing identity;
- source-provided sequence;
- ordering within a partition;
- replay/idempotency identity;
- pagination cursor; or
- globally unique technical row identity.

Source sequences, composite keys, event-derived identities, partition-local sequences, timestamps, or time-based UUIDs may be more appropriate for performance-sensitive inserts. That review is explicitly deferred and must not block Portfolio/Fund implementation.

## 13. Actor topology and NATS APIs

### 13.1 Actor boundaries

The initial topology should expose separate logical responsibilities:

- Portfolio Command actor;
- Portfolio Query actor;
- Fund Command actor within the Portfolio domain;
- Fund Query actor;
- Fund composition Command actor or a clearly separated composition command surface on the Fund actor;
- Portfolio/Fund EventProjector actors; and
- existing Strategy Workflow, TradeSelection, OrderComposition, and RiskManagement actors.

The detailed specification decides whether Portfolio and Fund commands are implemented by separate actor classes or one bounded context with distinct subjects. The public contracts must remain separated by responsibility.

### 13.2 Minimum commands

Minimum command concepts include:

- CreatePortfolio;
- AddPortfolioVersion;
- ChangePortfolioOperatingState;
- AddFundToPortfolio;
- AddFundMandateVersion;
- ChangeFundOperatingState;
- AssignTradeTemplateToFund;
- AssignTradeSelectionHintProfile;
- AssignOrderCompositionProfile;
- DelegateFundRiskEnvelope;
- ReserveFundOrderComposition;
- RecordFundOrderComposed;
- RecordFundOrderCompositionFailed;
- RecordFundOrderRiskOutcome; and
- CancelFundOrderComposition.

Names, payloads, MessagePack keys, and subject conventions are finalized in the specification.

### 13.3 Minimum queries

Minimum typed NATS queries include:

- GetPortfolio;
- GetPortfolioVersion;
- GetPortfolios;
- GetFundsByPortfolio;
- GetFundMandate;
- GetActiveFundByPortfolioAndHorizon;
- GetFundTemplateAssignments;
- GetFundComposition;
- GetFundCompositionsByPortfolioAndFund;
- GetFundOrderByOrderId;
- GetFundOrderTradeByTradeId; and
- GetPortfolioFundStrategyReferenceCombinations.

List queries must use bounded paging or streaming contracts where result size is not strictly bounded.

### 13.4 Terminal semantics

Every mutation follows the established actor conventions:

- validate before mutation;
- emit exactly one logical terminal outcome;
- preserve CommandId and trace context;
- support idempotent retry;
- project only committed events;
- use stable error categories and codes; and
- never treat a business rejection as an infrastructure failure.

## 14. Persistence boundaries

### 14.1 PortfolioDbContext

The new context owns logical projections for:

- Portfolio identity and versions;
- Portfolio policies and operating state;
- Funds and mandate versions;
- Portfolio-to-Fund membership history;
- Fund allocations and FundRiskEnvelope versions;
- TradeTemplate and profile assignments;
- FundOrder composition instances;
- FundOrderTrade composition instructions; and
- composition status/result references and audit provenance.

Exact physical CQL tables, partition keys, clustering order, paging, and retention policies belong to the detailed storage specification. Tables must be designed from approved query paths rather than relational normalization assumptions.

### 14.2 Strategy workflow persistence

The Strategy Workflow remains authoritative for accepted stage results, result hashes, continuation decisions, and workflow terminal state. PortfolioDb may project references needed for Portfolio/Fund views, but it must not create a second mutable authority for the exact stage result.

### 14.3 TradeDbContext

The existing TradeDb strategy-workflow projections may continue to support the current workflow implementation. Execution-facing TradeDb tables are not redesigned here.

Future OrderExecution will create working-order, acknowledgement, fill, trade, and live-position records using the approved PortfolioId, FundId, OrderId, and TradeId chain. TradeDb must not allocate replacement business IDs.

### 14.4 FundLegacyDbContext

Current Fund tables and historical data are legacy. They are retained through a `FundLegacyDbContext` boundary:

- no automatic migration;
- no dual write;
- no dual read in new Portfolio actors;
- no compatibility requirement unless explicitly approved;
- read-only behavior by default after cutover; and
- separate UI labeling and operational controls.

Existing table shapes may be consulted as design input but do not constrain the new schema.

## 15. Query projections

The new storage design must directly support the approved UI and actor queries. Minimum logical projections include:

- portfolios by operating state;
- Portfolio by PortfolioId;
- Funds by PortfolioId;
- active Fund by PortfolioId, trading year, and horizon;
- Fund mandate by FundId and version;
- template/profile assignments by FundId and mandate version;
- current FundRiskEnvelope by PortfolioId and FundId;
- FundOrders by PortfolioId, FundId, status, and time window;
- FundOrderTrades by PortfolioId, FundId, and OrderId;
- composition lookup by globally unique OrderId;
- trade-instruction lookup by globally unique TradeId; and
- active workflows/composition status by PortfolioId and FundId.

An optional Portfolio-wide blotter query may expose all Funds, but it requires its own projection. It must not perform an unbounded cross-partition scan.

## 16. UI design

### 16.1 Navigation

During transition:

- retain the existing Funds menu and current Fund UI;
- add a separate Portfolio menu and new Portfolio UI;
- label the old destination as Legacy Funds when operationally appropriate; and
- remove the old UI only after the new UI and workflows pass acceptance gates.

### 16.2 Portfolio UI minimum capabilities

The new UI should support:

- list and inspect Portfolios;
- create/version/retire Portfolio configuration;
- list Funds within a Portfolio;
- create/version/retire Fund mandates;
- assign templates and selection/composition profiles;
- inspect allocations and delegated FundRiskEnvelope versions;
- view Fund composition history and status;
- inspect immutable workflow/template/profile provenance; and
- display validation and terminal actor results.

### 16.3 Trade composition/blotter UI

The existing manual blotter interaction is retained conceptually:

1. select Portfolio;
2. select Fund within the Portfolio;
3. view FundOrders;
4. select a FundOrder;
5. view its FundOrderTrades;
6. inspect composition and, later, execution/position details.

Minimum changes are:

- add Portfolio selection;
- filter Funds by Portfolio;
- remove Create Fund from the Trade screen;
- retain integer OrderId and TradeId display/search;
- keep selection and detailed-view behavior; and
- use typed NATS queries rather than direct storage calls.

Until replacement is complete, the existing Trade UI remains a legacy manual blotter. It must not be partially rewired so that one operation mutates legacy Fund state while another mutates new Portfolio state.

## 17. Concurrency, consistency, and idempotency

### 17.1 Aggregate serialization

The actor mailbox remains the serialization boundary for one aggregate identity. Portfolio policy updates, Fund mandate updates, and composition reservations use explicit expected versions.

### 17.2 Composition reservation

The same workflow and reservation idempotency key must always resolve to the same FundOrder identity. A duplicate command must not allocate another OrderId or TradeId.

### 17.3 Frozen versions

A workflow must reject mismatched Portfolio, Fund, template, hint-profile, composition-profile, and risk-envelope versions. It cannot silently resolve the latest version after starting.

### 17.4 Projection behavior

Projectors must be replay-safe and idempotent. Older events or revisions cannot overwrite newer projections. Failed projection delivery must be retryable without reallocating IDs or duplicating composition history.

## 18. Validation and invariants

Minimum invariants include:

- positive PortfolioId, FundId, OrderId, and TradeId;
- one effective parent Portfolio per Fund version;
- no overlapping active versions for the same Portfolio or Fund identity where uniqueness is required;
- an active Fund requires an active parent Portfolio;
- a Fund template assignment must reference an allowed underlying, asset type, horizon, and effective version;
- TradeSelection may select only a template present in the frozen mandate;
- composition profile must match the selected template/version;
- FundOrder identity must match the accepted Portfolio/Fund/workflow context;
- FundOrderTrade identities must belong to their FundOrder;
- one business ID is never reassigned after reservation;
- OrderComposition output identities must exactly match the committed Fund composition reference;
- no candidate may be marked risk approved without a matching accepted RiskManagement result; and
- no Portfolio/Fund command may cause a broker side effect.

## 19. Observability and audit

Commands, events, projections, queries, and UI status should expose, where applicable:

- WorkflowId and StageInvocationId;
- trace and correlation context;
- PortfolioId and PortfolioVersion;
- FundId and FundMandateVersion;
- OrderId and TradeId;
- template, hint-profile, composition-profile, and risk-envelope versions;
- expected/current aggregate version;
- actor, verb, result, reason code, and error category;
- started, evaluated, committed, and projected timestamps; and
- idempotency and replay disposition.

Operator summaries may describe outcomes but are not authority. Machine-readable events and results remain authoritative.

## 20. Security and authorization

The detailed specification must define authorization for:

- Portfolio creation and policy versioning;
- Fund creation and mandate versioning;
- allocation and FundRiskEnvelope changes;
- template/profile assignment;
- manual composition initiation or cancellation;
- retirement/reactivation operations; and
- future execution authorization.

Broker credentials and secrets do not belong in Portfolio or Fund records. Only stable account references and approved scopes are stored.

## 21. Test strategy and gates

### 21.1 Unit tests

Unit tests cover:

- aggregate invariants and version transitions;
- mandate/template/profile compatibility;
- Portfolio/Fund identity validation;
- composition state transitions;
- deterministic snapshot construction;
- integer ID retention;
- idempotent reservation behavior;
- mapping and serialization; and
- no broker-side-effect boundaries.

### 21.2 BDD tests

BDD scenarios cover:

- create and activate Portfolio;
- add and activate Fund mandate;
- assign initial Daily, Weekly, and Monthly templates;
- resolve active Fund by Portfolio/horizon;
- select a permitted template;
- reject a template outside the mandate;
- reserve one FundOrder and required FundOrderTrades;
- repeat a reservation command without allocating new IDs;
- compose a valid candidate with retained identities;
- reject stale or mismatched versions;
- record composition failure or `NoCandidate` without execution; and
- prove that no broker or live-position operation occurs.

### 21.3 Integration tests

Integration tests use real NATS actor routing and the configured PostgreSQL/Scylla test infrastructure to verify:

- commands, events, projection, and typed queries;
- sequence allocation across multiple application instances;
- restart/replay and duplicate delivery;
- optimistic concurrency;
- Portfolio/Fund query projections;
- strategy workflow snapshot handoff;
- TradeSelection-to-composition identity reservation;
- OrderComposition result recording; and
- legacy/new context isolation.

### 21.4 Verification tests

Verification tests cover representative Portfolio/Fund/template combinations rather than uncontrolled Cartesian expansion. At minimum they verify:

- Daily Fund with directional future template;
- Weekly Fund with bullish and bearish vertical templates;
- Monthly Fund with neutral and directional-bias Iron Condor templates;
- active, paused, disabled, expired, missing, and duplicate configuration states;
- Portfolio Green/Constrained/Blocked-style permission inputs as defined by Risk design;
- TradeSelection `Selected` and `NoTrade` paths;
- OrderComposition `Composed`, `NoCandidate`, and `Failed` paths;
- immutable identity/version propagation; and
- no OrderExecution dispatch in the Portfolio/Fund implementation phase.

### 21.5 UI system tests

System tests verify:

- Portfolio navigation is separate from legacy Funds;
- existing Funds navigation remains operational during transition;
- Portfolio selection filters Funds;
- Fund selection filters composition records;
- OrderId/TradeId selection opens the expected detail;
- Create Fund is absent from the new Trade composition UI;
- actor errors and terminal status are visible; and
- no test leaves shared Portfolio, Fund, or temporary export data behind.

## 22. Implementation sequence

The recommended delivery order is:

1. maintain approval of this HLD and terminology;
2. review and approve the linked Portfolio/Fund detailed specification and its PF-01 through PF-20 implementation gates;
3. define shared identities, versions, snapshots, and MessagePack contracts;
4. implement PortfolioDb schema and contexts;
5. implement Portfolio/Fund actors, projectors, and NATS APIs;
6. implement template/profile assignment and active-Fund resolution;
7. implement FundOrder/FundOrderTrade reservation and lifecycle;
8. build the new Portfolio UI while retaining legacy Funds;
9. update TradeSelection to consume the frozen new Fund mandate;
10. design and implement OrderComposition against committed Fund composition identities;
11. implement Portfolio-aware RiskManagement;
12. complete all BDD, unit, integration, verification, and UI gates;
13. design OrderExecution and the execution-facing TradeDb replacement as a separate final workflow; and
14. review high-throughput ScyllaDB sequence-ID strategies during that later storage design.

## 23. Explicitly deferred work

The following are not part of the Portfolio/Fund implementation:

- broker API submission;
- broker order IDs;
- acknowledgement, replace, cancel, and reconciliation;
- automated fill ingestion;
- partial-fill and overfill handling;
- live TradeDb position creation;
- market-feed-driven position updates;
- final TradeDb execution schema;
- migration of legacy Fund, order, trade, or position history;
- removal of the existing Funds UI;
- removal of the existing manual blotter;
- high-throughput tick-table key redesign; and
- a universal sequence-ID strategy for all ScyllaDB tables.

Deferral does not permit placeholder shortcuts that would make the later OrderExecution boundary unsafe. The Portfolio/Fund result must preserve complete immutable attribution and exact accepted versions for that future handoff.

## 24. Acceptance criteria

This HLD is ready for detailed specification when stakeholders accept that:

- Portfolio is the financial authority;
- Fund is the mandate and composition-intent authority;
- FundOrder/FundOrderTrade are composition records, not execution truth;
- TradeSelection chooses templates and OrderComposition creates exact candidates;
- integer OrderId and TradeId values are allocated through PostgreSQL and retained unchanged;
- PortfolioDb is the new context while existing Fund data remains isolated legacy history;
- the Portfolio UI is introduced alongside, not by mutating, the existing Funds UI;
- all actor interactions use NATS;
- execution-facing TradeDb changes are deferred; and
- the high-throughput ScyllaDB ID review is retained as mandatory later work.

## 25. Summary

The new production-shaped hierarchy is Portfolio to Fund. Portfolio owns capital and financial risk; Fund owns mandate, selectable structures, and planned composition identities. TradeSelection chooses an allowed versioned TradeTemplate. The Fund composition authority commits the integer OrderId and TradeId identities. OrderComposition creates an exact immutable candidate using those identities. RiskManagement decides whether the Portfolio may accept it.

No broker effect occurs in this phase. OrderExecution and live TradeDb positions remain a separate final workflow. This allows Portfolio/Fund, TradeSelection, and OrderComposition to be designed and verified now without preserving the limitations of the legacy manual execution path.
