# Regime Discovery Implementation Specification

Implementation Specification v1.0

| Item | Value |
| --- | --- |
| Status | Proposed repository-specific implementation plan; no production implementation started |
| Date | 2026-08-26 |
| Design authority | `Regime-Discovery-Specification-v1.0.md` |
| Workflow authority | `Intrinsic-Time-Strategy-Workflow-Implementation-v1.0.md` |
| Target | Deterministic V1 / .NET 10 actor application |

## 1. Approved decisions

1. Each Strategy Workflow calculates only its own Daily, Weekly, or Monthly
   horizon. It may use configured supporting observation timeframes, but it
   does not calculate the other two workflow horizons.
2. The deterministic scoring, confidence, fusion, required-signal,
   freshness, quality, restriction, and reason-code rules are defined in the
   companion design specification and are implementation requirements.
3. PostgreSQL `ConfigurationDbContext` is authoritative for immutable,
   versioned strategy and pipeline parameter sets.
4. Configuration lifecycle belongs to the Reference bounded context under
   `Configuration/Trade/StrategyWorkflow` and
   `Configuration/Trade/StrategyWorkflow/Pipeline`.
5. Regime Discovery captures one immutable latest-signal snapshot and never
   substitutes database reads or numeric defaults for unavailable required
   hot signals.
6. Regime Discovery has one event-sourced Command actor. Trend, Volatility,
   Market Structure, and Fusion are sealed actor-owned calculation models,
   not actors. There is no Regime Discovery Realtime actor in V1.
7. The Command actor dispatches `StartRegimeDiscoveryPipelineCommand` through
   `_receiveMap` to an asynchronous command extension returning
   `Task<ServiceResult<GuidResult>>`. The extension owns calculation,
   private terminal-event creation, and the state update.
8. The EventProjector writes the final result or failure to ScyllaDB before it
   publishes the public `RegimeDiscoveryPipelineCompletedEvent` or
   `RegimeDiscoveryPipelineFailedEvent` to Strategy Workflow. V1 publishes no
   separate Regime Discovery Processing event.
9. Trend, Volatility, and Market Structure may run on ordinary .NET thread
   pool work and be awaited together only if repeatable benchmarks show a
   material benefit over deterministic sequential execution.

## 2. Project-dependency boundary

`TomasAI.IFM.Domain.Reference.Shared` already references
`TomasAI.IFM.Domain.Trade.Shared`. Making Trade.Shared reference
Reference.Shared would create a circular project dependency. Therefore:

- immutable parameter-set types used by pipeline messages live with their
  Trade.Shared pipeline contracts;
- Reference.Shared owns configuration lifecycle commands, queries, identities,
  metadata, and public read models;
- Domain.Reference implements configuration command/query actors under the
  approved Reference/Configuration hierarchy; and
- Application.Storage implements authoritative PostgreSQL persistence through
  ConfigurationDbContext.

This separates contract ownership from configuration lifecycle ownership
without introducing a new shared project or changing existing project
dependencies.

## 3. Configuration architecture

### 3.1 Logical Reference layout

```text
TomasAI.IFM.Domain.Reference.Shared/Configuration/
  Common/
  Trade/
    StrategyWorkflow/
      Pipeline/
        RegimeDiscovery/
        MarketCondition/
        TradeSelection/
        OrderComposition/
        RiskManagement/

TomasAI.IFM.Domain.Reference/Configuration/
  Trade/
    StrategyWorkflow/
      Command/ Query/
      Pipeline/
        RegimeDiscovery/Command/ Query/
        MarketCondition/Command/ Query/
        TradeSelection/Command/ Query/
        OrderComposition/Command/ Query/
        RiskManagement/Command/ Query/
```

Configuration is partitioned first by owning domain. Future Fund, MarketData,
Securities, or other configuration families receive sibling folders below
`Reference/Configuration`; they do not share the Trade strategy hierarchy.

