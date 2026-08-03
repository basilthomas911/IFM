# Application Storage — Implementation Details

## Purpose

`TomasAI.IFM.Application.Storage` is the application-layer persistence catalog for IFM. It adapts domain read models and commands to the provider-neutral repository primitives in `TomasAI.IFM.Framework.Storage` and exposes:

- database contexts grouped by business domain;
- read/write context interfaces;
- event-stream persistence and replay;
- command and projector checkpoint persistence;
- SQL/CQL command and parameter catalogs;
- ordered database-schema creation and removal;
- external data-reader contexts for economic calendars and yield curves; and
- a DI-backed context factory, resolver, and limited context pool.

The project does not select a database vendor directly. Each context receives a named `IDbConnectionSetting`; its `ProviderName` and credential-free connection string determine which framework provider creates connections, commands, parameters, readers, and bulk-copy operations. Result materialization is explicit ordinal mapping through `IObjectDataRecord`; application contexts do not register reflection-based result maps.

## Complete project folder map

The tree below records every directory currently present beneath the project root, from root to each leaf. `bin` and `obj` are generated trees whose framework, locale, and runtime branches can change after restore, build, or publish.

```text
TomasAI.IFM.Application.Storage/                    Project root
├── Docs/                                           Maintained project documentation
├── EconomicCalendarsDb/                            External economic-calendar reader
├── EventSourceDb/                                  Active event-source persistence
│   └── Schema/                                     Event-source SQL schema
├── FundDb/                                         Fund persistence
│   └── Schema/                                     Fund CQL schema
├── LogDb/                                          Telemetry-log persistence
│   └── Schema/                                     Log SQL schema
├── MarketDataDb/                                   Market data and analytics persistence
│   └── Schema/                                     Market-data CQL schema
├── OptionPricerDb/                                 Option-pricer persistence
│   └── Schema/                                     Option-pricer CQL schema
├── PredictiveModelDb/                              Predictive-model persistence shell
│   └── Schema/                                     Predictive-model CQL schema
├── ReferenceDb/                                    Reference and scheduling persistence
│   └── Schema/                                     Reference CQL schema
├── Schema/                                         Shared schema abstractions
├── SecuritiesDb/                                   Futures security-master persistence
│   └── Schema/                                     Securities CQL schema
├── SequenceIdDb/                                   PostgreSQL-style sequence access
│   └── Schema/                                     Sequence functions and sequences
├── TradeDb/                                        Trade and trade-plan persistence
│   └── Schema/                                     Trade CQL schema
├── TradePlanDb/                                    Legacy trade-plan source (excluded from build)
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
| `EconomicCalendarsDb/` | Active source leaf | Maps externally read economic-calendar JSON records and converts valid records to reference-domain read models. |
| `EventSourceDb/` | Active | Event stream/name/log persistence, bounded-context and actor variants, serialization/replay, command logs, and projector state/results. |
| `EventSourceDb/Schema/` | Active source leaf | Defines event sequences/tables and ordered create/drop operations. |
| `FundDb/` | Active | Fund, order, trade, transaction, balance, P&L, drawdown, bulk insert, update/delete, and backup operations. |
| `FundDb/Schema/` | Active source leaf | Defines fund, fund-order, fund-order-trade, and fund-transaction tables. |
| `LogDb/` | Active | Inserts telemetry logs and queries them by date range. |
| `LogDb/Schema/` | Active source leaf | Defines the telemetry-log table. |
| `MarketDataDb/` | Active | Broad futures tick/bar/EOD/option, volatility, analytics signal/model, yield curve, holiday, normal-curve, quote, ID, and live-feed persistence. |
| `MarketDataDb/Schema/` | Active source leaf | Defines 25 tables plus the RSI signal-type index in creation order. |
| `OptionPricerDb/` | Active | Option-pricer devices, spread distributions, distribution jobs, status transitions, and domain-specific exception/parameter definitions. |
| `OptionPricerDb/Schema/` | Active source leaf | Defines device, distribution-job, and spread-distribution tables. |
| `PredictiveModelDb/` | Active shell | Exposes a provider-backed repository and empty read/write marker contracts; current runtime methods are not implemented here. |
| `PredictiveModelDb/Schema/` | Active source leaf | Defines ITI trend class/delta data and model tables plus request IDs. |
| `ReferenceDb/` | Active | Lookup types, seed IDs, scheduled jobs, economic calendars, country codes, and MDI forward-loss-ratio persistence. |
| `ReferenceDb/Schema/` | Active source leaf | Defines economic-calendar, lookup, forward-loss, scheduled-job/day, and seed tables. |
| `Schema/` | Active source leaf | Shared `IDbSchemaContext`, `SchemaDbContext<T>`, and immutable schema object definition. |
| `SecuritiesDb/` | Active | Futures and futures-option contract queries, insert/update/delete operations, and currently-traded contract selection. |
| `SecuritiesDb/Schema/` | Active source leaf | Defines futures and futures-option contract tables. |
| `SequenceIdDb/` | Active | Obtains the next named sequence ID through checked-in SQL. |
| `SequenceIdDb/Schema/` | Active source leaf | Defines current/next sequence functions and one database sequence per `SequenceName`. |
| `TradeDb/` | Active | Option trades/legs/spreads, positions/states, fills, limits, orders, trade plans, forward-loss values, live feed, and placement signals. |
| `TradeDb/Schema/` | Active source leaf | Defines 17 trade-related tables. |
| `TradePlanDb/` | Excluded source leaf | Older standalone trade-plan context/contracts; the project file removes the entire folder from compilation. |
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

Most active database folders follow this pattern:

| File kind | Purpose |
| --- | --- |
| `I<Domain>DbContext.cs` | Combined repository contract, often extending the framework's generic repository plus read/write interfaces. |
| `I<Domain>DbReadContext.cs` | Query contract returning domain read models or scalars. |
| `I<Domain>DbWriteContext.cs` | Insert, update, delete, bulk, or backup contract. |
| `<Domain>DbContext.cs` | Model mappings and operation implementation using `ObjectDataRepository<T>`. |
| `<Domain>DbCql.cs` or `<Domain>DbSql.cs` | Checked-in provider command text. |
| `<Domain>DbParameters.cs` | Parameter records/classes bound to command text. |
| `<Domain>DbException.cs` | Domain-specific storage exception where present. |
| `Schema/<Domain>SchemaDb.cs` | Ordered schema-object catalog and connection selection. |
| `Schema/<Domain>SchemaCql.cs` or `...Sql.cs` | Checked-in create statements. |

Not every domain uses every file kind. Economic Calendars and Yield Curve Rates are read-only data-reader adapters; Predictive Model is currently a context/schema shell; Log and Sequence ID expose focused contracts instead of read/write splits.

## Project definition and dependencies

The project uses `Microsoft.NET.Sdk`, targets `net10.0`, and enables nullable reference types and implicit usings. It has no direct NuGet package references; functionality arrives through project references:

- Application Blackboard for event-source lookup/checkpoint caching;
- Framework Storage for generic repositories, mapping, execution, providers, transactions, readers, and bulk copy;
- Framework Sequence ID for sequence contracts and names;
- Shared for storage settings, event sourcing, primitives, and cross-domain contracts; and
- shared domain projects for Fund, Market Data, Analytics, Feed, Option Pricer, Predictive Model, Reference, System Admin, and Trade read models/contracts.

Internals are visible to `TomasAI.IFM.Application.Storage.IntegrationTests`.

The project explicitly removes `TradePlanDb/**` and a currently absent `SqlServer/**` path from Compile, EmbeddedResource, and None items. `TradePlanDb` remains in the repository for future work but does not appear in the built assembly. The obsolete `EventDb` implementation and its excluded SQL Server integration test were removed; `EventSourceDb` is the active event-source implementation.

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
- generic Predictive Model and Economic Calendar repositories;
- ten schema contexts; and
- a `ReferencePool` plus generic `Get<TRepo>()` internally used by pools.

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
| Event source | `EventSourceDbConnection` |
| Actor event source | `EventSourceActorDbConnection` |
| Fund | `FundDbConnection` |
| Log | `LogDbConnection` |
| Market data | `MarketDataDbConnection` |
| Option pricer | `OptionPricerDbConnection` |
| Predictive model | `PredictiveModelDbConnection` |
| Reference | `ReferenceDbConnection` |
| Securities | `SecuritiesDbConnection` |
| Sequence ID | `SequenceIdDbConnection` |
| Trade | `TradeDbConnection` |
| Economic calendar external reader | `EconomicCalendarsDbConnection` |
| Yield curve external reader | `YieldCurveRatesDbConnection` |

Schema contexts reuse their corresponding runtime context setting. `EventSourceSchemaDb` uses `EventSourceDbConnection`; it does not use the actor-specific connection key.

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
| `EventSourceDbContext` | Saves and loads bounded-context streams, assigns stream/event-name identities, deletes logs, supports last-N and snapshot replay, and invokes a denormalizer callback during saves. |
| `EventSourceActorDbContext` | Actor-oriented stream save/load/map-reduce, command log status, event projector state/result persistence, incomplete-projection recovery queries, and event identity caching. |
| `FundDbContext` | Full fund/order/trade/transaction CRUD and bulk operations plus balances, P&L, drawdown reporting, state/status changes, and database backup. |
| `LogDbContext` | Telemetry batch insert and date-range reads. |
| `MarketDataDbContext` | The largest context: market ticks/bars/EOD, option ticks/quotes, analytics indicators/signals/models, VIX, yield curves, holidays, normal curves, IDs, and trade live feed. |
| `OptionPricerDbContext` | Device registration and spread-distribution job/data lifecycle. |
| `PredictiveModelDbContext` | Provider-backed context with read/write marker interfaces but no declared runtime methods; its schema remains managed. |
| `ReferenceDbContext` | Lookup/seed values, scheduled jobs, economic calendars, country codes, and MDI forward-loss ratios. |
| `SecuritiesDbContext` | Futures and futures-option contract master data and currently traded contract queries. |
| `SequenceIdDbContext` | Executes `fn_get_next_sequence_id` for a named `SequenceName`. |
| `TradeDbContext` | Broad option trade, position, fill, limit, order, trade-plan, spread, loss-control, live-feed, and placement-signal persistence. |
| `EconomicCalendarsDbContext` | Reads JSON through the configured data-reader provider; converts valid rows and silently skips row-conversion failures. On an outer failure it returns an empty collection. |
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

Each `SchemaObjectDefinition` contains a stable name, create statement, and drop statement. The schema layer does not record migration versions; it executes the checked-in definitions against an already configured database/keyspace.

| Schema context | Managed objects |
| --- | --- |
| Event Source | 3 sequences and 5 tables: stream IDs, event names, event log, command log, and projector state. |
| Fund | 4 tables for funds, orders, order trades, and transactions. |
| Log | 1 telemetry-log table. |
| Market Data | 25 tables plus 1 index covering live feed, futures/option data, analytics, curves, holidays, and quotes. |
| Option Pricer | 3 tables for devices, jobs, and distributions. |
| Predictive Model | 5 tables for ITI trend data/models and request IDs. |
| Reference | 6 tables for calendars, lookups, forward-loss ratios, scheduled jobs/days, and seed IDs. |
| Securities | 2 contract tables. |
| Sequence ID | 2 functions plus a generated sequence definition for every `SequenceName`. |
| Trade | 17 option/trade/position/order/plan/signal tables. |

## Context pooling

`DbContextPool<TRepo>` uses one static `ConcurrentQueue<IObjectRepository<TRepo>>` per closed generic repository type:

1. dequeue a repository if available;
2. otherwise resolve one through `IDbContextFactory.Get<TRepo>()`;
3. invoke the supplied operation; and
4. return the repository to the queue in `finally`.

Only `ExecuteAsync` and the reference-type `GetAsync<TResult>` overload are implemented. Collection-returning `GetAsync` and value-type `GetScalarAsync` throw `NotImplementedException`. The factory currently exposes only `ReferencePool` publicly.

## Bulk, mapping, and read/write conventions

- Domain contexts declare static mapper methods that read `IObjectDataRecord` by zero-based ordinal and construct shared read models directly.
- Every SQL/CQL projection must remain in exactly the order expected by its mapper. Alias/name matching is not performed on the result hot path.
- Stream, query, single, scalar, immutable, and map/reduce calls receive the mapper explicitly; there is no `OnCreateModel`, result-map registry, property assignment, or reflection-based result construction.
- `ExecuteStreamAsync` is an additive, cold `IAsyncEnumerable<T>` API for large results. Existing methods remain available; an active stream must be fully enumerated or disposed because it owns its database reader/row set until then.
- All ScyllaDB parameter catalogs—Fund, Market Data, Option Pricer, Reference, Securities, Trade, and the compiled domain-local bind values—emit positional `object?[]` values through `IBindValue` in prepared-statement marker order. Live Scylla context call sites do not use anonymous parameter objects, and the provider has no reflection fallback or bind-property cache.
- PostgreSQL Event Source, Log, and Sequence ID catalogs emit strongly typed, unnamed `NpgsqlParameter<T>` arrays through `IBindValue`, ordered exactly like native `$n` SQL placeholders. The PostgreSQL provider no longer discovers properties, calls `PropertyInfo.GetValue`, uses a reflection/type cache, or clones generated parameters before command execution.
- Parameterized PostgreSQL text commands are explicitly prepared and persist on pooled physical connections. Queued PostgreSQL commands execute through one `NpgsqlBatch` round trip inside an explicit transaction; failures retain all-or-nothing rollback behavior.
- Single-record and batch Scylla writes execute checked-in CQL with the same positional contract. Enumerable `SetParameters` calls invoke each element's `IBindValue.Bind()`, avoiding per-item anonymous-object allocation and provider reflection.
- Database-independent tests verify all 236 non-Fund catalog bindings against their CQL marker sequence; dedicated Fund tests verify its 28 bindings, nullable values, update-marker order, and `DateOnly` values for CQL `date` columns.
- Several Fund, Market Data, and Trade APIs accept `IEnumerable<T>` and return inserted row counts for bulk operations.
- Combined contexts commonly expose `DbReader => this` and `DbWriter => this`, giving consumers capability-oriented interfaces over one repository object.
- Query methods generally return nullable single records and non-null collections.
- Command text and schema are source-controlled separately, allowing operation changes without embedding large query strings in context methods.

## Operational characteristics and current limitations

- **Resolution assumes correct DI registration.** `DbContextResolver` and several factory casts suppress nullability; a missing or incompatible registration becomes a later null-reference failure rather than a descriptive resolution exception.
- **Factory pool-map access is not synchronized.** Concurrent first access to the same pool type can race around the ordinary `Dictionary<Type, object>`.
- **The pool is incomplete.** Two result overloads throw `NotImplementedException`, and only the Reference pool is exposed.
- **The pool is unbounded and does not dispose entries.** Returned repositories remain in a static queue for the process lifetime.
- **A null pool callback can fail at runtime.** `ExecuteAsync` accepts a nullable delegate but awaits its null-forgiven invocation.
- **Repository thread safety depends on host lifetime and use.** Factory properties resolve on every access, while provider repositories contain mutable command/context state; hosts should avoid unsafe singleton sharing unless the framework registration is designed for it.
- **Schema management is not versioned migration.** Create/drop catalogs have no history, checksum, upgrade ordering across releases, or rollback metadata.
- **Create/drop is not wrapped here in a cross-object transaction.** Partial schema state is possible after a failure.
- **Actor and standard event sources can use different connections.** The shared schema manager targets only the standard event-source connection, so host configuration must intentionally align or separately provision actor storage.
- **Excluded source can appear active to readers.** `TradePlanDb` contains complete-looking code but is removed from all project item types.
- **External reader failure policies differ.** Economic Calendars converts/skips rows and returns empty on outer failure; Yield Curve Rates lets failures propagate.
- **Some contexts are very broad.** `MarketDataDbContext` and `TradeDbContext` combine many aggregates and analytics tables, increasing change and regression scope.
- **Provider syntax is mixed by design.** SQL and CQL catalogs coexist; connection provider settings must match the command/schema syntax selected by each context.
- **Ordinal projections are intentionally strict.** A reordered or inserted selected column can silently map the wrong value when compatible types are involved; projection and mapper changes must be reviewed and tested together.
- **Database credentials are runtime requirements.** PostgreSQL and ScyllaDB fail fast when their selected environment variable is absent, malformed, conflicts with the application environment, or when a base string still embeds credentials.
- **Legacy build artifacts remain.** `net8.0` output/intermediate folders are not part of the current `net10.0` target.

## Verification locations

Storage behavior is validated primarily outside this project:

- `TomasAI.IFM.Application.Storage.IntegrationTests` exercises Event Source, Fund, Log, Market Data, Option Pricer, Predictive Model, Reference, Securities, and Trade contexts.
- Its `FrameworkStorage/ScyllaDb` suite contains 17 real-provider tests across all four Fund tables. It covers every `IObjectRepositoryProvider` method, both Scylla queued-command modes, ordinal Fund types, argument guards, disposable pooled immutable results, async streaming, early disposal, and cancellation.
- Its `FrameworkStorage/Postgres` suite contains 19 real-provider tests across all five event-source tables. It covers the same provider API surface, ordinal PostgreSQL types, argument guards, async streaming lifecycle, server-side prepared-statement registration, single-round-trip queued batches, and rollback when a later queued command fails.
- Both provider suites disable collection parallelism, reserve deterministic negative identifiers/names, clean before and after every test, verify cleanup, and avoid production databases. They are selected with `Category=ScyllaDBIntegration` or `Category=PostgresIntegration`.
- `TomasAI.IFM.Application.Storage.LoadTests` covers storage load scenarios.
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
