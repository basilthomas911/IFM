# RiskManager, Portfolio, Trade Order, and Position Routing Runtime Integration Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Status | Implemented, including desktop Trade Order, Iron Condor monitor, and retained EOD screen migration; live-market soak deferred to the approved Monday window |
| Created | 2026-09-13 |
| Primary owner | Intrinsic Time Strategy Workflow RiskManager |
| Affected domains | Trade Strategy Workflow, Portfolio, Trade Order, Order Execution, Futures Trade Position, Futures Option Trade Position, Market Data Feed |
| Runtime | .NET 10, MessagePack, actor messaging, PostgreSQL EventSourceDb and PortfolioDb |
| Governing actor convention | `Documents/system/Actor-Implementation-Conventions.md` |

## Implementation outcome

The approved backend boundary is implemented. It ends at the durable Order Execution start request and includes the Contract-ID Trade Position routing foundation. The desktop Trade Order submission now sends broker-neutral opening candidates to Portfolio, dispatches all accepted orders through the canonical Trade Order actor lifecycle, and no longer depends on the removed legacy `TradeCommandService`. The Iron Condor monitor reads current strategy Trade Plans, while its historical mode retains read-only access to legacy projections. The retained EOD screen now queries and commands the strategy-specific Position actor. The IBKR adapter, IBKR emulator, and live-market execution fills remain later implementations.

The active schema-2 workflow path is Portfolio/Fund neutral through Order Composition. RiskManager maps the one selected opportunity to one immutable Portfolio request. Portfolio evaluates all eligible Funds under its PostgreSQL financial-authority lock, allocates business identities only for accepted Fund orders, applies accepted capacity to `working`, and commits decisions, orders, legs, capacity evidence, financial revision, receipt, and completed event in the EventSource-enlisted transaction. RiskManager then performs the deterministic Create, Approve, Ready, and Bind transitions. The existing Trade Order event projector emits the Order Execution start command after Bind commits.

The position router now uses an actor-owned, ordinal `ContractId -> PortfolioFundTradeLeg[]` copy-on-write index. Position open, close, and replacement transitions maintain route entries; startup performs one Scylla snapshot reconstruction; futures and futures-option ticks perform an in-memory lookup and fan out `ChangeTradeLegDataCommand` messages without a database query or expected-outcome exception.

### Qualification evidence

| Gate | Result |
| --- | --- |
| Trade unit | 1,121 passed |
| Portfolio unit | 220 passed |
| Trade BDD | 45 passed |
| Focused TradeFlow integration | 10 passed across registration, serialization, and recovery |
| Portfolio PostgreSQL and typed NATS integration | 7 passed, including atomic capacity evidence and injected rollback |
| Trade storage integration | 1 passed |
| Focused TradeFlow verification | 12 passed |
| API Server build | Passed with 0 warnings and 0 errors |
| Actor integration host build | Passed with 0 warnings and 0 errors |
| Full solution build | Passed with 0 warnings and 0 errors using Visual Studio 2026 Community native build tools |
| Contract-ID lookup benchmark | 40.58 ns unrouted, 36.14 ns one route, 103.91 ns sixty-four routes; 0 B allocated in every case |
| Desktop presentation unit | 387 passed, including EOD position flow and Portfolio order dispatch happy/no-trade/failure/inconsistent-receipt paths |
| Focused Trade desktop system tests | 16 passed for Trade Order and Iron Condor first-display/navigation behavior |

The full Trade verification project previously reported 49 passes and 18 failures in the deferred Regime Discovery calculation/transition suite. The complete desktop system-test assembly builds and runs; 202 tests pass, while seven unrelated environment/baseline tests require reference-dialog signature updates, dark-theme migration of `StrategyWorkflowDetailsAccordion`, or pre-generated Market Condition evidence. The API Server, desktop application, and complete solution build pass with zero warnings and errors.

### Desktop migration addendum

- `PortfolioTradeOrderService` creates a fully hashed `EvaluatePortfolioOrderCompositionCommand` under the explicit single-user development policy and dispatches each accepted order through Create, Approve, Ready, and Bind.
- `StrategyPositionCommandApi` and `StrategyPositionQueryApi` route Iron Condor, Vertical Spread, and Futures commands and queries to their strategy-specific actors.
- `StrategyPositionId.Create` centralizes the deterministic position identity already used by established-Trade projectors.
- `EndOfDayProcessViewModel` uses current Strategy Position P&amp;L and sends the strategy-specific EOD command. The form and operator flow remain available.
- The obsolete EOD UI event consumer and service were removed because the screen now obtains the terminal result through request/reply APIs.
- All public methods introduced or touched by this desktop migration have XML documentation.

## 1. Purpose

Complete the approved runtime path from one portfolio-independent composed trade opportunity through RiskManager, atomic Portfolio/Fund acceptance, Trade Order creation, and Order Execution initiation. Define and implement the Contract-ID Trade Position routing foundation used after the later broker/fill integration creates an established Trade.

The implementation shall preserve these ownership boundaries:

- Order Composition describes one executable market opportunity and does not allocate Portfolio, Fund, Order, or Trade identities.
- RiskManager validates the market and trade evidence, selects the target Portfolio authority, and submits one immutable Portfolio request.
- Portfolio evaluates every eligible Fund, allocates identities only for accepted Fund orders, applies the accepted capacity effect, and commits its decision with the completed event in one PostgreSQL transaction.
- Trade Order owns executable intent after Portfolio acceptance.
- Order Execution owns broker interaction, execution attempts, fills, and reconciliation.
- IBKR resolves canonical Contract IDs to IBKR contracts and `conId` values only inside the IBKR broker boundary.
- Established Trade and Trade Position retain canonical Contract IDs.
- Futures and Futures Option realtime actors own an in-memory `ContractId -> PortfolioFundTradeLeg[]` reverse index.
- Trade Position open, close, and leg-change lifecycle messages add, remove, or replace router entries.

## 2. Approved end-to-end lifecycle

