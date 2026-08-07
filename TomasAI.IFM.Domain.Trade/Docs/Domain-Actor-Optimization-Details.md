# Domain.Trade actor optimization details

Date: 2026-08-05

## Scope and outcome

The review covered `TomasAI.IFM.Domain.Trade` from its four active actor roots through command/query handlers, event-sourced state and repository code, domain collections, shared view-model extensions, validation, and the `Application.Storage/TradeDb` graph-hydration leaf used by the actors.

The pass removed the synchronous actor audit wait, eliminated position-level N+1 graph reads, bounded and parallelized independent storage I/O, replaced allocation-heavy history/position scans, indexed snapshot leg joins, cached immutable validation graphs, simplified actor dispatch, and allowed replay exceptions to reach the actor pipeline. The option-trade event history remains unbounded and snapshot reconstruction semantics are unchanged.

## Top ten issues found

| Rank | Issue | Impact | Resolution |
|---:|---|---|---|
| 1 | `ParseMessage` synchronously waited for command auditing | Blocked an actor worker on async I/O and risked thread-pool starvation | Audit work now starts without blocking and is awaited at the validation boundary through `CommandAuditTracker` |
| 2 | `FillOptionTradeAsync` used `.Result` inside a LINQ projection | Sync-over-async deadlock/starvation risk in the read path | Removed `.Result`; all storage work is awaited asynchronously |
| 3 | Option-leg data was queried once per trade position | N+1 I/O grew with unbounded position history | Query once per distinct value date, then group by position identity in memory |
| 4 | Independent trade graph reads and sibling trade hydration were sequential | Added storage round-trip latency and reduced throughput | Independent reads overlap; sibling/date fan-out is bounded to four concurrent operations |
| 5 | Trade history performed repeated scans for every date and status | Approximately quadratic work with long histories plus iterator allocations | One stable `ValueDate`/status ordering pipeline replaces per-date rescans |
| 6 | Position lookup sorted complete arrays and formatted `DateOnly` values as strings | O(n log n) lookup, avoidable strings, and Gen0 pressure | Single-pass latest selection, reverse scans, and direct `DateOnly` equality |
| 7 | Snapshot/factory reconstruction repeatedly scanned all option legs | O(position-leg count × option-leg count) joins and iterator garbage | One ordinal leg dictionary is built per reconstruction and reused |
| 8 | Domain collections used LINQ for hot first/last/exists/single lookups | Iterator/delegate garbage and unnecessary full scans | Allocation-free forward/reverse loops preserve first/last and uniqueness behavior |
| 9 | State application swallowed all exceptions; event/query dispatch added fake async layers | Corrupt replay could silently produce partial state; extra state-machine complexity | Replay exceptions now flow to the actor pipeline; completed `ValueTask` paths and static event dispatch are used |
| 10 | Validation constructed FluentValidation graphs per call and repeated the trade-limit check per position | Repeated rule graph allocation and redundant validation work | Immutable validators are cached and the trade-limit identity check executes once |

During issue 6, the review also found an impossible `Open && EndOfDay` predicate in `GetEodTradePnl`. It now intentionally includes opening and end-of-day positions and has a characterization test.

## Implementation notes

### Actor lifecycle and failure semantics

- Command auditing is asynchronous without blocking parsing. A command that bypasses parsing still receives an audit during validation.
- Command success and state mutation remain separate concepts. No change was made to the command result/state-change contract.
- `OptionTradeCommandState.Apply` no longer catches every exception. Unexpected replay failures are allowed to propagate to the existing actor exception pipeline.
- `OptionTradeEventActor` keeps its default event target role; dispatch storage is static, but intentionally empty actors/scaffolding are preserved.

### Threading, concurrency, and locking

