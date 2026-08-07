# DataBento futures tick aggregation implementation details

Status: V1 implementation complete for the approved futures scope on 2026-08-07.

The implementation follows [Databento-Futures-Tick-Aggregation-Specification-v1.md](Databento-Futures-Tick-Aggregation-Specification-v1.md), revision 1.8. The application-layer `IMarketDataApi` remains intentionally out of scope and will later become the sole lifecycle owner of one `ITickAggregationService`.

## Implemented pipeline

```text
DataBento native ring
  -> existing managed per-InstrumentKey bounded channels
  -> exclusive multiplexed zero-copy batch reader
  -> TickAggregationService
     -> per-ticker capacity-64 pooled quote buffer
     -> quote-before-trade sequence ordering
  -> bounded single-reader TickAggregationEventPublisher
  -> supervisor-owned synthetic JetStream producer
  -> TickAggregationEventActor changed-event handlers
  -> TickAggregationCommandActor insert commands
  -> ActorEventSourceDb inserted events
  -> TickAggregationEventActor projection handlers
  -> tick_trade_data / tick_quote_data in ScyllaDB
```

Every accepted trade emits one trade event. Quotes are isolated by ticker and flush before that ticker's trade, at 64 records, at value-date rollover, and during graceful stop. Raw 64-bit prices and exact decimal prices are retained. `SequenceId` is shared across trade and quote events for one contract/trading date, and aggregation timestamps are generated in UTC.

`ITickAggregationMetricsSource` provides a lock-free snapshot of source and emission counts, full/partial flushes, sequence gaps, duplicates, out-of-order records, publication failures, active tickers, and service-owned quote buffers. Source anomalies are measured without dropping or reordering their records.

## Top ten implementation issues addressed

| Rank | Issue | Implemented resolution |
|---:|---|---|
| 1 | A consumer per ticker would require thread/task fan-out and scale poorly | Added one exclusive signal-driven round-robin multiplexed reader over existing bounded per-ticker channels; `MarketDataBatch64` ownership remains zero-copy |
| 2 | Quote-per-event publication would amplify JetStream, actor, and Scylla overhead | Added bounded capacity-64 per-ticker quote aggregation with mandatory pre-trade flush |
| 3 | Slicing pooled arrays with `Take(...).ToArray()` allocated and copied on every partial flush | Added `FuturesTickQuoteDataSegment` and a custom MessagePack formatter that serializes only the active prefix |
| 4 | Ambiguous array ownership could leak, double-return, or lose rejected quote data | Added explicit once-only leases and pending event identity; ownership and sequence transfer only after bounded-channel acceptance, while rejection retains the same buffer and IDs for retry |
| 5 | Concurrent producer calls could reorder quote-before-trade messages | Added one bounded, single-reader publisher loop and one cached `IJSActorProducer` |
| 6 | A fake actor/mailbox for service publication would violate actor lifecycle semantics | Added concurrency-safe `IActorSupervisor.GetJSEventProducer` using a synthetic event producer identifier with collision protection and supervisor shutdown ownership |
| 7 | Contract/instrument identity without publisher and asset type was insufficient | Added `TickContractMapping` and a Blackboard-backed mapping path keyed by dataset, definition date, publisher, and instrument |
| 8 | Direct changed-event storage would bypass validation and durable actor facts | Changed events create validated insert commands; exact retries use a bounded command fingerprint and indexed inserted-event lookup before immutable event persistence |
| 9 | One Scylla row per quote would make quote row count and write amplification excessive | Added one frozen bounded UDT list per quote aggregation row and one trade row per trade |
| 10 | Date buckets would force multi-query range orchestration | Partitioned by `(asset_type_id, contract_id)` and clustered first by `value_date`, then UTC `aggregation_time` and sequence, enabling one no-filtering date-range statement |

## Storage

The MarketData keyspace now manages:

- UDT `tick_quote_item`;
- table `tick_trade_data`;
- table `tick_quote_data`.

Legacy tick tables remain unchanged and receive no V1 writes. Quote UDT mapping is resolved lazily against the active Scylla session, then cached per session. Both inserts are prepared, idempotent primary-key upserts. The integration test executes the required date-range query with partition-key equality and clustering-date bounds, without `ALLOW FILTERING`.

## BenchmarkDotNet results

BenchmarkDotNet 0.15.8 ran on .NET 10.0.10, Windows 10 22H2, AMD Ryzen Threadripper 1950X (16 physical/32 logical cores), x64 RyuJIT, concurrent workstation GC. The short job used three warmups and six measured iterations.

The baseline copies the active prefix with LINQ `Take(...).ToArray()` before MessagePack serialization. The implemented path serializes `FuturesTickQuoteDataSegment` directly from the pooled backing array.

| Quotes | Before mean | After mean | Latency reduction | Before allocation | After allocation | Allocation reduction |
|---:|---:|---:|---:|---:|---:|---:|
| 8 | 1.773 us | 1.637 us | 7.7% | 1,488 B | 416 B | 72.0% |
| 64 | 13.676 us | 12.967 us | 5.2% | 10,608 B | 3,264 B | 69.2% |

Raw artifacts are generated under `BenchmarkDotNet.Artifacts` and intentionally ignored. Reproduce the cases with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Feed.Benchmarks -- --filter "*TickAggregationBenchmarks*" --job short --warmupCount 3 --iterationCount 6
```

## Validation evidence

- MarketData Feed unit tests: 563 passed.
- DataBento unit tests: 68 passed.
- MarketData Feed BDD tests: 353 passed.
- Tick aggregation persistence integration tests: 3 passed, covering trade/quote UDT-list Scylla persistence, date-range reads, indexed actor-event command lookup, and conflict-safe command-audit insertion.
- Fund integration regression suite: 26 passed.
- Contract coverage includes active-prefix serialization, concrete complete/fail event round trips, completion-event type safety, quote-before-trade ordering, shared sequencing, raw-to-decimal conversion, signal-driven multiplexing, completed-command deduplication, and retry-safe pooled ownership.

## Deliberately deferred

- Application-layer `IMarketDataApi` definition, DI registration, and ownership of `ITickAggregationService`.
- Durable same-`ValueDate` sequence recovery across ActorEventSourceDb and both Scylla tables. Fresh-stream operation is implemented; same-day production restart remains gated until the system-wide recovery design is implemented.
- Solution-wide cancellation propagation through supervisor, actor, repository, and storage layers.
- Futures-option, equity, and equity-option message families.
- Tick query actors and application-facing query APIs.
- Credentialed live DataBento soak/paper-trading validation and production telemetry thresholds.
