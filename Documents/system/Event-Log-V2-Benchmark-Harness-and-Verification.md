# Isolated event-log v2 benchmark harness

Date: 2026-09-19
Status: Benchmark implementation; application migration is not enabled.

## Schema comparison with batching held constant

[Isolated API acceptance host](Event-Log-V2-Isolated-Acceptance-Host-2026-09-20.md): explicit guarded candidate entry point and disposable-store launcher implemented; 13 guard tests and 3 normal composition checks passed. Full running-host/UI acceptance is blocked by shared Docker AIO capacity, not marked complete.

[Full-host preflight](Event-Log-V2-Full-Host-Preflight-2026-09-20.md): all three non-starting API composition tests passed. Actual API/UI acceptance still requires an isolated host/profile and qualification-only candidate registration; normal Development configuration is not isolated.

Follow-up: [integration gate fixes](Event-Log-V2-Qualification-Gate-Fixes-2026-09-20.md) passed 260 final suite executions after adding broker-confirmed consumer readiness and correcting financial audit-conflict classification. Full isolated API/UI host acceptance remains outstanding; production activation is still disabled.

Combined campaign status: [2026-09-20 qualification report](Event-Log-V2-Combined-Qualification-2026-09-20.md). The 262,144-event migration rehearsal passed 22 checks, but actor startup and financial error-contract failures block integration/acceptance. Do not promote based on earlier isolated passes.

The benchmark-only --event-log-index-migration-qualification command rehearses a name-preserving forward/reverse index migration. [Migration results](Event-Log-V2-Index-Migration-2026-09-19.md): 22/22 checks passed, including full schema reapplication, lock timeout and SQL-error rollback. Preserving the existing index name resolves the redundant-index issue in this candidate without changing production startup SQL.

Fault qualification also supports --schema-comparison, optionally with --process-restart. See [schema recovery results and migration caveat](Event-Log-V2-Schema-Recovery-2026-09-19.md): 36 fault cases, 30 process/restart cases and 44 scoped projector cases passed. Reapplying the existing V3 index statement can recreate a redundant fourth index, so production promotion still requires migration work.

For longer same-table schema comparisons, combine --schema-batched-experiment with --retained-experiment. This retains the 8,192-capacity queue and uses timed windows with configurable seed history. See [retained schema results](Event-Log-V2-Retained-Schema-2026-09-19.md): eight measured samples passed; median paired throughput gain 4.9%, with production unchanged.

The additional `--schema-batched-experiment` mode compares `V2BatchedMarkers` (four indexes) with `V2BatchedStreamPrimaryKey` (three indexes). Both use event_log_v2 and marker batching. It tests only primary-key/index consolidation across no-marker, one-in-eight-marker and all-marker workloads. It verifies exact primary-key definitions and saves each fixture's index DDL. See [schema step 1 results](Event-Log-V2-Schema-Step-1-2026-09-19.md): 36/36 measured samples passed, with modest incremental gains; production migration remains disabled.

## Scope and variants

The executable compares the actual IFM persistence appenders against fresh, separate databases on a dedicated local PostgreSQL server:

| Variant | Event table | Index layout |
|---|---|---|
| Baseline | event_log | Current four indexes |
| V2Control | event_log_v2 | Identical four indexes |
| V2StreamPrimaryKey | event_log_v2 | Stream/version primary key, unique global event ID, command-ID B-tree |

V2Control is an A/A control: renaming a table is not expected to create a meaningful speed improvement. Its variation estimates experimental noise. V2StreamPrimaryKey removes only the redundant old primary-key index; it does not remove global identity, command audit, timestamp compatibility, or financial fencing.

Production constructors still use the current event_log SQL. An internal, non-configurable benchmark layout supplies alternate table SQL only for loopback databases with uniquely generated benchmark names. The sequence name is unchanged inside each separate database. No application settings or startup migration enables v2.

