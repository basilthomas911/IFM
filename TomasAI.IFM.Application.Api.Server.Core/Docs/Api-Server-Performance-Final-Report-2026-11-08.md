# API Server performance final report â€” 2026-11-08

The twenty audited paths have completed implementation or qualification dispositions, and Development is restored with Debug binaries. The final run completed **94 BenchmarkDotNet cases / 47 paired comparisons**, **5,983 passing correctness tests and zero failures**, plus all five actor-convention audits. Five opt-in/platform cases remain unexecuted. Active-market end-to-end acceptance is blocked by the closed session at verification time. Two items close as qualification of existing behavior: pooled NATS receives and coherent trade-cache mutation locks.

The results show substantial gains in seeding, restoration and serialization, together with explicit costs for bounded bookkeeping, asynchronous console delivery and retained pooled memory. This is not evidence that every path or the whole API became faster.

## Baseline and method

The baseline is the reviewed working tree captured November 7: HEAD `a17da60ca03d3924fd87f1fd078b316cb938c782`, its tracked diff and untracked source (15,920 files, 788,731,193 bytes). It already contains the historical multiplier/P&L corrections. A clean-HEAD comparison would be incorrect. Baseline export, file hashes, dirty diffs, SDK information and original traces remain under `E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107`.

Final measurements use BenchmarkDotNet 0.15.8, .NET SDK 10.0.302 / runtime 10.0.10, Windows 10 22H2, Threadripper 1950X (16 physical / 32 logical cores), out-of-process Release, server GC, two launches, three warmups and ten measured iterations. Development processes were stopped during all final timing and resource probes; no builds or tests ran concurrently with timing. This remains a shared workstation, not an isolated laboratory. Source and benchmark provenance were frozen before the final suites and measurements. Final cleanup removed four whitespace-only issues in three files; no behavior changed. A separate formatting-hash manifest records that difference, and Development was refreshed so its Debug source checksums/PDBs match. The final harness build reported zero warnings/errors.

Before/after implementations share fixtures and runtime configuration. Setup validates equivalent outputs where applicable. Means, confidence margins, managed bytes/op, Gen0/1/2 collections per 1,000 operations and monitor contention are retained in [all 47 comparisons](../../TomasAI.IFM.Application.Api.Server.Benchmarks/Results/2026-11-08/comparisons.md) and its CSV. Full generated tables retain error, standard deviation and threading diagnostics. Earlier partial runs are preserved as provenance and are superseded by the final runs.

## Selected measured results

| Workload | Before | After | Managed KiB/op before â†’ after |
|---|---:|---:|---:|
| RPC descriptor lookup | 233.645 ns | 19.417 ns | 0.27 â†’ 0.00 |
| RPC payload, 50,000 contracts | 1.952 ms | 1.190 ms | 2,637.20 â†’ 879.02 |
| Recorded-row seed reader, 300,000 trades / 48 bars | 444.710 ms | 190.907 ms | 57,025.87 â†’ 49,605.45 |
| EMA PostgreSQL persistence, 64 bars | 949.382 ms | 398.899 ms | 6,213.31 â†’ 1,655.77 |
| Saved-trade restoration, 128 trades | 1,834.326 ms | 318.198 ms | 13,400.27 â†’ 10,690.47 |
| Five independent PostgreSQL indicator commands | 763.485 ms | 567.646 ms | 3,783.81 â†’ 3,799.91 |
| Five projected indicator reads | 5.629 ms | 4.219 ms | 77.19 â†’ 80.74 |
| Idle expiry, 4,096 residents | 70.423 us | 13.885 ns | 0.00 â†’ 0.00 |
| 32 concurrent diagnostic consumers | 2.691 ms | 2.079 ms | 121.20 â†’ 12.32 |

The full seed reader includes raw-row materialization/streaming, partitioning, sorting and aggregation; its recorded source excludes Scylla network latency. At 300,000 rows both paths return the same 48 bars. Each resource probe performs 80 source-window reads across 20 measured requests on both paths; streaming changes allocation/processing, not the number of these source reads.

