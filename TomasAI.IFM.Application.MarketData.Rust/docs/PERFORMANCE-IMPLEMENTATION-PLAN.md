# Rust Market Data Host performance implementation plan

The scope includes the Development Market Data Host executable, its managed NATS and persistence adapters, and the native Rust runtime. The improvement numbers match the original 24-item audit. The first implementation batch is dispatcher reuse, batched reference reads, and shared seed replay. The November 1 benchmark results provide historical context. Capture a fresh baseline against the current Development source before changing runtime behavior.

## Measurement and acceptance

- Freeze the baseline revision and working-tree changes. Use the same recorded inputs, contract universe, consumer counts, machine, database contents, and build profile on both sides. Keep full source debugging in Development. Run optimized builds separately and label those results by profile.
- Run at least five alternating before/after pairs per scenario after warm-up. Keep individual observations and report sample counts, p50/p95/p99, and the median paired change. Measure cold starts separately from warm requests. Distinguish measured record latency from throughput-derived time per operation.
- Use isolated NATS and test databases for replay, overload, crash, and fault injection. Use existing captured data or authorized zero-cost acquisition. Run a final Development smoke test on the configured Rust Host once the isolated checks pass.
- Measure elapsed time, CPU seconds per million records, allocation count and bytes, peak private memory, and backlog or drop counts. Capture end-to-end timings for request admission through reply, source event through publication, restart through qualified indicator availability, and recovery through resumed pricing. Record provider and storage wait separately from processing time.
- Preserve public wire compatibility, numeric results and ranking tie-breaks, event ordering, quote qualification, generation fences, bounded memory and deadlines, and durable acknowledgements. An optimization qualifies when the intended metric improves repeatably and the paired runs show no material regression in other relevant metrics. A change with no repeatable gain stays out of the deployment.

## Phase 1. Dispatcher, reference reads, and seed replay

| ID | Implementation | Required tests and before/after evidence |
|---|---|---|
| 1 | Reuse an immutable Host operation table and a reusable dispatcher. Precompute method and parameter metadata. Benchmark typed delegates for the busiest RPCs against the reusable reflection baseline before adding them. | Exercise every declared operation and overload through the existing NATS contract. Cover signature mismatch, malformed argument, cancellation, lifecycle restrictions, and retry identity. Measure allocations and CPU per RPC plus p50/p95/p99 under mixed observation and control traffic. |
| 2 | Replace per-expiry definition reads and serial convention lookups with set-based PostgreSQL reads. Cache immutable results by dataset, definition generation, canonical ID, and version as applicable. Retain pagination and result ordering. | Compare old and new results over multiple expiries and versions, including missing rows, duplicate inputs, and concurrent version activation. Verify cache isolation and invalidation. Record SQL calls, returned rows, bytes, query plans, and cold/warm Trade Selection and recovery latency. |
| 4 | Plan 15-second and one-minute seed windows together. Share the overlapping original-trade read and feed separate session-aligned accumulators. Reuse stored bars first and share only compatible missing ranges. Keep independent completion, deadlines, and fallback for each timeframe. | Compare OHLCV, trade count, price-volume sum, timestamps, and provenance against separate replays. Cover maintenance gaps, missing intervals, late and duplicate trades, cancellation of one waiter, and a restart. Measure raw reads, provider calls, ticks decoded, startup CPU, and time to warm ATR/RSI/ADX/MACD/TDI. |

Finish the first three changes before broadening the replay cache. Live qualification must show both short frames seeded from genuine completed intervals, one-minute ATR(14) warm with its baseline, and a fresh Market Condition assessment free of the cold-ATR 24026 failure.

## Phase 2. Replay retention, option selection, and recovery

| ID | Implementation | Required tests and before/after evidence |
|---|---|---|
| 3 | Extend the four-hour shared acquisition and qualified-bar cache to the short and intermediate timeframes. Key coverage by contract, timeframe, interval, and source version. Read only missing intervals. | Test overlapping requests, partial coverage, a contract roll, provider failure, and restart reuse. Compare cold and warm startup, provider calls, and cache hits. |
| 5 | Track pricing work by changed option quote, underlying quote, and reference revision. Schedule expiry and freshness transitions even when no new quote arrives. Keep separate risk and selection budgets. | Compare full sweeps with incremental pricing for identical input sequences. Include stale quotes, reference changes, reconnects, and monitored-leg priority. Measure solves per second, CPU, timer delay, and time to the first qualified price. |
| 6 | Route capture wake-ups by requested instruments and scope. Preserve subscribe-before-read ordering and retirement notifications. | Test relevant and unrelated changes, lost wake-up races, cancellation, and deadline expiry. Measure attempts per capture and p99 under concurrent workflows. |
| 7 | Compare effective subscription membership before replacing resources. Reuse healthy resources across compatible scope changes and fence work when the provider requires a generation replacement. | Exercise no-op refreshes, one-scope changes, disconnects, and genuine generation changes. Check subscription counts, qualified-price interruption, and recovery time. |
| 8 | Build reusable strike indexes by expiry, underlying, and option right. Replace full sorts with bounded selection where result order and tie-breaks match. | Compare browser windows and composition candidates against the existing oracle, including equal distances, fractional strikes, and missing legs. Measure p95/p99 and allocation at representative and largest recorded chain sizes. |

