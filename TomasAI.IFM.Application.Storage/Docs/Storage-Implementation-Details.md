# Application Storage — Implementation Details

> **Implemented catalog replacement (2026-09-06):** ConfigurationDb now owns active strategy catalog authoring. Reference Data Manager edits all seven catalog sections, including balanced/directional variants; Portfolio mandates, assignments and policy limits use exact deployment GUID/version references. Existing family records are imported as Drafts without automatic permissions. The old family UI/write path is Legacy; historical contracts remain readable. [Integration details](ConfigurationDb-Strategy-Catalog-Implementation.md) and [UI guide](../../TomasAI.IFM.UI.Net/Docs/Strategy-Catalog-Reference-UI.md) supersede the older family-authoring descriptions below. TradeSelection execution remains on hold.

## Purpose

Grouped predefined Fund choices are stored in PostgreSQL `ConfigurationDb.lookup_definition`; see [ConfigurationDb lookup definitions](ConfigurationDb-Lookup-Definitions.md) for its schema, seed values, query transport and Databento-backed underlying selections.

`TomasAI.IFM.Application.Storage` is the application-layer persistence catalog for IFM. It adapts domain read models and commands to the provider-neutral repository primitives in `TomasAI.IFM.Framework.Storage` and exposes:

- database contexts grouped by business domain;
- read/write context interfaces;
- event-stream persistence and replay;
- command and projector checkpoint persistence;
- SQL/CQL command and parameter catalogs;
- ordered database-schema creation and removal;
- external data-reader contexts for economic calendars and yield curves; and
- a DI-backed context factory, resolver, and limited context pool.

The project does not select a database vendor directly. Each context receives a named `IDbConnectionSetting`; its `ProviderName` and credential-free connection string determine which framework provider creates connections, commands, parameters, readers, and bulk-copy operations. Result materialization uses explicit ordinal mapping; application contexts do not register reflection-based result maps. The PostgreSQL catalog uses operation-owned provider connections and typed readers for transactions, reconstructing its definition DTO from normalized rows.

## ConfigurationDb and strategy catalog direction (2026-09-06)

`ConfigurationDbContext` owns PostgreSQL schema `reference_configuration`. Its existing pipeline parameter tables store versioned JSONB configuration with identity, hash, lifecycle and audit metadata. The current schema includes workflow, RegimeDiscovery, MarketCondition, TradeSelection, OrderComposition and RiskManagement parameter tables plus MarketCondition assessment parameters.

The reusable trading-strategy catalog is now implemented in this context. Relational identities and versioned relationships separate strategy families, trading hypotheses, structures, variants, leg/expiry definitions, parameter schemas and product/timeframe deployments. Specialized settings use validated JSONB. A small asset universe can support many strategies without extending the existing strategy enum for every variation.

Fund assignments, permissions and financial limits remain Portfolio-owned. Instrument definitions and product/contract discovery remain with Reference/MarketData/Securities. Existing ReferenceDb `TradeStrategyFamily` rows retain their current identities until an explicit compatibility migration; they are not the new reusable strategy definitions.

[ConfigurationDb Strategy Catalog Design v1.0](ConfigurationDb-Strategy-Catalog-Design-v1.0.md) defines common/future variants, capability validation, version freezing and migration boundaries. [Catalog implementation](ConfigurationDb-Strategy-Catalog-Implementation.md) documents the normalized physical tables, immutable-draft context APIs, trusted capability/reference adapter contracts and PostgreSQL verification. **TradeSelection implementation remains on hold** pending realignment of its specification and plan. Normal ConfigurationDb schema initialization now creates the catalog additively; no production strategies or Fund permissions are seeded.

## DownloadLog addition (2026-09-05)

`MarketDataDbContext.DownloadLog.cs` and the `market_data_download_log` schema record immutable terminal outcomes for EconomicCalendar and TreasuryCurve imports. Queries use full dataset/provider/scope/date partitions, typed CQL bindings, bounded timestamp/UUID keyset paging, and millisecond-normalized payload hashes. Failed attempts retain nullable counts when persistence is uncertain. Source data is not copied into the log.

Private events implementing `IRequireDurableProjection` commit their initial projector-state marker with the event in PostgreSQL. Scalar and single-row provider reads now honor the active transaction, including `INSERT ... RETURNING`; rollback tests cover failure to create that marker. The durable projector performs the subsequent idempotent Scylla upsert asynchronously. See [DownloadLog implementation and qualification](../../TomasAI.IFM.Domain.MarketData/Docs/Domain-Actor-Implementation-Details.md).

## Complete project folder map

The tree below records every directory currently present beneath the project root, from root to each leaf. `bin` and `obj` are generated trees whose framework, locale, and runtime branches can change after restore, build, or publish.

```text
TomasAI.IFM.Application.Storage/                    Project root
├── ConfigurationDb/                               PostgreSQL strategy configuration
│   └── Schema/                                     Configuration SQL schema
├── Docs/                                           Maintained project documentation
├── EventSourceDb/                                  Active event-source persistence
│   └── Schema/                                     Event-source SQL schema
├── FundDb/                                         Fund persistence
│   └── Schema/                                     Fund CQL schema
├── MarketDataDb/                                   Market data and analytics persistence
│   └── Schema/                                     Market-data CQL schema
├── OptionPricerDb/                                 Option-pricer persistence
│   └── Schema/                                     Option-pricer CQL schema
├── ReferenceDb/                                    Reference and scheduling persistence
│   └── Schema/                                     Reference CQL schema
├── Schema/                                         Shared schema abstractions
├── SecuritiesDb/                                   Futures security-master persistence
│   └── Schema/                                     Securities CQL schema
├── SequenceIdDb/                                   PostgreSQL-style sequence access
│   └── Schema/                                     Sequence functions and sequences
├── TradeDb/                                        Trade and trade-plan persistence
│   └── Schema/                                     Trade CQL schema
├── TradePlanDb/                                    PostgreSQL plan snapshots and exit-stage projections
├── YieldCurveRatesDb/                              External yield-curve reader
├── bin/                                            Generated build output
│   ├── Debug/
│   │   ├── net10.0/
│   │   │   ├── de/                                 German resource leaf
│   │   │   ├── es/                                 Spanish resource leaf
│   │   │   ├── fr/                                 French resource leaf
│   │   │   ├── it/                                 Italian resource leaf
│   │   │   ├── ja/                                 Japanese resource leaf
│   │   │   ├── ko/                                 Korean resource leaf
│   │   │   ├── pt-BR/                              Brazilian Portuguese resource leaf
│   │   │   ├── ru/                                 Russian resource leaf
│   │   │   ├── zh-Hans/                            Simplified Chinese resource leaf
│   │   │   ├── zh-Hant/                            Traditional Chinese resource leaf
│   │   │   └── runtimes/
│   │   │       ├── unix/
│   │   │       │   └── lib/
│   │   │       │       └── net8.0/                 Unix managed-runtime leaf
│   │   │       ├── win/
│   │   │       │   └── lib/
│   │   │       │       ├── net8.0/                 Windows managed-runtime leaf
│   │   │       │       └── netcoreapp2.0/          Legacy Windows compatibility leaf
│   │   │       ├── win-arm/
│   │   │       │   └── native/                     Windows ARM native leaf
│   │   │       ├── win-arm64/
│   │   │       │   └── native/                     Windows ARM64 native leaf
│   │   │       ├── win-x64/
│   │   │       │   └── native/                     Windows x64 native leaf
│   │   │       └── win-x86/
│   │   │           └── native/                     Windows x86 native leaf
│   │   └── net8.0/                                 Legacy Debug .NET 8 output leaf
│   └── Release/
│       └── net10.0/
│           └── runtimes/
│               └── win-x64/
│                   └── native/                     Release Windows x64 native leaf
└── obj/                                            Generated compiler/MSBuild state
    ├── Debug/
    │   ├── net10.0/
    │   │   ├── ref/                                Debug .NET 10 reference leaf
    │   │   └── refint/                             Debug .NET 10 internal-reference leaf
    │   └── net8.0/
    │       ├── ref/                                Legacy Debug .NET 8 reference leaf
    │       └── refint/                             Legacy Debug .NET 8 internal-reference leaf
    └── Release/
        └── net10.0/
            ├── ref/                                Release .NET 10 reference leaf
            └── refint/                             Release .NET 10 internal-reference leaf
```

## Folder responsibilities

### Maintained source folders

