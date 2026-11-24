# Futures Trade Buffering: Durable Delivery and Individual-Row Batching

**Date:** November 23, 2026 (ET).  
**Implementation:** Enabled for the development API + Rust MarketData profile.  
**Physical format:** Existing individual trade rows and bounded frozen quote arrays; no live array-table cutover.

## What changed

Raw futures capture now happens in Rust before the replaceable display queue and before physical-generation fencing. The source writes a checksummed journal, group-commits at 64 observations or a 10 ms timer pulse, and only then advances that group through the hot book. Consecutive quotes retain their bounded 64-item array shape. Trade publication reuses the journaled event identity, causation, observation timestamp and row key; ITI's local ordered book sequence remains separate.

An independent NativeHost publisher sends bounded deliveries to the dedicated file-backed JetStream stream `IFM_RAW_FUTURES_development`. A durable broker acknowledgement precedes the atomic checksummed source cursor. Generation replacement and process restart do not delete pending history. Source segments disappear only after complete durable handoff. The API acknowledges deliveries only after Scylla confirms every table/partition group. Redelivery uses the original keys and is idempotent, including a lost completion after the database commit.

The selected writer uses prepared **unlogged batches of individual rows within each existing table/asset/contract partition**. Existing `tick_trade_data` and `tick_quote_data` readers continue working. Aggregation-time clustering keys retain the existing millisecond precision; original observation ticks and provider event/receive nanoseconds remain separate exact fields. Quote buffers remain owned until the actual driver request completes, including cancellation.

Raw persistence bypasses the optional tick-storage actor mailbox. The legacy per-trade and quote-storage forwarding is disabled under the feature flag. EOD observations use bounded cumulative high/low/last-trade views and latest official statistics, rather than amplifying every archived record into an EOD write. No storage retry or Scylla wait enters a strategy workflow decision.

## BenchmarkDotNet: current production writer

.NET 10.0.10, BenchmarkDotNet 0.15.8, Release, concurrent Workstation GC, in-process/no-emit, three warmups, eight measurement iterations, 32 invocations per iteration. Disposable keyspaces on the local Scylla node. The host also performed a development rebuild, so these are local comparative measurements, not service latency guarantees.

Each invocation includes fixture/event creation, MessagePack decoding, full persisted metadata binding, pooled quote CQL encoding and actual Scylla acknowledgement. Mixed cases contain alternating individual trade and quote records. Count is **records per invocation**, not contract volume; a production quote record can contain multiple quotes.

| Records | Input | Before: individual requests | After: grouped rows | Before allocation | After allocation |
| ---: | --- | ---: | ---: | ---: | ---: |
| 32 | Trades | 50.459 ms | 3.306 ms | 610.56 KiB | 354.55 KiB |
| 32 | Mixed trades/quotes | 36.835 ms | 4.423 ms | 579.32 KiB | 331.67 KiB |
| 64 | Trades | 102.120 ms | 5.080 ms | 1,220.20 KiB | 699.90 KiB |
| 64 | Mixed trades/quotes | 120.026 ms | 6.500 ms | 1,157.55 KiB | 645.74 KiB |

At 64 records this is approximately **20.1x faster for trades** and **18.5x for mixed records**, with **42.6% and 44.2% less managed allocation**, respectively. Baseline standard deviations were 15.99 ms and 13.58 ms; grouped deviations were 0.95 ms and 0.30 ms. Full uncertainty is in the generated report.

No Gen 2 collections were measured in these cases. Mixed 64-record Gen 1 collections fell from 62.5 to 31.25 per 1,000 operations. MemoryDiagnoser measures aggregate allocation and collections; this run does not supply a separate attributed live LOH allocation trace. Bounded journal/message/codec buffers prevent an ordinary message buffer from crossing the normal LOH threshold; total allocation per invocation includes many smaller objects.

These timings do **not** include native disk sync, broker delivery, buffer dwell or end-to-end source-to-Scylla tail latency. Native mixed capture plus real queue overflow was tested separately; the complete 123-test native suite finished in 2.38 seconds. That suite time is not a BenchmarkDotNet result or a throughput SLA.

Report: `.artifacts/trade-buffering-durable-20261123/bdn-final/results/TomasAI.IFM.Application.Storage.Benchmarks.RawFuturesRowBufferBenchmarks-report-github.md`.

