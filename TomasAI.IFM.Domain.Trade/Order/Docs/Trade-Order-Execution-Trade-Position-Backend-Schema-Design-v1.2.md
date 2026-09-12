# Trade Order, Execution, Trade, and Position Backend Schema Design

Date: 2026-09-12  
Version: 1.2  
Status: Authoritative backend design baseline  
UI status: Deferred; the legacy Trade Order entry UI is outside this design increment

## 1. Authoritative lifecycle

The backend lifecycle is:

```text
Approved TradeOrder
  -> OrderExecution
  -> confirmed and accepted fills/exposure
  -> OptionTrade | FuturesTrade | future EquityTrade | FixedIncomeTrade | CustomTrade
  -> strategy-specific TradePosition history
       Open -> MarkToMarket* -> EndOfDay* -> Close
```

`TradeOrder` is executable intent. `OptionTrade` and `FuturesTrade` are durable records of an established trade produced from actual execution. They are not child aggregates inside Trade Order before execution.

The typed Trade records and Trade Position records answer different questions:

- Trade query: what was actually established, from which order/execution/fills, using which instruments and strategy definition?
- Position query: what was that established trade worth and what was its risk state at specific times?

## 2. Ownership boundaries

| Aggregate/model | Authority |
| --- | --- |
| `TradeOrder` | Approved instruments/topology, target quantities, execution constraints, strategy/catalog evidence, and order lifecycle |
| `OrderExecution` | Manual/broker operations, acknowledgements, fills, fees, reconciliation, actual exposure, and terminal result |
| `OptionTrade` | Complete durable established option trade, original accepted fills, actual option-leg basis, costs, topology, and live-position launch evidence |
| `FuturesTrade` | Complete durable established futures trade, original accepted fills, actual futures basis, costs, and live-position launch evidence |
| Future typed trades | Durable established Equity, Fixed Income, FX, Crypto, or other asset-specific trade facts |
| `CustomTrade` | Durable accepted exposure that deliberately combines different asset/strategy components |
| Trade Position actor | Open, live mark-to-market, end-of-day, and close position calculations/history |

No aggregate owns another aggregate's mutable lifecycle. They reference immutable IDs, revisions, hashes, and event evidence.

## 3. Identity chain

```text
TradeOrderId
  -> ExecutionAttemptId
     -> ExecutionFillId[]
        -> TradeEntityId
           -> TradePositionEntityId / PositionSequence
```

### 3.1 Trade Order identity

`TradeOrderId` contains Portfolio ID, Fund ID, and the reserved integer Order ID. Value date is an attribute and query dimension.

### 3.2 Trade identity

`TradeEntityId` contains the parent Trade Order ID and reserved integer Trade ID. The Trade ID is reserved before execution so commands, fills, and resulting trade share a stable identity, but the typed Trade aggregate is not established until the accepted execution boundary.

### 3.3 Leg identity

Every proposed and filled leg has a stable GUID `TradeLegId`. Contract ID is not identity. The same contract can appear in several strategies, execution groups, or trades.

### 3.4 Position identity

The stable position stream identity is the `TradeEntityId` plus the concrete position actor type. Value date, phase, status, days to expiry, and timestamp are position record attributes rather than aggregate identity.

## 4. Trade Order schema

Trade Order contains a generic proposed execution structure, not a future `OptionTrade`/`FuturesTrade` aggregate instance.

### 4.1 Root fields

- schema version and order revision;
- `TradeOrderId` and reserved Trade IDs;
- origin (`StrategyWorkflow`, `Manual`, `Imported`, `RecoveryCorrection`);
- value date and validity interval;
- workflow, composition, risk, assignment, and approval IDs/versions/hashes;
- exact parent and component Deployment/Strategy/Structure/Variant catalog keys;
- generic proposed components and legs;
- target units/quantities and price economics;
- execution groups;
- execution channel policy and deterministic constraint versions;
- topology, execution-envelope, and complete definition hashes;
- lifecycle (`Draft`, `PendingApproval`, `Approved`, `ReadyForExecution`, `ExecutionBound`, terminal status);
- active Execution Attempt ID when bound;
- audit, correlation, and causation metadata.

