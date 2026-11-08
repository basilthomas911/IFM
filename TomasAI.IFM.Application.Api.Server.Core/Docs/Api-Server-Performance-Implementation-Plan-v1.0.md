# API Server performance implementation plan

Status: implementation and measured component qualification complete. Active-market end-to-end acceptance pending because the session is closed. Updated: 2026-11-08.

The reviewed working-tree baseline (15,920 files, tracked binary diff, untracked source and SDK/runtime information) is preserved at `E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107`. The final Release benchmark dependency graph builds with zero warnings/errors. Final qualification completed 94 BenchmarkDotNet cases (47 pairs), 5,983 passing tests and all five actor-convention audits; five opt-in/platform cases remain unexecuted. Development is restored in Debug. The [final report](Api-Server-Performance-Final-Report-2026-11-08.md) records every disposition, regressions, resource measurements, financial invariants and unverified end-to-end/soak acceptance boundaries. No October measurements are counted as this baseline.

Improve the twenty audited API Server execution paths while preserving event sourcing, actor ordering, financial outcomes, source identities and readiness checks. Extend the existing BenchmarkDotNet infrastructure to compare the current implementation against each candidate, then qualify the complete system against isolated infrastructure and Development.

## Scope and baseline qualification

The scope includes API Server Core and the shared actor runtime, NATS transport, Market Data RPC client and trade caches it executes. The Rust Market Data Host remains the Development provider. This is an optimization effort, not a host/provider migration.

The earlier audit is a prioritized list of code concerns, not a profiler-derived ranking. Qualify reachability, frequency and cost before changing a path. In particular:

- Item 5 was overstated: default actor receives already use pooled `NatsMemoryOwner<byte>` ownership. Investigate actual remaining byte-array fallback, fan-out and durable-envelope boundaries. Do not replace the working owned-payload path or promise zero total message allocation.
- Items 6 and 7 already benefit from deadline-aware qualification, cached membership arrays, direct leg lookup and optimized spread calculation. Preserve these measured improvements and target residual work inside locks and the outer expiry scan.
- Health responses already use a one-second output cache. Preserve it; measure work on misses and duplication across distinct consumers.
- Historical four-hour seeding already has native acquisition and persistence. Preserve restart reuse; optimize remaining API-side reads, aggregation and initialization.
- Shared initialization gates may be protecting singleton mutable state. Establish safe ownership before narrowing locks or increasing concurrency.

Use the reviewed working tree as the new baseline, including the historical EOD/P&L corrections. Record Git HEAD, tracked diff, relevant untracked source hashes, SDK/runtime, dependencies and fixture hashes. HEAD alone does not describe this baseline. Save immutable benchmark-only reference implementations or an isolated source export; do not reset the working tree. Existing October benchmark results provide regression coverage, not the baseline for this change.

## Implementation order

| Wave | Work | Dependencies and exit condition |
|---|---|---|
| 0 | Reachability checks, reproducible fixtures, baseline benchmarks and process traces for all 20 items | Record actual before measurements and current behavior before production edits. Classify already-optimized or inactive cases explicitly. |
| 1 | Logging and messaging: 1–5 | Retain wire compatibility and prove payload ownership through cancellation, rejection and fan-out. |
| 2 | Trade caches and diagnostics: 6–10 | Preserve quote freshness, generation fencing and expiry notifications; qualify under concurrent readers/writers. |
| 3 | Indicator seeding and startup: 11–18 | Implement shared seed ownership before concurrency changes; preserve per-aggregate bar order and independent timeframe progress. |
| 4 | Trade restoration and Redis startup: 19–20 | Prove idempotent recovery, bounded work and explicit failed/blocked dependency reporting. |
| 5 | Combined benchmarks, full affected suites and Development verification | Publish the 20-item comparison and verify system behavior, including the settled historical trade. |

Each item must finish correctness qualification and its before/after comparison before it is marked complete. A baseline qualification can close an item as already optimized or inactive with evidence; it must not trigger a speculative rewrite.

## Work packages and required tests

### 1. Routine actor logging