EMA and independent-indicator database fixtures include actual PostgreSQL work and stream resolution, load, compute and persistence. They exclude NATS/mailbox latency. EMA batch persistence atomically commits events, projections and the reconstructable latest full-state snapshot. Saved-trade restoration uses real NATS/PostgreSQL coordination but an immediate fixture worker start; it excludes provider discovery and option pricing latency. Per-trade contention benchmark means cover a batch of 64 updates per producer; they are not individual request percentiles.

## Tradeoffs and cases without a demonstrated gain

- **Dense expiry burst:** 4,096 residents rose from 7.645 to 9.898 ms (+29.5% mean); 64 residents rose from 429.6 to 478.6 us (+11.4%). The dense after case is noisy (confidence margin about 1.785 ms). Deadline indexing removes the normal idle scan, while bounded/fair publication and retry handling add burst work. This regression is disclosed and the correct bounded publication behavior is retained.
- **Fast console:** a batch of 32 writes to a no-delay fixture rose from 2.145 to 20.560 us, with 576 additional bytes/batch. With a nominal one-millisecond blocking consumer (Windows scheduling makes that longer), direct producer time was 494 ms versus 21.326 us queued. The consumer drains outside that measurement, with delivery/count/drop checks. This measures producer responsiveness, not end-to-end output throughput. Separate routine/critical queue bounds, overflow counters and bounded shutdown are tested.
- **Warm-up receipts and revision safety:** independent orchestration fell from 17.397 to 14.729 ms, with allocation 1,728â†’2,006 KiB; stale initialization fell from 3.685 to 2.643 ms. Retry was 4.042â†’4.347 ms, allocation 400â†’466 KiB, and same-key was 3.013â†’3.147 ms. Confidence intervals overlap in the retry/same-key comparisons. Correct source-revision hashing, cancellation ownership and bounded receipts are retained. Retry suppresses completed stage commands (10â†’6 in the fixture); stale initialization requests 48 bars instead of 21+48. Same-key commands remain five on both paths: do not claim fivefold work reduction.
- **Health summary:** its much smaller response serializes faster, but it does not contain the full actor/history payload. Capture is measured separately from serialization. Existing UI compatibility routes remain; the current UI does not automatically gain the summary route's byte savings.
- **Supervisor history:** the indexed warm read avoids reparsing the durable file; it is not a cold-start improvement. The bounded 10,080-row index uses retained memory, and durable append/compaction remains serialized.
- **Sampled logs:** reduced routine success output is intentional (Development Queries 1/100, Commands 1/10, 50 ms slow override). Failures, slow operations and independent audit/business events remain visible. It is not identical-output logging throughput.
- **Redis and locks:** Redis means were 2.880â†’2.801 ms with overlapping intervals and slightly higher allocation; safe asynchronous initialization is the primary result. Trade-cache mutation locks remain. The 4-producer BDN mean is slightly slower, while the 16-producer mean is lower; there is no universal locking-speedup claim.
- **Pooled NATS:** the measured ownership advantage was already active in the default receive path. It is compatibility/ownership qualification, not a newly deployed gain or zero total message allocation.

## All twenty dispositions

1. Sample routine successful Query/Command information logs; retain failures and slow operations. Development samples Queries 1/100 and Commands 1/10, with a 50 ms slow-operation override. Audit/business events are independent.
2. Bound asynchronous console delivery (routine and critical queues), expose overflow/failure counters and bound shutdown drain. Producer latency is distinct from completed delivery throughput.
3. Freeze and reuse RPC operation descriptors.
4. Write MessagePack RPC payloads directly through pooled buffers, preserving exact wire bytes and cancellation semantics.
5. Qualify the existing owned/pooled NATS default; retain compatibility array path where required.
6. Preserve coherent cache writer locks and generation fencing; qualify contention instead of removing synchronization speculatively.
7. Index expiry deadlines, avoid idle scans, use dense fallback and bound/fairly schedule publication/retry work.
8. Share health capture; add summary/detail/paged reads and retain UI compatibility routes.
9. Coalesce only in-flight identical backend diagnostic requests. Completed responses are not cached.
10. Maintain a bounded in-memory supervisor history index; retain serialized durable append/compaction.
11. Partition seed trades into requested bars in one pass.
12. Stream raw tick rows and use private bounded pooled working buffers. Verify page boundaries, cancellation, restart and midstream failures.
13. Avoid repeated stale 21-bar reads followed by 48-bar reads; use a consistent cutoff.
14. Retain receipts for completed seed stages and retry unfinished stages only. Identity includes complete typed source revisions.
15. Bound independent projected indicator reads to two per timeframe.
16. Bound independent indicator commands to two per timeframe; preserve per-aggregate bar order and independent timeframe progress.
17. Add atomic EMA SeedBatch (1â€“64 observations), persisting events, durable projections and reconstructable latest snapshot together. Preserve duplicate/retry safety.
18. Bound shared in-flight seed ownership to 64 keys, isolate waiter cancellation, enforce owner budget and bound retained receipts.
19. Restore saved trade leases with bounded concurrency, stable generation/intent receipts and no redundant unchanged intent writes.
20. Own Redis initialization asynchronously; adapt startup cache access and await dependency readiness before consumers use it.

