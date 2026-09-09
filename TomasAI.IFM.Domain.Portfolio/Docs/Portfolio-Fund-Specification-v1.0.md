# Portfolio and Fund Detailed Specification v1.2

> **Emulator scope correction (2026-09-08):** The IBKR emulator design has not started; its implementation is a future delivery after design approval. Do not list emulator fills, fees, settlement, cancellation or reconciliation as unfinished emulator work in the current Portfolio delivery. Current scope covers Portfolio accounting/capacity contracts, consumers and financial integrity tests using explicitly labelled execution-fact fixtures. Those tests do not qualify an emulator. Existing local submission/admission scaffolding is not a functioning emulator or evidence of broker acceptance. Full emulator integration is deferred; it is not a blocker for completing the current Portfolio development scope.

> **Development scope decision (2026-09-08):** The system is strictly in development while the complete trading system is being built. Production security implementation and qualification are deferred until immediately before production deployment. They do not block current development implementation or PF-FIN gate completion. Retain existing role/scope checks and all financial integrity rules; development principal/role metadata is not authenticated identity. Opening capital remains development-only. Completing development gates does not authorize production deployment.

> **Financial-domain revision — 2026-09-08:** Sections 37–46 specify `GeneralLedger` and `CapacityReservation`, atomic PostgreSQL financial completion, Fund transaction migration/UI, and the future QuickBooks boundary. These normative requirements have development implementations; gate status and executed evidence are recorded in the implementation plan and the 2026-09-09 gate evidence report. They supersede earlier transaction-migration exclusions and configuration-only completion criteria. The filename is retained for link compatibility.

> **Current catalog/pipeline alignment:** ConfigurationDb owns active strategy catalog authoring. Portfolio mandates, assignments and policy limits use exact deployment GUID/version references. Legacy family identities remain historical contracts. [Integration details](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md) and [UI guide](../../TomasAI.IFM.UI.Net/Docs/Strategy-Catalog-Reference-UI.md) supersede older family-authoring descriptions below. Regime Discovery, Market Condition, Trade Selection, Order Composition and Risk Management Function implementations exist, together with the two Portfolio capacity Functions and ledger/lifecycle Commands.

> **Historical catalog scope:** Earlier three-family restrictions and family-to-timeframe examples describe the original PF phase. New financial admission uses exact supported deployment/strategy/structure/variant versions; all twelve supported futures/vertical/iron-condor variants may use one triggering Daily, Weekly or Monthly horizon. Catalog existence never grants financial authority.

**Status:** Normative financial-domain specification with development implementation; see the current financial gate evidence. Earlier PF evidence retains its original scope.
**Date:** 2026-09-08
**Supersedes:** Earlier revisions where explicitly changed; existing wire keys, hashes and historical readers retain their original meanings
**Domain:** `TomasAI.IFM.Domain.Portfolio`  
**Authoritative design:** [Portfolio-Fund-High-Level-Design-v0.1.md](../../Documents/system/Portfolio-Fund-High-Level-Design-v0.1.md)  
**Related TradeSelection design:** [TradeSelection-High-Level-Design-v0.1.md](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/Docs/TradeSelection-High-Level-Design-v0.1.md)\
**Implementation plan:** [Portfolio-Fund-Implementation-Plan-v1.0.md](./Portfolio-Fund-Implementation-Plan-v1.0.md)  
**Runtime target:** .NET 10, MessagePack, NATS Core/JetStream, PostgreSQL EventSourceDb and SequenceIdDb, and ScyllaDB projections  
**Implementation boundary:** Portfolio/Fund configuration, General Ledger/Fund transactions, atomic capacity admission, financial history/UI, migration and reconciled execution-accounting inputs
**Deferred boundary:** Actual IBKR connectivity, external QuickBooks connector, broad execution-facing TradeDb replacement and unrestricted multicurrency support

**Financial boundary:** [Order Composition](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/OrderComposition-Specification-v1.0.md) produces one unapproved unit. [Risk Management](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RiskManager/Docs/RiskManagement-High-Level-Design-v0.1.md) determines final units. Portfolio's `CapacityReservationFunctionActor` reserves capacity against General Ledger balances and current financial authority, committing the receipt and completed event atomically before returning Complete. Recording a result reference alone does not implement that operation.

## 1. Purpose

This specification converts the Portfolio/Fund high-level design into repository-specific, testable requirements. It defines the domain types, actor boundaries, commands, events, queries, state transitions, persistence projections, pipeline handoffs, UI contracts, reason codes, gates, and definition of done needed to implement the new Portfolio-centric model.

The specification preserves the existing conceptual separation:

- Portfolio owns capital and financial-risk authority;
- Fund owns mandate, template assignments, selection guidance, and planned composition identities;
- TradeSelection selects a permitted template;
- OrderComposition creates an exact non-executable one-unit candidate with per-unit leg ratios;
- RiskManagement determines final units and independently evaluates financial eligibility;
- GeneralLedger owns authoritative financial postings and balances;
- CapacityReservation commits financial holds before accepted execution handoff; and
- future OrderExecution owns broker effects and execution-facing TradeDb records.

## 2. Normative language

`MUST`, `MUST NOT`, `REQUIRED`, `SHALL`, and `SHALL NOT` are mandatory requirements. `SHOULD` describes the expected implementation unless a reviewed exception is documented. `MAY` is optional.

The HLD controls domain intent. This specification controls the initial repository contract. If the two conflict, implementation stops until the documents are reconciled; code must not silently choose one interpretation.

## 3. Fixed decisions

1. The new domain hierarchy is `Portfolio -> Fund -> FundOrder -> FundOrderTrade`.
2. A Fund version has exactly one Portfolio parent.
3. Portfolio configuration and Fund mandate changes are versioned and append-only from a business perspective.
4. Existing Fund actors, contracts, tables, and UI are legacy and are not production authority for the new domain.
5. Explicit legacy Fund transaction migration is required by section 44. New admission cannot fall back to legacy balances. One financial writer per Fund is mandatory; legacy history remains separately labelled.
6. New application commands and queries use NATS actor messaging. UI and console clients do not access storage directly.
7. PostgreSQL EventSourceDb is authoritative for Portfolio and Fund aggregate history.
8. `PortfolioDbContext` exposes rebuildable ScyllaDB query projections.
9. PostgreSQL SequenceIdDb allocates positive integer PortfolioId, FundId, OrderId, and TradeId values.
10. OrderId and TradeId remain operator-facing integers and flow unchanged through every later workflow stage.
11. FundOrder and FundOrderTrade are composition records, not broker orders, fills, or live positions.
12. TradeTemplate and OrderCompositionProfile are reusable versioned definitions; FundOrder is an instantiated plan.
13. TradeSelection cannot create exact contracts, strikes, quantities, or prices.
14. OrderComposition constructs one normalization unit and cannot calculate final strategy-unit quantity, reserve risk, contact a broker, create a fill, or create a live position. RiskManagement owns final sizing; accepted references SHALL distinguish the unit-candidate hash from a sized risk-decision/result hash through explicit versioned contracts.
15. RiskManagement and OrderExecution are separate domains/workflows. This implementation may record their outcome references but cannot perform their work.
16. The existing Funds UI remains until the new Portfolio UI passes its system gates.
17. The new Trade composition UI removes Create Fund and filters Fund data through a selected Portfolio.
18. High-throughput ScyllaDB sequence-ID redesign is deferred.
19. PortfolioCode is removed. PortfolioId is the sequence-generated stable identity and Name is the display description. MessagePack key 1 remains reserved and is not reused.
20. Portfolio policy is a Portfolio-owned versioned `PortfolioFinancialPolicy`, identified by positive integer PolicyId and PolicyVersion; raw GUID and fabricated policy identities are prohibited.
21. ReferenceDb retains product discovery and legacy family compatibility; ConfigurationDb owns reusable strategies, structures, variants and deployments. Financial permission references exact catalog versions.
22. A PortfolioFinancialPolicy contains Portfolio-wide hard limits plus one versioned `TradeFamilyRiskLimit` row per configured family. Family limits may reduce but never enlarge the global limits.
23. New financial APIs use current ConfigurationDb deployment assignments. Historical family contracts remain readable but cannot implicitly authorize a new deployment or variant.
24. Portfolio Administration uses a compact command bar with Risk Policy as a primary action and no Planned Compositions action.
25. Trade Orders is the only UI for manual and Strategy Workflow compositions. The separate Portfolio composition viewer is removed.
26. GeneralLedger and CapacityReservation are Portfolio-owned subdomains. Journal posting, spendable balances and holds share a transactional PostgreSQL financial fence.
27. Financial Function completion requires business tables, receipt and completed event to commit in one transaction. Scylla/reporting delivery may lag and cannot authorize spending.
28. Holds are not cash expenses. Unknown commit outcomes require original-identity reconciliation; neither timeout nor a missing projection proves rollback.

## 4. Scope

### 4.1 Included

- Portfolio identity, lifecycle, versions, policy references, and operating state;
- PortfolioFinancialPolicy identities, immutable versions, lifecycle, global limits, and per-family limits;
- existing ReferenceDb TradeStrategyFamily compatibility catalog and idempotent legacy-seed bootstrap;
- Fund identity, Portfolio membership, mandate versions, and operating state;
- Portfolio-to-Fund allocation and FundRiskEnvelope versions;
- TradeTemplate, TradeSelectionHintProfile, and OrderCompositionProfile assignments;
- active Fund resolution by Portfolio, trading year, and decision horizon;
- frozen Portfolio/Fund strategy snapshots;
- FundOrder and FundOrderTrade identity reservation and state;
- integer ID allocation and idempotent reservation;
- recording accepted TradeSelection and OrderComposition result references;
- typed NATS commands, events, queries, and APIs;
- PostgreSQL event-source integration and ScyllaDB Portfolio projections;
- compact Portfolio/Risk Policy and unified Trade Orders UI contracts;
- legacy isolation;
- General Ledger accounting rules, immutable journals, authoritative balances, periods and reconciliation;
- atomic capacity reservation and lifecycle, including coordination with withdrawals and policy changes;
- legacy transaction/table/producer migration, scoped financial UI and future accounting export contracts;
- observability, authorization points, tests, and implementation gates.

### 4.2 Excluded

- broker submission, acknowledgement, replace, cancel, or reconciliation;
- broker order IDs;
- broker-side fill acquisition (financial ingestion/reconciliation of authenticated fill facts is included);
- live TradeDb order, trade, or position creation;
- market-feed subscription implementation (qualified valuation input and its accounting treatment are included);
- RiskManagement calculation details;
- OrderComposition algorithms for futures, verticals, or Iron Condors;
- deletion of legacy tables or UI;
- non-ES initial template definitions beyond extensibility contracts;
- high-throughput tick-table key or sequence redesign;
- new ConfigurationDb strategy-catalog management UI (existing Reference family management is documented separately);
- implementation of the new reusable strategy/structure/variant catalog; its accepted design includes independent side, bias and credit/debit choices;
- scheduled future policy activation; and
- a generic policy formula, script, or conditional-rule engine.

## 5. Required solution topology

Sections 37–46 add `GeneralLedger` and `CapacityReservation` under Domain.Portfolio, neutral financial contracts, PostgreSQL financial storage contexts and Portfolio Scylla projections. Existing configuration actor/storage boundaries below are retained.

Implementation SHALL use the following project boundaries:

```text
TomasAI.IFM.Domain.Portfolio/
  Command/Actor/
  Command/EventProjector/
  Command/State/
  Command/Model/
  Command/Validation/
  Query/Actor/
  Query/Api/
  Docs/

TomasAI.IFM.Domain.Portfolio.Shared/
  Commands/
  Events/
  Queries/
  ServiceApi/
  ViewModels/
  Validation/
  Identities/

TomasAI.IFM.Domain.Portfolio.UnitTests/
TomasAI.IFM.Domain.Portfolio.BDDTests/
TomasAI.IFM.Domain.Portfolio.IntegrationTests/
TomasAI.IFM.Domain.Portfolio.VerificationTests/
```

`TomasAI.IFM.Application.Storage` SHALL contain:

```text
PortfolioDb/
  IPortfolioDbReadContext.cs
  IPortfolioDbWriteContext.cs
  IPortfolioDbContext.cs
  PortfolioDbContext.cs
  PortfolioDbCql.cs
  PortfolioDbParameters.cs
  Schema/

FundLegacyDb/
  FundLegacyDbContext.cs
  read-only legacy interfaces as required

ReferenceDb/
  TradeStrategyFamily CQL/schema, idempotent bootstrap, and typed read context
```

`TomasAI.IFM.Domain.Reference.Shared` SHALL own the TradeStrategyFamily DTO/query API. `TomasAI.IFM.UI.Net.Views.Portfolio` SHALL own the compact Portfolio command bar and Risk Policy modal. `TomasAI.IFM.UI.Net.Views.Trade` SHALL retain and refactor the existing Trade Orders screen rather than adding another composition viewer.

The legacy project and namespaces MUST NOT be renamed mechanically into the new domain.

## 6. Domain ownership

### 6.1 Portfolio aggregate

The Portfolio aggregate is the write authority for:

- Portfolio versions and operating state;
- Portfolio policy references;
- broker-account references without credentials;
- Fund membership;
- Fund allocation versions;
- FundRiskEnvelope delegation; and
- Portfolio retirement.
- audited deletion of a never-activated Draft Portfolio and its operational draft projections.

The Portfolio aggregate does not own exact trade composition or broker execution.

### 6.2 PortfolioFund aggregate

The PortfolioFund aggregate is the write authority for:

- Fund mandate versions and operating state;
- template and profile assignments;
- FundOrder/FundOrderTrade composition identity reservation;
- accepted TradeSelection reference binding;
- composition status and accepted OrderComposition result reference; and
- cancellation or expiry before execution.

The actor route includes both PortfolioId and FundId. The legacy `FundCommandActor` MUST NOT process new PortfolioFund commands.

### 6.3 Strategy Workflow

The Strategy Workflow owns stage sequencing and accepted stage-result history. Portfolio projections may retain result IDs and hashes for navigation, but MUST NOT become a second mutable authority for TradeSelection or OrderComposition results.

### 6.4 PortfolioFinancialPolicy aggregate

The PortfolioFinancialPolicy aggregate owns immutable policy versions, global capital/risk hard limits, TradeFamilyRiskLimit rows, effective interval, activation/supersession/retirement, and deletion eligibility. It does not own actual Fund allocation, observed utilization, broker balances, execution, or strategy-family definitions.

### 6.5 TradeStrategyFamily compatibility catalog and future strategy authority

ConfigurationDb will own reusable strategy, structure, variant and deployment definitions. Portfolio-owned assignments must reference exact deployment versions and permitted subsets; the catalog cannot authorize a Fund. Mapping old family/template references and per-family risk limits requires explicit versioned contracts, preserving existing integer IDs, snapshots and hashes. Do not equate a strategy-family grouping with instrument class or reuse a new catalog UUID as an existing risk-limit key.


ReferenceDb owns immutable TradeStrategyFamily definitions. Bootstrap preserves the three legacy seeds; the command API now creates product-linked definitions in an additive catalog. Portfolio policy, Fund mandate, template, TradeSelection, OrderComposition, and RiskManagement contracts reference exact TradeStrategyFamilyId/DefinitionVersion values and MUST NOT infer family behavior from display text.

The [2026-09-05 catalog implementation amendment](../../TomasAI.IFM.Domain.Reference/Docs/Trade-Strategy-Symbol-Catalog-Implementation.md) supersedes earlier read-only/three-row restrictions in this specification. SystemKey is non-unique classification; the exact reference is ID/version. Existing seeds and historical records are preserved, not bulk-rewritten. Creation does not enable a trading strategy for any Fund.

## 7. Identity contracts

All identity records are MessagePack objects with stable numeric keys, parameterless serializer constructors, positive-value validation, and dot-separated `Format()` results.

All new low-volume business entities that require an integer ID MUST obtain it from `ISequenceIdGenerator` through the owning actor/service and a registered named PostgreSQL sequence. No UI, console, API, import, or other operator-facing creation path may request or accept a hand-entered integer ID. Creation allocates before submission, displays the result read-only when useful, and fails closed when allocation is unavailable. Versioning preserves the existing identity. Row-count, maximum-plus-one, random-number, timestamp-derived, and client-local integer allocation are prohibited.

### 7.1 `PortfolioId`

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioId([property: Key(0)] int Id) : IActorEntityId;
```

- `Id` MUST be greater than zero.
- `Format()` returns `PortfolioId`, for example `101`.

### 7.2 `PortfolioFundId`

| Key | Field | Type |
| ---: | --- | --- |
| 0 | `PortfolioId` | `int` |
| 1 | `FundId` | `int` |

`Format()` returns `PortfolioId.FundId`, for example `101.205`.

### 7.3 `PortfolioFundOrderId`

| Key | Field | Type |
| ---: | --- | --- |
| 0 | `PortfolioId` | `int` |
| 1 | `FundId` | `int` |
| 2 | `OrderId` | `int` |

`Format()` returns `PortfolioId.FundId.OrderId`.

### 7.4 `PortfolioFundOrderTradeId`

| Key | Field | Type |
| ---: | --- | --- |
| 0 | `PortfolioId` | `int` |
| 1 | `FundId` | `int` |
| 2 | `OrderId` | `int` |
| 3 | `TradeId` | `int` |

`Format()` returns `PortfolioId.FundId.OrderId.TradeId`.

### 7.5 `PortfolioFinancialPolicyId`

| Key | Field | Type |
| ---: | --- | --- |
| 0 | `PortfolioId` | `int` |
| 1 | `PolicyId` | `int` |

Both values MUST be positive. `Format()` returns `PortfolioId.PolicyId`.

### 7.6 `TradeStrategyFamilyId`

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record TradeStrategyFamilyId([property: Key(0)] int Id) : IActorEntityId;
```

The ID is sequence generated and never entered by an operator. A separate positive DefinitionVersion freezes the referenced catalog definition.

### 7.7 Technical identities

