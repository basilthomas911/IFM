# Storage Integration Tests Implementation Details

## Purpose

`TomasAI.IFM.Framework.Storage.IntegrationTests` contains the legacy opt-in tests for storage behaviors that depend on real infrastructure. The assembly name retains the older `IntegratedTests` spelling from its project file. It targets .NET 10 and is non-packable.

The project references the application storage layer, the core and Azure storage projects, and shared/domain contracts. Its test stack is xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.8.1, FluentAssertions 8.10.0, NSubstitute 6.0.0, and Microsoft.Extensions.Hosting 10.0.10.

Full PostgreSQL and ScyllaDB `IObjectRepositoryProvider` contract coverage now lives in `TomasAI.IFM.Application.Storage.IntegrationTests/FrameworkStorage`. It is colocated there so it can reuse the real Event Source and Fund schema catalogs while testing the public Framework Storage API directly.

## Root-to-Leaf Directory Inventory

Every current directory leaf is listed below relative to the project root. Each path includes all intermediate parents. `bin/` and `obj/` are generated build/package trees.

- `Docs/`
- `bin/Debug/net10.0/cs/`
- `bin/Debug/net10.0/de/`
- `bin/Debug/net10.0/es/`
- `bin/Debug/net10.0/fr/`
- `bin/Debug/net10.0/it/`
- `bin/Debug/net10.0/ja/`
- `bin/Debug/net10.0/ko/`
- `bin/Debug/net10.0/pl/`
- `bin/Debug/net10.0/pt-BR/`
- `bin/Debug/net10.0/ru/`
- `bin/Debug/net10.0/runtimes/unix/lib/net9.0/`
- `bin/Debug/net10.0/runtimes/win/lib/net10.0/`
- `bin/Debug/net10.0/runtimes/win/lib/net9.0/`
- `bin/Debug/net10.0/runtimes/win/lib/netcoreapp2.0/`
- `bin/Debug/net10.0/runtimes/win-arm64/native/`
- `bin/Debug/net10.0/runtimes/win-x64/native/`
- `bin/Debug/net10.0/runtimes/win-x86/native/`
- `bin/Debug/net10.0/tr/`
- `bin/Debug/net10.0/zh-Hans/`
- `bin/Debug/net10.0/zh-Hant/`
- `obj/Debug/net10.0/ref/`
- `obj/Debug/net10.0/refint/`

## Root Files

| File | Responsibility |
| --- | --- |
| `AzureStorageTests.cs` | Defines the skipped real Azure Blob upload test. |
| `CsvDataReaderTests.cs` | Defines three skipped licensed HTTP CSV-feed tests. |
| `JsonDataReaderTests.cs` | Defines three skipped licensed HTTP JSON-feed tests. |
| `ObjectDataRepositoryTransactionTests.cs` | Defines two active local PostgreSQL commit/rollback tests and their fixture. |
| `StringReaderTests.cs` | Defines a skipped licensed HTTP string-reader test. |
| `appsettings.json` | Supplies copied-at-build test configuration, including Azure upload mappings. |
| `TomasAI.IFM.Framework.Storage.IntegratedTests.csproj` | Defines packages, content-copy behavior, and project references. |

No test-source subfolders currently exist; all source and configuration files are in the project root.

## Test Inventory

The project contains 10 xUnit facts:

- 2 active PostgreSQL transaction tests;
- 1 skipped Azure Storage upload test;
- 3 skipped remote CSV reader tests;
- 3 skipped remote JSON reader tests; and
- 1 skipped remote string-reader test.

The skipped tests declare their infrastructure requirements in `Fact.Skip` and do not run by default.

## PostgreSQL Transaction Fixture

`EventDatabaseFixture` constructs a local `EventSourceDbContext` through the application storage factory and a PostgreSQL connection setting. It substitutes logging and blackboard dependencies, then exposes the context to tests through xUnit's `IClassFixture` mechanism.

The two active tests:

1. generate an isolated command identifier;
2. confirm that no matching `command_log` row exists;
3. begin a repository transaction;
4. insert a row using raw SQL;
5. commit or roll back;
6. verify the resulting row count; and
7. delete the test row in `finally` cleanup.

These tests require a reachable local PostgreSQL database with the expected event-source schema and credentials. They mutate `command_log` and therefore must not be pointed at production.

The connection string in `EventDatabaseFixture` is credential-free. With the default Development environment it resolves `POSTGRES_DEV_KEY`; when the process is explicitly set to Test it resolves `POSTGRES_TEST_KEY`.

## Full Provider Contract Suites

The active provider suites in `TomasAI.IFM.Application.Storage.IntegrationTests` cover all methods declared by `IObjectRepositoryProvider`:

| API behavior | ScyllaDB | PostgreSQL |
| --- | --- | --- |
| Commands with zero, one, and many parameter values | Covered | Covered |
| Queue creation and execution modes | Sequential and logged batch | Both transaction-flag paths through `NpgsqlBatch` |
| Cancellable async streaming | Full enumeration, early disposal, cancellation | Full enumeration, early disposal, cancellation |
| Mutable query materialization | Covered | Covered |
| Immutable value-type materialization | Disposable pooled buffer | Read-only value list |
| Single result found/missing | Covered | Covered |
| Scalar result | Covered | Covered |
| Map/reduce | Covered | Covered |
| Stream/query/single/immutable/map-reduce null delegates, excess query parameters, and empty queue guards | Covered | Covered |
| Provider-specific type/ordinal mapping | All four Fund tables | All five event-source tables |
| Failure atomicity | Logged-batch behavior | `NpgsqlBatch` transaction rollback |

The ScyllaDB suite contains 17 real-provider tests and uses `fund`, `fund_order`, `fund_order_trade`, and `fund_transaction`. Seven database-independent contract cases validate positional binding: two cover all 28 Fund `IBindValue` types, including CQL update-marker order, nullable trailing values, and `DateOnly` values; five dynamically validate all 236 Market Data, Option Pricer, Reference, Securities, and Trade bindings against the marker sequences in their checked-in CQL. The PostgreSQL suite contains 19 real-provider tests and uses `event_stream_id`, `event_name_id`, `event_log`, `command_log`, and `event_projector_state`. It validates both queued-command modes through `NpgsqlBatch`, transaction rollback, and explicit preparation through a `pg_prepared_statements` probe. Three additional database-independent catalog cases validate every Event Source, Log, and Sequence ID binding against its native `$n` SQL placeholders, explicit `NpgsqlDbType`, typed parameter implementation, value, and ordinal.

Both fixtures create missing tables/schema objects but not the keyspace/database. Before the collection starts, before each test, after each test, and at collection disposal they remove only reserved test rows. Cleanup is verified after deletion. Collections are non-parallel, ScyllaDB reserves negative Fund IDs, and PostgreSQL reserves negative event IDs plus names prefixed with `__framework_storage_postgres_it__`; PostgreSQL does not consume event-source sequences.

Every query supplies `Func<IObjectDataRecord, TResult>` and reads typed values by zero-based ordinal. The suites therefore detect mismatches between projection order, provider record adapters, and application mapper contracts without using the removed reflection-based result mapper.

## Azure and Remote Feed Tests

The Azure test binds `AppSettings:AzureStorage`, constructs `AzureStorage`, and attempts a real backup-file upload. It is skipped because it needs both a valid account and the configured local source file.

The CSV and JSON tests instantiate `HttpStringReader` against licensed remote feeds, create the corresponding data reader, and verify schema/index/name access. The string-reader test fetches an entire licensed CSV response. These tests are intentionally skipped to avoid network, licensing, and credential dependencies during ordinary builds.

## Configuration and Secret Handling

PostgreSQL and ScyllaDB test connection strings contain no database user ID or password. Framework Storage selects credentials from the runtime environment and injects them only when creating the physical connection. Set `DOTNET_ENVIRONMENT=Test`, then provide `POSTGRES_TEST_KEY` and/or `SCYLLADB_TEST_KEY` as `{"userid":"...","password":"..."}`. The full provider suites additionally require `IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION` and `IFM_SCYLLA_TEST_CONNECTION`; both must target dedicated test infrastructure.

The legacy `appsettings.json` is copied to the output directory on every build and still contains separate Azure Storage configuration and local file paths. The skipped Azure test is outside the PostgreSQL/ScyllaDB credential resolver. Do not reproduce its value in documentation or logs; any committed Azure secret should be treated as exposed and rotated independently.

The configuration file is optional at load time, but tests that dereference its bound options still require the expected section and entries.

See [`docs/database-credentials.md`](../../docs/database-credentials.md) and the provider-suite README files under `TomasAI.IFM.Application.Storage.IntegrationTests/FrameworkStorage` for the environment matrix and exact setup commands.

## Running Safely

Build without executing external tests:

```powershell
dotnet build TomasAI.IFM.Framework.Storage.IntegrationTests/TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --configuration Debug
```

Running `dotnet test` executes the two PostgreSQL tests even though the other eight are skipped. Only run it after verifying the target connection, schema, permissions, and cleanup expectations:

```powershell
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --configuration Debug
```

Run the isolated provider contract suites independently:

```powershell
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj --filter Category=ScyllaDBIntegration
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj --filter Category=PostgresIntegration
```

Keep new infrastructure-dependent tests explicitly isolated and ensure every mutation has deterministic cleanup.
