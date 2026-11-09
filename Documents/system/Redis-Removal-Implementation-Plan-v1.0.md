# Redis removal implementation plan

Version: 1.0

Date: 2026-11-08

Status: runtime removal and qualification complete. Ignored generated build-folder cleanup was blocked by automated approval. See [the final report](Redis-Blackboard-Removal-Report-2026-11-08.md) for evidence and the existing indicator warm-up limitation.

## Confirmed implementation decisions

The obsolete Iron Condor MDI-limit query and its old .NET chart overlay are retired, as authorized. There is no compatibility endpoint or replacement MDI producer. The old Blackboard abstraction and Redis cache projects are removed together.

Stable event-stream IDs use a bounded typed cache owned by each event-source database context. PostgreSQL remains authoritative. Mutable reference lookups refresh from projected queries, use post-commit invalidation and retain the 30-second refresh bound. Projection recovery reads durable PostgreSQL markers directly; its fencing, outbox and duplicate protections remain in place.

Database replacement stops application cache owners before restoration and creates fresh owners afterward. An isolated integration test performs an actual PostgreSQL dump/restore and verifies the restored identity and projected row. Development Redis was backed up and stopped with automatic restart disabled; its volume is preserved. Redis is used only by the isolated historical comparison benchmark.

Replace remaining Redis dependencies with typed process-local caches and authoritative PostgreSQL access. Preserve existing local/native market-data caches, event sourcing, durable projection recovery and database restoration. Completion means the supported application stack can start, operate, restart and recover without Redis installed or reachable.

## Current baseline

The Development audit found 94,389 Redis keys using approximately 37.5 MiB: 70,031 EventProjectorState, 24,205 EventStreamId, 88 FuturesRsiSignal, 42 VixFuturesEodData, 22 FuturesEodData and one ReferenceLookup. No keys had an expiry at inspection. Counts include possible historical leftovers and are not proof of current reader activity.

Current EOD/VX, RSI, Outlook, Regime Discovery and option-pricing readers already use local/native caches or projected PostgreSQL data. Redis remains wired into stream resolution, reference refresh, legacy queries, projection recovery, host composition, recovery probes and database replacement.

The existing Redis connection benchmark measures connection readiness. It is not a before/after benchmark for this removal and must not be presented as one.

## Target architecture and invariants

- PostgreSQL owns event streams, stream identities, projected read models, projection execution claims, sequence allocation and recovery records. NATS retains its existing delivery responsibilities.
- Typed caches contain disposable derived data only. Queries read projected models or published read caches, never Command or Function state directly.
- Keep the current actor ordering, expected-version checks, latest qualifying snapshot plus suffix loading, post-commit projections and durable duplicate handling.
- Stream identity cache keys include database identity or generation and the complete canonical stream identity. Cache only confirmed positive IDs. Database replacement must invalidate every affected cache before traffic resumes.
- Local caches have explicit capacity, expiry where appropriate, ownership and metrics. Use immutable values or safe copies. Do not replace Redis with an unbounded dictionary.
- Concurrent misses for the same key share one asynchronous load; unrelated keys remain independent. Bound PostgreSQL load concurrency. A cancelled caller must not cancel another caller's shared work; shutdown must cancel outstanding work.
- Publish entries only after a successful load or committed write. Failures, missing rows and ambiguous commits must not become permanent cached answers. Preserve cancellation and bounded database deadlines.
- Prevent stale in-flight refreshes from republishing data after invalidation by checking a generation or revision before publication.
- Mutable reference data retains bounded refresh and post-commit invalidation. Where more than one process consumes it, use existing committed notifications plus periodic refresh so a missed notification cannot leave it stale indefinitely. Stable stream IDs need no distributed invalidation during ordinary operation.
- Redis removal does not change quote freshness, indicative pricing, off-hours readiness, indicator seeding or independent strategy timeframes. Hazelcast-backed IDistributedCache is a separate dependency and is outside this removal unless a directly affected unused registration requires adjustment.

## Implementation sequence