## Phase 3. Interop, RPC, and live publication

| ID | Implementation | Required tests and before/after evidence |
|---|---|---|
| 9 | Replace profiled JSON and generic MessagePack trees at selected internal boundaries with typed layouts. Keep compatibility adapters for existing external messages. | Run ABI and wire parity with legacy and malformed payloads, exact decimals, and maximum sizes. Measure encode/decode CPU and allocations. |
| 10 | Batch native catalog and definition reads and reuse bounded buffers with explicit ownership. | Test partial batches, cancellation, concurrent callers, oversized inputs, and buffer return on failure. Measure crossings, bytes copied, and p99. |
| 11 | Reduce RPC envelope copies and completed-operation memo allocations. Retain payload identity, deduplication, and stop-lane capacity. | Test concurrent duplicates, conflicting payloads, capacity saturation, and emergency-stop progress. Measure allocation, retained memory, and latency with a full memo. |
| 12 | Tune publication batch sizes, flush cadence, and queue bounds by traffic class. | Replay quiet, normal, and burst traffic with a slow consumer. Compare throughput, quote age, queue depth, and ordered trade delivery. |
| 13 | Separate independent publication backlogs where the existing stream-order and receipt rules permit it. | Stall one stream and verify the other continues, then reconnect and drain. Measure per-stream lag and recovery under backpressure. |
| 14 | Reuse immutable encoded frames across consumers and tune compression by measured payload size. | Prove byte or decoded-wire parity as required, including mixed consumer versions. Compare CPU, allocation, and wire bytes across payload sizes. |
| 15 | Aggregate repetitive counters and sample routine diagnostics at their source. Keep errors, transitions, and required audit detail. | Replay an error burst and verify diagnostic evidence and sequence correlation survive. Compare logging CPU, allocations, and I/O under load. |

## Phase 4. Durable writes, pricing state, and historical isolation

| ID | Implementation | Required tests and before/after evidence |
|---|---|---|
| 16 | Batch evidence writes and receipts within compatible storage partitions or transactions. Acknowledge each record only after its durable write. | Inject partial failure, retries, duplicates, and restart. Compare write calls, throughput, and acknowledgement p99. |
| 17 | Store parsed timestamps and bounded pending-record indexes so retry selection can avoid parsing full payloads. | Verify existing outbox records reopen and retain identical retry order, including malformed and orphaned records. Compare recovery scan time and memory. |
| 18 | Evaluate bounded grouped audit commits using the same durability contract. Keep the existing path if crash testing cannot prove equivalent recovery. | Kill between append, flush, receipt, and cleanup. Prove recovery and acknowledgement behavior. Measure disk flushes, throughput, and receipt tail latency. |
| 19 | Track reference expiry and revision changes so pricing can skip the full validity scan on each pass. | Verify validity transitions at the exact boundary, including quiet markets, clock movement, and a refreshed or retired generation. Compare scan work, lock time, and pass latency. |
| 20 | Reuse pricing scratch buffers and shorten lock hold times. Consider sharding only if contention remains measurable. | Stress concurrent readers and writers, cancellation, and retirement. Check result parity and bound retained memory. Compare allocations and lock-wait p99. |
| 21 | Tune the existing pricing workers and batch budgets under mixed ingestion and capture load. Profile American IV and Greeks separately before considering changes to numerical kernels. | Compare numerical outputs and convergence cases with existing pricing oracles. Measure pricing throughput, timer p99, and publication lag together. |
| 22 | Give bulk historical jobs separate concurrency, I/O, and memory budgets. Preserve cancellation and progress reporting. | Run live capture alongside large history jobs and cancel an active job. Compare live p99, history throughput, and peak memory. |
| 23 | Persist qualified replay coverage and resume only incomplete ranges after a restart. Reuse the cache rules from items 3 and 4. | Crash mid-range and test partial records, duplicates, and source-version changes. Compare resumed bytes, provider calls, and restart time. |
| 24 | Remove profiled per-record copies from the managed replay reader by passing bounded batches with explicit lifetimes. | Verify native/managed parity, ownership on cancellation, and clean end-of-stream behavior. Compare allocations, bytes copied, and records per second. |

## Test and rollout sequence

1. Run the Rust native unit, ABI, wire, pricing, cache, and lifecycle tests for each affected component. Extend `examples/performance.rs` and the existing qualification benchmarks where their workloads match.
2. Run the managed Host, Market Data, and startup integration suites relevant to each phase. Use `NativeIndicatorSeedTests` and the stored-session warm-up tests for short-frame replay. Include PostgreSQL and NATS integration tests for batching, ordering, and compatibility, plus Scylla coverage for replay reads where applicable.
3. Run end-to-end replay, concurrent Trade Selection and option capture, slow-consumer, and recovery scenarios on the packaged Host. Report before and after for each item or inseparable batch. Compare the final package with the original baseline as well.
4. Deploy accepted changes to Development in batches with a rollback artifact. Verify futures publication, Options readiness and saved-leg pricing, short-frame historical seeding, and a fresh strategy assessment. Record the results and any deferred changes or unverified scenarios.
