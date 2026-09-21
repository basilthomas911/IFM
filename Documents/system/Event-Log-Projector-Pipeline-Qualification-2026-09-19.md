# Event-log to projector pipeline qualification

Date: 2026-09-19

## Outcome and scope

**14/14 new paired integration cases passed**, plus **30/30 existing PostgreSQL storage/recovery tests** and **26/26 focused projector unit regressions**.

The new suite connects the actual original/batched event-log appenders to the real PostgreSQL event-source context, BaseEventProjector execution engine, recovery coordinator, lease/checkpoint state machine, and—in the publication cases—the real background outbox dispatcher.

**This is not full actor/NATS end-to-end qualification.** Queue delivery is replaced with an in-process callback, outgoing transport sends are substituted, the blackboard uses an in-memory cache substitute, and the target projection is a synthetic PostgreSQL read model with atomic receipts. No real command actor mailbox, JetStream broker, production fund read model, UI, or broker execution workflow is started.

The existing full actor-host fixture does not expose a way to select the benchmark-only batched appender. Rather than activate production routing or label mocked transport as real coverage, this experiment explicitly tests the storage/projector boundary. Full actor-host transport qualification remains open.

No production code, routing, schema, application queue configuration or running IFM service was changed. The only new executable code is in the storage integration-test project.

## Paired cases

Each row ran once against the original writer and once against the batched writer.

| Scenario | Verified behavior | Passed |
|---|---|---:|
| Multi-stream bounded recovery | Eight streams × 16 persisted events, recovery pages of seven and cross-stream concurrency four; all target mutations and checkpoints complete; a fresh projector finds no pending recovery work; duplicate delivery causes no additional mutation | 2/2 |
| Failure before target mutation | Real engine releases its lease into Retrying, without target effect or advanced checkpoint; a fresh projector recovers both events | 2/2 |
| Failure after target transaction commits | Receipt and target mutation survive the injected exception; checkpoint has not advanced; recovery recognizes AlreadyApplied and completes without a second target mutation | 2/2 |
| Out-of-order same-stream delivery | Delivering the second event first produces no target effect/checkpoint; ordered recovery subsequently completes both | 2/2 |
| Competing projector instances | Hold the first instance inside apply after claim; the second cannot apply the same event; later duplicate delivery remains harmless | 2/2 |
| Expired owner | An explicitly expired persisted lease is taken over; completed state rejects renewal by the old token/revision | 2/2 |
| Real outbox publication retry | Public projector startup starts the real dispatcher; first transport send fails, second succeeds with the same publication identity; persisted outbox reports Published with two attempts; target mutation remains single-applied | 2/2 |

The typed publication case forwards the source event after applying the projection. Business-specific completed/failed-event factories are disabled in this synthetic descriptor; the test does not claim to exercise every terminal event shape.

The post-target failure is an injected exception, not an OS-process kill. Abrupt process/server failure was exercised separately in the [process/restart qualification](Event-Log-Process-and-Restart-Qualification-2026-09-19.md).

## Actual integration path

1. The actual BinaryCopyEventLogAppender commits command audit, events, stream versions and required markers.
2. Events are read back and deserialized through EventSourceActorDbContext.
3. The actual recovery coordinator pages persisted pending markers and groups delivery by source stream.
4. The callback delivers to the public BaseEventProjector.ProcessDomainEventAsync entry point.
5. Its actual fenced engine claims the PostgreSQL lease and runs the target descriptor.
6. A separate target transaction writes an idempotency receipt and read-model mutation atomically.
7. The engine terminalizes the marker and advances the persisted stream checkpoint.
8. In the publication cases, the real outbox worker claims, deserializes and retries the staged message; only its external send is substituted.

Both writers operate on the unchanged **event_log** table, with four indexes retained. The candidate uses the existing internal guarded benchmark layout with table renaming disabled and marker batching enabled. This tests compatibility with the existing table name/schema without enabling batching in production.

The target receipt is keyed by projector/event identity and stores the effect's message identity. The target mutation advances in stream order. Receipt insertion and mutation commit together; on retry an existing receipt returns AlreadyApplied. This demonstrates the required idempotency pattern, not automatic exactly-once behavior for an arbitrary downstream consumer.

## Final durable evidence

Before cleanup, independent SQL checks on the final fixture found:

| Item | Count |
|---|---:|
| Events | 274 |
| Projection markers | 274 |
| Unfinished markers | **0** |
| Target receipts | 274 |
| Target mutations | 274 |
| Audited commands | 28 |
| Stream checkpoints | 28 |
| Published outbox records | 2 |
| Minimum/maximum outbox attempt count | 2 / 2 |
| Original event-log indexes | 4 |

Every expected target effect was present once. Repeated delivery and the injected post-commit failure did not add mutations.

## Verification and artifacts

- Initial pipeline suite: 12/12 passed before publication coverage was added.
- Final extended suite: **14/14 passed**, no skipped cases.
- Existing snapshot/projector persistence/recovery suite: **30/30 passed**.
- Focused reliability/recovery/outbox/transient-flow unit suite: **26/26 passed**.
- Release test-project build: zero warnings and zero errors.
- [Final paired integration results](../../BenchmarkDotNet.Artifacts/event-log-v2/projector-pipeline-20260919/projector-pipeline-final.trx)
- [Real PostgreSQL regression results](../../BenchmarkDotNet.Artifacts/event-log-v2/projector-pipeline-20260919/storage-recovery.trx)
- [Focused unit regression results](../../BenchmarkDotNet.Artifacts/event-log-v2/projector-pipeline-20260919/projector-unit-regression.trx)
- [Initial integration results](../../BenchmarkDotNet.Artifacts/event-log-v2/projector-pipeline-20260919/projector-pipeline.trx)
- [Fixture counts, source hashes and boundaries](../../BenchmarkDotNet.Artifacts/event-log-v2/projector-pipeline-20260919/evidence.json)
- Test source: TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/MarkerProjectorPipelineTests.cs

Both disposable fixture databases were removed after verification. Only postgres remained on the isolated instance, and the benchmark container was stopped. No application data was removed.

## Remaining gate / next test

Add isolated real actor-host/JetStream test wiring with a **test-only** way to select the candidate writer. Then exercise actual command-mailbox delivery, commit-to-projector handoff, transport redelivery, actor restart and downstream publication handling.

That wiring must remain restricted to generated disposable database names and isolated broker endpoints. It should not turn the benchmark option into a production default. Longer production-like memory/storage testing and controlled activation/rollback design also remain outstanding.

This experiment is correctness evidence, not another throughput benchmark or approval to remove durable command audit, projection receipts or checkpoints.

## Reproduction

Start the dedicated benchmark PostgreSQL container on 127.0.0.1:25432 and wait for it to be ready. Create a new owned database named ifm_eventlog_bench_<12-lowercase-hex-digits>_projector. The fixture rejects missing connection settings, the normal application port, non-loopback hosts and names that fail the benchmark guard before schema creation.

Set DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT to Test. Set POSTGRES_TEST_KEY to the dedicated test credentials, and IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION to the fresh fixture's direct PostgreSQL connection with those credentials. Do not use an application database.

~~~powershell
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj -c Release --filter FullyQualifiedName~MarkerProjectorPipelineTests --logger "trx;LogFileName=projector-pipeline.trx" --results-directory BenchmarkDotNet.Artifacts/event-log-v2/new-projector-run
~~~

Use a fresh database for each run: synthetic target tables are intentionally created once, not silently reused. Drop only that owned database after recording evidence. Do not run multiple schema-initializing test classes against the same fixture concurrently.