| Phase | Work | Exit condition |
|---|---|---|
| 0 | Freeze inventory, fixtures and current measurements. | Every Redis/blackboard consumer, project reference, package, configuration key, script and deployment dependency is classified. |
| 1 | Introduce typed stream identity caching backed by PostgreSQL. | Warm lookups avoid network access; cold and competing processes resolve the same durable identity. |
| 2 | Replace reference and remaining legacy reads. | All supported queries work from local typed read caches and projected PostgreSQL data. |
| 3 | Remove redundant market-data and projector mirrors. | Durable writes, notifications and fenced recovery work without Redis. |
| 4 | Remove Redis from host composition, recovery and restore tooling. | API, Rust Host and database restoration have no Redis prerequisite. |
| 5 | Remove obsolete projects, fixtures and deployment wiring. | Supported builds and test suites require no Redis service or package for removed functionality. |
| 6 | Measure, deploy and verify Development. | Full correctness and operational acceptance evidence, comparative measurements and final report are complete. |

### Phase 0 inventory and baseline

Record the Git revision, working-tree diff, SDK/runtime, hardware, configuration, database fixture hashes, Redis key summary and process topology. Trace all 31 blackboard cache model files and specialized mapping caches, including old .NET hosts, utilities, backup hosts, isolated test stacks and deployment definitions. Search for direct Redis clients outside IRedisCache as well as IBlackboardService.

Classify each member as active read/write, write-only, unused, or supported legacy functionality. Confirm any external consumers before deleting a write-only contract. Preserve Redis data during implementation; do not flush a shared database or repurpose live business records for tests.

Create deterministic baseline fixtures for stream resolution, command auditing, snapshot loads, reference refresh, projection recovery and supported legacy queries. Run equivalent workloads against the unchanged implementation before edits. Keep benchmark reference code isolated in the benchmark project so the final application has no Redis fallback.

### Phase 1 stream identity cache

Replace EventStreamIdCacheModel access in EventSourceActorDbContext and its extensions with a typed cache abstraction owned by storage. Cache numeric IDs without JSON. Reuse existing PostgreSQL resolution and uniqueness/upsert semantics; cache eviction must never allocate a second stream.

Cover explicit stream resolution and command-log insertion as well as snapshot paths. Preserve the single-query snapshot approach: do not add an identity lookup to loaders that already resolve the stream inside SQL. Existing event-name/type/alias caches stay local and retain their registration invalidation rules.

Expose hit, miss, eviction, in-flight load and database failure counters. Reset the cache on database replacement and verify recreated database identities cannot reuse stale IDs. PostgreSQL unavailability must produce a bounded, explicit failure on a miss; it must never fabricate an ID.

### Phase 2 reference and legacy reads

Update ReferenceLookupActorService to build its typed local index from the projected reference query. Preserve its existing 30-second refresh bound initially, retain generation invalidation after LookupType projection commits and test concurrent refresh/invalidation ordering. Fetch related reference collections in batches rather than introducing one query per row.

Replace the legacy RiskFreeRateCacheModel consumer with the owned projected Treasury/rate source and typed cache where needed. Preserve effective-date identity, rate units, provenance and existing business freshness rules; wall-clock cache age is not a substitute for rate validity.

Retire GetIronCondorMDILimit and its obsolete .NET chart overlay, as authorized after confirming that its producer had been removed and the current Rust UI does not use it. Remove the endpoint, contract, routing and view-model dependencies together. No replacement limit is invented.

Retire unused streaming-request and Databento mapping adapters only after confirming registrations and supported callers. Preserve working native mappings and request-ID ownership conventions.

### Phase 3 redundant writes and projector recovery

Remove Redis EOD/VX/RSI and VixFuturesContractId mirrors where the completed inventory confirms no supported reader. Preserve PostgreSQL projections, CurrentFuturesEodCache, CurrentVixEodCache, indicator caches and committed notifications.

Remove the legacy EventProjectorState mirror from EventProjectorRecoveryCoordinator. Keep PostgreSQL claims, leases, revisions, predecessor validation, terminal states, retry schedules and outboxes authoritative. Retire the legacy Redis execution branch, or migrate any supported non-fenced deployment before removing it; a default configuration change alone is insufficient.

Prove crash recovery around claim, persistence, publication and acknowledgement boundaries. Recovery must neither lose pending work nor reapply committed business effects.

### Phase 4 hosts and operational tooling

Remove RedisConnectionStartup, StartupRedisCache and Redis DI registrations from the API and all supported managed market-data host compositions, including the Rust Host. Remove RedisRecoveryInfrastructureProbe and its policy requirements; retain probes of the actual PostgreSQL/NATS/native dependencies.