The following remain GUID/UUID identities and MUST NOT replace integer business IDs:

- CommandId;
- EventId;
- WorkflowId;
- StageInvocationId;
- CorrelationId;
- CausationId;
- idempotency key; and
- trace identity.

## 8. Sequence allocation

### 8.1 Named sequences

Implementation SHALL use:

| Business identity | `SequenceName` |
| --- | --- |
| PortfolioId | `Portfolio_PortfolioId` - new |
| PolicyId | `PortfolioPolicy_PolicyId` - new |
| TradeStrategyFamilyId | `Reference_TradeStrategyFamilyId` - new |
| FundId | `Fund_FundId` - existing |
| OrderId | `Trade_OrderId` - existing |
| TradeId | `Trade_TradeId` - existing |

All new sequence names MUST be added to `SequenceName`, `ToStringFast`, PostgreSQL schema initialization, cutover documentation, and integration tests before their create/bootstrap path is enabled.

### 8.2 Allocation requirements

- Allocation uses `ISequenceIdGenerator.GetSequenceIdAsync`.
- The returned `long` MUST be checked before conversion to `int`.
- Zero, negative, overflowed, or wrapped values MUST fail before command submission.
- Gaps are valid.
- IDs are never reused.
- A client MUST retain the allocated value; it cannot query the high watermark as its ID.
- OrderId/TradeId reservation occurs inside one idempotent application operation owned by the PortfolioFund command path.
- TradeStrategyFamily bootstrap resolves by stable system key before allocating, so restart or concurrent bootstrap cannot duplicate or renumber a seeded family.

### 8.3 Reservation failure

If ID allocation succeeds but event commit fails, the IDs remain consumed and unused. A retry with an idempotency key that was not committed may allocate new IDs. A retry after a committed reservation MUST return the committed original IDs.

## 9. Enum contracts

Enum numeric values are explicit and append-only.

### 9.1 `PortfolioOperatingState`

| Value | Name | Meaning |
| ---: | --- | --- |
| 0 | Unknown | Invalid/uninitialized |
| 1 | Draft | Not eligible for workflows |
| 2 | Active | New workflows permitted subject to policy |
| 3 | Paused | New exposure blocked |
| 4 | ReduceOnly | Only risk-reducing activity permitted later |
| 5 | Disabled | Operationally disabled |
| 6 | Retired | Permanently inactive for new workflows |

### 9.2 `FundOperatingState`

| Value | Name |
| ---: | --- |
| 0 | Unknown |
| 1 | Draft |
| 2 | Active |
| 3 | Paused |
| 4 | Disabled |
| 5 | Retired |

### 9.3 `FundCapacityState`

| Value | Name |
| ---: | --- |
| 0 | Unknown |
| 1 | Available |
| 2 | Constrained |
| 3 | Blocked |
| 4 | ReduceOnly |

### 9.4 `FundCompositionState`

| Value | Name | Terminal in this phase |
| ---: | --- | --- |
| 0 | Unknown | No |
| 1 | Draft | No |
| 2 | IdentityReserved | No |
| 3 | TemplateSelected | No |
| 4 | Composing | No |
| 5 | Composed | No |
| 6 | CompositionFailed | Yes |
| 7 | RiskPending | No |
| 8 | RiskRejected | Yes |
| 9 | RiskApproved | Yes for this implementation boundary |
| 10 | Cancelled | Yes |
| 11 | Expired | Yes |
| 12 | ExecutionRequested | Reserved, not implemented |
| 13 | Executing | Reserved, not implemented |
| 14 | Executed | Reserved, not implemented |
| 15 | ExecutionFailed | Reserved, not implemented |

Reserved execution values MUST NOT be emitted by this implementation.

### 9.5 `CompositionOrigin`

| Value | Name |
| ---: | --- |
| 0 | Unknown |
| 1 | StrategyWorkflow |
| 2 | ManualUi |
| 3 | ApprovedImport |

Manual origin does not bypass selection, composition, or risk validation.

A `CreateManualFundOrder` command SHALL accept the selected Portfolio/Fund identities and versions, underlying/date/reference values, UTC currentness window, and an idempotency key. The Portfolio/Fund actor SHALL reject stale or inactive Portfolio/Fund scope, allocate OrderId from `Trade_OrderId`, and commit a canonical `Draft` with `Origin=ManualUi`. The initial Draft SHALL contain no fabricated TradeSelection/template/profile references and no trade rows. It SHALL expose no execution, broker, fill, live-feed, End-of-Day, or position side effect. Retry with the same key and canonical payload SHALL return the original OrderId; changed-payload reuse SHALL fail.

Trade Orders SHALL load Funds and orders solely through typed Portfolio queries. Every Portfolio or Fund scope change SHALL invalidate earlier in-flight loads by generation; a response may update visible state only when its captured generation and Portfolio/Fund identities still match the current selection.

### 9.6 `PortfolioFinancialPolicyState`

| Value | Name |
| ---: | --- |
| 0 | Unknown |
| 1 | Draft |
| 2 | Active |
| 3 | Superseded |
| 4 | Retired |

Deleted is an authoritative tombstone outcome, not a reusable active read-model state.

### 9.7 `TradeStrategyFamilyState`

| Value | Name |
| ---: | --- |
| 0 | Unknown |
| 1 | Draft |
| 2 | Active |
| 3 | Retired |

V1 bootstrap creates only Active definitions and exposes no mutation command.

## 10. Core read models

All read models SHALL:

- use MessagePack with explicit append-only keys;
- contain `SchemaVersion`;
- use UTC timestamps;
- avoid mutable child collections in public APIs;
- provide intrinsic identity and basic validity checks;
- preserve version and provenance; and
- exclude secrets and broker credentials.

### 10.1 `PortfolioReadModel`

| Key | Field | Requirement |
| ---: | --- | --- |
| 0 | `PortfolioId` | Positive integer |
| 1 | Reserved | Former PortfolioCode key; MUST NOT be reused |
| 2 | `Name` | Required display name |
| 3 | `PortfolioVersion` | Positive `long` |
| 4 | `SchemaVersion` | Revised contract value 2 |
| 5 | `BaseCurrency` | Required; initially USD |
| 6 | `OperatingState` | Explicit enum |
| 7 | `EffectiveFromUtc` | Required UTC |
| 8 | `EffectiveUntilUtc` | Optional UTC after start |
| 9 | `ActivePolicyId` | Positive integer when assigned; required when Active |
| 10 | `ActivePolicyVersion` | Positive when assigned; required when Active |
| 11 | `BrokerAccountRefs` | Immutable array of references, no credentials |
| 12 | `CreatedOnUtc` | Required UTC |
| 13 | `CreatedBy` | Required principal |
| 14 | `SupersededOnUtc` | Optional UTC |
| 15 | `SupersededBy` | Optional principal |

### 10.2 `FundMandateReadModel`

| Key | Field | Requirement |
| ---: | --- | --- |
| 0 | `PortfolioId` | Positive integer |
| 1 | `FundId` | Positive integer |
| 2 | `FundCode` | Required stable code |
| 3 | `Name` | Required display name |
| 4 | `FundMandateVersion` | Positive `long` |
| 5 | `SchemaVersion` | Initial value 1 |
| 6 | `TradingYear` | Four-digit supported year |
| 7 | `OperatingState` | Explicit enum |
| 8 | `EffectiveFromUtc` | Required UTC |
| 9 | `EffectiveUntilUtc` | Optional UTC after start |
| 10 | `DecisionHorizon` | Required supported horizon |
| 11 | `Objective` | Required named objective |
| 12 | `UnderlyingUniverse` | Non-empty immutable array |
| 13 | `EligibleAssetTypes` | Non-empty immutable array |
| 14 | `PermittedDirections` | Immutable array |
| 15 | `PermittedConditions` | Immutable array |
| 16 | `PermittedTradeFamilies` | Permission classification codes; schema-v3 exact Deployment GUID/version references are stored in `PermittedTradeStrategyFamilies`. Both arrays may be empty for an unassigned Draft, Disabled or Retired Fund; operational Funds require permissions. |
| 17 | `CreatedOnUtc` | Required UTC |
| 18 | `CreatedBy` | Required principal |

### 10.3 `FundAllocationReadModel`

Required fields are PortfolioId/version, FundId/mandate version, allocation version, target weight, minimum/maximum weight, resolved allocated capital, currency, effective interval, source policy version, and audit provenance.

Weights are provenance and policy inputs. They MUST NOT be treated as quantities or contract counts.

### 10.4 `FundRiskEnvelopeReadModel`

Required fields are:

- PortfolioId and PortfolioVersion;
- FundId and FundMandateVersion;
- envelope ID and positive version;
- capacity state;
- currency;
- allocated and available capital;
- maximum risk per trade and aggregate risk;
- maximum margin and gross notional;
- maximum contracts and open positions;
- optional delta, gamma, vega, and drawdown limits;
- remaining loss budget;
- effective and expiry timestamps;
- source Portfolio policy identity/version; and
- audit provenance.

TradeSelection uses only eligibility/capacity facts. RiskManagement owns financial evaluation.

### 10.5 `FundTradeTemplateAssignmentReadModel`

Required fields are Portfolio/Fund identities and versions, assignment version, TradeTemplate ID/version, enabled state, horizon, underlying universe, asset type, exact TradeStrategyFamilyId/DefinitionVersion, priority, effective interval, TradeSelectionHintProfile ID/version, OrderCompositionProfile ID/version, and audit provenance.

### 10.6 `FundOrderReadModel`

This is the new Portfolio-domain model and MUST NOT reuse the legacy serialized contract.

| Key | Field |
| ---: | --- |
| 0 | `PortfolioId` |
| 1 | `PortfolioVersion` |
| 2 | `FundId` |
| 3 | `FundMandateVersion` |
| 4 | `OrderId` |
| 5 | `FundOrderVersion` |
| 6 | `SchemaVersion` |
| 7 | `Origin` |
| 8 | `State` |
| 9 | `WorkflowId` |
| 10 | `TradeSelectionInvocationId` |
| 11 | `TradeSelectionResultId` |
| 12 | `TradeSelectionResultSha256` |
| 13 | `TradeTemplateId` |
| 14 | `TradeTemplateVersion` |
| 15 | `OrderCompositionProfileId` |
| 16 | `OrderCompositionProfileVersion` |
| 17 | `UnderlyingRoot` |
| 18 | `DecisionHorizon` |
| 19 | `RequestedTradeDate` |
| 20 | `RequestedMaturityDate` |
| 21 | `Reference` |
| 22 | `IdempotencyKey` |
| 23 | `OrderCompositionResultId` |
| 24 | `OrderCompositionResultSha256` |
| 25 | `RiskManagementResultId` |
| 26 | `RiskManagementResultSha256` |
| 27 | `CreatedOnUtc` |
| 28 | `CreatedBy` |
| 29 | `UpdatedOnUtc` |
| 30 | `UpdatedBy` |

Exact candidate legs and prices do not belong in FundOrder. The accepted OrderComposition result is referenced by immutable ID and hash.

### 10.7 `FundOrderTradeReadModel`

| Key | Field |
| ---: | --- |
| 0 | `PortfolioId` |
| 1 | `FundId` |
| 2 | `OrderId` |
| 3 | `TradeId` |
| 4 | `FundOrderTradeVersion` |
| 5 | `SchemaVersion` |
| 6 | `TradeRole` |
| 7 | `TradeStrategyFamilyId` |
| 8 | `DirectionOrBias` |
| 9 | `TradeAction` |
| 10 | `IsPrimaryTrade` |
| 11 | `UnderlyingRoot` |
| 12 | `RequestedTradeDate` |
| 13 | `RequestedMaturityDate` |
| 14 | `Reference` |
| 15 | `CreatedOnUtc` |
| 16 | `CreatedBy` |
| 17 | `TradeStrategyFamilyDefinitionVersion` |

`TradeRole` initially supports Primary. Opening, Closing, Hedge, Roll, and Adjustment values MAY be defined now but are not required by the initial three-template catalog.

### 10.8 `PortfolioFundStrategySnapshot`

The snapshot is immutable and self-contained. It includes:

- workflow identity/revision and trace context;
- PortfolioReadModel identity/version subset;
- exact PortfolioFinancialPolicyReadModel identity/version and complete frozen global/family limits;
- FundMandateReadModel;
- current FundAllocation reference;
- current FundRiskEnvelope;
- enabled template assignments;
- parameter/profile identities and versions;
- resolved-at and valid-until timestamps; and
- canonical payload SHA-256.

Stages MUST validate the supplied snapshot and MUST NOT query for a newer version during the same workflow.

### 10.9 `TradeStrategyFamilyReadModel`

| Key | Field | Requirement |
| ---: | --- | --- |
| 0 | `TradeStrategyFamilyId` | Positive sequence-generated integer |
| 1 | `DefinitionVersion` | Positive immutable version |
| 2 | `SystemKey` | Exact `Family-Strategy` enum-name composition |
| 3 | `Family` | `TradeStrategyFamilyType`, defined and not Unknown |
| 4 | `Strategy` | `TradeStrategyType`, defined and not Unknown |
| 5 | `TimeFrame` | `TimeFrameType`: Daily, Weekly or Monthly |
| 6 | `Symbol` | Required trimmed underlying symbol; initial ES |
| 7 | `Currency` | Three uppercase letters; initial USD |
| 8 | `Description` | Required operator-facing description |
| 9 | `State` | Initial value Active |
| 10 | `CreatedOnUtc` | Required UTC |
| 11 | `CreatedBy` | Required server audit principal |
| 12 | `TradeStrategySymbolId` | Positive provider-product catalog ID for new definitions; zero only on preserved legacy seeds |
| 13 | `Exchange` | Required provider-derived Exchange for new linked definitions |

The preserved seed catalog contains `Futures-Futures` (Daily), `FuturesOption-VerticalSpread` (Weekly), and `FuturesOption-IronCondor` (Monthly), all ES/USD at DefinitionVersion 1. New product-linked definitions may share these SystemKeys and have their own exact ID/version. Long/Short and directional/credit variants are not separate families. The complete enum definitions, migration and deployment constraints are in [the typed catalog definition](../../TomasAI.IFM.Domain.Reference/Docs/Trade-Strategy-Family-Typed-Definition.md).

Fund mandate and template-assignment horizon dropdowns SHALL offer only the names Daily, Weekly and Monthly, mapped to their existing `TimeFrameType` values. Unsupported stored horizons require an explicit selection, not an automatic conversion to Daily. The existing Fund/assignment persistence contracts retain the selected enum name as a string.

Implemented UI wiring: Fund permitted families use an active-catalog multi-select checklist; Trade Assignment uses a non-editable dropdown limited by exact permitted ID/version. Labels include product/timeframe and identity. Updated editors persist SchemaVersion 2 with Fund key 21 PermittedTradeStrategyFamilies and assignment key 22 TradeStrategyFamily; existing strings are classification mirrors. Server validation checks active exact references and assignment membership. Legacy ambiguous names require explicit replacement; catalog errors block editing. Typed mandates cannot downgrade to name-only permissions.

### 10.10 `TradeFamilyRiskLimitReadModel`

| Key | Field | Requirement |
| ---: | --- | --- |
| 0 | `TradeStrategyFamilyId` | Positive exact reference |
| 1 | `DefinitionVersion` | Positive exact reference |
| 2 | `SystemKeySnapshot` | Frozen reference display/provenance |
| 3 | `DisplayNameSnapshot` | Frozen operator display value |
| 4 | `Enabled` | Explicit Boolean permission |
| 5 | `MaximumRiskPerTrade` | Non-negative decimal base-currency amount |
| 6 | `MaximumAggregateRisk` | Non-negative decimal base-currency amount |
| 7 | `MaximumMargin` | Non-negative decimal base-currency amount |
| 8 | `MaximumGrossNotional` | Non-negative decimal base-currency amount |
| 9 | `MaximumOpenPositions` | Non-negative integer |

### 10.11 `PortfolioFinancialPolicyReadModel`

| Key | Field | Requirement |
| ---: | --- | --- |
| 0 | `PortfolioId` | Positive owner identity |
| 1 | `PolicyId` | Positive sequence-generated identity |
| 2 | `PolicyVersion` | Positive immutable business version |
| 3 | `SchemaVersion` | Initial value 1 |
| 4 | `Name` | Required display name |
| 5 | `State` | Explicit PortfolioFinancialPolicyState |
| 6 | `BaseCurrency` | Must equal Portfolio base currency |
| 7 | `CapitalBase` | Positive decimal before activation |
| 8 | `ProtectedReserve` | Non-negative decimal |
| 9 | `MaximumDeployableCapital` | Non-negative decimal |
| 10 | `MaximumRiskPerTrade` | Non-negative global hard cap |
| 11 | `MaximumAggregateRisk` | Non-negative global hard cap |
| 12 | `MaximumMargin` | Non-negative global hard cap |
| 13 | `MaximumGrossNotional` | Non-negative global hard cap |
| 14 | `MaximumOpenPositions` | Positive integer before activation |
| 15 | `MaximumDrawdownAmount` | Non-negative decimal hard cap |
| 16 | `TradeFamilyLimits` | Immutable non-empty family-limit array |
| 17 | `EffectiveFromUtc` | Required UTC |
| 18 | `EffectiveUntilUtc` | Optional UTC after start |
| 19 | `CreatedOnUtc` | Required UTC |
| 20 | `CreatedBy` | Required principal |
| 21 | `SupersededOnUtc` | Optional UTC |
| 22 | `SupersededBy` | Optional principal |

All monetary values are decimal amounts in BaseCurrency. Zero is a blocking limit, never an unlimited sentinel. Every enabled family row MUST be unique, reference an Active catalog definition, be complete, and be less than or equal to corresponding global hard limits. Per-family caps are shared ceilings, not reserved allocations, and do not need to sum to Portfolio capital.

## 11. Version rules