### 4.2 Proposed component

`TradeOrderComponent` groups proposed legs using one strategy definition. It contains:

- reserved Trade ID and component ordinal;
- intended result kind (`Option`, `Future`, future typed kind, or `Custom`);
- role (`Primary`, `Hedge`, `Adjustment`, `Compensation`, `Custom`);
- exact catalog keys and runtime capability key;
- target strategy unit quantity and parent ratio;
- side, bias, and premium convention;
- underlying instrument identity;
- proposed Trade/expiry dates;
- proposed leg IDs;
- expected position actor type;
- component definition hash.

This is an approved construction/execution instruction. It is not yet an `OptionTrade` or `FuturesTrade` because it contains proposed quantities/prices rather than confirmed fills.

### 4.3 Proposed leg

`TradeOrderLeg` is generic and contains:

- stable leg ID and component ID;
- ordinal and structure-leg key;
- instrument/contract/underlying identity;
- asset and instrument class;
- Buy/Sell side and Open/Close effect;
- target ratio and quantity;
- multiplier, venue, currency, and trading class;
- expiry/last trading time where relevant;
- optional typed option details such as Call/Put and strike;
- tick-rule and contract-definition evidence;
- leg definition hash.

### 4.4 Multiple strategies

A custom Trade Order may contain several components, each with different strategy/catalog references. If each leg uses a different strategy, each component contains one leg. If several legs form one Vertical or Iron Condor strategy, they remain together in one component.

## 5. Order Execution schema

Order Execution consumes one exact approved Trade Order revision/hash.

It owns:

- channel (`Manual` or `Broker`) and provider;
- execution groups and operations;
- sent topology and quantities;
- broker/manual acknowledgements;
- working, modified, cancelled, rejected, and unknown states;
- immutable fills and fill corrections per leg;
- commissions and fees;
- filled quantity per proposed leg;
- balanced strategy units and residual exposure;
- reconciliation and compensation evidence;
- terminal result;
- typed Trade creation request and acknowledgement;
- position handoff after Trade creation.

### 5.1 Accepted execution boundary

A typed Trade is created only when Execution has durably classified the resulting exposure:

- complete intended fill;
- accepted balanced partial fill; or
- explicitly approved custom/adjusted exposure.

Unbalanced or unknown exposure must not be mislabeled as the intended OptionTrade/FuturesTrade. Execution retains ownership while reconciling or compensating. If the business explicitly accepts a different structure, that decision creates a new approved definition or a typed `CustomTrade` with complete evidence.

### 5.2 Opening versus closing execution

- Opening execution creates a new typed Trade from accepted fills.
- Closing execution references an existing Trade and appends durable close execution evidence to that Trade lifecycle.
- Adjustment execution either updates the existing supported Trade lifecycle or creates a separately identified adjustment Trade according to its approved structure.

## 6. Common established Trade contract

All typed trades implement `ITrade`:

- schema version;
- `TradeEntityId`;
- concrete `TradeKind`;
- source Trade Order ID/revision/hash;
- source Execution Attempt ID and terminal event ID;
- exact strategy/catalog/capability references;
- role and strategy semantics;
- actual established unit quantity;
- underlying instrument identity;
- trade date and maturity where applicable;
- immutable filled leg definitions/basis;
- immutable original accepted fills and their allocation to legs;
- allocated opening commissions/fees;
- lifecycle (`Open`, `Closing`, `Closed`, `CancelledAfterNoFill`, `Corrected` as applicable);
- position owner type;
- definition and execution evidence hashes;
- created/updated audit.

`ITrade` contains established facts only. The original fills copied from the terminal Execution result are part of those facts and are returned with the Trade. Current market marks, current Greeks, MTM P&L, and position history are excluded.

### 6.1 Original fill evidence