## Storage failures corrected

Migrate obsolete non-tick Scylla fixtures to disposable PostgreSQL databases, leave tick tests on Scylla, use positional values and UTC dates, and resolve repository source roots independently of E: build output. Preserve repository faÃ§ade/extension conventions. PostgreSQL EOD session-statistics updates now use native SQL and preserve close/stddev/intraday state. PostgreSQL Securities rebuild/reconciliation now avoids CQL journals; rebuild locks and commits both projections atomically, rejects ambiguous IDs before changes and preserves exact reference payloads.

Retired CQL generation/journal scenarios are listed in the evidence manifests. Their current guarantees are covered by PostgreSQL rebuild rollback, missing/stale projection repair, identity ambiguity, concurrent writes, immutable reference publication and existing canonical CRUD/rename/delete integration tests. Provider-neutral positional batch binding remains owned by Framework.Storage. Native established-trade history now uses bounded keyset pages with the complete timestamp/order/trade/revision key, validates cursor scope and preserves all tied revisions without duplicates. Rollover reads preserve front-first ordering. Legacy test projections now include accepted timestamps and current currency metadata/history semantics.

The retired manifests list 12 obsolete Market Data CQL generation/journal scenarios and seven Securities CQL-journal scenarios. They were not current PostgreSQL invariants. New native PostgreSQL tests cover rollback after a late rebuild failure, concurrent writes, missing/stale projection repair, ambiguity rejection and tied-row keyset paging, alongside existing CRUD and immutable-reference tests. Do not compare old and new test totals as identical scenarios.

The original Storage failures remain in the evidence archive; they are not outstanding failures. Final Storage qualification passes all 830 tests, including real Scylla 6,001-row paging/cancellation/restart and PostgreSQL latest-full-snapshot/history reconciliation coverage. EMA snapshot cases cover 21, 48 and 64 observations and transactional rollback.

## Correctness qualification

The total below excludes duplicate targeted reruns and excludes convention-audit actor counts. UI coverage here is the affected component/transport suite, not the entire desktop automation suite.

| Suite | Passed | Skipped |
|---|---:|---:|
| Storage integration | 830 | 0 |
| Application.MarketData.UnitTests | 842 | 5 |
| Domain.Application.Actor.UnitTests | 91 | 0 |
| Domain.MarketData.Analytics.BDDTests | 479 | 0 |
| Domain.MarketData.Analytics.IntegrationTests | 48 | 1 |
| Domain.MarketData.Analytics.UnitTests | 1,289 | 0 |
| Domain.Trade.BDDTests | 49 | 0 |
| Domain.Trade.UnitTests | 1,712 | 0 |
| Framework.Messaging.Nats.IntegratedTests | 62 | 0 |
| Framework.Messaging.Nats.UnitTests | 131 | 0 |
| Shared.UnitTests | 347 | 0 |
| UI performance components | 63 | 0 |
| Startup / recovery components | 39 | 0 |

All Command, Query, Event, Function and Realtime actor-convention audits pass. The normal suites reported 5,982 passes and six skips. A separate opt-in rerun then passed the previously skipped maximum-scope pricing case, giving 5,983 executed passes and five distinct unexecuted cases: live official Treasury, two supervised-process health/reset qualifications, platform process containment, and the historical ES/VX backtest fixture. Raw suite skip counts are preserved rather than rewritten. Source-level assertions and actual provider integration tests both pass; no Development data was used as disposable test infrastructure.