```text
Futures ITI signal
  -> Regime Discovery
  -> Market Condition
  -> Trade Selection: one best opportunity or NoTrade
  -> Order Composition: one portfolio-independent composed opportunity
  -> RiskManager: portfolio-independent risk evidence and Portfolio request
  -> PortfolioOrderCompositionFunctionActor
       -> ExecuteTradeOrders [one accepted TradeOrder per eligible Fund]
       -> NoTradeOrders [no accepted Funds]
       -> Failed [validation or infrastructure failure]
  -> TradeOrderCommandActor
  -> OrderExecutionCommandActor

Later broker/fill implementation:
  -> IBKR trade broker
       -> canonical ContractId to IBKR Contract/conId
  -> FuturesTrade or FuturesOptionTrade
  -> strategy-specific Trade Position actor
       -> open/close/change route registration
  -> FuturesRealtimeActor or FuturesOptionRealtimeActor
       -> ContractId lookup
       -> ChangeTradeLegDataCommand fan-out
```

## 3. Decisions that supersede earlier documentation

This plan supersedes conflicting statements in older documents for this runtime path.

| Previous statement | Replacement decision |
| --- | --- |
| RiskManager resolves `CompositionLeg.InstrumentId` through `IMarketDataApi.TryGetMarketInstrumentId` before Portfolio | RiskManager passes the canonical string Contract ID. Portfolio and Trade Order contain no Databento identity. |
| `TradeLegDefinition` requires a Databento `MarketInstrumentId` | A Trade leg requires canonical `ContractId`; provider identifiers remain at provider boundaries. |
| Position routing uses `MarketInstrumentId -> MarketPositionRoute[]` | Position routing uses `ContractId -> PortfolioFundTradeLeg[]`. |
| Realtime routing stores target actor names and arbitrary actor thread IDs | The route stores domain identity and trade type; actor and thread identity are derived deterministically. |
| Order Composition reserves Fund, Order, and Trade identities | Portfolio allocates Order IDs and reserved Trade IDs only after accepting a Fund. |
| RiskManager reserves capacity and separately asks a Fund to authorize risk | Portfolio acceptance and the associated capacity effect commit atomically in one Function transaction. |
| Unknown or closed-position ticks can cause handler exceptions | They are expected ignored outcomes with counters and bounded diagnostics. |

Documents requiring alignment when Gate 0 is processed:

- `TomasAI.IFM.Domain.Portfolio/Docs/Portfolio-PostgreSQL-Transactional-Function-Implementation-Plan-v1.0.md`
- `TomasAI.IFM.Domain.Trade/Order/Docs/Trade-Lifecycle-Backend-Implementation-Plan-v1.0.md`
- `TomasAI.IFM.Domain.Trade/Order/Docs/Generic-Trade-Order-Backend-Schema-Design-v1.1.md`
- `TomasAI.IFM.Domain.Trade/Order/Docs/Trade-Order-Execution-Trade-Position-Backend-Schema-Design-v1.2.md`
- RiskManager design, specification, qualification, and runbook documents in this folder

## 4. Non-negotiable invariants

1. The canonical string Contract ID is the durable instrument identity from Order Composition through established Trade and Trade Position.
2. Databento `MarketInstrumentId` is feed metadata and shall not be required by RiskManager, Portfolio, Trade Order, Order Execution, Trade, or Trade Position state.
3. IBKR `conId` is broker metadata and shall not become a Portfolio, Trade Order, Trade, or Trade Position identity.
4. No `FundId`, `OrderId`, or `TradeId` is allocated before Portfolio accepts a Fund.
5. One Portfolio Function call returns either `ExecuteTradeOrders` with a nonempty list, `NoTradeOrders` with an empty list, or a typed failure.
6. Portfolio decision rows, per-Fund decisions, accepted orders, accepted legs, accepted capacity effects, financial revision, operation receipt, and completed event commit or roll back together.
7. There is no staged reservation service, separate Fund-authorization service, recovery coordinator, or background Function poller in this flow.
8. External Trade Order and Order Execution dispatch starts only after Portfolio commit.
9. Lost replies reuse the exact persisted command identity and payload. They do not create a new request.
10. A Contract ID may route to zero, one, or many open Trade Position legs.
11. Open, close, and leg-change lifecycle messages are the normal source of router mutations.
12. The realtime tick path performs no database call, provider translation, reflection, LINQ, or per-tick string construction.
13. One failed destination shall not prevent a tick from reaching other matching positions.
14. Unknown, not-yet-open, closed, duplicate, out-of-order, and stale-route ticks are classified outcomes and do not throw.
15. All new actors and actor changes follow explicit parse, validation, receive, failure, and handler-extension maps.

## 5. Ownership and identity model

### 5.1 Durable instrument identity

`ContractId` is the canonical, broker-independent and feed-independent string selected by Order Composition. The same value appears in:

- `CompositionLeg`;
- `TradeLegDefinition`;
- `ExecutionFillEvidence` after broker normalization;
- `EstablishedTradeDefinition` legs and original fills;
- `StrategyPositionLeg`;
- open-position route recovery rows;
- `ChangeTradeLegDataCommand`.

### 5.2 Provider identifiers

| Identifier | Owner | Lifetime | Durable domain use |
| --- | --- | --- | --- |
| Canonical `ContractId` | IFM Trade domain | Contract lifecycle | Authoritative instrument reference |
| Databento `InstrumentId` | Market Data Feed adapter/epoch | Feed definition or epoch | Feed ingestion and diagnostics only |
| IBKR `conId` | IBKR execution adapter | Broker contract lifecycle | Broker request, callback correlation, and audit metadata only |

### 5.3 Business identity allocation

For each Fund accepted by Portfolio:

```text
TradeOrderId = PortfolioId + FundId + OrderId
TradeEntityId = TradeOrderId + ReservedTradeId
PortfolioFundTradeLeg = PortfolioId + FundId + OrderId + TradeId + TradeLegId + TradeType
```

`ReservedTradeId` identifies the future established Trade component. It is allocated only after Fund acceptance. The established Trade becomes active only from accepted execution fills.

## 6. Target contract changes

### 6.1 Portfolio-independent composition

Revise `CompositionCandidate` so it no longer contains:

- `OrderId`;
- `PrimaryTradeId`;
- `PortfolioId` as a calculated result field;
- `FundId`;
- preliminary Fund assignment version;
- reservation-owned order and trade data.