Every typed Trade contains an immutable `TradeExecutionFill[] OriginalFills` captured from the accepted Execution result. Each item contains:

- internal Fill ID;
- source Execution Attempt and Operation IDs;
- broker execution key or manual external reference;
- Trade Leg ID and execution group ID;
- execution channel/provider;
- fill time and receive time in UTC;
- side, filled quantity, and price;
- currency and multiplier needed to interpret the price;
- commission and fees when known;
- correction/supersession reference when applicable;
- source evidence hash.

Execution remains authoritative for the full operational fill timeline. The typed Trade holds a frozen, hash-verified copy of all fills accepted when the Trade was established. This is intentional duplication across aggregate boundaries, not dual mutation authority. The Trade never edits an original fill in place.

If a late commission or fill correction materially changes the established basis after Trade creation, Execution sends an idempotent `AmendTradeExecutionEvidenceCommand`. The typed Trade appends an amendment referencing the original Fill ID, recalculates its effective opening basis, increments its durable revision, and publishes an amendment event to the position owner. Both original and corrected evidence remain queryable.

## 7. OptionTrade schema

`OptionTrade` is the durable result of an accepted option execution.

### 7.1 Fields

In addition to `ITrade`, it contains:

- option pricing convention;
- option structure/risk-bound classification;
- underlying futures/instrument contract;
- effective option maturity/expiry grouping;
- immutable `OptionTradeLeg[]` based on actual fills;
- immutable `TradeExecutionFill[] OriginalFills` containing every accepted fill used to establish the trade;
- immutable execution-evidence amendments when late commissions or corrections arrive;
- actual filled strategy units;
- allocated opening net debit/credit;
- position actor type (`VerticalSpread`, `IronCondor`, future supported option strategy);
- option topology hash.

### 7.2 Established option leg

Each `OptionTradeLeg` contains:

- stable Trade Leg ID and structure-leg key;
- instrument/contract/underlying identity;
- Call/Put right and strike;
- expiry, multiplier, venue, currency, trading class;
- actual signed economic direction expressed as side plus positive quantity;
- actual filled quantity;
- weighted average fill price;
- allocated commission/fees;
- source Execution Fill IDs plus direct access to their immutable fill records;
- effective opening-basis version;
- contract-definition and leg-definition hashes.

### 7.3 What OptionTrade excludes

- proposed unfilled quantity;
- broker order state;
- execution workflow state or a mutable fill collection;
- current bid/ask/last;
- current IV/Greeks;
- `ITradePositionCollection`;
- MTM/EOD/close position snapshots.

## 8. FuturesTrade schema

`FuturesTrade` is the durable result of an accepted futures execution.

### 8.1 Fields

In addition to `ITrade`, it contains:

- exactly one established `FuturesTradeLeg` initially;
- immutable original accepted fills and later evidence amendments;
- futures root and contract month;
- expiry and last-trading UTC evidence;
- actual filled quantity and average fill price;
- allocated opening commission/fees;
- multiplier, venue, currency, trading class, tick rule;
- Long/Short variant evidence;
- target Futures position actor type.

### 8.2 What FuturesTrade excludes

- current futures price;
- current unrealized/realized position calculation;
- broker order status;
- execution workflow state or mutable fill collection;
- MTM/EOD/close position history.

## 9. Future typed trades

The same `ITrade` envelope can support:

- `EquityTrade` with one or more equity legs;
- `FixedIncomeTrade` with security, coupon, maturity, face value, price/yield basis, and settlement facts;
- `FxTrade` with currency pair and settlement facts;
- `CustomTrade` with an explicitly validated mixed component topology.

New types require an explicit MessagePack discriminator, schema, capability, validator, projector, query mapper, and position owner. Unknown types remain historical/diagnostic and cannot execute.

## 10. Trade creation messages

Execution emits a typed creation request after its accepted exposure is durably known:

- `CreateOptionTradeFromExecutionCommand`;
- `CreateFuturesTradeFromExecutionCommand`;
- future `CreateEquityTradeFromExecutionCommand`;
- future `CreateFixedIncomeTradeFromExecutionCommand`;
- `CreateCustomTradeFromExecutionCommand`.

The command includes:

- Trade ID and intended concrete kind;
- exact Trade Order revision/hash;
- exact Execution Attempt/terminal event;
- accepted fill allocation by leg;
- every immutable original fill used by that allocation;
- actual units and economic basis;
- strategy/catalog/capability evidence;
- idempotent creation ID and payload hash.

Result events:

- `OptionTradeCreatedEvent`;
- `FuturesTradeCreatedEvent`;
- typed future equivalents;
- `TradeCreationRejectedEvent` for topology/evidence mismatch.

Execution does not release ownership until the typed Trade actor acknowledges durable creation. Repeating the same creation command is idempotent; the same ID with different content is a conflict.

The resulting `OptionTradeCreatedEvent` or `FuturesTradeCreatedEvent` contains the complete established Trade, including original fills and effective opening basis. This event is sufficient to create the Open Trade Position without querying the Execution aggregate.

## 11. Typed Trade actors

- `OptionTradeCommandActor` owns durable OptionTrade creation and lifecycle facts.
- `FuturesTradeCommandActor` owns durable FuturesTrade creation and lifecycle facts.
- future typed trade actors follow the same conventions.

These actors are low-frequency standard event-sourced actors. They do not use the high-frequency resident position window. Live position actors remain separate.

The existing OptionTrade actor must be refactored so it no longer processes live leg data or owns positions. Its automatic order placement/open/close execution responsibilities move to `Order/Execution`.

## 12. Trade query model

Trade queries return complete durable typed Trade schema data, including original fills and execution-evidence amendments. They never mix current position values into the returned Trade definition.

### 12.1 Direct queries

- `GetOptionTradeQuery(TradeEntityId)` returns one complete `OptionTradeReadModel`, including original fills and effective opening basis.
- `GetFuturesTradeQuery(TradeEntityId)` returns one complete `FuturesTradeReadModel`, including original fills and effective opening basis.
- future typed equivalents return their exact schema.
- `GetTradeQuery(TradeEntityId)` returns a discriminated `TradeReadModelEnvelope` for cross-asset callers.

### 12.2 Concrete strategy Trade queries

Strategy queries use explicit, concrete names and typed results. Do not expose one public `GetOptionStrategyTradesQuery` whose runtime parameters silently select a payload type.

Initial Option query families are:

- `GetIronCondorOptionTradeQuery(TradeEntityId)` returns one `OptionTradeReadModel` and verifies that its structure/capability is Iron Condor;
- `GetIronCondorOptionTradesQuery(Fund/Portfolio, FromUtc, ToUtc, lifecycle, variants, page)` returns a bounded page of `OptionTradeReadModel` records whose structure/capability is Iron Condor;
- `GetVerticalSpreadOptionTradeQuery(TradeEntityId)` returns one `OptionTradeReadModel` and verifies that its structure/capability is Vertical Spread;
- `GetVerticalSpreadOptionTradesQuery(Fund/Portfolio, FromUtc, ToUtc, lifecycle, variants, page)` returns a bounded page of `OptionTradeReadModel` records whose structure/capability is Vertical Spread; and
- future option strategies add their own named singular and collection query contracts.

Short/Long Iron Condor and Put/Call Credit/Debit Spread remain exact variant filters or concrete payload discriminators within their owning strategy query. They do not require duplicate aggregate schemas. Separate variant query names may be added only when their returned schema or authorization differs materially.

Futures strategies follow the same pattern:

- `GetFuturesTradeQuery(TradeEntityId)` remains the asset-level direct query;
- each implemented Futures strategy publishes an explicitly named query, for example `GetIntrinsicTimeFuturesTradeQuery` and `GetIntrinsicTimeFuturesTradesQuery`; and
- future Futures strategy actors register their own typed query contracts rather than extending a central parameter switch.

