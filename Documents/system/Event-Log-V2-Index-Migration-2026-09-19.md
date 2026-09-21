# Event-log index migration rehearsal

Status: benchmark-only implementation and qualification; no production migration registered or applied.

## Outcome

22/22 final checks passed across empty and populated disposable databases. An initial 18-check run also passed before lock-contention and injected-error cases were added. Release builds passed with zero warnings/errors.

The redundant-index reapplication issue is avoided by preserving the existing index name:

```sql
ALTER TABLE public.event_log DROP CONSTRAINT event_log_pkey;
ALTER TABLE public.event_log ADD CONSTRAINT ux_event_log_stream_version_v3
    PRIMARY KEY USING INDEX ux_event_log_stream_version_v3;
```

The new constraint intentionally takes the existing index name. PostgreSQL therefore does not rename that index away from the name used by V3 startup SQL. Full EventSourceSchemaDb.CreateAllAsync reapplication twice retained exactly three event-log indexes. No production startup SQL change is needed for this approach.

This is index consolidation on event_log, not a table rename or routing cutover. The measured improvement comes from the index layout, not the event_log_v2 name. Event identity/sequence, command lookup, timestamp representation, durable markers/audit, payload checks, incoming identity foreign keys and financial fence remain.

## Rehearsal coverage

Each fixture passes these eleven checks:

1. A held reader lock causes the migration to fail with lock timeout, leaving the original layout/data intact.
2. An injected SQL error after forward DDL rolls back the entire transaction.
3. Explicit forward transaction rollback preserves the original layout.
4. Forward migration gives three indexes and the stream/version primary key.
5. Repeating forward migration is a no-op on the expected candidate state.
6. Reapplying the full application schema twice does not recreate the fourth index.
7. Explicit reverse transaction rollback preserves the candidate layout.
8. Reverse migration restores the original primary key plus separate stream/version unique index.
9. Repeating reverse migration is a no-op on the expected original state.
10. Full schema reapplication after reverse preserves the four-index layout.
11. Forward migration after reverse, followed by full schema reapplication, restores and retains three indexes.

Both fixtures additionally append successfully after forward, reverse and re-forward through the actual batched binary COPY writer. Stream versions and event counts advance correctly. The populated fixture begins with 256 events from 32 audited commands; the initially empty fixture has no events. These are correctness fixtures, not production-scale migration timing tests.

Every checkpoint compares ordered serialized row fingerprints of events, command audit and projector markers before/after DDL, checks the exact primary key and index count, verifies at least five incoming event-identity foreign keys, and confirms that the financial fence remains enabled. Fingerprints test data preservation; prior qualifications provide payload replay/projector execution coverage.

## Transaction and operational boundaries

The harness applies each forward/reverse body inside an explicit transaction, with a two-second lock timeout, a 30-second statement timeout and ACCESS EXCLUSIVE table lock. Reverse migration rebuilds the old primary-key index and the stream/version unique index, so its duration and blocking can be significant on a large store. Do not infer production timing from these small fixtures.

The exported forward.sql/reverse.sql files are body fragments, not standalone production deployment scripts: the transaction, lock and timeout wrapper is in the harness. The candidate accepts the known original and target PK shapes; unsupported shapes fail. This is not a generic repair tool for arbitrary schema drift or a previously renamed experimental constraint.

No OS-process crash was injected during migration itself. Explicit transaction rollback and SQL-error rollback were tested; the preceding qualification separately covered writer/server crashes with the candidate index layout.

## Implementation and artifacts

New benchmark-only EventLogIndexMigrationQualification is dispatched by --event-log-index-migration-qualification. It validates the dedicated labelled PostgreSQL container, loopback port 25432, empty admin server, enabled durability and generated database names before creating fixtures. Successful fixtures are automatically dropped. Only postgres remained afterward.

Reproduce with the existing isolated benchmark admin/test credential environment:

```text
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-index-migration-qualification --output=BenchmarkDotNet.Artifacts/event-log-v2/schema-migration-final-20260919
```

Use a fresh output directory on subsequent runs. Artifacts contain results.json plus forward/reverse SQL bodies. The initial run is under schema-migration-20260919.

## Next gate

The schema reapplication issue is resolved in the tested candidate design. Before production activation: qualify real broker/actor/financial workflows against the name-preserving candidate and choose an explicit deployment/rollback procedure, including production-sized lock/rebuild timing and interruption testing. Marker batching remains separately controlled; no production routing or configuration has changed.
