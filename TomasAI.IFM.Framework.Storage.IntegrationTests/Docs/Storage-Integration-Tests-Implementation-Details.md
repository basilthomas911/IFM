# Storage Integration Tests Implementation Details

## Purpose

`TomasAI.IFM.Framework.Storage.IntegrationTests` contains opt-in tests for storage behaviors that depend on real infrastructure. The assembly name retains the older `IntegratedTests` spelling from its project file. It targets .NET 10 and is non-packable.

The project references the application storage layer, the core and Azure storage projects, and shared/domain contracts. Its test stack is xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.8.1, FluentAssertions 8.10.0, NSubstitute 6.0.0, and Microsoft.Extensions.Hosting 10.0.10.

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

## Azure and Remote Feed Tests

The Azure test binds `AppSettings:AzureStorage`, constructs `AzureStorage`, and attempts a real backup-file upload. It is skipped because it needs both a valid account and the configured local source file.

The CSV and JSON tests instantiate `HttpStringReader` against licensed remote feeds, create the corresponding data reader, and verify schema/index/name access. The string-reader test fetches an entire licensed CSV response. These tests are intentionally skipped to avoid network, licensing, and credential dependencies during ordinary builds.

## Configuration and Secret Handling

`appsettings.json` is copied to the output directory on every build. The current file contains credential-bearing connection material and local file paths; the transaction fixture also embeds a database credential in source. Do not reproduce those values in documentation, logs, or new tests. Treat committed credentials as exposed, rotate them, and migrate the suite to environment variables, user secrets, or a dedicated test-secret provider.

The configuration file is optional at load time, but tests that dereference its bound options still require the expected section and entries.

## Running Safely

Build without executing external tests:

```powershell
dotnet build TomasAI.IFM.Framework.Storage.IntegrationTests/TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --configuration Debug
```

Running `dotnet test` executes the two PostgreSQL tests even though the other eight are skipped. Only run it after verifying the target connection, schema, permissions, and cleanup expectations:

```powershell
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --configuration Debug
```

Keep new infrastructure-dependent tests explicitly isolated and ensure every mutation has deterministic cleanup.