Scope: `ActorMessageProcessingLog`, actor information logging options and API log configuration. Add configurable sampling or slow-operation thresholds for routine Query/Command successes; retain errors, audit facts, cancellations and diagnostic correlation. Preserve existing Event/Realtime suppression and count suppressed messages.

Tests: exact route configuration, invalid options, slow-message capture, failure visibility, disabled-policy equivalence and concurrent sampling. Benchmark realistic message mixes with logging disabled, enabled and sampled; record CPU time, allocations, output bytes and dropped-log counters.

### 2. Development console output

Place console output behind a separately bounded asynchronous sink. Define overflow counters, warning/error treatment, bounded flush and shutdown behavior. Keep file and telemetry sink policies explicit so console backpressure cannot stall actor execution or hide failures.

Tests: stalled console writer, full buffer, sink exception, bounded shutdown, final flush and severity handling. Benchmark fast, slow and blocked consumers; measure producer latency, completed writes, drops and queue memory. Reduced delivery is not a throughput improvement unless disclosed.

### 3. RPC operation descriptors

Cache or generate the Market Data operation lookup, method signature and argument metadata instead of reflecting and rebuilding signatures on every call. Preserve all overloads, cancellation parameters and supported route names.

Tests: every supported method/overload, cold cache, concurrent first access, unsupported operation and exact existing signatures. Benchmark cold initialization separately from warmed descriptor lookup.

### 4. RPC payload construction

Reduce per-argument serialization buffers and nested envelope copies using cached codecs, reusable writers or explicitly owned memory. Keep the current Host protocol compatible. A typed protocol change requires an explicit versioned migration and compatibility tests; do not silently alter persisted or on-wire bytes.

Tests: old/new API–Host combinations, byte/semantic equivalence, null and cancellation arguments, oversized payload rejection, concurrent calls and cancellation during send. Benchmark status, contract batches, four-leg composition and seed requests at representative payload sizes; measure allocations and bytes copied.

### 5. Remaining NATS receive copies

Extend the existing MessagePack/NATS allocation plan. Trace which receive paths use pooled ownership and which still use `NatsByteArrayMessageSerializer`; remove only avoidable copies with safe payload ownership. Keep independently owned durable bytes where persistence requires them.

Tests: single/multi-segment payloads, delayed mailbox parsing, admission rejection, cancellation, fan-out, retirement, disposal, duplicate delivery, malformed/compressed input and pool reuse poisoning. Every owner must be released exactly once. Reuse messaging unit/integration suites and benchmarks; compare pooled default and actual fallback cases separately.

### 6. Trade-cache critical sections

Measure residual work in `TradeRealtimeHotCache.Apply`, `Publish`, qualification and position enrichment. Reuse unchanged immutable components and shorten critical sections through version-checked commits or single-writer ownership. Revalidate generation and revision if work moves outside a lock.

Tests: quote/plan/position races, partial fills, stale generations, retired caches, retained quotes, expiry, multiplier corrections and unchanged snapshots. Benchmark 1/4/16 producers with concurrent readers; measure allocations, monitor contention, async waits where applicable and tail latency. Retain existing randomized equivalence checks.

### 7. Expiry scheduling and publication

Replace unnecessary full scans with an indexed next-deadline scheduler where measurement supports it. Use bounded expiry-notification dispatch without changing per-trade ordering. Fence stale schedule entries and define retries when expiry publication fails.

Tests: no incoming ticks, multiple simultaneous deadlines, unchanged quality checks, quote renewal, trade removal/re-registration, backward/forward clock scenarios, slow NATS and shutdown. Benchmark 1/64/1,024/4,096 resident trades with sparse and burst expirations. Record detection lag and delivery lag as well as scan cost.

### 8. Actor Health response size

Separate summary, per-actor detail, projector/worker detail and paged history, keeping a compatibility path until all consumers migrate. Retain output caching, authoritative health states, access checks and bounded query ranges. Reuse immutable snapshots where their revision permits.

Tests: endpoint and UI contract compatibility, paging boundaries, cache invalidation, range limits, failure visibility and concurrent consumers. Benchmark snapshot construction plus real JSON serialization for small, medium and large actor populations, including cache hits and misses. Measure response bytes and peak memory.