| Folder | Status | Responsibility |
| --- | --- | --- |
| Project root | Active | Context discovery/factory/pool contracts and implementations, event-source actor contract, command status, and project definition. |
| `Docs/` | Active documentation leaf | Contains this implementation and complete folder reference. |
| `EventSourceDb/` | Active | Event stream/name/log persistence, bounded-context and actor variants, serialization/replay, command logs, and projector state/results. |
| `EventSourceDb/Schema/` | Active source leaf | Defines event sequences/tables and ordered create/drop operations. |
| `FundDb/` | Active | Fund, order, trade, transaction, balance, P&L, drawdown, bulk insert, update/delete, and backup operations. |
| `FundDb/Schema/` | Active source leaf | Defines fund, fund-order, fund-order-trade, and fund-transaction tables. |
| `MarketDataDb/` | Active | Broad futures tick/bar/EOD/option, volatility, analytics signal/model, yield curve, holiday, normal-curve, quote, ID, and live-feed persistence. |
| `MarketDataDb/Schema/` | Active source leaf | Defines canonical market-data tables, query-shaped tick/EOD/VX projections, cutover state, and the RSI signal-type index in creation order. |
| `OptionPricerDb/` | Active | Option-pricer devices, spread distributions, distribution jobs, status transitions, and domain-specific exception/parameter definitions. |
| `OptionPricerDb/Schema/` | Active source leaf | Defines device, distribution-job, and spread-distribution tables. |
| `ReferenceDb/` | Active | Lookup types, seed capabilities, scheduled jobs, instrument definitions, product/family catalogs, and MDI forward-loss-ratio persistence. |
| `ReferenceDb/Schema/` | Active source leaf | Defines the PostgreSQL lookup, forward-loss, scheduled-job/day, instrument-definition and product/family tables and indexes. |
| `Schema/` | Active source leaf | Shared `IDbSchemaContext`, `SchemaDbContext<T>`, and immutable schema object definition. |
| `SecuritiesDb/` | Active | Futures and futures-option contract queries, insert/update/delete operations, and currently-traded contract selection. |
| `SecuritiesDb/Schema/` | Active source leaf | Defines futures and futures-option contract tables. |
| `SequenceIdDb/` | Active | Obtains the next named sequence ID through checked-in SQL. |
| `SequenceIdDb/Schema/` | Active source leaf | Defines current/next sequence functions and one database sequence per `SequenceName`. |
| `TradeDb/` | Active | Option trades/legs/spreads, positions/states, fills, limits, orders, trade plans, forward-loss values, live feed, and placement signals. |
| `TradeDb/Schema/` | Active source leaf | The single PostgreSQL DDL catalog and schema executor own all trade tables, indexes and additive changes. |
| `TradePlanDb/` | PostgreSQL context | Active plan snapshot, ordered history and exit-stage persistence through typed contracts. |
| `YieldCurveRatesDb/` | Active source leaf | Reads externally supplied yield-curve JSON data and converts it to market-data read models. |

### Generated folders

| Folder family | Responsibility |
| --- | --- |
| `bin/Debug/net10.0/` and all locale/runtime descendants | Current Debug assemblies, symbols, localized provider resources, managed compatibility libraries, and native provider assets. |
| `bin/Debug/net8.0/` | Output retained from an earlier .NET 8 build. |
| `bin/Release/net10.0/` and runtime descendants | Current Release assemblies and Windows x64 native provider assets. |
| `obj/Debug/net10.0/` and `ref`/`refint` leaves | Current Debug restore/compiler caches and reference assemblies. |
| `obj/Debug/net8.0/` and `ref`/`refint` leaves | Intermediates retained from an earlier .NET 8 build. |
| `obj/Release/net10.0/` and `ref`/`refint` leaves | Current Release restore/compiler caches and reference assemblies. |

Do not manually edit or commit `bin` or `obj` contents.

## Source organization conventions

Database folders follow this pattern:

| File kind | Purpose |
| --- | --- |
| `I<Domain>DbContext.cs` | Combined repository contract, often extending the framework's generic repository plus read/write interfaces. |
| `I<Domain>DbReadContext.cs` | Query contract returning domain read models or scalars. |
| `I<Domain>DbWriteContext.cs` | Insert, update, delete, bulk, or backup contract. |
| `<Domain>DbContext.cs` | Model mappings and operation implementation using `ObjectDataRepository<T>`. |
| `<Domain>DbContextExtensions.cs` | The single home for every DbContext helper that is not a result-set `MapTo...` mapper or a public read/write-contract implementation. |
| `<Domain>DbCql.cs` or `<Domain>DbSql.cs` | Checked-in provider command text. |
| `<Domain>DbParameters.cs` | Parameter records/classes bound to command text. |
| `<Domain>DbException.cs` | Domain-specific storage exception where present. |
| `Schema/<Domain>SchemaDb.cs` | Ordered schema-object catalog and connection selection. |
| `Schema/<Domain>SchemaCql.cs` or `...Sql.cs` | Checked-in create statements. |

Every class named `*DbContext` is governed by the mandatory implementation conventions below. Any DbContext that does not conform is an alignment finding. Focused storage components that are not DbContext implementations, such as Log and Sequence ID services, may expose narrower contracts.

## Mandatory DbContext implementation conventions

These rules define the minimum implementation pattern for every application DbContext. They are architectural requirements, not optional style guidance.

### Context structure

- A DbContext must be implemented as one non-partial `NameOfDbContext` class. Its implementation must not be split across partial class files.
- The class must inherit `ObjectDataRepository<NameOfDbContext>`, where the generic type argument is the concrete DbContext class itself.
- The class must directly implement only its combined `INameOfDbContext` contract.
- `INameOfDbContext` must inherit both `INameOfDbReadContext` and `INameOfDbWriteContext`.
- The context must return itself from its `Database`, `DbReader`, and `DbWriter` properties.
- Every public constructor, property, and method must have XML documentation. Method documentation must describe every parameter and the returned value or asynchronous operation.

### Persistence and orchestration boundary

A DbContext must be as close to an ORM-style persistence adapter as possible. It exposes typed queries and mutations over its owned tables, maps records and delegates provider mechanics. It must contain minimal application or domain functionality. This rule applies to the entire database implementation, including its extension class and supporting folders; moving orchestration out of the context class into a Storage helper does not satisfy the boundary.

- **Domain ownership:** business calculations, business invariants, strategy decisions and rules determining which events or read models should be produced belong in the owning domain. Incoming request validation belongs to the caller before persistence is invoked.
- **Domain coordination ownership:** command execution coordination, duplicate-request coordination, workflow and pipeline sequencing, actor messaging, notifications, schedules, recovery/replay coordination, import orchestration and application-driven cache policy belong in the owning domain repositories, command actors, query handlers and workflow services.
- **Storage ownership:** typed queries and mutations, parameter binding, record mapping, provider-specific serialization, SQL/CQL execution, database constraints, optimistic-concurrency predicates, row locks, atomic compare-and-set updates and connection/transaction resources belong in Storage. Technical batching and binary COPY are permitted when they preserve the caller's requested writes and do not introduce business decisions.
- **Transactions:** the domain selects the business operation and the event/projection writes that must commit together. Storage supplies the enlisted connection and transaction/unit-of-work mechanism and executes those writes atomically. A DbContext must not discover downstream business work, invoke actors/providers or start another workflow while committing a transaction.
- **Idempotency and leases:** Storage may persist command identities, receipts, lease tokens, revisions and checkpoints and enforce their conditional database updates. The caller decides whether a command should execute, whether a lease should be acquired or renewed, and whether a failure should retry, replay, skip or escalate.
- **Audit and event sourcing:** deciding when a command is audited, reserving execution and coordinating event production belong to the domain command/repository layer. Storage persists the supplied audit/event facts and reports database outcomes. Audit orchestration interfaces must not be added to a DbContext simply because they eventually write rows.
- **Lifetime:** DbContext lifetime behavior is limited to releasing its database/provider resources. Starting or stopping application workers, recovery loops or background coordination is not a DbContext responsibility.
- **Dependencies:** DbContexts must not depend on domain actors, workflow runners, external market-data/broker clients, schedulers or application coordination services. The permitted dependency direction is application/domain consumers toward persistence contracts; the concrete Storage implementation does not call back into business orchestration.
- **Helper and folder ownership:** context extension methods contain persistence helpers only. Supporting Storage folders must represent substantial provider/persistence responsibilities. Domain orchestration must be moved to its owning layer, not hidden in a database folder. Benchmark fixtures and comparison stores belong in benchmark/test projects and must not be shipped as production Storage services.

Apply the following split when reviewing EventSourceDb:

| Responsibility | Storage portion | Domain coordination portion |
|---|---|---|
| Command audit | Persist supplied audit payloads, identity and status; provider encoding | Decide when to audit and coordinate command acceptance/execution |
| Command deduplication | Unique keys and conditional reservation/receipt writes | Coordinate duplicate callers and execution outcomes |
| Historical data loading | Persist import manifests and checkpoints in their owning database | Download, batch, resume, schedule and supervise imports |
| Event-log persistence | Append supplied events, assign database versions, encode bytes, COPY and transaction handling | Choose events, coordinate command processing, retries and replay |
| Projection rebuilding | Read source events and execute supplied projection mutations | Select replay/baseline scope, sequence rebuild work and verify application readiness |
| Command-log benchmarking | No production benchmark service | Benchmark/test project owns fixtures, workloads and comparisons |

Existing implementations that mix these responsibilities require an explicit separation refactor. Their current location or interface shape is not an exception to this convention. A refactor must preserve transaction atomicity, concurrency and idempotency guarantees while moving orchestration to the owning layer.

### Physical table naming

- Physical SQL and CQL table names must describe the table's current domain role and must not end in a schema-generation suffix such as `_v1`, `_v2`, or `_v3`.
- When a table shape is replaced, the obsolete table definition, runtime queries, fallback APIs, and legacy-only tests must be removed. The authoritative replacement keeps the unsuffixed domain name.
- Because legacy database layouts are not supported, a breaking table-shape replacement requires recreating the affected database or keyspace. Runtime dual-table routing and fallback reads are prohibited.
- Version numbers that are part of persisted business data, payload contracts, schema-object step identifiers, index names, or method names are outside this physical-table rule unless they identify a physical table.

The minimum class shape is:

```csharp
/// <summary>
/// Provides persistence operations for example domain data.
/// </summary>
/// <param name="connectionSettings">The named database connection settings.</param>
/// <param name="dbFactory">The factory used to resolve database contexts.</param>
/// <param name="logger">The database-provider logger.</param>
public class ExampleDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<ExampleDbContext>(
        connectionSettings[ExampleDbConnection], logger),
      IExampleDbContext
{
    private readonly IDbContextFactory _dbFactory = dbFactory;

    public const string ExampleDbConnection = "ExampleDbConnection";

    /// <summary>
    /// Gets the concrete database context.
    /// </summary>
    public override ExampleDbContext Database => this;

    /// <summary>
    /// Gets the read capability for this database context.
    /// </summary>
    public IExampleDbReadContext DbReader => this;

    /// <summary>
    /// Gets the write capability for this database context.
    /// </summary>
    public IExampleDbWriteContext DbWriter => this;
}
```

