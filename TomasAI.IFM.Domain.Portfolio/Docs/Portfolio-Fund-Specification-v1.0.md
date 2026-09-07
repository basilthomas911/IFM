# Portfolio and Fund Detailed Specification v1.1

> **Implemented catalog replacement (2026-09-06):** ConfigurationDb now owns active strategy catalog authoring. Reference Data Manager edits all seven catalog sections, including balanced/directional variants; Portfolio mandates, assignments and policy limits use exact deployment GUID/version references. Existing family records are imported as Drafts without automatic permissions. The old family UI/write path is Legacy; historical contracts remain readable. [Integration details](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md) and [UI guide](../../TomasAI.IFM.UI.Net/Docs/Strategy-Catalog-Reference-UI.md) supersede the older family-authoring descriptions below. TradeSelection execution remains on hold.

> **Strategy catalog direction (2026-09-06):** New reusable strategy families, strategy definitions, structures, variants and deployments are planned in PostgreSQL ConfigurationDb. Portfolio continues to own Fund assignments, permissions and financial/risk limits. Existing exact ReferenceDb family IDs/versions and risk-limit keys remain compatibility contracts until a versioned migration maps them explicitly; new variants must not expand Fund permission implicitly. Earlier three-family restrictions below describe the original PF scope, not the new catalog taxonomy. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

**Status:** Draft revision for review; v1.0 implementation evidence remains historical
**Date:** 2026-08-30
**Supersedes:** The approved v1.0 contract where this revision explicitly changes PortfolioCode, policy, reference-family, or UI behavior
**Domain:** `TomasAI.IFM.Domain.Portfolio`  
**Authoritative design:** [Portfolio-Fund-High-Level-Design-v0.1.md](../../Documents/system/Portfolio-Fund-High-Level-Design-v0.1.md)  
**Related TradeSelection design:** [TradeSelection-High-Level-Design-v0.1.md](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/Docs/TradeSelection-High-Level-Design-v0.1.md)\
**Implementation plan:** [Portfolio-Fund-Implementation-Plan-v1.0.md](./Portfolio-Fund-Implementation-Plan-v1.0.md)  
**Runtime target:** .NET 10, MessagePack, NATS Core/JetStream, PostgreSQL EventSourceDb and SequenceIdDb, and ScyllaDB projections  
**Implementation boundary:** Reference trade-family catalog, Portfolio/Fund and Risk Policy configuration, unified Trade Orders composition view, and composition identity through accepted OrderComposition result references
**Deferred boundary:** Broker OrderExecution, fills, live positions, and execution-facing TradeDb replacement

**Construction/sizing clarification — 2026-09-05:** The [Trade Strategy Builder design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/Trade-Strategy-Builder-Design-v1.0.md) specifies complete one-unit OrderComposition results with final unit quantity absent. Portfolio Risk Manager determines final units and atomically reserves risk. Recording result references does not implement this sizing engine or authorize execution; existing historical contracts/results are not silently reinterpreted.

## 1. Purpose

This specification converts the Portfolio/Fund high-level design into repository-specific, testable requirements. It defines the domain types, actor boundaries, commands, events, queries, state transitions, persistence projections, pipeline handoffs, UI contracts, reason codes, gates, and definition of done needed to implement the new Portfolio-centric model.

The specification preserves the existing conceptual separation:

- Portfolio owns capital and financial-risk authority;
- Fund owns mandate, template assignments, selection guidance, and planned composition identities;
- TradeSelection selects a permitted template;
- OrderComposition creates an exact non-executable one-unit candidate with per-unit leg ratios;
- RiskManagement determines final units, independently validates and reserves/approves risk, or rejects; and
- future OrderExecution owns broker effects and execution-facing TradeDb records.

## 2. Normative language

`MUST`, `MUST NOT`, `REQUIRED`, `SHALL`, and `SHALL NOT` are mandatory requirements. `SHOULD` describes the expected implementation unless a reviewed exception is documented. `MAY` is optional.