- Business versions are positive `long` values scoped to the aggregate/configuration identity.
- SchemaVersion is independent of business version.
- New business content creates a new version; existing historical rows/events are not edited in place.
- Effective intervals for simultaneously active versions MUST NOT overlap where uniqueness is required.
- The command actor checks expected version before emitting an event.
- A stale expected version fails with `VersionConflict` and does not mutate state.
- A workflow retains the exact versions accepted at start.
- Saved PortfolioFinancialPolicy versions, including Draft versions, are immutable; correction uses the next PolicyVersion.
- Existing Portfolio/Fund/policy snapshots never resolve a later TradeStrategyFamily definition or newly added family implicitly.

## 12. Actor topology

### 12.1 `PortfolioCommandActor`

- Actor constant: `PortfolioCommand`.
- Entity: `PortfolioId`.
- Event-sourced, one mailbox serialization boundary per Portfolio.
- Owns Portfolio lifecycle, Fund membership, allocation, and FundRiskEnvelope delegation.

### 12.2 `PortfolioFundCommandActor`

- Actor constant: `PortfolioFundCommand`.
- Entity: `PortfolioFundId`.
- Event-sourced, one mailbox serialization boundary per Portfolio/Fund.
- Owns mandate, assignments, and Fund composition records.

### 12.3 `PortfolioQueryActor`

- Actor constant: `PortfolioQuery`.
- Side-effect free.
- Reads only `IPortfolioDbReadContext` projections.
- Uses bounded pages/streams for lists.

### 12.4 EventProjectors

Portfolio and PortfolioFund command actors SHALL register durable projector descriptors. Projectors consume committed events, perform idempotent Scylla mutations, advance fenced PostgreSQL checkpoints, and publish terminal completion/failure according to existing repository conventions.

No separate public Event actor is required merely to denormalize state.

### 12.5 `PortfolioFinancialPolicyCommandActor`

- Actor constant: `PortfolioFinancialPolicyCommand`.
- Entity: `PortfolioFinancialPolicyId`.
- Event-sourced mailbox boundary per PortfolioId/PolicyId.
- Owns create-version, activate, supersede, retire, and Draft-deletion decisions.

### 12.6 `PortfolioFinancialPolicyQueryActor`

- Actor constant: `PortfolioFinancialPolicyQuery`.
- Side-effect free and projection backed.
- Supports exact policy/version, policies by Portfolio/state, and current assigned policy queries.

### 12.7 Reference TradeStrategyFamily query surface

The existing Reference query actor/API exposes exact point and bounded list queries. V1 registers no public TradeStrategyFamily command verb. Seed/bootstrap storage writes are infrastructure initialization, not UI/application commands.

## 13. Command envelope

Every command implements `ICommand<TEntityId>` and follows the repository MessagePack convention:

| Key | Base field |
| ---: | --- |
| 0 | `CommandId` |
| 1 | `Subject` |
| 2 | `PostEvents` |
| 3 | `EntityId` |
| 4 | `ErrorCode` |
| 5 | `RouteTo` |

Command-specific payload begins at key 6. Existing keys are never renumbered or reused. Derived names, stream IDs, origin timestamps, and origin principals are ignored by MessagePack unless the established command convention later changes globally.

The Portfolio domain reserves error-code family `34000-34299`, subject to the repository-wide error-code audit gate.

## 14. Portfolio commands

| Command / Verb | Entity | Payload beginning at key 6 | Required outcome |
| --- | --- | --- | --- |
| `CreatePortfolioCommand` / `CreatePortfolio` | PortfolioId | Initial PortfolioReadModel, idempotency key | Create version 1 |
| `AddPortfolioVersionCommand` / `AddPortfolioVersion` | PortfolioId | Expected version, replacement definition | Append next version |
| `ChangePortfolioOperatingStateCommand` / `ChangePortfolioOperatingState` | PortfolioId | Expected version, new state, reason | Append state change |
| `AddFundToPortfolioCommand` / `AddFundToPortfolio` | PortfolioId | Expected Portfolio version, Fund identity, initial mandate reference | Add membership |
| `DelegateFundAllocationCommand` / `DelegateFundAllocation` | PortfolioId | Expected version, complete allocation | Append allocation delegation |
| `DelegateFundRiskEnvelopeCommand` / `DelegateFundRiskEnvelope` | PortfolioId | Expected version, complete envelope | Append delegation |
| `RetirePortfolioCommand` / `RetirePortfolio` | PortfolioId | Expected version, reason | Retire for new workflows |
| `DeleteDraftPortfolioCommand` / `DeleteDraftPortfolio` | PortfolioId | Expected aggregate revision, reason | Delete a never-activated Draft from operational projections while retaining its tombstone history |

CreatePortfolio callers MAY preallocate PortfolioId through a typed sequence query/service. The command MUST reject an existing PortfolioId with different content and treat an identical idempotent replay as success.

### 14.1 PortfolioFinancialPolicy commands

| Command / Verb | Entity | Required outcome |
| --- | --- | --- |
| `CreatePortfolioFinancialPolicyCommand` / `CreatePortfolioFinancialPolicy` | PortfolioFinancialPolicyId | Commit immutable Draft version 1 |
| `AddPortfolioFinancialPolicyVersionCommand` / `AddPortfolioFinancialPolicyVersion` | PortfolioFinancialPolicyId | Append the next immutable Draft version |
| `ActivateAndAssignPortfolioFinancialPolicyCommand` / `ActivateAndAssignPortfolioFinancialPolicy` | PortfolioFinancialPolicyId | Validate, activate, supersede prior policy when applicable, and commit exact Portfolio reference as one logical idempotent transition |
| `RetirePortfolioFinancialPolicyCommand` / `RetirePortfolioFinancialPolicy` | PortfolioFinancialPolicyId | Retire eligible Active policy with reason |
| `DeleteDraftPortfolioFinancialPolicyCommand` / `DeleteDraftPortfolioFinancialPolicy` | PortfolioFinancialPolicyId | Tombstone a never-active, unreferenced Draft policy identity |

Policy creation and AddVersion commands carry the complete PortfolioFinancialPolicyReadModel and expected revision where applicable. ActivateAndAssign carries selected Portfolio and policy expected revisions, exact PolicyVersion, effective-as-of time, and idempotency key. It MUST preserve the prior assignment on any validation, persistence, projection, or concurrency failure and MUST return the original result on an identical committed retry.

Allocation of PolicyId occurs through a typed identity request before creation. Cancelling afterward consumes the allocated ID without creating a policy. No command accepts a client-selected or fallback ID.

## 15. PortfolioFund commands

| Command / Verb | Payload beginning at key 6 | Required outcome |
| --- | --- | --- |
| `CreateFundMandateCommand` / `CreateFundMandate` | Initial FundMandateReadModel, idempotency key | Create mandate version 1 |
| `AddFundMandateVersionCommand` / `AddFundMandateVersion` | Expected version, replacement mandate | Append next mandate version |
| `ChangeFundOperatingStateCommand` / `ChangeFundOperatingState` | Expected version, state, reason | Append state change |
| `AssignTradeTemplateCommand` / `AssignTradeTemplate` | Expected mandate version, complete assignment | Append assignment |
| `AssignTradeSelectionHintProfileCommand` / `AssignTradeSelectionHintProfile` | Expected mandate version, profile ID/version | Append assignment change |
| `AssignOrderCompositionProfileCommand` / `AssignOrderCompositionProfile` | Expected mandate version, profile ID/version | Append assignment change |
| `ReserveFundOrderCompositionCommand` / `ReserveFundOrderComposition` | Reservation request | Commit OrderId and TradeId identities |
| `MarkFundOrderComposingCommand` / `MarkFundOrderComposing` | Expected FundOrder version, OrderComposition invocation | Move to Composing |
| `RecordFundOrderComposedCommand` / `RecordFundOrderComposed` | Expected version, accepted result ID/hash, evaluated/expiry times | Move to Composed/RiskPending |
| `RecordFundOrderCompositionFailedCommand` / `RecordFundOrderCompositionFailed` | Expected version, stable failure reference/reason | Move to CompositionFailed |
| `RecordFundOrderRiskOutcomeCommand` / `RecordFundOrderRiskOutcome` | Expected version, accepted Risk result ID/hash and outcome | Move to RiskApproved/Rejected |
| `CancelFundOrderCompositionCommand` / `CancelFundOrderComposition` | Expected version, reason | Move to Cancelled if allowed |
| `ExpireFundOrderCompositionCommand` / `ExpireFundOrderComposition` | Expected version, reason/time | Move to Expired if allowed |

### 15.1 Reservation request

`ReserveFundOrderCompositionRequest` contains:

| Key | Field |
| ---: | --- |
| 0 | `WorkflowId` |
| 1 | `WorkflowRevision` |
| 2 | `TradeSelectionInvocationId` |
| 3 | `TradeSelectionResultId` |
| 4 | `TradeSelectionResultSha256` |
| 5 | `PortfolioId` |
| 6 | `PortfolioVersion` |
| 7 | `FundId` |
| 8 | `FundMandateVersion` |
| 9 | `TradeTemplateId` |
| 10 | `TradeTemplateVersion` |
| 11 | `OrderCompositionProfileId` |
| 12 | `OrderCompositionProfileVersion` |
| 13 | `UnderlyingRoot` |
| 14 | `DecisionHorizon` |
| 15 | `RequestedTradeDate` |
| 16 | `RequestedMaturityDate` |
| 17 | `TradeInstructions` |
| 18 | `Origin` |
| 19 | `IdempotencyKey` |
| 20 | `RequestedAtUtc` |
| 21 | `ExpiresAtUtc` |

The initial automated workflow requests exactly one Primary TradeInstruction. The collection exists so later related instructions do not require replacing the contract.

Every TradeInstruction carries the exact TradeStrategyFamilyId/DefinitionVersion accepted from the selected template and frozen policy. A display name or legacy TradeType string cannot substitute for that identity.

### 15.2 Reservation response

The command returns a typed `FundCompositionReservationResult` containing the committed FundOrderReadModel, immutable FundOrderTradeReadModels, aggregate version, committed timestamp, and idempotency disposition.

The response MUST NOT return until the authoritative event commit succeeds. Projection completion may follow according to existing command completion conventions.

## 16. Events

Events use the existing event-source base keys and append payload fields without reuse. Required event concepts are:

### 16.1 Portfolio events

- `PortfolioCreatedEvent`;
- `PortfolioVersionAddedEvent`;
- `PortfolioOperatingStateChangedEvent`;
- `FundAddedToPortfolioEvent`;
- `FundRiskEnvelopeDelegatedEvent`; and
- `PortfolioRetiredEvent`.
- `DraftPortfolioDeletedEvent`.

### 16.2 PortfolioFund events

- `FundMandateCreatedEvent`;
- `FundMandateVersionAddedEvent`;
- `FundOperatingStateChangedEvent`;
- `FundTradeTemplateAssignedEvent`;
- `FundTradeSelectionHintProfileAssignedEvent`;
- `FundOrderCompositionProfileAssignedEvent`;
- `FundOrderCompositionReservedEvent`;
- `FundOrderCompositionStartedEvent`;
- `FundOrderComposedEvent`;
- `FundOrderCompositionFailedEvent`;
- `FundOrderRiskOutcomeRecordedEvent`;
- `FundOrderCompositionCancelledEvent`; and
- `FundOrderCompositionExpiredEvent`.

Every event contains the complete identity/version chain required to replay without querying current configuration. Reservation events contain allocated OrderId and TradeId values.

### 16.3 PortfolioFinancialPolicy events

- `PortfolioFinancialPolicyCreatedEvent`;
- `PortfolioFinancialPolicyVersionAddedEvent`;
- `PortfolioFinancialPolicyActivatedEvent`;
- `PortfolioFinancialPolicySupersededEvent`;
- `PortfolioFinancialPolicyRetiredEvent`;
- `PortfolioFinancialPolicyAssignedEvent`; and
- `DraftPortfolioFinancialPolicyDeletedEvent`.

Policy events contain the complete global and family limits or immutable payload needed for replay, exact TradeStrategyFamilyId/DefinitionVersion values, expected/current revisions, and audit provenance. The coordinated activation/assignment operation uses one idempotency identity and cannot expose a partially selected policy.

## 17. State transitions

### 17.1 Portfolio

Allowed transitions are:

```text
Draft -> Active
Draft -> Disabled
Active -> Paused
Active -> ReduceOnly
Active -> Disabled
Paused -> Active
Paused -> Disabled
ReduceOnly -> Active
ReduceOnly -> Paused
ReduceOnly -> Disabled
Disabled -> Active only through an explicit new version
Any non-retired state -> Retired
Retired -> no transition
```

`Draft -> Deleted` is a separate terminal deletion command, not an operating-state transition. It is allowed only while the current Portfolio state is Draft and no Fund composition history exists. The command uses optimistic aggregate revision, requires a non-empty reason, records an authoritative deletion tombstone, removes the Portfolio and its draft-owned Fund/configuration rows from operational projections, and never releases PortfolioId, FundId, OrderId, or TradeId values for reuse. Active, Paused, ReduceOnly, Disabled, and Retired Portfolios cannot be deleted.

Activation requires valid policy, base currency, effective interval, and at least one permitted broker-account reference if the environment policy requires it.

### 17.2 Fund

Allowed transitions are Draft to Active/Disabled, Active to Paused/Disabled, Paused to Active/Disabled, Disabled to Active only through explicit new version, and any non-retired state to Retired. Retired is terminal.

Fund activation requires an active parent Portfolio version, valid mandate, and at least one enabled template assignment with matching hint/composition profiles.

### 17.3 Composition

```text
Draft -> IdentityReserved
IdentityReserved -> TemplateSelected
TemplateSelected -> Composing
Composing -> Composed
Composing -> CompositionFailed
Composed -> RiskPending
RiskPending -> RiskApproved
RiskPending -> RiskRejected
Draft/IdentityReserved/TemplateSelected/Composing/Composed/RiskPending -> Cancelled when permitted
IdentityReserved/TemplateSelected/Composing/Composed/RiskPending -> Expired
```

An implementation MAY combine IdentityReserved and TemplateSelected in one committed event because the accepted TradeSelection result already identifies the template. The projection must still expose unambiguous semantics.

### 17.4 PortfolioFinancialPolicy

```text
Create -> Draft v1
Draft/latest Active -> new immutable Draft vN
Draft vN -> Active and assigned
prior Active -> Superseded during replacement
eligible Active -> Retired
never-active unreferenced Draft identity -> Deleted tombstone
Superseded/Retired/Deleted -> no mutation
```

Activation requires current time within the effective interval, matching Portfolio ownership/base currency, complete global limits, at least one enabled Active catalog family, valid per-family caps, and expected Portfolio/policy revisions. Scheduled future activation is rejected in v1. A policy selected by the current Portfolio version cannot retire until a coordinated operation clears/replaces the reference; an Active Portfolio also requires a valid replacement or transition out of Active.

## 18. Idempotency and concurrency

This section describes original configuration/composition identity semantics. Financial postings and reservations additionally MUST satisfy section 41, including atomic business/event persistence, Portfolio-wide financial fencing and unknown-commit reconciliation. Allocating OrderId/TradeId does not reserve money.

### 18.1 Command idempotency

- CommandId protects transport retry.
- Business IdempotencyKey protects semantic retry across new CommandIds.
- The aggregate retains or can resolve a bounded durable idempotency record.
- Same key plus same canonical payload returns the prior successful result.
- Same key plus different payload fails with `IdempotencyConflict`.

### 18.2 Reservation atomicity

The reservation event MUST contain the OrderId and all TradeIds in one aggregate event. A partially committed reservation is forbidden.

### 18.3 Expected version

All non-create mutations contain ExpectedVersion. The command actor rejects stale or future versions before event creation.

### 18.4 Duplicate projection

Projectors compare event identity/revision and apply idempotent mutations. Duplicate delivery cannot append a second logical version, allocate IDs, or regress state.

## 19. Query contracts

All queries implement the established typed query contract and return `ServiceResult<T>`. Portfolio/Fund queries use `PortfolioQuery`, policy queries use `PortfolioFinancialPolicyQuery`, and trade-family catalog queries use the existing Reference actor route.

### 19.1 Point queries

- `GetPortfolioQuery(PortfolioId, Version?)`;
- `GetPortfolioFinancialPolicyQuery(PortfolioId, PolicyId, PolicyVersion?)`;
- `GetActivePortfolioFinancialPolicyQuery(PortfolioId, AsOfUtc)`;
- `GetTradeStrategyFamilyQuery(TradeStrategyFamilyId, DefinitionVersion?)`;
- `GetFundMandateQuery(PortfolioId, FundId, Version?)`;
- `GetActiveFundQuery(PortfolioId, TradingYear, DecisionHorizon, AsOfUtc)`;
- `GetFundRiskEnvelopeQuery(PortfolioId, FundId, AsOfUtc)`;
- `GetFundTemplateAssignmentsQuery(PortfolioId, FundId, FundMandateVersion)`;
- `GetFundOrderByOrderIdQuery(OrderId)`;
- `GetFundOrderTradeByTradeIdQuery(TradeId)`;
- `GetFundCompositionByWorkflowQuery(WorkflowId)`; and
- `GetPortfolioFundStrategySnapshotQuery(PortfolioId, TradingYear, DecisionHorizon, AsOfUtc)`.

### 19.2 List/page queries

- `GetPortfoliosPageQuery(OperatingState?, PageSize, PagingState?)`;
- `GetPortfolioFinancialPoliciesPageQuery(PortfolioId, State?, AsOfUtc?, PageSize, PagingState?)`;
- `GetTradeStrategyFamiliesQuery(State?, AsOfUtc?)`, bounded by the reference-catalog maximum;
- `GetFundsByPortfolioPageQuery(PortfolioId, State?, PageSize, PagingState?)`;
- `GetFundOrdersPageQuery(PortfolioId, FundId?, FromUtc, ToUtc, State?, PageSize, PagingState?)`; and
- `GetFundOrderTradesPageQuery(PortfolioId, FundId, OrderId, PageSize, PagingState?)`.

Page size MUST be bounded by configuration and server maximum. Empty result is a successful empty page. Invalid paging state is a validation failure.

