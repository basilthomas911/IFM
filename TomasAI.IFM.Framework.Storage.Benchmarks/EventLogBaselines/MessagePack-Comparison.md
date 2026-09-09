# Event-log JSON and MessagePack comparison

Follow-up: the [binary-only application cutover](../../TomasAI.IFM.Application.Storage/EventSourceDb/BinaryCutover.md) is now implemented. The measurements below remain the original pre-cutover runs.

Measured on 2026-09-09 using the same frozen 20-event corpus as the original JSON baseline. The original fixtures and JSON measurements are unchanged.

Corpus SHA-256: `c0978204799bab73ee3679acb489f368cf302f316f016d4b8ca4579b6d241ac3`.

## Largest workflow snapshot (E20)

| Measurement | JSON | MessagePack | MessagePack + LZ4 |
|---|---:|---:|---:|
| Serialize (ms) | 32.415 | 4.974 | 4.182 |
| Deserialize (ms) | 52.255 | 18.457 | 20.365 |
| RoundTrip (ms) | 72.680 | 24.442 | 20.541 |
| Payload (bytes) | 3,557,103 | 1,062,416 | 209,184 |
| Serialize allocations (MiB) | 16.382 | 5.250 | 1.009 |
| Deserialize allocations (MiB) | 13.196 | 6.598 | 6.598 |
| RoundTrip allocations (MiB) | 29.588 | 11.848 | 7.607 |

Uncompressed MessagePack deserialization was **2.83x faster**, serialization **6.52x faster**, and round trip **2.97x faster** for E20. Payload size fell by **70.1%**, or **94.1% with LZ4**. Deserialization allocations fell by approximately **50%** in both binary modes.

## Payload sizes for all 20 events

| ID | Event | JSON bytes | MessagePack bytes | LZ4 bytes |
|---|---|---:|---:|---:|
| E01 | YieldCurveRateRemovedEvent | 1,103 | 199 | 180 |
| E02 | YieldCurveRatesImportedEvent | 1,169 | 221 | 202 |
| E03 | StreamingRequestIdDeletedEvent | 1,242 | 240 | 220 |
| E04 | FuturesBarDataDeletedEvent | 1,321 | 264 | 247 |
| E05 | LookupTypeAddedEvent | 1,367 | 288 | 226 |
| E06 | OptionTradeSpreadBarDataInsertedEvent | 1,427 | 252 | 236 |
| E07 | FuturesMacdSignalStoppedEvent | 1,511 | 270 | 242 |
| E08 | FuturesContractAddedEvent | 1,596 | 307 | 265 |
| E09 | FuturesContractChangedEvent | 1,744 | 320 | 260 |
| E10 | FuturesRsiDailySignalGeneratedEvent | 1,882 | 390 | 295 |
| E11 | StrategyWorkflowStartRejectedEvent | 1,922 | 524 | 387 |
| E12 | FuturesOptionTickDataStreamingStartedEvent | 2,135 | 456 | 366 |
| E13 | StrategyWorkflowRiskManagementResultRecordedEvent | 2,279 | 689 | 572 |
| E14 | DatabaseOperationStartedEvent | 2,666 | 542 | 329 |
| E15 | FuturesVwapSignalUpdatedEvent | 2,811 | 662 | 426 |
| E16 | DatabaseBackupExecutionRequestedDomainEvent | 3,217 | 743 | 411 |
| E17 | FuturesAtrSignalGeneratedEvent | 4,648 | 1,310 | 1,109 |
| E18 | FuturesRsiDailySignalsGeneratedEvent | 9,821 | 1,680 | 287 |
| E19 | MarketConditionAssessmentCompletedEvent | 21,059 | 5,287 | 2,460 |
| E20 | WorkflowStrategyStateUpdatedEvent | 3,557,103 | 1,062,416 | 209,184 |

## Serialize: median microseconds per operation

| ID | JSON | MessagePack | LZ4 |
|---|---:|---:|---:|
| E01 | 180.00 | 2.09 | 5.07 |
| E02 | 177.04 | 0.57 | 1.14 |
| E03 | 172.57 | 0.58 | 1.23 |
| E04 | 6.82 | 0.70 | 1.42 |
| E05 | 175.46 | 0.81 | 1.63 |
| E06 | 176.60 | 0.83 | 1.66 |
| E07 | 175.04 | 0.61 | 1.38 |
| E08 | 175.55 | 0.90 | 1.71 |
| E09 | 178.18 | 1.01 | 1.76 |
| E10 | 186.79 | 1.10 | 2.07 |
| E11 | 174.62 | 0.97 | 1.90 |
| E12 | 181.27 | 1.38 | 2.23 |
| E13 | 188.66 | 1.25 | 2.45 |
| E14 | 13.44 | 1.50 | 2.34 |
| E15 | 13.29 | 2.18 | 3.30 |
| E16 | 14.55 | 2.21 | 2.89 |
| E17 | 227.44 | 5.08 | 7.44 |
| E18 | 230.99 | 7.39 | 8.27 |
| E19 | 265.82 | 18.04 | 21.22 |
| E20 | 32415.40 | 4974.06 | 4182.01 |

