# Market Data Feed Domain Actor Optimization

## Scope and constraints

This report records the root-to-leaf optimization pass completed on 2026-08-05 for `TomasAI.IFM.Domain.MarketData.Feed` and the directly consumed shared hot paths. The review covered garbage collection, threading, locking, async/sync patterns, memory use, concurrency, latency, throughput, and code complexity.

The following domain rules were preserved:

- Event histories remain immutable and unbounded.
- Replay begins from the latest requested snapshot and reads only the event range needed by the state.
- Missing snapshots or requested event types yield the best available, possibly empty, state; application code does not manufacture replay exceptions.
- A command may succeed without changing state.
- Empty event actors remain valid default publication targets by design.
- One accepted market tick still produces one persisted event; this pass does not coalesce data.
- Dispatch-table micro-optimization remains deferred until paper-trading measurements identify it as material.
- End-to-end cancellation propagation remains a separate solution-wide change after the domain optimization passes.

## Top ten issues found and resolved

| Rank | Issue found | Impact | Resolution |
|---:|---|---|---|
| 1 | Command audit persistence was synchronously blocked with `GetAwaiter().GetResult()` inside parse paths | Mailbox-thread blocking, sync-over-async deadlock risk, and lower throughput | Added a command-scoped audit tracker: parse starts the write without blocking, validation awaits and observes it, and already-completed failures are still surfaced immediately |
| 2 | Futures bar scheduling used one replaceable timer and synchronous callback | Streams could replace one another, callbacks could overlap, and shutdown did not drain work | Replaced it with per-entity `PeriodicTimer` registrations, `Func<ValueTask>` callbacks, idempotent start, non-overlap, targeted async stop, and actor-wide draining shutdown |
| 3 | `StreamIdCollection` used static mutable state, a replaceable lock, hash-derived IDs, and linear reverse lookup | Cross-instance leakage, collision risk, unsafe locking, and O(n) hot lookups | Made state instance-owned, used one stable lock, monotonic checked IDs, and bidirectional O(1) dictionaries |
| 4 | QLNet option calculations mutated process-global evaluation settings concurrently | Different actor entities could race and calculate against the wrong date/settings | Serialized the full calculation around QLNet global `Settings`; actor concurrency remains outside the critical section |
| 5 | Tick, bar, and option-quote state reconstruction replayed all insert events even though retained state only needs the latest snapshot | Replay time and allocation grew with the immutable stream | Switched those repositories to typed snapshot plus last-N replay with `N = 0`, preserving unbounded storage while making selected managed replay work constant-size |
| 6 | Option-quote start snapshots were deliberately ignored by an always-false branch | Reconstructed state lost active subscriptions, so valid stop/insert commands could not use snapshot state | Restored snapshot population and reconstruction while retaining the rule that a successful command need not change state |
| 7 | Option trade live-feed reverse lookup always allocated and returned an empty result | Incorrect downstream mapping plus needless allocation | Implemented concurrent-map value matching and allocate-on-first-match result construction |
| 8 | Independent query/storage calls were awaited sequentially | End-to-end latency was the sum of unrelated I/O latencies | Started independent iron-condor legs, EOD parameter reads, and moving-average reads together and awaited them as a group |
| 9 | EOD statistics used LINQ, MathNet estimation, intermediate lists/arrays, and unused high/low calculations | Avoidable Gen0 pressure and repeated enumeration in scheduled calculations | Added single-pass Welford sample deviation, scalar volatility calculation, direct bounded arrays, and removed MathNet from the EOD statistics path |
| 10 | Command-state `Apply` methods swallowed all exceptions and high-frequency feeds emitted console/info logs per tick | Corrupt supported events could silently disappear; logging dominated hot paths and increased contention | Removed broad empty catches so genuine failures reach the actor pipeline, removed console output, and moved accepted-tick diagnostics to debug level |

## Additional complexity reductions

Actor startup/receive methods that do not suspend now return completed `ValueTask` values rather than carrying unnecessary async state machines. Timer callback delegates that do no work are static completed `ValueTask` lambdas. The broker snapshot semaphore remains intentionally in place because the broker client's concurrency contract has not yet been proven safe.

