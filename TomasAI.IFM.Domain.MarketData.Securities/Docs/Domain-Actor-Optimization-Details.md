# Market Data Securities Domain Actor Optimization

## Scope and constraints

This report records the root-to-leaf optimization pass completed on 2026-08-05 for `TomasAI.IFM.Domain.MarketData.Securities` and its directly consumed shared validation path. The review covered garbage collection, threading, locking, async/sync patterns, memory use, concurrency, latency, throughput, and code complexity.

The following domain rules were preserved:

- Event histories remain immutable and unbounded.
- State recovery starts from the snapshot type required by the command stream.
- Missing snapshots or requested event types yield the best available, possibly empty, state; application code does not manufacture replay exceptions.
- A successful command is distinct from a state change and need not emit an event.
- Empty event actors remain intentional default publication targets.
- Dispatch dictionaries remain in place; jump-table micro-optimization is deferred until paper-trading telemetry identifies a material hot spot.
- Query cancellation now flows through the Securities actors, handlers, projection-consistency reads, and direct aggregate MarketData API; durable projection repair remains non-cancelable after its mutation boundary.

## Top ten issues found and resolved

| Rank | Issue found | Impact | Resolution |
|---:|---|---|---|
| 1 | Both command actors synchronously waited on audit persistence during message parsing | Blocked actor execution, introduced sync-over-async deadlock risk, and coupled mailbox latency to storage | Added a command-scoped audit tracker: parsing starts persistence without blocking and validation joins/observes it before command processing continues |
| 2 | Bulk option commands restored state using the singular option-added snapshot type | The required bulk snapshot could not match, allowing replay work to grow with the unbounded stream | Selected `FuturesOptionContractsAddedEvent` for bulk-add commands and retained the singular snapshot for individual commands |
| 3 | Option-ID queries performed one storage call per requested ID | N+1 I/O caused latency and work-item count to grow linearly | Replaced the loop with one bulk storage read and an ordinal set lookup while preserving input order and duplicates |
| 4 | Bulk option insertion enriched and persisted every contract serially | End-to-end latency was the sum of every actor request and write | Enrichment now runs in bounded chunks of eight and all enriched contracts are persisted through one batch operation |
| 5 | Option overwrite checks treated `overwrite = true` as an existence failure | Valid idempotent add/change/remove workflows could fail incorrectly | Corrected the predicates so overwrite bypasses only the matching existence or missing-state guard |
| 6 | Command state stored full contract wrapper objects although command decisions only require identifiers | Replay retained redundant strings, dates, prices, and objects and increased GC pressure | Replaced dictionaries of wrappers with identifier-only `HashSet` state using ordinal comparison for string IDs |
| 7 | State application used `ContainsKey` plus remove/add sequences and swallowed all exceptions | Multiple hash probes increased work, while malformed supported events could silently look like no change | Applied idempotent `HashSet.Add`/`Remove` directly and removed broad empty catches so genuine failures reach the actor pipeline |
| 8 | Option contract IDs were formatted and allocated on every property access | Repeated hot-path reads created avoidable strings and formatting work | Compute the immutable ID once in the wrapper constructor and return the cached string |
| 9 | Validators and simple actor/query paths created avoidable objects or async state machines | Extra allocations and continuations increased Gen0 pressure and code complexity | Reused the immutable option validator, removed no-op overwrite validation, and returned direct `ValueTask` values on genuinely completed paths; repository storage methods intentionally use explicit `async`/`await` for readability because their forwarding overhead is insignificant beside I/O |
| 10 | The Securities unit and BDD projects contained no discoverable tests | Correctness and concurrency changes had no deterministic regression signal | Added focused unit/BDD coverage for overwrite behavior, non-blocking audit parsing, typed snapshot selection, one-call bulk reads, input ordering, bounded concurrency, and batch persistence |

No project-local lock contention was found in the optimized CPU paths. Bounded task fan-out is used only for independent enrichment requests; actor state remains mailbox-owned. Cancellation tokens were intentionally not threaded into this one domain because partial propagation would create an inconsistent solution contract.

## BenchmarkDotNet environment

- BenchmarkDotNet 0.15.8
- .NET SDK 10.0.302; .NET runtime 10.0.10; x64 RyuJIT x86-64-v3
- Windows 10 22H2
- AMD Ryzen Threadripper 1950X, 16 physical/32 logical cores
- Three warmups and eight measured iterations
- `MemoryDiagnoser`, plus `ThreadingDiagnoser` for asynchronous orchestration benchmarks