Change DevelopmentDatabaseReplacementPlatform so application recovery no longer executes redis-cli. Replace Redis prefix invalidation with the verified process-stop/cache-generation/restart lifecycle for every affected cache consumer. Test rollback restoration as well as successful replacement; stale stream mappings must not survive either path.

Remove Redis-specific settings, required environment variables, secrets references, compose dependencies, health checks, startup scripts and obsolete tests. Preserve unrelated infrastructure and production data. Verify scheduler, backup and recovery utilities as well as the API and UI.

### Phase 5 dependency cleanup

Delete BlackboardService and its Redis-backed models only once all supported callers have replacements. Remove Framework.Caching.Redis, Redis-specific package/project references, obsolete Redis fixtures and solution entries when no supported consumer remains. Keep useful general local-cache contracts and metrics separate from removed Redis abstractions.

Update architecture, startup, recovery and deployment documentation. A repository search must find no production Redis client, endpoint requirement or redis-cli invocation; historical reports and explicitly isolated baseline benchmark code are permitted and documented exceptions.

## Required tests

Use the existing domain UnitTests, BDDTests and IntegrationTests projects. Storage tests belong in Application.Storage.IntegrationTests; host/recovery scenarios belong in LivePipeline.IntegrationTests and Application.MarketData.Rust.Host.Tests, with focused cache unit tests in the owning test project. Trade uses its existing IntegratedTests project naming. Keep benchmark-only Redis dependencies isolated from production and normal verification fixtures.

| Area | Unit tests | BDD and integration tests |
|---|---|---|
| Stream identities | Typed key equality, hit/miss, capacity eviction, single shared load, cancellation isolation, failed-load retry, generation reset and stale completion rejection. | Empty cache resolves through real PostgreSQL; concurrent independent processes converge on one stream; cold restart and database replacement never reuse stale IDs. Command audit and latest snapshot plus suffix remain identical. |
| Reference data | Immutable index, refresh expiry, post-commit invalidation, failed refresh policy and racing refresh completion. | Given a committed lookup update, same-process and second-process readers see projected values within the supported refresh bound; restart rebuilds with Redis absent. Queries cannot read command state. |
| Legacy reads | Rate date/units/freshness; MDI retirement and mapping compatibility. | Treasury reads preserve projected values and validity. The obsolete MDI query and overlay have no remaining supported caller or registration. |
| Market-data mirrors | Removed Redis writes do not remove local publication or notifications. | EOD/VX and RSI writes remain queryable after restart; indicator initialization, Outlook, all strategy timeframes and off-hours Options readiness remain correct. |
| Projection recovery | No Redis mirror dependency; claim/revision/lease and terminal-state behavior remain intact. | Crash/restart, duplicate delivery, missing predecessors, retry exhaustion and publication failure recover through PostgreSQL/NATS with no duplicate projection effects. Exercise every supported execution mode. |
| Cache load pressure | Same-key miss sharing, bounded distinct-key loads, bounded memory and cancellation/disposal. | PostgreSQL outage and recovery remain bounded and observable; concurrent cold API/Host startup does not create uncontrolled database load. |
| Host composition | Production service provider builds without Redis; no hidden Redis service resolution. | API plus Rust Host plus scheduler plus eGui start with Redis absent; health reports actual dependencies and no Redis errors. |
| Database restore | Cache-generation and process-lifecycle ordering, failed replacement rollback. | Restore a disposable database with different stream IDs, resume traffic and prove all consumers use restored identities and projected values; no Redis command is executed. |

BDD scenarios must express business outcomes using real owning actors/query paths where practical, rather than only checking that a Redis mock was not called. Integration fixtures use isolated databases, subjects and operation identities; no synthetic verification records remain in Development.

Run the full affected Storage, Reference, Feed, Analytics, Trade, recovery and Rust Host suites after focused tests pass. Run actor/query convention and latest-snapshot audits. Report failures and skips explicitly; environment skips cannot count as successful PostgreSQL qualification.

## Before and after measurements

Use BenchmarkDotNet in Application.Api.Server.Benchmarks and the existing storage benchmark project as appropriate. Enable MemoryDiagnoser and ThreadingDiagnoser. Capture contention and GC pause evidence with EventPipe/dotnet-trace for representative end-to-end workloads; BenchmarkDotNet allocation/GC counts alone do not describe process-wide pauses or all lock wait time.