The corresponding combined interface has this minimum shape:

```csharp
public interface IExampleDbContext :
    IExampleDbReadContext,
    IExampleDbWriteContext
{
}
```

### Read and write contracts

- `INameOfDbReadContext` owns all asynchronous queries that return data or scalar results.
- Read methods must normally be named `GetXXXAsync`.
- `INameOfDbWriteContext` owns all asynchronous commands that change persisted state.
- Write methods must use operation-oriented names such as `InsertXXXAsync`, `UpdateXXXAsync`, and `DeleteXXXAsync`.
- Every read/write interface method must be implemented as a public method on the single concrete `NameOfDbContext` class.
- Every public method declared by a DbContext must implement a member of either `INameOfDbReadContext` or `INameOfDbWriteContext`. A DbContext must not expose public convenience or helper methods outside those contracts.
- The only static methods permitted on a DbContext are result-set mappers named `MapTo...`.
- Every other helper method, whether private, internal, or static, must be defined in one `NameOfDbContextExtensions` class. A DbContext folder must not distribute helpers across multiple extension classes.
- A `NameOfDbContextExtensions` class must use C# 14 extension blocks. Every helper must be declared inside an `extension(receiver)` block; legacy `this`-parameter extension methods and ordinary static utility methods are prohibited. Methods sharing the same receiver type and semantic receiver role must be grouped into one block. Separate blocks for the same CLR type are permitted only when the receiver has a distinct domain role and therefore a different meaningful receiver name. Extension properties and other supported extension-member forms must likewise be declared inside the receiver's extension block.
- Every method in a `NameOfDbContextExtensions` class must have XML documentation describing its persistence operation.
- DbContext methods must not validate their method arguments. Validation belongs to the caller before the storage method is invoked.

### Query result ownership

- A query result that is not a primitive, enum, string, framework collection of those values, or an existing domain type must be a `ReadModel` defined in the owning `Domain.<Domain>.Shared` project.
- Storage projects must not define public query-result records, classes, or tuples.
- Read-context methods return the shared-domain read model. Storage-only parameter, mutation-state, and mapper-support types remain internal implementation details and are not query results.
- Provider-neutral buffers and deterministic byte encoders for domain data belong in the owning Domain.Shared project. Storage retains only provider-specific prepared-metadata validation, binding adapters, and command execution.

Each invocation in a fluent DbContext persistence chain must occupy one physical line. Keep `Use(...)`, `SetParameters(...)`, `Execute...(...)`, `QueueCommand()`, and `ConfigureAwait(...)` on separate lines, and extract complex lambdas or values before the chain when an invocation would otherwise span lines.

A simple command follows this pattern:

```csharp
/// <summary>
/// Deletes the MDI forward loss ratio for the supplied trend direction and trade type.
/// </summary>
/// <param name="trendDirection">The intrinsic-time trend direction.</param>
/// <param name="tradeType">The trade type.</param>
/// <returns>A task representing the asynchronous delete operation.</returns>
public async Task DeleteMDIForwardLossRatioAsync(
    IntrinsicTimeTrendType trendDirection,
    TradeType tradeType)
    => await _dbFactory.ReferenceDb.Use($"{nameof(ReferenceDbSql)}.{nameof(ReferenceDbSql.DeleteMDIForwardLossRatio)}", ReferenceDbSql.DeleteMDIForwardLossRatio)
        .SetParameters(new DeleteMDIForwardLossRatio(trendDirection.ToStringFast(), tradeType.ToStringFast()))
        .ExecuteCommandAsync();
```

### Result mapping

- Every result-set mapper must be a static `MapToXXX` method.
- A mapper must construct the target type directly from `IObjectDataRecord` fields using stable zero-based ordinals.
- Selected columns, mapper ordinals, field types, and constructor arguments must remain aligned.
- Result mapping must not use reflection or property-name matching.

The minimum mapper pattern is:

```csharp
static LookupTypeReadModel MapToLookupType(IObjectDataRecord e)
    => new(
        lookupTypeName: e.GetString(0),
        shortCode: e.GetString(1),
        orderId: e.GetInt(2),
        description: e.GetString(3),
        createdOn: e.GetDateTime(4),
        createdBy: e.GetString(5));
```

### Database schema implementation

- Each database owns a `Schema/` folder containing one `<Domain>SchemaDb.cs` executor and one `<Domain>SchemaSql.cs` PostgreSQL DDL catalog.
- `<Domain>SchemaDb` inherits `SchemaDbContext<<Domain>SchemaDb>` and selects the database context's named connection setting. Its `Definitions` cover every owned schema, table, index, constraint, sequence, function, trigger and required initialization step, in dependency order.
- All create/drop SQL and schema metadata belong in the single schema SQL catalog. Related definition groups may be nested in that catalog; additional feature-specific schema SQL files or schema folders such as `ParameterSets/` are prohibited.
- The executor uses the inherited `CreateAllAsync`, targeted `CreateAsync`, and table-validation operations. Schema creation must be additive and repeatable; ordinary startup must not drop application data.
- PostgreSQL databases must not retain `.cql` files, `*SchemaCql.cs` classes or CQL compatibility catalogs. ScyllaDB databases retain their provider-specific CQL schema catalog.
- Runtime query/command SQL remains in `<Domain>DbSql.cs`; schema DDL remains in `Schema/<Domain>SchemaSql.cs`.
- Focused tests verify the single-catalog layout and actual PostgreSQL creation, including indexes and lifecycle guards. `ConfigurationDb/Schema` implements this pattern; the shared schema executor provides the creation mechanism used by the MarketData schema executor as well.

### Statement and parameter catalogs

- Every ScyllaDB query or command string must be declared in `NameOfDbCql.cs`.
- Every PostgreSQL query or command string must be declared in `NameOfDbSql.cs`.
- SQL and CQL must not be embedded directly in DbContext method bodies.
- `NameOfDbParameters.cs` must contain the query/command parameter-binding models used by `SetParameters`.
- Parameter models should be internal `readonly record struct` values implementing `IBindValue`.
- The bound value order must exactly match the statement marker or placeholder order.

ScyllaDB statement example:

```csharp
public const string DeleteReferenceProjectionStateV3 = """
    DELETE FROM reference_projection_state_v3
    WHERE projectionName = :projectionName;
    """;
```

Parameter model example:

```csharp
internal readonly record struct GetTradeStrategyFamilies(string Catalog) : IBindValue
{
    public object Bind() => new object?[] { Catalog };
}
```

## Project definition and dependencies

The project uses `Microsoft.NET.Sdk`, targets `net10.0`, and enables nullable reference types and implicit usings. It has no direct NuGet package references; functionality arrives through project references:

- Application Blackboard for event-source lookup/checkpoint caching;
- Framework Storage for generic repositories, mapping, execution, providers, transactions, readers, and bulk copy;
- Framework Sequence ID for sequence contracts and names;
- Shared for storage settings, event sourcing, primitives, and cross-domain contracts; and
- shared domain projects for Fund, Market Data, Analytics, Feed, Option Pricer, Predictive Model, Reference, System Admin, and Trade read models/contracts.

Internals are visible to `TomasAI.IFM.Application.Storage.IntegrationTests`.

The project removes the currently absent `SqlServer/**` path from Compile, EmbeddedResource, and None items. TradePlanDb is compiled into the active storage assembly and resolved through the typed database factory. The obsolete `EventDb` implementation and its excluded SQL Server integration test were removed; `EventSourceDb` is the active event-source implementation.

## Context resolution and factory

The resolution flow is:

```text
Consumer
  └─ IDbContextFactory property or Get<TRepo>()
       └─ IDbContextResolver.Resolve<TRepo>()
            ├─ Construct requested IObjectRepository<TRepo> service type
            └─ Invoke host-supplied Func<Type, object>
                 └─ Return configured repository instance
```

`DbContextResolver` does not own a DI container; the host supplies the delegate that resolves a constructed `IObjectRepository<TRepo>` type.

`DbContextFactory` exposes:

- event-source, actor-event-source, log, and sequence repositories;
- typed Fund, Market Data, Option Pricer, Reference, Securities, Trade, and Yield Curve contexts;
- the remaining focused repository adapters;
- the configured schema contexts; and

Each factory property resolves on access rather than retaining a context instance. Actual lifetime therefore depends on the host's service registration.

## Repository execution model

Every active context inherits `ObjectDataRepository<TRepo>` from Framework Storage. Construction:

1. selects a named connection from `IDbConnectionSettings`;
2. stores its provider name and credential-free connection string; and
3. creates the provider adapter.

Operations then use the fluent framework surface:

```text
repository.Use(command text / stored procedure / reader / bulk table)
          .SetParameters(...)
          .ExecuteStreamAsync(ordinal mapper, cancellation token) /
          .ExecuteQueryAsync(ordinal mapper) /
           ExecuteQueryImmutableAsync(ordinal mapper) /
           ExecuteSingleAsync(ordinal mapper) / ExecuteScalarAsync(ordinal mapper) /
           ExecuteCommandAsync / ExecuteMapReduceAsync(ordinal mapper, reducer)
```

Provider-specific objects are created below this application project according to the connection setting's `ProviderName`. PostgreSQL and ScyllaDB credentials are added by Framework Storage only when their physical connection/cluster is created.

## Named connection settings

Hosts must register settings for the contexts they resolve:

| Context | Connection-setting key |
| --- | --- |
| Actor event source | `EventSourceActorDbConnection` |
| Fund | `FundDbConnection` |
| Market data | `MarketDataDbConnection` |
| Option pricer | `OptionPricerDbConnection` |
| Reference | `ReferenceDbConnection` |
| Securities | `SecuritiesDbConnection` |
| Sequence ID | `SequenceIdDbConnection` |
| Trade | `TradeDbConnection` |
| FMP Treasury/calendar adapter | `FMP_API_KEY` environment variable (secret; not a database connection) |