## Deserialize: median microseconds per operation

| ID | JSON | MessagePack | LZ4 |
|---|---:|---:|---:|
| E01 | 15.07 | 10.54 | 13.85 |
| E02 | 11.98 | 3.49 | 3.88 |
| E03 | 12.40 | 3.50 | 3.90 |
| E04 | 13.46 | 3.87 | 4.34 |
| E05 | 13.49 | 3.85 | 4.15 |
| E06 | 14.88 | 3.85 | 4.40 |
| E07 | 15.04 | 3.71 | 4.06 |
| E08 | 16.03 | 4.11 | 4.56 |
| E09 | 18.38 | 4.18 | 4.64 |
| E10 | 19.98 | 4.69 | 5.22 |
| E11 | 17.19 | 4.60 | 5.07 |
| E12 | 20.21 | 5.18 | 5.66 |
| E13 | 19.46 | 5.53 | 6.14 |
| E14 | 28.04 | 5.98 | 6.42 |
| E15 | 25.39 | 6.08 | 6.66 |
| E16 | 32.45 | 6.65 | 7.53 |
| E17 | 48.59 | 9.47 | 10.15 |
| E18 | 100.62 | 15.15 | 15.59 |
| E19 | 257.69 | 108.66 | 104.02 |
| E20 | 52254.70 | 18456.88 | 20364.88 |

## RoundTrip: median microseconds per operation

| ID | JSON | MessagePack | LZ4 |
|---|---:|---:|---:|
| E01 | 201.87 | 3.98 | 5.17 |
| E02 | 198.97 | 4.18 | 5.38 |
| E03 | 204.33 | 4.23 | 5.72 |
| E04 | 22.30 | 4.81 | 6.20 |
| E05 | 200.38 | 4.86 | 6.23 |
| E06 | 206.34 | 4.98 | 6.54 |
| E07 | 199.85 | 4.47 | 6.08 |
| E08 | 209.88 | 5.24 | 6.69 |
| E09 | 210.98 | 5.39 | 6.93 |
| E10 | 221.27 | 5.94 | 7.69 |
| E11 | 208.57 | 6.09 | 7.48 |
| E12 | 224.18 | 7.08 | 8.63 |
| E13 | 215.71 | 7.35 | 9.34 |
| E14 | 45.05 | 8.27 | 9.80 |
| E15 | 43.45 | 8.69 | 11.13 |
| E16 | 53.69 | 9.24 | 11.55 |
| E17 | 271.47 | 15.99 | 19.49 |
| E18 | 343.64 | 23.27 | 24.76 |
| E19 | 533.06 | 113.96 | 122.13 |
| E20 | 72679.50 | 24441.83 | 20541.00 |

## Method and correctness

- Each mode measured all 20 events across Serialize, Deserialize and RoundTrip: 60 measurements per mode. The custom harness uses a 300 ms warmup per case/operation, adaptive batches targeting 50 ms (maximum 8,192 operations), and nine measured batches. Tables show medians; raw results retain samples, means, standard deviations and thread allocations.
- All runs used .NET 10.0.10, Windows x64, 32 reported processors and workstation GC. MessagePack assembly version is 3.1.8. These are warm codec measurements, not BenchmarkDotNet reports, database timings or end-to-end workflow measurements. Separate runs remain sensitive to system load, JIT and GC; independently measured medians need not add up.
- JSON uses the existing ToEventData / ToDomainEvent paths. Binary decoding includes runtime concrete-type lookup and restoration of the authoritative EventId from the event-log version, matching the JSON reader.
- The binary envelope is [version=1, originalAggregateIdWasNull, concreteMessagePackEvent]. Direct native decoding changed AggregateId from null to an empty string in 11 fixture types because of their serialization constructors. The envelope restores this distinction without changing domain classes or fixtures. Envelope cost is included in every binary measurement.
- Both binary modes passed exact concrete-type and JSON-projected semantic checks for all 20 events. Only the two pre-existing computed diagnostic values in E20 (State.CompositionDispatch.OriginatedOn and OriginatedBy) are excluded from value equality; their presence is required. No further exclusions were introduced.
- Each mode also passed 80 malformed-payload rejection checks: empty, truncated, appended trailing data, and unsupported envelope version for every event. Total: 160 rejection checks.

## Artifacts and delivery boundary

- [Original JSON baseline](v1/README.md) and [raw JSON measurements](v1/results.json).
- [MessagePack measurements](messagepack-v1/results.json), [CSV](messagepack-v1/results.csv), [comparison against JSON](messagepack-v1/comparison.csv).
- [LZ4 measurements](messagepack-lz4-v1/results.json), [CSV](messagepack-lz4-v1/results.csv), [comparison against JSON](messagepack-lz4-v1/comparison.csv).

The MessagePack candidate is implemented in the benchmark project. The results support a binary codec performance benefit on this corpus. Application event-log writers/readers and the database schema have not been switched, and no event-log records have been deleted. A subsequent binary-only cutover must cover every append/read path and verify replay and recovery before treating these codec savings as workflow latency savings.
