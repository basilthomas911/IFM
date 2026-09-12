# Generic Trade Order Backend Schema Design

Date: 2026-09-12  
Version: 1.1  
Status: Superseded by `Trade-Order-Execution-Trade-Position-Backend-Schema-Design-v1.2.md`  
Scope: `TradeOrder`, `OptionTrade`, `FuturesTrade`, generic and typed legs, actors, messages, event schemas, TradeDb projections, execution integration, migration, and backend verification  
UI status: Legacy Trade Order UI replacement is explicitly deferred

> **Supersession notice:** Version 1.1 incorrectly modeled `OptionTrade` and `FuturesTrade` as children already owned by `TradeOrder`. Version 1.2 establishes the corrected lifecycle: Trade Order intent -> Execution and accepted fills -> durable typed Trade -> position history.

## 1. Purpose

This document defines a common backend schema in which:

- `TradeOrder` is the durable order and approval envelope;
- `OptionTrade` is a first-class option trade definition with one or more option legs;
- `FuturesTrade` is a first-class futures trade definition with exactly one futures leg in its standard form;
- one Trade Order may contain several trades, allowing different strategies within one custom order;
- manual and automatic broker execution both live under `Domain.Trade/Order/Execution`; and
- live position state remains outside all three order/trade definition models.

This is a breaking source-model replacement. Historical compatibility is handled explicitly through readers and migration adapters. The backend must be organized correctly before rebuilding the legacy Trade Order entry UI.

## 2. Corrections to version 1.0

Version 1.0 proposed retiring `OptionTrade`. That decision is superseded.

`OptionTrade` and `FuturesTrade` are useful durable economic definitions because they enforce different topology and instrument rules. They are not duplicate order roots. Both implement the same common trade contract and are children of a generic Trade Order.

The corrected hierarchy is:

```text
TradeOrder
  ├── OptionTrade
  │     └── 1..N OptionTradeLegs
  ├── FuturesTrade
  │     └── exactly 1 FuturesTradeLeg
  └── future CustomTrade
        └── 1..N typed Trade components and legs
```

For a custom order with different strategies on different legs, each strategy is represented by a separate child trade. A strategy applying to one leg is a single-leg child trade. This prevents a leg from carrying ambiguous strategy ownership while still supporting the requested one-strategy-per-leg case.

## 3. Verified current baseline

The current repository establishes these constraints:

1. The old Trade Order command handlers, decorators, exceptions, bounded context, and bounded-context state were removed.
2. Legacy shared Trade Order commands/events/read models and storage operations remain.
3. The current `TradeOrderReadModel` embeds `OptionTradeLegReadModel[]`; it is not instrument-neutral.
4. `OptionTradeFactory` accepts the legacy Trade Order and constructs only Long/Short Iron Condors.
5. Current `OptionTrade` owns static definition, lifecycle, fills, limits, trade positions, and dynamic option-leg data.
6. No equivalent `FuturesTrade` aggregate/model currently exists in `Domain.Trade/Model`.
7. The current `trade_order` table has no schema version or exact catalog keys and is keyed by trade ID/value date rather than a stable root order identity.
8. The current `option_leg` table assumes option-only fields and uses contract ID as part of identity.
9. Order Composition already produces generic `CompositionCandidate`/`CompositionLeg` data with exact Deployment, Strategy, Structure, and Variant catalog keys.
10. Existing Order Execution and IBKR documents already define the safety-critical execution workflow and broker boundary.

The new schema must replace the missing Trade Order implementation and reorganize, rather than preserve, the current OptionTrade coupling.

## 4. Core ownership

| Model | Owns | Does not own |
| --- | --- | --- |
| `TradeOrder` | Order identity, portfolio/fund, origin, approval evidence, execution envelope, order lifecycle, child trades, execution binding | Broker state, fills, current position, live prices, P&L |
| `OptionTrade` | Option strategy identity, exact catalog references, underlying, maturity, immutable option-leg topology, trade-level constraints | Order lifecycle, broker execution, fills, current leg marks/Greeks, live P&L |
| `FuturesTrade` | Futures strategy identity, exact catalog references, immutable single futures leg, trade-level constraints | Order lifecycle, broker execution, fills, current futures price/P&L |
| `OrderExecution` | Manual/broker attempt, submission, changes, cancellation, acknowledgements, fills, commissions, reconciliation, exposure, handoff | Approval topology changes and live position calculation |
| Strategy position actor | Confirmed position quantities, opening basis, current observations, P&L, Greeks, readiness | Order approval and broker mutation |

## 5. Aggregate hierarchy

### 5.1 Trade Order root

One `TradeOrder` contains one or more `ITrade` children. It is the atomic approval and execution-policy boundary. It may result in one broker combo, one native order, or several explicitly approved execution groups.

### 5.2 Common trade definition

`ITrade` supplies the fields shared by `OptionTrade` and `FuturesTrade`:

