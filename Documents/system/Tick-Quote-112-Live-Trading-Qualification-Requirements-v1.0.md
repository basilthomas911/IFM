# Tick quote 112-batch live-trading qualification requirements

**Date:** 2026-09-16
**Status:** Qualification requirements defined; 112 is not enabled for a live Databento feed
**Scope:** Futures and futures-option tick quote aggregation, NATS publication, actor projection, and Scylla storage in the IFM API and UI runtime

## Decision and evidence

The current live default is 64 quotes per batch for futures and futures options. Keep that default while 112 is qualified. The existing 112 experiment establishes that the retained-buffer pipeline can publish and store a large option-heavy workload with low allocation and no observed backlog. It does not establish live-feed quote age, live payload-size behavior, or full-application stability.

| Observation | Earlier 64 run | Pooled 112 run | Interpretation |
|---|---:|---:|---|
| Duration | 15 minutes | 15 minutes | Both isolated synthetic actor/Scylla runs |
| Quote items | 1,732,352 | 1,732,416 | Nearly matched volume |
| Stored batches | 27,068 | 15,468 | About 43% fewer writes at 112 |
| Sustained managed allocation | 2.292 MB/s | 0.864 MB/s | About 62% lower whole-host allocation in the 112 run |
| Sustained Gen0 / Gen1 / Gen2 | 171 / 0 / 0 | 47 / 2 / 1 | The 112 run still had one full collection |
| Publisher-to-row visibility p95 | 37.6 ms | 37.8 ms | Similar **after an assembled batch was submitted** |

All 15,468 accepted 112 batches were published; the per-contract Scylla row counts matched; the publisher had no failures, rejection, expiration, backlog, or outstanding leases; and the host exited cleanly. The prepared Scylla benchmark measured about 0.956 ms per 112-quote write with synthetic values. A repeatable local latency jump appeared at 124 quotes and above for those benchmark field values, while a 512-quote write measured about 44.7 ms. Real decimal lengths, encoded bytes, contract mix, and server conditions may move that boundary.

The earlier 64 run used the old allocating decoder/CQL path, while 112 used the new retained-buffer path. Therefore the allocation difference cannot be attributed solely to changing the batch count. A same-build 64-versus-112 live comparison is required before promotion. The [64/4,096 baseline](Tick-Quote-64-vs-4096-Capacity-Experiment-Results-v1.0.md), [retained-buffer results](Tick-Quote-512-Buffer-Reuse-Implementation-Results-v1.0.md), and [112 soak verdict](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit112Pooled/tick-quote-soak-20260916-002531.json) preserve the measurements.

## Current runtime constraints

The API reads `AppSettings:Databento:FuturesQuoteBatchCapacity` and `AppSettings:Databento:FuturesOptionQuoteBatchCapacity`, each defaulting to 64. [Startup](../../TomasAI.IFM.Application.Api.Server/Startup.cs) rejects either capacity above 64 unless Stage 3 is enabled and the source is Synthetic. Stage 3 itself rejects a live Databento source. The checked-in Development profile selects `DatabentoLive` and Stage 3 is disabled. Changing a configuration value to 112 in that profile currently stops API startup; the UI cannot observe a running 112 live feed.

For synthetic full-application rehearsal, the source must use `SyntheticCi`, Stage 3, and isolated PostgreSQL/Scylla targets identified as synthetic. The [synthetic persistence guard](../../TomasAI.IFM.Application.MarketData/DataBento/SyntheticPersistenceIsolationGuard.cs) enforces those targets. The configured synthetic producer has 10,000 records at 10 per second, so an overnight rehearsal needs a longer-running synthetic configuration. Run one API and one UI; do not run an isolated soak host against the same feed or storage targets concurrently.

The [aggregation service](../../TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/TickAggregationService.cs) currently flushes a quote buffer when it fills, when a trade is observed, on a value-date change, and when the feed stops. There is no maximum-age flush. Live quote and market-price routing occurs before quote-buffer flush, but durable quote history can wait behind an incomplete buffer. A quiet contract may wait indefinitely until another flush trigger. The isolated soaks submitted already assembled batches, so their row-visibility figures exclude this dwell time.

