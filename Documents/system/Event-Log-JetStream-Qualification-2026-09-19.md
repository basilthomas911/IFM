# Event-log marker JetStream qualification

Date: 2026-09-19

## Result

**4/4 new paired real-broker cases passed**, together with **14/14 prior projector pipeline cases** and **6/6 existing durable-queue integration regressions**. No production implementation or configuration was changed. This is correctness qualification, not a throughput measurement or production activation decision.

## New coverage

Both original and batched marker writers use the existing event_log schema in a generated disposable PostgreSQL database. Each writes one event with a durable projection marker. The real production NatsJSDurableReplayQueue publishes its serialized event twice with the same identity while workers are stopped. The publisher and its connection are disposed. A new queue instance consumes the persisted broker message through the real BaseEventProjector and PostgreSQL lease/checkpoint engine into a receipt-backed synthetic target.

| Scenario | Original | Batched |
|---|---|---|
| Queue recreation, duplicate publication suppressed, target applied once | Passed | Passed |
| Exception after projection commits, real process-to-replay handoff, fresh projector on replay, no repeated target mutation | Passed | Passed |

Server-side assertions require one stored process message, zero replay messages for normal delivery or one for the injected failure, and zero pending/unacknowledged messages on both consumers. Received event identity and event-log ID must match the committed source. Delivery counts must be one or two respectively. Target mutation count and stream checkpoint must both be one.

The injected exception occurs after projector completion but before queue acknowledgment. It is not an OS-process kill or broker restart. Queue recreation means new queue/connection objects in the same test process; no actor restart is claimed. Duplicate suppression is tested within JetStream's configured duplicate window, not indefinitely.

## Evidence

Final combined suite: 18 passed, zero failed/skipped. Existing durable queue suite: 6 passed, zero failed/skipped. The initial 18-case run passed before server-side acknowledgment assertions were added; it is not additional unique coverage.

Independent SQL after the final test process completed:

| State | Count |
|---|---:|
| Events | 278 |
| Markers | 278 |
| Unfinished markers | 0 |
| Target receipts | 278 |
| Target mutations | 278 |

- [Final tests](../../BenchmarkDotNet.Artifacts/event-log-v2/jetstream-20260919/jetstream-final.trx)
- [Durable queue regressions](../../BenchmarkDotNet.Artifacts/event-log-v2/jetstream-20260919/durable-queue-regression.trx)
- [Initial run](../../BenchmarkDotNet.Artifacts/event-log-v2/jetstream-20260919/jetstream-and-pipeline.trx)
- Test source: TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/MarkerProjectorJetStreamTests.cs
- Final test-source SHA256: CC9C72E6343139304B987EB68B8DED7624BBD0BBF404363390028EC83889F14D

## Isolation and reproduction

PostgreSQL used the existing owned benchmark container, loopback 25432. JetStream used a new nats:2.12.0-alpine container, ifm-marker-jetstream-20260919, published only on 127.0.0.1:24223, labeled ifm.purpose=eventlog-marker-qualification. Neither application broker nor application database was touched.

The test requires IFM_MARKER_TEST_NATS_URL exactly nats://127.0.0.1:24223 and the existing fixture guard requires an explicitly supplied benchmark database on 127.0.0.1:25432. Set DOTNET_ENVIRONMENT/ASPNETCORE_ENVIRONMENT to Test and test-only POSTGRES_TEST_KEY credentials. Supply IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION with a fresh database matching ifm_eventlog_bench_<12 lowercase hex>_<suffix>. Follow the prior pipeline report for schema fixture setup.

Run the storage integration project in Release with filter FullyQualifiedName~MarkerProjectorPipelineTests. Run the messaging Nats.IntegratedTests project with filter FullyQualifiedName~NatsJSDurableReplayQueueIntegrationTests and IFM_NATS_URL set explicitly to the same isolated endpoint. The existing messaging tests otherwise default to the application endpoint; do not omit that override.

Cleanup completed: databases ifm_eventlog_bench_a741c038b925_jetstream and ifm_eventlog_bench_c82654d709a1_jetstream were dropped; the disposable NATS container and its synthetic stream data were removed; the benchmark PostgreSQL container was stopped. Test results remain on disk; temporary data was intentionally discarded and has no backup.

## Remaining gate

This closes the real durable queue serialization, broker persistence, duplicate publication and process-to-replay boundary for the candidate marker writer. It does **not** close full command-actor end-to-end qualification. Append is invoked directly, queue handlers enter the projector directly, the target is synthetic and blackboard cache is substituted. Actual actor mailbox routing, actor restart, downstream business publication consumption and production read models remain outside scope.

The next actor-host gate still needs a test-only guarded way to select the candidate appender inside EventSourceActorDbContext. Its current public construction path selects the original writer. This experiment deliberately did not alter production composition to enable that selection. Longer-duration performance/GC testing and controlled activation/rollback remain outstanding.