- stable trade identity within the order;
- trade ordinal;
- trade kind/discriminator;
- role (`Primary`, `Hedge`, `Adjustment`, `Compensation`, `Custom`);
- exact Deployment, Strategy, Structure, and Variant catalog keys;
- strategy runtime capability/position-owner key;
- unit quantity and ratio relative to the parent order;
- side, bias, and premium mode where defined;
- underlying instrument identity;
- trade/maturity dates where applicable;
- immutable leg collection;
- trade definition hash;
- creation/audit metadata.

### 5.3 Option trade

`OptionTrade` implements `ITrade` and enforces option topology. It may represent:

- a single option;
- a two-leg vertical;
- a four-leg Iron Condor;
- another supported option structure whose exact catalog capability is installed; or
- an option-only custom structure explicitly defined by the catalog.

Every leg is an `OptionTradeLeg`. All legs share the parent OptionTrade strategy unless the parent Trade Order contains separate OptionTrade children.

### 5.4 Futures trade

`FuturesTrade` implements `ITrade` and initially contains exactly one `FuturesTradeLeg`. It supports Long/Short through generic Buy/Sell side and opening effect. It records the exact Future structure/variant and futures contract definition.

Spreads across futures expiries or a future plus hedge are represented as multiple trades under one parent order until a dedicated multi-future structure capability is designed. This keeps the one-leg FuturesTrade invariant clear.

### 5.5 Future custom trade

A future `CustomTrade` can implement `ITrade` when one strategy genuinely owns mixed instrument types. It is not necessary for the initial backend because a Trade Order can already contain multiple OptionTrade/FuturesTrade children with distinct strategies.

## 6. Identity design

### 6.1 `TradeOrderId`

```text
TradeOrderId
  PortfolioId : int
  FundId      : int
  OrderId     : int
```

`OrderId` remains the upstream reserved integer. Portfolio and Fund provide tenancy/authority context. `TradeId` and `ValueDate` are not part of the root identity.

### 6.2 `TradeId`

```text
TradeEntityId
  TradeOrderId
  TradeId : int
```

Every child trade retains an upstream-reserved integer Trade ID. The existing `OrderId + TradeId` references can therefore migrate without renumbering.

### 6.3 `TradeLegId`

```text
TradeLegId
  TradeEntityId
  LegId : Guid
```

`LegId` is created once when the trade definition is frozen. Contract ID is not identity. The same contract can legally appear in separate trades or future custom allocations.

### 6.4 Execution identities

- `ExecutionAttemptId`: one execution lifecycle for one exact order revision.
- `ExecutionGroupId`: one native/broker/manual execution grouping inside an attempt.
- `ExecutionOperationId`: one submit/modify/cancel/manual evidence operation.
- `BrokerOrderKey`: normalized broker order identity.
- `TradeFillId`: one immutable fill or correction record.
- `PositionHandoffId`: idempotent exposure-to-position transfer.

## 7. `TradeOrder` backend model schema

### 7.1 Required fields

| Group | Fields |
| --- | --- |
| Contract | `SchemaVersion`, `Revision`, `DefinitionHash` |
| Identity | `TradeOrderId`, `ValueDate` |
| Origin | `Origin`, `CreatedOnUtc`, `CreatedBy`, correlation/causation IDs |
| Workflow | Workflow ID/revision, Composition result ID/hash, Risk result ID/hash |
| Authority | Portfolio assignment version/hash, approval ID/version/hash/principal/time |
| Catalog | Optional parent custom Deployment/Strategy/Structure/Variant keys and dependency hash |
| Lifecycle | `Status`, approved/valid-until/cancelled/expired timestamps and reason |
| Execution | Channel policy, grouping, order type, TIF, atomic/legging policy, quantity, price/cost/time/freshness bounds, policy versions |
| Children | `ITradeCollection Trades`, execution groups, topology hash |
| Binding | Active execution attempt ID and binding revision when present |

### 7.2 Root invariants

1. At least one child trade exists.
2. Trade IDs and ordinals are unique within the order.
3. Every child trade belongs to this exact order ID.
4. Every leg ID is unique across the entire order.
5. Every execution group references existing legs exactly once unless explicit multi-group compensation policy permits otherwise.
6. Approval covers the same order revision, definition hash, topology hash, quantities, economics, and execution envelope.
7. A material change creates a new revision and invalidates approval.
8. An execution attempt binds one exact approved revision/hash.
9. Cancelled, expired, rejected, or failed orders cannot bind a new attempt without a new explicitly authorized revision.
10. Live market values, positions, fills, and broker status are absent from this model.

### 7.3 MessagePack layout

The new root uses a new contract rather than repurposing the legacy `TradeOrderReadModel` keys:

| Key | Field |
| ---: | --- |
| 0 | SchemaVersion |
| 1 | TradeOrderId |
| 2 | Revision |
| 3 | ValueDate |
| 4 | Origin |
| 5 | Status |
| 6 | WorkflowEvidence |
| 7 | ApprovalEvidence |
| 8 | ParentCatalogReferences |
| 9 | ExecutionEnvelope |
| 10 | Trades |
| 11 | ExecutionGroups |
| 12 | TopologyHash |
| 13 | DefinitionHash |
| 14 | ActiveExecutionAttemptId |
| 15 | ValidUntilUtc |
| 16 | CreatedOnUtc |
| 17 | CreatedBy |
| 18 | UpdatedOnUtc |
| 19 | UpdatedBy |
| 20 | CorrelationId |
| 21 | CausationId |

