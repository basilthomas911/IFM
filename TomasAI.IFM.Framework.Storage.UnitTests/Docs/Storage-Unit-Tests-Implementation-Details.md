# Storage Unit Tests Implementation Details

## Purpose

`TomasAI.IFM.Framework.Storage.UnitTests` is the isolated behavioral test suite for the core and Azure storage projects. It targets .NET 10, is marked as a non-packable test project, and exposes xUnit as a global using.

The test stack is xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.8.1, FluentAssertions 8.10.0, NSubstitute 6.0.0, Microsoft.Extensions.Hosting 10.0.10, and coverlet.collector 10.0.1. Project references include application storage, core storage, Azure storage, shared contracts, and reference/market-data domain contracts.

## Root-to-Leaf Directory Inventory

Every current directory leaf is listed below relative to the project root. Each path includes all intermediate parents. `bin/` and `obj/` are generated SDK, package, runtime, and localization trees.

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
- `bin/Debug/net10.0/runtimes/unix/lib/net8.0/`
- `bin/Debug/net10.0/runtimes/unix/lib/net9.0/`
- `bin/Debug/net10.0/runtimes/win/lib/net10.0/`
- `bin/Debug/net10.0/runtimes/win/lib/net8.0/`
- `bin/Debug/net10.0/runtimes/win/lib/net9.0/`
- `bin/Debug/net10.0/runtimes/win/lib/netcoreapp2.0/`
- `bin/Debug/net10.0/runtimes/win-arm/native/`
- `bin/Debug/net10.0/runtimes/win-arm64/native/`
- `bin/Debug/net10.0/runtimes/win-x64/native/`
- `bin/Debug/net10.0/runtimes/win-x86/native/`
- `bin/Debug/net10.0/tr/`
- `bin/Debug/net10.0/zh-Hans/`
- `bin/Debug/net10.0/zh-Hant/`
- `bin/Debug/net8.0/cs/`
- `bin/Debug/net8.0/de/`
- `bin/Debug/net8.0/es/`
- `bin/Debug/net8.0/fr/`
- `bin/Debug/net8.0/it/`
- `bin/Debug/net8.0/ja/`
- `bin/Debug/net8.0/ko/`
- `bin/Debug/net8.0/pl/`
- `bin/Debug/net8.0/pt-BR/`
- `bin/Debug/net8.0/ru/`
- `bin/Debug/net8.0/runtimes/unix/lib/net8.0/`
- `bin/Debug/net8.0/runtimes/win/lib/net8.0/`
- `bin/Debug/net8.0/runtimes/win/lib/netcoreapp2.0/`
- `bin/Debug/net8.0/runtimes/win-arm/native/`
- `bin/Debug/net8.0/runtimes/win-arm64/native/`
- `bin/Debug/net8.0/runtimes/win-x64/native/`
- `bin/Debug/net8.0/runtimes/win-x86/native/`
- `bin/Debug/net8.0/tr/`
- `bin/Debug/net8.0/zh-Hans/`
- `bin/Debug/net8.0/zh-Hant/`
- `bin/Release/net10.0/cs/`
- `bin/Release/net10.0/de/`
- `bin/Release/net10.0/es/`
- `bin/Release/net10.0/fr/`
- `bin/Release/net10.0/it/`
- `bin/Release/net10.0/ja/`
- `bin/Release/net10.0/ko/`
- `bin/Release/net10.0/pl/`
- `bin/Release/net10.0/pt-BR/`
- `bin/Release/net10.0/ru/`
- `bin/Release/net10.0/runtimes/unix/lib/net9.0/`
- `bin/Release/net10.0/runtimes/win/lib/net10.0/`
- `bin/Release/net10.0/runtimes/win/lib/net9.0/`
- `bin/Release/net10.0/runtimes/win/lib/netcoreapp2.0/`
- `bin/Release/net10.0/runtimes/win-arm64/native/`
- `bin/Release/net10.0/runtimes/win-x64/native/`
- `bin/Release/net10.0/runtimes/win-x86/native/`
- `bin/Release/net10.0/tr/`
- `bin/Release/net10.0/zh-Hans/`
- `bin/Release/net10.0/zh-Hant/`
- `Csv/`
- `Json/`
- `obj/Debug/net10.0/ref/`
- `obj/Debug/net10.0/refint/`
- `obj/Debug/net8.0/ref/`
- `obj/Debug/net8.0/refint/`
- `obj/Release/net10.0/ref/`
- `obj/Release/net10.0/refint/`
- `Postgres/`
- `ScyllaDb/`
- `TestData/`

