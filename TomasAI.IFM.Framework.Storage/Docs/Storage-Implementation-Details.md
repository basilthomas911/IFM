# Storage Implementation Details

## Purpose and Scope

`TomasAI.IFM.Framework.Storage` is the provider-neutral persistence and structured-data layer for IFM. It targets .NET 10 and supplies:

- repository, command, query, transaction, and result-mapping abstractions;
- SQL Server, PostgreSQL, and ScyllaDB provider implementations;
- ADO.NET/Cassandra record adapters and explicit ordinal mapper delegates;
- CSV and JSON `IDataReader` implementations;
- local-file and HTTP string readers;
- SQL Server bulk copy; and
- pooled immutable result buffers.

The project references `TomasAI.IFM.Shared` and `TomasAI.IFM.Domain.Trade.Shared`. Its database packages are CassandraCSharpDriver 3.22.0, Microsoft.Data.SqlClient 7.0.2, and Npgsql 10.0.3. Internal members are visible to the unit-test assembly.

## Root-to-Leaf Directory Inventory

Every current directory leaf is listed below relative to the project root. A leaf path includes every intermediate parent directory. `bin/` and `obj/` are generated SDK/package trees; their contents can change after a package or SDK update.

- `Docs/`
- `bin/Debug/net10.0/de/`
- `bin/Debug/net10.0/es/`
- `bin/Debug/net10.0/fr/`
- `bin/Debug/net10.0/it/`
- `bin/Debug/net10.0/ja/`
- `bin/Debug/net10.0/ko/`
- `bin/Debug/net10.0/pt-BR/`
- `bin/Debug/net10.0/ru/`
- `bin/Debug/net10.0/runtimes/unix/lib/net8.0/`
- `bin/Debug/net10.0/runtimes/win/lib/net8.0/`
- `bin/Debug/net10.0/runtimes/win/lib/netcoreapp2.0/`
- `bin/Debug/net10.0/runtimes/win-arm/native/`
- `bin/Debug/net10.0/runtimes/win-arm64/native/`
- `bin/Debug/net10.0/runtimes/win-x64/native/`
- `bin/Debug/net10.0/runtimes/win-x86/native/`
- `bin/Debug/net10.0/zh-Hans/`
- `bin/Debug/net10.0/zh-Hant/`
- `bin/Debug/net8.0/`
- `bin/Release/net10.0/`
- `Csv/`
- `Extensions/`
- `Json/`
- `obj/Debug/net10.0/ref/`
- `obj/Debug/net10.0/refint/`
- `obj/Debug/net8.0/ref/`
- `obj/Debug/net8.0/refint/`
- `obj/Release/net10.0/ref/`
- `obj/Release/net10.0/refint/`
- `Postgres/`
- `Properties/`
- `ScyllaDb/`
- `SqlServer/`

## Source Folder Responsibilities

| Folder | Responsibility |
| --- | --- |
| Project root | Provider-neutral contracts, repositories, contexts, mapping, URI readers, record conversion, pooling, and provider factories. |
| `Csv/` | CSV `IDataReader`, object-reader adapter, and reflection-based writer. |
| `Extensions/` | Typed conversion helpers for `object[]` and `ReadOnlySpan<object>`. |
| `Json/` | Newtonsoft.Json-backed `IDataReader` and object-reader adapter. |
| `Postgres/` | Npgsql connection, parameter, transaction, reader, and repository provider. |
| `Properties/` | Local launch profile metadata. |
| `ScyllaDb/` | Cassandra-driver connection/provider, row mappers, result materialization, queued commands, and Noda-style date/time serializers. |
| `SqlServer/` | Microsoft.Data.SqlClient connection, parameter, transaction, reader, and repository provider. |
| `Docs/` | Maintained implementation documentation. |

## Repository Execution Model

The principal flow is:

```text
ObjectDataRepository<TRepo>.Use(...)
  -> DbProvider creates a command/data/URI context
  -> ObjectDataRepositoryContext selects a provider by ProviderName
  -> SQL Server, PostgreSQL, or ScyllaDB provider executes
  -> caller-supplied Func<IObjectDataRecord, TResult> maps each row by ordinal
```