Keys are append-only. Child types are separately versioned.

## 8. Common `ITrade` schema

### 8.1 Required fields

| Field | Meaning |
| --- | --- |
| `SchemaVersion` | Child trade contract version |
| `TradeEntityId` | Exact parent order and Trade ID |
| `Ordinal` | Canonical deterministic order |
| `TradeKind` | Option, Future, or future supported kind |
| `Role` | Primary/Hedge/Adjustment/Compensation/Custom |
| Catalog references | Exact Deployment/Strategy/Structure/Variant keys |
| `RuntimeCapabilityKey` | Explicit factory/validator/position-owner capability |
| `UnitQuantity` | Approved number of strategy units |
| `ParentRatio` | Ratio to the order unit |
| `Side`, `Bias`, `PremiumMode` | Frozen strategy semantics where applicable |
| `UnderlyingInstrumentId` | Common underlying identity |
| `TradeDate`, `MaturityDate` | Definition dates |
| `Legs` | Immutable typed legs |
| `DefinitionHash` | Canonical child definition hash |
| Audit | Created/updated identity and UTC timestamps |

`ITrade` is a domain interface. MessagePack uses an explicit discriminated envelope rather than serializing arbitrary CLR interface types:

```text
TradeDefinitionEnvelope
  SchemaVersion
  TradeKind
  PayloadSchemaVersion
  Payload
  PayloadSha256
```

The allowed `TradeKind -> payload type` map is closed by the runtime capability registry. Unknown executable payloads fail validation.

## 9. `OptionTrade` backend schema

### 9.1 Responsibilities

`OptionTrade` retains these durable responsibilities:

- exact option strategy and structure definition;
- underlying futures/instrument identity;
- trade and maturity dates;
- primary/hedge/adjustment role;
- immutable option legs and canonical topology;
- strategy-unit quantity/ratio;
- trade-specific approved constraints; and
- mapping to the future Option strategy position owner.

It no longer owns:

- order placement/open/close state;
- fills or commissions;
- `ITradePositionCollection`;
- `OptionLegData` or current bid/ask/Greeks;
- current Trade P&L; or
- end-of-day position mutation.

### 9.2 Required OptionTrade fields

In addition to `ITrade`:

- option underlying contract ID;
- option expiry policy/effective maturity;
- structure pricing convention (`Debit`, `Credit`, or validated custom);
- same/different-expiry topology evidence;
- strategy payoff/risk-bound classification;
- `IOptionTradeLegCollection`;
- topology/canonical-order version; and
- target position actor type (`IronCondor`, `VerticalSpread`, future capability).

### 9.3 OptionTrade MessagePack payload

| Key | Field |
| ---: | --- |
| 0 | SchemaVersion |
| 1 | TradeEntityId |
| 2 | Ordinal |
| 3 | Role |
| 4 | DeploymentKey |
| 5 | StrategyKey |
| 6 | StructureKey |
| 7 | VariantKey |
| 8 | RuntimeCapabilityKey |
| 9 | UnitQuantity |
| 10 | ParentRatio |
| 11 | Side |
| 12 | Bias |
| 13 | PremiumMode |
| 14 | UnderlyingInstrumentId |
| 15 | TradeDate |
| 16 | MaturityDate |
| 17 | PricingConvention |
| 18 | RiskBoundClassification |
| 19 | PositionActorType |
| 20 | OptionLegs |
| 21 | TopologyHash |
| 22 | DefinitionHash |
| 23 | CreatedOnUtc |
| 24 | CreatedBy |
| 25 | UpdatedOnUtc |
| 26 | UpdatedBy |

### 9.4 `OptionTradeLeg`

| Field | Requirement |
| --- | --- |
| Identity | Stable `TradeLegId`, ordinal |
| Instrument | Contract/instrument ID, underlying ID, asset/instrument class |
| Economics | Buy/Sell side, Open/Close effect, ratio, approved quantity, multiplier |
| Option | Call/Put right, strike, expiry, style/settlement when material |
| Market rules | Venue, currency, trading class, tick-rule reference/version |
| Catalog topology | Structure leg key and expiry-group key |
| Evidence | Contract-definition reference/hash, leg-definition hash |

Long/Short is derived from Buy/Sell plus opening effect. Quantity remains positive.

### 9.5 Topology rules

- Single option: one option leg when supported.
- Vertical: two legs with catalog-defined rights, sides, strike relationship, expiry relationship, and ratios.
- Iron Condor: four legs under one OptionTrade strategy; component verticals are calculations inside its position model.
- Other option structures: exact versioned capability and catalog topology required.
- No global one/two/four-leg rule; each structure defines its supported count and maximum.

## 10. `FuturesTrade` backend schema

### 10.1 Responsibilities

`FuturesTrade` owns the durable definition of a futures strategy trade:

- exact future strategy/structure/variant;
- immutable futures contract leg;
- Long/Short economic direction;
- contract month/expiry/last trading evidence;
- multiplier, tick rule, venue, and currency;
- unit quantity and approved constraints; and
- mapping to `FuturesTradePositionCommandActor` or another explicit position capability.

It owns no current price, mark-to-market P&L, execution fill, or broker state.

### 10.2 FuturesTrade MessagePack payload

| Key | Field |
| ---: | --- |
| 0 | SchemaVersion |
| 1 | TradeEntityId |
| 2 | Ordinal |
| 3 | Role |
| 4 | DeploymentKey |
| 5 | StrategyKey |
| 6 | StructureKey |
| 7 | VariantKey |
| 8 | RuntimeCapabilityKey |
| 9 | UnitQuantity |
| 10 | ParentRatio |
| 11 | Side |
| 12 | Bias |
| 13 | UnderlyingInstrumentId |
| 14 | TradeDate |
| 15 | MaturityDate |
| 16 | PositionActorType |
| 17 | FuturesLeg |
| 18 | DefinitionHash |
| 19 | CreatedOnUtc |
| 20 | CreatedBy |
| 21 | UpdatedOnUtc |
| 22 | UpdatedBy |

### 10.3 `FuturesTradeLeg`

| Field | Requirement |
| --- | --- |
| Identity | Stable `TradeLegId`, ordinal zero for standard Future |
| Instrument | Authoritative futures instrument/contract ID and root |
| Direction | Buy/Sell side and Open/Close effect |
| Sizing | Positive approved quantity, ratio one initially, multiplier |
| Contract | Contract month, expiry, last-trading UTC, settlement attributes |
| Market rules | Venue, currency, trading class, tick-rule/version |
| Evidence | Contract-definition reference/hash and leg-definition hash |

### 10.4 Futures invariants

1. Standard `FuturesTrade` has exactly one leg.
2. The leg instrument class is Future.
3. It has no option right or strike.
4. Trade side and leg side agree with the selected Long/Short variant.
5. Contract expiry and last-trading time are after approval and satisfy roll policy.
6. Quantity, multiplier, and tick rule are positive/valid.
7. The exact contract is permitted by the selected deployment/product assignment.

## 11. Multiple strategies in one Trade Order

Different strategies are represented as different `ITrade` children:

```text
TradeOrder 500
  Trade 1001: FuturesTrade / Long ES future / one leg
  Trade 1002: OptionTrade / ES put vertical hedge / two legs
  Trade 1003: OptionTrade / independent call strategy / one leg
```

This supports:

- one strategy for the whole order;
- different strategies per trade;
- a distinct strategy for each leg by using one-leg trades;
- several legs sharing one strategy in a multi-leg OptionTrade; and
- future mixed custom structures.

The parent `TradeOrderExecutionGroup` determines whether trades execute atomically together, in ordered groups, or independently. Cross-strategy atomic execution is permitted only when the broker adapter and approved envelope support the exact grouping.

## 12. Collections and immutability

Required collections:

- `ITradeCollection` containing typed `ITrade` definitions;
- `IOptionTradeLegCollection`;
- the single `FuturesTradeLeg` exposed consistently through common leg enumeration;
- `ITradeOrderExecutionGroupCollection`.

Collections are mutable only within a draft builder. Once the Trade Order is approved/frozen:

- arrays/topology cannot change;
- mutable lists do not escape;
- lookup by Trade ID and Leg ID is allocation-free;
- contract lookup returns all matches and does not assume uniqueness;
- enumeration is canonical by explicit ordinal;
- canonical hashes include every economic and routing field.

## 13. Execution envelope and groups

`TradeOrderExecutionEnvelope` contains:

- allowed execution channels;
- provider/account/environment constraints;
- order type and time in force;
- order-unit quantity and maximum quantity;
- approved signed price and reservation price conventions;
- maximum slippage, commission, fees, and duration;
- market-data freshness limits;
- allow-legging and atomic requirements;
- partial-fill and compensation boundaries;
- deterministic policy, constraint, pricing, and market-rule versions;
- validity time and envelope hash.

`TradeOrderExecutionGroup` contains:

- stable group ID and ordinal;
- grouping type (`Single`, `AtomicCombo`, `Sequential`);
- referenced Trade IDs and Leg IDs;
- group quantity/ratio;
- group price convention/bounds;
- dependency/sequence constraints;
- allowed channels/providers; and
- definition hash.

The execution actor cannot regroup legs without a new approved order revision.

## 14. Actors and backend folders

### 14.1 `TradeOrderCommandActor`

A standard event-sourced actor owns the Trade Order aggregate. It validates, freezes, approves, cancels/expires, binds execution, and records final execution completion. Trade Order is low frequency and does not use resident high-frequency batching.

```text
Domain.Trade/Order/
  Command/Actor/
  Command/State/
  Command/Handlers/
  Command/Extensions/
  Command/Validation/
  Command/Exceptions/
  Command/EventProjector/
  Event/Actor/
  Event/Handlers/
  Event/Extensions/
  Query/Actor/
  Query/Handlers/
  Query/Extensions/
  Docs/
```

### 14.2 `OrderExecutionCommandActor`

Both manual and broker execution live here:

