# MarketData actor optimization results

Baseline source: repository commit `7596fc6`, before the production-code changes in this optimization pass. Both runs used BenchmarkDotNet 0.15.8, .NET 10.0.10, X64 RyuJIT, three warmups, eight measured iterations, and the same Windows/AMD Threadripper 1950X host.

## Before and after

| Benchmark | Size | Before mean | After mean | Latency reduction | Before allocated | After allocated | Allocation reduction |
|---|---:|---:|---:|---:|---:|---:|---:|
| State import replay | 32 | 938.6 ns | 267.8 ns | 71.5% | 5.94 KB | 872 B | 85.7% |
| State import replay | 256 | 6,960.0 ns | 1,873.3 ns | 73.1% | 51.95 KB | 4,968 B | 90.7% |
| State import replay | 2,048 | 292,381.2 ns | 15,433.8 ns | 94.7% | 450.75 KB | 37,608 B | 91.9% |
| Import validation | 1 | 17.50 us | 812.7 ns | 95.4% | 35.67 KB | 888 B | 97.6% |
| Import validation | 32 | 557.93 us | 24.03 us | 95.7% | 1,144 KB | 19,200 B | 98.4% |
| Import validation | 256 | 4,472.05 us | 192.45 us | 95.7% | 9,160 KB | 153,600 B | 98.4% |

At the largest state size, replay is 18.9 times faster, eliminates Gen2 collections observed in the baseline, and reduces Gen1 collections from 60.0586 to 0.5493 per 1,000 operations. The threading diagnoser reported no work-item scheduling or lock contention in either benchmark, as expected for these CPU-only actor hot paths.

Database and NATS latency are intentionally excluded from microbenchmarks. Bulk-read count, call ordering, and concurrent query fan-out are covered by unit tests; production latency still depends on the configured storage and network services.

## Top ten issues found

1. Command audit persistence used sync-over-async (`GetAwaiter().GetResult()`), blocking an actor thread and risking starvation or deadlock. Logging is now awaited in the validation pipeline before validation runs.
2. State replay retained complete yield-curve models even though command decisions only need value-date membership. A capacity-sized `HashSet<DateOnly>` now removes object retention and repeated dictionary growth.
3. Import validation constructed an entire FluentValidation object graph for every rate. The stateless validator is now cached and has explicit concurrent-use regression coverage.
4. Command and query parsing used static dictionaries with string hashing and indirect delegate calls. Typed switches simplify dispatch and measured faster for the four yield-curve command verbs; neither approach allocates per call.
5. Simple query wrappers created unnecessary async state machines, so genuinely completed paths use direct `ValueTask` propagation. Repository `LoadStateAsync` and `SaveStateAsync` methods intentionally retain explicit `async`/`await`; their forwarding overhead is insignificant beside storage I/O and the explicit flow is easier to read.
6. Yield-curve changes issued delete-then-insert projection writes. Cassandra's keyed upsert now performs the same logical update with one write and without a transient missing row.
7. Futures option ID lookup made one storage round trip per input ID. A single `IN` query now performs the bulk lookup while the API preserves the caller's original order and duplicates.
8. Iron-condor lookup performed seven independent reads serially. Four independent aggregate reads now start together, reducing round trips and end-to-end wait time without sharing a context instance.
9. Trading-day count materialized a complete date array only to read its length. A count-only storage operation now uses a holiday set and a single date-range pass.
10. Hot-path behavior lacked focused performance and concurrency safeguards. A dedicated BenchmarkDotNet project and state, validator, bulk-read, and concurrent-fan-out tests now make regressions visible.

## Preserved design contracts

- `YieldCurveRateEventActor` remains an intentionally empty same-domain event sink.
- A command may succeed when its state's `Update` result is `false`; that boolean means state changed, not operation succeeded.
- Cancellation propagation is not partially introduced here. It is documented as a solution-wide graceful-shutdown TODO in the implementation-details document.

## ParseMessage dispatch experiment

The benchmark-only `YieldCurveRateParseDispatchBenchmarks` compares the routing portion of `YieldCurveRateCommandActor.ParseMessage` with a balanced sequence of all four current verbs. Commands are pre-materialized so MessagePack deserialization does not conceal the dispatch cost.

| Strategy | Mean per command | Ratio | Allocated |
|---|---:|---:|---:|
| Current string switch | 17.27 ns | 1.00 | 0 B |
| Former static dictionary and delegate | 23.58 ns | 1.37 | 0 B |
| Collision-safe perfect-hash jump table | 16.99 ns | 0.98 | 0 B |

The prototype hashes `(verb[0] ^ verb.Length) & 7` into an eight-slot table. The current verbs occupy unique slots, and the selected entry performs an ordinal comparison before invoking its parser, preventing unknown or colliding verbs from being misrouted.

The jump table was only 1.6% faster than the switch, and their 99.9% confidence intervals overlap. That is not a compelling production improvement for four verbs; it adds a generated-table maintenance requirement and must be rebuilt whenever verbs change. The benchmark prototype is retained for experimentation, but production remains on the string switch.
