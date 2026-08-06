# Domain.MarketData actor optimization details

Date: 2026-08-06

## Scope and invariants

The recurring review covers the root MarketData query actors and API plus the YieldCurveRate command, event, query, validation, state, and repository paths. Specialized Analytics, Feed, and Securities actors have their own optimization reports.

The following contracts remain unchanged:

- `YieldCurveRateEventActor` is an intentionally empty same-domain event sink.
- A command can succeed without changing state; `Update` reports state change, not command success.
- Event history remains immutable and unbounded.
- Repository storage forwarding retains explicit `async`/`await` because its overhead is insignificant beside I/O.
- Cancellation remains a solution-wide follow-up.
- The measured string switch remains in production; the benchmark-only jump table is not justified by the current variance.

## Top ten findings and disposition

1. **Synchronous command-audit persistence - fixed.** Audit storage moved out of synchronous parsing and is awaited in the actor pipeline.
2. **Allocation-heavy yield-curve state - fixed.** State retains value-date membership in a capacity-sized `HashSet<DateOnly>` instead of complete rate models and repeated dictionary growth.
3. **Per-rate FluentValidation graph construction - fixed.** The stateless validator graph is cached and covered for concurrent use.
4. **Dictionary/delegate actor dispatch - fixed.** Typed switches replaced string dictionaries on the small command and query verb sets.
5. **Unnecessary async state machines - fixed selectively.** Completed query paths propagate `ValueTask` directly; storage-facing repository methods intentionally remain explicit awaits.
6. **Delete-then-insert projection update - fixed.** The keyed Cassandra write uses one upsert without a transient missing row.
7. **N+1 futures-option identifier lookup - fixed.** One bulk `IN` query preserves caller order and duplicates.
8. **Sequential iron-condor reads - fixed.** Independent aggregate queries start together and are awaited as a group.
9. **Trading-day array materialization - fixed.** A count-only path uses a holiday set and one date-range pass.
10. **Dispatch micro-optimization and cancellation - deferred.** The jump-table result is statistically neutral, while cancellation requires a coordinated supervisor-to-storage change.

## Benchmark summary

BenchmarkDotNet 0.15.8, .NET 10.0.10, x64 RyuJIT, AMD Ryzen Threadripper 1950X, three warmups and eight measured iterations unless noted otherwise.

| Hot path | Input | Before | After | Improvement | Allocation before | Allocation after |
|---|---:|---:|---:|---:|---:|---:|
| State import replay | 32 rates | 938.6 ns | 267.8 ns | 71.5% | 5.94 KB | 872 B |
| State import replay | 256 rates | 6,960.0 ns | 1,873.3 ns | 73.1% | 51.95 KB | 4,968 B |
| State import replay | 2,048 rates | 292,381.2 ns | 15,433.8 ns | 94.7% | 450.75 KB | 37,608 B |
| Import validation | 1 rate | 17.50 us | 812.7 ns | 95.4% | 35.67 KB | 888 B |
| Import validation | 32 rates | 557.93 us | 24.03 us | 95.7% | 1,144 KB | 19,200 B |
| Import validation | 256 rates | 4,472.05 us | 192.45 us | 95.7% | 9,160 KB | 153,600 B |

At the largest replay size, the optimized implementation is 18.9 times faster, removes the baseline Gen2 collections, and reduces allocation by 91.9%. The deterministic CPU benchmarks recorded no monitor-lock contention.

### Parse-dispatch experiment

| Strategy | Mean per command | Ratio | Allocated |
|---|---:|---:|---:|
| Current string switch | 17.27 ns | 1.00 | 0 B |
| Former dictionary/delegate | 23.58 ns | 1.37 | 0 B |
| Collision-safe jump table | 16.99 ns | 0.98 | 0 B |

The jump table was only 1.6% faster than the switch and their 99.9% confidence intervals overlap. It remains benchmark-only until paper-trading profiles show parsing is material.

Full tables and methodology are in `TomasAI.IFM.Domain.MarketData.Benchmarks/RESULTS.md`.

### 2026-08-06 regression run

All nine current benchmark cases completed on the same host. Replay means were 234.45 ns, 1,879.69 ns, and 14,451.47 ns for 32, 256, and 2,048 rates respectively. The dispatch experiment measured 15.02 ns for the switch, 23.83 ns for the dictionary/delegate route, and 16.03 ns for the jump table, so the production switch remains the fastest measured choice in this run.

An all-suite joined run initially attributed excess validation allocation to the 256-item case. A clean validation-only rerun measured 600 B, 19,200 B, and 153,600 B for 1, 32, and 256 rates, confirming linear 600-byte-per-rate allocation and no repeatable regression. Its means were 750.9 ns, 23.97 us, and 192.93 us.

## Verification

- Root MarketData unit tests cover command state, validation, query actors, API delegation, bulk lookup ordering, and concurrent query fan-out.
- The BenchmarkDotNet project is part of the solution and runs out of process in Release mode.
- The 2026-08-06 regression run completed all nine configured cases; the isolated validation confirmation completed all three parameter sizes.
- Root MarketData unit tests: 38 passed in Release configuration.
- Storage and NATS latency are intentionally excluded from microbenchmarks; production performance must be evaluated with mailbox and storage telemetry.

## Deferred work

- Propagate graceful cancellation solution-wide after the root-domain optimization sequence.
- Add paper-trading mailbox latency, allocation rate, storage round-trip, and queue-depth telemetry.
- Retain the current switch until measured runtime profiles justify a generated jump table.
