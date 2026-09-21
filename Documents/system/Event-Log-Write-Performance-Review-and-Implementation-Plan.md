# Event-log write-performance review and implementation plan

> **Historical baseline notice (2026-09-21):** This review records the pre-cutover event-log shape observed on 2026-09-19. The `financial_legacy_event_fence` and its supporting legacy Fund objects were subsequently removed by Portfolio financial schema version 2. References to that fence below describe the measured historical baseline, not the current schema.

Date: 2026-09-19
Status: Proposed; no implementation or database migration authorized by this review.

## 1. Decision

Proceed with measured, staged optimization, not the proposed stripped-down schema as written. Preserve durable command deduplication, optimistic concurrency, financial cutover fencing, event identity, and recovery. There is no current measurement establishing that the sequence is the dominant bottleneck or supporting a promised TPS target.

This review inspected repository code and the live local PostgreSQL 17.2 database event-source-dev-db in container ifm_db through read-only catalog queries. Production deployment and runtime configuration were not inspected. No workload, migration, configuration change, or application restart was performed.

## 2. Verified current state

The local event_log has an estimated 953,586 live rows and approximately 1,949 MB total storage including indexes/TOAST. These are observations, not a fresh exact count or performance measurement.

| Index | Definition | Size | Observed cumulative scans |
|---|---|---:|---:|
| event_log_pkey | unique (eventstreamid, eventnameid, eventversion) | 63 MB | 513,392 |
| ux_event_log_event_version | unique (eventversion) | 21 MB | 29,681,066 |
| ix_event_log_command_id | (commandid) | 36 MB | 12 |
| ux_event_log_stream_version_v3 | unique (eventstreamid, streamversion) | 32 MB | 67,035 |

Statistics reflect their collection window and do not prove an index is unnecessary or identify current latency. In particular, a rare recovery lookup can still require an index.

The sequence cache is 1. eventtimestamp is text. The financial_legacy_event_fence is a BEFORE INSERT row trigger.

Five foreign keys reference eventversion: projector execution state, business subscription projection receipts, business subscription projection issues, financial history receipts, and financial operation receipts.

### Writer paths

- EventSourceActorDbContext selects its regular appender through WriteMode; checked-in base/production configuration selects Sequential.
- The atomic command-plus-event path separately creates BinaryCopyEventLogAppender regardless of that setting. Switching WriteMode alone does not describe or tune all writes.
- Binary COPY already batches commands, reserves command audit records, locks streams, validates expected versions, updates stream counters, reserves global IDs, copies events, writes required projection markers, and commits before acknowledgment.
- Its single channel reader and retained connection serialize that appender instance's batches.
- Required projection markers currently incur individual awaited SQL commands within the transaction.
- Command IDs are durably reserved in command_log, with payload-hash validation. An in-memory duplicate coordinator supplements this guarantee; it does not replace it.

Historical repository benchmarks dated September 11 reported worse single-event/concurrent-command acknowledgment latency for Binary COPY despite reduced allocations. They are useful warnings, not measurements of today's atomic audit path. Rebaseline that exact path before choosing a writer.

## 3. Assessment of the submitted proposal

### Remove bigserial / EventVersion: reject for this optimization

Sequence allocation has synchronization cost, but it does not serialize entire transactions across all streams. Measure sequence waits before attributing the workload bottleneck to it. EventVersion currently supplies stable event identities, foreign-key targets, projector lookups, and some replay/snapshot boundaries. Removing it requires a separate coordinated identity/recovery redesign.