## Source Folder Responsibilities

| Folder | Responsibility |
| --- | --- |
| Project root | Core repository, map, record, context, extension, pooling, CSV writer, Azure options, and shared-cache tests. |
| `Csv/` | CSV reader, object-reader, and writer behavior. |
| `Json/` | JSON reader and object-reader behavior. |
| `Postgres/` | Npgsql connection, reader, and transaction wrapper tests using substitutes rather than a server. |
| `ScyllaDb/` | Cassandra connection, queued-command, conversion-extension, and custom serializer tests. |
| `TestData/` | Reusable entities for reader, property-map, parameter-map, CSV, and JSON scenarios. |
| `Docs/` | Maintained test implementation documentation. |

`TestObjectDataReader.cs` is a root-level test double for the abstract object-reader pipeline. `appsettings.json` is copied to the test output and supplies configuration-binding data.

## Coverage Summary

The suite contains 528 xUnit facts and no theories. At the current grouping level:

- root-level tests: 360 facts;
- CSV tests: 58 facts;
- JSON tests: 51 facts;
- PostgreSQL adapter tests: 18 facts; and
- ScyllaDB adapter tests: 41 facts.

### Core records and conversions

`AdoNetDataRecordTests`, `ObjectDataRecordTests`, `ObjectArrayExtensionTests`, `ReadOnlySpanExtensionTests`, and `ScalarTests` verify supported primitive, nullable, enum, GUID, binary, and date/time access, plus invalid indexes, nulls, database nulls, and conversion failures.

### Mapping and repository composition

The map and mapper tests cover `DbMap`, `DbMapCollection`, property/parameter maps, expression-based field selection, constructor/property result creation, stored-procedure and command-text contexts, queued commands, provider factories, connections, and parameter wrappers. `DbCacheTests` exercises a shared storage cache referenced by the suite rather than a type declared in the core framework project.

### Structured files

CSV and JSON tests exercise reader construction, schemas, cursor behavior, indexed/name access, supported conversions, null/default behavior, and the object-reader adapters. CSV writer tests use disposable temporary files and verify headers and reflected output formatting. File-reader tests cover file URI validation and asynchronous whole-file/line reads; HTTP tests only validate construction failures in this unit suite.

### Providers and buffers

PostgreSQL tests substitute Npgsql-facing objects to verify connection and transaction lifecycle without a server. ScyllaDB tests cover connection routing, queued command state, date/time extensions, and custom driver serializers. `PooledReadOnlyBufferTests` verify enumeration, indexing, ownership transfer, and disposal behavior.

### Azure configuration

`AzureStorageTests` binds the Azure options section and verifies expected database/backup mappings. It does not upload a blob; the real upload case is isolated in the integration project.

## Test Boundaries

These unit tests do not establish real SQL Server, PostgreSQL, ScyllaDB, Azure, or HTTP connections. End-to-end transaction and remote-source coverage belongs to `TomasAI.IFM.Framework.Storage.IntegrationTests`.

The suite's copied settings file contains credential-bearing configuration. Test secrets should be moved to environment-specific secret storage and rotated when exposed; tests should assert configuration shape without embedding production-capable values.

## Running the Suite

```powershell
dotnet test TomasAI.IFM.Framework.Storage.UnitTests/TomasAI.IFM.Framework.Storage.UnitTests.csproj --configuration Debug
```

Optional coverage collection:

```powershell
dotnet test TomasAI.IFM.Framework.Storage.UnitTests/TomasAI.IFM.Framework.Storage.UnitTests.csproj --configuration Debug --collect:"XPlat Code Coverage"
```
