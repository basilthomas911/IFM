# Storage Implementation Details

## Purpose and Scope

`TomasAI.IFM.Framework.Storage` is the provider-neutral persistence and structured-data layer for IFM. It targets .NET 10 and supplies:

- repository, command, query, transaction, and result-mapping abstractions;
- SQL Server, PostgreSQL, and ScyllaDB provider implementations;
- ADO.NET record adapters and expression-based object maps;
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
  -> IObjectMapReader or IObjectDataRecord maps each result
```

`ObjectDataRepository<TRepo>` is the base for application repositories. At construction it reads an `IDbConnectionSetting`, stores the connection string and provider name, creates an `ObjectDataDbProvider`, and invokes `OnCreateModel`. Consumers can then:

- select stored procedures by enum or expression;
- select raw command text;
- attach one object, an `IBindValue` struct, or an enumerable of parameter values;
- execute commands, queries, scalar reads, single-result reads, immutable result reads, or map/reduce operations;
- queue homogeneous text or stored-procedure commands;
- begin a provider transaction; or
- open a configured file URI context.

`ObjectDataStoredProcedureContext` and `ObjectDataCommandTextContext` define command text, command type, and parameter-name conventions. The abstract `ObjectDataRepositoryContext` delegates execution to the selected `IObjectRepositoryProvider` and owns the mutable parameter list. Contexts implement `IDisposable`; callers should scope them tightly.

The repository exposes a process-wide `SemaphoreSlim` through `LockAsync` and `Unlock`. Callers are responsible for pairing them correctly, preferably in `try/finally`.

## Provider Selection and Behavior

Provider creation is string-based:

- `System.Data.Postgres` selects Npgsql implementations.
- `System.Data.ScyllaDb` selects Cassandra-driver/ScyllaDB implementations in the connection and provider factories.
- other values fall back to SQL Server.

SQL Server and PostgreSQL providers implement command execution, queued commands, query and single-result mapping, scalar reads, immutable struct results, map/reduce, and transactions. The SQL Server bulk-copy context maps source columns by name and writes a `DataTable` through `SqlBulkCopy`.

ScyllaDB uses prepared/bound statements, Cassandra `RowSet` results, typed row accessors, and custom `DateOnly`, `DateTime`, and `TimeOnly` serializers. Cassandra/Scylla transactions are not implemented.

Provider-name handling is not fully consistent: parameter and transaction factories check `System.Data.Cassandra` or `System.Data.Scylla`, while the connection/provider factories check `System.Data.ScyllaDb`. New configuration must be tested end to end; centralizing these identifiers would prevent a Scylla configuration from accidentally taking a SQL Server fallback path.

## Mapping and Record Access

`DbModel<TRepo>`, `DbMap<TEntity>`, and `ObjectDataTypeMapper<TEntity>` build entity maps from expressions during `OnCreateModel`. Two mapping styles are supported:

- property maps assign database fields to writable entity properties; and
- parameter maps describe constructor parameters by field name and index.

Maps are stored in `IObjectRepository.ResultTypeMap` by result type. `ObjectDataReader<TResult>` selects a map, iterates an underlying `IDataReader`, and creates objects by reflection or a caller-supplied mapper.

`IObjectMapReader<TResult>` offers expression-based typed reads. `IObjectDataRecord` offers allocation-conscious, index-based reads. `AdoNetDataRecord` adapts an `IDataReader`; `ScyllaDbDataRecord` adapts a Cassandra row. Supported values include numeric primitives, booleans, strings, GUIDs, byte arrays, enums, and date/time types.

`PooledBufferBuilder<T>` rents memory from `MemoryPool<T>` and transfers ownership to `PooledReadOnlyBuffer<T>`. The returned buffer must be disposed to release the owner and rejects access after disposal.

## CSV, JSON, and URI Sources

`IStringReader` abstracts whole-content and asynchronous line reads:

- `FileStringReader` requires a file URI and uses `File.ReadAllTextAsync` or `File.ReadLinesAsync`.
- `HttpStringReader` requires HTTP/HTTPS when it fetches content, creates an `HttpClient` per operation, and can expose split lines as an async stream.

`CsvDataReader<TData>` reflects public properties to define its schema, treats the first input line as a header by default, builds a case-insensitive header index, and converts cells to common primitive/nullable types. `CsvWriter` reflects public properties into a header and rows.

`JsonDataReader<TData>` uses Newtonsoft.Json to deserialize a JSON array, then exposes reflected property values through `IDataReader`. `ObjectDataMapReader<TResult>` maps either reader through expression-selected result properties.

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

To add a mapped model, override `OnCreateModel` in the application repository and call `model.Map(...).Properties(...)` or `.Parameters(...)`. Treat field names, constructor indexes, provider identifiers, and stored-procedure names as persistence contracts.

## Build and Test

```powershell
dotnet build TomasAI.IFM.Framework.Storage/TomasAI.IFM.Framework.Storage.csproj --configuration Debug
dotnet test TomasAI.IFM.Framework.Storage.UnitTests/TomasAI.IFM.Framework.Storage.UnitTests.csproj --configuration Debug
```

The unit and integration test implementations are documented in their respective `Docs` folders.
