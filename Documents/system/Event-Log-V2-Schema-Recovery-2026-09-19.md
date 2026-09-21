# Three-index event-log recovery and projector qualification

Status: isolated qualification completed; production unchanged.

## Results

- 36/36 fault/concurrency cases: four-index and three-index event_log_v2 layouts, both with marker batching, three repetitions of six scenarios.
- 30/30 process/restart cases on the same two layouts after a benchmark-admin connection fix.
- 44/44 scoped projector integration cases: 22 on each index layout. These use the existing event_log table name and both original/batched writer cases.
- Benchmark and integration projects built in Release with zero warnings/errors.

Fault cases cover competing stream versions, duplicate commands, checkpoint advancement, backend termination, cancellation after admission and lost commit acknowledgment. Process cases cover cross-process version/duplicate races, abrupt writer death, graceful PostgreSQL restart and SIGKILL server recovery. Durable event, marker, audit, stream-counter and retry invariants passed. These are not physical power-loss tests or throughput measurements.

Projector cases use the actual PostgreSQL-backed recovery/checkpoint/lease engine and outbox dispatcher with a synthetic target and substituted delivery/transport where applicable. The scoped filter excludes JetStream_queue and Command_actor_routes cases. This does not qualify real broker/actor integration with the new schema, financial business workflows, or a production table rename.

## Harness changes and failed attempts

EventLogMarkerQualification now accepts --schema-comparison. Both variants batch markers, and fixtures assert the exact primary key, three/four index count and at least five incoming identity foreign keys. Production persistence code is unchanged.

The first restart run passed all 15 four-index cases, then cleanup failed with an exception reading the admin connection after deliberate server crashes. The completed disposable fixture was inspected and removed. Administrative pooling is now disabled in this fault harness so reopening establishes a fresh socket. The full rerun passed all 30 cases and cleaned its fixtures successfully.

MarkerProjectorFixture accepts the test-only IFM_PROJECTOR_SCHEMA_TEST=three-index switch after the existing loopback/generated-database guard, and verifies its actual PK/index count. The default fixture retains four indexes.

The initial projector class filter selected 30 cases: 22 passed and eight failed because the explicitly required isolated JetStream endpoint was not supplied. The failures occurred at environment prechecks, not schema assertions. This report preserves that failed TRX and does not count those eight as qualified. Fresh scoped reruns passed 22/22 for candidate and 22/22 for control, with zero skips.

## Confirmed migration issue

Promoting ux_event_log_stream_version_v3 with PRIMARY KEY USING INDEX renames the index to the primary-key constraint name. The existing CreateEventStreamVersionAndProjectorCheckpointV3 SQL still contains:

```sql
CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_stream_version_v3
    ON event_log (EventStreamId, StreamVersion);
```

Executing this statement in a transaction on the populated three-index projector fixture produced index counts 3 -> 4. ROLLBACK restored 3. Thus reapplying this SQL can silently undo the index-count optimization. This experiment does not assert that every normal startup reruns that migration.

A deployment migration must coordinate index/constraint naming with fresh-install and schema-reapplication logic, and supply a tested reverse migration. Retaining the existing event_log name would avoid an unrelated table-routing cutover; the performance gain comes from indexes, not the v2 name.

## Decision / remaining gates

The index candidate retains its prior measured incremental gain (4.9% median paired retained-history throughput) and passed this storage/recovery scope. Do not activate production yet. Remaining work: idempotent forward/rollback migration with populated data and schema reapplication, and real broker/actor/financial workflow qualification against the candidate. Global event identity, command lookup/audit, financial fence and timestamp format remain unchanged.

## Artifacts and reproduction

- BenchmarkDotNet.Artifacts/event-log-v2/schema-fault-20260919: 36-case fault results.
- schema-restart-20260919: initial 15 passed cases followed by admin-cleanup failure.
- schema-restart-rerun-20260919: successful 30-case rerun, metadata and child/server evidence.
- schema-projector-20260919: initial three-index.trx, plus successful three-index-scoped.trx and four-index-scoped.trx.

Fault invocation: --event-log-marker-qualification --schema-comparison; add --process-restart for OS-process/server tests. Use the existing isolated admin connection and a new output directory.

Projector invocation: dotnet test with filter FullyQualifiedName~MarkerProjectorPipelineTests&FullyQualifiedName!~JetStream_queue&FullyQualifiedName!~Command_actor_routes, on a fresh guarded database. Set IFM_PROJECTOR_SCHEMA_TEST=three-index only for candidate, unset it for control. All test data is disposable; artifacts preserve test evidence, not a database backup.