Schema contexts reuse their corresponding runtime context setting. `EventSourceSchemaDb` uses `EventSourceActorDbConnection`, the same setting as `EventSourceActorDbContext`.

### Credential-free configuration

All PostgreSQL and ScyllaDB connection strings in solution source/configuration omit user IDs and passwords. Hosts provide a provider- and environment-specific JSON environment variable instead:

| Environment | PostgreSQL | ScyllaDB |
| --- | --- | --- |
| Development (default) | `POSTGRES_DEV_KEY` | `SCYLLADB_DEV_KEY` |
| Test | `POSTGRES_TEST_KEY` | `SCYLLADB_TEST_KEY` |
| Staging | `POSTGRES_STAGING_KEY` | `SCYLLADB_STAGING_KEY` |
| Production | `POSTGRES_PROD_KEY` | `SCYLLADB_PROD_KEY` |

Each value has the case-insensitive schema `{"userid":"...","password":"..."}`. `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` select the row; neither being set means Development, while conflicting or unsupported values fail before connection creation. A PostgreSQL or ScyllaDB base connection string containing an inline user ID or password is rejected. See [`docs/database-credentials.md`](../../docs/database-credentials.md) for aliases, examples, validation, and rotation guidance.

## Domain context catalog

| Context | Main behavior |
| --- | --- |
| `EventSourceActorDbContext` | Actor-oriented stream save/load/map-reduce, command log status, event projector state/result persistence, incomplete-projection recovery queries, and event identity caching. |
| `FundDbContext` | Full fund/order/trade/transaction CRUD and bulk operations plus balances, P&L, drawdown reporting, state/status changes, and database backup. |
| `MarketDataDbContext` | The largest context: market ticks/bars/EOD, option ticks/quotes, analytics indicators/signals/models, VX, yield curves, holidays, normal curves, IDs, and trade live feed. |
| `OptionPricerDbContext` | Device registration and spread-distribution job/data lifecycle. |
| `ReferenceDbContext` | Lookup/seed capabilities, scheduled jobs, instrument/product catalogs, and MDI forward-loss ratios. |
| `SecuritiesDbContext` | Futures and futures-option contract master data and currently traded contract queries. |
| `SequenceIdDbContext` | Executes `fn_get_next_sequence_id` for a named `SequenceName`. |
| `TradeDbContext` | Broad option trade, position, fill, limit, order, trade-plan, spread, loss-control, live-feed, and placement-signal persistence. |
The former `EconomicCalendarsDbContext` external-reader facade has been retired. FMP acquisition belongs to
`Framework.MarketData.FinancialModelingPrep`, orchestration belongs to `Application.MarketData`, and durable calendar
reads/writes use the country/month-partitioned `MarketDataDbContext.economic_calendar_v2` table.
| `YieldCurveRatesDbContext` | Reads yield-curve JSON through the configured data-reader provider and converts all returned rows. |

## Event-sourcing behavior

### Bounded-context path

`BaseEventSourceRepository` provides protected helpers for domain repositories:

- resolve a stream ID from a command stream name;
- create and replay a complete bounded context;
- replay only a last-N event range;
- replay from the most recent snapshot type;
- create an empty context;
- persist pending state events; and
- post resulting events to `IEventDenormalizer`.

Storage/conversion failures are logged and wrapped in `StorageException`; concurrency exceptions are allowed to propagate unchanged.

### Actor path

`EventSourceActorDbContext` implements the actor-facing contract and combines database state with Blackboard event-source caches. It supports:

- event stream and event-name ID lookup/creation;
- command log insert/status update;
- event-log insertion/deletion;
- full, last-N, and snapshot-based actor replay;
- map-reduce replay without always materializing an entire result collection;
- per-event/per-projector checkpoint and result persistence; and
- recovery queries for nonterminal projector events.

Event type identity uses assembly-qualified type names. The context maintains process-local concurrent event-name caching in addition to Blackboard integration.

## Schema lifecycle

`SchemaDbContext<TSchemaDb>` implements a simple ordered migration baseline:

```text
CreateAllAsync: Definitions first → last
DropAllAsync:   Definitions last → first
```

Each `SchemaObjectDefinition` contains a stable name, create statement, drop statement, and an optional exact set of provider-error fragments that mean an additive operation is already applied. The error fragments are used only where a supported Scylla release does not accept `ADD IF NOT EXISTS`; unrelated errors still fail schema application. The schema layer does not record migration versions; it executes the checked-in definitions against an already configured database/keyspace.

The Market Data catalog uses that narrow additive convention for the authoritative `futures_rsi_signal.sourceSequence` and `sourceEventTimestamp` columns. New table creation includes the columns directly, while repeated application to an existing development table accepts only Scylla's already-existing-column responses. The RSI insert, select, positional bind catalog, and ordinal mapper preserve both source-lineage values end to end.

| Schema context | Managed objects |
| --- | --- |
| Event Source | 3 sequences and 5 tables: stream IDs, event names, event log, command log, and projector state. |
| Fund | 14 canonical/projection tables, including permanent order-ID ownership, exact-key transaction identity reservations, monthly transaction queries, readiness markers, and distributed write ownership. |
| Log | 1 telemetry-log table. |
| Market Data | Canonical live-feed, futures/option, analytics, ITI trend data/models, curve, holiday, and quote objects plus tick/EOD/VX query projections and cutover state. |
| Option Pricer | 3 tables for devices, jobs, and distributions. |
| Reference | 13 canonical/projection tables, including country/month and exact-name queries, scoped projection readiness, scheduled-job ID/name mutation ownership, and the LWT seed allocator. |
| Securities | Canonical futures/option contracts plus symbol projections and generation-aware cutover state. |
| Sequence ID | 2 functions plus a generated sequence definition for every `SequenceName`. |
| Trade | 17 option/trade/position/order/plan/signal tables. |

## Bulk, mapping, and read/write conventions

- Domain contexts declare static mapper methods that read `IObjectDataRecord` by zero-based ordinal and construct shared read models directly.
- Every SQL/CQL projection must remain in exactly the order expected by its mapper. Alias/name matching is not performed on the result hot path.
- Stream, query, single, scalar, immutable, and map/reduce calls receive the mapper explicitly; there is no `OnCreateModel`, result-map registry, property assignment, or reflection-based result construction.
- `ExecuteStreamAsync` is an additive, cold `IAsyncEnumerable<T>` API for large results. Existing methods remain available; an active stream must be fully enumerated or disposed because it owns its database reader/row set until then.
- All ScyllaDB parameter catalogs—Fund, Market Data, Option Pricer, Reference, Securities, Trade, and the compiled domain-local bind values—emit positional `object?[]` values through `IBindValue` in prepared-statement marker order. Live Scylla context call sites do not use anonymous parameter objects, and the provider has no reflection fallback or bind-property cache.
- PostgreSQL Event Source, Log, and Sequence ID catalogs emit strongly typed, unnamed `NpgsqlParameter<T>` arrays through `IBindValue`, ordered exactly like native `$n` SQL placeholders. The PostgreSQL provider no longer discovers properties, calls `PropertyInfo.GetValue`, uses a reflection/type cache, or clones generated parameters before command execution.
- Parameterized PostgreSQL text commands are explicitly prepared and persist on pooled physical connections. Queued PostgreSQL commands execute through one `NpgsqlBatch` round trip inside an explicit transaction; failures retain all-or-nothing rollback behavior.
- Single-record and multi-record Scylla writes execute checked-in CQL with the same positional contract. Enumerable `SetParameters` inputs are consumed lazily through the Framework Storage bounded-write pipeline, which avoids whole-input parameter materialization, per-row tasks, and ordinary logged batches. The public Application Storage APIs are unchanged.
- Ordinary Scylla collection writes execute cached prepared statements with per-call bounded concurrency and token-aware routing. `SCYLLADB_BULK_MAX_CONCURRENCY` defaults to 32 and `SCYLLADB_BULK_BOUNDED_CAPACITY` defaults to 64. No storage-layer semaphore serializes repository instances or simultaneous provider calls. Explicit queued `useTransaction: true` workflows remain logged atomic batches; trade-fill writes are limited to one logical fill batch at a time and option-trade deletion remains a small explicit cross-table batch.
- Fund transaction retries publish their distributed mutation marker before using an exact-logical-key `IF NOT EXISTS` identity reservation. This prevents concurrent callers from allocating different canonical IDs without introducing a process lock, semaphore, or fund-wide Paxos partition.
- Fund order IDs remain permanently assigned to their first fund in `fund_order_by_order_id_v3`, including after canonical deletion; `fund_order_write_ownership_v3` serializes mutations by order ID and is reclaimed only by exact operation UUID under an explicit writers-drained cutoff. Reference scheduled-job names remain reusable, while `scheduled_job_write_ownership_v3` claims the job ID and affected exact-name scopes before state reads and holds them through canonical/name-reservation completion.
- Option trade-spread sequence generation is awaited before each row is submitted; the previous unawaited `List<T>.ForEach(async ...)` and synchronous `GetAwaiter().GetResult()` paths have been removed. Futures ITI trend class/delta collection writes now use positional `IBindValue` records instead of anonymous objects.
- Database-independent catalog tests verify positional bindings against each CQL marker sequence, including nullable values, update-marker order, LWT ownership parameters, and `DateOnly` values for CQL `date` columns.
- Several Fund, Market Data, and Trade APIs accept `IEnumerable<T>` and return inserted row counts for bulk operations.
- Combined contexts must expose `DbReader => this` and `DbWriter => this`, giving consumers capability-oriented interfaces over one repository object.
- Query methods generally return nullable single records and non-null collections.
- Command text and schema are source-controlled separately, allowing operation changes without embedding large query strings in context methods.

