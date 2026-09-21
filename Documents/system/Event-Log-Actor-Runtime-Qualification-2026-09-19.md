# Event-log command actor runtime qualification

Follow-up: the blocker documented below was fixed and verified by [93 passing tests and 36 fault checks](Event-Log-Actor-Payload-Conflict-Fix-2026-09-19.md). This report preserves the original failing experiment and its evidence.

Date: 2026-09-19

## Outcome: blocked by a correctness defect

The final strengthened suite reports **35 passed / 4 failed**, with another **30/30 storage regressions** and **16/16 audit/cache regressions** passing. Total unique final tests: **81 passed, 4 failed, zero skipped**. Do not promote the optimized writer yet.

All four new actor cases fail the same assertion: after recreating the actor runtime and database context, reusing a committed command ID with changed content returns Success=true instead of a payload-conflict failure. This reproduces with both original and batched projection-marker paths, with and without an omitted first-lifetime projection handoff. No additional event or target mutation is written by the conflicting request.

This is a shared existing binary-copy atomic append defect, not evidence of a batching-specific regression. No fix to that production behavior was made during this qualification. The regression test remains enabled and intentionally fails until it is corrected.

## Verified cause

1. EventSourceActorDbContext.SaveCommandEventsAtomicallyAsync computes a payload identity and consults CommandDuplicateCoordinator.
2. While the completed command is cached, changed content correctly raises CommandAuditPayloadConflictException.
3. A fresh context has no completed cache entry. CommandAuditPostgres.ReserveAsync detects a stored hash mismatch and returns PayloadConflict=true, Accepted=false.
4. BinaryCopyEventLogAppender.PersistBatchAsync examines only Accepted. Every nonaccepted reservation becomes CommandAuditDuplicateException; PayloadConflict is discarded.
5. An actor using the atomic-audit hooks treats that duplicate exception as a successful retry. The test actor exercises the real BaseEventSourceCommandActor duplicate-response path.

The optimization gate must require consistent duplicate semantics for warm cache, cold cache and after restart. An in-memory cache cannot replace durable identity checks.

### Required next fix (not implemented)

Preserve PayloadConflict as CommandAuditPayloadConflictException before handling ordinary nonaccepted reservations as CommandAuditDuplicateException. Verify mixed append windows still isolate an invalid request from valid requests, identical retries still succeed, and no audit/events/markers/checkpoints are partially written. Review legacy audit rows without hashes explicitly rather than silently treating them as verified identical payloads. Rerun the enabled actor cases, atomic/audit regression suites and affected fault/benchmark experiments before considering activation.

## What the test actually runs

- Real isolated NATS broker and MessagePack request/reply.
- NatsActorConsumer owned command payload path and dispatch stripes.
- ActorSupervisor, ActorMailbox, ActorThreadQueues, ActorThreadQueueV2 and ActorThreadPoolV2.
- Synthetic command actor derived from the real BaseEventSourceCommandActor, with a real CommandActorContext.
- Real EventSourceActorDbContext atomic command audit/events/markers transaction and original/batched BinaryCopyEventLogAppender.
- Real NatsJSDurableReplayQueue and BaseEventProjector startup, recovery and fenced execution against PostgreSQL.
- Synthetic PostgreSQL target using the existing atomic receipt/mutation implementation.

Each case submits commands for four streams concurrently, generating eight events per accepted command. Eight identical retries are then submitted concurrently on one stream. A changed-content retry must fail. A second lifetime uses entirely new supervisor, actor, context, cache, queue and projector instances against the same durable stores. It retries the original commands and then should append eight more events per stream. Stale-version requests must fail without leaving audit rows.

In the missing-handoff variant, the first lifetime deliberately omits queue publication after committing 32 events. The second projector startup must discover those 32 markers and recover them. This is an injected handoff omission followed by orderly disposal, not an OS crash. Normal cases discover zero unfinished startup markers. Queue settlement checks require zero pending and unacknowledged process/replay messages when reached.

The strengthened run fails at the changed-content assertion during the second lifetime, before its continuation/stale-write/settlement assertions. The preceding less-strict run passed all four cases, including second-lifetime continuation to 64 events per case and stale-version rejection. It did not include changed-content retries, concurrent retry bursts or broker settlement assertions. It is not a substitute for passing the strengthened suite.