- No locks were added to actor-owned aggregate state. Mailbox serialization remains the synchronization boundary.
- Only independent read operations are overlapped. Hydrated collections are assembled after reads complete, so concurrent tasks do not mutate aggregate state.
- Trade and distinct-date storage fan-out is capped at four per hydration scope to protect the storage connection pool while reducing wall-clock latency.
- Event denormalization remains ordered because event sequence is semantically significant.

### Memory and history behavior

- Option-trade histories remain unbounded. This pass does not truncate state, position, or event history.
- Snapshot reconstruction still starts from the stored snapshot and replays subsequent events. Indexed joins only change how the in-memory object graph is reconstructed.
- Missing data behavior is unchanged; unexpected storage/replay exceptions are not converted into new command-processing exceptions.

## BenchmarkDotNet results

BenchmarkDotNet 0.15.8, .NET 10.0.10 x64, Concurrent Workstation GC, AMD Ryzen Threadripper 1950X (16 physical/32 logical cores). Five measured iterations after two warmups. Means are per operation.

| Scenario | Size | Before | After | Time ratio | Allocation before | Allocation after |
|---|---:|---:|---:|---:|---:|---:|
| Latest matching position | 32 | 560.24 ns | 24.48 ns | 0.04 (22.9× faster) | 848 B | 0 B |
| Latest matching position | 512 | 20.06 µs | 418.17 ns | 0.02 (48.0× faster) | 8,528 B | 0 B |
| Exact-date position | 32 | 244.86 ns | 1.09 ns | 0.004 (224× faster) | 192 B | 0 B |
| Exact-date position | 512 | 252.61 ns | 1.09 ns | 0.004 (231× faster) | 192 B | 0 B |
| Trade-history ordering | 32 | 20.62 µs | 497.67 ns | 0.02 (41.4× faster) | 20.73 KB | 1.34 KB |
| Trade-history ordering | 512 | 532.99 µs | 27.67 µs | 0.05 (19.3× faster) | 58.77 KB | 14.46 KB |
| Snapshot leg join | 32 | 2.67 µs | 427.3 ns | 0.16 (6.2× faster) | 4,352 B | 608 B |
| Snapshot leg join | 512 | 43.32 µs | 3.71 µs | 0.09 (11.7× faster) | 69,632 B | 608 B |
| Simulated storage fan-out | 4 reads | 48.54 ms | 11.29 ms | 0.23 (4.3× faster) | 1.23 KB | 1.46 KB |
| Simulated storage fan-out | 8 reads | 91.53 ms | 11.14 ms | 0.12 (8.2× faster) | 2.30 KB | 2.60 KB |

The fan-out benchmark uses deterministic 1 ms asynchronous operations to isolate orchestration. It demonstrates latency shape, not a database SLA; production fan-out is bounded. The microbenchmarks compare the prior algorithms with the implemented algorithms on identical data.

Reproduce:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.Trade.Benchmarks -- --filter "*"
```

## Verification

- `dotnet test TomasAI.IFM.Domain.Trade.UnitTests/TomasAI.IFM.Domain.Trade.UnitTests.csproj -c Release`: 39 passed, 0 failed, including non-blocking audit/validation coordination, position-history characterization, and actor/direct-API cancellation coverage.
- `TomasAI.IFM.Domain.Trade.BDDTests` builds successfully but currently discovers zero tests; this pre-existing coverage gap remains visible rather than being counted as a passing suite.
- Dedicated BenchmarkDotNet project builds and all 20 benchmark cases completed.
- The only restore-time warning observed is the pre-existing transitive `Framework.Storage` obsolete `RowSet.Dispose()` warning; Domain.Trade builds cleanly under `--no-restore`.

## Deferred work

- Trade query/read-model cancellation is complete across actors, handlers, the direct API, and concrete storage. Required post-commit denormalization retains the shared durable-outcome boundary.
- Before reactivating legacy `AlgorithmBuilder` registrations, remove its `.Result` calls and repair its cache lifetime/ownership.
- Paper-trading telemetry should guide any later verb-dispatch or other microoptimization; dictionary dispatch remains appropriate for this pass.