Candidate creation uses the production event-source schema and full financial schema, then renames only the candidate fixture table. Existing foreign keys follow the rename. The harness checks enabled financial fencing, expected index count, and at least five incoming event-identity foreign keys.

## Workloads

The standard run has six scenarios:

1. Regular sequential writer: 64 independent streams, one event per command.
2. Regular binary COPY writer: 64 independent streams, one event per command.
3. Atomic audited COPY writer: 64 independent streams, one event per command.
4. Atomic audited COPY writer: one stream, dependent one-event commands.
5. Atomic audited COPY writer: four financial streams, 64 events and required projection markers per command.
6. Atomic audited COPY writer: four streams, 256 events per command without required markers.

Regular writer cases deliberately have no command-audit envelope: they characterize that existing path, not a substitute for atomic audited persistence. Only audited scenarios represent the combined command/event transaction.

Each stream advances its in-memory expected version only after durable acknowledgment. Event construction, command serialization/hash creation (audited paths), event serialization, queueing, locking, append, and commit acknowledgment are timed. Domain actor execution, NATS, UI, financial journal posting, and actor duplicate-cache lookup are not included. This is not evidence that database concurrency checks can be removed.

All variants use LZ4, the same deterministic 1,024-character synthetic payload, and current default queue/batch limits. Commands/events have distinct identities. Seed writes use the same real durable path as measured writes. Default sizes are a screening workload, not a production-sized event-store qualification.

## Safety

- Requires an explicitly supplied administration connection to an empty dedicated server.
- Accepts loopback hosts only; refuses port 5432 and any administration database other than postgres.
- Refuses a server with existing non-template databases other than postgres.
- Creates database names with a run-specific random token, never takes a user-selected target table/database name.
- Requires fsync, synchronous_commit, and full_page_writes to be on.
- Does not reuse, truncate, or modify an existing application database.
- Removes successful generated fixtures; failed fixtures remain for diagnosis.
- Uses FORCE only when dropping that exact successfully completed, generated fixture, to close its own pooled connections.
- Refuses a nonempty results directory instead of overwriting prior evidence.
- Never logs connection strings or credentials.

Use a dedicated temporary container. Do not point this at the IFM application's PostgreSQL instance even if a spare test database exists there.

## Running

Build from the repository root:

~~~powershell
dotnet build TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release
~~~

Provide process-scoped environment variables:

- DOTNET_ENVIRONMENT=Test
- ASPNETCORE_ENVIRONMENT=Test
- POSTGRES_TEST_KEY: the normal IFM credential JSON with userid/password for the dedicated benchmark server.
- IFM_EVENTLOG_BENCH_ADMIN_CONNECTION: its administration connection, including credentials, database postgres, and non-5432 loopback port.

The IFM schema/appender connections use POSTGRES_TEST_KEY; administrative connections use the supplied admin connection. Both must refer to the same dedicated server credentials. Do not commit credentials.

Smoke pilot:

~~~powershell
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-v2-benchmark --quick --output=BenchmarkDotNet.Artifacts/event-log-v2/new-pilot
~~~

Balanced repeated screening run:

~~~powershell
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-v2-benchmark --repeats=6 --rounds=32 --seed-rounds=8 --output=BenchmarkDotNet.Artifacts/event-log-v2/new-paired-run
~~~

Multiples of three repetitions balance each variant's position in the rotated run order. Increase rounds and seed rounds for longer samples and a larger dataset; budget runtime and disk accordingly. Avoid builds, test suites, or other intentional competing workloads while timed runs are active.

After a failed run, inspect its named fixture in the dedicated container before removing it. A subsequent run deliberately refuses to reuse a server containing that fixture. The harness never automatically removes failed data.

## Evidence and correctness gates

Every sample verifies:

- Expected event, audit, and marker counts.
- Contiguous per-stream versions and matching durable counters.
- Complete payload deserialization and in-memory state reconstruction in stream order.
- Rejection of stale expected versions with no durable side effects.
- Audited duplicate and changed-payload rejection with no second append.
- Durable deduplication from a second appender with no shared appender-local state.
- Rejection of a fenced financial stream and rollback of audit/events/markers/counters.

