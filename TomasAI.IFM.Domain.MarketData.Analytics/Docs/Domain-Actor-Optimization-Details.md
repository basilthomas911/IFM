# Market Data Analytics actor optimization details

## Scope

This pass reviewed the 21 command, event, and query actors under the ADX, ATR, ITI, MACD, RSI, TDI, and Trade Signal roots, including their state, calculation, validation, repository, event-orchestration, and query API leaves. The baseline was repository commit `c5419d7`; 743 unit tests passed before production changes.

Persisted event history remains intentionally unbounded. The shared storage follow-up now applies database-bounded typed recovery to RSI, MACD, ADX, and ATR, including their daily variants.

## Top ten issues and outcomes

1. **RSI timer registry races.** Two ordinary static dictionaries were read and modified without synchronization. A `ConcurrentDictionary` now owns one registration per entity and duplicate starts are idempotent.
2. **Overlapping and unobservable timer callbacks.** An async lambda was converted to `async void` through `Action<T>`, while periodic `Timer` callbacks could overlap. The scheduler now accepts `Func<T, ValueTask>`, awaits each callback, drains in-flight work on stop, and stops all registrations during actor shutdown.
3. **Sync-over-async command audit logging.** Seven command actors blocked their actor thread with `GetAwaiter().GetResult()`. Logging is awaited in `OnValidateAsync` before validation, preserving audit-before-validation ordering.
4. **Complete RSI state copied on every access.** `FuturesRsiSignalCommandState.FuturesRsiSignals` allocated a new array containing the entire retained history for every command. It now exposes the existing collection through `IReadOnlyCollection` without deleting or truncating history.
5. **Indicator calculations allocated full transient arrays.** ATR, ADX, and MACD projected all prices and allocated one or more intermediate arrays. Single-pass calculations now preserve formula ordering while allocating only the result model.
6. **TDI repeatedly sorted and rescanned its window.** Already ordered inputs now avoid a copy/sort; unsorted inputs retain the defensive sort. Five-minute counts and direction are calculated once per model.
7. **Independent query reads ran serially.** ITI signal-data reads and up/down MDI reads now start together and await as a group. Deterministic tests prove every read starts before any result is released.
8. **Independent event requests ran serially.** ITI completion handlers now overlap independent EOD, RSI, TDI, ITI, and VIX actor requests, then perform the dependent command only after all results are available.
9. **Stateless validators were rebuilt per call.** Nine FluentValidation rule graphs are cached as immutable static instances. Representative validation benchmarks show substantially lower latency and allocation, with concurrent execution covered by a regression test.
10. **Replay failures were silently converted to no-change.** Empty catch blocks were removed from all seven command states. Unsupported events still return `false`; malformed supported events now reach the actor error path instead of looking like a legitimate no-op.

Additional complexity cleanup removed unnecessary command-receive async state machines and cached the latest of the three ITI TradeSignal inputs without iterator and sorting allocations. Dispatch maps were deliberately retained because prior measurement showed switch/jump-table selection is a microoptimization at this verb count.

## Benchmark environment

- BenchmarkDotNet 0.15.8
- .NET SDK 10.0.302; .NET runtime 10.0.10; X64 RyuJIT x86-64-v3
- Windows 10 22H2
- AMD Ryzen Threadripper 1950X, 16 physical/32 logical cores
- Three warmups and eight measured iterations
- `MemoryDiagnoser` and `ThreadingDiagnoser`

The benchmark project retains the original calculation and validator construction implementations as `Before` methods. Both versions therefore run with identical inputs in the same BenchmarkDotNet job. Database and NATS latency are excluded from microbenchmarks and covered with behavioral/concurrency tests instead.

## Before and after results

| Benchmark | Count | Before mean | After mean | Latency reduction | Before allocated | After allocated | Allocation reduction |
|---|---:|---:|---:|---:|---:|---:|---:|
| ATR calculation | 32 | 277.2 ns | 160.5 ns | 42.1% | 648 B | 48 B | 92.6% |
| ATR calculation | 256 | 2,361.7 ns | 1,359.8 ns | 42.4% | 4,232 B | 48 B | 98.9% |
| ATR calculation | 2,048 | 18,622.6 ns | 10,983.6 ns | 41.0% | 32,904 B | 48 B | 99.9% |
| ADX calculation | 32 | 639.9 ns | 232.3 ns | 63.7% | 1,288 B | 56 B | 95.7% |
| ADX calculation | 256 | 5,617.4 ns | 1,773.3 ns | 68.4% | 8,456 B | 56 B | 99.3% |
| ADX calculation | 2,048 | 46,363.9 ns | 14,044.1 ns | 69.7% | 65,800 B | 56 B | 99.9% |
| MACD calculation | 32 | 378.2 ns | 198.9 ns | 47.4% | 608 B | 88 B | 85.5% |
| MACD calculation | 256 | 3,520.0 ns | 1,268.8 ns | 64.0% | 4,192 B | 88 B | 97.9% |
| MACD calculation | 2,048 | 28,439.2 ns | 9,921.6 ns | 65.1% | 32,864 B | 88 B | 99.7% |
| RSI entity validation | 1 | 7.394 us | 1.484 us | 79.9% | 13,538 B | 920 B | 93.2% |
| RSI entity validation | 32 | 236.344 us | 47.583 us | 79.9% | 432,451 B | 30,213 B | 93.0% |
| RSI entity validation | 256 | 1,874.688 us | 385.398 us | 79.4% | 3,465,758 B | 241,702 B | 93.0% |