The earlier conditional-array experiment remains benchmark/test code. Its schema definitions are additive and may create empty prototype tables, but live writes and reads do not use them. The row-batch choice supersedes the original array-table migration and streaming-reader cutover stages. See [v0.1 component results](Futures-Trade-Buffering-Implementation-Results-v0.1.md) for that comparison.

## Correctness and recovery

- **123 native tests passed**, zero failures; 12 deliberately ignored helpers/other opt-in cases. Tests include bounded 64-quote arrays, original trade/publication identity equality, mixed overflow and physical-generation changes, owner restart and an abrupt child-process exit after commit.
- The mixed native stress submits **20,000 observations**, intentionally overflows the real optional output queue, clears that queue to represent generation retirement, and recovers all **10,000 trades and 10,000 quotes** from checksummed journals. The C# interop check reads those real Rust-generated files and verifies original fields and durable cursor advancement. This is a synthetic stress tape, not the missing authentic closing quote tape.
- **38 targeted C# tests passed**, zero failures. The disposable Scylla + file-backed NATS fixture sends **10,000 mixed records**, loses a publication completion, restarts NATS with retained file data, injects failure after a real Scylla commit, restarts the API consumer and verifies **5,000 unique trades plus 5,000 quotes**. It checks original identities, command IDs, observation ticks, event/receive nanoseconds, quote prices/sizes/counts, total volume and existing window-reader compatibility.
- Existing-schema reapplication preserves populated rows. No legacy/new reader merge is necessary for the selected format.
- Retired partial records, invalid checksums, source ordinal gaps and corrupt source cursors are not skipped. A dedicated health test verifies a corrupt cursor retains its journal and makes API storage readiness unhealthy through the native publisher's health responder.
- EOD coalescer tests retain high, low, final authentic trade price, original event/command identity and original timestamp. Existing indicator seed/restart regression tests are included in the targeted suite.

Logs: `native-full-tests.log`, `native-export-final.log`, `recovery-tests-final.log`, and `benchmark-build-final.log` under `.artifacts/trade-buffering-durable-20261123/`. Disposable recovery containers are fixture-owned and removed after testing.

## Capacity and operational limits

Native files rotate at approximately 16 MiB with a 2 GiB source spool bound. The broker has an 8 GiB file-backed stream, no expiry and reject-new behavior. Saturation retains upstream history and blocks/reports failure; it does not silently evict old storage records or fall back to Core NATS. Uncommitted provider network input is outside the durability boundary. Damaged source history requires explicit repair and keeps readiness blocked.

The current development flag is `RawFuturesStorage.Enabled=true`, with 64-record deliveries and a 10 ms flush pulse. Options/equity storage is not switched by this futures change. The next cash-close burst remains an operational observation to perform; the after-hours activation and synthetic overflow tests cannot be presented as that future event.


## Development restart and live verification

The VS Code API + Rust eGui + Rust MarketData profile was rebuilt and restarted twice. The second restart loaded the final native quote batching and source-health responder. At **20:24:36 ET on November 23**, `/health/ready` returned **200**, NativeHost reported **Ready**, all **76 indicator entries** were ready, and the five open positions' EOD evidence remained healthy. The eGui process had its `IFM - eGui 0.1.0` window open.

While the API was stopped for the second rebuild, file-backed JetStream retained **29 pending messages**, through stream sequence **13,030**. After restart, the acknowledgement floor advanced to **15,506**, with **zero pending and zero unacknowledged messages** in that sample. This verifies that the retained pre-restart backlog drained; native/API counters are record completions and can include prior-process deliveries, not unique quote counts.

The final snapshot recorded **3,643 native published records** for the new process and **3,843 API stored-record completions**, including retained work. The optional `TickAggregationRealtime` actor and EOD realtime actor both had **zero queue depth and zero rejections**. Live Scylla samples confirmed current ES trade rows with original event/receive nanoseconds and identities, plus quote arrays containing multiple observations. This is after-hours live verification, not the next cash-close stress observation.

Evidence: `.artifacts/trade-buffering-durable-20261123/restart-recheck/verification-summary.json`, `api-health-final.json`, `native-health-final.json`, `jetstream-final.json`, `actor-health-final.json`, `live-trade-sample.txt`, `live-quote-sample.txt` and `profile-restart.log`. `jetstream-during-restart.json` in the parent evidence directory records the retained backlog before restart. `git diff --check` passed; private recovery containers and benchmark keyspaces were cleaned up.