### 19.3 Strategy-reference query

`GetPortfolioFundStrategyReferenceCombinationsQuery` returns non-authoritative DTO rows derived on demand from current versioned configuration. It performs no mutation and does not persist generated combinations. The existing shared CSV export service may export the typed result collection.

## 20. Active Fund resolution

`GetActiveFundQuery` applies these ordered rules:

1. Portfolio exists and requested version/as-of state is Active or otherwise explicitly permitted.
2. The exact assigned PortfolioFinancialPolicy exists, is Active/effective, and contains the referenced global and family limits.
3. Exactly one Fund mandate matches PortfolioId, TradingYear, DecisionHorizon, active effective interval, and Active state.
4. At least one enabled template assignment matches the Fund mandate, initial ES universe, and an enabled exact TradeStrategyFamily definition in the policy.
5. Referenced TradeSelection and OrderComposition profiles exist and are effective.
6. A current FundRiskEnvelope exists, is unexpired, and is not Blocked for new exposure.
7. Return one immutable `PortfolioFundStrategySnapshot` plus canonical hash.

Missing or duplicate active Fund configuration is a configuration failure, not `NoTrade`.

## 21. NATS service APIs

Shared service APIs SHALL expose task-based cancellation-aware methods and typed results:

- `IPortfolioCommandApi`;
- `IPortfolioFundCommandApi`;
- `IPortfolioQueryApi`;
- `IPortfolioFinancialPolicyCommandApi`;
- `IPortfolioFinancialPolicyQueryApi`;
- `ITradeStrategyFamilyReferenceQueryApi` (read-only in v1);
- application NATS clients implementing those interfaces; and
- UI services mapping backend results to UI operation results without discarding error codes.

Direct REST/HTTP APIs MAY be added later as adapters, but the Portfolio UI and strategy actors use NATS.

Actor subjects are created through `ActorSubject`; callers do not concatenate raw subjects. The formatted entity key must match the command/query EntityId exactly.

## 22. Persistence architecture

### 22.1 Authoritative PostgreSQL history

For GeneralLedger and CapacityReservation, the authoritative `portfolio_financial` schema MUST reside in the database configured by `EventSourceActorDbContext.EventSourceActorDbConnection`, with its completed-event append enlisted in the same connection/transaction. Section 40 defines the financial tables and required contexts. Sequence allocation may remain in the existing SequenceIdDb with valid gaps; it is not a separate cash-authority transaction.

PortfolioCommandActor and PortfolioFundCommandActor use the existing event-source repository, snapshot, stream-version, and durable projector conventions. EventSourceDb is authoritative for aggregate reconstruction.

### 22.2 ScyllaDB PortfolioDb projections

`PortfolioDbContext` is a projection/read-model context. Initial logical tables are:

| Table | Primary key/query purpose |
| --- | --- |
| `portfolio_by_id` | `((portfolioId), portfolioVersion)` descending |
| `portfolio_by_state` | `((operatingState, bucket), portfolioId)` bounded operational list |
| `portfolio_policy_by_id` | `((portfolioId, policyId), policyVersion)` descending |
| `portfolio_policy_by_portfolio` | `((portfolioId, policyState), policyId, policyVersion)` bounded list |
| `active_portfolio_policy` | `((portfolioId), effectiveFromUtc, policyId, policyVersion)` current/effective lookup |
| `fund_by_portfolio` | `((portfolioId), fundId, fundMandateVersion)` |
| `fund_by_id` | `((fundId), fundMandateVersion)` direct attribution/history |
| `active_fund_by_portfolio_horizon` | `((portfolioId, tradingYear, decisionHorizon), effectiveFromUtc, fundId)` |
| `fund_template_assignment` | `((portfolioId, fundId, fundMandateVersion), tradeTemplateId, tradeTemplateVersion)` |
| `fund_allocation` | `((portfolioId, fundId), allocationVersion)` descending |
| `fund_risk_envelope` | `((portfolioId, fundId), envelopeVersion)` descending |
| `fund_order_by_portfolio_fund_month` | `((portfolioId, fundId, orderMonth), createdOnUtc, orderId)` descending |
| `fund_order_by_order_id` | `((orderId))` direct lookup |
| `fund_order_trade_by_order_id` | `((orderId), tradeId)` |
| `fund_order_trade_by_trade_id` | `((tradeId))` direct lookup |
| `fund_composition_by_workflow` | `((workflowId), orderId)` |

`bucket` and `orderMonth` are explicit bounded partitioning values. Portfolio-wide order views require their own page projection or controlled fan-out; they cannot use `ALLOW FILTERING` or an unbounded scan.

### 22.3 Projection row requirements

Rows include schema version, aggregate version, source EventId, updated timestamp, explicit query columns, canonical typed payload, and payload hash. Older aggregate/event versions cannot overwrite newer rows.

### 22.4 Context interfaces

`IPortfolioDbReadContext` exposes only approved point and paged queries. `IPortfolioDbWriteContext` exposes projector-oriented upsert/delete methods and is not injected into UI or strategy actors.

### 22.5 Schema initialization

Portfolio schema initialization is idempotent and registered with application startup/test infrastructure. Destructive drops are test-only or separately approved administrative operations.

### 22.6 ReferenceDb TradeStrategyFamily catalog

ReferenceDb uses a query-shaped `trade_strategy_family_v3` table with a fixed catalog partition and a stable `(SystemKey, DefinitionVersion)` clustering identity. Rows contain the complete typed TradeStrategyFamilyReadModel, including the sequence-generated display/foreign-key identity. The old `trade_strategy_family_v2` table remains intact as a read-only migration source. The schema/bootstrap path:

1. creates the table idempotently;
2. queries the fixed catalog partition by stable SystemKey;
3. preserves the legacy ID/version/audit for a mapped legacy row, or allocates `Reference_TradeStrategyFamilyId` when both typed and mapped legacy keys are absent;
4. conditionally inserts exactly Futures-Futures, FuturesOption-VerticalSpread, and FuturesOption-IronCondor definition version 1 as Active using `IF NOT EXISTS`;
5. verifies duplicate keys/IDs/versions are absent; and
6. exposes no public write context or command API for family mutation in v1.

Risk Policy and pipeline consumers use typed Reference NATS queries. They MUST NOT inject `IReferenceDbReadContext` directly. A bootstrap restart or concurrent initializer is idempotent and cannot create a duplicate family or reassign an existing ID. Losing concurrent initializers may consume unused sequence values; sequence gaps are valid and IDs are never reused.

## 23. Legacy isolation

### 23.1 `FundLegacyDbContext`

The current FundDb tables are wrapped or renamed behind a legacy read context. Default production registration is read-only after the new Portfolio cutover.

### 23.2 Prohibited behavior

New Portfolio actors MUST NOT:

- read current FundDb to fill missing new data;
- write old Fund tables;
- translate new commands into legacy Fund commands;
- assume legacy FundOrder IDs are new composition IDs; or
- migrate historical rows automatically.

Section 44 defines the required explicit transaction migration. Its controlled import tools may read legacy sources, preserve source identities and write reconciled new records; normal Portfolio APIs MUST NOT use that as a runtime dual-read or dual-write adapter.

### 23.3 UI transition

Legacy Funds and Trade screens remain operational against legacy services until replacement acceptance. A single form/session cannot combine a new Portfolio mutation with a legacy Fund mutation.

## 24. TradeSelection integration

TradeSelection receives `PortfolioFundStrategySnapshot` as frozen input. It validates:

- matching workflow, Portfolio, Fund, horizon, and instrument identities;
- effective and active versions;
- enabled template assignment;
- the template's exact TradeStrategyFamilyId/DefinitionVersion is Active in the frozen policy and its family row is Enabled;
- hint-profile identity/version;
- unexpired FundRiskEnvelope capacity permission; and
- payload hashes.

TradeSelection returns `Selected` or `NoTrade`. `Selected` includes the template and OrderComposition profile references. It does not allocate OrderId/TradeId. After workflow acceptance of `Selected`, the workflow invokes ReserveFundOrderComposition exactly once logically.

## 25. OrderComposition integration

OrderComposition starts only with:

- accepted unexpired TradeSelection result;
- committed FundCompositionReservationResult;
- frozen PortfolioFundStrategySnapshot;
- exact OrderComposition profile;
- permitted relevant market/reference data; and
- workflow invocation/deadline context.

OrderComposition output identities MUST exactly equal the reservation. It returns `Composed`, `NoCandidate`, or `Failed` and an immutable result/hash. It causes no Portfolio financial approval and no broker effect.

When the workflow accepts `Composed`, it records the result reference on FundOrder and continues to RiskManagement. `NoCandidate` stops normally according to workflow continuation rules. `Failed` stops as failed.

## 26. RiskManagement boundary

RiskManagement receives the exact candidate, all four accepted upstream results, frozen policy/envelope and qualified current financial evidence. It calculates an eligible whole-unit size or rejects. Portfolio owns General Ledger balances and atomic CapacityReservation admission; it independently validates proposed consumption against current authority without repeating strategy selection or silently resizing. Result-reference recording is not a capacity reservation.

Financial admission checks remaining Portfolio, exact deployment, Fund and applicable concentration limits separately using their current utilization. Disabled, missing, mismatched or stale required authority fails closed. No deployment cap may enlarge global or delegated limits. Section 42 defines exact risk result/receipt acceptance and compatibility.

`RiskApproved` alone is not executable authority. The workflow must accept the typed sized assessment and committed reservation receipt; execution must consume the still-valid hold before submission. Actual emulator integration is a future joint gate after its design and implementation. Current Portfolio qualification uses labelled execution-boundary fixtures and does not claim broker acceptance. Actual IBKR submission remains outside this Portfolio specification.

## 27. UI requirements

### 27.1 Navigation

- Keep existing Funds navigation during transition.
- Add Portfolio navigation.
- Label legacy navigation clearly before production cutover.
- Do not remove the old UI until new system gates pass.

### 27.2 Portfolio view

The Portfolio Administration command bar SHALL expose four primary actions: Refresh, New Portfolio, Risk Policy, and Portfolio Actions. `Show State` is a list filter. Portfolio Actions contains existing lifecycle actions plus scoped General Ledger administration. Planned Compositions remains absent. Section 43 adds financial views within the current three-section layout.

Existing Fund/configuration views remain accessible. Add Transactions, Balances, Reservations and reconciliation/journal details as specified in section 43. Preserve the Dark Trading Theme and bottom metric rows.

`Risk Policy...` is disabled until a Portfolio is selected and opens one modal scoped to that Portfolio. The modal SHALL implement the section 14.1 command lifecycle and the HLD section 16.3 layout, including:

- fixed Portfolio identity/state/version/base-currency context;
- a bounded policy/version grid and immutable selected-version detail;
- sequence-generated read-only PolicyId;
- global capital/risk fields;
- ReferenceDb-backed family selection with Enabled and five per-family caps;
- field/summary validation where zero means blocked, never unlimited;
- New Policy, New Version, Save Draft/Cancel, Activate & Assign, eligible Retire/Delete Draft, and Close behavior;
- exact typed confirmation and reason requirements;
- pending projection, conflict, timeout, authorization, unavailable, and validation states; and
- no direct database access.

The Reference screen SHALL show preserved seeds and newly created product-linked definitions in a Family master list, a filtered Strategy detail list, and a right-hand exact-definition selector/read-only details. Its `trade strategy families` selector SHALL expose shared Add/Save/Cancel controls for inline creation using provider-backed symbols, read-only Currency/Exchange and Daily/Weekly/Monthly timeframes. Detail inputs SHALL match Lookup Type's black background/white foreground, and all Reference Data controls SHALL use Microsoft Sans Serif 10pt. Existing definitions SHALL remain read-only with Change, Remove and Import disabled. SystemKey SHALL NOT be used as an exact identity.

### 27.3 Trade Orders view

The existing Trade Orders screen is the only manual/automated composition view. `PortfolioCompositionForm`, its navigation action, and competing planned-composition presentation state SHALL be removed. The minimum interaction is:

1. select Portfolio;
2. clear stale Fund/order/trade/detail state and load Funds owned by that Portfolio;
3. select Fund;
4. view canonical manual and StrategyWorkflow FundOrders for a bounded date range;
5. filter Source by All, Manual, or Strategy Workflow;
6. select FundOrder and view its FundOrderTrades;
7. inspect exact composition plus workflow/template/profile/composition/risk provenance; and
8. later inspect execution/position projections when implemented.

The view MUST remove Create Fund while retaining manual Create Order/Add Trade for an eligible Portfolio/Fund. StrategyWorkflow orders and all accepted immutable composition results are read-only. Order/trade lists retain the current operator interaction and add Source plus composition/risk status. Changing Portfolio or Fund cancels/supersedes outstanding loads so delayed responses cannot display the prior scope.

Submit, fill, live-feed, End-of-Day, and position actions remain legacy execution controls and MUST be disabled for new pre-execution Portfolio-backed records until the OrderExecution/TradeDb specification authorizes them. The cutover MUST replace legacy Fund mutations with the canonical new actor surface as one tested boundary; one form/session cannot mix legacy and new writes.

### 27.4 Integer display

OrderId and TradeId are primary operator-visible columns and searchable fields. Workflow GUIDs may be shown in diagnostics but do not replace integer identifiers.

PortfolioId, FundId, OrderId, TradeId, and any later approved integer business identity are display/search values, never editable creation inputs. Forms may show an allocated value read-only, but they MUST NOT permit operator override or fabricate a fallback value when sequence allocation fails.

## 28. Validation reason codes

Stable initial reason-code names are:

### 28.1 Identity and version

- `PortfolioIdInvalid`;
- `FundIdInvalid`;
- `OrderIdInvalid`;
- `TradeIdInvalid`;
- `VersionInvalid`;
- `VersionConflict`;
- `IdentityMismatch`;
- `IdempotencyConflict`; and
- `SequenceIdOverflow`.

### 28.2 Configuration

- `PortfolioNotFound`;
- `PortfolioNotActive`;
- `PortfolioVersionExpired`;
- `PolicyIdInvalid`;
- `PortfolioPolicyMissing`;
- `PortfolioPolicyNotActive`;
- `PortfolioPolicyNotEffective`;
- `PortfolioPolicyOwnershipMismatch`;
- `PortfolioPolicyReferenced`;
- `PortfolioPolicyLimitInvalid`;
- `TradeStrategyFamilyMissing`;
- `TradeStrategyFamilyVersionMismatch`;
- `TradeStrategyFamilyDisabled`;
- `TradeStrategyFamilyLimitInvalid`;
- `FundNotFound`;
- `FundNotActive`;
- `FundMandateExpired`;
- `FundParentMismatch`;
- `ActiveFundMissing`;
- `ActiveFundDuplicate`;
- `TemplateAssignmentMissing`;
- `TemplateAssignmentInvalid`;
- `HintProfileMissing`;
- `CompositionProfileMissing`;
- `FundRiskEnvelopeMissing`;
- `FundRiskEnvelopeExpired`; and
- `FundCapacityBlocked`.

### 28.3 Composition

- `TradeSelectionResultInvalid`;
- `TradeSelectionResultExpired`;
- `CompositionReservationExists`;
- `CompositionStateInvalid`;
- `CompositionIdentityMismatch`;
- `CompositionResultInvalid`;
- `CompositionResultExpired`;
- `CompositionFailed`;
- `RiskResultInvalid`;
- `CompositionCancelled`; and
- `CompositionExpired`.

Reason names are append-only. Numeric error-code assignments are finalized in the implementation plan after the central code audit; they remain within the approved Portfolio family.

## 29. Observability

### 29.1 Required span/log attributes

- actor and verb;
- CommandId/EventId;
- WorkflowId/StageInvocationId;
- PortfolioId/PortfolioVersion;
- PolicyId/PolicyVersion;
- TradeStrategyFamilyId/DefinitionVersion where applicable;
- FundId/FundMandateVersion;
- OrderId/TradeId where allocated;
- aggregate expected/current version;
- template/hint/composition/envelope versions;
- idempotency disposition;
- state transition;
- reason/error code; and
- commit/project timestamps and latency.

### 29.2 Metrics

Minimum metrics include command/query counts and latency, validation failures, version conflicts, idempotent replays/conflicts, sequence allocation failures, projection lag/failures, active Funds by state, composition outcomes, and query page sizes.

No metric label may use unrestricted high-cardinality values such as WorkflowId, OrderId, TradeId, or raw exception text.

## 30. Authorization and security

**Current delivery scope:** Production security is deferred until immediately before production deployment. Requirements for authenticated principal/transport identity in this specification are retained for that phase, not current development gate exit criteria. Development callers retain explicit principal/role/scope metadata and existing permission validation, without claiming those values are authenticated. Financial authority, membership, exact deployment permissions, limits, source integrity, deadlines and atomic persistence remain current functional requirements. No new bypass of existing checks is required.

Authorization policies must distinguish:

- Portfolio administration;
- PortfolioFinancialPolicy Draft administration;
- PortfolioFinancialPolicy activation/assignment;
- PortfolioFinancialPolicy retirement/deletion;
- TradeStrategyFamily reference read access;
- Fund administration;
- allocation/risk-envelope administration;
- template/profile administration;
- manual composition initiation/cancellation;
- read-only operations; and
- future execution authority.

Every mutation records caller provenance; before production, it SHALL be bound to an authenticated principal. Development principal metadata SHALL NOT be reported as authenticated. Broker credentials, API keys, and secrets are forbidden in contracts, events, logs, projections, and UI DTOs.

## 31. Test requirements

### 31.1 Unit tests

Unit tests SHALL cover identities/formatting, MessagePack round trips and keys, reserved Portfolio key 1, enum numeric assignments, catalog bootstrap mapping, global/family policy validators, policy state transitions, expected versions, idempotency, allocation overflow, snapshot/hash determinism, active Fund resolution, Trade Orders source/action state, and mapping.

### 31.2 BDD tests