The first implementation exposes Regime Discovery end to end. The other
pipeline tables and type-specific repository methods are created so later
pipeline specifications do not require a new storage architecture. Business
properties for later parameter sets remain opaque JSON until their approved
specifications exist.

### 3.2 PostgreSQL storage

Proposed files:

- `TomasAI.IFM.Application.Storage/ConfigurationDb/ConfigurationDbContext.cs`
- `TomasAI.IFM.Application.Storage/ConfigurationDb/IConfigurationDbContext.cs`
- `TomasAI.IFM.Application.Storage/ConfigurationDb/ConfigurationDbSql.cs`
- `TomasAI.IFM.Application.Storage/ConfigurationDb/ConfigurationDbParameter.cs`
- `TomasAI.IFM.Application.Storage/ConfigurationDb/Schema/ConfigurationSchemaDb.cs`
- `TomasAI.IFM.Application.Storage/ConfigurationDb/Schema/ConfigurationSchemaSql.cs`

`ConfigurationDbConnection` is a PostgreSQL connection. Development may point
it at the same physical database as EventSourceDb, but ConfigurationDb uses its
own `reference_configuration` PostgreSQL schema and context.

Tables:

- `reference_configuration.intrinsic_time_strategy_workflow_parameter_set`
- `reference_configuration.regime_discovery_parameter_set`
- `reference_configuration.market_condition_parameter_set`
- `reference_configuration.trade_selection_parameter_set`
- `reference_configuration.order_composition_parameter_set`
- `reference_configuration.risk_management_parameter_set`

Every table has:

```text
parameter_set_id uuid
version integer
schema_version smallint
status smallint
effective_from_utc timestamptz null
retired_at_utc timestamptz null
payload_json jsonb
payload_sha256 text
description text
created_utc timestamptz
created_by text
PRIMARY KEY (parameter_set_id, version)
```

Published parameter identity, version, schema, payload, and hash are
immutable. Guarded lifecycle metadata may publish or retire a row; changing a
published payload inserts a new version. Publication verifies that the
deserialized typed payload is valid and that its canonical JSON SHA-256 equals
the stored hash. Selection is explicit by ID/version or deterministic by
effective time; ties are rejected as configuration faults. No historical
workflow replay re-resolves an effective version.

### 3.3 Factory, DI, and startup changes

Append `ConfigurationDb` and `ConfigurationSchema` to
`IDbContextFactory`/`DbContextFactory`. Register the PostgreSQL connection,
context, interface, and schema in Application.Api.Server Startup and add the
connection string to development/production settings without embedding
credentials.

### 3.4 Workflow resolution

The Strategy Workflow Command context receives an immutable configuration
resolver abstraction. When accepting a new ITI trigger, the Command actor:

1. resolves the effective Intrinsic Time Strategy Workflow parameter set;
2. resolves its referenced Regime Discovery parameter-set ID/version;
3. validates and canonicalizes the typed RegimeDiscoveryParameterSet;
4. commits the selected identities, payload hash, and immutable parameter
   payload with the accepted workflow transition; and
5. allows the EventProjector/Realtime dispatch path to append the parameter
   set to `StartRegimeDiscoveryPipelineCommand`.

The payload is part of durable workflow history. A replay uses the recorded
payload and never reads ConfigurationDb to reinterpret an old workflow.

## 4. Current hot-cache and indicator inventory

### 4.1 Infrastructure that exists