`ObjectDataRepository<TRepo>` is the base for application repositories. At construction it reads an `IDbConnectionSetting`, stores the credential-free connection string and provider name, and creates an `ObjectDataDbProvider`. Consumers can then:

- select stored procedures by enum or expression;
- select raw command text;
- attach one object, an `IBindValue` struct, or an enumerable of parameter values;
- execute commands, materialized queries, cancellable async streams, scalar reads, single-result reads, immutable result reads, or map/reduce operations;
- queue homogeneous text or stored-procedure commands;
- begin a provider transaction; or
- open a configured file URI context.

`ObjectDataStoredProcedureContext` and `ObjectDataCommandTextContext` define command text, command type, and parameter-name conventions. The abstract `ObjectDataRepositoryContext` delegates execution to the selected `IObjectRepositoryProvider` and owns the mutable parameter list. Contexts implement `IDisposable`; callers should scope them tightly.

Repository operations do not expose or acquire a framework-wide synchronization gate. Concurrency is delegated to the database providers and their connection pools.

Removing that gate also removes the former `IObjectRepository.LockAsync()` and `Unlock()` members. This is an
intentional source/binary breaking contract change: consumers must not treat the upgrade as API-compatible or replace
the removed calls with no-op synchronization. Workflows that require serialization must use a database-backed
ownership/transaction protocol appropriate to their consistency boundary.

## Provider Selection and Behavior

Provider creation is string-based:

- `System.Data.Postgres` selects Npgsql implementations.
- `System.Data.ScyllaDb` selects Cassandra-driver/ScyllaDB implementations in the connection and provider factories.
- other values fall back to SQL Server.

SQL Server and PostgreSQL providers implement command execution, queued commands, query and single-result mapping, scalar reads, immutable struct results, map/reduce, and transactions. The SQL Server bulk-copy context maps source columns by name and writes a `DataTable` through `SqlBulkCopy`.

ScyllaDB uses prepared/bound statements, Cassandra `RowSet` results, typed row accessors, and custom `DateOnly`, `DateTime`, and `TimeOnly` serializers. Cassandra/Scylla transactions are not implemented.

Every provider implements the same `IObjectRepositoryProvider` surface:

| Contract method | Behavior |
| --- | --- |
| `ExecuteCommandAsync` | Executes command text for zero, one, or multiple parameter objects. |
| `QueueCommand` / `ExecuteQueuedCommandsAsync` | Creates provider-specific queued commands and executes the complete queue. |
| `StreamObjectsAsync` | Returns a cancellable `IAsyncEnumerable<TResult>` and owns provider resources for the enumerator lifetime. |
| `GetObjectsAsync` | Materializes mutable results through an ordinal mapper. |
| `GetImmutableObjectsAsync` | Materializes value-type results through an ordinal mapper; PostgreSQL uses a pooled temporary builder and returns an owned array, while ScyllaDB retains its existing disposable pooled-buffer contract. |
| `GetObjectAsync` | Maps the first row, or returns the default/null result when no row exists. |
| `GetScalarAsync` | Maps a scalar/value result through the same record contract. |
| `ExecuteMapReduceAsync` | Lazily maps rows and invokes the reducer while the provider result is available. |

### PostgreSQL and ScyllaDB credentials

PostgreSQL and ScyllaDB base connection strings must contain endpoint/database settings only. Connection creation resolves credentials from environment variables and rejects base strings containing `Username`, `User ID`, or `Password` values.

`DatabaseCredentialResolver` selects the credential key from `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT`. The default is Development; if both variables are present they must resolve to the same supported environment. Credentials use case-insensitive JSON properties in the form `{"userid":"...","password":"..."}`.