BDD scenarios SHALL cover Portfolio lifecycle, Draft-only deletion, three-family Reference catalog behavior, policy create/version/activate/assign/supersede/retire/Delete-Draft, global/family cap behavior, Fund lifecycle, assignments, active Fund resolution, manual and StrategyWorkflow order visibility, composition reservation, duplicate reservation, composition success/failure, risk outcome recording, cancellation/expiry, and prohibited execution effects.

### 31.3 Integration tests

Integration tests use real NATS routing, PostgreSQL event/sequence databases, ReferenceDb and PortfolioDb Scylla schemas/projections, idempotent family bootstrap, policy activation/assignment coordination, actor restart/replay, and typed query clients. Test-owned IDs and rows are isolated and cleaned through public APIs or bounded test teardown.

### 31.4 Verification tests

The representative catalog includes at least:

| Portfolio/Fund | Template case | Expected |
| --- | --- | --- |
| Active Portfolio / Daily Fund | ES directional future | Resolves and reserves one OrderId/TradeId |
| Active Portfolio / Weekly Fund | Bullish vertical | Resolves matching template/profile |
| Active Portfolio / Weekly Fund | Bearish vertical | Resolves matching template/profile |
| Active Portfolio / Monthly Fund | Neutral Iron Condor | Resolves matching template/profile |
| Active Portfolio / Monthly Fund | Bullish-biased Iron Condor | Resolves matching template/profile |
| Active Portfolio / Monthly Fund | Bearish-biased Iron Condor | Resolves matching template/profile |
| Paused/Disabled/Retired Portfolio | Any | Configuration failure |
| Paused/Disabled/Retired Fund | Any | Configuration failure |
| Missing/duplicate active Fund | Any | Configuration failure |
| Blocked/expired envelope | Any | No new-exposure permission |
| Duplicate reservation | Same payload/key | Same integer IDs |
| Duplicate reservation | Different payload/key reuse | IdempotencyConflict |
| Reference bootstrap | Repeated/concurrent startup | Exactly three unique Active family definitions |
| Futures policy row | Enabled below global caps | Effective limit is the smaller global/family capacity |
| Vertical Spread policy row | Disabled | Family rejected before composition |
| Iron Condor policy row | Enabled with zero risk/margin capacity | Family retained but blocked |
| Any family row | Cap exceeds global value | Policy activation rejected |
| Missing/stale family version | Any template | Configuration failure |

Verification is representative, not an uncontrolled Cartesian expansion.

### 31.5 UI system tests

System tests verify compact Portfolio command-bar actions; Risk Policy modal layout, validation, immutable versioning, sequence gaps, activation/assignment, retirement/deletion, family selection and per-family editing; read-only three-row Reference screen; Portfolio-to-Fund Trade Orders cascading selection; Manual/Strategy Workflow source filtering; stale-load clearing; integer identity display/search; absence of Create Fund and Planned Compositions; pre-execution action fencing; error presentation; lifecycle cleanup; and continued legacy navigation during transition.

## 32. Implementation gates

Each gate requires code, required documentation updates, proportional BDD/unit/integration/verification coverage, and clean targeted test results.

| Gate | Deliverable |
| --- | --- |
| PF-01 | Project topology, shared identities, enums, serialization contracts, and repository-wide confirmation of the `34000-34299` error-code reservation |
| PF-02 | PostgreSQL Portfolio sequence and allocation tests |
| PF-03 | Portfolio aggregate, commands, events, state, validation |
| PF-04 | PortfolioFund mandate aggregate, commands, events, validation |
| PF-05 | Template/hint/composition profile assignments |
| PF-06 | Fund allocation and FundRiskEnvelope delegation |
| PF-07 | PostgreSQL event-source repositories, replay, snapshots |
| PF-08 | PortfolioDb schema and read/write contexts |
| PF-09 | Durable event projectors and idempotent projections |
| PF-10 | Typed NATS command/query APIs and clients |
| PF-11 | Active Fund resolution and frozen strategy snapshot |
| PF-12 | FundOrder/FundOrderTrade reservation and integer ID retention |
| PF-13 | TradeSelection reservation handoff |
| PF-14 | OrderComposition result-reference handoff |
| PF-15 | Risk outcome reference and boundary fencing |
| PF-16 | Portfolio UI and legacy-navigation coexistence |
| PF-17 | Portfolio/Fund Trade composition view changes |
| PF-18 | Full BDD/unit/integration/verification/system acceptance suite |
| PF-19 | Legacy Fund read-only isolation and no dual-write audit |
| PF-20 | Documentation, observability, security, performance baseline, and release evidence |
| PF-21 | Revised contract baseline: remove PortfolioCode/raw policy identity and reserve serialized key |
| PF-22 | ReferenceDb TradeStrategyFamily schema, sequence-backed idempotent seed, typed reads, and read-only Reference UI |
| PF-23 | Policy/family-limit identities, DTOs, serialization, validation, and sequence allocation |
| PF-24 | PortfolioFinancialPolicy aggregate lifecycle and atomic activation/assignment |
| PF-25 | Policy EventSourceDb repository, PortfolioDb projections, replay, tombstones, and rebuild |
| PF-26 | Policy/reference typed NATS APIs and frozen workflow propagation |
| PF-27 | Compact Portfolio command bar and complete Risk Policy modal |
| PF-28 | Unified Portfolio-to-Fund Trade Orders UI and separate composition-view removal |
| PF-29 | Cross-pipeline global/family-limit qualification across all five test layers |
| PF-30 | Regression, operational evidence, documentation reconciliation, and release approval |

OrderExecution and TradeDb execution redesign are not PF gates.

## 33. Performance requirements

- Point identity queries use one intended partition.
- Page queries are bounded and never use `ALLOW FILTERING`.
- Actor command processing has no blocking `.Result`/`.Wait()` calls.
- Portfolio/Fund hot state uses indexed lookup rather than repeated linear scans where collection size is unbounded.
- Sequence allocation uses the existing block allocator; no per-ID direct PostgreSQL `nextval` call is added outside it.
- Projection batches are bounded.
- Load tests establish baselines for active Fund resolution, composition reservation, paged order queries, and concurrent distinct-Fund commands.
- Performance optimization cannot weaken idempotency, replay, version, or validation guarantees.

## 34. Deferred work register

The implementation plan SHALL retain these deferred items:

1. Broker OrderExecution and all external effects.
2. Broker order IDs and reconciliation.
3. Fill lifecycle and live TradeDb positions.
4. Position monitoring and market-feed updates.
5. Uncontrolled legacy migration or physical deletion; section 44 requires explicit reconciled transaction migration and retains PF-31 historical-only mappings.
6. Legacy Funds UI removal.
7. Multi-asset and unrestricted multi-template ranking.
8. Advanced Portfolio optimization beyond approved hard limits.
9. High-throughput ScyllaDB sequence/tick identity review.
10. Operator-facing integer-width expansion beyond current checked Int32 contracts.
11. External QuickBooks connector implementation; mapping/export contracts are included in section 45.
12. Unqualified new strategy/product/currency admission; existing catalog variants are supported only by exact qualified models and permissions.
13. Scheduled PortfolioFinancialPolicy activation.

Deferred work cannot be implemented accidentally inside a PF gate.

## 36. Legacy Test Portfolio history query

PF-31 adds an explicit, non-authoritative history adapter. Imported Fund mandates append `HistoricalSource` and `HistoricalSourceFundId` to the MessagePack/JSON contract, remain Draft forever, and use newly allocated Portfolio/Fund IDs. The original IDs are never reused as new authority.

The Portfolio NATS query actor exposes typed queries for legacy scopes, source Fund catalog, bounded FundOrders, and FundOrderTrades with hydrated TradeDb execution evidence. The adapter may read `FundLegacyDbContext` and `ITradeDbReadContext`; it exposes no write context. Matching uses the unambiguous source `(OrderId, TradeId)` pair and returns `NoTradeDbDefinition`, `DefinitionOnly`, `PositionHistory`, or `FillHistory`. Any order/composition FundId absent from the source Fund catalog is returned as a separately queryable unassigned entry using its original source FundId. Such entries are never mapped as canonical Funds.

Trade Orders SHALL default to `Current`. `Legacy History` is an explicit mode that clears stale scope state, uses source-labelled DTO collections, broadens the operator date range for historical browsing, and disables Create Order, Add/Remove Trade, state changes, submit, fill, live feed, End-of-Day, and position mutations. Returning to `Current` restores the existing canonical PortfolioDb query path. Delayed responses are fenced by mode, Portfolio, Fund, and generation.

Single-selecting a supported legacy Iron Condor SHALL render the original four-leg `IronCondorTradeOrderView` in the lower Trade Orders detail region without closing Trade Orders. The lower editor SHALL show the stored TradeDb-backed leg actions, expirations, strikes, quantities, bid/ask values, spreads, probabilities, limits, and trade values; it SHALL NOT substitute the graph-enabled operational `IronCondorView`. Selection replacement SHALL await closure and disposal of the preceding editor and SHALL fence delayed work by selection generation. A missing TradeDb definition SHALL show only `No corresponding TradeDb trade exists for OrderId:TradeId`; an unsupported type or unavailable exact base contract SHALL show a concise editor-unavailable message. The historical editor SHALL consume the hydrated TradeDb option trade returned by the legacy query and the source legacy Fund balance without making current reference, Fund, feed, or market-data queries during load. Every text input SHALL be read-only, selectors/date/quantity/action controls SHALL be disabled, and submission, removal, risk generation, order-action lookup, and live-feed entry points SHALL fail closed before service execution. Its value date SHALL retain the original editor semantics: opening compositions use the FundOrder trade date, closing compositions use the FundOrderTrade trade date, and an unavailable date falls back to the TradeDb trade date. `View Legacy Trade` and legacy-trade double-click SHALL continue returning the accepted historical selection, source Fund, and source FundOrder to the main application. The main application SHALL create or activate one graph-enabled middle-screen tab named `OrderId:TradeId`; repeated opening SHALL not duplicate that legacy tab, and its separate immutable historical-read-only boundary SHALL remain in force. The Trade Orders form SHALL use a compact resizable layout with a 900-pixel default client height and an adaptive detail region.

## 35. Definition of done

The earlier configuration phase is implemented only when the applicable criteria below hold. Full v1.2 financial completion additionally requires every PF-FIN gate and invariant in section 46; old PF counts do not cover the new financial subdomains.

- all applicable PF-01 through PF-31 gates are complete, with PF-01 through PF-20 retaining their historical evidence and reopened status where superseded behavior invalidates acceptance;
- new Portfolio/Fund actors use NATS and authoritative PostgreSQL event history;
- Scylla PortfolioDb projections rebuild from events;
- Portfolio and Fund versions freeze correctly into the workflow;
- active Fund resolution is deterministic and fails safely;
- OrderId and TradeId are positive PostgreSQL-generated integers retained unchanged;
- duplicate composition reservation returns the same IDs;
- TradeSelection and OrderComposition handoffs satisfy their mandates;
- no Portfolio/Fund path performs broker execution or creates a live position;
- the Portfolio UI and composition view pass system tests;
- the Reference screen exposes exactly three read-only v1 families and repeated bootstrap remains idempotent;
- Risk Policy global/family limits and atomic activation/assignment pass all five test layers;
- Trade Orders is the sole manual/automated composition view with Portfolio-to-Fund scoping;
- legacy Funds remain isolated without dual writes;
- all required tests pass without residual test data; and
- release evidence records commands, tests, schemas, versions, and known deferred work.

## 37. Financial subdomain scope and ownership

### 37.1 Normative authority