| Capability | Current contract/source | Assessment |
| --- | --- | --- |
| Latest live futures price | `IMarketDataApi.TryGetLastTickPrice` returning `FuturesMarketPriceSnapshot` from TickAggregation | Usable; has contract/value-date identity, trade source sequence, and exchange timestamp |
| Stream activity | `IMarketDataApi.IsTickDataStreamActive` | Usable as live-price validity evidence |
| RSI(14) latest value | `FuturesRsiSignalCacheModel` and Daily counterpart in Redis blackboard | Partly usable; only indicator with an explicit latest-value cache |
| RSI contract | `FuturesRsiSignalReadModel` | Usable values and source provenance are present |
| TDI | `FuturesTdiSignalReadModel` and Scylla latest query | Values/provenance exist; no unified latest cache; intraday only |
| MACD | `FuturesMacdSignalReadModel` and Scylla latest query | Values exist; latest cache and source sequence/event timestamp are missing |
| ADX | `FuturesAdxSignalReadModel` and Scylla latest query | ADX/+DI/-DI exist; latest cache and source provenance are missing |
| ATR | `FuturesAtrSignalReadModel` and Scylla latest query | ATR/true range exist; baseline ratio, latest cache, and source provenance are missing |
| ITI | `FuturesItiSignalGeneratedEvent` / `FuturesItiSignalV2ReadModel` | Target-horizon trigger has direction, band level, reversal level, sequence, and intrinsic time |
| Daily Bollinger values | futures EOD read models | Only Daily/EOD context; no intraday latest-value contract or width baseline |
| VIX futures EOD | VIX EOD/open-price Redis blackboard models | Historical/EOD context only; not a current VIX spot/term-structure snapshot |
| Generic caches | Redis blackboard, `IDataCacheService`, `IDbCache`, latest-value channels | Reusable primitives, but none provides an atomic typed regime snapshot |

Scylla latest queries are useful for startup warming, diagnostics, and tests.
They are not the hot-path source during a Regime Discovery calculation.

### 4.2 Missing signal infrastructure

The following must exist before the full deterministic V1 pipeline can be
enabled:

- one typed latest-signal cache keyed by instrument, signal type, observation
  timeframe, and calculation configuration;
- atomic snapshot capture with a cache revision and immutable values;
- common metadata: MarketDataAsOfUtc, CalculatedAtUtc, source sequence,
  IsWarm, IsValid, schema version, and calculation version;
- EMA20/EMA50/EMA200 values, ATR-normalized slopes, and prior values;
- ATR baseline and ATR ratio;
- intraday Bollinger(20,2) width, position, and width baseline;
- rolling 20-observation high/low and breakout-distance inputs;
- current VIX level plus front/second VIX-futures term structure;
- prior volatility composite inputs needed for expanding/contracting; and
- optional realized-volatility percentile.

### 4.3 Proposed snapshot provider

Add `IRegimeDiscoveryMarketSignalSnapshotProvider` with its provider-neutral
request/response contracts in Domain.MarketData.Analytics.Shared and a
singleton implementation in the Market Data Analytics boundary. Generated
indicator events update immutable cache entries. Startup warming loads only
the most recent compatible projection for each configured key, marks it warm
after validation, and then realtime events advance it. The authoritative
upstream design is `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md`.

Each mutation increments a monotonic cache revision. Capture reads the
revision, gathers all configured keys, then verifies that the revision did not
change; it retries a bounded number of times before reporting a consistency
failure. The returned `RegimeDiscoveryMarketSignalSnapshot` is immutable and records
the capture revision, snapshot identity, target horizon, supporting
timeframes, all source identities/timestamps, and data-quality results.

The V1 cache is process-local because the current API host runs the relevant
actors together. The provider interface prevents that deployment assumption
from leaking into Regime Discovery; a later distributed cache/actor-backed
implementation can replace it without changing pipeline contracts.

## 5. Parameter and result contracts

Proposed Trade.Shared paths:

```text
Strategy/Workflow/IntrinsicTime/Pipeline/Configuration/RegimeDiscovery/
  RegimeDiscoveryParameterSet.cs
  RegimeDiscoveryHorizonConfiguration.cs
  TrendRegimeConfiguration.cs
  VolatilityRegimeConfiguration.cs
  MarketStructureRegimeConfiguration.cs
  MarketRegimeFusionConfiguration.cs
  RegimeDiscoveryFreshnessConfiguration.cs
  RegimeDiscoveryDataQualityConfiguration.cs

Strategy/Workflow/IntrinsicTime/Pipeline/RegimeDiscovery/Model/
  TrendRegimeResult.cs
  VolatilityRegimeResult.cs
  MarketStructureRegimeResult.cs
  MarketRegimeFusionResult.cs
  RegimeDiscoveryResult.cs
  RegimeDiscoveryEvidence.cs
  RegimeDiscoveryReason.cs
  RegimeDiscoveryEnums.cs
```