The current atomic COPY writer reports changed-payload conflicts as CommandAuditDuplicateException rather than the more specific payload-conflict exception. The harness records this existing diagnostic limitation and verifies rejection/unchanged durable state; it does not alter the writer's behavior.

These checks are smoke qualification, not exhaustive crash correctness. A second appender is not a second process or a simulated machine crash.

Twelve focused tests verify production SQL remains unchanged, unsafe layout creation or reuse is rejected, table substitution does not rename sequences or index identifiers, and an empty marker set performs no database access.

## Output

Each run writes:

- metadata.json: environment, durability, options, deterministic payload hash, and source hashes.
- samples.json: every raw successful sample, run order, and exact generated fixture identity.
- summary.md: per-scenario medians and paired throughput differences.

Measurements include commands/sec, events/sec, p50/p95/p99/max acknowledgment latency, process-wide allocated bytes and GC counts, client CPU, server WAL delta, event-table total bytes, appender commit count, maximum reported queue depth, and warm full replay duration.

WAL includes background server activity. Allocation includes asynchronous client work during the interval. Queue depth is the existing appender metric, not a full queue-age/byte sampler. Tail quantiles with few observations are descriptive only. Closed-loop clients do not measure open-loop overload response or eliminate coordinated omission.

## Promotion gate and remaining work

Do not migrate because one median is faster. Require repeatable paired gains beyond the identical-control variation, acceptable tail latency, and no recovery/correctness regression. An inconclusive result is a reason to retain the current production layout.

Before production promotion, add target-host and production-scale qualification, event-type/snapshot query benchmarks, concurrent replay, open-loop overload/backpressure, detailed phase timing, server CPU/wait/disk sampling, crash/commit-ack-loss injection, multiple writer processes, and financial cutover races. Current warm full replay does not measure actor startup or snapshot-plus-tail recovery.

The original index-only experiment does not enable marker batching. The extension below evaluates it separately; production behavior remains unchanged.

## Marker-batching experiment extension (2026-09-19)

The next experiment is now available with --marker-experiment. Without this flag the original index comparison is unchanged.
The marker experiment selects Baseline, V2Control, and V2BatchedMarkers. All three retain four indexes, text timestamps,
global event identities, command audit, stream locking, and the original financial fence. Only the last variant batches
required marker INSERT statements; this remains an internal benchmark-only choice, not an application configuration option.

~~~powershell
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-v2-benchmark --marker-experiment --repeats=6 --rounds=32 --seed-rounds=8 --output=BenchmarkDotNet.Artifacts/event-log-v2/new-marker-run
~~~

Five matched workloads cover one-event commands with and without markers (64 streams), single-stream marker writes,
and 64-event commands with and without markers (four streams). All use financial-stream names so the fence cost is matched.
The quick mode runs three smaller workloads for smoke verification.

New sample fields record successful marker statement count, affected marker rows, and cumulative marker operation duration
(client preparation plus database await, not server CPU time). Statement-level metrics are not commit counters: a later
transaction rollback can invalidate their rows; only successful benchmark samples contribute to the report.

Every sample additionally verifies checkpoint-behind/equal/ahead behavior, missing checkpoints, both initial stages,
mixed material/nonmaterial events, missing event-name joins, missing event IDs, duplicate markers/inputs, and empty sets.
The missing-join test exercises full event/audit/counter rollback through the actual appender.

The candidate assigns one creation/update timestamp per marker statement and reads checkpoints from one statement snapshot.
Per-stream ordering still uses stream versions, not marker timestamps. Concurrent checkpoint progression and operational
failure injection remain explicit promotion gates; single-statement semantics must not be mistaken for an exhaustive
concurrency proof.

## Marker fault/concurrency qualification (2026-09-19)