The retained-buffer implementation has a 512 MiB producer generation reservation limit, 128 warmed 512-item decoder slots with allocating overflow, and a 64 MiB idle CQL byte-buffer cache. The normal live publisher has a bounded waiting channel by message count; the separate Stage 3 publisher also bounds retained quote items. Quote-buffer pool waits, publisher waits, decoder overflow, and idle CQL cache occupancy must be visible in a live pilot. Buffer ownership must continue until NATS send, actor projection, and the underlying Scylla request are finished, including after caller cancellation or feed reset.

## Required implementation before a live 112 pilot

1. **Narrow startup gate.** Add an explicit, off-by-default live-quote pilot setting that permits only capacity 112, independently for futures and futures options, in a named Development or PaperTrading feed profile. Do not remove the general above-64 rejection or permit arbitrary values up to 4,096 on a live source. Keep Production at 64 until its own qualification is approved. Log the effective source, profile, asset capacities, and pilot state at startup.
2. **Bound incomplete-batch age.** Add a configurable per-contract maximum age measured from the first accepted quote in the current buffer. When the age limit is reached, emit the partial batch once through the same ordered publication path. This must preserve contract/value-date identity, quote order, source sequence, and idempotent reset behavior. It must avoid a one-timer-per-quote allocation pattern. Select the initial age limit from a measured 64-quote live baseline and the downstream quote-history freshness requirement before enabling the pilot.
3. **Bound retained work in the normal live publisher.** Extend the normal publisher's message-count bound with a retained-quote-item or byte budget that includes queued and in-flight batches. Saturation must apply backpressure rather than silently discard quotes or allocate an unbounded overflow queue. Publish depth, oldest age, retained items/bytes, wait duration, and failures as health metrics.
4. **Expose end-to-end timing.** Capture first-quote-to-flush, flush-to-NATS acceptance, NATS-to-actor, actor-to-Scylla completion, and first-quote-to-row-visibility distributions by asset and contract-rate bucket. Distinguish pending-buffer age from publisher and database queue age. Capture Scylla prepared-write p50/p95/p99 and encoded CQL payload bytes, including maxima and the distribution around the synthetic 120–124 quote latency boundary.
5. **Preserve pool and cancellation safety.** Verify producer reservations for the full live subscription set stay within the generation budget. An old generation's buffers may be returned only to that generation; a pending driver request may retain its encoded buffer until actual completion. Resets, cancellations, shutdown, and provider failure must leave no outstanding leases, reused in-flight arrays, or missing stored batches.
6. **Keep 64 available for rollback.** Store the capacities separately for futures and futures options, record the effective value in health output, and provide a restartable rollback to 64 without schema or wire changes. A pilot rollback must drain or safely retire the old generation before the new one accepts data.

The current retained 112 quote arrays are below the ordinary .NET large-object threshold. Exact encoded CQL array size varies with quote values, so record actual buffer lengths and full-GC behavior during the live run. Microsoft's [.NET LOH guidance](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/large-object-heap) identifies allocations of 85,000 bytes or more as large objects on Windows and explains why repeated large allocations can affect Gen2 collections. This is a monitoring reason, not proof that the earlier local Scylla latency jump was caused by the GC.

## Verification and acceptance gates

**Gate A — same-build controlled baseline.** With one API and UI, run the new retained-buffer build at 64 on a real Databento Development feed. Record an active trading session and a quiet/overnight interval with the same subscription set and storage schema intended for the 112 pilot. Collect quote-rate buckets per contract, buffer ages, end-to-end lag, publisher depth, Scylla write latency, encoded-byte distribution, process allocation, working set, Gen0/Gen1/Gen2 counts and pause times, and rows written. This baseline replaces the older allocating 64 soak for causal comparison.