## Operational characteristics and current limitations

- **Application IDs use one PostgreSQL authority.** Tick, Reference, trade, fund, request, and quote identifiers are allocated through the named `ISequenceIdGenerator`; the former Scylla and Redis counters have been removed.
- **Resolution assumes correct DI registration.** `DbContextResolver` and several factory casts suppress nullability; a missing or incompatible registration becomes a later null-reference failure rather than a descriptive resolution exception.
- **Factory pool-map access is not synchronized.** Concurrent first access to the same pool type can race around the ordinary `Dictionary<Type, object>`.
- **The pool is incomplete.** Two result overloads throw `NotImplementedException`, and only the Reference pool is exposed.
- **The pool is unbounded and does not dispose entries.** Returned repositories remain in a static queue for the process lifetime.
- **A null pool callback can fail at runtime.** `ExecuteAsync` accepts a nullable delegate but awaits its null-forgiven invocation.
- **Repository thread safety depends on host lifetime and use.** Factory properties resolve on every access, while provider repositories contain mutable command/context state; hosts should avoid unsafe singleton sharing unless the framework registration is designed for it.
- **Schema management is not versioned migration.** Create/drop catalogs have no history, checksum, upgrade ordering across releases, or rollback metadata.
- **Create/drop is not wrapped here in a cross-object transaction.** Partial schema state is possible after a failure.
- **Actor and standard event sources can use different connections.** The shared schema manager targets only the standard event-source connection, so host configuration must intentionally align or separately provision actor storage.
- **TradePlanDb is active PostgreSQL persistence.** Its context, contracts and schema are compiled and covered by isolated transaction/history regression tests.
- **External reader failure policies differ.** Economic Calendars converts/skips rows and returns empty on outer failure; Yield Curve Rates lets failures propagate.
- **Some contexts are very broad.** `MarketDataDbContext` and `TradeDbContext` combine many aggregates and analytics tables, increasing change and regression scope.
- **Provider syntax is mixed by design.** SQL and CQL catalogs coexist; connection provider settings must match the command/schema syntax selected by each context.
- **Ordinal projections are intentionally strict.** A reordered or inserted selected column can silently map the wrong value when compatible types are involved; projection and mapper changes must be reviewed and tested together.
- **Database credentials are runtime requirements.** PostgreSQL and ScyllaDB fail fast when their selected environment variable is absent, malformed, conflicts with the application environment, or when a base string still embeds credentials.
- **Legacy build artifacts remain.** `net8.0` output/intermediate folders are not part of the current `net10.0` target.

## Verification locations

Storage behavior is validated primarily outside this project:

- `TomasAI.IFM.Application.Storage.IntegrationTests` exercises Event Source, Fund, Log, Market Data, Option Pricer, Reference, Securities, and Trade contexts.
- Its `FrameworkStorage/ScyllaDb` suite contains 20 real-provider tests across all four Fund tables. It covers every `IObjectRepositoryProvider` method, large single-pass bounded writes, write cancellation before enumeration, both Scylla queued-command modes (including multiple bind values in one logged command), ordinal Fund types, argument guards, disposable pooled immutable results, async streaming, early disposal, and cancellation.
- Its `FrameworkStorage/Postgres` suite contains 19 real-provider tests across all five event-source tables. It covers the same provider API surface, ordinal PostgreSQL types, argument guards, async streaming lifecycle, server-side prepared-statement registration, single-round-trip queued batches, and rollback when a later queued command fails.
- Both provider suites disable collection parallelism, reserve deterministic negative identifiers/names, clean before and after every test, verify cleanup, and avoid production databases. They are selected with `Category=ScyllaDBIntegration` or `Category=PostgresIntegration`.
- `TomasAI.IFM.Application.Storage.LoadTests` covers storage load scenarios.
- `TomasAI.IFM.Framework.Storage.Benchmarks` compares the former Scylla logged-batch implementation with bounded concurrency at 100/1,000 rows and one/32 partitions, then emits individual and overall percentage changes.
- `TomasAI.IFM.Framework.Storage.UnitTests` validates lower-level provider-neutral repository behavior.
- Event projector persistence tests exercise per-projector state in the Event Source database.

## Safe extension points

1. Add new operations to capability-specific read/write interfaces before implementing the context method.
2. Keep command projection order, ordinal mapper access, parameter objects, and integration tests aligned.
3. Add schema objects in dependency order and drop them in reverse through `SchemaObjectDefinition`.
4. Treat connection-setting names, credential environment keys, and provider syntax as deployment contracts; never add PostgreSQL or ScyllaDB credentials back to a base connection string.
5. Complete and test pool overloads before exposing additional pooled contexts.
6. Prefer explicit resolution errors over null-forgiving casts when evolving the factory/resolver.
7. Decide whether excluded legacy folders should be migrated, split into separate projects, or removed to avoid ambiguity.
8. Add migration/version tracking if schema evolution must support production upgrades rather than clean provisioning.


## Option pricing reference ownership

SecuritiesDb is the sole active persistence owner of reviewed security contract versions,
the pricing conventions derived from those versions, and `option_pricing_reference_bundle`
manifest rows. Runtime and explicit maintenance publication use the same SecuritiesDb connection.

- Publish qualified `FuturesOptionContractReadModel` values through `StageReferenceVersionAsync`
  followed by `CommitReferenceVersionAsync`. `GetAsync` derives the convention from the
  published version; there is no independently writable convention table.
- ReviewedOptionReference in Application.MarketData applies explicit pricing review terms to
  an exact provider definition. Provider identity, digest, strike and underlying identity remain
  attached to that definition. Published version conflicts require a new mapping version.
- OptionPricingReferenceBundleRepository in Domain.MarketData.Securities/OptionPricing/Repository
  owns bundle validation, serialization, publication and read-back confirmation. Storage/OptionPricing
  is retired. Persistence remains behind the SecuritiesDb read/write contracts; the context
  returns the primitive manifest payload and does not coordinate reference publication.
- Bundle publication checks the exact published version, definition digest and calendar version
  of every member before inserting an immutable manifest. Reads validate its content digest.
- ReferenceDb retains general reference metadata and instrument-definition ingestion. Its pricing
  stores, pricing properties and pricing schema registrations are retired.

### Existing installations

Schema creation adds the bundle table to SecuritiesDb without deleting any ReferenceDb data.
Retired ReferenceDb pricing rows are not read as a runtime fallback. Existing reviewed bundles
must be explicitly republished through maintenance after their complete provider contracts have
been qualified and published in SecuritiesDb. Old convention-only rows lack the strike/right and
underlying provider evidence required for a complete reviewed contract and must not be blindly
copied into a new reference-version envelope. Do not automatically delete the retired tables.

Explicit maintenance modes require only SecuritiesDbConnection for pricing publication. The
reviewed ES tool records the reviewed profile evidence timestamp separately from any purported
provider update timestamp. The cached October qualification uses the existing definition evidence.

Development cleanup (2026-10-11): at the owner's request, the unused
`reference_test_db.option_pricing_reference_bundle` and
`reference_test_db.option_pricing_convention` tables were dropped and their absence
verified. No rows were migrated. Other ReferenceDb tables and SecuritiesDb tables
were left intact. SecuritiesDb schema initialization creates its new bundle table
on the next schema initialization or explicit pricing publication.

## EventSourceDb convention alignment

The structural cleanup and domain ownership separation below follow the DbContext boundary: domain repositories and actors coordinate command execution; Storage supplies persistence operations and provider mechanics.

EventSourceDb retains its established `EventSourceActorDbContext` and read/write contracts. The concrete context implements read/write persistence and resource lifetime contracts; `EventSourceActorDbContextExtensions` owns its C# 14 context helpers. Both carry explicit XML operation documentation. Local cache-resolution helpers and inline projector protocol guards are owned by that extension class. Existing revision checks, execution ownership, timestamp requirements and command deduplication remain enforced.

`EventSourceDbParameters.cs` is the single catalog for all context `IBindValue` records; the former per-record `Parameters` directory is removed. `Schema/EventSourceSchemaSql.cs` owns all production DDL and dependency-ordered schema definitions, including the business-subscription and risk-history receipt tables. `EventSourceSchemaDb` selects the named connection and delegates to that catalog and the inherited schema executor. Runtime journal operations reference the catalog when they need additive initialization. Benchmark-specific provider fixtures reside in `Framework.Storage.Benchmarks/CommandLog` and are not compiled into production Storage.

## EventSourceDb domain ownership separation

Shared domain event-source repository coordination is owned by `TomasAI.IFM.Domain.EventSourcing/Repository/BaseEventSourceActorRepository.cs`; concrete domain repositories continue inheriting that base. `Command/CommandAuditCoordinator.cs` implements `ICommandAuditLogger` and owns pre-parse acceptance, duplicate callers and command reservation diagnostics. The bounded coordinator resides in `Command/Deduplication`. Hosts only register this domain service. DbContext no longer implements the audit coordination interface or retains command acceptance caches. It persists supplied audit facts through `TryInsertCommandAuditAsync` and retains atomic command-audit/event commits and database optimistic concurrency.

Provider serialization and command-audit SQL writers remain in `EventSourceDb/CommandAudit`; event-log appending and technical per-stream preparation remain in `EventSourceDb/EventLog`. Command-log comparison stores are owned by `Framework.Storage.Benchmarks/CommandLog` and excluded from the production Storage assembly. Historical checkpoint/manifest persistence is owned by `MarketDataDbContext`, with statements in `MarketDataDbSql`, bindings in `MarketDataDbParameters` and DDL in the MarketData schema catalog. Physical table names remain unchanged, preserving existing records in the consolidated PostgreSQL database. Import orchestration already resides in the owning market-data services.

### Verification (2026-10-12)

