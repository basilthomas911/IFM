# Futures Trade Buffering Implementation Plan

**Updated:** November 23, 2026  
**Implementation:** Durable native capture and bounded individual-row batching.  
**Results:** [Durable delivery and row-batching results](Futures-Trade-Buffering-Implementation-Results-v0.2.md).

## Selected design

The user selected the faster same-partition individual-row batch after the initial array experiment. Keep `tick_trade_data`, `tick_quote_data` and the existing public readers. The conditional array/manifest implementation remains an offline comparison; it is not the live format and needs no reader cutover.

The original delivery blocker was upstream of Scylla: the display queue could evict quotes, a generation replacement could discard pending display frames, and Core NATS flush did not prove durable storage. Capture raw futures records before those boundaries.

## Delivery and ownership

1. The Rust source captures each registered, non-replay futures trade and quote into a checksummed source journal. Group-commit at 64 observations or a 10 ms pulse before processing that group through the hot book. Keep each original trade identity, causation, timestamp, source sequence, raw price and size. Consecutive quotes retain the existing bounded array representation, at most 64 items.
2. Keep source files across physical generations and process restarts. Reserve monotonic row identities durably. Display queue overflow and generation fencing can no longer remove the independent archive copy.
3. A NativeHost publisher reads bounded groups from the journal and publishes them to a dedicated file-backed JetStream stream. Commit an atomic checksummed source cursor only after durable broker acknowledgement. Retain unresolved records; delete only fully handed-off retired/sealed segments.
4. The API uses independent storage workers, with explicit broker acknowledgement only after all Scylla requests for that delivery complete. Lost acknowledgements/redelivery reuse the original keys. Storage recovery retries are outside workflow decisions.
5. Group prepared individual-row inserts into bounded unlogged batches for each existing table/asset/contract partition. Preserve quote CQL buffer ownership through actual driver completion, including cancellation. Existing aggregation-key millisecond precision and separate original timestamp fields remain compatible.
6. Coalesce EOD work independently: retain cumulative high/low and the last authentic trade plus latest official statistics, publish changed views at a one-second cadence and a five-second heartbeat. Do not emit an EOD write for every archived tick.

## Bounds and failure behavior

- Native pending observations: 64; group-commit pulse: 10 ms.
- Quote arrays: at most 64 items; journal payload: at most 16 KiB.
- Journal segments: approximately 16 MiB; source spool capacity: 2 GiB, checked on rotation. Capacity failure stops advancement rather than evicting history.
- Broker messages: at most 64 records and 64,000 encoded bytes; journal read byte budget is 56,000 bytes before envelope overhead.
- File-backed stream: 8 GiB, reject new publication when full, no expiry, no Core NATS fallback.
- API storage concurrency: four workers; 16 outstanding broker acknowledgements maximum.
- Checksums, source ordinals, cursor ownership and retired partial tails fail closed. The API queries the source publisher's health as well as broker/consumer status; a broken source cursor cannot look healthy because the latest Scylla write succeeded.
- Durability starts at committed native admission. This does not promise recovery of provider network bytes never committed locally. Power loss or a damaged tail blocks visibly; it is not silently skipped.

## Completed implementation stages

| Stage | Implemented result | Verification |
| --- | --- | --- |
| Baseline and physical format | Compared individual requests, same-partition row batches and conditional arrays; selected row batches | Initial and final BenchmarkDotNet reports |
| Native capture and batching | Pre-queue journal, count/pulse flush, bounded quote arrays, original trade identity reuse | Native suite, overflow/generation/owner-restart and abrupt-process-exit tests |
| Durable delivery | NativeHost journal publisher, file-backed JetStream, durable cursor, API consumer | Real native-to-C# journal decoding; broker restart; lost-ack and post-commit redelivery |
| Scylla writer | Prepared bounded individual-row batches and retained pooled quote owners | Disposable Scylla readback; unique trade/quote counts and original field equality |
| Reader compatibility | Retained existing tables, keys and readers | Legacy time-window reads, millisecond endpoints, ordering and exact source timestamps |
| EOD isolation | Bounded cumulative trade/statistics views | High/low/last-price and original identity/timestamp tests |
| Failure/performance qualification | Damaged journal/cursor blocks; no accepted test records lost; production writer benchmark | 123 native tests; 38 targeted C# tests; current BenchmarkDotNet report |
| Development activation | Enabled in the VS Code API + Rust eGui + Rust MarketData profile | Restart and live readiness/counter evidence in results |

## Operational follow-up

The implementation and synthetic failure qualification do not substitute for observing the next cash-close burst. Record queue pressure, broker backlog, source spool growth and reconciliation at that close. The earlier full closing quote tape is unavailable, so the 20,000-observation mixed stress fixture is explicitly synthetic. Do not label it an exact historical feed replay.

Existing ITI classifications, thresholds, trade eligibility and exit rules are outside this change. Source history persistence does not wait for optional EOD/display mailbox admission; strategy decisions do not query Scylla or retry storage.

## Evidence

- Current: `.artifacts/trade-buffering-durable-20261123/`.
- Previous component experiment: `.artifacts/trade-buffering-20261123/` and [v0.1 results](Futures-Trade-Buffering-Implementation-Results-v0.1.md).
- Original closing-pressure and indicator prerequisite evidence: `.artifacts/daily-indicators-20261123-rollover/`.
