# Domain.MarketData actor optimization report

## Review record

| Field | Value |
|---|---|
| Review date | 2026-08-05 |
| Project | `TomasAI.IFM.Domain.MarketData` |
| Baseline | Repository commit `7596fc6` |
| Runtime | .NET 10.0.10, X64 RyuJIT |
| Benchmark tool | BenchmarkDotNet 0.15.8 |
| Primary objective | Complete the top-ten optimization pass for the MarketData actors after migration of the trading-system core to actor-based concurrency |
| Status | Implemented and verified; paper-trading measurement remains the authority for further optimization |

## Scope and optimization boundary

This review covered the MarketData domain from its actor entry points through command validation, event-sourced state, repositories, projections, query APIs, and the storage operations directly used by those paths. The reviewed actors were:

- `MarketDataQueryActor`;
- `YieldCurveRateCommandActor`;
- `YieldCurveRateQueryActor`; and
- `YieldCurveRateEventActor`.

The optimization categories were garbage collection, threading, locking, async/sync usage, memory utilization, concurrency correctness, throughput, and code complexity.

This was a top-ten structural optimization pass, not an attempt to minimize every instruction. Further microoptimization should be driven by end-to-end measurements collected during paper trading. In particular, message deserialization, storage latency, NATS latency, mailbox pressure, and downstream consumers will dominate nanosecond-scale dispatch differences.

## Design contracts preserved

- `YieldCurveRateEventActor` remains an intentionally empty same-domain event sink. Command actors may publish to the domain's default event actor even when no downstream behavior is currently required.
- A command state's `Update` result reports whether state changed. It does not report command success. A command can succeed without changing state.
- Cancellation propagation was not partially added. Graceful supervisor-driven cancellation requires a later solution-wide change spanning actor contracts, repositories, storage providers, and network operations.
- Actor ordering and event-sourcing behavior remain unchanged.

## Top ten findings and changes

| # | Area | Finding | Implemented change | Expected effect |
|---:|---|---|---|---|
| 1 | Threading / async | Command audit logging blocked synchronously with `GetAwaiter().GetResult()`. | Audit persistence is awaited before validation. | Removes actor-thread blocking and sync-over-async deadlock/starvation risk. |
| 2 | Memory / GC | Yield-curve command state retained complete rate models although command decisions only require date membership. | Replaced the model dictionary with a capacity-sized `HashSet<DateOnly>`. | Substantially reduces retained memory, allocations, and replay GC pressure. |
| 3 | Validation / GC | A complete FluentValidation rule graph was constructed for every rate. | Reuse one stateless validator and verify concurrent execution. | Reduces validation latency and allocation volume. |
| 4 | Dispatch / complexity | Actor parsing and handling used static string dictionaries and indirect delegates. | Replaced them with typed switches. | Simplifies control flow and removes string hashing/delegate indirection from the four- and five-case actor routes. |
| 5 | Async / complexity | Several wrappers created async state machines solely to await and return another operation. | Return `ValueTask` directly where type compatibility and post-await behavior permit it. | Reduces state-machine overhead and simplifies leaf methods. |
| 6 | Projection writes | Yield-curve changes deleted and then inserted the same Cassandra row. | Use the keyed insert as a single Cassandra upsert. | Halves writes for changes and removes the transient missing-row interval. |
| 7 | Storage fan-out | Futures-option ID filtering performed one database request per input ID. | Added one bulk `IN` read and restored caller order and duplicates after lookup. | Converts N+1 reads into one storage round trip. |
| 8 | Query concurrency | Iron-condor market data performed seven independent reads serially. | Start four independent aggregate reads together and await them as a group. | Shortens critical-path latency without concurrently sharing a context instance. |
| 9 | Materialization | Trading-day queries built a complete date array only to read its length. | Added a count-only storage path using a holiday-date set and one range pass. | Removes the result array and its associated population cost. |
| 10 | Regression visibility | Actor hot paths lacked dedicated allocation, concurrency, and fan-out measurements. | Added a BenchmarkDotNet project plus state, validation, bulk-read, and concurrent-start tests. | Makes future regressions measurable and repeatable. |

## Benchmark summary

The primary benchmarks use three warmups and eight measured iterations on the same AMD Ryzen Threadripper 1950X host. They isolate actor CPU and allocation behavior; they do not simulate database or NATS latency.

| Benchmark | Size | Before | After | Latency reduction | Allocation before | Allocation after | Allocation reduction |
|---|---:|---:|---:|---:|---:|---:|---:|
| State import replay | 32 | 938.6 ns | 267.8 ns | 71.5% | 5.94 KB | 872 B | 85.7% |
| State import replay | 256 | 6,960.0 ns | 1,873.3 ns | 73.1% | 51.95 KB | 4,968 B | 90.7% |
| State import replay | 2,048 | 292,381.2 ns | 15,433.8 ns | 94.7% | 450.75 KB | 37,608 B | 91.9% |
| Import validation | 1 | 17.50 us | 812.7 ns | 95.4% | 35.67 KB | 888 B | 97.6% |
| Import validation | 32 | 557.93 us | 24.03 us | 95.7% | 1,144 KB | 19,200 B | 98.4% |
| Import validation | 256 | 4,472.05 us | 192.45 us | 95.7% | 9,160 KB | 153,600 B | 98.4% |