PostgreSQL injects the resolved values with `NpgsqlConnectionStringBuilder` while lazily constructing a cached `NpgsqlDataSource`; subsequent operations create logical pooled connections from that data source. ScyllaDB parses the credential-free connection string with `CassandraConnectionStringBuilder` and applies the values through `Cluster.Builder().WithCredentials(...)`. Parsed credentials are cached by environment-variable name and JSON value, and validation messages do not include the secret. See [`docs/database-credentials.md`](../../docs/database-credentials.md) for the complete key matrix and deployment examples.

The PostgreSQL data source and ScyllaDB cluster retain the credential snapshot with which their process-lifetime pool
was created. Credential rotation therefore requires an orderly application restart; changing only the environment
variable does not replace an already-cached pool.

This environment credential path is limited to PostgreSQL and ScyllaDB; the retained SQL Server provider's connection configuration is unchanged.

Provider-name handling is not fully consistent: parameter and transaction factories check `System.Data.Cassandra` or `System.Data.Scylla`, while the connection/provider factories check `System.Data.ScyllaDb`. New configuration must be tested end to end; centralizing these identifiers would prevent a Scylla configuration from accidentally taking a SQL Server fallback path.

## Mapping and Record Access

Database result mapping is ordinal-only. Stream, query, single, scalar, immutable, map/reduce, data-reader, and file-reader APIs require a `Func<IObjectDataRecord, TResult>` supplied by the caller. The former result-type map, expression/property map, constructor map, and reflection-based object construction path has been removed.

Application contexts normally keep static mapper methods close to their SQL/CQL. A mapper constructs the result directly with `GetInt(0)`, `GetString(1)`, and the other typed ordinal accessors. Consequently, the selected column order is a compiled persistence contract: changing a projection requires changing its mapper in the same commit and exercising it through integration tests.

`AdoNetDataRecord` adapts SQL Server/PostgreSQL `IDataReader` instances and `ScyllaDbDataRecord` adapts Cassandra rows. Both expose the same `IObjectDataRecord` typed API for numeric primitives, booleans, strings, GUIDs, byte arrays, enums, and date/time values. The hot result path no longer performs property lookup, reflective construction, or intermediate result-object arrays.

ScyllaDB accepts positional `object?[]` bind values and passes them directly to `PreparedStatement.Bind`; the provider contains no property discovery, name lookup, `PropertyInfo.GetValue`, or bind-property cache. Scylla application and compiled domain parameter catalogs implement `IBindValue` and emit values in prepared-statement marker order. Enumerable `SetParameters` values are retained as a deferred, single-pass parameter source and bound as the Scylla write pipeline consumes them. Indexed struct sources are also bound by the single bounded producer because `IBindValue.Bind()` has no cross-thread safety contract; database submission remains concurrent. Providers that require random access materialize the source through the existing `ParameterValues` compatibility property.

Ordinary multi-value Scylla commands do not use CQL batches. The provider asynchronously prepares the CQL once and sends bound statements through a fixed worker set. A bounded channel limits the live parameter arrays for deferred inputs, so peak client memory is controlled by `SCYLLADB_BULK_BOUNDED_CAPACITY` instead of total input size. `SCYLLADB_BULK_MAX_CONCURRENCY` controls the worker count within each call. Defaults are 64 buffered values and 32 workers; invalid values fail when the provider is created. No repository or provider-wide semaphore is used, so simultaneous calls may each schedule up to the configured worker count. There is no application-created task per row, `GetRange` copy, large logged batch, or whole-collection application retry. Prepared bound statements retain Cassandra-driver token-aware routing.

The shared Scylla session defaults ordinary reads and writes to `LOCAL_QUORUM`, with `LOCAL_SERIAL` for the serial phase of LWT statements. This is required by the projection marker/generation protocols: pre/post checks must not observe a marker on one replica and stale readiness on another. It trades some latency and availability for replica-safe decisions; keyspaces must use a replication factor capable of satisfying a local quorum.

`ExecuteCommandAsync(CancellationToken)` is an additive overload. Cancellation stops parameter production, prevents additional statements from being scheduled, and stops the caller waiting for in-flight driver tasks. Cassandra driver 3.x does not expose request cancellation, so statements already sent to ScyllaDB may still complete. The provider observes each abandoned driver task and disposes its eventual result; a cancelled streaming page fetch transfers its existing row-set ownership to that drain. Ordinary bounded bulk writes can therefore partially succeed on cancellation or failure; callers requiring a small atomic mutation group must use an explicit logged queued command.