The benchmark project retains reviewed `Before` implementations and runs each pair with identical inputs in one BenchmarkDotNet job. Controlled `Task.Delay(1)` cases measure orchestration shape and storage/actor call count, not production database, NATS, or network latency. The Windows host timer quantum produced roughly 13-15 ms per simulated asynchronous delay. Replay benchmarks isolate managed deserialization and state reconstruction; PostgreSQL/index traversal and network time are excluded.

## Before and after results

### Option ID reads

| Requested IDs | Before sequential | After bulk | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 8 | 106.23 ms | 14.89 ms | 7.13x | 2,360 B | 480 B |
| 32 | 470.99 ms | 13.36 ms | 35.25x | 8,888 B | 448 B |

The optimized path performs one storage operation at both sizes. Completed work items fell from 8 to 1 and from 32 to 1 respectively; no lock contention was reported.

### Option enrichment and persistence

| Contracts | Before serial | After bounded + batch | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 8 | 224.53 ms | 28.79 ms | 7.80x | 4,536 B | 2,792 B |
| 32 | 912.06 ms | 73.97 ms | 12.33x | 17,592 B | 9,800 B |

The after path permits at most eight simultaneous enrichment requests and performs one final batch write. At 32 contracts this reduced latency by 91.9% and managed allocation by 44.3% in the controlled workload.

### Actor parse release from command-audit I/O

| Path | Mean | Allocated | Completed work items | Lock contention |
|---|---:|---:|---:|---:|
| Before blocking parse | 14,305.35 us | 336 B | 1 | 0 |
| After non-blocking parse | 3.971 us | 272 B | 1 | 0 |

This benchmark measures only how quickly parsing releases the actor execution path. The audit operation is not discarded: production validation awaits the same tracked task and propagates a persistence failure through the normal actor pipeline.

### Identifier-only command state

| Entries | Before dictionary | After identifier set | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 32 | 1,301.1 ns | 754.7 ns | 1.72x | 4.28 KB | 1.45 KB |
| 512 | 21,444.4 ns | 13,421.8 ns | 1.60x | 82.97 KB | 33.61 KB |
| 4,096 | 572,336.3 ns | 175,820.7 ns | 3.26x | 728.87 KB | 315.01 KB |

At 4,096 entries the optimized state reduced managed allocation by 56.8% and avoided constructing wrapper objects whose fields were never used for command decisions.

### Cached option-contract ID

| Path | Mean | Allocated |
|---|---:|---:|
| Before format on every access | 240.60 ns | 80 B |
| After cached at construction | 0.791 ns | 0 B |

Caching removes the per-access string allocation. The sub-nanosecond after result is a property-read microbenchmark and should be interpreted as elimination of formatting work, not as an end-to-end actor latency claim.

### Typed latest bulk-snapshot replay

| Bulk snapshots in history | Before full matching history | After latest typed snapshot | Speedup | Before allocation | After allocation |
|---:|---:|---:|---:|---:|---:|
| 8 | 34.403 us | 4.487 us | 7.67x | 58.17 KB | 6.77 KB |
| 128 | 967.464 us | 4.021 us | 240.60x | 998.79 KB | 6.80 KB |
| 1,024 | 8,497.502 us | 3.846 us | 2,209.44x | 6,839.18 KB | 6.80 KB |

The optimized managed replay remains effectively constant as immutable history grows. This validates selecting the bulk snapshot type for the bulk command stream; it does not include database/index or network latency.

## Verification

- Before edits, both the Securities unit and BDD projects built but reported zero discoverable tests.
- Ten deterministic unit tests and two BDD tests pass in Release mode. The two cancellation tests verify token propagation and suppression of stale actor replies.
- The production and benchmark projects build successfully with zero warnings and zero errors.
- BenchmarkDotNet completed all 24 configured cases successfully.
- The infrastructure-dependent integration suite compiled and executed all 14 tests: 7 passed and 7 command/event completion tests failed because expected actor completion events or successful external responses were absent. The intentionally empty event actors were not changed to satisfy those expectations, so this suite is recorded separately rather than used as the deterministic regression signal for this pass.
- Reproduce the measurements from the repository root with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Securities.Benchmarks -- --filter *
```

## Deferred work

The wider solution migration continues for the remaining domain query/read-model surfaces and event handlers that perform cancellable pre-commit I/O. Securities command persistence and durable projection repair retain the solution-wide non-cancelable post-mutation rule.

Future regular passes should retain these benchmarks, add paper-trading mailbox latency, allocation rate, batch-size, and storage round-trip telemetry, and optimize dispatch or similar micro-paths only when production measurements show material impact.
