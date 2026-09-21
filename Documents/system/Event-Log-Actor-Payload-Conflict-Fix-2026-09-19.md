# Atomic event-log payload conflict fix and verification

Date: 2026-09-19

## Result

The four actor-runtime failures are fixed. **93 tests and 36 fault checks passed**, zero failures/skips in these runs. Changed-content retries now return a payload-conflict failure with warm or cold state; identical retries still return successful duplicate responses through the command actor.

This is a production writer correctness fix in source, not deployment or activation of optimized marker batching. Public writer selection, application configuration and schemas remain unchanged. Running IFM services were not restarted or modified.

## Changes

BinaryCopyEventLogAppender now preserves CommandAuditPostgres.PayloadConflict as CommandAuditPayloadConflictException before checking for an ordinary duplicate. Previously it converted both outcomes to CommandAuditDuplicateException, which atomic command actors could acknowledge as an identical successful retry.

There was also a related mixed-window problem: the writer assigned one audit rejection exception to every pending request in the physical batch. An unrelated valid command could therefore receive another command's duplicate result. After the rejected transaction is rolled back, the writer now isolates each request into a separate transaction for these two specific audit errors. Valid requests commit normally; rejected requests retain their own typed error and command identity. This fallback preserves input order and does not dequeue the requests twice in metrics.

The fallback never applies to generic database/transport failures, concurrency errors or EventLogCommitOutcomeUnknownException. No blind retry is added for an uncertain commit. The healthy batch path remains batched; only audit-rejected multi-request windows take the isolation path. No new throughput claim is made without a fresh benchmark.

Legacy audit rows without a stored payload hash retain their existing duplicate handling. They cannot prove identical content; this fix does not migrate them or change their compatibility policy.

## Tests

| Suite | Passed |
|---|---:|
| Original/batched actor runtime, restart, concurrent duplicates and changed-content rejection | 4 |
| New mixed-window audit isolation, both writer variants, both request orders, duplicate/conflicting payload | 8 |
| Prior projector and real JetStream cases | 18 |
| Actor constructor and SQL layout guards | 17 |
| Existing storage/atomic persistence and audit/cache regressions | 46 |
| Independent-connection fault qualification | 36 |

The mixed-window tests observe two failed physical attempts (the shared transaction and the rejected singleton), one committed command and net-zero queue depth. Thus passing does not merely mean that timing happened to put the requests in separate successful windows. They verify the original audit hash is unchanged, exactly two events/markers exist including the preseeded event, and recovery produces one target effect for each accepted command. A 500 ms coalescing limit is test-only; the two-event batch cap normally causes immediate flush once both requests arrive.

The actor cases now run through both full lifetimes, including continuation appends after restart, stale-version rejection and server-observed queue settlement. Their response assertion requires the specific different-command-payload error, not just any failure. The missing-handoff cases recover 32 committed events at startup.

Fault qualification repeats each of six scenarios three times for both variants: competing stream versions, duplicate commands, checkpoint advancement, precommit backend termination, cancellation after admission and lost COMMIT acknowledgment. All 36 passed. The lost-ack tests retain the unknown-outcome exception and verify authoritative durable state before an explicit fresh-writer duplicate check.

## Durable evidence and artifacts

After the 47-case combined actor/pipeline/guard run, before the 46 existing storage/audit regressions modified the fixture:

| State | Count |
|---|---:|
| Events | 550 |
| Markers | 550 |
| Unfinished markers | 0 |
| Target receipts | 550 |
| Target mutations | 550 |
| Audited commands | 80 |

- [Actor/pipeline/guard/isolation results: 47 passed](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-fix-20260919/actor-fix.trx)
- [Storage/audit/cache results: 46 passed](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-fix-20260919/storage-audit.trx)
- [Fault cases: 36 passed](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-fix-fault-20260919/results.json)
- [Fault summary](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-fix-fault-20260919/summary.md)
- [Original failing evidence](Event-Log-Actor-Runtime-Qualification-2026-09-19.md)

Release builds of the storage test project and fault harness succeeded with zero warnings/errors. The fault harness's existing metadata field ProductionChanged=false describes its lack of production infrastructure mutation; this turn did change writer source as documented above. Raw generated metadata was retained unchanged.

SHA256:

- BinaryCopyEventLogAppender.cs: 65B02DA0F564A981FAEC883899D7092DCA93C914DD23B569FBCE41DDC83E0C28
- MarkerAuditBatchIsolationTests.cs: D6F139BA1EECBB37F0E215E30255073B24CB394499140043EAA2160D202F1EC8

## Isolation and cleanup

Tests used PostgreSQL 17.2 on 127.0.0.1:25432 and a fresh nats:2.12.0-alpine broker on 127.0.0.1:24223. The synthetic database ifm_eventlog_bench_d70983a4c16b_fix was removed after verification. The fault harness created and removed its own guarded databases; afterward only postgres remained. The owned ifm-marker-fix-20260919 container and synthetic stream data were removed; the benchmark PostgreSQL container was stopped. Synthetic data has no backup; reports and raw results remain on disk.

Use the prior actor qualification's guarded environment setup to reproduce with a fresh database. The combined filter is MarkerProjectorPipelineTests OR MarkerActorBenchmarkGuardTests OR EventLogBenchmarkLayoutTests. Then run EventSourceActorSnapshotRangeTests OR CommandAuditPersistenceTests OR CommandDuplicateCoordinatorTests. Before fault qualification, remove only the owned test database: the fault harness requires an otherwise empty isolated PostgreSQL instance. Run the benchmark project with --event-log-marker-qualification and a new --output directory.

## Remaining work

This removes the reported payload-conflict blocker. It does not qualify all production business aggregates, downstream business event consumption, the full API/UI host, or long-duration production-sized workloads. Marker batching remains benchmark-only. Resume controlled performance experiments and complete activation/rollback design before enabling it in production.