```text
Domain.Trade/Order/Execution/
  Command/Actor/
  Command/State/
  Command/Handlers/
  Command/Extensions/
  Event/Handlers/
  Event/Extensions/
  Query/
  Model/
  Gateways/
    Manual/
    Broker/
  Docs/
```

One execution state machine uses `ExecutionChannel.Manual` or `ExecutionChannel.Broker`. Manual evidence and broker callbacks normalize into typed execution messages. Manual execution does not bypass approval, fill allocation, reconciliation, exposure classification, or position handoff.

### 14.3 Model folder

```text
Domain.Trade/Model/
  Order/
    ITradeOrder
    TradeOrder
    ITradeCollection
    TradeCollection
    ITrade
    TradeDefinitionEnvelope
    TradeOrderExecutionEnvelope
    TradeOrderExecutionGroup
    Builders/
    Validation/
    Hashing/
  Option/
    IOptionTrade
    OptionTrade
    IOptionTradeLeg
    OptionTradeLeg
    IOptionTradeLegCollection
    OptionTradeLegCollection
    Validation/
  Futures/
    IFuturesTrade
    FuturesTrade
    IFuturesTradeLeg
    FuturesTradeLeg
    Validation/
  Position/
  Strategy/
```

Calculations/topology validation live in Model folders. Actor classes contain maps and infrastructure lifecycle only. Message construction/sending uses extensions.

## 15. Commands and events

### 15.1 Trade Order commands

- `CreateTradeOrderCommand` with complete frozen candidate/approval package;
- `CreateManualTradeOrderDraftCommand`;
- `ReplaceTradeOrderDraftCommand` before approval;
- `SubmitTradeOrderForApprovalCommand`;
- `ApproveTradeOrderCommand`;
- `RejectTradeOrderCommand`;
- `MarkTradeOrderReadyForExecutionCommand`;
- `CancelTradeOrderCommand`;
- `ExpireTradeOrderCommand`;
- `BindTradeOrderExecutionCommand`;
- `ReleaseTradeOrderExecutionCommand` only after proven zero exposure;
- `CompleteTradeOrderExecutionCommand`; and
- `SnapshotTradeOrderCommand`.

### 15.2 Trade Order events

- `TradeOrderCreatedEvent` containing the entire canonical definition;
- `TradeOrderDraftReplacedEvent`;
- `TradeOrderSubmittedForApprovalEvent`;
- `TradeOrderApprovedEvent`;
- `TradeOrderRejectedEvent`;
- `TradeOrderReadyForExecutionEvent`;
- `TradeOrderCancelledEvent`;
- `TradeOrderExpiredEvent`;
- `TradeOrderExecutionBoundEvent`;
- `TradeOrderExecutionReleasedEvent`;
- `TradeOrderExecutedEvent`; and
- `TradeOrderSnapshotEvent`.

OptionTrade and FuturesTrade are child definition payloads. They do not have separate order-lifecycle command actors or duplicate placed/opened/closed events.

### 15.3 Execution messages

Execution messages follow the existing Execution specification and are generalized to Trade/Leg/ExecutionGroup IDs. Required families include:

- start/submit/modify/cancel/reconcile/resume;
- manual acknowledgement/fill/correction/cancellation;
- broker acknowledgement/status/fill/commission/error;
- exposure classified and compensation;
- terminal execution result; and
- position handoff requested/acknowledged/failed.

## 16. Enums and types

Enum values use explicit numeric assignments and are never reordered:

- `TradeOrderOrigin`: StrategyWorkflow, Manual, Imported, RecoveryCorrection;
- `TradeOrderStatus`: Draft, PendingApproval, Approved, ReadyForExecution, ExecutionBound, Executed, Cancelled, Expired, Rejected, Failed;
- `TradeKind`: Option, Future, Custom;
- `TradeRole`: Primary, Hedge, Adjustment, Compensation, Custom;
- `TradeLegSide`: Buy, Sell;
- `PositionEffect`: Open, Close;
- `OptionRight`: Put, Call;
- `OptionPricingConvention`: Debit, Credit, Custom;
- `TradeOrderPositionActorType`: Futures, VerticalSpread, IronCondor, Custom;
- `ExecutionChannel`: Manual, Broker;
- `BrokerProvider`: None, IBKR, future registered provider;
- `ExecutionGroupingType`: Single, AtomicCombo, Sequential;
- `ExecutionExposureState`; and
- `ExecutionEvidenceSource`.

Existing `TradeType`, `TradeSubType`, `OptionLegAction`, and `OptionLegType` remain legacy adapters. Executable behavior is selected from exact catalog and capability keys, not display enums.

## 17. Event sourcing and state

### 17.1 Trade Order stream

```text
trade-order/{portfolioId}/{fundId}/{orderId}
```

One stream contains root lifecycle and the complete typed child trade definitions. Revision is exact and contiguous. The creation event contains the canonical definition to prevent reconstruction from mutable Reference data.

### 17.2 Execution stream

```text
order-execution/{executionAttemptId}
```

It binds the exact Trade Order revision/hash. Manual/broker evidence is append-only. Unknown broker outcomes are reconciled before new mutation. Success/handoff obey the existing durability rules.

