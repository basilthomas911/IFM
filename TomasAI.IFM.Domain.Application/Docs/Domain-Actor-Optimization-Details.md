# Domain.Application actor optimization details

Date: 2026-08-06

## Scope and invariants

The recurring review covers Application lifecycle commands and events from shared contracts through the command/event actors, state, handlers, repository, persistence boundary, and current tests.

The following contracts remain unchanged:

- Startup and shutdown events are broadcast notifications; the event actor intentionally validates and acknowledges them without adding state or side effects.
- A command's success result remains distinct from whether its state changed.
- Command logging, event persistence, and event publication remain awaited.
- Repository storage forwarding retains explicit `async`/`await`; its overhead is immaterial beside I/O.
- Coordinated graceful shutdown and cancellation now cover the active command/event-source path; concrete query/read-model APIs remain the next migration tranche.

## Top ten findings and disposition

1. **Incorrect event mailbox and empty dispatch maps - fixed.** The event actor previously used `FundTransactionEvent` and could not dispatch any Application lifecycle event. It now owns `ApplicationEvent` and accepts startup and shutdown events.
2. **Command-log sync-over-async - fixed.** `InsertCommandLogAsync(...).GetAwaiter().GetResult()` was removed from synchronous message parsing. Persistence is awaited after command materialization in the actor pipeline.
3. **Allocation-heavy command-ID validation - fixed.** The one lifecycle invariant no longer constructs a `List<ValidationError>` and supporting error machinery for every valid command.
4. **Dictionary/delegate command dispatch - fixed.** Two stable verbs use typed switches for parse and execution routing, removing string dictionary probes and indirect delegates.
5. **Broad state exception swallowing - fixed.** Supported event application is a direct type switch. Unexpected failures can reach the established actor exception pipeline instead of appearing as a normal no-change result.
6. **Empty event receive graph and unnecessary suspension - fixed.** The event actor performs direct supported-type validation and returns `ValueTask.CompletedTask` because acknowledgement has no asynchronous work.
7. **Recursive exception publication - fixed.** A failure while publishing an event-actor error is logged once instead of attempting the same failing publication again.
8. **Denormalization dispatch overhead - fixed.** Ordered indexed traversal and a typed switch replace enumerator/interface dispatch while preserving event order and awaited publication.
9. **Shutdown completion conversion contract - fixed.** `ApplicationShutdownEvent.ToCompleteEvent` now validates `ApplicationEntityId`, matching `ApplicationShutdownCompleteEvent`, and a regression test verifies the typed conversion and preserved metadata.
10. **Graceful cancellation and lifecycle foundation - implemented for the command/event-source path.** Supervisor shutdown now stops intake, drains accepted mailbox work, and then stops actors. Tokens flow through command validation, replay, NATS, and storage while persistence/publication obeys the documented commit boundary. Query/read-model API migration and broader host integration coverage remain.

## Benchmark summary

BenchmarkDotNet 0.15.8, .NET 10.0.10, x64 RyuJIT, AMD Ryzen Threadripper 1950X, Concurrent Workstation GC, three warmups, eight replay iterations and twelve dispatch/validation iterations.

| Hot path | Input | Before | After | Improvement | Allocation before | Allocation after |
|---|---:|---:|---:|---:|---:|---:|
| State replay | 32 events | 170.01 ns | 128.22 ns | 24.6% | 152 B | 152 B |
| State replay | 256 events | 1,190.09 ns | 699.78 ns | 41.2% | 152 B | 152 B |
| State replay | 2,048 events | 8,207.04 ns | 5,899.57 ns | 28.1% | 152 B | 152 B |
| Command dispatch | Per command | 11.39 ns | 4.41 ns | 61.3% | 0 B | 0 B |
| Valid command-ID validation | Per command | 8.32 ns | 0.42 ns | 95.0% | 32 B | 0 B |

The fixed replay allocation comes from state/base collection construction and does not grow with event count. No thread-pool work or monitor contention was recorded during replay. The validation measurement is close to the timer resolution; the durable result is elimination of the 32-byte allocation.

Full results and methodology are in `TomasAI.IFM.Domain.Application.Benchmarks/RESULTS.md`.

## Verification

- Application benchmark project builds in Release with zero warnings and errors.
- All 10 BenchmarkDotNet cases completed successfully.
- Application unit tests: 4 passed.
- Application BDD project: 1 placeholder test passed; it is not behavioral actor coverage.
- Application integrated project: 1 placeholder test passed; it is not an end-to-end lifecycle test.
- Root MarketData unit tests also passed during this coverage pass: 38 passed.

## Deferred work

1. Replace placeholder BDD/integrated tests with real actor-pipeline startup, shutdown, event-routing, persistence, and failure cases.
2. Propagate graceful cancellation solution-wide, including explicit drain and persistence/publication semantics.
3. Add production lifecycle duration and shutdown-drain telemetry. Do not pursue further Application dispatch micro-optimization without profiles showing it matters.