### 9. Shared backend health snapshots

Coalesce concurrent backend status/read-snapshot refreshes and share a freshness-qualified result across diagnostics consumers. Define maximum age, failure state, refresh ownership and independent cancellation of waiters. Cached health must not mask a known failure or revive stale readiness.

Tests: many simultaneous callers produce one refresh, cancellation isolation, failed refresh/retry, generation changes, expiry and quiet off-hours readiness. Benchmark 1/8/32 consumers and distinct health endpoints; measure Host RPC count, allocations and p95/p99 request latency.

### 10. Supervisor history persistence

Avoid full JSONL parsing and sorting behind the append semaphore. Add a bounded in-memory index or segmented files, with background compaction and a short atomic replacement boundary. Preserve crash recovery, retention and chronological query semantics.

Tests: concurrent append/read/compaction, corrupt/truncated final lines, cancellation, disk full, process restart, file replacement and retention boundaries. Benchmark empty, 1,000-row, retained-capacity and size-limit files with concurrent readers/writers. Measure read/append latency, parsing allocations, peak memory and gate wait time.

### 11. Repeated seed aggregation

Aggregate shared raw input once into requested intervals, avoiding repeated filtering, sorting and temporary arrays. Preserve event-time order, duplicate identities, exact decimals, OHLCV, trade count, price-volume sums and source provenance. Use an explicit strategy for genuinely unordered input rather than assuming arrival order.

Tests: ordered/shuffled input, duplicate and late trades, ties, session boundaries, midnight/DST, invalid rows and independent timeframe output. Benchmark 15-second/one-minute windows and mixed timeframe requests against identical raw corpora.

### 12. Raw seed memory usage

Stream bounded raw-tick pages into accumulators instead of retaining complete chunks. Bound deduplication and sorting buffers, maintain partition/window semantics and dispose pages promptly. Retain the native four-hour seed cache path.

Tests: page boundaries, cancellation and read failures mid-page, out-of-order trades across pages, overlapping windows and bounded retention on dense input. Benchmark low/median/dense four-minute raw chunks; report peak process memory and allocated bytes in addition to elapsed time.

### 13. Duplicate structure and seed reads

Avoid the overlapping 21-bar read when committed indicator timestamps already prove it cannot qualify. Preserve the `structure.Length == 48` reuse branch: qualification showed that native fallback can return 48 bars after a 21-bar request. Capture one cutoff for both paths. Key reuse by contract, frame, source version and completed interval, with explicit invalidation.

Tests: warm fast path, stale indicators needing 48 bars, missing history, rollover and source-version changes. Benchmark warm, partial and cold requests; count storage/Host calls and bars transferred.

### 14. Retry only unfinished warm-up work

Track progress per indicator and qualified seed revision. Reuse successful initialization when a later indicator or Bollinger dependency fails; apply bounded retry/backoff to missing work. Do not suppress required-data validation or allow one timeframe to block another.

Tests: failure after each initialization step, retry after restart, source changes, genuine stale results and Daily/Weekly/Monthly independence. Benchmark partial failures across repeated retry cycles; count actor commands, database writes and time to readiness.

### 15. Batch indicator read-model restoration

Batch the independent ADX/ATR/MACD/RSI/TDI/Bollinger reads where practical; otherwise use bounded asynchronous reads through concurrency-safe contexts. Keep each result's identity, period, value date and provenance checks.

Tests: missing/invalid individual results, cancellation, mixed contracts/timeframes, provider parameter bindings and consistent revision qualification. Benchmark isolated PostgreSQL reads with the same data and connection-pool limits; report round trips and total restoration latency.

### 16. Independent indicator initialization

Start independent RSI13, RSI14, ADX, ATR and MACD work with bounded concurrency. Make dependencies explicit and keep per-aggregate messages ordered. Return a complete failed/blocked/succeeded report rather than stopping unrelated initialization at the first error.

Tests: one slow/failing indicator, maximum concurrency, cancellation and shutdown; verify all independent attempts finish and downstream readiness remains truthful. Benchmark real command execution in an isolated host as well as scheduling overhead; simulated delays alone cannot establish application speedup.

