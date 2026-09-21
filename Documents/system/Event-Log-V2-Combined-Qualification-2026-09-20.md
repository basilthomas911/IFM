# Combined three-stage event-log qualification

Overall verdict: NOT QUALIFIED for deployment. Production unchanged.

Candidate: batched binary COPY/marker path plus a name-preserving stream/version primary key (three indexes). This is not the original fully stripped-down schema.

## Stage 1: integration — failed gate

| Run | Passed | Failed |
|---|---:|---:|
| Candidate actor/JetStream/projector/guards | 46 | 1 |
| Four-index actor/JetStream/projector/guards | 47 | 0 |
| Fresh candidate repeat, unchanged test behavior | 46 | 1 |
| Candidate financial messaging/ledger/recovery/fence | 22 | 1 |
| Four-index financial control | 22 | 1 |

Zero skips. The candidate fixture now preserves ux_event_log_stream_version_v3 as the PK/index name, matching the migration design. Independent inspection after financial schema initialization confirmed candidate retained three indexes and control retained four.

### Actor startup failure

Both candidate runs received NatsNoRespondersException on the first group of requests after StartConsumersAsync. The first failure was the unbatched/no-omitted-handoff case; the repeat failed the batched/omitted-handoff case. The four-index control passed. This is an unresolved intermittent startup issue, not proven to be caused by index changes and not cleared by other passing cases.

NatsActorConsumer.StartCoreAsync sets running and starts RunMessageLoopAsync without awaiting broker-confirmed subscription readiness. Subscription occurs in that background loop. This is a plausible readiness race; the run does not establish causality. No sleeps/retries were added to mask the failure, and no production consumer change was made.

### Financial contract failure

Audited_but_uncommitted_command_resumes_and_replay_checks_receipt_even_when_notification_is_down expected RequestMismatch (34114) but received PersistenceFailed (34118) for changed-content replay. The same failure reproduced on the four-index control, demonstrating it is not specific to index consolidation. The assertion stops before that test's final receipt/balance checks, so those checks are not counted as passed.

Other selected tests cover real NATS financial actors, independent process replay, ledger posting, workflow recovery and legacy writer fencing. Financial workflows use their existing transactional persistence path, not forced benchmark marker batching. These tests establish compatibility boundaries, not a claim that every financial write was routed through the optimized writer.

### Scope

Real isolated NATS/JetStream and PostgreSQL were used. Marker actor tests recreate real supervisor/runtime/consumer components and recover persisted projections. Synthetic command/projector targets and substituted blackboards remain. This is not the full API/UI host, live market data, or brokerage execution.

## Stage 2: larger migration rehearsal — 22/22 passed within bounded scope

The benchmark harness now accepts --seed-events=N (256 through 1,048,576, multiples of 256) and records successful migration transaction timings. The populated fixture used 262,144 real-writer-persisted synthetic events, 1,024 audited seed commands, and durable markers. Event table/index size was 138,936,320 bytes (about 132.5 MiB). Repeated 1,024-character payloads compress well; this is not the production payload distribution.

Checks cover lock timeout, injected SQL failure after DDL, explicit transaction rollback in both directions, forward/reverse idempotence, full schema reapplication, data fingerprints, exact PK/index shape, financial FK/fence preservation and subsequent appends.

Observed populated-fixture successful transaction times:

| Operation | Elapsed |
|---|---:|
| First committed forward migration | 13.35 ms |
| Committed rollback rebuilding original indexes | 277.62 ms |
| Re-forward after rollback | 11.63 ms |

These are single local samples including lock acquisition/commit, not production timing promises. The two-second lock-timeout case and deliberate failed transaction are not included in timings.json. Fingerprint/reapplication/seed time is outside these transaction timings.

No production size target was supplied; this baseline cannot be called production-sized. DDL rollback under an injected SQL failure and explicit abort were tested, not OS death/power loss during migration. Earlier server-crash qualification remains separate.

## Stage 3: end-to-end acceptance — BLOCKED / NOT RUN

Stage 1 did not pass. No dependent full acceptance or production cutover was attempted. The existing fixtures also do not supply a complete isolated API/UI deployment with candidate routing and all external stores configured. Prior retained-load and server-recovery runs are useful evidence but do not substitute for this acceptance stage.

## Required next actions

1. Diagnose/fix subscription readiness so completed startup guarantees the command subscription exists before the first request.
2. Resolve financial conflict error classification versus its intended API contract; do not simply weaken the assertion.
3. Rerun failed integration gates, then run full isolated application acceptance with an explicit deployment configuration.
4. Obtain representative production volume/distribution before approving migration downtime/rollback bounds; add process-interruption migration coverage if required for deployment.

No production fix is included in this testing campaign.

## Artifacts and cleanup

BenchmarkDotNet.Artifacts/event-log-v2/combined-20260920:

- stage1/actor-projector.trx
- stage1/actor-control.trx
- stage1/actor-candidate-repeat.trx
- stage1/financial.trx
- stage1/financial-control.trx
- stage2/results.json, timings.json, forward.sql, reverse.sql

All failed reports are retained. Three campaign integration databases and successful migration fixtures were removed; only postgres remained on the dedicated test server. Synthetic fixture data has no backup. The dedicated campaign broker is removed and the benchmark PostgreSQL container stopped. Application databases/services were not modified.

Builds of storage integration, portfolio integration and benchmark projects passed with zero warnings/errors.