The HLD controls domain intent. This specification controls the initial repository contract. If the two conflict, implementation stops until the documents are reconciled; code must not silently choose one interpretation.

## 3. Fixed decisions

1. The new domain hierarchy is `Portfolio -> Fund -> FundOrder -> FundOrderTrade`.
2. A Fund version has exactly one Portfolio parent.
3. Portfolio configuration and Fund mandate changes are versioned and append-only from a business perspective.
4. Existing Fund actors, contracts, tables, and UI are legacy and are not production authority for the new domain.
5. No migration, dual read, dual write, or backward-compatibility adapter is required unless separately approved.
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
21. ReferenceDb owns the existing versioned product/timeframe `TradeStrategyFamily` compatibility catalog. The three original seeds are preserved; current commands support additional product-linked definitions. The proposed reusable strategy/structure/variant catalog belongs to ConfigurationDb.
22. A PortfolioFinancialPolicy contains Portfolio-wide hard limits plus one versioned `TradeFamilyRiskLimit` row per configured family. Family limits may reduce but never enlarge the global limits.
23. The current Reference editor supports Add/Change/Remove for legacy product/timeframe family definitions. Reusable strategy-definition/variant authoring and Portfolio deployment assignments require the new ConfigurationDb design and a separate versioned migration.
24. Portfolio Administration uses a compact command bar with Risk Policy as a primary action and no Planned Compositions action.
25. Trade Orders is the only UI for manual and Strategy Workflow compositions. The separate Portfolio composition viewer is removed.

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
- observability, authorization points, tests, and implementation gates.

### 4.2 Excluded

- broker submission, acknowledgement, replace, cancel, or reconciliation;
- broker order IDs;
- automated or manual broker fill processing;
- live TradeDb order, trade, or position creation;
- position valuation or market-feed subscriptions;
- RiskManagement calculation details;
- OrderComposition algorithms for futures, verticals, or Iron Condors;
- migration of legacy Fund history;
- deletion of legacy tables or UI;
- non-ES initial template definitions beyond extensibility contracts;
- high-throughput tick-table key or sequence redesign;
- new ConfigurationDb strategy-catalog management UI (existing Reference family management is documented separately);
- implementation of the new reusable strategy/structure/variant catalog; its accepted design includes independent side, bias and credit/debit choices;
- scheduled future policy activation; and
- a generic policy formula, script, or conditional-rule engine.

## 5. Required solution topology

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

RiskManagement receives the exact candidate plus frozen Portfolio policy and FundRiskEnvelope. Portfolio/Fund records only the accepted RiskManagement result reference and state transition. The Portfolio domain does not duplicate the risk calculation.

RiskManagement applies the most restrictive remaining Portfolio-wide limit, TradeFamilyRiskLimit, FundRiskEnvelope, and current-capacity value. A disabled, missing, mismatched, or stale family fails closed. No family row can enlarge the global policy or delegated Fund envelope.

`RiskApproved` is terminal for the Portfolio/Fund implementation boundary. No OrderExecution command is emitted by this implementation unless the separately approved strategy-workflow execution handoff is later enabled.

## 27. UI requirements

### 27.1 Navigation

- Keep existing Funds navigation during transition.
- Add Portfolio navigation.
- Label legacy navigation clearly before production cutover.
- Do not remove the old UI until new system gates pass.

### 27.2 Portfolio view

The Portfolio Administration command bar SHALL expose exactly four visible actions: Refresh, New Portfolio, Risk Policy, and Portfolio Actions. `Show State` is labeled as a list filter. Portfolio Actions contains New Portfolio Version, Change Operating State, and context-valid Delete Draft only. Planned Compositions is absent.

The existing Funds, Allocation, Risk Envelope, and Trade Assignments detail tabs remain. The command bar retains black background, white title/foreground, and a visible gray border.

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