At 2,048 rates, replay is 18.9 times faster. Gen2 collections observed in the baseline were eliminated, and Gen1 collections fell from 60.0586 to 0.5493 per 1,000 operations. The threading diagnoser reported no scheduled work items or lock contention in these CPU-only benchmarks.

### Dispatch experiment and stopping point

A separate benchmark isolated the routing portion of `YieldCurveRateCommandActor.ParseMessage` using a balanced sequence of its four verbs and pre-materialized commands.

| Strategy | Mean per command | Relative to switch | Allocated |
|---|---:|---:|---:|
| Current string switch | 17.27 ns | Baseline | 0 B |
| Former static dictionary and delegate | 23.58 ns | 36.6% slower | 0 B |
| Collision-safe perfect-hash jump table | 16.99 ns | 1.6% faster | 0 B |

The switch and perfect-hash confidence intervals overlap. The jump table therefore was not introduced into production: its generated-table maintenance burden is not justified by the measured difference. A fully direct table would also require ingress to translate strings into stable dense verb identifiers before `ParseMessage`.

## Verification completed

- 38 `TomasAI.IFM.Domain.MarketData.UnitTests` tests passed.
- 5 storage CQL positional-parameter contract tests passed.
- The MarketData integration-test dependency graph built in Release with zero warnings and zero errors.
- The BenchmarkDotNet project built in Release with zero warnings and zero errors.
- `git diff --check` passed.
- Regression tests cover imported-state replay, removal/re-add behavior, concurrent validator reuse, one-call option bulk lookup, input-order preservation, and concurrent start of independent Iron Condor reads.

Integration tests requiring live storage or messaging infrastructure were not used as microbenchmarks. Their latency and reliability should be assessed in the paper-trading environment.

## Paper-trading measurements for the next review

The next optimization pass should begin with runtime evidence rather than the current source layout. Capture at least:

- per-actor message throughput and handler latency at p50, p95, p99, and maximum;
- mailbox depth, oldest-message age, rejected writes, and drain time;
- allocation rate, Gen0/Gen1/Gen2 frequency, GC pause time, heap size, and large-object-heap growth;
- thread-pool queue length, worker availability, starvation events, and lock contention;
- command validation, state replay, event persistence, projection, and publication durations separately;
- database request count, latency distribution, timeouts, retries, and connection-pool pressure;
- NATS publish/consume latency, redelivery, pending messages, and payload sizes;
- actor exceptions, retries, duplicate commands/events, and idempotency outcomes; and
- process CPU, working set, sustained throughput, and shutdown/drain behavior under realistic market bursts.

Use representative scenarios: normal market flow, opening and closing bursts, yield-curve imports, reconnect/replay, transient storage latency, and supervisor shutdown.

## Repeatable optimization-review procedure

1. Record the date, baseline commit, runtime, hardware, configuration, and data shape.
2. Capture paper-trading traces and rank the ten highest-impact problems by measured cost and operational risk.
3. Separate architectural issues from microoptimizations and correctness fixes.
4. Add deterministic benchmarks for CPU/allocation hot paths and integration measurements for I/O paths.
5. Capture the untouched baseline before changing production code.
6. Make one conceptually isolated change at a time and preserve actor ordering, event-sourcing, and command-success contracts.
7. Add regression and concurrency tests for every behavior or scheduling change.
8. Rerun benchmarks on the same host/configuration and report absolute numbers, percentages, allocations, and GC generations.
9. Validate under paper trading and compare p95/p99 latency, throughput, resource use, and error rates with the baseline.
10. Record accepted changes, rejected experiments, deferred cross-cutting work, and the evidence-based stopping point in a dated report under `Docs/`.

## Deferred solution-wide work

Graceful cancellation remains the principal cross-cutting follow-up after all root domain optimization passes are complete. Its design must cover supervisor deadlines, mailbox draining, in-flight handler cancellation, caller-versus-server cancellation semantics, repository/storage/network token propagation, and consistency when cancellation intersects event persistence or publication.

## Related artifacts

- Detailed MarketData actor implementation notes: `TomasAI.IFM.Domain.MarketData/Docs/Domain-Actor-Implementation-Details.md`
- Benchmark source and exact results: `TomasAI.IFM.Domain.MarketData.Benchmarks/`
- Summary benchmark report: `TomasAI.IFM.Domain.MarketData.Benchmarks/RESULTS.md`
- Dispatch experiment: `TomasAI.IFM.Domain.MarketData.Benchmarks/YieldCurveRateParseDispatchBenchmarks.cs`
