# Reference Domain Actor Optimization

## Scope and constraints

This report records the root-to-leaf optimization pass completed on 2026-08-05 for `TomasAI.IFM.Domain.Reference` and its directly consumed blackboard and Reference storage paths. The review covered garbage collection, threading, locking, async/sync patterns, memory use, concurrency, latency, throughput, and code complexity.

The following domain rules were preserved:

- Event histories remain immutable and unbounded.
- State recovery begins with the snapshot type required by the command stream.
- Missing snapshots or requested events produce the best available, possibly empty, state; application code does not manufacture replay exceptions.
- Command success remains distinct from state mutation. A successful command is not forced to produce an event.
- `EconomicCalendarEventActor` and `LookupTypeEventActor` remain intentionally empty default event targets.
- Dictionary dispatch remains in place. A generated jump table is deferred until paper-trading telemetry shows dispatch to be material.
- The solution-wide cancellation pass now covers all Reference query actors, handlers, direct API operations, storage reads, and external calendar parsing.

## Top ten issues found and resolved

| Rank | Issue found | Impact | Resolution |
|---:|---|---|---|
| 1 | Both command actors synchronously waited for audit persistence inside `ParseMessage` | Blocked actor execution and coupled mailbox latency to storage | Added a reference-identity command audit tracker: parsing starts persistence without blocking and validation asynchronously observes the same operation |
| 2 | Calendar import emitted one added event and performed one denormalized write per row despite existing batch contracts | Event count, work items, allocations, and storage calls grew linearly | Import now emits one cumulative `EconomicCalendarsImportedEvent` snapshot and uses `InsertEconomicCalendarsAsync` once |
| 3 | Import duplicate detection checked the synthetic batch ID instead of each calendar ID and mutated state while iterating | Existing or repeated IDs were not detected correctly and a later failure could follow earlier mutations | The complete batch is checked with a `HashSet` before mutation; one atomic state update follows validation |
| 4 | Every reference existence check synchronously read Redis, deserialized JSON, double-probed a dictionary, and linearly scanned a list | Validation latency and CPU grew with lookup size; repeated calls created distributed-cache pressure | Added a versioned immutable `FrozenDictionary<string, FrozenSet<string>>` with ordinal case-insensitive constant-time hot reads |
| 5 | Concurrent cold cache misses could all issue the same actor request and rebuild the same map | Cache stampede, redundant actor/storage work, and unnecessary locking risk | Cold initialization and refresh are single-flight behind one off-hot-path gate; the published frozen snapshot is read without locks |
| 6 | Lookup add/change/remove operations did not invalidate the lookup cache | Valid commands could leave consumers reading stale reference values | Successful lookup denormalization removes the Redis entry and advances an in-process generation; remote snapshots have a bounded 30-second lifetime |
| 7 | Default futures and strike definitions awaited six and three independent reads sequentially in two duplicated implementations | End-to-end latency was the sum of independent I/O delays | Centralized each mapping, starts every independent read first, and awaits the group |
| 8 | Short-code existence used duplicated LINQ scans in both query entry points | Iterator/closure allocation and duplicated comparison behavior on every check | Centralized an allocation-light, case-insensitive partition scan in the storage context; the deployed clustering order requires `OrderId` before `ShortCode`, so an exact short-code predicate is intentionally deferred to a migrated index/table design |
| 9 | Both command states swallowed every supported-event exception | Malformed replay could silently appear to be a normal no-change result | Removed broad empty catches so unexpected failures reach the established actor exception pipeline |
| 10 | Validator graphs, derived IDs, empty maps, weekly boundaries, and genuinely non-suspending async wrappers created avoidable work | Higher Gen0 pressure and unnecessary code complexity | Reused immutable validators, used event IDs where available, made empty maps static, used constant-time week arithmetic, and returned direct `ValueTask` values only for completed paths; repository storage methods intentionally use explicit `async`/`await` because their forwarding overhead is insignificant beside I/O |

No project-local lock contention was found in actor-owned state. Actor state remains mailbox-owned. The lookup refresh gate is entered only for a missing, expired, or invalidated snapshot; normal reference checks are lock-free.

## BenchmarkDotNet environment

- BenchmarkDotNet 0.15.8
- .NET SDK 10.0.302; .NET runtime 10.0.10; x64 RyuJIT x86-64-v3
- Windows 10 22H2
- AMD Ryzen Threadripper 1950X, 16 physical/32 logical cores
- Concurrent Workstation GC
- Three warmups and eight measured iterations
- `MemoryDiagnoser`; `ThreadingDiagnoser` on asynchronous orchestration cases

The benchmark project retains explicit `Before` implementations and runs each pair with identical inputs. Controlled `Task.Delay(1)` cases measure orchestration shape rather than production database, Redis, broker, or network latency. The import benchmark uses one `Task.Yield` per simulated write to isolate event/write work-item scaling. Lookup benchmarks isolate the managed hot index; they do not include the additional Redis and JSON work removed from production hot reads.

## Before and after results

### Command audit parse release