- 69 focused tests passed, including PostgreSQL audit reservations, conflicting payloads, atomic command/event persistence, snapshot reads and command-log benchmark contracts.
- 12 final tests passed for domain audit coordination, actor dispatch and DbContext architecture conventions. The architecture gate permits shared domain data contracts but prevents Storage from depending on domain coordination implementations or benchmark assemblies.
- Storage, API Server Core and the actor integration test host compiled with zero warnings and errors. API Server Core was compiled against rebuilt dependencies because the running market-data workers lock their output DLLs; a full dependency build cannot overwrite those active worker files.
- EventSourceDb XML documentation and semantic unused-import checks passed. No deployed data was deleted or moved, and the running application was not restarted as part of this refactor.

## MarketDataDb convention alignment (2026-10-12)

MarketDataDb uses one non-partial `MarketDataDbContext` that implements its read/write contracts. All 190 context binding types are consolidated in `MarketDataDbParameters.cs`; the former individual parameter source files are removed. Public context and contract operations have XML summaries, parameter and return documentation. Context helpers are consolidated in the C# 14 blocks in `MarketDataDbContextExtensions.cs`, including transaction-bound realtime and VIX helpers.

`Schema/MarketDataSchemaSql.cs` owns the PostgreSQL DDL and the complete dependency-ordered schema definition catalog. `MarketDataSchemaDb` selects the named MarketData connection and delegates schema execution to that catalog. Existing PostgreSQL table/index statements and historical loader table names are preserved. Normal persistence paths use PostgreSQL statements. Raw trade/quote persistence continues delegating to TradeTickQuoteDb.

Calendar calculations are owned by `Domain.MarketData.Shared/Calendar/TradingCalendar.cs`; context queries read current persisted holidays and pass them to that calculator. The context no longer keeps trading-day or normal-curve caches. Missing option quote Greeks are calculated by `Domain.MarketData.Feed/Pricing/OptionQuoteGreekEnrichment.cs` in the option tick domain projector before quote persistence. The raw tick is preserved and supplied Greeks remain unchanged. Historical backfill test tooling uses the same domain calculator. Storage receives the resulting quote facts.

### Verification

- 15 MarketDataDb convention and isolated PostgreSQL persistence tests passed, including direct observation transactions, related schema/catalog reads, signal writes, lookup repair rollback and option-volatility persistence.
- Four domain calendar tests passed for inclusive dates, holidays, updated holiday inputs, cancellation and date bounds.
- Four market-data projector tests passed, including the moved quote enrichment path, preservation of raw prices and supplied Greeks, and persisted-event ID behavior.
- One pricing regression test passed: all Greeks are calculated without changing recorded prices or timestamps.
- Semantic verification found zero errors, unused imports and malformed XML documentation in MarketDataDb.
- Storage and domain dependency builds passed; API Server Core compiled against rebuilt dependencies with zero warnings/errors. Full host dependency overwrite remains unsuitable while market-data worker binaries are in use.
- Test persistence uses isolated fixture databases. No development database data was deleted or migrated, and running application processes were not restarted by this refactor. This focused suite establishes coverage for affected paths; it does not claim a full repository test run.

### PostgreSQL maintenance API update

The public `BackfillQueryProjectionsV2Async` and `GetQueryProjectionReadinessAsync` methods are retained and implemented for PostgreSQL. Their Scylla mutation-journal/cutover implementation is replaced. Normal PostgreSQL EOD, VIX and ITI queries read canonical tables directly, so no duplicate projection-table population is required. Backfill recreates missing canonical indexes and reindexes them in one transaction. It takes table share locks to keep the source inventory stable; run this explicit maintenance outside active ingestion. Lock waits are bounded to five seconds, individual maintenance statements to fifteen minutes, and caller cancellation remains supported. Failure before commit rolls back the maintenance transaction. The statement budget is local to that transaction and does not change normal business transaction settings or durability.

Readiness uses PostgreSQL catalog evidence: index existence, table ownership, valid/ready flags, expected key columns, and absence of unexpected partial/expression definitions. It performs no schema writes and does not rely on old Scylla cutover markers. A correctly named index with incorrect columns is not ready.

The compatibility backfill result's source/projected counts and fingerprints describe logical queries on the same canonical table, not independent copies. Raw tick maintenance is explicitly excluded with `RawTicksManagedByTradeTickQuoteDb`; the tick readiness flag is false and ignored by overall PostgreSQL readiness only when that ownership flag is set. Raw tick backfill counts are zero with a not-applicable marker. These methods make no claim about Scylla health or tick reconciliation. `batchSize` and `staleOperationCutoffUtc` remain validated compatibility arguments; server-side index maintenance does not batch row writes or recover Scylla journals.

The market migration CLI uses `IFM_STORAGE_MIGRATION_MARKET_DATA_POSTGRES_CONNECTION` with the PostgreSQL provider. Its additive schema option uses the PostgreSQL catalog. Scylla catalogs/support methods retained for other historical compatibility paths are not called by these two APIs. This is index maintenance of existing canonical data, not a replacement for domain-owned replay from the authoritative event log.

Verification of the API update: the final 14-test run passed (13 convention checks and the isolated EOD transaction/maintenance regression). It covers missing and incorrectly defined index detection, index recreation, rollback, repeatability, fresh writes and cancellation. The other PostgreSQL atomic observation test passed in the preceding run. Storage, ProjectionMigration and API Server Core builds completed with zero warnings/errors; XML/import semantic checks passed. Test sample setup errors (credential resolver, deleted input rows and UTC timestamps) were corrected before the successful final run. No maintenance was run against the active Development databases.


## Historical-load persistence consolidation

The separate `MarketDataDb/HistoricalDataLoader` folder and standalone `PostgresHistoricalDataLoaderStore` repository are removed. `GetHistoricalDataLoadAsync`, `GetCompletedHistoricalDataLoadAsync` and `SaveHistoricalDataLoadAsync` are documented operations on the MarketDataDb read/write contracts and main context. The mapper is in the context, three bindings are in `MarketDataDbParameters`, and the existing checkpoint/manifest statements are in `MarketDataDbSql`. DDL remains in `MarketDataSchemaSql`; table names, binding order, JSON serialization and the atomic checkpoint/immutable-manifest statement are preserved.

`Domain.MarketData.Analytics/HistoricalDataLoader/HistoricalDataLoaderStore` is the thin adapter for the existing `IHistoricalDataLoaderStore` port. It delegates to the factory's MarketDataDb context without owning SQL, provider connections or loading orchestration. API and actor-host registration use that adapter. Historical acquisition and analytics coordination remain outside DbContext.

Verification: 14 context convention and isolated PostgreSQL persistence tests passed, including checkpoint resume reads, completed-request lookup, immutable manifests, duplicate-completion refusal, atomic rollback and cancellation. Two existing historical-loader unit tests passed. Domain Analytics, API Server Core and actor integration host builds completed with zero warnings/errors. MarketDataDb semantic checks reported zero errors, unused imports and malformed XML documentation. Test data was confined to a disposable fixture database; existing Development data was not changed.


### MarketDataDb PostgreSQL schema catalog

MarketDataDb schema creation is owned exclusively by `Schema/MarketDataSchemaDb.cs` and `Schema/MarketDataSchemaSql.cs`. The retired `MarketDataSchemaCql.cs` and `OptionVolatilitySchemaCql.cs` files have been removed. Schema policy tests now validate PostgreSQL DDL, indexes, conflict fences and immutable volatility evidence. Volatility persistence verification uses a disposable PostgreSQL instance. Raw trade/quote Scylla schema creation remains under TradeTickQuoteDb.


### Internal persistence helper types

Small internal types used exclusively by DbContext persistence helpers are declared at file scope in `<Name>DbContextExtensions.cs`, alongside the helpers that consume them. They retain internal visibility and are not public query result models or parameter bindings. Public query results remain in shared read models, parameter bindings remain in `<Name>DbParameters.cs`, and result-set `MapTo...` methods remain in the main context.

MarketDataDb consolidates its calendar continuation token and partition, ITI projection scope identity, and trade-signal repair row/parse result in `MarketDataDbContextExtensions.cs`. Their members, record semantics, JSON continuation-token shape and struct layout remain unchanged; the five standalone files are removed.


### Transaction-bound MarketDataDb persistence

Caller-owned PostgreSQL transaction operations are consolidated in the `IEnlistedPostgresTransaction` C# 14 receiver block in `MarketDataDbContextExtensions.cs`. Operations needed by domain repositories/projectors are public; other helpers remain internal. These helpers preserve the supplied transaction and never create independent commits. SQL statements remain in `MarketDataDbSql.cs` and import ownership remains an internal persistence type alongside the extensions.

The standalone PostgreSQL economic-calendar, yield-curve, option-chain parameter, volatility and trade-signal index stores are removed. `Domain.EventSourcing/MarketData/AcceptedMarketSignalProjection.cs` owns source-event dispatch and mapping an accepted daily session bar into an EOD observation. Storage exposes typed transaction mutations and does not dispatch arbitrary `IEvent` objects. Provider calls, signal calculation and notification publication are absent from the persistence helpers. Context public instance operations continue implementing read/write contracts.


### MarketDataServiceDb alignment

- `MarketDataServiceDbContext` implements the read/write contracts and ordinal result mapping.
- `MarketDataServiceDbParameters.cs` contains all internal PostgreSQL bindings.
- `MarketDataServiceDbContextExtensions.cs` owns provider operations and file-scoped internal transaction/result helpers.
- `Schema/MarketDataServiceSchemaSql.cs` is the single schema catalog; `MarketDataServiceSchemaDb` creates all tables, constraints and indexes in dependency order.
- Pure subscription ownership rules live in `Application.MarketData/Subscriptions/Persistence/DurableSubscriptionTransition.cs`. Persistence evaluates those rules under the dataset row lock and atomically saves the snapshot, operation outcome, identity reservations, watermark and outbox. No dispatch or recovery orchestration is added to the context.