### 17.3 Position streams

Confirmed execution allocates signed fills to each child Trade and routes one idempotent handoff to its declared position actor type. Iron Condor stays one position aggregate; standalone Vertical stays one; Future stays one.

## 18. TradeDb projection schemas

The event log is authoritative. TradeDb schemas are versioned read projections.

### 18.1 `trade_order_v2`

Conceptual columns:

```text
portfolio_id, fund_id, order_id
schema_version, revision, value_date
origin, status
workflow_id, workflow_revision
composition_result_id, composition_result_sha256
risk_result_id, risk_result_sha256
approval_id, approval_version, approval_sha256
parent_deployment_id/version
parent_strategy_id/version
parent_structure_id/version
parent_variant_id/version
execution_envelope_payload/schema/sha256
topology_sha256, definition_sha256
active_execution_attempt_id
valid_until_utc
created_on_utc, created_by, updated_on_utc, updated_by
```

Primary exact-lookup key: `(portfolio_id, fund_id, order_id)`. Separate query tables/indexed projections support fund/date/status and active execution views.

### 18.2 `trade_order_trade_v2`

Common child index:

```text
portfolio_id, fund_id, order_id, order_revision, trade_id
trade_ordinal, trade_kind, role
deployment_id/version, strategy_id/version
structure_id/version, variant_id/version
runtime_capability_key
unit_quantity, parent_ratio
underlying_instrument_id
trade_date, maturity_date
payload_schema_version, payload, payload_sha256
definition_sha256
```

This permits common trade enumeration without decoding an entire order payload.

### 18.3 `option_trade_v2`

```text
portfolio_id, fund_id, order_id, order_revision, trade_id
trade_ordinal, role
deployment/strategy/structure/variant exact IDs+versions
runtime_capability_key, position_actor_type
unit_quantity, parent_ratio
side, bias, premium_mode, pricing_convention
underlying_instrument_id
trade_date, maturity_date
risk_bound_classification
topology_sha256, definition_sha256
created/updated audit
```

This is the current OptionTrade schema upgraded to definition ownership. It contains no positions, dynamic leg data, fills, or execution lifecycle state.

### 18.4 `option_trade_leg_v2`

```text
complete order/trade/revision identity, leg_id
leg_ordinal, structure_leg_key, expiry_group_key
instrument_id, contract_id, underlying_instrument_id
side, position_effect, ratio, quantity, multiplier
option_right, strike, expiration_utc
option_style, settlement_type
venue, currency, trading_class
tick_rule_id/version
contract_definition_sha256, leg_definition_sha256
```

Primary identity uses `leg_id`; contract ID is query/routing data.

### 18.5 `futures_trade_v2`

```text
portfolio_id, fund_id, order_id, order_revision, trade_id
trade_ordinal, role
deployment/strategy/structure/variant exact IDs+versions
runtime_capability_key, position_actor_type
unit_quantity, parent_ratio, side, bias
underlying/instrument root
trade_date, maturity_date
definition_sha256
created/updated audit
```

### 18.6 `futures_trade_leg_v2`

```text
complete order/trade/revision identity, leg_id
instrument_id, contract_id, futures_root, contract_month
side, position_effect, ratio, quantity, multiplier
expiration_utc, last_trading_utc, settlement_type
venue, currency, trading_class
tick_rule_id/version
contract_definition_sha256, leg_definition_sha256
```

### 18.7 `trade_order_execution_group_v2`

```text
complete order/revision identity, execution_group_id
group_ordinal, grouping_type
channel/provider constraints
quantity/ratio, price convention/bounds
referenced trade/leg ID payload + hash
dependency/sequence payload + hash
definition_sha256
```

### 18.8 Consistency

All rows carry `order_revision` and root `definition_sha256`. The projector writes child rows first and commits the root/current revision marker last. Readers load one declared revision and reject mixed hashes/revisions.

For Cassandra/Scylla projections, tables are designed per query and denormalized; no hot query depends on `ALLOW FILTERING` or cross-partition joins.

## 19. Existing schema migration

### 19.1 Legacy `trade_order`

Legacy rows map to a v2 root plus one child Trade when unambiguous. Embedded option legs map to one `OptionTrade`. Exact reviewed catalog mappings are required. A legacy record without sufficient approval/catalog evidence is historical/read-only and cannot become executable.

### 19.2 Current OptionTrade

Current durable definition fields move to `option_trade_v2` and `option_trade_leg_v2`:

- preserve order/trade IDs, dates, type/strategy evidence, underlying, static legs, and audit;
- map current display trade type to reviewed exact catalog/capability keys;
- remove positions, dynamic leg data, fills, and order lifecycle from the new OptionTrade payload;
- retain old events/tables for historical reads;
- stop new writes only after new actors/projectors/queries pass verification.

### 19.3 FuturesTrade

There is no current FuturesTrade aggregate to migrate. It is created as a new first-class model and schema. Any legacy futures order rows are adapted into one FuturesTrade with one stable leg after contract/catalog validation.

### 19.4 Old OptionTrade commands/events