**Gate B — synthetic full-runtime rehearsal.** Run 112 with the API and UI under isolated SyntheticCi persistence for a long enough record count to include sustained traffic and a quiet interval. Confirm UI feed health displays capacity, pending-buffer age and retained-work metrics. Exercise partial age flush, trade-triggered flush, date rollover, cancellation, feed reset, and shutdown; verify row counts and nested quote fields/order against the produced data. Ensure the synthetic host and UI exit cleanly after verification.

**Gate C — real-feed Development pilot.** Permit 112 for one asset class at a time. The independent settings allow a futures-option 112 / futures 64 pilot, or the reverse, without changing both together. Run at least one full active session and the adjacent overnight period with the API and UI together, with no parallel test host or second feed owner. Use the same subscription set as Gate A where feasible. Capture quiet contracts as well as the busiest contracts. A pilot is a pass only when:

- Accepted quote counts, emitted quote counts, NATS publications, and per-contract Scylla stored counts reconcile after drain; sampled rows preserve all nested UDT fields and quote order.
- No publisher failures, unaccounted discards, expired batches, permanent backlog, decoder overflow under expected concurrency, premature buffer reuse, or outstanding leases remain after reset/shutdown.
- The agreed first-quote-to-storage freshness limit is met for both busy and quiet contracts. Publisher-to-row p95 alone does not satisfy this gate.
- Prepared-write p95/p99 and Scylla server-side write latency have no material regression against the same-build 64 baseline; any local latency band near the 112 payload distribution is understood or absent. Scylla's [proxy histogram command](https://docs.scylladb.com/manual/stable/operating-scylla/nodetool-commands/proxyhistograms.html) can supplement client timings with server write percentiles.
- Whole-process allocation rate and GC pause burden improve under comparable quote volume without a sustained growth trend in live heap, working set, producer reservations, or CQL cache. A one-off Gen2 collection is assessed by pause impact rather than treated as an automatic failure.
- Full app startup, Market Outlook and Feed Health remain usable; any degradation is classified and visible rather than hidden by retries.

**Gate D — PaperTrading and Production.** After Development acceptance, repeat the real-feed run in PaperTrading under its actual subscription and broker-account workload. Production enablement is a separate, off-by-default change after PaperTrading acceptance. Start with a limited asset scope and defined observation window, retain an immediate 64 rollback, and verify position/trade lifecycle and feed-health behavior alongside quote storage. A quote batching decision cannot substitute for the separate broker and trading-account release gates.

Before Gates C and D, record the numeric freshness limit, permitted p95/p99 change, memory/GC pause limits, observation windows, and rollback triggers in the run sheet. These limits must come from the measured 64 baseline and the trading workflow's maximum acceptable quote-history age; the isolated synthetic data cannot set them. The run sheet must identify the deployed build, feed source/profile, contract set, asset capacities, Scylla keyspace, PostgreSQL database, start/end times, and owner of the acceptance decision.

## Failure response and rollback

Revert the affected asset capacity to 64 and restart or reset the feed generation if pending quote age breaches the agreed limit, publisher or Scylla backlog grows without recovery, row counts diverge, write p99 enters the observed slow band, or GC pauses disrupt live processing. Drain in-flight NATS and database work before retiring owned buffers. Preserve the recorded quote and feed-health diagnostics for investigation. The rollback needs no Scylla schema or NATS message change; it changes only the active aggregation capacity and the generation's private buffer reservations.

## Open findings

- The 112 versus old 64 allocation difference is promising but confounded by the decoder/CQL pooling change; the matched live baseline is still required.
- The prepared-write latency boundary has been measured only with local Scylla and synthetic quote values; its cause has not been isolated.
- The 112 soak did not measure first-quote dwell time or actual live contract-rate distribution.
- An overnight run is valuable for quiet-buffer age and long-term memory behavior. It does not replace an active-session run for throughput, encoded payload variance, and p95/p99 write behavior.
- The current startup gate blocks live Databento 112 and the current normal publisher bounds message count rather than retained quote items/bytes. These are implementation requirements, not configuration-only switches.
