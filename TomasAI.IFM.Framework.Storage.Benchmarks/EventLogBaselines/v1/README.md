# Event-log serialization baseline v1

Twenty distinct production event types captured from the local synthetic integration-test event store. The 20 checksummed fixture files are frozen for future comparisons. No production serialization code was changed.

Measured 2026-09-09T19:56:08.9990402Z using .NET 10.0.10 on Microsoft Windows 10.0.19045 (X64). Server GC: False. Logical processors: 32.

The runner uses the existing `ToEventData()` JSON writer and `EventStreamReadModel.ToDomainEvent()` reader used by Application.Storage. Database access, disk I/O, workflow cloning and actor transport are excluded from timed operations. UTF-8 sizes describe the frozen JSON files; the writer returns a .NET UTF-16 string.

Each event/operation has 300 ms of warmup, adaptive power-of-two batches targeting at least 50 ms (up to 8,192 operations), and nine measured batch samples. The table reports median batch-average microseconds per operation. Raw samples, mean, sample standard deviation, allocated bytes per operation, runtime, codec identity and corpus hash are retained in results.json and results.csv. These are warmed desktop baselines, not cold-start or individual-operation tail-latency percentiles. Different operations are measured separately; round-trip medians need not equal the sum of serialize and deserialize medians.

| ID | Event type | UTF-8 bytes | Serialize us | Deserialize us | Round trip us | Decode allocated KiB/op |
|---|---|---:|---:|---:|---:|---:|
| E01 | YieldCurveRateRemovedEvent | 1,103 | 180.00 | 15.07 | 201.87 | 7.27 |
| E02 | YieldCurveRatesImportedEvent | 1,169 | 177.04 | 11.98 | 198.97 | 7.34 |
| E03 | StreamingRequestIdDeletedEvent | 1,242 | 172.57 | 12.40 | 204.33 | 7.41 |
| E04 | FuturesBarDataDeletedEvent | 1,321 | 6.82 | 13.46 | 22.30 | 7.70 |
| E05 | LookupTypeAddedEvent | 1,367 | 175.46 | 13.49 | 200.38 | 7.53 |
| E06 | OptionTradeSpreadBarDataInsertedEvent | 1,427 | 176.60 | 14.88 | 206.34 | 7.66 |
| E07 | FuturesMacdSignalStoppedEvent | 1,511 | 175.04 | 15.04 | 199.85 | 7.93 |
| E08 | FuturesContractAddedEvent | 1,596 | 175.55 | 16.03 | 209.88 | 8.52 |
| E09 | FuturesContractChangedEvent | 1,744 | 178.18 | 18.38 | 210.98 | 9.34 |
| E10 | FuturesRsiDailySignalGeneratedEvent | 1,882 | 186.79 | 19.98 | 221.27 | 8.38 |
| E11 | StrategyWorkflowStartRejectedEvent | 1,922 | 174.62 | 17.19 | 208.57 | 8.92 |
| E12 | FuturesOptionTickDataStreamingStartedEvent | 2,135 | 181.27 | 20.21 | 224.18 | 8.97 |
| E13 | StrategyWorkflowRiskManagementResultRecordedEvent | 2,279 | 188.66 | 19.46 | 215.71 | 9.52 |
| E14 | DatabaseOperationStartedEvent | 2,666 | 13.44 | 28.04 | 45.05 | 8.24 |
| E15 | FuturesVwapSignalUpdatedEvent | 2,811 | 13.29 | 25.39 | 43.45 | 7.64 |
| E16 | DatabaseBackupExecutionRequestedDomainEvent | 3,217 | 14.55 | 32.45 | 53.69 | 9.42 |
| E17 | FuturesAtrSignalGeneratedEvent | 4,648 | 227.44 | 48.59 | 271.47 | 13.93 |
| E18 | FuturesRsiDailySignalsGeneratedEvent | 9,821 | 230.99 | 100.62 | 343.64 | 17.53 |
| E19 | MarketConditionAssessmentCompletedEvent | 21,059 | 265.82 | 257.69 | 533.06 | 96.92 |
| E20 | WorkflowStrategyStateUpdatedEvent | 3,557,103 | 32415.40 | 52254.70 | 72679.50 | 13512.56 |

This is an empirical event-type mix, not uniformly spaced synthetic size buckets: most events are small, with collection payloads and the large workflow snapshot exercising larger structures. Keep this mix fixed when evaluating an optimization.

Correctness: all 20 events must deserialize to their exact production type and pass semantic JSON round-trip comparison. E20 has two explicitly listed computed diagnostic getters (`State.CompositionDispatch.OriginatedOn` and `.OriginatedBy`); their values depend on the current clock/machine and are excluded from semantic value equality, but their presence is required. No other field is excluded. Raw fixture checksums cover every byte, including these diagnostic values.

Verified: 20 distinct event types; 60 event/operation results; all corpus checksums; semantic round trips; self-comparison produces 1.0000 time/allocation ratios for every row. Different-corpus comparison is rejected. The baseline has not been optimized.