Every mutation records the authenticated principal. Broker credentials, API keys, and secrets are forbidden in contracts, events, logs, projections, and UI DTOs.

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
5. Bulk legacy history migration or deletion; PF-31 permits only explicit read-only mapping and cross-context query composition.
6. Legacy Funds UI removal.
7. Multi-asset and unrestricted multi-template ranking.
8. Advanced Portfolio optimization beyond approved hard limits.
9. High-throughput ScyllaDB sequence/tick identity review.
10. Operator-facing integer-width expansion beyond current checked Int32 contracts.
11. TradeStrategyFamily mutation commands and management UI.
12. Trade-strategy variants/subtypes including Long, Short, bullish, bearish, neutral, debit, and credit.
13. Scheduled PortfolioFinancialPolicy activation.

Deferred work cannot be implemented accidentally inside a PF gate.

## 36. Legacy Test Portfolio history query

PF-31 adds an explicit, non-authoritative history adapter. Imported Fund mandates append `HistoricalSource` and `HistoricalSourceFundId` to the MessagePack/JSON contract, remain Draft forever, and use newly allocated Portfolio/Fund IDs. The original IDs are never reused as new authority.

The Portfolio NATS query actor exposes typed queries for legacy scopes, source Fund catalog, bounded FundOrders, and FundOrderTrades with hydrated TradeDb execution evidence. The adapter may read `FundLegacyDbContext` and `ITradeDbReadContext`; it exposes no write context. Matching uses the unambiguous source `(OrderId, TradeId)` pair and returns `NoTradeDbDefinition`, `DefinitionOnly`, `PositionHistory`, or `FillHistory`. Any order/composition FundId absent from the source Fund catalog is returned as a separately queryable unassigned entry using its original source FundId. Such entries are never mapped as canonical Funds.

Trade Orders SHALL default to `Current`. `Legacy History` is an explicit mode that clears stale scope state, uses source-labelled DTO collections, broadens the operator date range for historical browsing, and disables Create Order, Add/Remove Trade, state changes, submit, fill, live feed, End-of-Day, and position mutations. Returning to `Current` restores the existing canonical PortfolioDb query path. Delayed responses are fenced by mode, Portfolio, Fund, and generation.

Single-selecting a supported legacy Iron Condor SHALL render the original four-leg `IronCondorTradeOrderView` in the lower Trade Orders detail region without closing Trade Orders. The lower editor SHALL show the stored TradeDb-backed leg actions, expirations, strikes, quantities, bid/ask values, spreads, probabilities, limits, and trade values; it SHALL NOT substitute the graph-enabled operational `IronCondorView`. Selection replacement SHALL await closure and disposal of the preceding editor and SHALL fence delayed work by selection generation. A missing TradeDb definition SHALL show only `No corresponding TradeDb trade exists for OrderId:TradeId`; an unsupported type or unavailable exact base contract SHALL show a concise editor-unavailable message. The historical editor SHALL consume the hydrated TradeDb option trade returned by the legacy query and the source legacy Fund balance without making current reference, Fund, feed, or market-data queries during load. Every text input SHALL be read-only, selectors/date/quantity/action controls SHALL be disabled, and submission, removal, risk generation, order-action lookup, and live-feed entry points SHALL fail closed before service execution. Its value date SHALL retain the original editor semantics: opening compositions use the FundOrder trade date, closing compositions use the FundOrderTrade trade date, and an unavailable date falls back to the TradeDb trade date. `View Legacy Trade` and legacy-trade double-click SHALL continue returning the accepted historical selection, source Fund, and source FundOrder to the main application. The main application SHALL create or activate one graph-enabled middle-screen tab named `OrderId:TradeId`; repeated opening SHALL not duplicate that legacy tab, and its separate immutable historical-read-only boundary SHALL remain in force. The Trade Orders form SHALL use a compact resizable layout with a 900-pixel default client height and an adaptive detail region.

## 35. Definition of done

The specification is implemented only when:

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
    -> RiskManagement approves/rejects
    -> STOP for this implementation

Future only:
    -> OrderExecution
    -> broker order/fills
    -> TradeDb live trade/position
```