An asset-family query such as `GetOptionTradesQuery` or `GetFuturesTradesQuery` remains valid for deliberately cross-strategy administrative/blotter retrieval. It returns a discriminated asset-family envelope. Strategy-specific application paths use the concrete named queries.

“Iron Condor Option Trade” and “Vertical Spread Option Trade” are typed query views over the durable OptionTrade authority. They do not create competing writable aggregates.

### 12.3 Query paging

All collection/date-range queries require:

- inclusive `FromUtc` and exclusive `ToUtc` or explicit DateOnly semantics;
- deterministic sort by trade date/time plus Trade ID;
- bounded page size;
- opaque continuation token;
- exact catalog-key filters rather than display strings;
- explicit inclusion/exclusion of closed trades.

### 12.4 Query registration and validation

Each concrete query has its own actor receive-map entry, parameter validator, result type, MessagePack schema, authorization check, storage query, continuation-token contract, and tests. Unknown strategy/capability combinations fail before storage access. A concrete query verifies that every returned row has the expected strategy capability and payload discriminator; a mismatched row is a projection/data-integrity failure rather than an empty/default result.

## 13. Trade Position model

Position history is separate from Trade schema data.

### 13.1 Position phases

Use explicit `TradePositionPhase`:

```text
Open
MarkToMarket
EndOfDay
Close
Correction
```

- `Open`: immutable opening position created from the accepted execution basis.
- `MarkToMarket`: current/live calculated position observations.
- `EndOfDay`: immutable EOD checkpoint for a trading date.
- `Close`: immutable final/partial-close position derived from close execution.
- `Correction`: explicitly authorized correction referencing the superseded record.

### 13.2 Common position envelope

Every position record has:

- schema version;
- Trade ID and concrete trade/position kind;
- position sequence/version;
- phase and trade lifecycle state;
- value date and calculated/observed UTC timestamps;
- source market event/generation/tick IDs when applicable;
- source execution event for Open/Close;
- source Trade revision and effective opening-basis version;
- position readiness/freshness;
- actual quantity/exposure;
- current/opening/closing value;
- commission/fees allocation;
- realized and unrealized P&L where applicable;
- strategy payload discriminator/version/hash;
- event/position hash and audit.

### 13.3 Strategy-specific payload

The common envelope routes and indexes the record. Concrete payloads preserve the real strategy schema:

- `FuturesTradePositionReadModel`;
- `VerticalSpreadTradePositionReadModel`;
- `IronCondorTradePositionReadModel`;
- future Equity/FixedIncome/custom position read models.

An OptionTrade position query returns a discriminated option position envelope whose payload is Vertical, Iron Condor, or another supported option strategy. It does not flatten every strategy into nullable columns.

### 13.4 Position aggregate ownership and per-leg updates

Each established Trade has one strategy-specific position aggregate. Its resident state is an `ITradePositionCollection` keyed by stable Trade Leg ID and containing the current accepted observation for every leg.

The Open position is initialized directly from the complete typed Trade creation event:

- actual filled quantity and side for every leg;
- weighted opening price by leg;
- every original fill and allocated costs;
- total opening debit/credit or futures basis;
- exact topology and strategy capability;
- source Trade/Execution hashes.

After opening, each live market change addresses one Trade Leg ID/contract. The position actor:

1. validates that the leg belongs to the open Trade;
2. rejects duplicate/stale source observations;
3. replaces only that leg's current market observation;
4. retains the current observations for all other legs;
5. recalculates affected components and the complete strategy position;
6. appends a new durable MTM position event/version; and
7. publishes/projects one coherent position result.

This supports any bounded supported leg count. The calculation capability defines how many legs and which relationships are required; the actor framework does not hard-code two or four legs.

## 14. Position query contracts

### 14.1 Latest/current position

