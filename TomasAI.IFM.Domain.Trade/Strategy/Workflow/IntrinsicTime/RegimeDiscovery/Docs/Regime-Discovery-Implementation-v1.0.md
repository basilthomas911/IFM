# Regime Discovery Implementation Specification

Implementation Specification v1.0

| Item | Value |
| --- | --- |
| Status | Proposed repository-specific implementation plan; no production implementation started |
| Date | 2026-08-25 |
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
6. Trend, Volatility, Market Structure, and Fusion are private Regime
   Discovery command/realtime processing units. Only the Regime Discovery
   boundary publishes the public Processing, Completed, or Failed events
   consumed by Strategy Workflow.
7. The fourth private actor is `MarketRegimeFusion`; the repeated
   `MarketStructureRegime` name in the design discussion is treated as a
   duplicate rather than a fifth actor.

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

## 6. Private actor topology

The private actors live under
`TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RegimeDiscovery`.
Their commands/events are `internal` and are not placed in Trade.Shared.

```text
Strategy Workflow Realtime actor
  -> StartRegimeDiscoveryPipelineCommand
RegimeDiscoveryPipeline Command actor
  -> commits RegimeDiscoveryPipelineProcessingEvent
RegimeDiscoveryPipeline EventProjector
  -> publishes public Processing
  -> publishes private specialist-dispatch instruction
RegimeDiscoveryPipeline Realtime actor
  -> StartTrendRegimeCommand
  -> StartVolatilityRegimeCommand
  -> StartMarketStructureRegimeCommand

Each specialist Command actor
  -> commits private Processing event
Specialist EventProjector
  -> publishes Processing to that specialist Realtime actor
Specialist Realtime actor
  -> calculates from the frozen snapshot/configuration
  -> sends private Complete/Fail command to its own Command actor
Specialist Command actor
  -> commits private Completed/Failed event
Specialist EventProjector
  -> publishes private terminal realtime event to pipeline Realtime actor
Pipeline Realtime actor
  -> sends RecordSpecialistResult/Failure command to pipeline Command actor

When all three specialist results are durably recorded:
Pipeline EventProjector -> private fusion-dispatch instruction
Pipeline Realtime actor -> StartMarketRegimeFusionCommand
Fusion Command/EventProjector/Realtime pair -> same private lifecycle
Pipeline Command actor -> records fusion -> commits public terminal state
Pipeline EventProjector -> public Completed or Failed to Strategy Workflow
```

This arrangement guarantees that no calculation is dispatched from an
uncommitted decision, every private actor retains its own event-sourced state,
and only a committed pipeline-boundary terminal transition can reach Strategy
Workflow.

### 6.1 Private actor groups

Each group has `Command/Actor`, `Command/State`, `Command/EventProjector`,
`Command/Validation`, `Realtime/Actor`, and `Extensions` folders:

- `TrendRegime`
- `VolatilityRegime`
- `MarketStructureRegime`
- `MarketRegimeFusion`

The boundary uses the same folders directly under `RegimeDiscovery`.
Contexts follow the closed-generic `ICommandActorContext<TActor>` and
`IRealtimeActorContext<TActor>` conventions already used by Strategy Workflow.
No base actor class changes are required.

### 6.2 Private message rules

The initial exact private contract names are:

| Processing unit | Commands | Realtime events |
| --- | --- | --- |
| Trend | `StartTrendRegimeCommand`, `CompleteTrendRegimeCommand`, `FailTrendRegimeCommand` | `TrendRegimeProcessingEvent`, `TrendRegimeCompletedEvent`, `TrendRegimeFailedEvent` |
| Volatility | `StartVolatilityRegimeCommand`, `CompleteVolatilityRegimeCommand`, `FailVolatilityRegimeCommand` | `VolatilityRegimeProcessingEvent`, `VolatilityRegimeCompletedEvent`, `VolatilityRegimeFailedEvent` |
| Market Structure | `StartMarketStructureRegimeCommand`, `CompleteMarketStructureRegimeCommand`, `FailMarketStructureRegimeCommand` | `MarketStructureRegimeProcessingEvent`, `MarketStructureRegimeCompletedEvent`, `MarketStructureRegimeFailedEvent` |
| Fusion | `StartMarketRegimeFusionCommand`, `CompleteMarketRegimeFusionCommand`, `FailMarketRegimeFusionCommand` | `MarketRegimeFusionProcessingEvent`, `MarketRegimeFusionCompletedEvent`, `MarketRegimeFusionFailedEvent` |

