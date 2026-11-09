# Redis and Blackboard removal

Date: 2026-11-08 (qualification continued overnight)

Status: runtime removal and qualification complete. Generated build-folder cleanup blocked by automated approval.

## Delivered behavior

The application no longer registers or requires Redis or Blackboard. The obsolete Iron Condor MDI-limit query, public client endpoint and old .NET chart overlay are retired. Their producer had already been removed; no new MDI calculation or compatibility projection was introduced.

Event-stream identities use a typed cache owned by each PostgreSQL event-source context. It retains at most 32,768 positive identities, coalesces concurrent loads for a key and permits at most 16 concurrent database loads. Failures remain retryable. Caller cancellation does not cancel another caller's shared load. Invalidation prevents an older load from republishing after a generation change; shutdown cancels database work.

Reference lookups use the existing immutable local index, projected actor queries, post-commit invalidation and a 30-second refresh bound. Refresh failures do not become cached empty results. PostgreSQL remains authoritative for stream identities, reference projections, projection markers, fencing, outboxes and recovery.

Redundant Redis copies of EOD/VX, RSI, contract lists and projector state are removed. Existing local/native publications and durable projections remain. The legacy risk-free-rate calculation reads the projected Treasury curve once per trade and shares it across its legs.

Database replacement stops cache owners before restoring PostgreSQL and starts fresh owners afterward. Tests cover competing owners and an actual PostgreSQL dump/restore with changed stream identities and projected data. Latest snapshot selection and the single-query snapshot path retain their existing behavior.

## Verification evidence

Evidence root: `E:\IFM\RedisRemoval\20261108`.

The production dependency audit scans 8,989 files plus the supported API, Rust Host and database cutover manifests: no violations. Command, Query, Function, Event and Realtime convention audits pass.

The .NET unit/BDD matrix passed 7,366 tests, with five existing platform/live/load skips. Storage integration passed 882 tests, Reference 32, Feed 74, Analytics 48, MarketData 43, Securities 16, Trade 162, Portfolio 223, BrokerAccount six and SystemAdmin six. The recovery audit passed eight tests against real isolated PostgreSQL/NATS/native publication. Feed's isolated broker-outage/generation cases executed; its five remaining skips are four retired direct-insertion cases and the live Databento qualification.

Additional checks passed: one actual PostgreSQL dump/restore test, two repository transaction tests, one actor-host composition test and eight legacy .NET UI startup/configuration tests. Rust eGui passed 393 tests across 92 test executables/doc-test groups; 75 existing tests are ignored in the default configuration. The typed Rust contract generator check passed (219 types and 1,643 fields), with 417 public client operations after MDI retirement.

LivePipeline was rerun with its explicit isolated JetStream endpoint: all 322 tests passed, with no skips. This includes durable Options/Futures terminal recovery notifications, the API mailbox-deadline notification and Core NATS console delivery. These previously environment-skipped theories expand to five concrete cases when enabled.

No new removal-specific test was skipped. Remaining intentional skips are listed by name in `verification-summary.json`; they include retired direct tick-insertion paths, live-provider/long-history qualifications, platform checks and sustained-load/production-restart opt-ins. Skips do not count as successful PostgreSQL qualification. A transient full Trade run failed a Weekly risk-persistence deadline under concurrent load; all five workflow cases then passed in isolation and the final full Trade run passed all 162 executed cases.

| Final .NET group | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Unit and BDD matrix | 7,366 | 0 | 5 |
| Full integration suites | 1,822 | 0 | 17 |
| Additional restore/transaction/composition/UI startup checks | 12 | 0 | 0 |
| Total | 9,200 | 0 | 22 |

The earlier Feed run with optional broker tests enabled exposed an uncorrelated listener taking a VX event as the ES result. After correlating the command ID and ensuring listener disposal, the full expanded Feed suite passed. Earlier failed/exploratory TRX files are retained for traceability; `verification-summary.json` identifies the final accepted run for each suite and does not double-count focused reruns.

Redis remains a dependency of the isolated historical comparison benchmark only. The benchmark starts its own disposable Redis, PostgreSQL and NATS services. Normal integration infrastructure does not start Redis.

## Comparative measurement scope

