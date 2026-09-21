# Event Log V2 Focused Qualification Plan

**Date:** 2026-09-20
**Decision target:** complete production cutover from `event_log` to `event_log_v2`, with a proven rollback to the retained legacy `event_log`

## Purpose and authoritative table roles

This plan qualifies the event-log storage boundary through a focused sequence of functional, recovery, migration, performance, and operational gates. `event_log` is the current production source. `event_log_v2` is the actual future production table, not merely a benchmark name. After cutover, `event_log` is retained as a frozen legacy rollback table for an explicitly approved rollback window.

Exactly one table is authoritative whenever the application is allowed to operate. Before cutover, every production event read and write uses `event_log`. After activation, every production event read and write uses `event_log_v2`, and `event_log` receives no normal writes. Ongoing dual writes are forbidden.

Qualification covers event append; ordered read and deterministic replay; concurrency and duplicate rejection; atomic persistence of event, command audit, and projector marker; data and relationship migration; rollback synchronization; restart and uncertain-commit recovery; paired performance; exact path routing; and a full production-equivalent cutover and rollback rehearsal.

## Authority states

| State | Application writes | Authoritative table | Required condition |
|---|---|---|---|
| Before cutover | Allowed only to `event_log` | `event_log` | `event_log_v2` is a qualification/copy target only. |
| Drain/synchronize | No application writes to either table | Neither | Old writers and projectors are fully stopped and drained while final synchronization and parity checks run. |
| After activation | Allowed only to `event_log_v2` | `event_log_v2` | `event_log` is frozen, protected, backed up, and retained as legacy rollback state. |
| Rollback synchronization | No application writes to either table | Neither | All post-cutover events and related durable state are reverse-synchronized and verified. |
| After completed rollback | Allowed only to `event_log` | `event_log` | `event_log_v2` receives no normal writes. |

No transition may create a dual-write or split-brain interval. Admission remains stopped whenever neither table is authoritative.

## Invariants that must not change

All gates must preserve the following contracts exactly:

| Contract | Required evidence |
|---|---|
| Single authority | At every application-operating point, exactly one event-log table receives all production reads and writes. |
| Global ordering | Existing global `eventversion` and `sequence` values are preserved; new values remain monotonic and unambiguous. |
| Stream concurrency | Stream ID/version identity and uniqueness remain enforced under normal append, retry, and racing writers. |
| Serialized data | Timestamps and payloads remain byte-for-byte identical, or canonically equivalent only where the existing contract explicitly permits it. |
| Command audit | Command IDs, lookup keys, uniqueness behavior, and event associations remain queryable through cutover and rollback. |
| Referential integrity | All dependent relationships and incoming foreign keys remain present, valid, enforced, and routed to the authoritative table. |
| Legacy isolation | `financial_legacy_event_fence` remains installed and effective. |
| Projection durability | Durable projector markers survive commit, restart, synchronization, rollback, retry, and replay without advancing past uncommitted events. |
| Physical shape | `event_log_v2` has exactly the primary key plus three intended indexes: no missing, duplicate, legacy, or accidental indexes. |
| Legacy immutability | After v2 activation and before rollback synchronization, frozen `event_log` count and ordered fingerprint/hash remain unchanged. |

## Environment, ownership, and evidence controls

Run the consolidated qualification against the intended Release binaries and a target-like PostgreSQL version, configuration, storage class, connection policy, and resource envelope. Use an isolated database owned by the qualification run. Record binary hashes, source revision, migration identifiers, PostgreSQL version and settings, host specification, test commands, environment variables with secrets redacted, start/end timestamps, and raw outputs.

The control and candidate environments must be isolated and identically configured except for the intended table/index difference. Do not share mutable databases, queues, or workers. Pin or document background activity, retain PostgreSQL logs and statistics, and reset both sides by the same documented procedure. Approval requires one clean consolidated Q0-Q6 run on the intended Release binaries and target-like PostgreSQL.

Before Q6, create and review an exact inventory of every production event read, write, join, foreign key, journal, audit, marker, replay, projector, administrative query, and recovery path. The inventory must map each path to the production-capable routing mechanism used to switch tables and must include checks that detect literal or indirect references to the inactive table.