### OptionPricerDb alignment

OptionPricerDb uses PostgreSQL exclusively. `OptionPricerDbContext` is the non-partial read/write facade with ordinal `MapTo` mappings. Bind values are consolidated in `OptionPricerDbParameters.cs`; UTC timestamp conversion and typed provider operations use receiver blocks in `OptionPricerDbContextExtensions.cs`. `Schema/OptionPricerSchemaSql.cs` is the single schema catalog, executed by `OptionPricerSchemaDb`.

`Domain.OptionPricer/Persistence/AcceptedOptionPricerProjection.cs` owns accepted-event dispatch. Typed extensions on `IEnlistedPostgresTransaction` persist distribution pairs, jobs and status/deletion facts in the caller's transaction, without independent commits or recalculation. Deterministic source-derived IDs, numeric precision, nullable timestamps and source-order selection remain unchanged. In-progress count and cleanup execute directly in SQL rather than loading and iterating all jobs. Pricing tests share disposable infrastructure; they do not use Development databases.


## PortfolioDb alignment

`PortfolioDbContext` implements the documented read/write contracts and delegates typed persistence to the single C# 14 `PortfolioDbContextExtensions` class. Accepted Portfolio projection writes use the caller's enlisted transaction; they do not start a separate commit or reconstruct aggregate state. Positional and JSONB bindings are consolidated in `PortfolioDbParameters.cs`.

`Schema/PortfolioSchemaSql.cs` owns projection, financial-history and financial-authority DDL. `PortfolioSchemaDb` exposes both named schema definitions and creates them after the event-log schema, which supplies financial foreign-key targets. Runtime queries remain in `PortfolioDbSql.cs`.

Ledger/capacity decisions, authority preparation, accepted-event dispatch, baseline capture and event-only rebuilding belong to `Domain.Portfolio/Financial/Repository`. Order-composition transaction coordination belongs to `Domain.Portfolio/OrderComposition/Repository`. Their existing request-owned transaction sequences, locks, source fences, receipts, accepted facts and failure ordering are preserved. Domain repositories use the storage SQL catalog and enlisted persistence boundary. Financial transaction diagnostics remain in `Storage/EventSourceDb/Diagnostics`; they cannot abort transaction outcomes. Host registration now identifies the financial services as domain services.


Portfolio verification: 15 storage integration cases and 129 financial/projection unit cases passed. An isolated harness executed the existing 18 ledger, atomicity and event-only rebuild cases directly; all passed, including the verified baseline and capacity reconstruction. The full host test bootstrap was not the persistence verification boundary. API Server Core and all three migration CLIs compiled. Semantic checks found zero unused imports and malformed XML documentation in the context and relocated repositories. Disposable database infrastructure was removed; the running Development application was not restarted.


### ReferenceDb convention alignment (2026-10-12)

ReferenceDb is PostgreSQL persistence. Its organization is:

```text
ReferenceDb/
  ReferenceDbContext.cs
  IReferenceDbContext.cs
  IReferenceDbReadContext.cs
  IReferenceDbWriteContext.cs
  ReferenceDbContextExtensions.cs
  ReferenceDbParameters.cs
  ReferenceDbSql.cs
  Schema/
    ReferenceSchemaSql.cs
    ReferenceSchemaDb.cs
```

The nonpartial context implements documented read/write capabilities and contains ordinal
`MapTo...` mappings. All bindings are internal readonly records in `ReferenceDbParameters.cs`;
small internal support records and persistence helpers reside in the one receiver-based extension
catalog. SQL statements live in `ReferenceDbSql.cs`. The schema executor owns active Reference
tables and indexes through the single PostgreSQL DDL catalog. Removed CQL branches and reservation
support cannot be selected at runtime. Previously created shadow tables are not dropped by this
source refactor; new schema creation omits them.

`Domain.Reference/Repository` owns the instrument-definition adapter, product identity allocation,
publication sequencing, cursor validation/filtering, family bootstrap and retained family-catalog
operation policy. These repositories use typed Reference read/write capabilities. The context
neither constructs their instances nor invokes their coordination logic. The API Core registers
these services in `DependencyInjection/Domain/RegisterReferenceServices.cs`; the futures-contract
query actor receives `IInstrumentDefinitionStore` through its typed context.

Accepted lookup writes accept the caller-owned `IEnlistedPostgresTransaction`, preserving atomic
source-event append and read projection updates. Standalone context CRUD still owns its local
PostgreSQL transaction. Scheduled jobs use transaction-scoped advisory locks and exact, case-sensitive
PostgreSQL unique indexes; no cross-table Scylla ownership handshake is required. Compatibility
backfill is a bounded validated no-op because native query indexes require no separate backfill;
reconciliation checks canonical rows and orphan weekday rows. Existing seed APIs continue to delegate
to the technical sequence allocator, preserving their named query contracts.

Regression tests use generated databases on disposable PostgreSQL infrastructure. Coverage includes
lookup ordering and accepted-event rebuilding/rollback, scheduled-job weekdays/rename/delete,
case-sensitive uniqueness and rename collision rollback, exact instrument JSON preservation,
publication visibility, stable product identities, selector pagination and cursor isolation,
concurrent family-catalog operations, and restart reads. Former CQL reservation tests were replaced
by the active PostgreSQL checks.

Verification: all **22 Reference storage/convention tests** and **208 Reference domain unit tests**
passed. Storage regression tests ran on disposable PostgreSQL infrastructure, including generated
instrument/catalog databases. API Server Core compiled with zero warnings and errors. Semantic checks
of ReferenceDb and the moved Reference repositories reported zero errors, unused imports and malformed
XML documentation. This refactor did not restart the Development application or run a data deletion.

The two focused futures-contract query tests also passed, including injected instrument-service/cancellation coverage. The affected Portfolio integration-test assembly compiled in an isolated output directory with zero warnings and errors. Disposable test containers were removed by their fixtures.


## ScheduledTaskDb alignment (2026-10-12)

`ScheduledTaskDbContext` is the sole PostgreSQL scheduled-task repository. `IScheduledTaskDbContext` combines the generic repository, read and write contracts; the existing domain scheduled-task read/projection contracts remain supported. `IDbContextFactory` exposes the typed context and schema. Host registrations resolve the same context for domain reads, projections and enlisted writes.

- `ScheduledTaskDbContext.cs`: documented contract methods and ordinal snapshot/date mappers.
- `ScheduledTaskDbContextExtensions.cs`: technical keyset cursor handling, revision-fenced accepted snapshot writes, and local transaction resources. Enlisted writes use the caller's event transaction without a separate commit; standalone projection writes commit the run and completed session date together.
- `ScheduledTaskDbParameters.cs`: ordered `IBindValue` bindings.
- `ScheduledTaskDbSql.cs`: the single parameterized runtime SQL catalog.
- `Schema/ScheduledTaskSchemaSql.cs`: all PostgreSQL table/index DDL; `ScheduledTaskSchemaDb.cs` creates these objects additively.

The obsolete Scylla paths and duplicate PostgreSQL store are removed. Persisted PostgreSQL table identities, MessagePack payloads, revision comparisons, query-scoped history cursors, output-tail stripping and connection-name fallback are retained. The bounded filesystem output reader lives in `Storage/FileSystem`, outside database persistence; scheduler policy, run lifecycle, event append and replay coordination remain in Domain.SystemAdmin.

Regression verification uses disposable PostgreSQL infrastructure, covering restart reads, stale definition/catalog/run revisions, monotonic completed session dates, multi-page history, cursor isolation, atomic standalone writes, and enlisted source-event/projection commit and rollback. Architecture tests guard contracts, helper placement, schema/SQL ownership and absence of Scylla paths. Host compilation and SystemAdmin tests cover the changed registrations and artifact reader.

Verification completed: **13 ScheduledTaskDb regression/architecture cases** and **155 SystemAdmin unit tests** passed. Storage, Api.Server.Core and both affected migration utilities compiled with zero warnings/errors. Semantic review found no unused imports or malformed XML documentation in ScheduledTaskDb. Tests created and disposed their own infrastructure; no Development data was changed.


## SystemAdminDb alignment (2026-10-12)

`SystemAdminDbContext` provides typed PostgreSQL reads and accepted projection writes through `ISystemAdminDbReadContext` and `ISystemAdminDbWriteContext`. Every context contract method is documented; ordinal mappers remain on the nonpartial context.

The database folder contains the main context, three contracts, one `SystemAdminDbParameters.cs` binding catalog, one `SystemAdminDbSql.cs` runtime SQL catalog, one C# 14 receiver-helper catalog, and `Schema/SystemAdminSchemaDb.cs` plus `SystemAdminSchemaSql.cs`. Internal operation mapping support is consolidated with the receiver helpers. The schema executor owns dependency order; all creation, indexes, additive changes and drops are catalogued in the schema SQL file.

Domain.SystemAdmin now owns `DatabaseBackupProjectionFactory`, which validates authoritative events, preserves the existing receipt hash and derives operation routing, terminal/verification times, eligibility, legal holds, policy enforcement, service readiness and retention approval. It supplies a `DatabaseBackupProjectionSnapshot` to storage. DbContext helpers bind these accepted facts, maintain revision fences and receipts, and preserve caller-owned transaction enlistment. Standalone read-model rebuild writes retain their existing local transaction boundary.

`DatabaseBackupExecutionIntentRepository` owns domain decoding and dispatch coordination. The context exposes only typed enqueue, pending event-record reads and publication acknowledgement operations; SQL remains in the database catalog. Source append, projections, receipts and enqueue still share the command's PostgreSQL transaction. Backup-set completeness, run-statistics aggregation and recovery-objective query translation belong to domain query handlers. The filesystem backup output reader is outside the database folder in `Storage/FileSystem`.