- `GetOptionTradePositionQuery(TradeEntityId)` returns a discriminated latest Option position only for deliberately cross-strategy infrastructure/UI use.
- `GetIronCondorOptionTradePositionQuery(TradeEntityId)` returns the latest committed `IronCondorTradePositionReadModel`.
- `GetVerticalSpreadOptionTradePositionQuery(TradeEntityId)` returns the latest committed `VerticalSpreadTradePositionReadModel`.
- `GetFuturesTradePositionQuery(TradeEntityId)` returns the latest committed futures position at the asset-family boundary.
- each implemented Futures strategy adds its own explicitly named latest-position query, such as `GetIntrinsicTimeFuturesTradePositionQuery`.
- future option, futures, equity, fixed-income, and custom strategies register their own typed latest-position queries.

### 14.2 Date-range history

- `GetOptionTradePositionsQuery(TradeEntityId, FromUtc, ToUtc, phases, page)` remains the cross-strategy history query for one known OptionTrade and returns discriminated position envelopes.
- `GetIronCondorOptionTradePositionsQuery(TradeEntityId or Fund/Portfolio scope, FromUtc, ToUtc, phases, variants, page)` returns typed Iron Condor Open/MTM/EOD/Close records.
- `GetVerticalSpreadOptionTradePositionsQuery(TradeEntityId or Fund/Portfolio scope, FromUtc, ToUtc, phases, variants, page)` returns typed Vertical Spread records.
- `GetFuturesTradePositionsQuery` remains the asset-family history query.
- each implemented Futures strategy adds a concrete date-range query, such as `GetIntrinsicTimeFuturesTradePositionsQuery`.
- future typed strategies follow the same singular-latest and plural-date-range naming convention.

The query parameter contract uses either one `TradeEntityId` or one portfolio/fund scope; supplying both or neither is invalid. The concrete strategy identity is fixed by the query type. Exact Deployment/Strategy/Structure/Variant keys may further restrict versions but cannot change the strategy payload family.

### 14.3 History semantics

- Open, EOD, and Close records are immutable checkpoints.
- MTM records are immutable accepted position versions; “latest” is a separate current projection/pointer.
- Date range applies to `CalculatedAtUtc` by default and states that explicitly.
- Results sort by calculated time, Trade ID, then position sequence.
- Pagination is mandatory because MTM history can be large.
- Optional sampling/rollup is a separate query and never replaces exact history.
- Readers never combine strategy components from different position versions.

## 15. Storage projections

None of the tables below replace the PostgreSQL event log authority. They are typed read/query projections.

### 15.1 Order and execution

- `trade_order_v3`;
- `trade_order_component_v3`;
- `trade_order_leg_v3`;
- `trade_order_execution_group_v3`;
- execution-attempt, operation, fill, reconciliation, and handoff projections from the Execution specification.

### 15.2 Established trades

#### `option_trade_v3`

Contains Trade ID, source order/execution evidence, exact catalog/capability references, established units, underlying/maturity, opening debit/credit and costs, original-fill payload/hash or immutable fill references, effective opening-basis version, lifecycle, topology/definition/execution hashes, and audit.

#### `option_trade_leg_v3`

Contains stable leg ID, OptionTrade partition identity, option contract facts, actual fill quantity/average price/cost allocation, source fill evidence, and hashes.

#### `trade_execution_fill_by_trade_v1`

Contains the immutable fill records returned with a typed Trade, keyed by Trade ID, source Execution Attempt, Fill ID, and evidence revision. It preserves original fills and append-only corrections/commission amendments. A composed OptionTrade/FuturesTrade query loads this bounded opening evidence as part of the typed Trade result.

#### `futures_trade_v3`

Contains Trade ID, source order/execution evidence, catalog/capability references, established quantity/basis/cost, futures contract facts, lifecycle, hashes, and audit.

#### Future typed trade tables

Each family has a typed table/projection with the common evidence envelope and required asset-specific facts.

### 15.3 Position current and history

Use separate current and history projections:

- `option_trade_position_current_v1`;
- `option_trade_position_history_v1`;
- `futures_trade_position_current_v1`;
- `futures_trade_position_history_v1`;
- optional strategy-specific component tables when needed.

History keys support Trade ID plus calculated date/time and position sequence. Each concrete strategy query has a purpose-built date query projection keyed by its strategy capability plus exact strategy key, Fund/Portfolio, calculated date/time, Trade ID, and sequence. Hot queries do not use database filtering across unrelated partitions or deserialize unrelated strategy payloads to filter in memory.

Current pointers advance only after all components for a position version are committed/projected. History rows are append-only and idempotent by source event/position version.

## 16. Position creation and update flow

### 16.1 Opening

```text
Execution terminal accepted exposure
  -> typed Trade durably created
  -> typed Trade creation event already contains original fills and opening basis
  -> Create/Open strategy TradePosition command
  -> Open position event/history row
  -> current position pointer
  -> Execution receives position handoff acknowledgement
```

### 16.2 Mark to market

```text
Market observation
  -> realtime router identifies open typed Trade
  -> strategy position actor command
  -> resident state calculation
  -> durable position event window
  -> MTM history/current projection
```

### 16.3 End of day

An explicit market-session/EOD event causes the position actor to write an immutable EOD checkpoint from one committed coherent state. EOD is not inferred solely from the last MTM row.

### 16.4 Close

Confirmed close execution is allocated against the existing Trade. The position actor writes a Close record containing actual close basis, realized P&L, remaining quantity, and partial/complete closure state.

### 16.5 Evidence amendments after opening

A late commission or corrected execution fill updates the typed Trade through an append-only evidence amendment. The Trade publishes the new effective opening-basis version. The strategy position actor applies that version as a durable correction barrier, recalculates Open/current P&L bases, and writes a `Correction` position record. Live leg updates cannot overtake an unresolved basis correction.

## 17. Migration effect on current OptionTrade

The current OptionTrade must be split without deleting the aggregate:

| Current field/behavior | Target |
| --- | --- |
| Strategy, underlying, maturity, static leg topology | New established `OptionTrade` |
| Order placement/open/close commands | Trade Order and Order Execution |
| Trade fills | Execution authority; every original accepted fill and its immutable allocation copied into the created Trade |
| Trade limits | Approved Trade Order/Risk/Execution envelope references |
| `ITradePositionCollection` | Strategy position actor state |
| `OptionLegData` | Strategy position state |
| MTM/EOD/Close values | Position history/current projections |
| OptionTrade query | Definition-only durable OptionTrade query |

Legacy rows/events remain readable through explicit adapters. New writers use the new authority only after end-to-end qualification.

## 18. Backend implementation gates

### Gate 0: Dependency and historical fixture freeze

- Inventory all removed TradeOrder references and current OptionTrade readers/writers.
- Freeze legacy MessagePack/event/TradeDb samples and query results.

### Gate 1: Generic TradeOrder intent

- Implement new identities, components, proposed legs, execution groups/envelope, canonicalization, hashes, validation, actor, and schemas.

### Gate 2: Execution alignment

- Generalize Execution to proposed components/legs and Manual/Broker channels.
- Produce deterministic accepted fill allocation and typed trade-creation requests.

### Gate 3: FuturesTrade

- Implement FuturesTrade actor/model/messages/projector/query and create it from accepted one-leg fills.

### Gate 4: OptionTrade refactor

- Refactor OptionTrade into established definition/basis ownership.
- Implement single Option, Vertical, Iron Condor creation from accepted fills.
- Return all original accepted fills with OptionTrade and support append-only evidence amendments.
- Remove live positions, leg observations, and execution behavior.

### Gate 5: Typed Trade queries

- Implement asset-family direct queries plus explicitly named Iron Condor and Vertical Spread Trade queries.
- Implement explicitly named queries for each supported Futures strategy.
- Give every concrete query a typed result, receive-map entry, validator, authorization, purpose-built projection query, paging contract, and data-integrity tests.

