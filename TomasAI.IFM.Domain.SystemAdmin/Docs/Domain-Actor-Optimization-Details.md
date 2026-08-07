# System Administration Domain Actor Optimization

## Scope and constraints

This report records the root-to-leaf optimization pass completed on 2026-08-05 for `TomasAI.IFM.Domain.SystemAdmin`, its shared contracts, and directly affected consumers. The review covered garbage collection, threading, locking, async/sync patterns, memory use, concurrency, latency, throughput, and code complexity.

The following domain rules were preserved:

- Event histories remain immutable and unbounded. Backup events are still appended; only the unnecessary pre-command replay was removed because no backup decision reads prior state.
- Command success remains distinct from state mutation.
- `SystemAdminEventActor` remains an intentionally empty default event target.
- Dictionary dispatch remains in place. Jump-table micro-optimization is deferred until paper-trading telemetry shows dispatch to be material.
- End-to-end cancellation remains a separate solution-wide change after the root-domain optimization passes.

## Top ten issues found and resolved

| Rank | Issue found | Impact | Resolution |
|---:|---|---|---|
| 1 | `ParseMessage` synchronously blocked on command-audit storage | An actor thread was held for the complete storage round trip, limiting latency and throughput | Parsing now starts the audit without blocking; validation awaits the same reference-tracked operation so audit failures still reach the actor pipeline |
| 2 | Every backup command reconstructed event-sourced state even though execution never consulted prior state | One unnecessary snapshot/event storage query and replay preceded every backup | The repository creates fresh state for this stateless decision while the save path continues appending the emitted backup event |
| 3 | Event publication rebuilt a route through string replacement and the generic reflection-oriented publication path | Repeated subject parsing, string work, and dispatch complexity on every event | Denormalization validates the existing event and sends it directly through its already-correct `ActorSubject` |
| 4 | The fixed database-name set was rebuilt as a new mutable array for every query | Gen0 allocation and repeated construction on an invariant read path | A single immutable, MessagePack-compatible read model is cached and shared safely |
| 5 | The direct query API created a new task/result graph for immutable data | Avoidable task and wrapper allocation reduced query throughput | The API returns one cached completed task containing the immutable result |
| 6 | Successful validation allocated an empty `List<ValidationError>` | Every valid command paid allocation cost for an exceptional path | The valid path performs a direct ID check; a one-item error list is created only on failure |
| 7 | Non-suspending startup, resolver, and query-dispatch wrappers used `async` state machines | Extra state-machine complexity and possible allocation | Genuinely completed paths return `ValueTask`, cached `Task`, or values directly; repository persistence intentionally retains explicit `async`/`await` because its forwarding overhead is insignificant beside I/O |
| 8 | State replay swallowed all exceptions in a broad empty catch | Corrupt or failed replay could be reported as a normal no-change outcome | Removed the catch so unexpected faults use the established actor exception pipeline |
| 9 | Command metadata repeatedly used reflection, interpolation, environment lookup, and a changing `UtcNow` getter | CPU work and inconsistent audit metadata on each property access | Stable command name, event source, origin user, stream ID, and construction timestamp are reused |
| 10 | Empty maps were allocated per event actor and an empty command-model type duplicated no behavior | Per-instance memory and unnecessary maintenance surface | Empty maps are static, the assembly marker is cached, and the dead model was removed; the intentionally empty event actor itself remains |

No project-local lock was added to actor-owned state. The audit tracker uses `ConcurrentDictionary` because parse/validation completion can cross asynchronous continuations; each entry is removed before its audit is awaited. Immutable query state is published once and requires no locking.

## BenchmarkDotNet environment

- BenchmarkDotNet 0.15.8
- .NET SDK 10.0.302; .NET runtime 10.0.10; x64 RyuJIT x86-64-v3
- Windows 10 22H2
- AMD Ryzen Threadripper 1950X, 16 physical/32 logical cores
- Concurrent Workstation GC
- Three warmups and eight measured iterations
- `MemoryDiagnoser`; `ThreadingDiagnoser` on asynchronous cases

The benchmark project retains explicit `Before` implementations and gives each pair identical inputs. `Task.Delay(1)` cases model asynchronous storage boundaries rather than production ScyllaDB or network latency. Nanosecond results isolate managed hot-path overhead and should not be treated as end-to-end actor latency.

## Before and after results

| Optimization pair | Before mean | After mean | Improvement | Before allocation | After allocation |
|---|---:|---:|---:|---:|---:|
| Snapshot round trip vs fresh backup state | 15.353 ms | 11.924 ns | about 1.29 million times faster in the controlled I/O model | 440 B | 104 B |
| Blocking audit parse vs tracked parse release | 15.564 ms | 392.373 ns | parse returns about 39,700 times sooner | 232 B | 184 B |
| Recomputed command metadata vs cached metadata | 156.732 us | below measurable overhead | repeated work effectively eliminated | 136 B | 0 B |
| Mutable database-name response vs cached read-only response | 6.848 ns | 0.923 ns | 7.42x faster | 80 B | 0 B |
| Rebuilt event route vs existing route | 11.004 ns | 1.279 ns | 8.60x faster | 0 B | 0 B |
| Always-allocated validation list vs failure-only allocation | 4.665 ns | 0.668 ns | 6.99x faster | 32 B | 0 B |

The audit benchmark measures how quickly parsing releases the actor; it does not discard audit durability. Production validation observes the tracked operation and propagates a failure normally. The state-load benchmark removes an entire irrelevant storage boundary, so its controlled ratio is deliberately much larger than the CPU-only cases. BenchmarkDotNet reported the cached metadata case as indistinguishable from empty-method overhead, so no precise speedup is claimed for that pair.

## Verification

- Baseline Release unit tests: 64 passed.
- Baseline BDD project: built with no discoverable tests.
- Final Release unit tests: 71 passed, including non-blocking audit completion, stateless load, immutable caching, task reuse, MessagePack round-trip coverage, and actor/direct-API cancellation behavior.
- Final Release BDD tests: 2 passed, covering backup event production and stable read-only database-name results.
- Final Release integration tests: 1 passed (the current placeholder integration test).
- Domain, benchmark, SystemAdmin server, API server, and UI model projects build successfully with zero warnings and errors. A pre-existing obsolete `RowSet.Dispose()` warning may appear only when the transitive storage framework is rebuilt from restore.
- BenchmarkDotNet completed all 12 configured before/after cases. The corrected database-name allocation pair was rerun separately after preventing JIT dead-code elimination.
- Reproduce the measurements from the repository root with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.SystemAdmin.Benchmarks -- --filter '*' --join
```

## Deferred work

### Solution-wide cancellation

SystemAdmin cancellation is complete across command-audit observation, command replay, the query actor/resolver, and the direct in-process query API. Event persistence retains the shared non-cancelable durable-outcome boundary. The database-name query has no storage leaf because it returns an immutable process-local snapshot.

### Regular review

Retain this benchmark suite for future passes. Add paper-trading measurements for mailbox latency, audit-storage latency, allocation rate, command throughput, and backup-event publication. Pursue dispatch jump tables or similar micro-optimizations only when production profiles show material impact.