Do not increase sequence caching immediately either. Cached ranges are session-local and can produce numerically out-of-order IDs across sessions. Existing per-stream readers order by EventVersion and snapshot readers use its maximum. Move those ordering/boundary decisions to StreamVersion first. Global identity is not a cross-stream commit-order cursor. See [PostgreSQL sequence semantics](https://www.postgresql.org/docs/17/sql-createsequence.html).

### Composite stream primary key: accept conditionally

(eventstreamid, streamversion) is the right per-stream uniqueness key, and its unique index already exists. Consolidating it with the primary key can remove the old three-column index after dependent reads are qualified.

A unique constraint alone does not enforce expected-version equality, gap-free version assignment, or multi-event atomicity. Preserve database stream-counter locking and transactional validation; actor mailboxes do not protect against another process or a stale writer.

Keep the unique EventVersion index because foreign keys and identity lookups depend on it. Keep the command index initially pending a complete consumer/recovery audit. The first target is three event-log indexes, not one.

### Remove financial trigger: reject without an equivalent transactional guard

The trigger looks up the stream for each event. For Fund/FundTransaction command streams it obtains the fund advisory transaction lock (namespace 34101) and rejects a Fenced legacy writer scope. This prevents writes racing financial cutover.

An application-only check has a check/write race and an asynchronous check happens too late. Optimize the database guard, not its safety guarantee.

Candidate: a statement-level AFTER INSERT trigger with a transition relation, finding distinct affected funds and locking them in deterministic order before checking the fence. It still executes before commit and rejects the whole transaction on violation. Qualify COPY behavior, all legacy/direct writers, and lock ordering against the cutover implementation before replacement. Transition relations are supported by [PostgreSQL triggers](https://www.postgresql.org/docs/17/sql-createtrigger.html); suitability for IFM requires integration proof.

### Native timestamp: accept through a compatibility migration

Use an explicitly supplied UTC timestamptz value representing the existing timestamp semantics. A server clock default must not silently replace the original event time or historical timestamps.

PostgreSQL timestamps have microsecond precision, whereas .NET ticks support 100 ns. Decide whether exact original precision is required before conversion; retain original text/ticks if necessary. See [PostgreSQL date/time types](https://www.postgresql.org/docs/17/datatype-datetime.html).

This improves type safety and can reduce storage/parsing costs, but payloads, WAL, transaction round trips, and indexes must also be measured. Column ordering alone is not a guaranteed material win.

### Replace durable deduplication with memory/Scylla: reject

Restart, multiple writers, cache eviction, and lost commit acknowledgments require a durable authority in the same transaction as the event append. An asynchronously updated read model cannot provide that atomicity. Keep command_log's unique command ID and payload hash.

A nonunique event_log command index is distinct from command_log's deduplication constraint. Hash indexes cannot enforce uniqueness and are not universally faster; only consider one for a measured equality-lookup workload. See [PostgreSQL hash indexes](https://www.postgresql.org/docs/17/hash-index.html).

### CLUSTER ON: omit from the write-performance path

ALTER TABLE ... CLUSTER ON only selects the index for a future clustering operation. Actual CLUSTER physically reorders once, takes an exclusive table lock, and does not maintain ordering for future appends. It is optional offline recovery/read-layout maintenance, not a continuously clustered append log. See [PostgreSQL CLUSTER](https://www.postgresql.org/docs/17/sql-cluster.html).

## 4. Implementation stages

### Stage A — Baseline and observability

1. Inventory all writers/readers: regular, atomic audited, direct transaction, raw SQL, COPY, migration, recovery, snapshot, projector, and financial receipt paths.
2. Record effective deployed configuration separately from checked-in defaults.
3. Instrument queue age/depth/bytes, serialization, command reservation, stream-lock wait, counter update, sequence reservation, COPY, marker insertion, and commit.
4. Measure events/sec, commands/sec, and database transactions/sec separately; capture p50/p95/p99 commit acknowledgment, CPU, allocations/GC, WAL bytes, disk latency, database waits, and replay/startup duration.
5. Benchmark an isolated database clone with representative payloads, 1/64/256 events per command, independent streams, hot-stream skew, concurrent replay, financial fences, durable markers, and mixed writer paths. Keep durability enabled.
6. Establish numerical acceptance thresholds from the baseline before promoting changes. Do not generate benchmark writes in the active trading database.

Deliverable: reproducible baseline and ranked measured costs.

### Stage B — Reduce writer overhead without changing guarantees

1. Replace per-event required projection marker commands with a set-based insert inside the existing transaction, preserving keys, timestamps, conflict semantics, and all-or-nothing behavior.
2. Add byte-aware queue/backpressure limits alongside request-count limits. Explicitly handle a single atomic command larger than normal batch limits; never split its commit.
3. Measure batch-age scheduling, connection utilization, and single-reader head-of-line delays.
4. Qualify duplicate/version-conflict behavior when one invalid request shares a batch with unrelated requests. Isolate safely classified failures if needed; never blindly retry an ambiguous commit.
5. If measurements justify multiple writer lanes, defer activation until Stage C is complete. Use stable stream affinity, bounded connections, and database concurrency checks across processes.
6. Benchmark compression and batch-size choices; do not switch the default writer solely because COPY sounds faster.

Deliverable: lower round-trip/queue cost with unchanged durable acknowledgment.

### Stage C — Make replay ordering explicitly stream-based

1. Replace per-stream ORDER BY EventVersion and snapshot MAX(EventVersion) boundaries with StreamVersion-based ordering and boundaries.
2. Preserve EventVersion for identity, receipts, foreign keys, and exact-event lookup.
3. Audit every global cursor/checkpoint; do not assume a sequence value equals commit order.
4. Migrate snapshot/checkpoint metadata compatibly and validate existing stream versions, gaps, duplicates, and legacy backfills. Do not renumber historical events blindly.
5. Test replay with intentionally non-monotonic global IDs but monotonic stream versions, including snapshot-plus-tail recovery.
6. Only then benchmark sequence cache candidates and additional writer lanes. Leave CACHE 1 if there is no demonstrated benefit.

Deliverable: correct recovery independent of global allocation order.

### Stage D — Consolidate indexes

1. Audit old-primary-key dependent reads, foreign keys, named ON CONFLICT clauses, replica identity, publications/CDC, and administrative tooling.
2. Qualify event-type-filtered latest-event and snapshot queries that may benefit from the old three-column key.
3. On a clone, promote the existing unique stream/version index to the primary key and remove the old primary-key index in a controlled migration. Confirm index suitability and exact PostgreSQL rename/constraint behavior.
4. Keep unique EventVersion and the command-ID B-tree initially.
5. Update schema bootstrap definitions with the migration so startup does not recreate removed indexes.
6. Use a short approved lock window, lock timeout, prerequisite validation, and a tested rollback/rebuild procedure.

Deliverable: one fewer maintained B-tree if read/recovery gates pass. Defer command-index removal until its rare operational uses have been assessed.

### Stage E — Migrate timestamps safely

1. Define UTC meaning and precision policy.
2. Add a nullable native timestamp column; preserve the original column.
3. Update every writer, including COPY column/type definitions and direct transaction paths, to dual-write.
4. Backfill in bounded batches with explicit timezone parsing, invalid-value reporting, restartable progress, and no substitution of current time.
5. Validate completeness and precision; migrate readers with compatibility fallback.
6. Enforce required constraints and perform the final naming/cutover only after older writers are excluded.
7. Retain the original values through the rollback period; remove them only after explicit retention approval.

Deliverable: native timestamp storage without historical time corruption.

### Stage F — Optimize financial fencing if measured worthwhile

1. Implement the statement-level candidate in an isolated migration/test environment.
2. Preserve the same fund advisory-lock protocol, scope rules, rejection behavior, and transactional rollback.
3. Audit lock ordering against stream locks and the cutover transaction to prevent deadlocks.
4. Verify ordinary INSERT, multirow INSERT, COPY, direct legacy writers, multiple funds, and mixed financial/nonfinancial batches.
5. Race cutover against append repeatedly; prove no legacy event commits after the corresponding fence takes effect.
6. Replace the original guard atomically only after correctness and latency gates pass; otherwise retain it.

Deliverable: less per-row guard work, not weaker financial protection.

## 5. Concrete change areas

Paths are relative to the repository root.

- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/BinaryCopyEventLogAppender.cs: batching, markers, optional lanes, metrics.
- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogAppenderSupport.cs: marker preparation and timestamp handling.
- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/SequentialEventLogAppender.cs: compatibility and matched baseline.
- TomasAI.IFM.Application.Storage/EventSourceDb/EventSourceActorDbContext.cs: routing, snapshot/replay boundaries, atomic audit path.
- TomasAI.IFM.Application.Storage/EventSourceDb/EventSourceDbSql.cs: stream-ordered queries, snapshot selection, migration-compatible SQL.
- TomasAI.IFM.Application.Storage/EventSourceDb/Schema/EventSourceSchemaSql.cs and EventSourceSchemaDb.cs: coordinated schema/bootstrap changes.
- TomasAI.IFM.Application.Storage/CommandAudit/CommandAuditPostgres.cs: preserve atomic command reservations and hash conflict behavior.
- TomasAI.IFM.Application.Storage/PortfolioDb/PortfolioDbSql.cs: equivalent database financial fence.
- Storage integration tests, event-log benchmarks, and TomasAI.IFM.Domain.Portfolio.IntegrationTests/Persistence/LegacyFinancialWriterFenceIntegrationTests.cs: expanded qualification.

The writer inventory must identify any additional direct SQL paths before coding.

## 6. Required verification after each stage

Unit/BDD:

- Given a stale expected version, append rejects without partial events or counter advancement.
- Given duplicate command IDs, matching payloads cannot append twice and mismatched payloads reject.
- Given multiple events in one command, either every event/audit/required marker commits or none does.
- Given queue pressure or shutdown, memory stays bounded and accepted requests receive a terminal result.
- Given distinct streams, concurrency does not violate ordering within either stream.

Database integration/fault tests:

- Two independent writer processes racing the same stream.
- Crash before commit, after commit before acknowledgment, cancellation, disconnect, retry, and restart.
- Out-of-order global IDs, sequence gaps, mixed appenders, snapshots, and complete replay.
- Projection marker/outbox recovery and all five EventVersion foreign-key relationships.
- Financial fence races including multi-fund COPY; no bypass or partial financial writes.
- Timestamp offsets, invalid text, microsecond boundaries, historical values, and mixed-version writers.
- Schema upgrade, bootstrap restart, interrupted migration resumption, and rollback.
- Read performance after index consolidation, including event-type lookups and actor startup recovery.

Performance qualification:

Compare one change at a time against the same dataset and hardware. Require repeatable throughput improvement or lower tail latency/allocation without correctness, recovery, or resource-bound regressions. Historical short benchmarks are not deployment approval.

## 7. Rollout and rollback

Rehearse on an isolated restored database. Deploy additive schema and compatible readers/writers first. Drain incompatible writers before destructive cutovers. Use approved maintenance windows for blocking DDL. Preserve financial guards and synchronous durable commits throughout; no unlogged event tables or durability relaxation.

Rollback must preserve existing event IDs, stream versions, receipts, and timestamp originals. Reverting application binaries alone is unsafe after enabling cached/non-monotonic IDs if old readers still sort by EventVersion; retain corrected readers or restore ordering-compatible operation under a separately tested recovery procedure.

Market-closed functional/fault/performance work can begin now. Production activation requires target-host benchmarks and a controlled canary; representative market-hours validation remains necessary.

## 8. Scope boundary

This plan does not remove financial protections, command audit, durable idempotency, required projection recovery, or global event identity. It does not promise tens of thousands of transactions per second.

Recommended first implementation slice: Stage A plus Stage B's set-based projection markers. Stage C then enables safe ordering-independent tuning; the remaining schema/guard changes are separately gated.

Review output only: this document. No application code, configuration, schema, running service, or data was changed.