### 17. Ordered historical seed batches

Replace per-bar request/reply and observation writes with bounded ordered seed batches where aggregate ownership permits. Preserve stream creation, snapshots, event identities, transaction boundaries and per-aggregate event ordering; retries must not duplicate accepted observations or events.

Tests: partial batch failure, restart, duplicate batch, overlapping seeds, chronology validation, PostgreSQL latest-snapshot restoration, and reconciliation of source/read-model counts. Benchmark 21/48/64-bar workloads, including stream resolution and persistence on both paths. Report event/write counts alongside timings.

### 18. Initialization lock scope

Replace coarse singleton initialization gates with keyed in-flight operations for contract/date/timeframe. First establish safe ownership of mutable completion dictionaries and seed readers. One caller cancelling must not cancel shared work still needed by other callers; retire completed keys under a bounded retention policy.

Tests: same-key coalescing, different-key progress, concurrent date changes, cancelled waiter, faulted operation, retry and bounded key retention. Benchmark identical-key contention and multiple independent contracts; record semaphore wait histograms separately from monitor contention.

### 19. Saved trade restoration

Restore independent saved trade leases with bounded concurrency, retaining per-trade ordering and success receipts. Refresh the recovery list once per pass rather than per failed trade; retry only unresolved registrations. Bind all attempts to the original recovery deadline and generation.

Tests: partial success, missing/deleted registration, Host timeout, restart, duplicate restore, lost reply, stale generation, quiet options and cancellation. Verify no duplicate subscriptions, orders or fills. Benchmark 1/4/32/128 saved trades with healthy, slow and failed subsets; record restoration time and Host request count.

### 20. Asynchronous Redis connection startup

Create the shared Redis multiplexer through asynchronous startup orchestration instead of blocking a singleton factory. Preserve connection reuse, explicit availability to dependents, disposal and failed/blocked reporting. Independent services must continue their startup attempts when Redis is unavailable.

Tests: healthy/slow/unavailable Redis, startup cancellation, dependent blocked states, reconnect and disposal exactly once. Benchmark cold and warm startup against isolated Redis; record connection latency, blocked threads and overall startup duration.

## BenchmarkDotNet before and after method

Use the existing `TomasAI.IFM.Application.Api.Server.Benchmarks` project and its pinned BenchmarkDotNet 0.15.8 dependency. Reuse `ApiHotPathOptimizationBenchmarks`, health/JSON/cache benchmarks and randomized equivalence checks. Extend `TomasAI.IFM.Framework.Messaging.Nats.Benchmarks` for item 5 rather than creating a competing transport harness. Existing legacy baselines stay intact.

Add new benchmark classes with an `ApiServerPerformance` prefix and categories tied to IDs 01–20. Pair an immutable current baseline with the production candidate, keeping identical fixtures and semantic outputs. Counter-based tests must demonstrate reduced work; a renamed implementation or a benchmark that duplicates the optimization logic is insufficient.

- Run out of process in Release on the same .NET 10 SDK/runtime, architecture, server-GC setting and CPU/power configuration. Do not change the running Development app's full-debug build to obtain benchmark results.
- Use at least two launches, three warm-ups and ten measured iterations for stable microbenchmarks; increase duration when uncertainty remains. Separate cold initialization from warmed throughput. Async I/O cases need independently reset fixtures and must not amortize a one-time operation accidentally.
- Enable `MemoryDiagnoser`, `ThreadingDiagnoser`, categories and raw JSON export. Record mean, error/confidence interval, ratio, allocated bytes/op and Gen0/1/2 counts. Use separate EventPipe/process traces for GC pause duration, LOH/peak memory and allocations on background threads.
- Instrument semaphore/async wait time explicitly. ThreadingDiagnoser's monitor-contention count does not measure all asynchronous lock waits.
- Measure end-to-end p50/p95/p99 latency and throughput with a timestamped request/load harness. BenchmarkDotNet iteration percentiles and normalized concurrent batch means are not per-request latency.
- Use representative matrices rather than a huge Cartesian product: payloads 256 B/4 KiB/64 KiB/near-limit; trades 1/64/1,024/4,096; concurrency 1/4/16 where relevant; seeds 21/48/64 bars; healthy, delayed and failed dependencies.
- Exclude fixture generation, build, setup, teardown and injected delay from pure compute timings. Include serialization, stream resolution, pool acquisition and I/O symmetrically in the end-to-end comparisons that claim to improve those costs.
- Run before/after in alternating order or paired jobs; repeat significant results. Do not build, run tests or profile concurrently with timing runs. Record background Development load; use an isolated quiet window for acceptance measurements.
- Keep large raw outputs and generated benchmark builds on E: because C: has limited space. Retain compact reports, fixture hashes, configuration, baseline provenance and reproduction commands in the repository benchmark Results directory.