Retain:

- candidate, workflow, selection, and composition identities;
- deployment, strategy, structure, and variant keys;
- product and target horizon;
- side, bias, premium mode, and strategy kind;
- canonical Contract IDs and leg ratios;
- prices, Greeks, payoff, liquidity, risk, execution envelope, versions, hashes, and expiry.

If the workflow activation carries the target Portfolio assignment, treat it as routing authority rather than evidence that the earlier pipeline belongs to a Fund.

### 6.2 Trade leg

Revise `TradeLegDefinition`:

- add required canonical `string ContractId`;
- remove required `uint MarketInstrumentId`;
- retain stable `TradeLegId`, asset family, signed quantity, price, expiry, strike, put/call, and any contract-definition evidence needed to detect an incorrect broker resolution;
- remove or rename `ContractKey` if it duplicates `ContractId`;
- use ordinal comparison and one canonical normalization rule.

Revise structural validation so it rejects empty or inconsistent Contract IDs, duplicate leg IDs, zero quantities, invalid topology, invalid asset family, and invalid timestamps without requiring a provider identity.

### 6.3 Fill evidence

Revise `ExecutionFillEvidence`:

- replace `MarketInstrumentId` with canonical `ContractId`;
- retain `TradeLegId`, component ID, quantity, price, commission, fill time, and external execution ID;
- allow broker-specific `conId` only in broker audit metadata where required;
- require the IBKR adapter to translate a callback back to exactly one approved Trade Leg before emitting normalized fill evidence.

### 6.4 Trade Position leg

Revise `StrategyPositionLeg`:

- replace `MarketInstrumentId` with canonical `ContractId`;
- retain price, basis, quantity, source sequence, source time, and stable Trade Leg ID;
- keep feed-specific sequence/generation evidence separate from durable Contract identity.

### 6.5 Route identity and command

Add a compact `PortfolioFundTradeLeg` value contract containing:

- Portfolio ID;
- Fund ID;
- Order ID;
- Trade ID;
- Trade Leg ID;
- trade or strategy type;
- route generation.

Add a shared `ChangeTradeLegDataCommand` containing:

- deterministic command ID;
- exact Trade Position entity identity;
- `PortfolioFundTradeLeg`;
- canonical Contract ID;
- price;
- source sequence;
- event time;
- route generation and source event identity.

The same command contract may be handled by each supported strategy-specific Trade Position command actor through its own mapped extension handler.

### 6.6 MessagePack compatibility

- Increment schema versions for modified durable contracts.
- Append new keys when compatibility is required; do not renumber existing keys.
- Preserve explicit readers for persisted workflow snapshots needed during cutover.
- Do not overwrite immutable historical Portfolio decisions or Trade events.
- Record a deliberate development-data reset only where the repository proves no authoritative history exists.

## 7. Gate 0 - inventory, documentation alignment, and baseline

1. Update the documents listed in section 3 to the approved identity and routing decisions.
2. Inventory every production and test reference to `CompositionCandidate.PortfolioId`, `FundId`, `OrderId`, `PrimaryTradeId`, `TradeLegDefinition.MarketInstrumentId`, `ExecutionFillEvidence.MarketInstrumentId`, `StrategyPositionLeg.MarketInstrumentId`, and `MarketPositionRoute`.
3. Inventory all callers of capacity reservation and Fund risk authorization before retiring the old RiskManager path.
4. Inventory all actor maps, host registrations, serializers, storage mappings, query DTOs, UI models, and benchmark fixtures affected by the contract changes.
5. Capture build and test baselines for Trade, Portfolio, Market Data, API Server, and UI projects.
6. Confirm whether any persisted workflow snapshot requires compatibility decoding rather than a development reset.

**Exit:** every affected symbol has a destination or retirement disposition, conflicting documentation is aligned, and baseline evidence is recorded.

## 8. Gate 1 - broker-neutral shared contracts

1. Implement the section 6 contract changes in the owning Shared projects.
2. Update Trade Order canonical hashing to include canonical Contract ID and exclude provider IDs.
3. Update equality, validation, and topology rules.
4. Update Order Execution fill-to-leg matching to use `TradeLegId` plus canonical Contract ID.
5. Update established Trade creation and original-fill retention.
6. Update Trade Position state construction to retain Contract IDs.
7. Add `PortfolioFundTradeLeg` and `ChangeTradeLegDataCommand` contracts.
8. Add MessagePack round-trip, frozen-payload compatibility, and invalid-input tests.

**Exit:** the durable Trade lifecycle compiles and validates without a Databento or IBKR numeric instrument ID.

## 9. Gate 2 - portfolio-neutral pipeline cutover

### 9.0 Verified current state and workflow root

The four stages have not yet completed this cutover. The implementation baseline is:

| Stage | Direct dependency today | Indirect dependency today | Required result |
| --- | --- | --- | --- |
| Regime Discovery | No direct Portfolio/Fund calculation field was found | `ExecuteRegimeDiscoveryPipelineCommand` serializes the complete workflow view, which contains `FundId` | Stage-specific input containing workflow, ITI signal, horizon, parameters, market evidence, and lineage only |
| Market Condition | `FundId` exists in execute command, result, snapshot, read model, queries, and storage access | Complete workflow view also carries early ownership | Contract/root, horizon, value date, parameters, market evidence, and lineage only |
| Trade Selection | Activation, binding, authority validation, evaluator, result, handoff, history, authorization, and paging use Portfolio/Fund | Starts by calling Portfolio to resolve one Fund | One highest-ranked opportunity or `NoTrade`, independent of Portfolio/Fund |
| Order Composition | Candidate, decision context, query/history, paging, validation, dispatch, and model use Portfolio/Fund/Order/Trade identities and a preliminary reservation | Expiry and hashes depend on the reservation | One complete composed opportunity or `NoCandidate`, with no financial ownership or allocated business identity |

The workflow root also accepts and stores `FundId` in `ExecuteIntrinsicTimeStrategyWorkflowCommand`, `StrategyWorkflowStartAcceptedEvent`, `IntrinsicTimeStrategyWorkflowState`, and `IntrinsicTimeStrategyWorkflowView`. Removing stage-level fields without removing this root assignment would leave the old dependency active.