## BenchmarkDotNet results

Measurements used BenchmarkDotNet 0.15.8 on .NET 10.0.10 (SDK 10.0.302), Windows 10 22H2, AMD Ryzen Threadripper 1950X, 16 physical/32 logical cores, x64 RyuJIT, concurrent workstation GC. Jobs used three warmups and eight measured iterations. Times are means; allocations are managed bytes per operation.

### Stream identifier reverse lookup

| Entries | Before | After | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 128 | 2,451.11 ns | 19.91 ns | 123.1x | 80 B | 0 B |
| 4,096 | 76,979.85 ns | 19.75 ns | 3,898x | 24 B | 0 B |
| 32,768 | 615,717.48 ns | 20.64 ns | 29,832x | 144 B | 0 B |

The optimized lookup remains approximately constant as the collection grows. The 4,096 and 32,768 optimized cases were repeated after Windows Defender interrupted the first combined process; the table merges the completed baseline and repeat measurements.

### EOD sample standard deviation

| Window | Before | After | Time reduction | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 20 | 163.9 ns | 106.3 ns | 35.1% | 48 B | 0 B |
| 50 | 358.9 ns | 266.0 ns | 25.9% | 48 B | 0 B |
| 200 | 1,340.2 ns | 1,086.5 ns | 18.9% | 48 B | 0 B |
| 1,000 | 6,596.1 ns | 5,457.6 ns | 17.3% | 48 B | 0 B |

The before case uses the prior MathNet `Normal.Estimate` and LINQ path. The after case is the production single-pass Welford implementation.

### Independent asynchronous I/O fan-out

| Independent operations | Before sequential | After concurrent | Latency reduction | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 3 | 46.50 ms | 15.46 ms | 66.8% (3.01x) | 992 B | 1,208 B |
| 5 | 77.60 ms | 15.55 ms | 80.0% (4.99x) | 1,536 B | 1,792 B |

This is a controlled `Task.Delay` workload. The host's timer quantum produced approximately 15.5 ms per simulated operation. It measures orchestration shape, not database or network latency. The small task-array allocation increase is accepted in exchange for removing additive I/O latency.

### Snapshot-only managed replay scaling

| Insert events after snapshot | Before snapshot-to-end | After snapshot-only | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 256 | 170.13 us | 0.581 us | 293x | 736.66 KB | 2.84 KB |
| 4,096 | 2.477 ms | 0.624 us | 3,971x | 11,746.66 KB | 2.84 KB |
| 32,768 | 21.781 ms | 0.612 us | 35,571x | 93,954.66 KB | 2.84 KB |

This benchmark isolates managed JSON deserialization and replay scaling. SQL/index traversal and network time are excluded. It demonstrates why selecting only the snapshot for state types that retain no inserts prevents managed replay cost from growing with the unbounded stream.

## Verification

- Baseline before edits: 548 unit tests passed and 353 BDD tests passed.
- Final Release verification: 557 unit tests passed, 353 BDD tests passed, and both production and benchmark projects built with zero warnings and zero errors.
- The integration suite compiled and started before edits but timed out at 180 seconds while waiting on external actor infrastructure, with missing-event failures already present. It is recorded as an environment-dependent baseline rather than used as a regression signal.
- Deterministic coverage was added for per-entity timer lifetime/non-overlap/shutdown, stream-ID isolation and concurrency, option-quote snapshot behavior, option live-feed mapping, and EOD statistics/window bounds.
- Reproduce benchmarks from the repository root with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Feed.Benchmarks -- --filter *
```

## Deferred work

End-to-end cancellation is intentionally deferred. It must be implemented solution-wide so a supervisor cancellation token flows consistently through actor dispatch, APIs, repositories, storage, broker operations, timers, and external I/O, with graceful shutdown semantics and no partial persistence. Applying cancellation to this domain alone would create an incomplete contract.

Future regular optimization passes should retain these benchmarks, add production/paper-trading latency and allocation telemetry, and only pursue dispatch-table or similar micro-optimizations after a measured hot spot is observed.