Queued commands carry command-local type, provider, and opaque base-connection identity metadata. This removes repository-global queue bookkeeping and rejects accidental execution of a queue against another database or Scylla keyspace. Legacy manually constructed public command objects remain accepted without an identity token for compatibility. Queued Scylla commands retain explicit semantics: `useTransaction: false` executes in queue order, while `useTransaction: true` creates a logged atomic batch. The logged path includes every bind payload from a queued command, omits serial consistency for ordinary non-LWT CQL, and warns above 50 statements. Logged batches are an atomicity tool rather than the ordinary bulk-throughput path.

PostgreSQL accepts only `NpgsqlParameter[]` bind payloads ordered like the SQL command's native `$1`, `$2`, ... placeholders. `PostgresParameter` creates unnamed `NpgsqlParameter<T>` instances with an explicit `NpgsqlDbType`; this avoids runtime property discovery, `PropertyInfo.GetValue`, the old CLR-type dictionary, value-type boxing inside non-generic parameters, and the former second parameter-copy pass. Single parameterized text commands are explicitly prepared. Multi-value commands consume deferred values once and send bounded `NpgsqlBatch` chunks; `POSTGRES_BULK_BATCH_SIZE` defaults to 256 and accepts values from 1 through 4,096. This bounds client memory without returning to one network round trip per row. Parameterless commands and stored procedures are not prepared, limiting unnecessary server-side plan growth.

PostgreSQL queued commands are packed into one `NpgsqlBatch`. When `useTransaction` is true, the provider uses an explicit transaction and rolls back the batch on failure; when false, it avoids the extra transaction and lock lifetime. Eligible parameterized text batches are prepared before execution. SQL Server continues to bind provider-specific parameter objects by name. CSV/JSON readers inspect their source type to define a tabular schema, and the CSV writer reflects source properties; those paths are separate from database result and PostgreSQL/ScyllaDB parameter mapping.

The `TomasAI.IFM.Framework.Storage.Benchmarks` BenchmarkDotNet project retains a benchmark-only copy of the removed cached-reflection algorithm for comparison. Live ScyllaDB and PostgreSQL bulk-write comparisons measure 100 and 1,000 rows over one and 32 partitions against disjoint reserved test partitions. After a run they write provider-specific Markdown comparisons with individual and aggregate latency, rows/second, allocated-byte, and GC changes. See the benchmark project README and [the top-ten performance review](Storage-Performance-Top-10.md) for the credential, cleanup, and interpretation contracts.

`PooledBufferBuilder<T>` rents memory from `MemoryPool<T>`. ScyllaDB transfers that ownership to `PooledReadOnlyBuffer<T>`, which must be disposed and rejects access afterward. PostgreSQL copies the populated span to one exact caller-owned array and returns the temporary owner before completing the query.

### Asynchronous database streaming

`IObjectRepositoryContext.ExecuteStreamAsync` is additive; existing collection, immutable, single, scalar, and map/reduce APIs are unchanged. The returned stream is cold: the provider opens its resources when enumeration begins and disposes them when enumeration completes, throws, is cancelled, or its enumerator is disposed after an early `break`.

PostgreSQL and SQL Server advance their data readers with `ReadAsync(CancellationToken)`. ScyllaDB disables driver auto-paging, consumes the available page, and awaits `FetchMoreResultsAsync` before consuming the next page. Cassandra driver operations do not accept `CancellationToken`, so ScyllaDB uses `Task.WaitAsync(cancellationToken)` and checks cancellation between mapped rows; cancellation stops the consumer wait and disposes the row set, although an already-issued driver operation may finish in the background.

Callers should consume streams with `await foreach` or explicitly dispose an acquired async enumerator. Do not retain `IObjectDataRecord`; the mapper must return an independent result value/object for the current row.

