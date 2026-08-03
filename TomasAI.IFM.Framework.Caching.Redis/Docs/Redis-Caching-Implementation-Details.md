# Redis Caching Implementation Details

## Purpose

`TomasAI.IFM.Framework.Caching.Redis` is the concrete Redis adapter for the framework-level `IRedisCache` contract. It targets .NET 10, uses `StackExchange.Redis` 3.0.17, and depends on `TomasAI.IFM.Framework.Caching` for the abstraction and `TomasAI.IFM.Shared` for argument validation helpers.

The adapter stores strings only. JSON serialization, deserialization, domain cache-key construction, and cache-model semantics belong to callers such as the Application Blackboard project.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Framework.Caching.Redis/`. Each leaf path includes every intermediate parent folder.

```text
Docs/
bin/Debug/net10.0/runtimes/win/lib/net8.0/
bin/Debug/net8.0/
bin/Release/net10.0/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Debug/net8.0/ref/
obj/Debug/net8.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

`Docs/` contains this document. `bin/` and `obj/` are generated output and intermediate trees. The `net8.0` branches are legacy/generated dependency artifacts; the project itself currently targets `net10.0`.

The project has no source subfolders. Its two source-owned root files are:

- `RedisCache.cs` — the Redis implementation.
- `TomasAI.IFM.Framework.Caching.Redis.csproj` — framework, package, and project references.

## Construction and connection ownership

`RedisCache` receives an `IConnectionMultiplexer`, obtains its default `IDatabase`, and retains both objects. A nullable `TimeProvider` can be injected for deterministic expiration calculations; production defaults to `TimeProvider.System`.

The class does not create, configure, reconnect, or dispose the multiplexer. The composition root owns that lifecycle. Application startup currently registers `IRedisCache` as a singleton, matching the intended long-lived multiplexer usage.

## Read operations

- `Get(key)` performs synchronous `StringGet` and returns `null` for a missing key.
- `GetAsync(key)` performs asynchronous `StringGetAsync`.
- `TryGet(key, out value)` first calls `KeyExists` and then `StringGet`; it returns `false` for missing, null, or empty-string values. This is a two-command, non-atomic read.

Values are returned exactly as Redis strings. No type metadata or serialization is added by this project.

## Write and expiration operations

- `Set(key, value)` and `SetAsync(key, value)` write without expiration.
- The `TimeSpan` overloads pass a relative TTL directly to Redis.
- The `absoluteExpiry` plus `ttl` overloads enforce a renewable TTL bounded by a hard UTC deadline. `GetExpiration` selects whichever deadline occurs first.
- A non-positive TTL throws `ArgumentOutOfRangeException` for `ttl`.
- An absolute expiration at or before the current injected UTC time throws `ArgumentOutOfRangeException` for `absoluteExpiry`.
- Bounded writes use `ValueCondition.Always`, so an existing key is replaced.

Absolute deadlines are normalized to UTC. Injecting `TimeProvider` avoids wall-clock dependence in tests.

## Removal and maintenance operations

- `Remove(key)` issues Redis `DEL` synchronously.
- `RemoveByPrefix(prefix)` validates a nonblank prefix, escapes Redis glob characters, scans every connected non-replica, non-Sentinel server with page size 250, de-duplicates returned keys, deletes them individually, and returns the successful deletion count. It does not flush the database.
- Public `RemoveAsync(key)` reads the value and replaces a nonempty value with an empty string. It does not delete the key and is not declared by `IRedisCache`; callers through the interface cannot access it.
- `DeleteAllKeys()` issues `FLUSHDB` against the selected database. This requires appropriate Redis administrative permissions and irreversibly removes all keys in that database.
- `Increment(key)` delegates to Redis `INCR`, providing an atomic integer counter initialized from zero when absent.

## Prefix escaping and topology behavior

`EscapePattern` prefixes `*`, `?`, `[`, `]`, and `\` with a backslash before appending the terminal wildcard. Therefore the supplied prefix is interpreted literally. Scanning skips disconnected endpoints, replicas, and Sentinels. A `HashSet<RedisKey>` prevents duplicate deletion attempts when the same primary is reachable through multiple endpoints.

Because prefix removal uses server-side key enumeration, Redis server access must permit scanning. Large keyspaces still require operational care even though enumeration is incremental.

## Consumers

The primary consumers are Blackboard cache models, which serialize domain objects and build stable key names before invoking `IRedisCache`. Domain integration-test fixtures also construct `RedisCache` directly. The API Server and Actor Integration Tests register the adapter as the singleton `IRedisCache` implementation.

## Error and concurrency behavior

Redis and network exceptions are not caught or translated; they propagate to the caller. Individual string writes, deletes, and increments inherit Redis command atomicity, but multi-command methods such as `TryGet`, `RemoveAsync`, and prefix scanning are not atomic transactions.

## Safe extension points

When extending the adapter:

1. Add the operation to `IRedisCache` if interface consumers need it.
2. Preserve multiplexer reuse rather than opening a connection per operation.
3. Prefer native async Redis commands for asynchronous APIs.
4. Make delete-versus-empty-value semantics explicit.
5. Keep serialization outside this infrastructure adapter unless the abstraction is deliberately redesigned.
6. Add mocked protocol tests and, where server behavior matters, isolated Redis integration tests.