| Path | Mean | Allocated | Completed work items |
|---|---:|---:|---:|
| Before blocking parse | 14.689 ms | 232 B | 1.0000 |
| After non-blocking parse | 0.388 us | 184 B | 0.2099 |

Parsing releases the actor approximately 37,900 times sooner in the controlled test. Audit work is not discarded: validation awaits the tracked operation and propagates failure through the actor pipeline.

### Immutable reference lookup index

| Short codes | Before list scan | After frozen lookup | Speedup | Allocation |
|---:|---:|---:|---:|---:|
| 32 | 1,179.03 ns | 16.57 ns | 71.2x | 0 B / 0 B |
| 512 | 16,656.65 ns | 18.03 ns | 923.7x | 0 B / 0 B |
| 4,096 | 163,821.73 ns | 23.58 ns | 6,948.6x | 0 B / 0 B |

The optimized managed lookup remains effectively constant as the cached category grows. Production hot calls also avoid synchronous Redis access and JSON deserialization, which are intentionally excluded from this CPU benchmark.

### Economic-calendar batch import orchestration

| Rows | Before per-row | After batch | Speedup | Before allocation | After allocation | Work items before/after |
|---:|---:|---:|---:|---:|---:|---:|
| 8 | 13.870 us | 1.344 us | 10.3x | 2,401 B | 200 B | 8.16 / 1.04 |
| 64 | 74.845 us | 1.446 us | 51.8x | 18,112 B | 205 B | 64.54 / 1.04 |
| 512 | 558.533 us | 1.284 us | 435.1x | 143,552 B | 205 B | 514.79 / 1.04 |

At 512 rows, batching reduced managed allocation by 99.86%. Real storage improvement depends on ScyllaDB and network latency, but the production path now uses the existing batch storage operation rather than one call per row.

### Independent reference I/O

| Reads | Before sequential | After concurrent | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 3 | 42.443 ms | 10.451 ms | 4.06x | 1,000 B | 1,216 B |
| 6 | 93.400 ms | 13.676 ms | 6.83x | 1,816 B | 2,088 B |

The small task-array allocation increase is accepted in exchange for removing additive independent-I/O latency. No `Task.Run` is used in production.

### Cached validation rules

| Rows | Before new graph | After cached graph | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 1 | 3.305 us | 0.227 us | 14.5x | 8,096 B | 664 B |
| 32 | 106.097 us | 7.347 us | 14.4x | 259,072 B | 21,248 B |
| 256 | 864.009 us | 56.536 us | 15.3x | 2,072,576 B | 169,984 B |

Validator reuse reduced allocation by approximately 91.8% at every tested size.

### Weekly boundary calculation

| Input day | Before loop | After arithmetic | Result |
|---|---:|---:|---|
| Sunday | 12.64 ns | 6.40 ns | 1.97x faster |
| Wednesday | 4.59 ns | 6.40 ns | 1.39x slower |
| Saturday | 10.07 ns | 6.40 ns | 1.57x faster |

The arithmetic path has constant bounded cost and fixes `NextWeek` on Monday so it starts the following Monday. This is a correctness and complexity improvement; it is not faster for every day of the week.

## Verification

- Baseline Release unit tests: 10 passed.
- Baseline BDD project: built but contained no discoverable tests.
- Final Release unit tests: 19 passed, including actor and direct-API cancellation coverage.
- Final Release BDD tests: 1 passed.
- Live Reference integration verification: 28 of 31 tests passed. Both optimized short-code existence cases passed after validating the deployed Scylla clustering order (existing code and missing code). The remaining failures were shared-fixture state issues: one remove test could not perform its prerequisite add, and two calendar-range fixtures did not find their directly inserted rows among stale shared projections.
- Production, benchmark, API server, actor integration-test, and Reference integration-test projects build successfully with zero warnings and errors. One existing obsolete `RowSet.Dispose()` warning can appear while rebuilding the transitive Framework.Storage project and is outside this domain pass.
- BenchmarkDotNet completed all 30 configured cases.
- Reproduce the measurements from the repository root with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.Reference.Benchmarks -- --filter '*' --join
```

## Deferred work

### Solution-wide cancellation

Reference cancellation is end-to-end across query actors, handlers, direct API operations, read-model storage, and external calendar parsing. Canceled reads publish no actor reply and direct APIs rethrow cancellation. Seed compare-and-set submission retains a non-cancelable resolution boundary to avoid losing an allocated identifier.

### Synchronous lookup interface

`IReferenceLookupService` remains synchronous because it is consumed by validation across multiple domains. The optimized service confines blocking to a single guarded cold load and makes all normal reads lock-free. Converting the cold boundary to fully asynchronous calls requires a coordinated shared-interface and consumer update, ideally alongside the cancellation pass.

### Regular review

Retain this benchmark suite for future passes. Add paper-trading measurements for mailbox latency, lookup refresh frequency, allocation rate, storage round trips, and cache invalidation lag. Pursue dispatch jump tables or similar micro-optimizations only when production profiles show material impact.