The market-signal snapshot, requirement, availability, signal envelope, and
provider contracts live in
`TomasAI.IFM.Domain.MarketData.Analytics.Shared/RegimeDiscovery`; Trade.Shared
already depends on Analytics.Shared, which preserves the dependency direction.

All public/shared contracts use append-only MessagePack keys and XML comments.
Parameter/result records are immutable. Score values are validated for finite
range and persisted after six-decimal midpoint-to-even rounding.

`StartRegimeDiscoveryPipelineCommand` appends the typed parameter set,
parameter payload hash, and target horizon. Existing keys are never reordered.
`RegimeDiscoveryPipelineCompletedEvent` continues to carry the opaque
`StrategyStageResultEnvelope`, whose payload is the MessagePack-serialized
typed RegimeDiscoveryResult.

## 6. Command actor and calculation topology

The implementation lives under
`TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RegimeDiscovery`.
Only the Regime Discovery Command actor owns durable private state.

```text
Strategy Workflow Realtime actor
  -> StartRegimeDiscoveryPipelineCommand
RegimeDiscovery Command actor
  -> _receiveMap
  -> StartRegimeDiscoveryPipeline.ExecuteAsync(...)
Async command extension
  -> capture immutable signal snapshot and configuration
  -> run Trend, Volatility, and Market Structure calculation models
  -> pass all three immutable results to Market Regime Fusion model
  -> state.Update(private RegimeDiscoveryCalculationCompletedEvent, command)
     OR state.Update(private RegimeDiscoveryCalculationFailedEvent, command)
  -> Task<ServiceResult<GuidResult>>
Command actor repository
  -> commit pending event and state revision to PostgreSQL event log
RegimeDiscovery EventProjector
  -> write final result/failure projection to ScyllaDB
  -> publish public RegimeDiscoveryPipelineCompletedEvent or FailedEvent
Strategy Workflow Realtime actor
  -> sends the appropriate continuation command to Strategy Workflow
```

There is no Regime Discovery pipeline Realtime actor, Event actor, private
component actor, or component mailbox. The EventProjector is part of the
Command actor's persistence boundary; it is not an Event actor and does not
create independently durable computation state.

### 6.1 Command actor layout and dispatch

The Command actor follows the system convention documented in
`Documents/system/Actor-Implementation-Conventions.md`:

```text
RegimeDiscovery/
  Command/
    Actor/
    State/
    EventProjector/
    Validation/
    Extensions/
  Model/
  Query/
```

Its derived actor remains mechanical and owns explicit `_parseMap`,
`_validationMap`, and `_receiveMap` dictionaries. `ReceiveAsync` looks up the
runtime command name in `_receiveMap`; it must not use a type switch. The Start
entry delegates to an asynchronous extension:

```csharp
static readonly Dictionary<string, Func<
    ICommand,
    ICommandActorContext<RegimeDiscoveryCommandActor>,
    RegimeDiscoveryCommandState,
    Task<ServiceResult<GuidResult>>>> _receiveMap;
```

The framework-facing `ValueTask<ServiceResult<GuidResult>>` override adapts
the mapped `Task`. The concrete Start extension is named for the business
operation, for example `StartRegimeDiscoveryPipeline.ExecuteAsync`, without a
redundant `Command` suffix.