The benchmark-only --event-log-marker-qualification entry point runs six fault/concurrency scenarios three times
against both the original and batched marker writers. It uses fresh isolated databases and the unchanged four-index
schema. It tests competing versions, duplicate commands, checkpoint advancement, precommit backend termination,
cancellation after admission, and a real lost COMMIT acknowledgment through a loopback protocol proxy.

The final suite passed 36/36 cases, including payload replay and durable retry checks. See the
[qualification report](Event-Log-Marker-Fault-Qualification-2026-09-19.md) for results, reproduction, and limits.
Independent-process and full-server crash tests remain outstanding; this does not activate the candidate in production.

## Mixed marker-density experiment (2026-09-19)

Use --mixed-marker-experiment to compare Baseline, V2Control, and V2BatchedMarkers with the unchanged
four-index schema and financial fence. All commands are audited. Five cases use four streams and 64 events
per command: no markers, every 64th event (1.5625%), every eighth (12.5%), every second (50%), and every
event (100%). A sixth case uses 64 streams and one event per command, requiring a marker every eighth
stream version. That last case models synchronized bursts, not a random production arrival distribution.

~~~powershell
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-v2-benchmark --mixed-marker-experiment --repeats=6 --rounds=128 --seed-rounds=32 --output=BenchmarkDotNet.Artifacts/event-log-v2/new-mixed-run
~~~

Use the dedicated empty loopback benchmark server and environment configuration described above.
The command produces 108 samples with balanced rotated order, 128 measured commands per stream and
32 seed commands per stream. This is four times the measured per-stream rounds of the earlier marker
experiment, not a time-based soak or open-loop saturation test. All three variants use matched event counts.

Marker selection is deterministic by stream version modulo the scenario interval. Durable verification
checks the exact event-to-marker placement, identities, persisted projection flags, payload replay, command
counts and stream versions. Measured marker telemetry excludes seeded events and supports transactions
containing no markers. Existing stale-version, duplicate/restart, financial-fence and checkpoint/marker
failure checks still run after each sample. Successful fixtures are removed; failed fixtures are retained.

The completed 108-sample run and its limitations are documented in the
[mixed-density results](Event-Log-Mixed-Marker-Experiment-2026-09-19.md).

## Independent-process and PostgreSQL restart qualification (2026-09-19)

The --event-log-marker-qualification --process-restart mode tests two independent writer processes,
abrupt owned writer termination, graceful database restart, and SIGKILL recovery of the whole dedicated
PostgreSQL container. It validates container identity, loopback mapping, exclusive persistent volume,
and database ownership before server faults. Use Pooling=false in the benchmark admin connection.

The completed run passed 30/30 cases; the original connection/acknowledgment suite passed 36/36 again.
See the [process/restart report](Event-Log-Process-and-Restart-Qualification-2026-09-19.md) for reproduction,
server-log evidence, the corrected initial harness failure, safety guards, and remaining soak/rollout gates.

## Bounded queue-pressure / timed soak experiment (2026-09-19)

The --pressure-experiment mode uses 64 bounded producers, one outstanding audited command per stream,
eight events per command and one required marker per eight events. It compares Baseline and V2BatchedMarkers
with four indexes and the financial fence retained. The queue capacity is deliberately reduced to eight for
this experiment only; this is not an application queue-setting recommendation.

~~~powershell
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-v2-benchmark --pressure-experiment --soak-seconds=60 --repeats=4 --seed-rounds=8 --output=BenchmarkDotNet.Artifacts/event-log-v2/new-pressure-run
~~~

Use the dedicated empty loopback PostgreSQL server and Test credential setup described above. This mode
also validates the labelled exclusive-volume benchmark container for read-only resource observation.
Four paired repetitions alternate run order. Each writer receives the same one-minute demand window,
not the same completed command count. Producers stop issuing commands at the deadline and outstanding
appends finish before verification. The 4,096-command-per-stream safety cap fails a sample if reached early;
it does not silently shorten the configured duration. A 10-second, two-repeat run is suitable for smoke testing.

