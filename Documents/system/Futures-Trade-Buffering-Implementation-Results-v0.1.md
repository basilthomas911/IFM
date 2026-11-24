# Futures Trade Buffering: Component Results and Rollout Blockers

**Historical experiment:** The delivery blockers below were subsequently addressed by the [durable delivery and individual-row implementation](Futures-Trade-Buffering-Implementation-Results-v0.2.md). The measurements on this page describe the earlier array prototype.

**Date:** November 23, 2026  
**Status:** Partial implementation. Live ingestion has not been switched or restarted for this work.

## Completed implementation

- Versioned MessagePack raw-trade batch and pooled, bounded trade segments, up to 256 records.
- Binary CQL encoder and decoder retaining original event/receive nanoseconds, decimal/raw price, volume, source flags, identities, causation, acceptance ordinal and timestamps. No JSON conversion is used.
- Exact-length encoded buffers capped at 64,000 bytes. The maximum pooled trade-struct backing array also remains below the normal LOH threshold; the unit suite checks its actual struct size.
- Additive Scylla UDT, immutable chunk and batch-manifest tables; prepared statements and explicit binary-layout validation.
- One-second source-time chunks partitioned by exact contract, trading date and absolute UTC hour. Late arrivals create additional immutable chunks.
- Idempotent manifest/chunk identity checks and partial-write retry. Buffers remain owned until the actual driver request completes, including cancellation.
- Prototype half-open nanosecond range reader with deterministic event-time/acceptance ordering. The existing public legacy readers have not been cut over; this prototype materializes the requested result and is not the planned streaming adapter.
- Scylla raw-trade UDT serializer honoring each item's offset and length. The driver's default unmapped UDT serializer returns the entire underlying buffer, including unrelated/padded bytes. Returning a different CLR slice type is incompatible with this driver's cached collection element type, so the prototype uses small, bounded per-item copies. It is configured explicitly in the benchmark and fixture, not globally enabled in the live API.

## BenchmarkDotNet results

One invocation writes or reads **64 source trades**. These are direct storage-component timings, not end-to-end feed, actor or workflow latency.

.NET 10.0.10, BenchmarkDotNet 0.15.8, Release, in-process/no-emit, concurrent Workstation GC, three warmups and eight measurement iterations, 64 invocations per iteration. Every benchmark uses its own disposable keyspace on the local Scylla node. Other development processes share the host; write timing variance is substantial.

| Method | Mean | Standard deviation | Allocated per invocation |
| --- | ---: | ---: | ---: |
| Current individual writes | 80.388 ms | 12.657 ms | 1,109.90 KiB |
| Individual rows in one same-partition unlogged batch | 4.295 ms | 0.297 ms | 594.86 KiB |
| Array + conditional manifest/chunk writes | 68.683 ms | 17.494 ms | 49.36 KiB |
| Legacy narrow five-field read | 2.011 ms | 0.264 ms | 39.33 KiB |
| Legacy full-record read/materialization | 3.156 ms | 0.147 ms | 117.24 KiB |
| Array full-record read/materialization | 4.166 ms | 0.252 ms | 88.40 KiB |

The unlogged individual-row batch was about **18.7 times faster** than sequential writes in this run, with approximately **46% less allocation**. It preserves the existing schema and reads. Its acknowledgement alone does not provide immutable batch-conflict detection or durable source recovery.

The array implementation allocated approximately **95.6% less** than sequential writes, but the conditional manifest and chunk writes were costly. Its 99.9% timing intervals overlap the individual-write baseline, so the measured mean difference is not sufficient evidence of a reliable latency improvement. Full-record reads were approximately **32% slower**, though they allocated approximately **25% less**. The narrow five-field read does less work and is not an equivalent full-record comparator.

No Gen 2 collections were recorded in these measured BenchmarkDotNet intervals. That is not a live API Gen 2 reduction claim. Buffer bounds were verified; a complete mixed trade/quote LOH allocation trace and closing-burst recovery replay remain outstanding.