## Sequential qualification gates

Gates are sequential. A gate begins only after the prior gate passes and its evidence is captured. Any failure, skip, unexplained retry, or manual repair makes the consolidated run non-passing. Q0-Q4 remain the focused storage gates. Q5 is paired performance qualification only. Q6 is the definitive full cutover and rollback qualification gate; no production cutover approval exists before Q6 passes and its artifacts are reviewed.

### Q0 - Scope, configuration, and isolation freeze

Freeze the binaries, schema and migration SQL, table-routing implementation, copy and rollback tooling, runtime configuration, dataset generator, command lines, expected schema manifests, path inventory, cutover procedure, and artifact directory. Prove database ownership with a qualification-run identifier and confirm that normal application instances are disconnected. Capture the pre-test schema and validate the exact current `event_log` and intended `event_log_v2` definitions, including columns, types, defaults, constraints, primary keys, indexes, triggers, incoming foreign keys, command-audit objects, projector-marker objects, and `financial_legacy_event_fence`.

Create separate control and candidate databases from the same known baseline. Execute a short connectivity and cleanup check without changing the frozen configuration. Q0 passes only when the intended database, ownership, isolation, binaries, schemas, tooling, and routing inventory are unambiguous.

### Q1 - Core append, read, and replay

Exercise a single event, multiple events in one stream, and interleaved events across multiple streams. For each case, verify committed counts, stream IDs and versions, global `eventversion` and `sequence`, timestamps, event types, metadata, command IDs, and exact payloads. Read each stream and the global sequence in canonical order. Replay from the beginning and representative checkpoints, comparing deterministic output fingerprints and final state.

Close all application connections, reopen the database through a fresh application instance, append to existing streams, and repeat ordered read and replay checks. Include boundary payloads and representative production-like payload sizes without expanding into actor-by-actor certification. Q1 passes only when all order, identity, serialization, and deterministic replay assertions match exactly.

### Q2 - Rejection, concurrency, atomicity, and fence

| Scenario | Required result |
|---|---|
| Stale expected version | Append is rejected; no event, audit, or marker residue exists. |
| Same duplicate retry | Existing idempotency contract is honored; no second event or version advance occurs. |
| Changed duplicate | Reuse of the same command/event identity with changed content is rejected and diagnosed. |
| Same-stream race | Two writers target the same next stream version; exactly one wins and one is rejected. |
| Independent streams | Concurrent writers progress without false conflicts, loss, duplication, or ordering corruption. |
| Event/audit/marker transaction | Inject failure at each feasible boundary; event, command audit, and projector marker are all committed or all absent. |
| Legacy fence | Writes prohibited by `financial_legacy_event_fence` fail, while permitted event-log operations remain valid. |

Repeat races enough to expose timing variation and retain per-attempt outcomes. Verify stream-version uniqueness directly and confirm marker positions never exceed committed global order. Q2 passes only with one-winner semantics, correct duplicate classification, and no partial transaction.

### Q3 - Storage migration and rollback integrity

Seed **262,144 representative events** spanning multiple streams, payload sizes, timestamps, commands, audit lookups, projector positions, and referenced rows. Before migration, capture counts and stable ordered fingerprints over identity, ordering, version, timestamp, payload, and audit fields. Capture marker values, foreign-key definitions and validation state, trigger definitions, and complete primary-key/index shape.

Run and verify this focused storage sequence:

1. Forward migration from the four-index control shape to the three-index candidate shape.
2. Full counts, fingerprints, versions, audits, markers, foreign keys, trigger, primary key, and exact index-shape validation.
3. Append, ordered read, audit lookup, marker update, and deterministic replay.
4. Rollback to the original shape, followed by the same validations and append/read checks.
5. Re-forward migration and repeat all validations.
6. Reapply the migration to validate documented reapplication or idempotency behavior without schema drift.
7. Induce a controlled SQL failure and prove transactional cleanup and recoverability.
8. Induce a lock timeout and prove bounded failure, no partial DDL, no data change, and successful later retry.

Q3 passes only with zero count or fingerprint mismatches, unchanged logical contracts, exact expected schema shapes at every stage, and demonstrated forward/rollback/re-forward safety. Q3 does not substitute for the two-table production cutover and reverse synchronization required by Q6.