Required workflow-root and activation changes:

1. Remove `FundId` from the workflow start command, accepted event, durable state, view, constructors, validation, fingerprints, diagnostic output, and test builders.
2. Remove any Portfolio/Fund identity whose only purpose is to preassign the opportunity before Portfolio evaluation.
3. Preserve workflow definition, ITI signal entity, canonical Contract ID, timeframe/horizon, value date, workflow execution identity, correlation, causation, deadlines, and parameter-set assignments.
4. Split strategy/catalog activation from financial ownership. The first four stages resolve strategy definitions and parameter sets without Portfolio access. RiskManager later resolves the target Portfolio using the authoritative workflow-definition/horizon/strategy-family assignment, and Portfolio alone selects Funds.
5. Add a versioned compatibility rule for persisted workflow snapshots. A legacy snapshot containing a preassigned Fund shall either finish on the legacy route during a bounded cutover or be rejected with an explicit incompatible-workflow reason; it shall not silently enter the new Portfolio handoff.
6. Update workflow status/detail output so Portfolio and Fund identities are absent until a committed Portfolio receipt exists.

### 9.1 Regime Discovery

- Preserve instrument, timeframe, parameter set, readiness, degradation, and evidence behavior.
- Remove only Portfolio/Fund requirements that exist solely to preassign financial ownership.
- Prove identical regime calculations for identical market inputs.
- Replace the serialized complete `IntrinsicTimeStrategyWorkflowView` in `ExecuteRegimeDiscoveryPipelineCommand` with the smallest immutable stage input containing workflow identity/revision, ITI trigger, canonical Contract ID, target horizon, parameter evidence, market-signal snapshot, lineage, and deadline.
- Remove constructors, validation, fingerprints, fixtures, and result checks that reach into the complete workflow view.
- Prove byte-equivalent results for identical market evidence and parameters regardless of external Portfolio configuration.

### 9.2 Market Condition

- Remove Fund identity from calculation input, result validation, and history keys where it does not affect the market assessment.
- Retain workflow, contract/product, horizon, parameter-set, and evidence identities.
- Preserve degraded and unavailable outcomes.
- Remove `FundId` from `ExecuteMarketConditionPipelineCommand`, `MarketConditionSnapshot`, `MarketConditionResult`, and `MarketConditionReadModel`.
- Replace the complete workflow view with the minimum immutable calculation input.
- Re-key latest/history queries, paging, and new storage schema versions by canonical Contract ID or instrument root, horizon, value date/time, workflow/result identity where required, and deterministic cursor.
- Replace Fund authorization in Market Condition query handlers with strategy-observation access control.
- Retain existing immutable Fund-keyed rows as legacy history rather than rewriting them.
- Prove that changing Portfolio or Fund configuration cannot change a Market Condition result produced from identical inputs.

### 9.3 Trade Selection

- Evaluate eligible strategy opportunities independently of a Fund.
- Return exactly one highest-ranked opportunity or `NoTrade`.
- Remove early Fund-specific authority and result validation.
- Move Portfolio routing assignment to the approved workflow/deployment mapping read by RiskManager.
- Remove `PortfolioId` and `FundId` from `TradeSelectionActivation` and its validation.
- Remove `PortfolioQueries.ResolveForSelectionAsync` from `StartTradeSelectionPipeline`.
- Replace `TradeSelectionPipelineInitialization(Binding, FundId)` with neutral strategy/catalog initialization.
- Remove Portfolio snapshot, Fund mandate, Fund allocation, Fund risk envelope, and Portfolio financial policy authority from `TradeSelectionBinding`.
- Resolve strategy family, variants, eligibility rules, and parameter sets from Reference/Configuration data without selecting a Fund.
- Remove Portfolio/Fund checks from contracts, authority validation, result validation, evaluator, completion handlers, and hashes.
- Remove the preliminary order/trade reservation and current `TradeSelectionHandoff` identity checks.
- Re-key result/history rows, queries, paging, authorization, and new storage schema versions by workflow, contract/product, horizon, value date, result identity, and deterministic cursor.
- Retain old Portfolio/Fund-keyed history as immutable legacy data.
- Prove that the same opportunity is selected independently of the number, identity, or financial state of Portfolios and Funds.

### 9.4 Order Composition

- Remove preliminary Portfolio order/trade reservation.
- Remove reservation-owned expiry and identity dependencies.
- Build one complete composed opportunity using canonical Contract IDs.
- Preserve stable candidate and evidence hashes across retries.
- Preserve one best candidate or `NoCandidate` behavior.
- Remove `OrderId`, `PrimaryTradeId`, `PortfolioId`, `FundId`, and preliminary assignment version from `CompositionCandidate`.
- Remove Portfolio/Fund fields from `CompositionDecisionContext`, result validation, query/history rows, paging, and new storage keys.
- Remove `Reservation` from the execute request, serialization, validation, catalog validation, preparation acceptance, dispatch, expiry calculation, and hashing.
- Remove every Order Composer call that allocates or validates Portfolio Order and Trade identities.
- Derive validity only from workflow, selection, market snapshot, contract trading lifetime, parameters, and candidate lifetime.
- Preserve stable composition, candidate, component, and Trade Leg identities without allocating financial business IDs.
- Retain canonical Contract IDs, topology, leg ratios/sides, prices, Greeks, payoff, liquidity, objective risk evidence, execution envelope, versions, hashes, and expiry.
- Re-key history queries, paging, authorization, and new storage schema versions without Portfolio/Fund ownership.
- Retain old Portfolio/Fund-keyed history as immutable legacy data.
- Prove byte-equivalent composition results for identical selection and market inputs regardless of Portfolio/Fund configuration.

### 9.5 Workflow state