OptionTrade-specific placement/open/close/fill commands migrate to generic Trade Order or Order Execution commands. OptionTrade-specific live leg-data commands migrate to strategy position actors. OptionTrade definition changes, if ever permitted before approval, occur through replacement of the parent Trade Order draft/revision.

## 20. Query contracts

Backend queries include:

- `GetTradeOrderQuery` by root ID/revision;
- `GetTradeOrdersQuery` by portfolio/fund/date/status;
- `GetTradeOrderTradesQuery`;
- `GetOptionTradeQuery` by `TradeEntityId`/revision;
- `GetFuturesTradeQuery` by `TradeEntityId`/revision;
- `GetTradeOrderExecutionGroupsQuery`;
- `GetTradeOrderLineageQuery`;
- `GetActiveExecutionForTradeOrderQuery`; and
- execution/fill/handoff queries from the Execution subsystem.

Read models are bounded and separate:

- `TradeOrderReadModel`;
- `OptionTradeReadModel` containing definition only;
- `FuturesTradeReadModel` containing definition only;
- typed leg read models;
- `OrderExecutionSummaryReadModel`;
- `TradeExecutionFillReadModel`;
- application-level composed views later required by the replacement UI.

## 21. UI boundary

The legacy Trade Order entry UI is outside the current implementation/design scope. Backend contracts must not be shaped around that UI.

Temporary UI/API compatibility may compose legacy-shaped read results, but:

- it cannot write the new backend through legacy OptionTrade mutation commands;
- it cannot create incomplete executable orders;
- it cannot own position calculations;
- it cannot bypass approval or execution actors.

A separate UI specification will follow after backend qualification.

## 22. Validation

Validation is layered:

1. Contract shape and schema version.
2. Identity and tenant authority.
3. Catalog kind/version/dependency graph.
4. Runtime capability availability.
5. Instrument/contract existence and definition hashes.
6. Trade-specific topology.
7. Cross-trade order topology and execution grouping.
8. Quantity/economic/risk envelope.
9. Approval hash and validity.
10. Execution readiness.

Expected validation failure returns structured error codes and full locations such as `Trades[1].OptionLegs[2].Strike`; it is not logged as an unhandled exception.

## 23. Error and recovery rules

- Unsupported trade kind/capability: fail order creation/readiness explicitly.
- Invalid OptionTrade/FuturesTrade topology: reject before approval/execution.
- Unknown schema: preserve historical bytes for diagnostics; do not execute.
- Projection failure: events remain authoritative; explicit projector recovery handles the gap.
- Execution outcome unknown: keep the order bound and reconcile.
- Manual fill incomplete: reject without mutation.
- Position handoff fails: Execution retains ownership until idempotent recovery succeeds.
- Ambiguous legacy mapping: quarantine/report; never guess catalog or economic facts.
- Any exception report includes inner chain, actor/thread, root/trade/leg/revision/hash, command/event, stream versions, and persistence disposition.

## 24. Observability

Supervisor metrics include:

- Trade Order/Execution mailbox depth and oldest age;
- orders by origin/status and trades by kind;
- validation/capability/catalog failures;
- active attempts by channel/provider;
- acknowledgement/fill/cancel/reconcile/handoff latency;
- partial/unbalanced/unknown exposure;
- projection lag/recovery;
- migration accepted/rejected/quarantined counts;
- allocation rate and GC statistics; and
- bounded detailed failure history.

## 25. Backend implementation gates

### TO-0: Impact and durable-fixture freeze

- Build after the legacy removals and inventory every broken reference.
- Freeze legacy TradeOrder/OptionTrade MessagePack and DB fixtures.
- Inventory API, service, actor, projector, scheduled task, and UI readers/writers.

### TO-1: Identity, enums, and common trade contracts

- Implement new root/trade/leg/execution IDs and explicit enums.
- Implement `ITrade`, discriminated envelope, immutable collections, canonicalization/hash.
- Prove MessagePack schemas and size bounds.

### TO-2: FuturesTrade backend

- Implement `IFuturesTrade`, `FuturesTrade`, leg, collection/common enumeration, validator, factory, read model.
- Convert one-leg Order Composition candidates deterministically.

### TO-3: OptionTrade backend update

- Refactor current OptionTrade into definition-only `ITrade` implementation.
- Replace option-only identity/action semantics with stable leg ID and generic side/effect.
- Remove positions/dynamic leg data/fills/order lifecycle from the new model.
- Implement single option, Vertical, and Iron Condor topology validators.

### TO-4: TradeOrder aggregate and actor

- Implement root, children, execution groups/envelope, commands/events/state/repository/maps/extensions.
- Support automatic approved package and manual draft/approval backend flow.

### TO-5: Generic projection schemas and queries

- Add v2 TradeDb tables, projectors, query tables, exact/revision-safe queries.
- Retain old schemas unchanged.

### TO-6: Execution alignment

- Update Execution approved structure/envelope to consume v2 TradeOrder/Trade/Leg/Group IDs.
- Add Manual/Broker channel under the same execution actor.
- Align IBKR translation for Future, Vertical, and Iron Condor.