### Q4 - Restart and uncertain-commit recovery

Terminate a writer process before commit and verify that the entire event/audit/marker unit is absent. Then terminate or disconnect the writer after database commit but before acknowledgement reaches the caller. Treat the result as uncertain, retry deterministically using the production identity/version protocol, and prove that the committed event is discovered or safely deduplicated rather than appended twice.

Use a separate process to reopen storage and perform ordered reads, audit lookup, marker inspection, replay, and a subsequent append. Test both graceful and abrupt restart of an isolated PostgreSQL instance, then repeat deterministic replay and retry checks. Confirm sequence and stream versions contain neither corruption nor an unexplained gap caused by application-side behavior, and that durable projector markers reflect only committed work. Physical power-loss testing is optional and must be labeled separately if performed; it is not required for this focused approval.

### Q5 - Paired performance qualification only

Run a paired, same-host comparison of isolated four-index `event_log` control versus three-index `event_log_v2` candidate. Enable batching in both. Use identical Release binaries, PostgreSQL configuration, seeded inputs, client concurrency, and measurement windows. Perform two warmups, then at least four measured samples in alternating order (control/candidate, then candidate/control) to reduce drift. Retain individual samples rather than reporting only averages.

Measure append throughput; p50, p95, and p99 latency; WAL bytes per event; CPU and database waits; application allocation/GC; queue-drain time; ordered-read time; and replay time. Include the retained-history experiment so the candidate is measured against meaningful table and index size, not only a fresh database. Report dispersion, run order, database size, index size, and anomalies. There is no fixed speedup threshold, but there must be no material regression. Any apparent regression must be explained, reproduced, and accepted explicitly before Q5 passes.

Q5 contains no cutover approval or operational rehearsal. Passing Q5 only establishes the paired performance result.

### Q6 - Definitive full cutover and rollback qualification gate

Q6 must use the real production-capable routing mechanism and exact migration/rollback tooling. Benchmark-only `EventLogSqlLayout` substitution is not acceptable. Begin with a populated, authoritative `event_log` and an `event_log_v2` that has already passed its independent Q1-Q5 qualification. Rehearse the exact production process in order:

1. Verify and record a usable backup/recovery point, its scope, and its restore procedure.
2. Stop admission, fully stop and drain all old-table writers and projectors, and prove that no application writes can reach either table.
3. Record `event_log` high-water marks, sequence state, row counts, stable ordered fingerprints, command audits, projector markers, and all dependent relationship state.
4. Perform the initial copy and final delta synchronization into `event_log_v2`, preserving `eventversion`, stream ID/version, command ID, timestamp, payload, audits, markers, and every dependent relationship.
5. Validate exact source/target parity and safely set `event_log_v2` sequence state so that the next allocation cannot collide, regress, or skip through incorrect initialization.
6. Freeze and protect legacy `event_log` from normal writes; record its final count, ordered fingerprint/hash, sequence state, and protection configuration.
7. Through the production-capable routing mechanism, switch every production event read, write, join, foreign key, journal, audit, marker, replay, projector, administrative, and recovery path to `event_log_v2`.
8. Start a fresh application process. Replay streams containing pre-cutover events from `event_log_v2`; append, read, and replay new v2 events; test stale, duplicate, and atomic event/audit/marker behavior.
9. Restart the application and PostgreSQL, separately as applicable, and repeat historical replay, new-event read/replay, audit, marker, sequence, and append checks.
10. Prove that frozen `event_log` remained byte/count unchanged after activation and that no production query or write path still targets it. Correlate path-inventory checks with database statements, dependencies, permissions, logs, and observed traffic.
11. Create additional post-cutover production-shaped events in `event_log_v2`, including related audits, markers, and dependent rows, and record their identities and high-water marks.
12. Stop admission and fully stop and drain v2 writers and projectors; prove neither table accepts application writes during rollback synchronization.
13. Reverse-synchronize every post-cutover v2 event and all related durable state into `event_log`, preserving identities, ordering, stream versions, command IDs, timestamps, payloads, audits, markers, dependent relationships, and safe sequence state.
14. Verify exact parity between `event_log_v2` and `event_log`, including counts, ordered fingerprints, high-water marks, audits, markers, relationships, constraints, and next sequence allocation.
15. Switch every inventoried production path back to `event_log` through the real routing mechanism and prove no normal path remains routed to v2.
16. Start fresh processes, restart the application and PostgreSQL, replay both historical and post-cutover events, validate audits and markers, and append/read/replay a new event in `event_log`.
17. Prove `event_log_v2` receives no normal writes after completed rollback and that `event_log` is again the sole authoritative table.

