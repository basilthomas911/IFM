# Actor pool performance

## Implemented scheduling and admission optimizations

The ready queue consumes its channel with `WaitToReadAsync` and `TryRead`, avoiding a nested async iterator while retaining readiness metrics and channel completion/cancellation behavior.

`ActorThreadQueues` validates admission directly with `ActorThreadPoolV2` and signals the shared scheduler only when the mailbox changes to scheduled. It no longer resolves and hashes a worker for every message. All workers consume the same ready queue; choosing a worker does not assign mailbox execution to that worker. A stable signal target remains available through disposal for admission already in flight. Other pool implementations continue using the existing supervisor interface.

Unlimited `ObserveOnly` admission uses 32 padded stripes per actor-type slot. A reservation records its stripe so another thread can release the same counters. This reduces a reserve/release pair from eight shared atomic updates to four striped updates. Accepted/released metrics remain enabled as before. Any configured global, actor-type or payload limit selects the original accounting path, as does `Enforce` mode. Mailbox capacity checks are unchanged.

The stripe array contains approximately 36 KiB of elements per unlimited-observation controller, plus array overhead. The reservation charge grows from 12 to 16 bytes. Diagnostic totals scan the stripes: they are exact when quiescent but are not atomic snapshots during concurrent activity. These are throughput improvements with a small retained-memory cost, not a general reduction in allocations.

The worker-count policy remains `Environment.ProcessorCount * 2`, and mailbox batches remain capped at 64 messages. The worker-count and time-budget prototypes were not included in this change.

## Verification and measurement

The Release actor-runtime suite passes 151 tests, including FIFO and exclusive per-entity execution, concurrent mailbox retirement, graceful drain, exact disposal, capacity enforcement, concurrent ready-queue consumption, cross-thread counter release, bounded-observation metrics, canceled/missing/disposed admission and notification after pool disposal.

```powershell
dotnet test TomasAI.IFM.Shared.UnitTests/TomasAI.IFM.Shared.UnitTests.csproj -c Release --filter FullyQualifiedName~EventModelActor
```

The local BenchmarkDotNet harness and reports are in `.test-results/actor-pool-implemented/`. This directory is ignored by Git. The harness preserves the pre-change baseline and compares it with a source-hash-verified copy of the implemented application C# files. Assembly names and internal-access grants differ only to permit side-by-side measurement. The prior candidate run remains in `.test-results/actor-pool5/`.

The rerun retains the same inputs, operation counts, warmups, iterations, runtime and diagnosers. All After measurements include the three implemented changes together. The database bursts call the actual futures-option Scylla DbContext, checking existing ES contracts without database writes. NATS transport, domain reply wrappers and UI rendering are outside these timings. The microbenchmarks isolate scheduler/admission costs; their gains must not be reported as full workflow speedups. Multi-producer counter ns/op is aggregate elapsed time divided by total operations, not individual producer latency.


## Implemented rerun results (2026-09-07)

BenchmarkDotNet 0.15.8, .NET 10.0.10 Release, Windows 10, AMD Threadripper 1950X (32 logical processors). All 14 cases completed. Before and After were both measured again in the same run; these are not comparisons against timings copied from the earlier prototype report.

| Workload | Unit | Before | After | Time change | Interpretation |
|---|---|---:|---:|---:|---|
| Ready-queue schedule/consume | ready entry | 87.60 ns | 66.72 ns | -23.8% | Lower measured component time |
| Hot-mailbox admission/drain | message | 332.90 ns | 288.54 ns | -13.3% | Lower measured component time |
| 128-query burst, combined implementation | 128-query burst | 12.89 ms | 8.30 ms | -35.6% | Inconclusive |
| 4,160-query burst, combined implementation | 4,160-query burst | 269.85 ms | 283.59 ms | +5.1% | Inconclusive |
| Admission reserve/release (Producers=1) | reserve/release pair | 60.15 ns | 27.72 ns | -53.9% | Lower measured component time |
| Admission reserve/release (Producers=4) | reserve/release pair | 313.17 ns | 11.88 ns | -96.2% | Lower measured component time |
| Admission reserve/release (Producers=8) | reserve/release pair | 207.43 ns | 11.61 ns | -94.4% | Lower measured component time |

Ready-queue and counter operations allocated 0 bytes per measured operation on both sides; hot-mailbox admission/drain allocated 146 bytes per message on both sides, including the harness workload. Striped counters improve contention at the retained-memory cost described above.

Both database burst comparisons have overlapping 99.9% BenchmarkDotNet intervals. A separate experiment with 20 alternating paired rounds also has 99% intervals containing zero for both batch time and within-round request p99. The results establish the component improvements, but do not establish an end-to-end database throughput or tail-latency improvement or regression. The full local report retains raw samples, allocations, threading diagnostics, confidence intervals and reproduction commands in `.test-results/actor-pool-implemented/RESULTS.md`.
