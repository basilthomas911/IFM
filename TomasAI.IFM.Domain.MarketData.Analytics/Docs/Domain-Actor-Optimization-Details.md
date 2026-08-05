# Market Data Analytics actor optimization details

## Scope

This pass reviewed the 21 command, event, and query actors under the ADX, ATR, ITI, MACD, RSI, TDI, and Trade Signal roots, including their state, calculation, validation, repository, event-orchestration, and query API leaves. The baseline was repository commit `c5419d7`; 743 unit tests passed before production changes.

Persisted event history remains intentionally unbounded. Snapshot-plus-last-N-range recovery is documented as a separate shared-storage follow-up and was not implemented here.

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

## Concurrency and regression coverage

- RSI timer duplicate-start idempotency.
- Per-entity timer callbacks never overlap.
- Stop waits for an in-flight callback and prevents later ticks.
- ITI query reads all start before any delayed fake is released.
- Cached validation rules execute safely under concurrent callers.
- Command audit writes remain observable and failures propagate asynchronously.
- Existing command/event/query behavior remains covered by 749 passing unit tests and 449 passing BDD tests in Release mode.

## Repeatable review process

1. Run the Analytics unit suite in Release mode.
2. Run `dotnet run --project TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks -c Release -- --filter "*IndicatorBenchmarks*" "*ValidationBenchmarks*"`.
3. Compare paper-trading actor mailbox latency, request rate, allocation rate, Gen0/Gen1 frequency, timer backlog, and storage latency against this report.
4. Add a benchmark only for a measured hot path; retain before implementations or a clean baseline artifact so comparisons remain reproducible.
5. Revisit snapshot-last-N recovery separately because its shared storage contract and end-to-end replay metrics require a solution-wide change.