## Test-only code changes and isolation

EventSourceActorDbContext now has an **internal** constructor that selects marker batching for qualification. Both baseline and candidate require a generated ifm_eventlog_bench_<12 lowercase hex>_<suffix> database on exactly 127.0.0.1:25432 and BinaryCopy mode. It never renames event_log. Public construction still has its original six parameters and uses the original writer; no application settings switch was added.

Five new safety tests verify rejection of application database names, remote hosts, the application PostgreSQL port and nonexact host aliases, plus the unchanged public constructor surface. The final suite also includes the existing 12 layout guards, 14 projector cases and four JetStream cases: 35 passing tests before the four blocked actor cases.

The test container resolves only a real command audit context and fresh real ActorThreadQueueV2 instances; unknown dependencies fail closed. The blackboard cache remains an in-memory substitute. This is not the full API host, a production business aggregate, a Redis restart test, downstream business publication consumption, UI or broker execution qualification. Production configuration and running IFM services were not changed. The internal constructor is a source change, not activation of batching.

## Durable evidence

Independent SQL after the final run completed:

| State | Count |
|---|---:|
| Events | 406 |
| Projection markers | 406 |
| Unfinished markers | 0 |
| Target receipts | 406 |
| Target mutations | 406 |
| Audited commands | 48 |

The four actor scenarios account for 128 events, 16 commands and 128 completed target effects (32 per case). Other pipeline cases account for the remainder. The conflicting command content was not applied; the defect is the incorrect success acknowledgment.

## Runs and artifacts

- [Final strengthened run: 35 passed, 4 failed](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-runtime-20260919/actor-final.trx)
- [Earlier less-strict actor run: 4 passed](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-runtime-20260919/actor-wired.trx)
- [Storage regressions: 30 passed](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-runtime-20260919/storage-regression.trx)
- [Audit/cache regressions: 16 passed](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-runtime-20260919/audit-regression.trx)
- [Initial fixture failure](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-runtime-20260919/actor-first.trx)
- [Diagnostic fixture failure](../../BenchmarkDotNet.Artifacts/event-log-v2/actor-runtime-20260919/actor-diagnostic.trx)

The first two runs failed before persistence because the provisional test container supplied a proxy rather than ActorThreadQueueV2. That fixture defect was corrected by a fail-closed concrete resolver; no production scheduler fix was needed. Release build after the test changes succeeded with zero warnings/errors.

Source SHA256 at final run:

- MarkerCommandActorRuntimeTests.cs: 599BCA268B9D6A2BA7205FCBF227D6DC1DF0809CFCF8A6D123E1BAFDAF21C96A
- EventSourceActorDbContext.cs: 17E4FB06951C681700BE8231DA761BA86BBE432A95F4E72E3F96F9F92D1BA7DA
- BinaryCopyEventLogAppender.cs: D0C0D4A5120960C5C8D633B56E985B0EEB93BBFF8183A9663D490B68FD013AAB

## Reproduction and cleanup

Use owned PostgreSQL on loopback 25432 and a disposable nats:2.12.0-alpine JetStream broker on 127.0.0.1:24223. Set IFM_MARKER_TEST_NATS_URL=nats://127.0.0.1:24223, DOTNET_ENVIRONMENT/ASPNETCORE_ENVIRONMENT=Test, and the test-only POSTGRES_TEST_KEY. Set IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION to a newly created guarded database with test credentials. Run the storage integration project in Release with filter FullyQualifiedName~Command_actor_routes_atomic for the blocker alone. For the combined suite use MarkerProjectorPipelineTests, MarkerActorBenchmarkGuardTests and EventLogBenchmarkLayoutTests joined with filter OR. A fresh database is required because synthetic target tables are created once.

Cleanup completed: the four databases with suffixes e1792bc8064a_actor, 6038bc19ad72_actor, 81ce6a9d3072_actor and 39a8401be6d2_actor under the ifm_eventlog_bench_ prefix were dropped. The labeled ifm-marker-actor-20260919 broker was stopped and removed, including its disposable stream data. The owned benchmark PostgreSQL container was stopped. Only synthetic data was discarded; it has no backup. TRX evidence remains on disk.