At 2,048 signals, the optimized indicator methods eliminated the Gen1 collections observed in each baseline. The threading diagnoser reported no work-item scheduling or lock contention for these CPU-only methods, as expected.

## Snapshot range recovery results

The new storage benchmark compares the former snapshot-to-stream-end managed replay with the new snapshot-plus-last-60-typed-events replay. PostgreSQL execution and network latency are excluded from this microbenchmark and covered by integration tests.

| Matching events after snapshot | Before mean | After mean | Speedup | Before allocated | After allocated |
|---:|---:|---:|---:|---:|---:|
| 256 | 7.468 ms | 990.3 us | 7.54x | 3,707.57 KB | 462.87 KB |
| 4,096 | 119.246 ms | 973.7 us | 122.47x | 59,213.60 KB | 462.87 KB |
| 32,768 | 958.839 ms | 998.2 us | 960.57x | 473,657.38 KB | 462.87 KB |

The optimized replay remains effectively constant once at least 60 matching events are available. Full environment and allocation/GC results are retained in `TomasAI.IFM.Application.Storage.Benchmarks/RESULTS.md`.

### Period signal and daily variant matrix

The follow-up benchmark applies the same controlled workload to RSI, MACD, ADX, and ATR, including all daily variants: 4,096 matching events interleaved with 4,096 unrelated events before, versus the typed last 60 selected by PostgreSQL after.

| Variant | Before mean | After mean | Speedup | Before allocated | After allocated |
|---|---:|---:|---:|---:|---:|
| RSI | 122.332 ms | 965.7 us | 126.68x | 59,213.60 KB | 462.87 KB |
| RSI daily | 123.115 ms | 953.9 us | 129.06x | 58,950.74 KB | 452.26 KB |
| MACD | 119.015 ms | 940.6 us | 126.53x | 58,541.08 KB | 446.22 KB |
| MACD daily | 111.749 ms | 912.6 us | 122.45x | 58,317.08 KB | 442.94 KB |
| ADX | 117.626 ms | 923.5 us | 127.37x | 58,540.79 KB | 445.93 KB |
| ADX daily | 113.549 ms | 891.3 us | 127.40x | 58,284.79 KB | 442.18 KB |
| ATR | 117.352 ms | 907.1 us | 129.37x | 58,511.95 KB | 445.48 KB |
| ATR daily | 115.578 ms | 880.3 us | 131.29x | 58,027.95 KB | 438.43 KB |

Every variant reduced managed allocation by approximately 99.2%. The prior repositories used different recovery behavior, so this is a normalized scaling comparison rather than a claim that every old path replayed the full mixed history on every request. It excludes PostgreSQL/network time; integration tests verify that filtering and limiting happen in SQL.

## Concurrency and regression coverage

- RSI timer duplicate-start idempotency.
- MACD, ADX, and ATR intraday timers now have the same duplicate-start, non-overlap, stop-drain, and actor-shutdown behavior; their daily variants remain outside recurring lifecycle scheduling.
- Per-entity timer callbacks never overlap.
- Stop waits for an in-flight callback and prevents later ticks.
- ITI query reads all start before any delayed fake is released.
- All seven query actors propagate their worker cancellation token through handlers and 17 PostgreSQL read-model leaves.
- Canceled query work publishes no reply; focused RSI and composite ITI tests cover the single-read and concurrent-read paths.
- Cached validation rules execute safely under concurrent callers.
- Command audit writes remain observable and failures propagate asynchronously.
- Existing command/event/query behavior remains covered by 760 passing unit tests and 449 passing BDD tests.
- PostgreSQL integration coverage proves latest-snapshot selection, typed filtering before limiting, ascending replay order, snapshot boundaries, missing snapshot/range behavior, nonpositive ranges, and exact/fewer/more-than-N ranges.

## Repeatable review process

1. Run the Analytics unit suite in Release mode.
2. Run `dotnet run --project TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks -c Release -- --filter "*IndicatorBenchmarks*" "*ValidationBenchmarks*"`.
3. Compare paper-trading actor mailbox latency, request rate, allocation rate, Gen0/Gen1 frequency, timer backlog, and storage latency against this report.
4. Add a benchmark only for a measured hot path; retain before implementations or a clean baseline artifact so comparisons remain reproducible.
5. Run `dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release -- --filter "*SnapshotRangeReplayBenchmarks*" "*PeriodSignalReplayBenchmarks*"` and compare database latency separately under paper-trading load.
