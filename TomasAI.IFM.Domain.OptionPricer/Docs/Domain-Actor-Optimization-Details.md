# Option Pricer Domain Actor Optimization

## Scope and invariants

This pass reviewed `TomasAI.IFM.Domain.OptionPricer` from actor APIs through command, event, query, repository, validation, spread calculation, and directly invoked OptionPricer framework code.

The following domain invariants were preserved:

- `SpreadDistributionEventActor` remains an intentionally empty default event target.
- Command success and state mutation remain separate concepts. A successful command is not forced to create a state change.
- Event history remains immutable and unbounded.
- Missing replay data continues to produce the best available or empty state. No application-generated replay exception was added.
- The process-wide QLNet lock remains because QLNet `Settings` is global mutable state.

## Top ten issues found and resolution

1. **Option-trade query used the wrong actor route.** `GetOptionTradeAsync` constructed a `GetOptionTradeQuery` with the futures-EOD actor, verb, and error code. It now uses the option-trade query contract end to end.
2. **Job publication raced read-model insertion.** The submitted domain event was published before the in-progress job row was inserted, allowing a fast completion to update a row that did not yet exist. The repository now inserts first, then publishes the submitted event and its completion event; failures still publish the matching failure event.
3. **Command parsing synchronously blocked actor threads on audit I/O.** Command auditing and duplicate reservation now execute once in `BaseEventSourceCommandActor` through `ICommandAuditLogger`; domain actors no longer start or join audit writes.
4. **Independent market-data requests were serialized.** Historical market data and live feed data now start concurrently after the trade is loaded, then are awaited without `Task.Run` or actor-context fan-out.
5. **Spread simulations were flattened, normalized, independently sorted, and allocated repeatedly.** `ProbabilityValueCollection` now materializes the normalized spread array once and reuses it for forward-price and loss calculations.
6. **Loss probability built three lists and multiple LINQ sort intermediates.** The hot path now fuses put P&L, call P&L, and combination into one pooled buffer and performs the median/MAD work in place. The compatibility API remains available.
7. **Failed QLNet calculations silently supplied zero Greeks.** The job now checks every leg's `OptionGreeks.Success` and returns the established service-failure result instead of pricing with invalid zeros. A one-lock four-leg batching candidate was benchmarked and rejected because it was 1% slower with no memory benefit.
8. **Stateless distribution commands replayed all deletes after the latest insert snapshot.** The repository now uses snapshot-last-N replay with `N = 0`, preserving the latest snapshot and empty-state semantics while avoiding unnecessary post-snapshot delete replay.
9. **State and event handlers suppressed unexpected exceptions.** The job state no longer has a blanket empty catch. Unexpected event-handler failures now flow to the base actor pipeline; expected service failures still issue the explicit fail command. The previously unregistered status-updated event is now routed by the job event actor.
10. **Validation and actor setup created avoidable objects and missed payload validation.** Fluent validator graphs are cached, insert commands now validate their spread payloads, empty receive maps are static, unused state/repository dependencies were removed, direct task wrappers were simplified, and test/benchmark friend assemblies were corrected.

## BenchmarkDotNet results

Environment: BenchmarkDotNet 0.15.8, .NET 10.0.10, Windows 10, AMD Ryzen Threadripper 1950X (16 cores / 32 logical processors), Concurrent Workstation GC. General jobs used 3 warmups and 8 measured iterations. Results are means.

| Scenario | Input | Before | After | Change | Before allocation | After allocation |
|---|---:|---:|---:|---:|---:|---:|
| Independent actor I/O | two 2 ms requests | 31.116 ms | 13.947 ms | 55.2% lower latency | 728 B | 744 B |
| Loss probability | 256 paths | 17.679 us | 7.072 us | 60.0% faster | 26,080 B | 48 B |
| Loss probability | 4,096 paths | 675.702 us | 332.120 us | 50.8% faster | 394,816 B | 48 B |
| Repeated spread consumption | 256 paths | 36.457 us | 0.201 us | 99.4% faster steady-state | 34,576 B | 0 B |
| Repeated spread consumption | 4,096 paths | 1,368.197 us | 3.262 us | 99.8% faster steady-state | 526,096 B | 0 B |
| QLNet four-leg lock batching candidate | four legs | 109.1 ms | 110.2 ms | 1.0% slower; rejected | 101.89 MB | 101.88 MB |

The spread-consumption comparison measures reuse after the first materialization. The initial flatten-and-sort cost still occurs once per `ProbabilityValueCollection`; the gain is removal of every repeated materialization in the same job.

The independent-I/O benchmark uses the same start-both-then-await pattern with controlled asynchronous actor-request latency. Actual production latency depends on broker, storage, and service load.

## Verification

Run the benchmark suite with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.OptionPricer.Benchmarks -- --filter '*' --join
```

Regression coverage verifies spread-value equivalence and caching, fused loss-probability equivalence, and correct option-trade actor routing. Production, unit-test, BDD-test, and benchmark projects are built and tested in Release configuration. The coordinated cancellation follow-up added focused actor and direct-API coverage; the final OptionPricer unit suite contains 10 passing tests.

## Deferred work

- **Solution-wide cancellation propagation:** OptionPricer query/read-model cancellation is complete from actor/direct API entry points through concrete storage. Command validation and replay are cancellable, while event persistence and required post-commit denormalization retain the non-cancelable durable-outcome boundary. Long-running job-event cancellation remains part of the later coordinated event-context/API work because its cross-domain requests currently expose no token-aware context contract.
- **QLNet graph allocation:** four legs allocate about 102 MB in the current QLNet workflow. Lock batching did not improve it. Any future graph/process reuse must be benchmarked for numerical equivalence and global-settings safety before adoption.
- **Dispatch micro-optimization:** retain dictionary dispatch. Revisit a generated true jump table only with paper-trading profiles showing parsing to be material.

## Repeatability

Keep this document as the baseline for the next recurring OptionPricer optimization pass. Re-run the benchmark suite on the same machine/runtime where possible, append new measurements, and treat improvements smaller than observed variance as neutral.