- Revise state, view, events, completion commands, diagnostics, and history records affected by the neutral pipeline contracts.
- Ensure old Fund-specific checkpoints cannot enter the new RiskManager handoff unnoticed.
- Update MessagePack contracts by appending keys or introducing explicit versioned replacements; never reuse a key for a different meaning.
- Update stage-result envelopes, state application, Redispatch behavior, correlation/causation, canonical hashes, UI detail models, and diagnostic renderers.
- Remove Portfolio/Fund dimensions from new first-four-stage projection primary keys and paging tokens.
- Preserve legacy readers only for immutable history that must remain queryable.
- Confirm that Portfolio/Fund/Order/Trade identities first appear in workflow state only after `PortfolioOrderCompositionCompletedEvent` commits.

### 9.6 Required cutover tests

- Repository and reflection audits find no Portfolio/Fund service dependency in the four pipeline actors or their calculation handlers.
- Regime Discovery and Market Condition outputs remain invariant under Portfolio/Fund configuration changes.
- Trade Selection returns the same highest-ranked opportunity with zero, one, or many configured Funds.
- Order Composition creates no Portfolio, Fund, Order, or Trade identity and performs no reservation call.
- The workflow reaches RiskManager with all pre-Portfolio business identities absent.
- Legacy Fund-bound snapshots cannot silently enter the neutral path.
- Neutral history queries page deterministically without Portfolio/Fund keys.
- `NoTrade` and `NoCandidate` remain normal terminal results.
- Complete error details survive every updated stage boundary.
- MessagePack round trips, frozen compatibility payloads, canonical hashes, actor parse/receive maps, projections, and UI detail models remain valid.

**Exit:** the workflow root and all four pre-RiskManager stages run without a Portfolio/Fund service dependency; their current/result/history contracts contain no preassigned Fund, Order, or Trade identity; RiskManager receives one neutral composed opportunity and is the first stage allowed to resolve the target Portfolio.

## 10. Gate 3 - RiskManager calculation and Portfolio request preparation

1. Split RiskManager logic into:
   - portfolio-independent opportunity risk and market restrictions;
   - Portfolio-owned Fund sizing, capacity, and final acceptance.
2. Remove the Fund-specific financial admission snapshot from `PrepareRiskManagement`.
3. Remove preallocated Order and Trade IDs from `RiskAssessmentResult`, sized legs, hashes, and validators.
4. Retain per-unit loss, stress loss, margin evidence, costs, liquidity maximum, execution envelope, and all configuration/evidence hashes needed by Portfolio.
5. Resolve the target Portfolio from the workflow definition/deployment assignment.
6. Map the composition and risk output into one complete `PortfolioOrderCandidate` using canonical Contract IDs.
7. Produce deterministic component and Trade Leg IDs that remain identical on retry.
8. Validate the full candidate locally before saving the handoff.
9. Produce normal `NoTrade` when market or opportunity rules reject the candidate before Portfolio.
10. Produce a classified preparation failure with complete context for invalid contracts or configuration.

**Exit:** RiskManager can persist one exact, valid Portfolio request without choosing a Fund and without consulting a market-data provider identity map.

## 11. Gate 4 - durable RiskManager handoff state

Add a versioned workflow handoff state containing:

- handoff phase;
- operation and command IDs;
- Portfolio ID;
- expected financial revision;
- exact `EvaluatePortfolioOrderCompositionCommand`;
- input hash and expiry;
- committed Portfolio completion/receipt identity;
- returned status and per-Fund decisions;
- accepted Trade Orders;
- per-order Trade Order and execution-dispatch checkpoints;
- complete classified failure details.

Add standard workflow commands and extension handlers to:

1. prepare and persist the exact Portfolio request;
2. accept and validate a Portfolio completion;
3. record `NoTradeOrders`;
4. record a typed Portfolio failure;
5. record Trade Order materialization progress;
6. record Order Execution dispatch progress;
7. complete or fail the workflow.

All command actor support shall use the existing single base actor, explicit parse/validation/receive maps, and domain behavior in extension handlers.

Retry rules:

- persist before sending;
- resend only the saved request;
- identical retry returns the original Portfolio decision and IDs;
- conflicting input under the same operation fails;
- timeout or lost reply is not treated as rollback proof;
- no timer poller or recovery service is introduced.

**Exit:** restart or duplicate delivery resumes the exact handoff without allocating new identities or repeating a committed decision.

## 12. Gate 5 - Portfolio-owned Fund evaluation and atomic acceptance

### 12.1 Request

Expand `PortfolioOrderCandidate` only as needed to carry complete immutable market opportunity and per-unit risk evidence. It shall carry no preselected Fund, Order, or Trade ID.

### 12.2 Evaluation

Under the Portfolio financial-authority lock, evaluate every eligible Fund against:

- Portfolio and Fund operating state;
- `CanSpend`;
- workflow/deployment/strategy-family assignment;
- target horizon assignment;
- exact authority and policy versions;
- available cash and required capital;
- per-trade maximum risk;
- Fund, Portfolio, and underlying/product capacity limits;
- held, working, and position usage;
- liquidity maximum units;
- expiry, execution account, and environment.

For each Fund:

1. determine an accepted unit count or a stable rejection reason;
2. resize the canonical Trade legs using the accepted units;
3. allocate one Order ID only when accepted;
4. allocate one reserved Trade ID per accepted component;
5. create one fully validated Trade Order;
6. calculate the exact capacity effect.

Use `TradeOrderDefinition.Validate()` before persistence. `NoTradeOrders` is a completed business decision, not an exception.

### 12.3 Atomic storage

Within the existing EventSource-owned PostgreSQL transaction, commit:

- one composition decision;
- one decision for every evaluated Fund;
- one accepted Trade Order per accepted Fund;
- all accepted components and legs;
- accepted capacity allocation/usage changes;
- immutable Portfolio risk evidence;
- financial revision;
- operation receipt;
- completed event and delivery metadata.

Rollback all rows and the event on validation, expiry, concurrency, or storage failure. Sequence gaps after rollback are allowed.

### 12.4 Later execution transitions

Define the Portfolio commands/events that release or convert accepted capacity when an order expires, execution is rejected with proven zero exposure, fills become working/position exposure, or a position closes. These later transitions use the exact Portfolio/Fund/Order/Trade identities and do not reopen the original composition decision.

**Exit:** zero, one, or multiple Fund decisions are complete, atomic, idempotent, and financially coherent.

## 13. Gate 6 - Portfolio API call and outcome handling