The extension receives the typed command, typed context, and state. It
validates state-dependent rules, obtains readonly
dependencies through typed context properties/extensions, awaits the complete
calculation, creates exactly one private durable terminal event, invokes
`state.Update(event, command)`, and returns the command GUID in the service
result. Returning a failed service result without updating state is reserved
for a rejected command; a calculation failure that must be durable updates
state with `RegimeDiscoveryCalculationFailedEvent`.

### 6.2 Actor-owned calculation models

The `Model` folder contains sealed, deterministic computation types:

- `TrendRegimeCalculationModel`
- `VolatilityRegimeCalculationModel`
- `MarketStructureRegimeCalculationModel`
- `MarketRegimeFusionModel`
- `RegimeDiscoveryCalculationModel`, which coordinates the component models

Each model receives immutable input and returns an immutable typed result. A
model does not mutate actor state, send messages, resolve services, write
storage, or publish events. Fusion receives the three completed component
results directly from the coordinator.

The coordinator initially supports both deterministic sequential execution
and awaited `Task.Run`/`Task.WhenAll` execution on the normal .NET thread pool.
It must not use dedicated threads, `TaskCreationOptions.LongRunning`, or
fire-and-forget work. Production selects parallel execution only after the
RD-13 benchmark demonstrates identical normalized output and a material
end-to-end latency improvement without p95/p99, allocation, or thread-pool
regression. Otherwise V1 remains sequential.

### 6.3 Private and public event rules

Internal durable domain events are not public workflow contracts:

| Outcome | Private committed event | Projected public event |
| --- | --- | --- |
| Success | `RegimeDiscoveryCalculationCompletedEvent` | `RegimeDiscoveryPipelineCompletedEvent` |
| Failure | `RegimeDiscoveryCalculationFailedEvent` | `RegimeDiscoveryPipelineFailedEvent` |

- The private completed event contains the full typed result, deterministic
  result hash, snapshot/configuration identities, and evidence needed to
  reconstruct Command state.
- The private failed event contains `StrategyPipelineFailure`, structured
  Regime Discovery reason codes, and input identities; no partial result is
  treated as complete.
- Duplicate starts and matching terminal outcomes are idempotent. A conflicting
  result hash for the same execution/result identity is a consistency failure.
- V1 removes the public `RegimeDiscoveryPipelineProcessingEvent` from the live
  flow. If compatibility requires retaining its contract temporarily, no actor
  routes or publishes it.
- Only the EventProjector publishes the public terminal event, and only after
  the terminal ScyllaDB projection succeeds.

## 7. Persistence and projections

The single Regime Discovery Command actor uses the existing PostgreSQL
event-source repository for authoritative private state and a conventional
EventProjector. There are no Event actors, Realtime actors, private component
actors, or durable message-replay consumers in Regime Discovery V1.

TradeDb ScyllaDB projections are rebuildable and add query tables for:

- pipeline terminal state/history;
- Trend, Volatility, and Market Structure component results;
- Fusion/result by workflow execution;
- evidence/reasons;
- snapshot data-quality summary; and
- terminal operational status and failure details.

Projection schemas must be query-shaped, versioned, and avoid
`ALLOW FILTERING`. Projector replay rebuilds them deterministically from the
PostgreSQL event log. Projection precedes public terminal publication so a
Strategy Workflow continuation never observes a missing Regime Discovery read
model.

## 8. Implementation gates