The initial before-only baseline was 67.310 ms / 1,109.92 KiB for 64 individual writes, and 1.866 ms / 39.41 KiB for the narrow read. The paired table above is the preferred comparison. Earlier prototype runs used a reader subsequently replaced after multi-chunk correctness checks; their read timings are not the final result.

Reports and logs:

- `.artifacts/trade-buffering-20261123/qualified-bdn/results/TomasAI.IFM.Application.Storage.Benchmarks.TradeBufferScyllaBenchmarks-report-github.md`
- `.artifacts/trade-buffering-20261123/qualified-bdn.log`
- `.artifacts/trade-buffering-20261123/baseline-bdn/`
- `.artifacts/trade-buffering-20261123/qualified-build.log`

Reproduce the paired run with the Release storage benchmark assembly and `--trade-buffer --filter '*TradeBufferScyllaBenchmarks*' --artifacts <output-directory>`. It requires local Scylla on port 9042 and creates/drops only its uniquely named `ifm_trade_buffer_bdn_…` keyspace.

## Verification

- **12/12 storage unit tests passed**, including seven new buffer cases: 1/32/64/128/256-record binary and CQL round trips, decimal extremes, UTF-8 metadata, corrupt/truncated payload rejection, byte bounds and slice bounds.
- **Disposable Scylla integration fixture passed.** It ran against an isolated Scylla container and verified fresh creation; upgrade from a populated legacy table; schema reapplication; original fields; exact nanosecond endpoints; out-of-order timestamps; tied timestamps; multiple source seconds and an hour boundary; empty ranges; late arrival; repeated delivery; changed-content rejection; partial-chunk loss and retry with a freshly prepared store; and cancellation before submission. The partial-write test removes a chunk in the disposable fixture and verifies that retry repairs it without duplicating records. This is not yet native process-crash recovery or a mixed legacy/new reader test.
- **3/3 existing native output tests passed.** These confirm the upstream retention behavior described below, rather than proving loss-free delivery.
- Release builds passed without warnings/errors. Existing application changes from earlier work were retained.

Logs: `unit-tests.log`, `scylla-qualified-tests.log`, `native-output-tests.log` under the same artifact directory. The disposable integration containers were removed by the fixture.

## Blockers to activation

### Native delivery can discard raw history before a durable receiver

The current native futures quote FIFO keeps 8,192 observations and evicts older ones when full. The existing `futures_quote_overflow_evicts_only_oldest_quotes_and_preserves_controls` test submits 20,000 quotes and retains the latest 8,192: **11,808 earlier quote observations are removed** in that test. It does not show that this exact count was lost live.

The native delivery pump also discards waiting frames when their physical generation no longer matches, and its publisher reports a Core NATS protocol flush rather than JetStream persistence or Scylla completion. Trade/control records have protected queue capacity, but there is no complete durable raw trade/quote journal bridging source acceptance, physical generation replacement and broker acknowledgement. The existing ES ITI journal lacks the full raw trade fields and quote history needed to replace it.

Implement durable raw capture before eviction/generation retirement, stable pending identities and a durable acknowledgement cursor. Recover pending accepted data after native/API/broker restart, and fail visibly when a gap cannot be recovered. Then connect the dedicated storage consumer and disable the legacy forwarding path at a verified cutover. Downstream buffering alone cannot qualify this guarantee.

### Physical array format has not passed performance qualification

Do not select this conditional-array implementation for rollout on these measurements. The same-partition individual-row batch is the faster measured write candidate, whereas the array saves more managed memory. Resolve the durability/immutability approach and remeasure before choosing the storage representation. The current prototype is useful for those comparisons but is not a completed trade-buffering rollout.

## Outstanding plan stages

Native count/byte/deadline assembler and quiet-feed flushes; producer journal and dedicated durable stream/consumer; outage/restart/generation recovery; bounded streaming legacy/new reader adapter and cutover; EOD cumulative-state coalescing; mixed trade/quote closing-burst replay, ITI/OHLCV equality, LOH trace and capacity matrix; then feature-gated development activation and live verification.