BenchmarkDotNet compares the retained pre-removal algorithm with the production typed stream-identity cache, using .NET 10, Server GC, MemoryDiagnoser and ThreadingDiagnoser. The historical warm path performs Redis EXISTS/GET and JSON deserialization; the replacement uses the actual storage context. Cold and same-key concurrent tests include PostgreSQL stream resolution on both paths and verify matching durable identities.

The projector comparison measures the removed JSON/Redis mirror operation around an already retrieved durable state. Eliminating that operation does not eliminate PostgreSQL projection work; it must not be presented as the latency of a complete projector.

These are component measurements. No valid before/after whole-API startup or process-wide GC pause comparison was captured. A readiness endpoint's own response duration is not a startup duration.

### Measured results

BenchmarkDotNet: two launches, three warmups and ten measured iterations per launch. Cold resolution: 200 existing durable streams per path. Concurrent bursts: 40 cold keys per concurrency and path. All times are measured on the same workstation with isolated stores.

| Workload | Before | After | Allocation evidence |
|---|---:|---:|---|
| Warm stream identity, BDN mean | 2.25565 ms | 33.39 ns | 688 B to 72 B/op |
| Cold existing stream, mean | 3.823 ms | 1.611 ms | Includes PostgreSQL resolution on both paths |
| Cold existing stream, p95 / p99 | 5.222 / 6.895 ms | 1.990 / 2.157 ms | 200 samples per path |
| Same-key cold burst, 8 callers, mean | 14.493 ms | 1.487 ms | Equal durable identities verified |
| Same-key cold burst, 32 callers, mean | 80.869 ms | 1.503 ms | One shared load for the replacement |
| Latest snapshot and suffix, paired mean / p95 | 1.951 / 2.579 ms | 1.883 / 2.512 ms | 17,872 / 18,521 B per validated sample |
| Command outcome by stream name, paired mean / p95 | 1.730 / 2.331 ms | 1.701 / 2.415 ms | 14,718 / 14,941 B per sample |
| Durable marker read/update batch of 32, paired mean / p95 | 284.641 / 328.121 ms | 245.508 / 284.487 ms | 937,611 / 906,952 B per batch |
| Removed mirror operation alone, BDN mean | 0.98081 ms | Operation removed | 688 B to zero |

The single-query snapshot and command-outcome algorithms are unchanged and already resolve names within SQL. Their before/after rows are controls, with no claimed speedup. They retain one SQL query and do not gain a Redis or separate stream-ID lookup. The paired harness alternates order, validates identical snapshot windows and seeds 512 source events with a checkpoint at revision 511. Its allocation values include validation overhead and process-wide asynchronous allocations; use BDN for the cleaner warm-cache allocation comparison.

The marker batch is a controlled PostgreSQL read/upsert workload with the old Redis mirror included only on the baseline. It uses 64 PostgreSQL commands on both paths, plus 32 Redis SET operations before removal. It measures marker persistence, not complete business projection, NATS delivery or actor recovery latency.

BDN warm identity contention was 0.0039/op before and below the diagnoser's displayed resolution afterward; the mirror showed 0.0020/op before and none afterward. Warm identity Gen0 was 0.0009 collections per 1,000 operations afterward; no Gen1/2 collections were reported. A dash in the baseline GC column means no collection was observed in that measurement, not zero allocation. The removed-operation comparison is below timer resolution after removal and is not reported as a meaningful near-zero latency estimate.

Raw evidence: `bdn-final/results`, `cold-timings-final.log`, `store-path-timings-paired.log`. An earlier unpaired store run showed order/load noise; alternating the paired order confirmed the unchanged SQL paths remain equivalent. Historical exploratory runs are retained separately.

### Development runtime sample

A two-minute post-restart `System.Runtime` capture recorded approximately 546 MiB mean API working set, 3.59 MiB/s mean allocation, zero thread-pool queue length in all samples and 66.7 ms total GC pause time across the observed intervals. The busiest one-second interval contained 30.1 ms of aggregate GC pauses; this is not an individual-pause duration. Monitor contention averaged 11.8 events/s, with a maximum of 214 in one interval. This process-wide startup/steady transition sample is not a before/after improvement claim. Raw counters, their summary and a separate GC/contention EventPipe trace are retained on E:.

## Development cutover

The Development Redis database was saved and copied to `E:\IFM\RedisRemoval\20261108\baseline\redis.rdb`, with a recorded hash and container configuration. Its container is stopped and its restart policy disabled. The original volume is retained for recovery.

