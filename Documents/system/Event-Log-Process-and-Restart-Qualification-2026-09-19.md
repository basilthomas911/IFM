# Event-log independent-process and database-restart qualification

Date: 2026-09-19

## Outcome

**30/30 process/restart cases passed**, covering five scenarios, three repetitions and both the original and batched marker writers. The preceding connection-fault qualification was rerun after the shared harness changes: **36/36 passed again**.

The batched writer preserved the tested atomicity, durable replay and duplicate-rejection behavior across independent OS processes, abrupt writer death, graceful PostgreSQL restart and whole-server SIGKILL recovery.

This is a correctness experiment, not a new throughput measurement. It does not change the earlier speedup estimates or activate batching in the application. Production source, configuration, event-log schema and services were unchanged.

## Cases

| Scenario | Injection and evidence | Required result | Passed |
|---|---|---|---:|
| Cross-process stream-version race | Two separately launched dotnet processes; distinct OS process IDs; both observed blocked on database locks before release | Exactly one command commits; the other reports ConcurrencyException; a fresh process rejects the winner's duplicate | 6/6 |
| Cross-process duplicate command | Two independent processes submit the same command identity and payload | Exactly one commits; the other reports CommandAuditDuplicateException; another fresh process still rejects it | 6/6 |
| Abrupt writer-process death | Kill only the owned child after observing its marker INSERT blocked in the fixture-only advisory-lock trigger | Abnormal child exit, backend disappearance, zero durable events/markers/audit rows/version advancement; fresh-process retry commits once | 6/6 |
| Graceful database restart | Commit eight events, restart the validated container, observe a newer postmaster start time | All committed data replays; duplicate rejected by a fresh process; the next command advances the stream to version 16 | 6/6 |
| Whole-server crash recovery | Commit an eight-event prefix; block the next eight-event append at marker insertion; SIGKILL the container, assert exit 137, restart the same persistent volume | Recovery preserves exactly the committed prefix, rolls back the interrupted append, then allows its retry exactly once | 6/6 |

Every relevant state check verifies event and command-audit counts, exact marker counts and identities, Processing/ApplyProjection state, contiguous stream versions, stream counters, and deserialized event payload order/value/aggregate/command identity. Retries use new OS processes, not a shared in-memory duplicate cache.

Both variants use event_log_v2 with the original four indexes and financial-fence schema installed. Only the marker strategy differs. The fixture streams in this suite are synthetic process-test streams; financial-fence rejection was covered in the earlier performance/verification suites, not re-proven by these particular stream names.

## Restart evidence

The final run performed six graceful restarts and six abrupt whole-server kills. Each restarted server retained fsync/synchronous_commit/full_page_writes=on/on/on and had a strictly newer pg_postmaster_start_time.

- All six graceful-restart logs contain a clean database shutdown and no automatic crash-recovery message in the captured restart interval.
- All six SIGKILL-recovery logs confirm automatic recovery.
- Per-restart JSON evidence retains the validated container ID, before/after postmaster timestamps, fault type and server logs.

These are genuine database-process crashes, unlike the previous backend-only disconnect tests. They are **not physical power-loss tests**: the Docker host and storage remain running.

## Isolation and safety

Fault operations target only the pre-existing disposable container ifm-eventlog-benchmark-20260919, using its validated full container ID rather than a caller-provided destructive target.

Before restart qualification the harness checks:

- Exact container name, postgres:17.2 image and ifm.purpose=eventlog-v2-benchmark label.
- Loopback-only 127.0.0.1:25432 mapping to PostgreSQL.
- Persistent writable PostgreSQL volume, with no other container sharing that volume source.
- No other non-template database besides postgres and the current generated fixture before a server restart.
- No container auto-removal; restart must reuse its persistent data.

Only owned child process handles are killed. Process launches use no visible shell window. All readiness/fault barriers and process waits are bounded; failure to observe the intended barrier fails the case.

The initial attempt failed before any append or fault injection because the child credential guard did not handle an omitted password represented as null. This was a **test-harness issue**, fixed with a null-safe guard. The failed fixture was verified to contain zero events, audit rows and markers, then removed. Its diagnostic results remain retained. It is not counted among the 30 passing final cases.

All final and regression fixture databases were removed successfully. Only postgres remained on the dedicated instance, which was then stopped. No application data was deleted.

## Build and evidence

- Release benchmark build: zero warnings, zero errors.
- Final process/restart qualification: 30/30.
- Connection/acknowledgment regression qualification: 36/36.
- [Final case results](../../BenchmarkDotNet.Artifacts/event-log-v2/process-restart-final-20260919/results.json)
- [Final environment and harness hashes](../../BenchmarkDotNet.Artifacts/event-log-v2/process-restart-final-20260919/metadata.json)
- [Generated summary](../../BenchmarkDotNet.Artifacts/event-log-v2/process-restart-final-20260919/summary.md)
- [Example baseline crash-recovery evidence](../../BenchmarkDotNet.Artifacts/event-log-v2/process-restart-final-20260919/baseline-crash-1.json)
- [Example batched crash-recovery evidence](../../BenchmarkDotNet.Artifacts/event-log-v2/process-restart-final-20260919/batched-crash-1.json)
- [36-case regression results](../../BenchmarkDotNet.Artifacts/event-log-v2/process-restart-regression-20260919/results.json)
- [Initial harness failure evidence](../../BenchmarkDotNet.Artifacts/event-log-v2/process-restart-20260919/results.json)

All twelve per-restart evidence files are beside the final case results.

## Remaining activation gates

The independent-process and whole-server restart gates are now covered for these deterministic scenarios. Remaining work is:

1. Sustained, bounded queue-pressure/soak testing with representative event sizes and marker density, plus server CPU/I/O/wait observation.
2. End-to-end actor/projector/checkpoint progression under that load. This experiment checks persistence recovery, not the entire application workflow.
3. A controlled activation and rollback switch that preserves the original marker writer, followed by target-host verification.

The crash point here is deliberately before COMMIT, with an already committed prefix. It is not an exhaustive sweep of crashes at every transaction stage. Lost COMMIT acknowledgment is covered separately by the rerun proxy test; physical storage failure, failover, and production-scale recovery remain outside this experiment.

No schema replacement or removal of durable command deduplication is supported by these results. A fresh process after failure still needs durable evidence of previously committed commands.

Follow-up: the [bounded queue-pressure/short-soak test](Event-Log-Queue-Pressure-Soak-2026-09-19.md)
passed all eight one-minute samples with client memory, queue/admission, PostgreSQL wait and container
resource observations. Longer production-like retention/latency testing, end-to-end actor/projector
qualification and controlled activation remain open; this is not a multi-hour leak proof.

## Reproduction

Build the benchmark in Release and use the Test environment/credential setup from the [benchmark guide](Event-Log-V2-Benchmark-Harness-and-Verification.md). Start the dedicated labelled container and confirm it has no retained fixture databases.

Set IFM_EVENTLOG_BENCH_ADMIN_CONNECTION to the dedicated 127.0.0.1:25432 postgres administration connection with **Pooling=false**, and the same test-only credentials used by POSTGRES_TEST_KEY. Neither connection should point at an application database.

~~~powershell
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-marker-qualification --process-restart --output=BenchmarkDotNet.Artifacts/event-log-v2/new-process-restart
~~~

Docker must be available. The output directory must be new/empty. Failed fixtures are retained for diagnosis and cause subsequent runs to refuse a nonempty server. The --event-log-process-child entry point is an internal stdin-handshake worker, not a standalone user command.