## CSV, JSON, and URI Sources

`IStringReader` abstracts whole-content and asynchronous line reads:

- `FileStringReader` requires a file URI and uses `File.ReadAllTextAsync` or `File.ReadLinesAsync`.
- `HttpStringReader` requires HTTP/HTTPS when it fetches content, creates an `HttpClient` per operation, and can expose split lines as an async stream.

`CsvDataReader<TData>` reflects public properties to define its schema, treats the first input line as a header by default, builds a case-insensitive header index, and converts cells to common primitive/nullable types. `CsvWriter` reflects public properties into a header and rows.

`JsonDataReader<TData>` uses Newtonsoft.Json to deserialize a JSON array, then exposes reflected property values through `IDataReader`. `ObjectDataReaderContext` and `ObjectFileUriContext` adapt either reader to `IObjectDataRecord` and invoke the caller's ordinal mapper.

Current parser constraints are important:

- CSV parsing uses `string.Split(',')`; quoted commas, embedded newlines, and standard CSV escaping are not supported.
- The writer surrounds string and date/time values with single quotes and does not escape delimiters or quotes.
- Several optional `IDataReader` members throw `NotImplementedException`.
- The CSV reader initializes its cursor to zero and pre-increments in `Read`, which skips the first stored data row.
- File-context record mapping catches mapper exceptions, writes them to the console, and continues.
- `ObjectDataRepository.Use(Uri)` accepts only file URIs; `CreateHttpUriContext` is currently a placeholder.
- The JSON branch of `ObjectFileUriContext` constructs an `HttpStringReader` from its file URI, so that path cannot currently read local JSON successfully.

## Transactions, Commands, and Failure Semantics

SQL Server and PostgreSQL transaction wrappers keep a shared connection/transaction command available through `InTransaction`. `Commit` and `Rollback` complete the transaction and clear repository transaction state. Callers should always roll back in a `finally` block when commit does not complete.

Queued commands must all use the same command type. Mixing stored procedures and text causes `CreateQueuedCommandsContext` to reject the batch.

Most provider errors are logged and/or wrapped according to the concrete provider. Many APIs do not accept `CancellationToken`, so task cancellation must currently be enforced outside this layer. Raw SQL passed to `Use(string)` is not parameterized automatically; use parameters for untrusted values.

## Extension Guidance

To add a provider, implement connection, parameter, transaction, reader, and `IObjectRepositoryProvider` adapters, then update every provider factory consistently. Add unit tests for routing and conversions plus opt-in integration tests for commands, queries, transactions, and cleanup.

To add a result model, add a static `IObjectDataRecord` mapper beside the repository operation and pass it explicitly to the execution method. Keep every selected column and typed ordinal accessor in identical order. Treat projection order, parameter names, provider identifiers, and stored-procedure names as persistence contracts.

## Build and Test

```powershell
dotnet build TomasAI.IFM.Framework.Storage/TomasAI.IFM.Framework.Storage.csproj --configuration Debug
dotnet test TomasAI.IFM.Framework.Storage.UnitTests/TomasAI.IFM.Framework.Storage.UnitTests.csproj --configuration Debug

# Real ScyllaDB provider contract suite
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj --filter Category=ScyllaDBIntegration

# Real PostgreSQL provider contract suite
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj --filter Category=PostgresIntegration

# Live ScyllaDB before/after BenchmarkDotNet comparison
dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- --filter '*ScyllaBulkWriteBenchmarks*' --artifacts .test-results/benchmarks

# Live PostgreSQL before/after BenchmarkDotNet comparison
dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- --filter '*PostgresBulkWriteBenchmarks*' --artifacts .test-results/benchmarks-postgres
```

The provider suites cover every `IObjectRepositoryProvider` method plus validation behavior, deterministic cleanup, ordinal type mapping, and PostgreSQL rollback. Their configuration and isolation contracts are documented in the ScyllaDB and PostgreSQL README files under `TomasAI.IFM.Application.Storage.IntegrationTests/FrameworkStorage`.