Before each measured window, a stream-row lock holds the writer while all 64 producers submit. Once the
lock wait is observed, the harness proves blocked admission from the pending metric minus queue capacity
and one possible consumer carry item. It then releases the lock and requires all writes to complete. This
barrier is untimed and separate from the load window. A zero queue/admission counter is required after drain.

The existing queue metric includes channel contents and producers waiting for admission, but excludes
the transaction batch after it is dequeued. It is **not** channel occupancy. Observation JSON files record
that metric, commits, client managed/private/working-set memory, and active PostgreSQL wait snapshots.
Periodic docker stats captures container CPU, memory and cumulative block-I/O accounting. The observer
targets roughly one-second snapshots; docker stats can delay a snapshot and is sampled less frequently.
All observations carry elapsed times. Observer overhead is included for both variants. Container block-I/O
accounting is not disk latency or event-log-only I/O; wait snapshots are not time-weighted wait statistics.

Full durable replay/count/marker-placement checks and the existing rejection/rollback gates run afterward.
This short bounded soak does not constitute hours-long leak testing, production arrival-rate validation,
default-capacity testing, or end-to-end actor/projector qualification.

The completed eight-sample main run, resource observations and reporting clarification are documented in
the [queue-pressure/short-soak results](Event-Log-Queue-Pressure-Soak-2026-09-19.md).

## Storage-to-projector pipeline qualification (2026-09-19)

MarkerProjectorPipelineTests connects both marker writers to real PostgreSQL projector state, the production
BaseEventProjector engine, recovery coordinator and outbox dispatcher, with a synthetic receipt-backed target.
The final suite passed 14/14 cases; storage and focused projector regressions passed 30/30 and 26/26.
Queue delivery and outgoing transport are substituted: this is not full actor/NATS qualification.
See the [pipeline report](Event-Log-Projector-Pipeline-Qualification-2026-09-19.md) for evidence, reproduction,
exact boundaries and the remaining real-host writer-selection test seam.
# Real JetStream qualification follow-up (2026-09-19)

See [JetStream qualification](Event-Log-JetStream-Qualification-2026-09-19.md): four new paired broker cases, fourteen pipeline regressions and six durable queue regressions passed. Real queue recreation, duplicate suppression, post-commit replay and server-side acknowledgment were verified. Full command-actor host qualification remains open; production is unchanged.
# Actor runtime qualification blocker (2026-09-19)

See [actor runtime qualification](Event-Log-Actor-Runtime-Qualification-2026-09-19.md). The strengthened suite has four failing actor cases: cold-cache changed-payload retries are incorrectly acknowledged as identical duplicates by the shared binary-copy atomic append path. 81 other final tests passed. Do not promote the candidate until this defect is fixed and the enabled tests pass. Public production writer selection is unchanged.
# Actor payload-conflict blocker resolved (2026-09-19)

See [fix and verification](Event-Log-Actor-Payload-Conflict-Fix-2026-09-19.md): 93 tests plus 36 fault checks passed. Changed-content retries now fail correctly after restart; audit-rejected mixed batches isolate unrelated valid commands. Public writer selection remains unchanged and marker batching is not activated. Earlier blocker sections below retain historical results.
# Post-fix performance requalification (2026-09-19)

The [matched mixed-density rerun](Event-Log-Post-Fix-Performance-2026-09-19.md) passed 108/108 samples. Candidate/control paired median speedups range from 1.154x for sparse markers to 11.238x for all-marker events; no-marker throughput is inconclusive. Healthy-path candidate throughput remains close to the prior run. No production activation or schema migration occurred.
# Retained-history timed load (2026-09-19)

The [retained-history experiment](Event-Log-Retained-History-Load-2026-09-19.md) passed four two-minute main samples plus eight smoke/regression samples. Each main fixture retained 262,144 seed events with an 8192-capacity queue. Paired throughput improved 2.499x and allocation/command fell 12.2%; Gen 2 collections did not decrease. New benchmark-only flag: --retained-experiment. Production marker batching remains disabled.