Development is restored using the Rust Market Data Host, API, scheduler and standalone Rust eGui with full source debugging symbols. The observation ran from 23:21:13 to 23:51:42 Toronto time on November 8: 30 minutes 29 seconds, 57 healthy client samples, and healthy overlapping server audit history. Redis stayed stopped, and all five Development process identities remained stable. ES, VX and Options were healthy at completion; the dataset generation IDs matched the saved mid-soak values. No broker order or synthetic business transaction was submitted for this removal.

Final evidence includes `development-soak/summary.json`, `development-soak/samples.json`, `development-final-health.json`, `development-pipeline-history-final.json`, `development-nats-connections.json` and `verification-summary.json`. All final .NET runs have zero failures. Changes are left in the working tree for review.

The API reached startup `Running` with all eight activities satisfied and 216 registered actor types. Native Host readiness is `Ready`; ES, VX and Options datasets are healthy. eGui has an active NATS connection with advancing message counters and zero pending bytes, and received the live Options monitoring-ready notification. Server Manager, API, Rust Host, scheduler and eGui are all owned by the same Development session.

The initial post-restart readiness samples included normal incomplete-startup/stale-audit observations; the soak starts after these clear. The observer briefly stopped when an isolated test child API was counted as a second Development API. Ownership filtering corrected the observer; the actual Development process IDs and healthy HTTP responses remained unchanged. The sample file preserves that brief observation gap.

Log review found no API/Host Error or Fatal entries and no Redis messages in this Development run. Existing FifteenSeconds/OneMinute stored-seed and Bollinger warm-up warnings continue. The same warnings were present before cutover: 418 matching entries precede the baseline capture, including examples at 22:07 on November 8. These are a separate historical-input limitation; the healthy process/pipeline checks do not prove those indicators are fully warm. This removal does not claim live strategy qualification beyond the available inputs and passing isolated workflow tests. Evidence: `development-log-summary.json` and `pre-removal-seed-warning-evidence.json`.

Automatic approval review rejected removal of the four retired project folders' ignored generated outputs with only `blocked by policy`. Tracked source/projects and active deployment dependencies are removed. Generated leftovers are retained, along with the intentional Redis recovery volume and backup. These files are not loaded by Development.

## Additional verification repairs

- Recovery fixtures build their Stage 3 worker explicitly and resolve configuration from assembly metadata instead of mistaking `net10.0` for a build configuration.
- Recovery audit tests use their dedicated isolated PostgreSQL/NATS/native publisher harness.
- DownloadLog fault injection is scoped to the test's own import, leaving earlier durable markers recoverable.
- Economic-calendar import producers use the same whole-second timestamp as their actor identity. Integration coverage exercises an arbitrary sub-second input.
- Import test fixtures exercise the owning command's PostgreSQL transaction and select their mocked in-process provider explicitly.
- Focused actor fixtures do not start a remote tick receiver without its Feed mailbox.
- The streaming integration listener now correlates the returned command ID, so background VX subscriptions cannot satisfy an ES test's event assertions.
- Rust compatibility fixtures include the existing cumulative P&L columns. Rust Regime Discovery editor mapping now mirrors the server's Daily EMA baseline normalization.

## November 9 indicator warm-up follow-up

The separate FifteenSeconds/OneMinute initialization warnings described above were investigated and fixed. Startup now restores qualified committed indicators before requesting historical replay, reattaches live processors without replacing their checkpoints, and waits for the asynchronous Bollinger projection after EMA seeding. Local history gaps are warnings only when native historical fallback also cannot supply the required bars.

After the November 9 06:01 Development restart, both short frames were verified warm and strategy indicator initialization completed at 06:01:38 EST. PostgreSQL observations confirmed warm, valid RSI13/14, ADX, ATR and MACD, schema-2 TDI, and warm valid Bollinger bands; subsequent observations advanced, including Bollinger on the active continuous ES series. No seeding/Bollinger warning or error was observed in the restarted process during verification. The full isolated-NATS LivePipeline suite passed 328 tests with no skips. API and native Host readiness were healthy, with API, eGui, Rust Host and scheduler running. Evidence: `E:/IFM/IndicatorWarmup/20261109`.