1. Add `IPortfolioOrderCompositionApi` to the RiskManager realtime/command contexts that perform the handoff.
2. Remove `IMarketDataApi` from this handoff.
3. Use the existing typed NATS Portfolio client and Function route.
4. Validate response identity, command ID, operation ID, Portfolio ID, workflow ID, composition ID, input hash, financial revision, status, and Trade Order list.
5. Enforce:

```text
ExecuteTradeOrders => TradeOrders.Count > 0
NoTradeOrders      => TradeOrders.Count == 0
```

6. Persist the validated completion before dispatching Trade Orders.
7. End the workflow normally on `NoTradeOrders` with all Fund rejection reasons visible.
8. Preserve full error details for validation, authority, timeout, database, uncertain commit, and malformed reply failures.

**Exit:** RiskManager consumes the committed Portfolio outcome exactly once and does not use the old reservation/Fund-authorization path.

## 14. Gate 7 - Trade Order materialization and Order Execution dispatch

For each accepted `TradeOrderDefinition`:

1. derive deterministic command IDs from Portfolio operation, Order ID, and transition;
2. send `CreateTradeOrderCommand`;
3. send `ApproveTradeOrderCommand`;
4. send `ReadyTradeOrderCommand`;
5. derive a stable execution-attempt ID;
6. send `BindTradeOrderExecutionCommand`;
7. allow the existing Trade Order event projector to emit `StartOrderExecutionCommand` after Bind commits, using the configured Manual or Broker channel;
8. persist each successful RiskManager-owned boundary in workflow handoff state.

Reconcile the Portfolio-returned Approved status with the current Trade Order state machine, which materializes Create as Draft. Use the explicit Draft -> Approved -> Ready transitions unless a separately reviewed direct Portfolio-acceptance transition is introduced.

Partial dispatch rules:

- do not roll back a committed Portfolio acceptance because NATS or Order Execution is unavailable;
- resume only incomplete transitions after restart;
- treat already-applied deterministic commands as success;
- preserve the exact order identity and definition hash;
- expose the exact order and transition that remains pending or failed.

**Exit:** every accepted Portfolio order has one idempotent Trade Order stream and one bound Order Execution attempt, with no dispatch before Portfolio commit.

## 15. Gate 8 - IBKR Contract ID translation boundary (later implementation)

The broker execution boundary shall receive canonical Trade Orders. Immediately before broker submission, the IBKR adapter shall:

1. resolve every canonical Contract ID to exactly one IBKR contract and `conId`;
2. verify security type, symbol/root, exchange, currency, expiry, strike, right, and multiplier against the approved leg evidence;
3. build one exact IBKR outright or combination order;
4. reject missing, ambiguous, or conflicting resolutions before submitting any leg;
5. retain a request-scoped canonical Contract ID to IBKR `conId` correlation map;
6. translate IBKR callbacks and fills back to canonical Contract ID and Trade Leg ID;
7. emit only normalized broker-neutral execution messages into the Trade domain.

IBKR `conId` may be retained in broker audit evidence but shall not replace canonical Contract ID in durable domain state.

If resolution fails after Portfolio acceptance:

- Order Execution records a typed rejection;
- no partial broker submission occurs;
- Portfolio receives the exact zero-exposure release transition;
- RiskManager/Operations exposes the complete Contract ID and broker-resolution reason.

Live IBKR connectivity and production trading acceptance require a separate approved trading window. The adapter boundary, emulator, and their deterministic tests are outside the approved implementation represented by this completed plan. This section preserves the required future boundary contract.

**Exit:** provider-specific broker identity exists only inside the IBKR boundary and normalized fills match approved canonical legs exactly.

## 16. Gate 9 - Contract-ID position router

### 16.1 Router ownership and shape

Replace the numeric route index with an actor-owned reverse index equivalent to:

```text
Dictionary<string, PortfolioFundTradeLeg[]> routes
```

Use `StringComparer.Ordinal`, pre-sized dictionaries, and copy-on-write route arrays. The realtime actor exclusively owns mutations and reads; no cross-actor shared dictionary or locking is permitted.

### 16.2 Route registration

After a Trade Position open transition commits:

- publish one complete route-change message containing every open leg;
- add each `PortfolioFundTradeLeg` under its canonical Contract ID;
- make duplicate registration idempotent.

After close commits:

- remove all routes for the exact Portfolio/Fund/Order/Trade position;
- retain other positions using the same Contract ID;
- make duplicate removal a no-op.

After correction, roll, or supported leg replacement:

- atomically replace that position's route set;
- increment route generation;
- reject older route messages.

The position actor does not mutate realtime memory directly. It commits state and sends a route-change actor message; the realtime actor mutates its own index.

### 16.3 Tick routing

For each futures or futures-option trade tick:

1. read the canonical Contract ID already present in `EntityId`/`TickDataId`;
2. validate the event Contract IDs agree;
3. perform one dictionary lookup;
4. walk the stable route array;
5. derive the destination command actor from trade type;
6. derive the actor thread identity from Portfolio/Fund/Order/Trade identity;
7. send one `ChangeTradeLegDataCommand` per matching leg;
8. continue fan-out when one destination fails.

The Trade Position actor applies its route-generation, source-sequence, leg-membership, and open-state fences before updating the leg and recalculating the coherent whole-position state.

### 16.4 Expected outcomes

Classify and count without exceptions:

- no open position;
- unknown Contract ID;
- duplicate or out-of-order source sequence;
- stale route generation;
- already closed position;
- not-yet-open position;
- invalid tick.

Diagnostics are first-occurrence, sampled, or periodically aggregated. The hot path does not allocate a log message for every ignored tick.

### 16.5 Startup reconstruction

1. Subscribe or buffer route lifecycle messages.
2. Load one complete snapshot of currently open Trade Position legs using canonical Contract IDs.
3. replace the in-memory index;
4. apply buffered changes newer than the snapshot generation/watermark;
5. mark routing ready and process live ticks.

There is no recurring database query. A tick received before readiness is classified and observed without throwing.

**Exit:** one Contract ID fans out deterministically to all matching Portfolio/Fund/Order/Trade legs, and open/close/change events maintain the index without polling.