| Workload | Before | After | Required comparison |
|---|---|---|---|
| Warm stream resolution | Current Redis read-through path. | Typed local hit. | Latency, throughput, bytes allocated, Redis/SQL requests. |
| Cold stream resolution | Empty Redis key and normal PostgreSQL resolution. | Empty local key and same PostgreSQL resolution. | Full caller latency, SQL count, uniqueness under contention. |
| Latest snapshot and command audit | Current complete path, including stream resolution. | New complete path, including stream resolution. | State equivalence, rows/bytes read, SQL count and latency. |
| Reference lookup | Warm local hit; separately local-expired/Redis-hit and full cold load. | Warm local hit; separately projected-query refresh and full cold load. | Do not claim the existing local hit becomes faster by removing Redis. |
| Market-data write pipeline | Durable projection/local publication plus Redis mirror. | Same work without mirror. | Per-event latency, throughput, CPU, allocations and resulting read-model equivalence. |
| Recovery batch | Current fenced recovery including legacy mirror writes. | PostgreSQL recovery without mirrors. | Fixed pending batch completion time, DB load, duplicates and failures. |
| Startup and steady state | Current API/Host stack with Redis ready, plus separate delayed/unavailable Redis case. | Redis-free API/Host stack. | Time to actual readiness, per-process CPU/RSS/managed heap, thread-pool queueing and GC. |
| Concurrent misses | Redis baseline at representative concurrency. | Local cache with bounded PostgreSQL loads. | p50/p95/p99, SQL rate, lock contention, retained memory and error rate. |

Use identical deterministic inputs, hardware, PostgreSQL fixture and runtime settings for paired comparisons. Include representative concurrency levels of 1, 8 and 32 plus actual observed load, unique-key churn and capacity overflow. Control warm/cold state explicitly. Keep service-dependent timings separate from pure in-memory microbenchmarks.

Use BenchmarkDotNet optimized builds for both sides, keeping full debugging symbols; restore the normal Development profile afterward. Record run parameters, sample counts, confidence/error, Gen0/1/2 collections, bytes/op, contention and memory. Collect end-to-end percentiles with an instrumented load harness rather than inferring them from BenchmarkDotNet mean/error. Repeat noisy comparisons and disclose changes in PostgreSQL load.

Write raw results and a final comparative report under the established benchmark Results directories and Documents/system. Store bulky traces and generated artifacts on E: through the current artifact layout. Do not substitute estimated speedups for measurements.

## Development cutover and acceptance

1. Pass focused tests, then full affected suites and comparative measurements against disposable infrastructure.
2. Preserve a recoverable Redis data/configuration backup and the previous application build while qualifying the cutover. No database/event deletion is required for removal.
3. Deploy API and Rust Host together as needed; restart every affected local-cache consumer through the existing VS Code Development profile. Verify with Redis unreachable to those processes, without stopping an unrelated/shared Redis instance.
4. Verify API readiness, scheduler operation, Rust eGui connection, ES/VX Outlook, seeded indicators and independent Daily/Weekly/Monthly strategy processing with available valid inputs. Quiet option feeds must not delay readiness. Do not submit broker orders for this infrastructure verification.
5. Run a minimum 30-minute representative soak including reference refresh and projection recovery. Check bounded cache memory, database load, error logs and absence of Redis connection attempts. Verify restart reconstruction and disposable database restoration separately.
6. Remove the IFM-owned Redis service/configuration after all supported dependencies and acceptance checks are resolved. Delete retained Redis recovery data only under the agreed retention policy.
7. Publish all before/after timings, allocations, GC, contention, memory and SQL comparisons, plus suite totals and any unresolved blocker. Restore and leave Development running.

Acceptance requires equivalent business results, working latest-snapshot reconstruction and durable recovery, bounded cache growth, no duplicate streams/effects, and no supported Redis dependency. A reproducible latency, database-load or memory regression must be investigated before cutover; simplification alone does not justify hiding it.

## Blockers and rollback

Potential blockers are an unidentified external Redis consumer, a Redis-only value with no authoritative producer/history, a supported non-fenced recovery mode without a PostgreSQL replacement, or inability to qualify database restoration. Continue independent work while isolating any blocked path; report the exact missing contract or evidence.

Rollback restores the previous compatible application/configuration and reconstructs disposable Redis caches from authoritative sources. Do not reuse stale identity entries after a database replacement. Preserve PostgreSQL events and projections throughout; no financial or broker state is migrated to memory.