Planned invocation after the new benchmark classes exist:

```powershell
$perfRun = 'E:\IFM\BenchmarkArtifacts\ApiServerPerformance\<run-id>'
dotnet run --project TomasAI.IFM.Application.Api.Server.Benchmarks -c Release -- --filter '*ApiServerPerformance*' --artifacts "$perfRun\paired" --exporters json --buildTimeout 600
```

Configure generated build output on E: as part of the runner, not just report output. The paired run must label current-baseline and candidate methods clearly. Preserve the same environment for before and after, including startup/stream resolution on both paths.

For each ID publish: workload, source/fixture hashes, before time, after time, percentage change, before/after allocation, GC/peak-memory evidence, contention/wait evidence, RPC/SQL/log-byte counts and correctness result. Final measured cells are published in the November 8 report and comparison CSV; previous October results and projected gains must not be presented as new measurements. Do not add overlapping microbenchmark speedups together.

## Validation and acceptance

Use existing affected projects: Domain.Application actor unit tests; Application.MarketData unit tests; Framework.Messaging.Nats unit and broker-backed integrated tests; Application.Storage integration tests; Domain.MarketData.Analytics unit/integration tests; Domain.Trade unit/BDD tests; UI.Rust system tests and targeted native Rust controller/rendering tests. Add new scenarios to the owning suite instead of one catch-all test project.

Infrastructure tests use disposable PostgreSQL, NATS and Redis instances, and isolated Scylla tick data only where required. Refuse Development databases, production subjects and default service endpoints in destructive fixtures. Use recorded deterministic data; no paid provider acquisition or real broker orders.

Accept each item only when behavior and ordering remain correct, all required tests pass, and the claimed improvement is reproducible beyond measurement noise. Record mixed outcomes: reducing allocation at a material latency cost is not automatically an improvement. Proposed initial guardrail: investigate repeatable regressions above 5% in an affected latency/throughput case before enabling it; this is a review trigger, not a guaranteed universal budget. Where gains are immaterial, retain the current implementation and close the item with qualification evidence.

Combined verification must include:

1. Full affected suites and existing actor/query/event-sourcing convention audits, including latest full-state snapshot loading and idempotent historical import/seed reconciliation.
2. Sustained isolated-host replay with realistic actor, quote, workflow and health-query traffic. Measure queue growth, resident memory, GC pauses, lock waits, p95/p99 latency and stable shutdown.
3. Fault injection for slow logs, blocked history compaction, Redis outage, NATS loss, Host delays, cancellation and restart. Preserve independent startup and truthful failed/blocked states.
4. Development deployment using the Rust Market Data Host and Rust eGui, retaining full source-level debugging. Verify independent Daily/Weekly/Monthly progress, quiet-feed Options readiness, fresh-quote OrderPrice rules and cached four-hour startup seeds.
5. Read-only regression for trade 4202/position 1901:1501/order 701: 18 history rows, recovered October 26/27 EOD rows, history P&L sum -352.70, realized P&L -347.50, commissions 5.20, Closed state, four unique close fills and unchanged settlement postings. No resubmission.

Deliver the source changes, tests, twenty-item disposition, raw/summary benchmark references, combined before/after report and Development verification evidence. Keep changes independently reversible by wave. Capture any actual blocker with its evidence rather than weakening readiness, quote freshness or event ordering to obtain faster timings.