## 17. Gate 10 - storage and query schema changes

Apply additive, versioned changes for:

- canonical Contract ID on accepted Portfolio order legs;
- canonical Contract ID on Trade Order, execution fill, established Trade, Trade Position current/history, and route recovery rows;
- Portfolio per-Fund decision and accepted capacity evidence;
- RiskManager Portfolio handoff and per-order dispatch observation;
- any paging/index changes caused by removing early Fund ownership from pipeline history.

Requirements:

- centralize Portfolio SQL in `PortfolioDbSql.cs`;
- preserve the single sealed PostgreSQL `PortfolioDbContext` and its read/write interfaces;
- keep authoritative Portfolio changes in the EventSource-enlisted transaction;
- retain provider IDs only in provider-owned audit/projection tables;
- create indexes for Contract-ID open-route reconstruction and Portfolio operation lookup;
- make clean install and upgrade idempotent;
- do not overwrite immutable definitions or completed decisions.

**Exit:** clean creation, compatible upgrade, rollback, query, paging, and reconstruction tests pass.

## 18. Gate 11 - actor and host wiring

1. Update Shared message registration and serializer qualification.
2. Update the Intrinsic Time workflow command and realtime contexts.
3. Update explicit command/realtime parse and receive maps.
4. Add extension handlers for every new command or route-change message.
5. Update Portfolio Function dependencies and transactional repository wiring.
6. Update Trade Order and Order Execution actor routing.
7. Update Futures and Futures Option realtime actor subscriptions.
8. Register the Contract-ID route indexes as actor-owned context state.
9. Update API Server and any console/verification host registrations.
10. Verify parse/receive parity and exact-type rejection for every affected actor.

No additional actor base class, coordinator actor, denormalizer actor, background worker, or polling service shall be introduced.

**Exit:** all production hosts resolve the same contracts and actor routes, with no old numeric route or RiskManager reservation route active.

## 19. Gate 12 - observability and failure behavior

Expose structured events, traces, metrics, and UI/query details for:

- Portfolio operation and command IDs;
- workflow, composition, and evidence hashes;
- Portfolio decision status and financial revision;
- accepted and rejected Fund counts;
- stable per-Fund reason codes;
- accepted Portfolio/Fund/Order/Trade identities;
- Trade Order and Order Execution dispatch phases;
- canonical Contract IDs;
- IBKR resolution failures and external references;
- router route count, additions, removals, replacements, and generation;
- tick counts for received, routed, fan-out, unrouted, invalid, duplicate, stale, closed, and failed destination sends;
- mailbox depth and processing latency through Supervisor actor metrics.

Failure details shall include the causal exception where one exists, complete domain reason codes, and the exact identity at the failed boundary. Expected routing outcomes shall not be logged as exceptions.

**Exit:** an operator can determine whether a workflow is awaiting Portfolio, accepted with no Funds, accepted and dispatching orders, blocked in execution, or degraded in live position routing.

## 20. Gate 13 - automated qualification

### 20.1 Unit tests

- canonical Contract ID equality, hashing, normalization, validation, and MessagePack round trips;
- removal of provider identity requirements from durable Trade contracts;
- composition candidate contains no Fund, Order, or Trade identity;
- RiskManager candidate mapping and deterministic IDs/hashes;
- Portfolio Fund eligibility, sizing, capacity, and stable reason codes;
- no business ID allocation for rejected Funds or `NoTradeOrders`;
- one Order ID and one reserved Trade ID per accepted order/component;
- full Trade Order validation before commit;
- old reservation/Fund-authorization path is absent from RiskManager maps;
- Trade Order materialization transition sequence;
- IBKR resolution success, missing, ambiguous, and field-conflict cases;
- IBKR callback-to-canonical-leg normalization;
- Contract-ID route add, duplicate add, remove, replace, generation, and one-to-many lookup;
- `ChangeTradeLegDataCommand` mapping for Futures, Iron Condor, Vertical Spread, and future supported trade types;
- ignored tick outcomes and failed-destination continuation.

### 20.2 BDD tests

- one opportunity accepted by one Fund produces one Trade Order;
- one opportunity accepted by several Funds produces one order per Fund;
- one Fund rejection does not block another accepted Fund;
- no eligible Fund completes normally with `NoTradeOrders`;
- identities do not exist before Portfolio acceptance;
- Portfolio acceptance precedes Trade Order and Order Execution dispatch;
- identical retry returns identical orders and reserved Trade IDs;
- one Contract ID updates positions in multiple Portfolios and Funds;
- opening a position adds all leg routes;
- closing a position removes only its routes;
- tick before open and after close is ignored;
- close/tick race is fenced without throwing;
- restart snapshot plus newer lifecycle messages produces the correct route set.

### 20.3 PostgreSQL integration tests

- one transaction commits decision, Funds, orders, legs, capacity, receipt, revision, and event;
- injected failure at every write boundary rolls back everything;
- duplicate and conflicting request behavior;
- concurrency under the same Portfolio financial authority;
- expiry before and during evaluation;
- reusable test database runs do not collide because a process-local allocator resets;
- route recovery query returns only currently open canonical Contract-ID routes.

### 20.4 Actor/NATS integration tests

- real RiskManager request reaches Portfolio Function and receives only committed completion;
- lost Portfolio response plus identical retry returns original completion;
- malformed reply cannot advance workflow;
- `NoTradeOrders` produces no Trade Order command;
- accepted orders create exact Trade Order streams and execution attempts;
- partial dispatch restart resumes only incomplete transitions;
- route lifecycle and tick messages reach the correct realtime and Trade Position mailboxes;
- one failed position destination does not block remaining fan-out.

### 20.5 End-to-end verification

The completed backend verification boundary is:

```text
ITI -> Regime Discovery -> Market Condition -> Trade Selection
    -> Order Composition -> RiskManager -> Portfolio
    -> TradeOrder -> OrderExecution start request
```

After the IBKR adapter/emulator implementation is approved, extend verification through:

```text
OrderExecution -> emulator fill -> established Trade -> Trade Position open
    -> Contract-ID tick fan-out -> ChangeTradeLegDataCommand
    -> Trade Position close -> route removal
```