Stored table names, event payloads, receipt hashing, row revision comparisons and read-model response contracts are preserved. Verification uses disposable PostgreSQL infrastructure so projection clear/rebuild tests cannot alter Development databases.

Verification completed: **16 SystemAdminDb schema, projection, rebuild, atomicity and architecture cases** and **161 SystemAdmin unit tests** passed. Api.Server.Core compiled with zero warnings/errors. Storage and the new domain repositories have no unused imports or malformed XML documentation. Disposable test infrastructure was cleaned up; no Development database data was changed.


## TradeDb alignment (2026-10-12)

TradeDb is PostgreSQL persistence with the following organization:

    TradeDb/
      TradeDbContext.cs
      ITradeDbContext.cs
      ITradeDbReadContext.cs
      ITradeDbWriteContext.cs
      IEnlistedTradeDbWriter.cs
      TradeDbContextExtensions.cs
      TradeDbParameters.cs
      TradeDbSql.cs
      Schema/
        TradeSchemaDb.cs
        TradeSchemaSql.cs

The nonpartial context implements documented read/write capabilities and owns ordinal
MapTo result mappings. The write capability includes the caller-owned PostgreSQL
projection transaction contract. Operations use this repository directly rather than
routing back through IDbContextFactory; the constructor retains that argument for host
registration compatibility.

The single parameter catalog consolidates the existing internal readonly binding records
without changing their ordered values. The one receiver-helper catalog owns technical
serialization, query cursor validation, immutable revision checks and persistence resource
handling. Its small internal cursor/result records are declared at file scope. Separate
PostgreSQL trade-flow, pipeline, regime and workflow stores are retired. SQL constants and
fixed query builders reside in TradeDbSql; all schema creation and additive changes use
Schema/TradeSchemaSql through TradeSchemaDb. Retired Scylla schema catalogs and provider
fallback branches are removed.

Domain.Trade repositories and projectors retain command audit, source-event append,
accepted-event dispatch, notifications and replay coordination. The database-independent
Domain.Trade.Shared/Persistence/TradeProjectionFacts owns accepted pipeline identity checks
and workflow status/outcome interpretation. DbContext does not rerun selection, compose an
order, call the broker, or dispatch actors.

Accepted order, execution/fills, trade, position/routes and workflow/pipeline projections
continue using the supplied transaction without a separate commit. Standalone writes retain
their local transaction boundary. Table identities, MessagePack payloads, revision fences,
daily position history semantics, routing generations and ascending history pages are
preserved. Native workflow queries bind the PostgreSQL parameter array as one execution
through TradeDbValues, avoiding the enumerable overload that would treat individual
parameters as separate executions.

Regression coverage uses disposable infrastructure and includes source/projection rollback,
order revisions, position lifecycle and route removal, stale position protection, all 205
position history rows through bounded pages, workflow read units and notifications,
Regime Discovery immutable invocation conflicts, tied-timestamp pipeline pages and
query-scope cursor rejection. Convention checks guard the ten-file structure, contracts,
ordinal mapper ownership, receiver helpers and parameter binding. No Development database
data is changed by this source refactor.


Verification: all 33 TradeDb storage/architecture regression cases and all 1,496 trade
domain unit cases passed. Api.Server.Core and the offline migration utility compiled
with zero warnings and errors. TradeDb semantic checks found zero errors, unused imports
and malformed XML documentation. Regression fixtures removed their own containers;
Development services and databases were not restarted or modified.

All 12 trade-flow qualification checks also passed after updating obsolete schema
references, using a weekday session for market-tick replay, and asserting the existing
disposable-plan projection contract. These qualification updates change test expectations
and fixtures, not trading behavior.

## TradePlanDb convention alignment

TradePlanDb is PostgreSQL-only. Its persistence folder contains nine C# files:

- TradePlanDbContext.cs: the non-partial public context implements the documented read/write contracts and owns ordinal result mapping.
- ITradePlanDbContext.cs, ITradePlanDbReadContext.cs and ITradePlanDbWriteContext.cs: typed repository, query and mutation capabilities, including caller-owned transactions.
- TradePlanDbContextExtensions.cs: C# 14 receiver helpers for accepted mutations, local transaction/resource ownership, identity validation and scoped keyset pagination. The internal cursor type is file-scoped here.
- TradePlanDbParameters.cs: the position binding and the complete Iron Condor scalar binding; scalar ordinals are unchanged.
- TradePlanDbSql.cs: native PostgreSQL positional SQL, immutable-revision conflict fences and deterministic paging query construction. No runtime CQL conversion remains.
- Schema/TradePlanSchemaSql.cs: the single PostgreSQL DDL catalog for all plan/workflow tables, scalar columns, indexes and additive migrations, plus explicit administrative teardown.
- Schema/TradePlanSchemaDb.cs: executes that catalog through SchemaDbContext.

PostgresTradePlanStore, the extra scalar projector, the obsolete CQL catalogs and superseded per-operation binding files are retired. Physical table names, MessagePack payloads, risk scalar field ordering, revision conflict behavior and scoped descending history cursors remain unchanged.

Caller-owned mutations never commit independently: source event, plan/activity rows or exit-stage rows participate in the supplied transaction. Standalone monitoring writes attempt one local transaction; failures are logged and the snapshot is dropped. This refactor adds no retry, plan replay, history repair or retained monitoring cache.

ExitWorkflowProjectionBaseline resides in Domain.EventSourcing/TradePlan. It owns quiescence admission, baseline event construction/verification and controlled exit-stage reconstruction. Its stage read/lock uses ITradePlanDbReadContext.ReadExitWorkflowBaselineAsync; SQL and ordinal mapping remain in storage. This is explicit recovery of recorded exit-stage business facts, not replay of disposable monitoring plans. ExitWorkflowProjectionFacts in Domain.Trade.Shared owns the accepted stage consistency checks used by both persistence and baseline capture.

Regression checks use disposable infrastructure, covering source/projection atomic commit and rollback, scalar-column round trips, conflicting revisions, one-attempt failure handling, standalone snapshot log/drop behavior, ordered scoped history, exit-stage persistence and verified baseline reconstruction into an isolated database. Architecture checks enforce the nine-file structure, typed public operations, mapper ownership and receiver helper layout.

Verification: all 25 TradePlanDb storage/convention regressions and all 12 trade-flow qualification checks passed. The API host services and offline exit-workflow baseline utility compiled with zero warnings and errors. Semantic verification found zero errors, unused imports or malformed XML documentation. Tests used disposable infrastructure and isolated restore targets; no Development database data was changed.

## TradeTickQuoteDb convention alignment

TradeTickQuoteDb remains the ScyllaDB owner of raw Databento trade/quote observations and futures/option ticks. This provider exception keeps CQL query and schema catalogs; it does not introduce PostgreSQL dual writes or business event-log appends.

Its persistence folder contains nine C# files:

- TradeTickQuoteDbContext.cs: non-partial public typed operations and ordinal result mappings.
- ITradeTickQuoteDbContext.cs, ITradeTickQuoteDbReadContext.cs and ITradeTickQuoteDbWriteContext.cs: documented repository/read/write contracts, retaining existing parameter names and defaults.
- TradeTickQuoteDbContextExtensions.cs: C# 14 receiver helpers for ingestion binding, validation and partition-scoped technical batching. The unchanged Scylla prepared-value adapter is a file-scoped internal type alongside these helpers.
- TradeTickQuoteDbParameters.cs: all eight binding declarations in one file, retaining their names, namespaces and ordinal values.
- TradeTickQuoteDbCql.cs: the unchanged provider queries and mutations.
- Schema/TradeTickQuoteSchemaCql.cs: the sole Scylla DDL catalog and ordered schema definitions, including the quote UDT before the quote table and the administrative drop statements.
- Schema/TradeTickQuoteSchemaDb.cs: executes that dependency-ordered catalog through SchemaDbContext.

The former Parameters folder and separate raw trade/quote binding and prepared-adapter files are consolidated. The six insert overload bodies move to receiver helpers without changing write behavior. Futures tick batches still group by contract/value date and contain at most 24 rows, writing both the identity and time indexes with the existing CQL batch behavior; Scylla batching is not a PostgreSQL transaction boundary. Option price/Greek columns remain double. Table identities, partition keys, CQL text, source timestamp precision, MessagePack event contracts and query result ordinals remain unchanged.

Provider ownership exception to binding conventions: InsertTickQuoteData remains a single-use reference object implementing IScyllaOwnedBindValues. It must not become a copyable struct: it transfers its parameter array once and retains the encoded quote value until the executor releases it on success or failure. TickQuoteScyllaBindValue owns only provider encoding/metadata checks and buffer lifetime; its prepared-metadata cache is technical provider state. Quote encoding and pooling remain in Domain.MarketData.Feed.Shared. These internal resource owners may be file-scoped beside the bindings/helpers; they do not coordinate business workflows, schedule work or append events.

Regression coverage uses a disposable CQL destination with separate PostgreSQL settings and no Development database access. It covers raw ownership routing, 55 futures ticks across batch boundaries, identity/time index consistency and deletion, option tick values/Greeks, raw trade identifier/decimal/uint/nanosecond precision, and every UDT field in one- and 512-quote segments with nullable prices. Additional checks guard single-use parameter transfer, pooled buffer reuse/size, CQL marker order, the nine-file layout, typed public methods and receiver helpers.

Verification: all 15 targeted TradeTickQuoteDb routing, raw trade/quote, architecture, binding-order and buffer ownership regressions passed. Storage and Api.Server.Core compiled with zero warnings and errors. Final semantic verification found zero errors, unused imports or malformed XML documentation. The test infrastructure was disposable; Development data and running services were not changed.