| Gate | Outcome |
| --- | --- |
| RD-0 | Approve revised design and this implementation specification; baseline build/tests |
| RD-1 | Add immutable parameter/result/enums/reason contracts with validation, MessagePack, and XML comments |
| RD-2 | Add PostgreSQL ConfigurationDb context/schema, all six parameter-set tables, factory/DI/settings, and storage integration tests |
| RD-3 | Add Reference Configuration identities, commands, queries, actors, and Regime Discovery typed lifecycle tests |
| RD-4 | Extend Strategy Workflow resolution/history and append StartRegimeDiscovery fields; preserve replay compatibility |
| RD-5 | Add common latest-signal cache/snapshot provider and deterministic atomic-capture tests |
| RD-6 | Add missing provenance to MACD/ADX/ATR contracts and populate/cache existing RSI/TDI/MACD/ADX/ATR/ITI signals |
| RD-7 | Implement missing EMA and ATR-baseline upstream signals for all configured observation timeframes |
| RD-8 | Implement missing Bollinger/range/high-low market-structure signals and caches |
| RD-9 | Implement current VIX/term-structure inputs; keep realized volatility optional |
| RD-10 | Implement the single Regime Discovery Command actor, state, repository, EventProjector, validation, `_receiveMap`, asynchronous Start extension, private terminal events, and idempotency |
| RD-11 | Implement sealed Trend, Volatility, and Market Structure calculation models with exact deterministic tests |
| RD-12 | Implement the sealed Fusion model and coordinator, including reason ordering, quality/restrictions, and envelope serialization |
| RD-13 | Benchmark sequential versus awaited thread-pool-parallel component calculation for Daily, Weekly, and Monthly workloads; verify identical output and select the production execution mode |
| RD-14 | Integrate immutable snapshot acquisition, calculation coordinator, Command state update, PostgreSQL commit, ScyllaDB projection, then public Completed/Failed publication |
| RD-15 | Add TradeDb projections, query actor/API contracts, replay/rebuild/storage tests |
| RD-16 | Run boundary integration with real Strategy Workflow for Daily, Weekly, and Monthly executions, success/failure/idempotency/restart paths |
| RD-17 | Run full Trade, Reference, MarketData Analytics, Application Storage, actor BDD/unit/integration suites and full solution build |
| RD-18 | Enable live Regime Discovery only after cache warm-up health and all required signals pass qualification |

Optional timeout/manual-cancel gates remain deferred and are not blockers for
RD-18 unless separately approved.

## 9. Test requirements

- Golden-vector unit tests cover every threshold boundary, piecewise segment,
  weight override, rounding rule, confidence band, classification precedence,
  restriction, and reason-code order.
- Cache tests cover missing, stale, future, not-warm, invalid, incompatible,
  optional omission, concurrent mutation, bounded capture retry, and startup
  warming.
- Command-actor BDD tests cover `_receiveMap` dispatch, async extension success
  and failure, durable private terminal replay, duplicate start, duplicate
  terminal, conflicting terminal hash, and rejected commands that do not
  change state.
- Model unit tests prove deterministic Trend, Volatility, Market Structure,
  and Fusion output without actor infrastructure.
- Benchmark/correctness tests compare sequential and thread-pool-parallel
  component execution for typical and maximum Daily, Weekly, and Monthly
  snapshots, including one and three concurrent workflows. Both modes must
  produce byte-for-byte equivalent normalized output.
- Configuration integration tests use real PostgreSQL and verify append-only
  versions, hash validation, deterministic effective selection, retirement,
  and all six table mappings.
- Projection integration tests use real ScyllaDB and verify deterministic
  rebuild with no filtering queries.
- Workflow integration tests run one Daily, one Weekly, and one Monthly
  workflow. Each asserts that only its own target-horizon result is returned,
  while its supporting observation evidence is preserved.
- A complete integration cycle verifies exactly one public Completed or Failed
  event after its ScyllaDB projection, opaque envelope round-trip, workflow
  continuation, and no private calculation contract leaking to Strategy
  Workflow routes.

## 10. Initial implementation blockers

The actor skeleton can be built before all market signals exist, but live
success cannot be enabled until RD-7 through RD-9 provide every configured
required signal. Tests may inject immutable snapshots; production code may not
fabricate required EMA, Bollinger, term-structure, freshness, or provenance
values.

No automatic retry, Regime Discovery Realtime actor, Event actor, private
component actor, durable message replay consumer, raw tick/bar recalculation
inside Regime Discovery, cross-pipeline addressing, or pipeline-specific
TraceId field is introduced by this plan.