The current gate verifies identities, hashes, per-Fund decisions, accepted capacity effects, route contents, restart behavior, and full error details. Original fills, live P&L, and execution-driven capacity conversion/release belong to the later execution implementation.

**Exit:** all affected tests inside the approved backend boundary pass; separately deferred Regime, broker/fill, Supervisor UI, and legacy Trade Order UI work remains explicitly identified.

## 21. Gate 14 - performance and soak qualification

Use BenchmarkDotNet and runtime telemetry to measure:

- RiskManager candidate mapping allocations;
- Portfolio evaluation for 0, 1, 8, 32, and 128 Funds;
- atomic PostgreSQL latency for 0, 1, and multiple accepted orders;
- Contract-ID route lookup with 0, 1, 4, 16, and 64 destinations;
- add/remove/replace cost outside the tick hot path;
- `ChangeTradeLegDataCommand` construction and fan-out allocations;
- Trade Position update throughput and allocation by strategy leg count;
- mailbox depth, P50/P95/P99 latency, and recovery after burst traffic.

Acceptance requirements:

- no database or provider lookup per tick;
- no managed allocation for a valid unrouted Contract-ID lookup;
- no route-collection allocation for a routed lookup;
- bounded fan-out allocation limited to the commands that must cross actor boundaries;
- no exception allocation for expected routing outcomes;
- stable per-contract ordering under sustained bursts;
- startup reconstruction completes before the configured readiness deadline;
- no leaked test host, NATS process, broker emulator, or application process after qualification.

The live-market soak remains scheduled for the separately approved Monday trading window. Real IBKR acceptance additionally requires the later IBKR adapter implementation and credentials.

**Exit:** benchmark evidence is recorded. The Monday integrated soak will add bounded-memory, GC, mailbox-depth, and recovery evidence without reopening the completed implementation gates.

## 22. Retirement and removal list

After replacement behavior passes all gates, remove or retire:

- Order Composer reservation-based identity allocation;
- `CompositionCandidate.OrderId`, `PrimaryTradeId`, and `FundId` dependencies;
- Fund-specific RiskManager preparation and sizing authority where Portfolio now owns the decision;
- RiskManager `EnsureFundCompositionAsync`;
- `RiskFinancialHandoff` reservation and Fund-authorization phases;
- `AdvanceRiskFinancialHandoffCommand` where it has no remaining owner;
- RiskManager calls to `FinancialApi.ReserveAsync` and `PortfolioCommands.AuthorizeRiskAsync` for this flow;
- numeric `MarketInstrumentId` requirements in durable Trade lifecycle contracts;
- `MarketInstrumentRouteIndex` and `MarketPositionRoute` after Contract-ID routing replaces them;
- strategy-specific tick-price update commands replaced by `ChangeTradeLegDataCommand`, after all actor maps are migrated;
- obsolete database columns, projections, tests, and host registrations only after additive compatibility qualification permits removal.

Do not remove shared capacity or financial operations still used by another approved workflow until their callers have been inventoried and migrated.

## 23. Implementation order and dependency chain

```text
Gate 0  Inventory and documentation
  -> Gate 1  Broker-neutral contracts
  -> Gate 2  Portfolio-neutral pipeline
  -> Gate 3  RiskManager preparation
  -> Gate 4  Durable handoff state
  -> Gate 5  Atomic Portfolio acceptance
  -> Gate 6  Runtime Portfolio API call
  -> Gate 7  TradeOrder and OrderExecution dispatch
  -> Gate 8  IBKR translation boundary [later implementation]
  -> Gate 9  Contract-ID position router
  -> Gate 10 Storage and query migration
  -> Gate 11 Actor and host wiring
  -> Gate 12 Observability
  -> Gate 13 Automated qualification
  -> Gate 14 Benchmarks and soak
```

Contract and migration work may be prepared together, but runtime cutover shall follow the dependency order. Old and new writers shall not both allocate business identities or maintain active route indexes.

## 24. Blocker conditions

Stop implementation only if one of these domain conditions is found:

1. No authoritative workflow-definition-to-Portfolio assignment exists for RiskManager routing.
2. The canonical Contract ID does not uniquely identify the exact futures or option contract required by Portfolio and IBKR resolution.
3. Required Fund acceptance evidence cannot be represented by the current Portfolio financial authority, limits, and balances.
4. Another component claims authoritative ownership of Fund acceptance, Order ID allocation, Trade ID allocation, or accepted capacity.
5. Portfolio and EventSource atomic tables are configured in different physical PostgreSQL databases.
6. Existing authoritative events require a contract migration that cannot be decoded without changing their meaning.

Ordinary compile failures, test failures, schema defects, stale development projections, missing benchmarks, or integration-fixture defects are implementation work rather than domain blockers.

## 25. Definition of done

The implementation is complete only when:

- the first four pipeline stages can produce one portfolio-independent opportunity;
- RiskManager persists and sends one exact Portfolio request without Fund, Order, Trade, Databento, or IBKR identity assignment;
- Portfolio evaluates all eligible Funds and atomically returns `ExecuteTradeOrders` or `NoTradeOrders`;
- accepted orders contain Portfolio, Fund, Order, reserved Trade, stable Trade Leg, and canonical Contract identities;
- accepted capacity is committed with the Portfolio decision and completed event;
- the old staged reservation and Fund-authorization RiskManager path is inactive;
- every accepted order is materialized once and dispatched once to Order Execution;
- the durable contracts preserve the future IBKR boundary without storing provider identity in Portfolio or Trade state;
- established Trade contracts preserve original fills and canonical Contract IDs for the later execution implementation;
- Trade Position open/close/change lifecycle messages maintain the in-memory Contract-ID router;
- a tick fans out `ChangeTradeLegDataCommand` to every matching Portfolio/Fund/Order/Trade leg;
- missing or closed routes never throw and never block other destinations;
- restart reconstructs open routes once and requires no polling;
- complete operational details are emitted through workflow, Portfolio, execution, and route observations; Supervisor UI aggregation remains part of the separate Supervisor workstream;
- all automated gates within the approved backend boundary pass and their evidence is recorded in this document.