Sections 37–46 implement the requirements of [HLD revision 0.3, sections 27–34](../../Documents/system/Portfolio-Fund-High-Level-Design-v0.1.md#27-new-portfolio-financial-subdomains). `GeneralLedger` SHALL own transaction posting, balanced journals, financial account balances, periods, corrections and reconciliation. `CapacityReservation` SHALL own financial holds, usage, admission, consumption and release. Risk Management owns strategy sizing; it does not own either ledger. A hold SHALL NOT be posted as a cash expense.

The first release SHALL support USD, one verified Portfolio/account capacity pool and multiple Funds within it. Accounting entity/book, Portfolio, Fund and broker account are distinct identities. Unsupported currencies, cross-Portfolio transfers and multiple Portfolios independently sharing account buying power SHALL fail explicitly. This restriction MAY be lifted only by a separately versioned and tested allocation/FX protocol.

All mandatory money/count limits use zero to mean no capacity. Optional controls require an explicit enabled flag; zero is never infinity. Missing financial observations are unknown, not a zero balance. A complete-empty ledger is valid only when its authority, opening basis and ingestion watermark are known.

### 37.2 Required actors and APIs

**2026-09-08 actor revision:** Exactly two Portfolio financial Functions are required: reservation and pre-submission consumption. Ledger posting and other lifecycle changes are Commands. This supersedes the earlier four-Function proposal; none of these actors is claimed implemented.

| Owner / actor | Required request surface | Result / responsibility |
| --- | --- | --- |
| GeneralLedger `GeneralLedgerCommandActor` | `PostFundTransactionCommand`, verb `Post`; `PostFundTransactionsCommand`, verb `PostBatch` | Corresponding `LedgerPostingCompletedEvent` / `LedgerPostingFailedEvent` or batch events; atomic business/receipt/domain-event persistence and correlated post-commit delivery |
| CapacityReservation `CapacityReservationFunctionActor` | `ReservePortfolioTradeRiskCommand`, verb `Reserve` | `CapacityReservationCompletedEvent` / `CapacityReservationFailedEvent`; atomic hold/receipt/completion |
| CapacityReservation `CapacityConsumptionFunctionActor` | `ConsumeCapacityReservationCommand`, verb `Consume` | `CapacityConsumptionCompletedEvent` / `CapacityConsumptionFailedEvent`; committed consumption before submission |
| CapacityReservation `CapacityReservationCommandActor` | `ChangeCapacityReservationCommand`, verb `Change`; excludes Consume | `CapacityLifecycleCompletedEvent` / `CapacityLifecycleFailedEvent`; working/fill/submission-unknown/cancel/release/expiry updates |
| GeneralLedger configuration Command surface | Create/version/activate account and posting rule; close/reopen period | Existing mapped Command conventions; authority changes participate in the financial fence |
| GeneralLedger Query actor | Journal, source receipt, account balance, paged Fund transactions, trial balance, reconciliation | Exact authorized scope and explicit authority/projection revision |
| CapacityReservation Query actor | Original receipt, current reservation, usage snapshot, paged reservations | Distinguish immutable completion from current available authority |

Function subjects SHALL use `ActorType.Function`, the exact mailbox/verb and typed PortfolioId + OperationId execution identity, never a permanently completed Portfolio stream. Ledger/lifecycle subjects SHALL use `ActorType.Command` and continuing aggregate identities: ledger scoped by Portfolio, lifecycle scoped by Portfolio + ReservationId. PF-FIN-01 freezes their exact typed key/route manifests. Command expected stream revision is tracked independently from ExpectedFinancialRevision; OperationId/source receipts deduplicate attempts without making the aggregate completed-only. Commands must reload current aggregate state before a new mutation; old-operation replay cannot rewind it. The shared spending fence remains Portfolio-wide across both actor types.

The ledger Command's request kind is a validated business transaction type; dispatch and transaction-specific rules SHALL live in mapped extensions/Models. Raw caller-authored journal lines SHALL be allowed only for the explicitly authorized adjustment/import surface, not deposit, withdrawal or execution-feed APIs.

`ICapacityReservationApi` and `ICapacityConsumptionApi` SHALL use typed Function request/reply. `ILedgerPostingApi` and `ICapacityLifecycleApi` SHALL use typed Commands with correlated committed/failed outcomes and receipt reconciliation; accepted/queued is not financial completion. `IGeneralLedgerQueryApi` and `ICapacityReservationQueryApi` serve typed reads. No client may inject a financial DbContext. Shared DTOs belong in the neutral `Domain.Strategy.Contracts.Shared` Portfolio area where cross-domain dependencies require it, with existing public forwarding conventions; no concrete storage/pricing/QuickBooks SDK dependencies.

### 37.3 Actor mapping and completion requirements

All financial Functions SHALL derive from the shared Function lifecycle and declare frozen `_parseMap`, `_validationMap`, `_receiveMap`, `_executionPolicyMap` and `_eventMap`. `ValidateAsync` SHALL use base `ValidateMappedCommand` and ordered `List<ValidationError>` extensions. Directly inject each typed context and alias the generic Function context to the same singleton. No `Typed()`, actor-owned timer helper, domain SQL/calculation in actor overrides or direct terminal-handler bypass is permitted.

Function Execute/Complete/Fail and execution-policy handlers SHALL be separate extensions. Models calculate/validate domain effects. The shared Function lifecycle owns transactional completion, cancellation, late-operation observation, reply and completed-state replay; Function terminals SHALL NOT be published through an eventual projector.

Ledger/lifecycle Commands SHALL use standard mapped Command parsing, base `ValidateMappedCommand`, ordered `List<ValidationError>` extensions, receive/event handlers and continuing aggregate state. Do not require Function execution-policy/terminal maps or wrap Commands in Functions. Shared persistence SHALL enlist business changes, receipt and Command domain outcome in one transaction before success. Durable Command publication/projection follows commit with restartable delivery; it never applies financial mutations again. Configuration Commands retain their conventions and participate in the financial fence when changing spending authority.

## 38. Financial identities and wire contracts

### 38.1 Identity, types and common request manifest

PortfolioId/FundId/OrderId/TradeId remain checked Int32 business identities. New BookId and AccountId SHALL be generated positive Int32 values; JournalId and financial source TransactionId SHALL be positive Int64 values. Named sequence registrations SHALL be `PortfolioLedger_BookId`, `PortfolioLedger_AccountId`, `PortfolioLedger_JournalId` and `PortfolioLedger_TransactionId`, accessed through the existing allocator. Gaps are valid; IDs SHALL NOT be reused or typed by an operator. ReservationId, OperationId, EventId, source-event and trace identities are UUIDs. Line ordinal is a positive Int32 within its journal.

Dates SHALL distinguish accounting date, source value date, settlement date and UTC occurrence/record/commit times. Currency SHALL be explicit ISO code `USD` in v1. Monetary DTO values use decimal; PostgreSQL stores ledger USD amounts as `numeric(28,2)`. Input requiring finer precision SHALL use an explicit immutable rounding rule with a separately identifiable rounding line where needed, not silent truncation. Risk measurements retain higher precision and explicit units; reject overflow rather than clamp.

All new financial request DTOs SHALL use the following explicit MessagePack keys with actor-specific concrete identity/Body types. `Body` is not `object` or a serialized byte payload. These are unreleased contracts; PF-FIN-01 SHALL verify the Command envelope against standard Command interfaces before enabling routes, without repurposing any released key:

| Key | Field | Rule |
| --- | --- | --- |
| 0 | SchemaVersion | 1 |
| 1 | CommandId | Nonempty; stable for a transport retry |
| 2 | Subject | Exact Function execution or Command aggregate mailbox/verb/identity |
| 3 | PostEvents | False for Functions; normal durable post-commit Command delivery for posting/lifecycle, as pinned by the Command contract |
| 4 | EntityId | Functions: typed PortfolioId + OperationId; ledger Command: Portfolio scope; lifecycle Command: PortfolioId + ReservationId |
| 5 | ErrorCode | Distinct registered command error ID |
| 6 | RouteTo | Correct Portfolio financial bounded-context route |
| 7 | OperationId | Stable logical operation; matches Function execution identity, independent of continuing Command aggregate identity |
| 8 | PortfolioId | Positive and matches identity |
| 9 | CorrelationId | Nonempty |
| 10 | CausationId | Nonempty |
| 11 | RequestedAtUtc | Fixed UTC |
| 12 | ExpiresAtUtc | Fixed UTC, later than requested; bounded by authority |
| 13 | ExpectedFinancialRevision | Nonnegative and checked against current authority |
| 14 | Body | Concrete immutable LedgerPostingRequest, LedgerPostingBatchRequest, CapacityReservationRequest, CapacityLifecycleRequest or LedgerConfigurationRequest |
| 15 | InputSha256 | Canonical semantic hash excluding this field and local diagnostics |
| 16 | Access | Authenticated principal, explicit financial role and Portfolio scope grants; never inferred from the selected UI Fund |

`FinancialExecutionId` keys are 0 PortfolioId, 1 OperationId and apply only to the two Functions. Command aggregate key manifests follow section 37.2 and SHALL be frozen separately in PF-FIN-01; never reuse a legacy FundId.OrderId stream. Additional enum values/routes and numeric error IDs SHALL be allocated append-only after checking the complete registry; until allocated, production route enablement is prohibited. Semantic reasons below are normative independently of registry numbers.

The current concrete registry and legacy type/source inventory are recorded in [Portfolio financial implementation manifests](./Portfolio-Financial-Implementation-Manifests-v1.0.md). `ConfigureLedgerCommand` uses Command/LedgerConfigurationCommand/Configure and error ID 34125. Configuration actions retain the same atomic receipt/event boundary as posting. FinancialAuthorityReference.EnvelopeId is a GUID. FinancialFundAuthority key 7 contains per-deployment reference/limit entries, so several deployments can share one Fund's aggregate caps. Semantic hashing normalizes UTC timestamp representation and decimal scale independently of MessagePack encoding.

### 38.2 Ledger payload manifests

Each comma-separated item in the following table is one successive numeric MessagePack key, starting at zero. These are new schema-1 contracts; all nested owned DTOs SHALL have explicit key tests before release.

| DTO | Ordered fields |
| --- | --- |
| LedgerPostingRequest | BookId, FundId, TransactionKind, AccountingDate, ValueDate, SettlementDate, Currency, Amount, Description, Source, PostingRule, RelatedJournalId, CounterpartyFundId, Lines, Authority, MovementEvidence, RelatedObligationId |
| LedgerPostingBatchRequest | BookId, Items, BatchSourceReference, ManifestHash |
| LedgerPostedTransaction | Ordinal, TransactionId, JournalId, JournalHash, Source, ObligationId |
| LedgerPostingBatchReceipt | SchemaVersion, OperationId, PortfolioId, BookId, Items, ManifestHash, InputHash, FinancialRevision, CommittedAtUtc, CompletedEventId |
| LedgerSourceReference | System, SourceEntityId, SourceEventId, SourceSequence, SourceContentHash, OccurredAtUtc, LegacyTransactionId, LegacyFundId, OrderId, TradeId, FillId |
| LedgerPostingRuleReference | RuleId, Version, ContentHash |
| LedgerEntryDraft | Ordinal, AccountId, FundId, PostingSide, Amount, Currency, OrderId, TradeId, SourceLineReference |
| LedgerMovementEvidence | Status, SourceReference, ObservedAtUtc, ReceivedAtUtc, ValidUntilUtc, ContentHash |
| FinancialAuthorityReference | PortfolioVersion, FundMandateVersion, PolicyId, PolicyVersion, EnvelopeId, EnvelopeVersion, AssignmentVersion, DeploymentKey, AuthorityEpoch, ValuationWatermark, SourceWatermark, FinancialSnapshotHash, ValidUntilUtc |
| LedgerPostingReceipt | SchemaVersion, OperationId, PortfolioId, BookId, FundId, TransactionId, JournalId, JournalHash, InputHash, FinancialRevision, CommittedAtUtc, CompletedEventId, Source, ObligationId |

FundId is required for a Fund business transaction; a Portfolio-wide accounting line may omit FundId only under a qualified book-level rule. Missing optional identities SHALL be null, never fabricated zero business IDs. `Lines` SHALL be empty on normal business transaction requests; the posting rule generates them. On authorized adjustment/import requests it SHALL be complete, balanced and bounded. `Amount` is a positive business magnitude for deposits, withdrawals and transfers; debit/credit placement comes from the rule. General adjustment entries use positive line magnitudes plus PostingSide. Source P&L sign is explicitly interpreted by its typed posting rule.

Ledger TransactionKind values SHALL be append-only: Undefined=0, DepositConfirmed=1, WithdrawalRequested=2, WithdrawalSettled=3, WithdrawalCancelled=4, FundTransfer=5, TradeSettlement=6, Commission=7, RealizedPnl=8, Valuation=9, Reversal=10, Adjustment=11, OpeningBalance=12. Period close/reopen belongs to the configuration/control surface, not a zero-money posting. PostingSide is Undefined=0, Debit=1, Credit=2. MovementStatus is Undefined=0, Pending=1, Confirmed=2, Cancelled=3, Unknown=4. Unsupported kind/evidence combinations SHALL fail, not post a generic amount.

Opening capital is **development-only** (owner clarification, 2026-09-08). A new `OpeningBalance` posting SHALL require the trusted API host environment to be Development, the stored book environment to be Emulator, an Importing/unqualified book, and source system `DevelopmentOpeningCapital`. Request roles, principal text and environment labels SHALL NOT enable the host policy. Missing policy denies the posting. Apply these checks inside the shared financial transaction for every batch item; failure SHALL leave no journal, balance, source receipt, operation receipt or completion event. Existing privileged-posting, period, rule and source checks still apply. A replay of an already committed operation returns its original receipt without adding capital, including after development funding is disabled. Opening capital SHALL NOT qualify a book or activate spending automatically. No test capital amount is a production default; production funding needs separately qualified actual movement evidence. Historical balances and `OpeningTrade` snapshots SHALL NOT be relabelled as deposits. This restriction takes precedence over the earlier general opening-balance migration alternative for the current implementation.

An encumbrance-only withdrawal request/cancellation records a business transaction and financial obligation without inventing a cash journal. Its JournalId/JournalHash are null and ObligationId is required. Actual journal postings require non-null JournalId/JournalHash and balanced entries. Withdrawal settlement/cancellation SHALL reference the exact original obligation, validate remaining amount and update it atomically; source receipt identity prevents repeat settlement. Journal-free operations still commit business state and their completed event together.

Batch Items SHALL contain 1–100 ordered LedgerPostingRequest values under the same Portfolio/book/USD authority and no duplicate source keys. All items are validated before commit and their cumulative financial effect is checked under the shared fence. A batch has one OperationId, financial revision and completed event, with an ordered LedgerPostedTransaction manifest; item sources/journals link to that original batch receipt. It commits fully or rolls back fully. Oversized imports use independently identified batches with explicit progress; partial import is not reported as one atomic success.

`Authority` SHALL be checked according to operation: a confirmed execution settlement is not discarded merely because its Fund is now paused; it must be recorded once and may produce an over-limit state blocking new spending. New withdrawals/reservations require current spending permission. Supplied principal strings and source labels are provenance only; authenticated message context determines permission.

### 38.3 Capacity payload manifests

| DTO | Ordered fields, keys from zero |
| --- | --- |
| CapacityReservationRequest | ReservationId, FundId, BookId, OrderId, TradeIds, WorkflowId, InputWorkflowRevision, RiskInvocationId, RiskResultId, RiskAssessmentHash, CompositionResultId, CompositionResultHash, UnitCandidateHash, SizedOrderHash, StrategyUnits, Requirements, Authority, MarginEvidenceReference, ExecutionEnvironment, ValidUntilUtc, AcceptedIntentReference |
| CapacityRequirements | Currency, SettlementCash, MarginFunding, FeeReserve, VariationReserve, LossCharge, MarginRequirement, GrossNotional, GrossContracts, PositionSlots, Exposures, AccountingMethodVersion, ContentHash |
| CapacityExposure | ScopeKind, ScopeKey, Measure, Amount, Unit, MethodVersion |
| FinancialEvidenceReference | EvidenceId, Version, ContentHash, Source, Environment, ObservedAtUtc, ValidUntilUtc |
| CapacityReservationReceipt | SchemaVersion, OperationId, ReservationId, PortfolioId, FundId, BookId, OrderId, TradeIds, RiskResultId, RiskAssessmentHash, CompositionResultHash, UnitCandidateHash, SizedOrderHash, StrategyUnits, Requirements, AuthorityEpoch, FinancialRevision, GrantedAtUtc, ValidUntilUtc, ExecutionEnvironment, CompletedEventId, InputHash |
| CapacityLifecycleRequest | ReservationId, ExpectedReservationVersion, ChangeKind, ExecutionId, ExecutionRevision, Source, FilledUnits, CancelledUnits, RemainingUnits, RelatedPostingReference, ExpectedRequirementsHash |
| CapacityLifecycleReceipt | SchemaVersion, OperationId, ReservationId, ReservationVersion, FinancialRevision, Status, FilledUnits, CancelledUnits, RemainingUnits, CurrentRequirementsHash, CommittedAtUtc, CompletedEventId, InputHash |

`AcceptedIntentReference` SHALL identify immutable authoritative workflow evidence binding the exact sized decision to this order. Preparation SHALL materialize required qualified input evidence before the transaction. A hash/reference alone from an untrusted caller is not proof of a valid risk calculation. Admission SHALL verify the committed lineage and independently validate the requirement vector using the pinned model/evidence; it SHALL NOT accept arbitrary caller-supplied smaller loss/margin amounts.

CapacityExposures SHALL use explicit enumerated scope/measure/unit identifiers for Portfolio, exact deployment, Fund and underlying concentration. Each enabled limit SHALL have exactly one compatible usage basis; unknown units or ambiguous duplicate measures fail. Money and risk vector arithmetic SHALL not sum amounts that overlap: MarginRequirement is the margin limit measure; MarginFunding is only incremental cash collateral not already included in settlement/free-cash accounting. The frozen accounting method SHALL prove which components reduce spendable cash.

StrategyUnits SHALL be positive for reservation, and unchanged from accepted sizing. GrossContracts SHALL count absolute leg quantities; PositionSlots SHALL count the logical strategy position, not legs or units. Signed Greek constraints SHALL retain per-market normalization and conservative pending-fill exposure as required by Risk design. Contract multipliers and included fee/slippage reserves are applied once.

ReservationStatus values SHALL be Undefined=0, Reserved=1, Consumed=2, Working=3, PartiallyFilled=4, SubmissionUnknown=5, CancelPending=6, Filled=7, Released=8, Expired=9. ChangeKind values SHALL be Undefined=0, Consume=1, RecordWorking=2, RecordFill=3, MarkSubmissionUnknown=4, RequestCancel=5, ConfirmCancel=6, ReleaseUnconsumed=7, ExpireUnconsumed=8. No generic SetStatus operation is permitted.

`ConsumeCapacityReservationCommand` uses the concrete `CapacityLifecycleRequest` body restricted to ChangeKind Consume and returns a concrete `CapacityLifecycleReceipt` in `CapacityConsumptionCompletedEvent`. Consumption loads and rechecks the reservation's exact accepted intent, sized order, authority and execution identity before commit. `ChangeCapacityReservationCommand` SHALL reject Consume even for an authorized caller; the consumption Function SHALL reject every other kind. Both share the same reservation version/financial fence and immutable lifecycle history. Command handlers SHALL reload authoritative PostgreSQL reservation/usage under that fence, including changes committed by Functions in separate event streams; cached Command state is insufficient. An exact consumption replay cannot authorize another submission or reset current lifecycle state.

### 38.4 Terminal events and serialization

Every concrete financial Completed event SHALL have keys: 0 SchemaVersion, 1 Id, 2 Subject, 3 EntityId, 4 CommandId, 5 OperationId, 6 PortfolioId, 7 CorrelationId, 8 CausationId, 9 CommittedAtUtc, 10 InputHash, 11 Receipt (concrete typed receipt). Event and receipt IDs/time/revision SHALL match the transaction's stored event; receipt replay SHALL not change them. ReplayDisposition is transport observation metadata and SHALL NOT rewrite original journal/completion content.

Every concrete Failed event SHALL have keys: 0 SchemaVersion, 1 Id, 2 Subject, 3 EntityId, 4 CommandId, 5 OperationId, 6 PortfolioId, 7 CorrelationId, 8 CausationId, 9 FailedAtUtc, 10 ErrorCode, 11 ReasonCode, 12 FailureClass, 13 CommitDisposition, 14 ExpectedRevision, 15 ObservedRevision, 16 Message, 17 ExistingOperationId (nullable). FailureClass distinguishes BusinessRefusal, InvalidRequest, Configuration, Infrastructure, Timeout and Conflict. CommitDisposition is NotCommitted, OutcomeUnknown or NoNewMutation; it SHALL NOT claim rollback from cancellation alone. A previously committed conflicting operation may exist under the reused identity; NoNewMutation does not deny that history.

Same-OperationId/hash retry returns the exact original completion. A new operation containing a source already committed elsewhere returns `GL.SOURCE.ALREADY_POSTED` with NoNewMutation and the original operation reference; the caller queries its original receipt. It SHALL NOT fabricate a newly correlated completion or partially post the other items in a batch. Different content for an existing source is `GL.SOURCE.CONFLICT`.

Use shared MessagePack transport/storage and uncompressed size measurement. No serializer round-trip cloning, JSON inner payload or manual MessagePack result bytes. Canonical hashing SHALL normalize decimals/UTC, use ordinal ordering for sets, preserve ordered journal lines and exclude self-hash/local diagnostic fields. Existing historical field meanings and hashes are unchanged. Failures are not persisted as completed Function state; workflow/operations records retain failure and reconciliation status separately.

## 39. Posting rules, accounting and financial invariants

### 39.1 Posting classification

Account identifiers SHALL come from exact versioned rule bindings, not hard-coded user account numbers. Accounts SHALL have immutable category, normal side, currency and allowed book/Fund dimensions. Draft configuration may be edited; used/activated versions are retained. Journal posting SHALL enforce all of:

1. At least two and at most 256 lines; positive, finite, representable amounts; unique contiguous ordinals.
2. Total debit equals total credit exactly in USD; no automatic suspense plug to make an invalid request balance.
3. Accounts belong to the authorized book, permit posting, match currency and required Fund ownership.
4. Accounting period is open or an explicit adjustment/reopening authorization exists.
5. Source identity is unique and bound to canonical content; duplicate content replays, different content conflicts.
6. Spending operations fit current spendable funds and financial controls under the same fence as reservations.
7. Receipt, journal, balance revision and completed event either all commit or none commit.

### 39.2 Required business treatments

| Source operation | Required financial treatment |
| --- | --- |
| Confirmed deposit | Increase cash and corresponding capital/clearing account under the rule; count as external flow, not trading profit |
| Withdrawal requested | Encumber available cash atomically; do not assert a bank movement or reduce confirmed cash twice |
| Withdrawal settled | Post actual cash movement and clear exactly the related encumbrance in the same transaction |
| Withdrawal cancelled | Release only the confirmed unexecuted obligation; unknown bank/venue state retains the encumbrance |
| Intra-Portfolio Fund transfer | Debit/credit both Fund dimensions atomically, preserve Portfolio totals and currency, validate source Fund unreserved funds |
| Trade settlement | Map actual instrument/account settlement facts through a versioned rule; order notional is not automatically cash paid |
| Commission/fee | Post once by source fee identity; cumulative corrections post only the delta or reverse/rebook explicitly |
| Realized P&L | Record from qualified realization/settlement facts and reconcile any previously booked unrealized valuation |
| Unrealized valuation | Book difference from prior valuation cut or reverse/rebook; never treat a model mark alone as settled spendable cash |
| Reversal/adjustment | Link original journal/source and authorized reason; preserve original entries; prevent duplicate or excess reversal |
| End of day | Apply qualified settlement/valuation rules at one complete source cut, then period/control markers; rerun cannot create profit twice |
| Opening balance | Reconciled migration/init journal with explicit source basis and approval; no automatic fabricated seed cash |

Pending withdrawal obligations SHALL use `financial_encumbrance` records distinct from trading reservations, but the same spendable-cash calculation and fence. Deposit confirmation SHALL require trusted source evidence or an explicitly authorized operator attestation that a movement already occurred. Creating a ledger entry SHALL NOT itself call a bank or broker.

Confirmed fees, fills and settlement losses SHALL be recorded even if they make balances negative or exceed current policy. The transaction SHALL atomically mark the affected spending authority blocked/constrained and preserve the exception; it MUST NOT hide financial reality by rejecting an authentic settlement as an ordinary insufficient-funds withdrawal. Admission of new exposure then fails until reconciled.

### 39.3 Available funds, periods and corrections

Spendable funds SHALL derive from the versioned cash method: settled usable cash minus non-overlapping unsettled obligations, withdrawal encumbrances, unconsumed holds and working-order cash commitments, subject to protected reserve and allocation limits. Existing uses SHALL not be subtracted twice from a source that already reports net free funds. Store the basis and ingestion watermark. For internal transfers, source availability and destination updates occur under one transaction.

At each applicable Portfolio/deployment/Fund/concentration scope, current usage plus proposed consumption SHALL fit the corresponding limit. Intersecting maximum limits alone is insufficient because scopes have different existing usage. Policy activation, Fund suspension and kill switches participate in admission fencing. Optional disabled constraints must be explicit. No unfilled hedge may finance new exposure through an assumed simultaneous fill.

Periods SHALL be Open, Closing or Closed. Closing freezes a source watermark and requires complete reconciliation; late data creates a visible exception until an authorized adjustment or reopen is processed. Closed journals SHALL never be overwritten. Reversal stores `reverses_journal_id` and adjusts remaining reversible amounts under lock. Financial records SHALL not be physically deleted after posting. Retention rules must preserve evidence for open obligations and configured audit periods.

Valuation sources SHALL carry observation time, received time, method/version, completeness and a monotonically comparable source cut. Source timezone and exchange/account value date SHALL be explicit; UTC midnight is not a universal accounting rollover. Flow-adjusted equity/drawdown SHALL exclude deposits/withdrawals from trading performance. Reports SHALL label raw legacy balance metrics when they do not meet this method.

## 40. PostgreSQL and Scylla storage specification

### 40.1 Physical authority and transaction API

Create schema `portfolio_financial` in the existing EventSourceActor PostgreSQL database. Introduce typed `IPortfolioFinancialDbContext`, `IGeneralLedgerStore` and `ICapacityReservationStore` abstractions with a request-scoped unit of work. The unit of work SHALL expose one enlisted connection/transaction to financial writes and completed-event append. Ordinary store methods opening independent connections SHALL NOT satisfy transactional completion. No distributed transaction with SequenceIdDb, Scylla, NATS or external APIs is permitted.

Existing `PortfolioDbContext` remains Scylla. General Ledger financial schema initialization SHALL be additive/versioned and complete before its mutation routes are enabled. Missing/mismatched schema or unavailable shared transaction capability SHALL fail readiness; no fallback to FundDb. ConfigurationDb may hold reusable versioned posting/mapping definitions, but ledger book/account authority and current balances/holds SHALL remain under Portfolio ownership.

### 40.2 Required relational tables and constraints

All tables are in `portfolio_financial`; names and keys below are the financial specification. Audit hashes and immutable payload columns supplement, not replace, the indexed keys and constraints.

| Table | Key and required columns / invariants |
| --- | --- |
| `ledger_book` | PK book_id int; accounting_entity_id UUID, portfolio_id int, base_currency, execution_account_ref, environment, version bigint, status; active exclusive capacity-account mapping enforced |
| `ledger_account` | PK (book_id, account_id int, version bigint); category, normal_side, currency, Fund-dimension policy, status, content_hash; referenced versions never removed |
| `ledger_posting_rule` | PK (book_id, rule_id UUID, version bigint); kind, exact account-version bindings, content_hash, status, effective interval |
| `ledger_transaction` | PK transaction_id bigint; book/Portfolio/Fund, operation_id, item ordinal, transaction kind, source identities/hash, amount/currency, dates, related obligation/journal references and immutable business payload; includes journal-free obligations |
| `ledger_journal` | PK journal_id bigint; transaction_id bigint unique FK ledger_transaction, book_id, portfolio_id, fund_id nullable, operation_id UUID, accounting/value/settlement dates, kind, source hash, rule reference, reversal reference, committed UTC, financial_revision; immutable |
| `ledger_entry` | PK (journal_id, ordinal int); account_id/version, fund_id nullable, debit numeric(28,2), credit numeric(28,2), currency, source line/order/trade references; exactly one side positive, other zero; FK journal and exact account version |
| `ledger_account_balance` | Unique book/account/Fund/currency scope with null Fund treated as one scope; debit/credit totals numeric(28,2), balance, revision bigint; updated inside journal transaction |
| `ledger_posting_receipt` | PK (portfolio_id, operation_id); execution_id, input_hash, concrete receipt type, transaction/journal manifest, completion_event_id, committed UTC, financial_revision, payload; each non-null journal reference names a committed journal |
| `financial_source_receipt` | Unique (book_id, source_system, source_event_key, posting_purpose); source_content_hash, operation_id, journal_id/receipt reference; same source cannot be posted under a new command identity |
| `financial_encumbrance` | PK obligation_id UUID; Portfolio/book/Fund, source identity, kind, amount/currency, status, revision, settlement/release evidence; current withdrawals and non-trade obligations |
| `financial_authority` | PK portfolio_id; financial_revision bigint, authority_epoch bigint, policy/source versions, operating state, active book/account mapping, last source/valuation watermarks; serialization point for spending invariants |
| `capacity_reservation` | PK reservation_id UUID; unique Portfolio/operation, Fund/book/order/candidate/risk hashes, units, requirements, environment, expiry, status, version, completion event; one active reservation per accepted business order |
| `capacity_usage` | PK (portfolio_id, scope_kind, scope_key, measure, unit); current held/working/position values and revision; coherent with immutable lifecycle changes |
| `capacity_lifecycle` | PK (reservation_id, version); unique source transition identity, operation_id, prior/new status, usage delta, quantity totals, execution reference, commit time and completion event |
| `ledger_period` | PK (book_id, period_id); accounting-date bounds, state, revision, closing source cut and authorized close/reopen evidence; no overlapping effective periods |
| `ledger_reconciliation` | PK reconciliation_id UUID; book/Portfolio/Fund scope, source cut, counts/totals/hashes, difference records, resolution links and status |
| `ledger_migration` | PK migration_id UUID; source-to-target mappings, mode, watermarks, manifest hash, verified totals, writer fence and cutover state |
| `accounting_export` | Unique (destination_company, export_id); source journal set/cut, payload hash, mapping version, delivery status, external receipt, retry/reconciliation metadata; no duplicate source inclusion under the selected export mode |

Database constraints SHALL enforce row identities, positive IDs, nonnegative line sides, exact account references, receipt/source uniqueness and one active order hold. Balanced journals require a deferred database constraint/constraint trigger or an equally restrictive transactional posting procedure that prevents any alternate writer from committing unbalanced entries. Cross-row invariants SHALL NOT rely solely on UI validation. Portfolio-scoped mutations acquire `financial_authority` first, then account scopes in stable sorted order; unique source checks and reservation transitions use the same prescribed order.

Journal and capacity outcomes SHALL append through the existing event-store schema/API inside that transaction. A first completed Function operation uses expected stream version zero; Commands append at their current expected aggregate version and support many distinct operations in that stream. Do not write a second unrelated completion log or save an event again through the ordinary base path. Rebuild/reset tooling MUST NOT delete live financial authority with a Scylla projection reset.

### 40.3 Observation projections

Add Portfolio-owned Scylla tables with these query partitions:

- `ledger_journal_by_id`: partition `(portfolio_id, journal_id)`; immutable typed detail and source event revision.
- `fund_transaction_history`: partition `(portfolio_id, fund_id, month_bucket)`; clustering `(accounting_date, committed_at_utc, transaction_id)` descending for history; book/currency/type/status as returned metadata.
- `fund_balance_snapshot`: partition `(portfolio_id, fund_id, book_id)`; account/currency key and financial revision/source watermark.
- `fund_reservation_history`: partition `(portfolio_id, fund_id, month_bucket)`; creation time/reservation ID with current projected status/revision.
- Dedicated book journal, amount/type and reconciliation query indexes when those filters are exposed. Filtering SHALL use a qualified indexed partition path or bounded PostgreSQL query, never `ALLOW FILTERING` or an unbounded client scan.

Projection writes SHALL be idempotent and revision-fenced. Actual CQL fields and cursor DTO manifests SHALL be pinned and tested in PF-FIN-01 before schema release. Queries spanning months SHALL bound the requested range and continuation state; max page size 100, max initial history window 366 days. Larger exports SHALL be explicit asynchronous export operations with their own limits.

Authoritative receipt/current reservation/available funds queries SHALL read PostgreSQL through actors. Scylla status SHALL expose its financial revision and projection timestamp; a missing row is not evidence that a posting or hold failed. Trial balance SHALL use one coherent accounting cut, not add unrelated eventually updated Fund snapshots.

## 41. Atomic financial lifecycle and recovery

### 41.1 Successful operation

1. Parse/validate shape, authenticated authority, hash, bounded payload and deadlines through the relevant Function or Command maps.
2. Functions load completed state; Commands load current aggregate state and the requested operation receipt. Same-input committed attempts replay their original outcome without rewinding later state; conflicting reuse fails. Resolve source identity independently of command/operation identity.
3. Materialize required immutable source/valuation/risk evidence outside the financial transaction. Do not query QuickBooks, broker, pricing or other actors while holding locks.
4. Enter the opt-in shared transactional completion stage. Lock the Portfolio authority row, then read current policy/period/ownership, balances, source receipts and relevant usage under that fence.
5. Recheck expected revision, admission/revocation epoch, operation-specific permissions, expiry and all domain invariants. Compute exact posting/reservation effects from qualified evidence.
6. Write journal/lifecycle records, balances/usage, receipt/source identity and the authoritative event on the same transaction: completed Function event or committed Command domain outcome at the expected stream revision. Increment financial revision once per logical mutation.
7. Commit before updating in-memory state. Functions finalize completed state and map/return Complete. Commands advance continuing state and arrange durable correlated outcome delivery/projection; receipt queries can confirm commit before notification arrives. Post-commit telemetry, delivery or reply failure MUST NOT relabel a committed mutation as rollback.

Policy/mandate/period changes affecting spending SHALL participate in this same protocol when becoming authoritative. An eventual projection of an updated policy is insufficient fencing. Admission must verify current authority even when a frozen assessment remains within its own expiry. The ledger snapshot and capacity snapshot SHALL carry compatible financial revision/watermarks.

### 41.2 Failure and uncertainty

| Situation | Required behavior |
| --- | --- |
| Shape/ownership/business admission refusal before commit | Typed Fail, no mutation; classify business refusal separately from infrastructure |
| Exception after account update but before commit | Roll back journal/balance/usage/receipt/completed event together |
| Competing request or event append uniqueness conflict | Re-read authoritative receipt; replay only identical content; otherwise Fail/Conflict with no new charge |
| Connection lost during COMMIT | Reconcile original operation/source identity; if unresolved return OutcomeUnknown, never NotCommitted |
| Commit known, response lost | Retry original identity returns stored completion; no new source posting/reservation |
| Caller cancellation/deadline while write is in flight | Observe late operation; do not assume cancellation rolled back; reconcile before a dependent retry |
| Scylla or external export unavailable | Financial commit remains authoritative; observation/export recovery is asynchronous |
| Expired replay | Original completion remains readable; receipt validity/current lifecycle governs consumption, not replay time |

The new financial persistence path SHALL replace independently committed financial writes/event appends for both actor types. Commands require enlisted event/business persistence and restartable post-commit delivery; Functions require opt-in shared transactional completion. Neither is already guaranteed by the current eventual projector. A transaction cannot live in a singleton context. Preserve existing calculation Function and unrelated Command behavior/regressions; no actor-specific timer helper.

Loading permits a separately bounded replay read. New financial execution/commit deadline is the minimum of request, source/authority, candidate and workflow expiry as applicable. Initial engineering limits: 1 MiB uncompressed request, 512 KiB result, 256 lines per journal, 100 journals per explicitly atomic batch within the same request limit, 100 entries per query page, 1 second replay-read budget and 2 seconds maximum new financial operation budget. Effective transport/source deadlines may be stricter. These are bounded test defaults, not measured production latency claims. No automatic deadline extension or unbounded retry.

Serialization/lock conflicts may retry the **same** transaction operation at most three times within its original deadline only after rollback is established. A changed risk assessment/capacity snapshot is a new workflow attempt with a new identity; an unknown previous commit must be reconciled first. Risk contention attempts remain bounded by the Risk design.

### 41.3 Capacity state invariants

Reserved -> Consumed occurs before external submission. Consumed becomes Working, SubmissionUnknown or reconciled terminal state based on execution evidence. Partial fills atomically transfer filled exposure to positions/accounting and retain residual commitment. Quantity conservation SHALL hold: original approved units equal filled plus confirmed-cancelled plus remaining units, with no negative or duplicate counts. Replacement cannot increase quantity or worsen price beyond authority without a new approved assessment.

Released/Expired are permitted only for an unconsumed hold with proof no execution commitment exists. Consumed/Working/PartiallyFilled/SubmissionUnknown/CancelPending cannot release by time alone. A consumed authorization's expiry prevents new submission but does not cancel its obligations. Filled completion does not release open-position risk; position close/reconciled accounting controls that later transition.

Reconciliation SHALL fence duplicate/out-of-order source facts with execution/source identity and revision. Gaps retain conservative exposure and block new spending as necessary. Corrective negative cash/over-limit exposure SHALL be recorded faithfully while new admission is blocked. A lifecycle transition affecting both accounting and hold usage SHALL commit together under the shared financial transaction, or retain the prior conservative commitment until its related posting is proven; no transient release window.

## 42. Risk Management and Fund order handoff

The fifth workflow stage SHALL consume the four accepted results for the single triggering horizon. Portfolio financial Functions are auxiliary services, not additional analysis stages. Risk decides whole-unit quantity; capacity admission verifies the exact candidate/quantity/requirements and current authority. It SHALL NOT select contracts or silently resize.

Add a versioned `FundRiskAuthorizationReference` with explicit schema, risk result/invocation identity, CompositionResultHash, UnitCandidateHash, RiskAssessmentHash, SizedOrderHash, ReservationId, ReservationCompletedEventId, StrategyUnits, FinancialRevision, AuthorityEpoch, ValidUntilUtc and ExecutionEnvironment. Preserve existing `RiskManagementResultReference` as historical compatibility: its CandidateSha256 currently compares with the stored composition **result** hash. Never repurpose that field as the unit-candidate hash.

Use a new typed Fund outcome command or append-only versioned payload that requires the new reference for financial approval. Legacy generic Approved results SHALL NOT enter new execution. Exact wire keys for the chosen Fund command and workflow append slots SHALL be verified against the current shared manifests before PF-FIN-01 release; no existing slots are reused.

The durable sequence SHALL be: accepted Composed and Fund composition reference -> accepted sized risk assessment -> atomic Portfolio reservation -> Fund risk outcome reference -> workflow acceptance with recoverable execution intent -> current reservation consumption -> execution request. Lost responses between these actors are reconciled through deterministic IDs. Each owner persists its own intent/acceptance; there is no distributed transaction across NATS and PostgreSQL.

Calculated rejection records normal NoTrade with zero units and no hold. Failure/timeout cannot start execution. If a hold may have committed before workflow stop, reconcile and release only if unconsumed. A stopped workflow cannot be revived by a late completion. The receipt is historical proof of its grant; current reservation state, expiry and policy fence determine whether execution can consume it.

## 43. Financial UI and query contracts

Keep the equal-width Portfolio/Fund/selected-Fund detail layout and the bottom metric rows. Add scoped Transactions, Balances and Reservations views plus journal/reconciliation detail. General Ledger administration is a Portfolio Actions entry for chart, journal, period and reconciliation management. No separate planned-composition viewer or manual risk-permission shortcut is introduced.

| UI action/view | Required contract and behavior |
| --- | --- |
| Transactions | Fund/date/kind/status/book/currency-scoped paged query; original source and immutable journal link; separate legacy label |
| Deposit | Record confirmed source/authorized attestation; accounts from rule; explicit amount/date/currency/description; await posting receipt |
| Withdrawal | Request and inspect encumbrance, then separate qualified settlement/cancellation evidence; cannot withdraw reserved cash |
| Transfer | Same Portfolio/book/USD only; validated source/destination Funds; one atomic operation |
| Adjustment/reversal | Select original journal and rule/accounts, reason and authorized effective date; preserve original lines |
| Balances | Settled cash, unsettled obligations, valuation/P&L, active holds, spendable/withdrawable amounts with exact basis/revision/as-of |
| Reservations | Original assessment/order/receipt, units/requirements, current lifecycle and expiry; reconcile unknown status; no unconditional release button |
| Journal/period/reconciliation | Immutable posted detail, period state and source differences; privileged close/reopen/resolution |

All operations SHALL use typed NATS APIs; no local optimistic balance mutation counts as success. Confirmed Complete may be displayed with History updating while Scylla catches up. OutcomeUnknown disables creation of a duplicate operation and exposes receipt reconciliation using the same identity. The UI SHALL preserve the pending ID across refresh/navigation/restart through the operations recovery mechanism.

Use generated read-only IDs and populated account/Fund/type/currency selectors. No user-entered codes. Enforce existing Dark Trading Theme, enabled white/disabled gray button text, uniform fonts and aligned spacing. Read-only posted entries cannot be edited or removed. Selection changes cancel/discard stale results and never display one Fund's balances under another Fund. Metrics SHALL label valuation/cash-flow method and freshness; raw legacy balance drawdown is not risk-authoritative drawdown.

Financial query APIs SHALL expose GetPostingReceipt, GetJournal, GetAccountBalances, GetFundTransactionsPage, GetTrialBalance, GetReconciliation, GetCapacityReservation, GetCapacityUsage and GetFundReservationsPage with exact scope. Receipt/state queries return explicit NotFound/Unknown/Unavailable, not an invented zero balance. Authenticate Portfolio/Fund access on every request and cursor continuation; source/company mappings cannot broaden access.

## 44. Legacy transaction migration requirements

Inventory and assign disposition to legacy `fund_transaction`, `fund_transaction_identity_v4`, `fund_transaction_timeline_v3`, `fund_balance_by_status_day_v3`, `fund_transaction_amount_v3`, `fund_transaction_projection_state_v3`, `fund_transaction_projection_mutation_v3`, `fund_transaction_write_mutation_v3`, `fund_transaction_write_ownership_v3` and the `fund.balance` dependency. Include all actor routes/producers, DTOs, APIs, UI, Trade event handlers, EOD jobs and report consumers. Mutation markers and read indexes are not imported as money.

A migration manifest SHALL bind source environment/database, Fund/account mappings, preserved source IDs, target Portfolio/Fund/book, posting rule versions, history mode, starting/ending watermarks, counts, sums, hashes, reconciliation exceptions, approvals and cutover revision. New JournalId may differ from legacy TransactionId, but the original identity SHALL remain immutable provenance with uniqueness preventing repeat import. Existing OrderId/TradeId identity meanings SHALL NOT be silently remapped or collide with new allocations.

Map every legacy category/sign combination explicitly. Quarantine missing Fund ownership, unknown currency or irrecoverable settlement/valuation meaning. Historical-only Draft test mandates remain ineligible for trading until an independently authorized real mandate/account mapping exists. Never fabricate a target Fund for orphan rows or make legacy history spendable merely by importing it.

Support two explicit modes: full reconstructed journal history from a known opening cut, or a reconciled opening journal with labelled historical records kept separately. Never include both the same historical postings and their closing balance as new money. Do not use a hidden suspense plug to conceal reconciliation errors. Signed totals, cash, realized/unrealized P&L, booked valuation and outstanding commitments SHALL reconcile independently.

Dry-run migration and actual import SHALL be idempotent by source identity. Before cutover fence legacy writes, drain/reconcile accepted events/background producers, capture the final watermark and reconcile delta history. Atomically mark the target Fund's financial writer mode Current only after its checks pass; new admission remains disabled while migration state is incomplete. No runtime fallback or simultaneous legacy/current financial writes.

Before new target writes, rollback may restore the fenced baseline. After target postings/holds exist, rollback requires explicit financial reconciliation and writer fencing; a route toggle to stale legacy balances is prohibited. Preserve original data/events and labelled historical UI until operator verification and release acceptance. No physical legacy deletion is authorized by this specification update.

## 45. QuickBooks and external accounting integration contract

This release SHALL define accounting entity/book/account mapping and export identities, but SHALL NOT implement external QuickBooks connectivity. Choose Online versus Desktop, company/region capabilities and authentication in that later connector plan. Portfolio/Fund IDs are not automatically company/legal-entity IDs. Credentials/tokens SHALL remain outside financial rows in the secret/integration configuration boundary.

Export only committed qualified accounting postings, as individual journals or a versioned summary policy with exact source inclusion. A durable export record SHALL bind destination, source journal IDs/cut, immutable payload hash, mapping version, external receipt and retry/reconciliation state. Capture export eligibility within the ledger commit or derive it from authoritative committed events using durable checkpoints; a process crash after commit cannot lose the accounting export permanently. No external HTTP call belongs inside the financial transaction.

Initial direction is outbound. Destination edits, bank-feed overlaps, unknown delivery and duplicate references SHALL become reconciliation exceptions, not overwrite IFM balances. Retry SHALL reconcile original destination identity before sending another posting; late corrections SHALL use linked correction entries. Summary export must prevent source inclusion in multiple batches. Holds are not exported as cash expenses. Internal model marks export only when their qualified accounting policy creates actual ledger entries.

QuickBooks outages SHALL leave internal financial authority and trading admission operational, with pending export visible. Testable fake destination contracts can qualify delivery logic without claiming a live connector. [Intuit's journal-entry model](https://static.developer.intuit.com/sdkdocs/qbv3doc/ippdotnetdevkitv3/html/124adc26-3988-e8ee-c447-c1ada39393fe.htm) supports account-linked debit/credit entries; actual product mapping remains later qualification.

## 46. Financial reasons, verification and delivery gates

### 46.1 Stable reason families

Financial Fail results SHALL distinguish `GL.CONTRACT.INVALID`, `GL.AUTHORITY.DENIED`, `GL.JOURNAL.UNBALANCED`, `GL.ACCOUNT.INVALID`, `GL.CURRENCY.UNSUPPORTED`, `GL.PERIOD.CLOSED`, `GL.CASH.INSUFFICIENT`, `GL.SOURCE.CONFLICT`, `GL.REVERSAL.EXCESS`, `GL.MIGRATION.UNRECONCILED`, `CR.CAPACITY.INSUFFICIENT`, `CR.AUTHORITY.REVOKED`, `CR.REVISION.CONFLICT`, `CR.REQUEST.MISMATCH`, `CR.RESERVATION.EXPIRED`, `CR.LIFECYCLE.INVALID`, `FIN.COMMIT.UNKNOWN`, `FIN.PERSISTENCE.FAILED` and `FIN.TIME.EXPIRED`. Stable numeric IDs SHALL be allocated distinctly in the complete registry before enabling routes. Diagnostic text does not determine retry or financial authority.

### 46.2 Minimum independent verification cases

| ID | Required evidence |
| --- | --- |
| FIN-T01 | USD debit 100/credit 100 commits; debit 100/credit 99.99 fails with no journal/balance/event mutation |
| FIN-T02 | Every legacy category/sign mapping, commission delta, valuation-to-realization and duplicate EOD; no cash/P&L double count |
| FIN-T03 | 1000 spendable cash, competing withdrawal 700 and hold 700: at most one admits; losing request cannot use stale 1000 |
| FIN-T04 | Competing Funds/server instances under one Portfolio risk limit; exact scope usage and policy-revocation race |
| FIN-T05 | Fail after ledger/usage updates before event append: rollback all; lost COMMIT/response: reconcile original receipt, no duplicate money |
| FIN-T06 | Same operation/source/hash replay and changed-content conflicts; source redelivery under a new CommandId cannot repost |
| FIN-T07 | Reversal maximum, immutable posted history, period close/reopen, late valuation and source-gap exceptions |
| FIN-T08 | One intra-Portfolio Fund transfer changes both dimensions and zero net Portfolio capital; cross-currency/foreign Fund fails |
| FIN-T09 | Actual settlement loss may create negative cash, is recorded once, and blocks new admission rather than disappearing |
| FIN-T10 | Reservation consume/partial fill/cancel race/unknown submission/expiry; quantity and exposure conservation |
| FIN-T11 | Original receipt replay after release/expiry does not grant execution; consumed holds cannot expire-release blindly |
| FIN-T12 | Real NATS/PostgreSQL/Scylla runs: enlisted atomic event/business commit, restart replay, projection outage/rebuild and exact authorization |
| FIN-T13 | Exactly two capacity Functions use five maps/typed contexts/policies/terminals; ledger/lifecycle use standard Command maps and list validation. Consume cannot enter the lifecycle Command route; existing Command/calculation Function regressions pass |
| FIN-T14 | Explicit DTO/event keys, legacy readers/hashes, decimal/culture normalization, payload bounds, overflow and checked identities |
| FIN-T15 | Migration manifests, counts/hashes/totals, opening-balance double-count prevention, orphan quarantine and one-writer cutover/recovery |
| FIN-T16 | UI committed/history-pending, unknown outcome retry, closed period, readonly reversal chain, stale Fund selection and labelled legacy history |
| FIN-T17 | Five pipeline stages to exact sized assessment, Portfolio hold, Fund/workflow acceptance and emulator consumption; no unreserved execution |
| FIN-T18 | External export source inclusion/retry/corrections and unavailable destination do not alter ledger authority; no live-connector claim |

Tests SHALL include unit and BDD business cases, real transport/storage integration, independent numerical/transaction verification and UI system coverage. Record exact passed/failed/skipped counts and source/environment. Measure bounded maximum posting/usage workloads and contention; no busy retry, blocking `.Result`/`.Wait()` or actor lock around provider I/O. Performance gains cannot relax accounting or replay invariants.

### 46.3 Required implementation-plan gates

| Gate | Exit requirement |
| --- | --- |
| PF-FIN-01 | Complete contracts/manifests, registry allocations, schema/constraints, actor APIs, all legacy posting-rule mappings and financial authority semantics |
| PF-FIN-02 | Shared atomic financial Command persistence and opt-in Function completion; rollback/unknown-commit/replay, durable Command outcomes and unchanged existing actor behavior |
| PF-FIN-03 | GeneralLedgerCommandActor single/batch posting, balances, encumbrances, periods, corrections and reconciliation |
| PF-FIN-04 | Reservation and consumption Functions plus lifecycle Command actor; shared fence, exact inputs and conservative uncertainty |
| PF-FIN-05 | Risk/Fund/workflow integration, current receipt consumption and Portfolio execution-fact reconciliation with labelled fixtures; actual emulator integration deferred |
| PF-FIN-06 | Complete producer/table migration, reconciled fenced cutover and new Portfolio financial UI |
| PF-FIN-07 | All required test layers, migration/reconciliation manifests, restart/rollback runbooks and explicit operational limitations |

The [Portfolio implementation plan v1.2, sections 15–22](./Portfolio-Fund-Implementation-Plan-v1.0.md#15-financial-phase-authority-scope-and-dependencies) now expands these gates with deliverables, guardrails, migration, operations and five-layer test traceability. Current PF-FIN statuses are recorded in implementation plan section 22; none is complete merely because its folder exists or an earlier PF gate passed. Actual IBKR connection and QuickBooks connector are later deliveries; their absence does not remove Portfolio execution-fact and internal export-boundary tests. Emulator behaviour and end-to-end integration tests await the future emulator design and implementation.

### 46.4 Definition of financial completion

The financial extension is complete only when journal/balance or hold/usage mutations and completed events commit atomically; retries cannot duplicate money or reservations; unknown outcomes reconcile safely; all required transaction producers and tables have an explicit migrated/legacy disposition; UI shows verifiable current financial state; and the Risk Manager handoff cannot execute an unsized, expired or unreserved trade. Preserve original PF release evidence separately and report any remaining operational qualifications honestly.

## Appendix A. Initial catalog

| Horizon | Underlying | Asset type | Trade family | Minimum assignment |
| --- | --- | --- | --- | --- |
| Daily | ES | Futures | Futures | One enabled directional template and composition profile |
| Weekly | ES | FuturesOptions | VerticalSpread | Bullish and bearish compatibility through configured template/profile variants |
| Monthly | ES | FuturesOptions | IronCondor | Neutral, bullish-bias, and bearish-bias compatibility as configured |

The three TradeStrategyFamily definitions are ReferenceDb configuration. Direction/bias examples are template/profile behavior, not additional v1 family rows. Adding templates later does not change Portfolio/Fund ownership or identity contracts.

## Appendix B. Current-to-target terminology

| Current/legacy term | New authoritative meaning |
| --- | --- |
| Fund | Legacy current Fund aggregate; new Fund is a PortfolioFund mandate |
| FundOrder | Legacy manual order plan; new FundOrder is a versioned composition identity |
| FundOrderTrade | Legacy manual trade plan; new FundOrderTrade is a planned composition instruction |
| TradeOrderReadModel | Current concrete/manual TradeDb order; future execution contract, not Portfolio record |
| FundDbContext | Legacy Fund projection context |
| PortfolioDbContext | New Portfolio/Fund Scylla projection context |
| TradeDbContext | Strategy workflow projections today and future execution truth; not Portfolio configuration authority |

## Appendix C. Non-negotiable boundary summary

```text
Portfolio policy + Fund mandate
    -> TradeSelection chooses template
    -> PortfolioFund reserves integer OrderId/TradeId
    -> OrderComposition creates exact candidate
    -> RiskManagement calculates final units or rejects
    -> CapacityReservation atomically commits capacity and completion
    -> Workflow accepts exact assessment/receipt and records execution intent

Future only:
    -> OrderExecution
    -> broker order/fills
    -> TradeDb live trade/position

GeneralLedger owns posted financial balances.
CapacityReservation owns holds/commitments against those balances.
Execution facts update accounting through authenticated idempotent ingestion.
Actual broker connection and external QuickBooks connector remain separate.
```

## Appendix D. Financial recovery and internal export implementation clarifications

- A replenishing deposit is a financial fact, not an admission reset. If the book is Overdrawn or NeedsReconciliation, successful journal/balance reconciliation checks every Fund's remaining cash after obligations. Any shortfall remains Overdrawn. A qualified solvent book moves to NeedsRefresh; only a subsequent source-validated authority refresh can restore Active. An unqualified book remains Importing.
- Every history page is limited to 100 records. Capacity admission loads only the requested scope/measure/unit keys, at most 256; a source invocation with more than 64 committed events is rejected as unqualified evidence rather than loaded without a bound.
- Internal accounting exports pin a stable ExportId, Portfolio/book, source financial revision, up to 100 journals and exact account-version mapping. Journals retain their original identities, amounts and correction references. A company cannot include one journal in two exports. Payloads, source membership and delivery attempt facts are immutable. Stable attempt IDs distinguish failed, unknown and confirmed delivery without reposting money. Pending delivery is reconciled using the same payload and export identity. These internal contracts do not enable a QuickBooks connector.
- The new Financials viewer is read-only and displays panel-specific revisions. It is not the complete financial administration write UI. Missing financial authority must remain visibly unavailable; no Fund balance fallback or automatic capital migration is permitted.


## Development financial continuation - 2026-09-09

The current workflow boundary is committed financial authorization (`Authorized`), with an exact saved sized execution intent. It does not consume capacity or dispatch preliminary emulator admission. Future execution owns consumption immediately before submission. A bounded PostgreSQL workflow snapshot recovery scan reissues existing mapped commands with saved identities and fixed deadlines; malformed latest snapshots never cause fallback to older financial intent.

Portfolio Financials includes ledger control/configuration reads, period/reconciliation/retirement commands, durable pending-configuration recovery and development book setup. Setup uses committed current Funds and configured Portfolio execution-account choices, generates business keys and creates an unqualified Importing book only. It posts no capital and enables no spending. Named sequence gaps after abandoned preparation are valid. See the financial implementation manifests for default account/rule definitions and append-only DTO keys.

Canonical legacy source inventory is a streamed, resumable audit archive with immutable original payloads/hashes and explicit quarantine reasons. Its `UnfencedInventory` result is not a reconciled source cut, migration qualification or writer switch. Missing legacy currency, movement and correction evidence is never inferred from a destination book or development capital. Writer fencing, qualified import/cutover and their recovery evidence remain mandatory before migrating a scope.

The implementation plan section 22.9 records this continuation and its test evidence; no financial gate is closed solely by these additions. Production security and the future emulator remain separately deferred as previously agreed.


## Development authority and qualification clarification - 2026-09-09

The implemented development UI separates setup, explicitly entered opening capital, reconciliation, source qualification and spending-authority review. A fresh-scope qualification SHALL verify the trusted Development host, unqualified Emulator book, matched ledger reconciliation, exact source versions and a legacy writer fence followed by verified source absence. It SHALL record an immutable migration manifest and leave CanSpend false. Existing legacy history, pending/previous writes or missing source evidence SHALL NOT qualify as a fresh scope. No automatic capital amount or production opening capital is authorized.

Authority preparation SHALL use current committed Portfolio, Fund mandate, assignment, financial policy/envelope and exact published catalog deployment/product identities. The draft SHALL pin the current financial revision and epoch. Saving SHALL fail if those or committed source versions changed. Authority refresh SHALL preserve qualified Fund membership; adding a Fund to Portfolio administration alone does not qualify it for a financial book. A separate membership/source qualification remains required.

The exact deployment MaximumRiskPerTrade SHALL be carried separately from aggregate loss limits into Risk sizing and capacity admission. Missing legacy fields default to zero and SHALL deny new spending, without invalidating replay of an existing committed receipt. Financial risk references SHALL expire no later than their portfolio, mandate, assignment, policy and envelope sources.

Underlying exposure SHALL aggregate by normalized published symbol/exchange/currency across expiring contracts, deployments, Funds and the single triggering Daily/Weekly/Monthly timeframe. Contract matching remains a separate pricing/evidence check. Per-market delta, gamma and vega units SHALL be explicit; shared limits SHALL be consistent across Funds and use the most restrictive enabled prepared cap. Old nonzero contract-scoped usage requires reconciliation before enabling product-scoped authority.

The authority review screen SHALL show the prepared constraints and whether each Fund is enabled, start with permission for new spending unchecked, require an audit reason and persist the original ConfigureLedgerCommand before dispatch. Changing the spending option invalidates the draft. A lost response SHALL use the existing Pending recovery journey; queued is not committed.

The implementation/evidence status is in plan section 22.10. Historical migration and complete multi-host/five-stage qualification are not implied by the fresh-scope implementation. Production security and broker-emulator behavior remain deferred by the owner's scope clarification.

### Owner decision - 2026-09-09: retained read-only history and separate development capital

The selected migration mode is `ReadOnlyHistoryWithDevelopmentCapital`. Retain all original legacy records for read-only viewing, including unknown/unqualified records with their labels. Preserve original identities, dates, decimal precision, descriptions and source amounts. Missing currency remains unrecorded. No legacy amount, balance, opening-trade snapshot, correction or aggregate becomes a GeneralLedger posting or available cash.

Development capital is entered separately through the existing explicit DevelopmentOpeningCapital posting journey, restricted to a Development host and an unqualified Emulator book. There is no inferred funding amount or automatic spending activation. Existing PF-31 historical mappings remain permanent Draft; new development Funds use their own generated identities and separately qualified books. Shared numerical Fund IDs do not imply a historical mapping.

This resolves the previous request for a choice between legacy posting reconstruction and read-only retention. Reconstruction of historical journals is outside the selected delivery. Retained source disposition still requires verified writer fencing/drain and immutable inventory evidence; this decision does not waive pending writes, open obligations, recovery or gate verification. No application data has been cut over by this documentation change.