Q6 passes only if this complete sequence succeeds without manual data repair, unexplained retry, missing evidence, dual writing, or split authority. The isolated rehearsal recorded below completed the Q6 harness successfully and qualifies the implementation and tooling against this gate. It does not authorize or perform production cutover.

## Rollback windows

There are two distinct rollback windows:

- **Before the first production write to `event_log_v2`:** rollback is routing-only, provided admission remains stopped while every path is switched back and the original `event_log` frozen-state fingerprint is reverified.
- **After any production write to `event_log_v2`:** reverse synchronization of all new v2 events and related durable state into `event_log` is mandatory before routing returns. Rollback must fail closed, with admission stopped and neither table accepting application writes, if completeness, identity preservation, sequence safety, or exact parity cannot be proven.

Restoring only routing after a v2 write would lose or split authoritative history and is forbidden.

## Legacy retention after activation

Retain `event_log` unchanged for an explicitly approved rollback window. Protect it from normal application writes with routing, permissions, and monitoring controls; keep it backed up; and continuously monitor its count, ordered fingerprint/hash, sequence state, schema, protection settings, and integrity. Any change is an incident and invokes the abort/fail-closed rules.

Removal, mutation, or repurposing of the legacy table requires separate approval after demonstrated `event_log_v2` stability. Passing Q6 or authorizing production cutover does not authorize legacy-table removal.

## Executable mapping for Q1-Q5

Keep and run the following existing commands for the focused Q1-Q5 qualification:

```powershell
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~EventLogDualAppenderIntegrationTests|FullyQualifiedName~EventSourceActorSnapshotRangeTests"
```

```powershell
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-marker-qualification --schema-comparison --process-restart --output=<new-output-directory>
```

```powershell
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-index-migration-qualification --seed-events=262144 --output=<new-output-directory>
```

```powershell
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-v2-benchmark --schema-batched-experiment --retained-experiment --soak-seconds=60 --repeats=2 --seed-rounds=512 --output=<new-output-directory>
```

If a listed executable or filter selects zero tests, that is a failure, not a successful run. Raw command lines and exit codes must be saved.

**Q6 command status:** `--event-log-v2-cutover-qualification` is the dedicated production-equivalent cutover/rollback rehearsal command. Isolated run `98cb3e91fd85` executed successfully against PostgreSQL 17.2, and all 11 recorded steps passed. Its evidence is retained at `BenchmarkDotNet.Artifacts/event-log-v2/cutover-qualification-20260920-r6`. The run-owned database was dropped after success. These Q1-Q5 commands must not be represented as Q6 evidence.

## Current Q6 evidence and qualification status

Run `98cb3e91fd85` proves the Q6 implementation and removes the prior tooling blocker. The rehearsal used the real production-capable `TableTarget` routing rather than benchmark-only SQL substitution. It switched and then restored five incoming foreign keys, froze legacy `event_log` and verified it remained unchanged during v2 authority, reverse-synchronized nine post-cutover events, and passed the fresh-process and PostgreSQL restart checks. After routing returned to legacy, append, ordered read, and deterministic replay passed, and writes through the inactive-v2 route were rejected.

This successful isolated rehearsal qualifies the implementation's forward-copy, final-delta, sequence-state, parity-validation, freeze/protection, foreign-key retargeting, reverse-synchronization, rollback, restart, and fail-closed routing behavior. It does not authorize or perform the production cutover. Production remains on `event_log` until a separate production authorization is granted and the approved cutover procedure is executed.

## Pass criteria and artifacts

Approval requires all of the following:

- Zero test failures and zero skips across the consolidated Q0-Q6 run.
- `event_log_v2` verified as exactly three indexes plus the primary key.
- No count, ordered fingerprint/hash, ordering, identity, version, payload, timestamp, audit, marker, relationship, or foreign-key mismatch.
- No partial event/audit/marker commit and no duplicate accepted contrary to contract.
- Deterministic replay, uncertain-commit retry, focused storage migration/rollback, and paired performance qualification proven.
- An exact, complete production path inventory and routing proof showing every read, write, join, foreign key, journal, audit, marker, replay, projector, administrative, and recovery path uses the sole authoritative table.
- One and only one authoritative table whenever application admission is open; no dual-write or split-brain interval.
- Frozen legacy `event_log` byte/count and ordered fingerprint/hash stability proven throughout v2 activation.
- Post-cutover events and all related durable state reverse-synchronized into `event_log` with exact parity and safe sequence state before rollback.
- Fresh-process and application/PostgreSQL restart paths proven both after v2 activation and after completed rollback.
- `event_log` proven to receive no normal writes after v2 activation, and `event_log_v2` proven to receive no normal writes after completed rollback.
- No material regression in Q5, with retained raw samples.
- The complete Q6 cutover and rollback sequence passed with full artifacts. There is no production cutover approval before this criterion is met.

The evidence bundle must contain configuration and schema snapshots; binary, routing, and migration/rollback-tool hashes; seed manifest; exact path inventory; test inventory and results; database and application logs; query/traffic routing proof; permissions and protection state; counts and ordered fingerprints; reconciliation queries; concurrency outcomes; both restart timelines; forward- and reverse-synchronization transcripts; sequence-state evidence; benchmark raw data and summaries; PostgreSQL statistics; resource metrics; backup/recovery-point verification; frozen-legacy integrity monitoring; and the Q6 checklist with operator timestamps and decision points.

## Abort and fail-closed rules

Abort immediately, preserve the state and evidence, and keep admission stopped on any of the following:

- Wrong database name or ownership marker, a database not owned by the qualification run, an unexpected connected application, or a baseline that differs from the frozen manifest.
- Any dual-write, split-brain, mixed-read, or mixed-authority observation.
- Any v2/legacy count, ordered fingerprint/hash, identity, sequence, audit, marker, payload, relationship, or schema parity failure.
- Any unknown or unaccounted-for post-cutover event or related durable-state change.
- Any inability to reverse-synchronize completely, preserve identity, set sequence state safely, or prove exact parity.
- Any write to frozen `event_log` after v2 activation or normal write to `event_log_v2` after completed rollback.
- Any production path that cannot be inventoried, switched, or proven to target only the authoritative table.
- Any unexpected timeout, lock timeout outside the deliberate Q3 case, SQL error, process-control ambiguity, invariant failure, partial commit, or inability to retain required evidence.

Do not attempt corrective writes or silently rerun a failed step. After any v2 production write, rollback must fail closed if reverse synchronization and parity cannot be proven. A new consolidated run starts only after the cause is understood and the environment is reset through the documented procedure.

## Frozen qualification scope and requalification triggers

Qualification scope is frozen after this revision. No additional actor, Trade, Portfolio, broker, MTM, market-feed, or UI gates may be added. Such areas remain outside this focused approval; a failure observed through them blocks approval only when it demonstrates a retained event-log invariant or Q6 path defect.

Requalification is required only if the schema, writer/reader, migration/rollback tooling, table-routing implementation, retained invariants, or cutover procedure changes, or if Q6 exposes a defect. Adding unrelated workflow certification is not a prerequisite for event-log approval.

## Approval boundary

This plan approves only the event-log storage transition and its named persistence, recovery, synchronization, performance, routing, cutover, rollback, and retention contracts. It does not certify all IFM workflows and must not be represented as Trade, Portfolio, broker, MTM, market-feed, UI, or actor-by-actor recertification.

The successful Q6 isolated rehearsal proves that `event_log_v2` qualifies for complete cutover and that `event_log` remains a usable rollback target. It removes the event-log tooling blocker but neither authorizes nor performs production cutover. Production cutover remains a separate, explicit authorization and execution decision.