### Gate 6: Position schemas and queries

- Implement common phase envelope and strategy payloads.
- Implement latest and date-range query pairs for Iron Condor, Vertical Spread, and every supported Futures strategy.
- Retain asset-family envelope queries only for cross-strategy operational views.

### Gate 7: Position actor/handoff integration

- Create Open directly from the complete typed Trade/fill evidence, MTM from per-leg market changes, EOD from explicit session events, Close from close execution, and Correction from evidence amendments.

### Gate 8: Legacy cutover and qualification

- Stop old writers after complete build, unit, integration, storage, actor, NATS, replay, failure, benchmark, and soak evidence.
- UI remains deferred.

## 19. Acceptance criteria

1. A Trade Order represents approved executable intent only.
2. Execution owns all manual/broker attempts and actual fills.
3. Accepted opening execution creates exactly one correct typed Trade per accepted component.
4. Every typed Trade returns all original accepted fills and effective opening-basis evidence.
5. Option strategy trades use the OptionTrade schema.
6. Futures strategy trades use the FuturesTrade schema.
7. Concrete Iron Condor, Vertical Spread, and Futures strategy queries return typed results from the same durable Trade authority, not duplicate writable aggregates.
8. Trade queries contain no live position data.
9. Position queries return Open/MTM/EOD/Close records independently of Trade definitions.
10. Latest and date-range position queries are both supported.
11. Each concrete strategy has explicitly named latest and date-range position queries that are bounded, deterministic, typed, and paged.
12. Each accepted live leg change produces a coherent whole-strategy position version for any supported leg count.
13. Unbalanced/unknown exposure is never mislabeled as an intended strategy Trade.
14. Typed Trade creation and position handoff are durable and idempotent.
15. Current OptionTrade responsibilities are migrated to their correct owners.
16. FuturesTrade is a first-class backend aggregate/schema/query.
17. Future asset families can add typed Trade and Position schemas through explicit capabilities.
18. Historical immutable data remains readable and unmodified.
19. The backend is qualified before any Trade Order UI redesign begins.

## 20. Related documents

- `Generic-Trade-Order-Backend-Schema-Design-v1.1.md` is superseded on the TradeOrder-to-Trade lifecycle.
- `Order/Execution/Docs/OrderExecutionWorkflowSpecification.md` owns execution safety and recovery.
- `Order/Execution/Docs/IbkrOrderExecutionAdapterSpecification.md` owns IBKR translation.
- `Futures/Option/Docs/Futures-Option-Strategy-Trade-Position-Actor-Design-v1.0.md` owns futures-option MTM routing and resident strategy position actors.
- `Documents/system/Actor-Implementation-Conventions.md` owns actor implementation structure.
- `Trade-Lifecycle-Backend-Implementation-Plan-v1.0.md` owns the gated implementation, test, migration, and optimized realtime-routing work.

## 21. Realtime contract-to-position routing clarification

Futures and futures-option ticks use a mailbox-owned, in-memory, one-to-many reverse index from canonical numeric Market Instrument ID to active position-leg routes. The same contract can belong to multiple trades, Funds, or Portfolios, so a lookup returns a stable route bucket rather than one Trade ID. Each route contains Portfolio ID, Fund ID, Trade ID, Strategy Position ID, Trade Leg ID, destination actor/thread identity, and route generation.

Open, Close, and Correction lifecycle events update the index. Startup recovery replaces it from a durable open-position projection before live routing begins. Tick processing performs no database access. Valid ticks with no open position, ticks for closed positions, stale-generation ticks, unknown instruments, and duplicate/out-of-order ticks are classified, counted, logged through rate-limited/aggregate diagnostics, acknowledged, and ignored without exceptions or retries. Invalid tick payloads follow the explicit data-quality failure path.

The hot path permits no LINQ, reflection, string construction, per-tick formatted logging, or route-collection allocation. Benchmarks must prove zero managed allocation for a valid unrouted tick and no route-collection allocation for a routed tick.