Additional opt-in stress passed with 512 controlled option instruments, 100 real managed runtime/session/consumer reconstructions, and 491,520 quote updates over 60.016 seconds. All captured instruments had valuations, and resources released successfully. This is a managed-consumer correctness stress test, not a sustained whole-API/Rust Host mixed-workload capacity certification; the 60-second window is not the optional 30-minute soak. It ran after BDN with Development restored, so its throughput is not used as a before/after gain. Evidence: `sustained-pricing-20261108.json` and `sustained-pricing-final.trx`.

## CPU, GC, memory and locking probes

These are separate instrumented processes, three alternating before/after repetitions, actual server GC verified, with 20 selected-path warm-up seed reads followed by 20 measured reads. The table reports each metric's median independently; it is not one particular run. Runtime is .NET 10.0.10 with 32 logical processors. GC columns use `GC.CollectionCount` deltas (higher-generation collections can also increment lower-generation counts). Individual request p99 includes monitor waiting; BDN batch means cannot substitute for it.

| Probe | Elapsed ms before â†’ after | CPU ms before â†’ after | GC pause ms before â†’ after | Sampled peak working set MiB before â†’ after | p99 us before â†’ after |
|---|---:|---:|---:|---:|---:|
| 300k seed, 20 reads | 8,938.38 â†’ 3,970.79 | 18,546.88 â†’ 11,125.00 | 126.41 â†’ 84.20 | 217.25 â†’ 261.07 | n/a |
| Cache / 1 producer | 2,192.50 â†’ 2,070.03 | 2,328.12 â†’ 2,234.38 | 1.82 â†’ 1.64 | 48.03 â†’ 47.82 | 16.80 â†’ 15.10 |
| Cache / 4 producers | 2,177.91 â†’ 2,113.69 | 2,453.12 â†’ 2,343.75 | 5.14 â†’ 4.69 | 46.95 â†’ 47.02 | 163.30 â†’ 167.10 |
| Cache / 16 producers | 2,346.85 â†’ 2,406.58 | 2,734.38 â†’ 2,843.75 | 20.60 â†’ 13.68 | 51.80 â†’ 57.44 | 856.40 â†’ 791.60 |

Seed all-thread allocation/read fell from 58,481,802 to 50,835,913 bytes. Seed collection deltas were 17/14/14â†’15/12/12. The private pooled buffers increase retained/peak footprint despite lower allocations; retention is capped at 16 arrays per bucket and 65,536 references per array, not a bound on total input memory. Ten-millisecond footprint sampling can miss shorter peaks and includes identical fixture/equivalence initialization.

At 16 producers, all-thread bytes/request fell 3,591â†’2,732, GC collection deltas were 71/0/0â†’38/2/2, but median CPU rose 2,734â†’2,844 ms, working set rose 51.8â†’57.4 MiB and completed monitor contentions rose 4,899â†’6,351. Median p99 fell 856â†’792 us. These mixed results support retaining the correctness locks rather than claiming contention was eliminated. Detailed all-thread allocations and all p50/p95/p99 values remain in the resource CSV/JSON.

Twenty-second real API traces are observational, with unmatched workloads: before/after each recorded one Gen2 start; GC pause was 236.20â†’258.43 ms, peak GC-reported heap 111.01â†’102.07 MiB, LOH 63.92â†’55.69 MiB, and completed monitor waits 13â†’5 (0.4715â†’0.0878 ms total). Zero allocation-tick events are missing sampled evidence, not zero allocation. The traces do not establish a whole-process performance improvement. Async semaphore waits are not monitor contention.

## Development restoration and verification

API and Rust Market Data Host were rebuilt in Debug; API build passed with zero warnings/errors. Server Manager and scheduler were republished in Debug. The native development-profile DLL and PDB hashes match the selected Rust build on E:. Existing Rust eGui binaries and full PDB were retained because this work did not change Rust UI source.