### TO-7: Position handoff alignment

- Route FuturesTrade fills to Futures position owner.
- Route OptionTrade fills to Vertical/Iron Condor owner.
- Prove exact signed quantity, opening basis, commission allocation, and idempotency.

### TO-8: Legacy backend cutover

- Convert OptionTrade commands/events/projectors/APIs to the new authorities.
- Stop legacy TradeOrder and old OptionTrade writes.
- Keep verified historical readers/adapters.

### TO-9: Full backend qualification

- Repository-wide build and dependency audit.
- Unit, integration, event-store, TradeDb, NATS, replay, failure, benchmark, and soak tests.
- Confirm no test/benchmark host remains running.

UI redesign is a later, separately approved gate.

## 26. Required tests

### Unit

- all IDs and MessagePack round trips;
- immutable collections and canonical hashes;
- Future one-leg invariants;
- single-option, all Vertical, Long/Short Iron Condor topology;
- multiple trades with different strategies;
- one strategy per leg using one-leg child trades;
- same contract in different trades with distinct leg IDs;
- execution group completeness/no duplicate allocation;
- catalog/capability validation;
- new OptionTrade contains no position/fill/live data;
- legacy adapter acceptance/rejection;
- actor receive/validation map completeness.

### Actor and event store

- create/approve/ready/bind/execute/cancel/expire transitions;
- duplicate command and conflicting payload;
- exact stream version and replay/snapshot equivalence;
- automatic and manual origins;
- binding/release barriers;
- detailed exception containment.

### TradeDb

- additive schema migration;
- root/trade/leg/group projection coherence;
- no mixed revision/hash reads;
- OptionTrade and FuturesTrade exact queries;
- fund/date/status query tables without filtering;
- legacy schemas remain readable and unchanged;
- migration quarantine evidence.

### Execution/handoff

- manual and broker one-leg Future;
- manual and broker Vertical;
- manual and broker Iron Condor combo;
- multi-strategy execution grouping;
- unsupported custom grouping rejection;
- partial/unbalanced/cancel-fill/reconnect/reconciliation;
- exact and idempotent position handoff.

### Performance

- construct/hash/serialize 1, 2, 4, 16, and configured maximum legs/trades;
- actor create/replay/snapshot;
- projection/query latency;
- execution callback bursts;
- allocation/GC and payload sizes.

## 27. Acceptance criteria

1. `TradeOrder` is a generic approved order root containing one or more typed trades.
2. `OptionTrade` remains a first-class definition-only backend model.
3. `FuturesTrade` exists as a first-class one-leg backend model.
4. Both implement the common `ITrade` contract and retain typed invariants.
5. Different strategies coexist in one order as separate child trades.
6. A distinct strategy per leg is supported through one-leg child trades.
7. Every leg has a stable ID independent of contract ID.
8. Exact catalog and capability references determine behavior.
9. Order Composition converts without loss of evidence.
10. Trade Order contains no broker truth, fills, live marks, positions, or P&L.
11. OptionTrade/FuturesTrade contain no broker truth, fills, live marks, positions, or P&L.
12. Manual and broker execution share one execution aggregate under `Order/Execution`.
13. Execution cannot alter approved topology/economics.
14. Confirmed fills hand off to the correct strategy position actor exactly once.
15. Existing historical events/tables remain readable without mutation.
16. Old backend writers are stopped only after the new path is qualified.
17. Full backend build/test/replay/storage/failure/benchmark evidence passes.
18. UI redesign remains deferred and cannot constrain the backend schema.

## 28. Non-negotiable constraints

- Do not retire OptionTrade; refactor it to definition ownership.
- Do not create duplicate TradeOrder roots for Options and Futures.
- Do not put multiple unrelated strategies into one ambiguous trade.
- Do not use contract ID as leg identity.
- Do not encode side as negative quantity.
- Do not put execution fills/status into trade definitions.
- Do not put positions/live leg observations into TradeOrder, OptionTrade, or FuturesTrade.
- Do not infer runtime behavior from display enum/text.
- Do not allow Manual execution to bypass approval/reconciliation/audit.
- Do not expose broker SDK types outside infrastructure.
- Do not rewrite immutable history or overwrite legacy schemas.
- Do not begin the UI redesign as part of backend schema implementation.

## 29. Related documents

- `Order/Docs/Generic-Trade-Order-System-Design-v1.0.md` is superseded where it retires OptionTrade.
- `Order/Execution/Docs/OrderExecutionWorkflowSpecification.md` owns execution state, policy, reconciliation, compensation, and handoff.
- `Order/Execution/Docs/IbkrOrderExecutionAdapterSpecification.md` owns IBKR translation and broker behavior.
- `Order/Execution/Docs/ScriptedBrokerTestHarnessSpecification.md` owns deterministic broker verification.
- `Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/OrderComposition-Specification-v1.0.md` owns candidate construction.
- `Futures/Option/Docs/Futures-Option-Strategy-Trade-Position-Actor-Design-v1.0.md` owns option position actors and realtime routing after confirmed execution.
- `Documents/system/Actor-Implementation-Conventions.md` owns actor structure and messaging conventions.
