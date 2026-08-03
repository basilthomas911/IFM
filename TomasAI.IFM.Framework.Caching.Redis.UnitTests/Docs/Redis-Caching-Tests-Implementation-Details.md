# Redis Caching Tests Implementation Details

## Purpose

`TomasAI.IFM.Framework.Caching.Redis.UnitTests` validates expiration selection, prefix removal, and basic Redis storage operations for `RedisCache`. It targets .NET 10 and references the Redis caching implementation project.

Despite the project name, the suite mixes isolated unit tests with tests that connect to `localhost:6379`. A local Redis server is therefore required to run the complete suite.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Framework.Caching.Redis.UnitTests/`. Each listed leaf includes every parent folder.

```text
Docs/
bin/Debug/net10.0/cs/
bin/Debug/net10.0/de/
bin/Debug/net10.0/es/
bin/Debug/net10.0/fr/
bin/Debug/net10.0/it/
bin/Debug/net10.0/ja/
bin/Debug/net10.0/ko/
bin/Debug/net10.0/pl/
bin/Debug/net10.0/pt-BR/
bin/Debug/net10.0/ru/
bin/Debug/net10.0/runtimes/win/lib/net10.0/
bin/Debug/net10.0/runtimes/win/lib/net8.0/
bin/Debug/net10.0/tr/
bin/Debug/net10.0/zh-Hans/
bin/Debug/net10.0/zh-Hant/
bin/Debug/net8.0/cs/
bin/Debug/net8.0/de/
bin/Debug/net8.0/es/
bin/Debug/net8.0/fr/
bin/Debug/net8.0/it/
bin/Debug/net8.0/ja/
bin/Debug/net8.0/ko/
bin/Debug/net8.0/pl/
bin/Debug/net8.0/pt-BR/
bin/Debug/net8.0/ru/
bin/Debug/net8.0/runtimes/win/lib/net8.0/
bin/Debug/net8.0/tr/
bin/Debug/net8.0/zh-Hans/
bin/Debug/net8.0/zh-Hant/
bin/Release/net10.0/cs/
bin/Release/net10.0/de/
bin/Release/net10.0/es/
bin/Release/net10.0/fr/
bin/Release/net10.0/it/
bin/Release/net10.0/ja/
bin/Release/net10.0/ko/
bin/Release/net10.0/pl/
bin/Release/net10.0/pt-BR/
bin/Release/net10.0/ru/
bin/Release/net10.0/runtimes/win/lib/net10.0/
bin/Release/net10.0/tr/
bin/Release/net10.0/zh-Hans/
bin/Release/net10.0/zh-Hant/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Debug/net8.0/ref/
obj/Debug/net8.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

`Docs/` contains this document. `bin/` and `obj/` are generated trees. Locale leaves contain test-platform resources; runtime leaves contain Windows runtime assets. The `net8.0` branches are legacy artifacts because the test project now targets `net10.0`.

The project has no test-source subfolders. Its source-owned root files are:

- `RedisCacheTests.cs` — all current tests and the manual clock helper.
- `TomasAI.IFM.Framework.Caching.Redis.UnitTests.csproj` — test packages and project reference.

The project file also removes a `CommandHandlers/**` tree from compile, embedded-resource, and none items. No such directory currently exists.

## Test dependencies

- xUnit supplies the test runner and `[Fact]` tests.
- FluentAssertions supplies readable result and exception assertions.
- NSubstitute supplies `IConnectionMultiplexer`, `IDatabase`, and `IServer` substitutes.
- Microsoft.NET.Test.Sdk integrates the project with `dotnet test`.
- The project under test contributes `RedisCache` and `StackExchange.Redis` types.

## Isolated unit coverage

The deterministic tests inject `ManualTimeProvider` with a fixed UTC timestamp and substitute the Redis database:

- A shorter TTL is selected when the absolute deadline is later.
- An earlier absolute deadline is sent to Redis exactly.
- An expired absolute deadline throws before any write.
- The asynchronous bounded write selects the earlier deadline.
- Prefix removal escapes Redis pattern characters, scans with page size 250, and deletes only the returned keys.

These tests verify calls to `StringSet`, `StringSetAsync`, `IServer.Keys`, and `KeyDelete` without opening a real connection.

## Local Redis coverage

The remaining tests connect directly to `localhost:6379`:

- `GetOk` writes and retrieves `testKey`.
- `GetWithNonExistingKey` verifies a GUID key returns no value.
- `RemoveOk` verifies synchronous `DEL` behavior.
- `DeleteAllKeys_RemovesAllKeysFromDatabase` enables administrative commands, writes three keys, invokes `FLUSHDB`, and verifies their removal.

The `FLUSHDB` test affects every key in the selected local Redis database. It should run only against an isolated disposable database, never a shared development or production instance.

## Current coverage gaps

There is no direct test for `TryGet`, plain async get/set, relative-expiry overloads, invalid/non-positive TTL, `Increment`, disconnected/replica/Sentinel filtering, duplicate prefix results, failed deletion counts, Redis exception propagation, or the public empty-value behavior of `RemoveAsync`.

The local-server tests use fixed keys and do not clean up independently if an assertion fails. Parallel execution against a shared Redis database can therefore cause interference.

## Running the tests

Run isolated and local-server tests together with:

```powershell
dotnet test TomasAI.IFM.Framework.Caching.Redis.UnitTests/TomasAI.IFM.Framework.Caching.Redis.UnitTests.csproj --configuration Debug
```

Ensure a disposable Redis server is listening on `localhost:6379` and permits `FLUSHDB`. To make the suite hermetic, move real-server cases to an integration-test project or provision an ephemeral Redis instance per test run.

## Safe extension points

Add mocked tests for protocol decisions and isolated Redis tests for server-dependent semantics. Prefer unique key prefixes and scoped cleanup over database-wide flushing. If `RemoveAsync` is corrected to delete keys or added to `IRedisCache`, capture the intended behavior in both contract and implementation tests.