- API PID 127280: `/health/launch-ready` HTTP 200, Healthy; deployment identity enforced and matched; actor intake open; eight startup activities satisfied, zero failed/skipped.
- Rust Host PID 131696: `/health/ready` HTTP 200. Native package is Debug/development with matching source-debug symbols; no legacy provider DLLs are packaged.
- Server Manager PID 111548 owns the restored development processes.
- Scheduler PID 125760: named-pipe read-only dashboard reports Ready, database/Quartz available and scheduling started. Its initial pre-API `NoResponders` registration error recovered on the bounded retry; Quartz subsequently started. PostgreSQL Quartz has two WAITING triggers: November 8 at 18:00 and November 9 at 17:01 Toronto time (Market Open / Market Close). These schedules were inspected without executing either task.
- eGui PID 30816: responding window `IFM - eGui 0.1.0`, established connections to Development NATS port 4222 and the API port 22543. No ephemeral benchmark broker is configured. Successful Weekly/Monthly Strategy Updates grid queries were observed after this restart. The new summary, paged-actor, projector and worker read routes returned HTTP 200. Interactive grid/quote behavior was not tested through desktop automation in this run.

Verification occurred Sunday afternoon while the authoritative session was stopped: lifecycle `ScheduledStopped`, active value date absent, Futures/Options datasets Healthy/Idle. `/health/ready` remains HTTP 503 because its `live_pipeline` health check maps an Inactive pipeline to Degraded, even though its component checks are healthy. Launch readiness and backend control remain Healthy. Startup also retained the optional warning that the in-memory Market Outlook snapshot for ES20261218 / November 6 was absent after the historical warm-up reported AlreadyCurrent (261 valid sessions). This was not treated as proof of an active-market failure or silently changed in the performance refactor. A fresh market-hours workflow/feed-to-screen qualification is blocked by the closed session and is not claimed; no synthetic trading signals were manufactured to make it green.

The exact structured before/after financial comparison is unchanged: current position payload, all history rows, Fund order/trade payload hashes, financial postings, allocation versions and accepted-close mappings. Position 1901:1501 remains closed, order 701 and trade 4202 remain Closed, four unique close fills and 18 history rows remain. Corrected multiplier 50 gives realized loss $347.50, commissions $5.20 and net loss $352.70. No order was submitted and no financial reconciliation write was performed by this work.

The owned benchmark NATS container was stopped after all measurements. Development NATS, databases, AWS backup/cutover services, active artifact junctions and recovery copies were preserved.

## Evidence and reproduction

Compact tables, individual BDN reports, test totals, raw-result hashes and source provenance are published under `TomasAI.IFM.Application.Api.Server.Benchmarks/Results/2026-11-08`. Large traces, TRX, recorded fixtures, resource-probe JSON and superseded runs remain on E: under the evidence root. The compact evidence manifest hashes published files; raw-result provenance identifies exact JSON files and hashes.

Descriptor timings cover warm lookup, not separately timed cold initialization. A sustained isolated whole-API mixed actor/quote/workflow soak and fresh-quote UI/independent timeframe progression still require their own end-to-end acceptance run; component stress and microbenchmarks do not certify them. No whole-server capacity or SLA improvement is claimed.

Use `scripts/Benchmarks/Run-ApiServerPerformance.ps1` with a new E: run root for the API project, then again with `-Project TomasAI.IFM.Framework.Messaging.Nats.Benchmarks/TomasAI.IFM.Framework.Messaging.Nats.Benchmarks.csproj`. The final roots are `acceptance-20261108-final` and `acceptance-nats-final`. `Summarize-ApiServerPerformance.ps1 -EvidenceRoot <root> -FinalRunRoot <API root> -OutputDirectory <new E: summary>` combines only final API/NATS results. Do not run builds, tests or Development concurrently with timing.

The benchmark executable also exposes `--api-performance-resource-probe --producers 1|4|16 [--candidate]` and `--api-performance-seed-resource-probe [--candidate]`. Invoke these through a runtimeconfig with server GC enabled and verify the emitted `ServerGc` value. Preserve all repetitions rather than selecting the most favorable sample. Run the full Storage suite and the project suites in `test-summary.json`; the exact component filters, commands and audit output used for this qualification remain in `qualification-20261108.ps1` and `qualify-components-final.ps1` under the evidence root.

All source changes remain uncommitted for review. No commit or push was requested for this task.