The boundary adds private
`RecordTrendRegimeResultCommand`, `RecordVolatilityRegimeResultCommand`,
`RecordMarketStructureRegimeResultCommand`,
`RecordMarketRegimeFusionResultCommand`, and
`RecordRegimeDiscoveryInternalFailureCommand`. Its committed private dispatch
instructions are `RegimeDiscoverySpecialistsDispatchReadyEvent` and
`MarketRegimeFusionDispatchReadyEvent`. Those two instructions are projected
to the boundary Realtime actor, which is the only component allowed to send
the corresponding private Start commands.

- Start commands contain workflow identity/revision, pipeline execution ID,
  target horizon, immutable relevant configuration, frozen signal snapshot,
  correlation/causation IDs, and expected input identities.
- Complete commands contain the full typed specialist result and deterministic
  result hash.
- Failed commands contain `StrategyPipelineFailure` plus structured Regime
  Discovery reason codes; no partial result is treated as complete.
- Processing/Completed/Failed private realtime events are one-way and have no
  reply contract.
- Duplicate starts and duplicate matching terminal results are no-ops.
- A conflicting result hash for the same execution/result identity is a
  consistency failure reported to the boundary.
- Specialists never address one another and never address Strategy Workflow.
- Fusion receives all three complete typed specialist results from the
  boundary, not by querying specialist actors.

## 7. Persistence and projections

All five Regime Discovery Command actors use the existing PostgreSQL
event-source repository for authoritative private state. The boundary and each
private actor have conventional EventProjectors. There are no Event actors and
no durable replay consumers.

TradeDb Scylla projections are rebuildable and should add query tables for:

- pipeline state/history;
- specialist latest/history results;
- fusion/result by workflow execution;
- evidence/reasons;
- snapshot data-quality summary; and
- processing/terminal operational status.

Projection schemas must be query-shaped, versioned, and avoid
`ALLOW FILTERING`. Projector replay rebuilds them from PostgreSQL event logs.

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
| RD-10 | Implement Regime Discovery boundary Command/Realtime actors, state, projector, validation, and idempotency |
| RD-11 | Implement private Trend actor pair and exact deterministic tests |
| RD-12 | Implement private Volatility actor pair and exact deterministic tests |
| RD-13 | Implement private Market Structure actor pair and exact deterministic tests |
| RD-14 | Implement private Fusion actor pair, reason ordering, quality/restrictions, and envelope serialization |
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
- Actor BDD tests cover private Processing -> Completed/Failed lifecycles,
  durable private replay, duplicate start, duplicate terminal, conflicting
  terminal hash, and specialist isolation.
- Configuration integration tests use real PostgreSQL and verify append-only
  versions, hash validation, deterministic effective selection, retirement,
  and all six table mappings.
- Projection integration tests use real ScyllaDB and verify deterministic
  rebuild with no filtering queries.
- Workflow integration tests run one Daily, one Weekly, and one Monthly
  workflow. Each asserts that only its own target-horizon result is returned,
  while its supporting observation evidence is preserved.
- A complete integration cycle verifies public Processing then exactly one
  public Completed or Failed event, opaque envelope round-trip, workflow
  continuation, and no private specialist contract leaking to Strategy
  Workflow routes.

## 10. Initial implementation blockers

The actor skeleton can be built before all market signals exist, but live
success cannot be enabled until RD-7 through RD-9 provide every configured
required signal. Tests may inject immutable snapshots; production code may not
fabricate required EMA, Bollinger, term-structure, freshness, or provenance
values.

No automatic retry, Event actor, durable message replay consumer, raw
tick/bar recalculation inside Regime Discovery, cross-pipeline addressing, or
pipeline-specific TraceId field is introduced by this plan.
