# Production binary event-log timing comparison

Binary run: 2026-09-09T20:40:06.1511383Z. JSON baseline: 2026-09-09T19:56:08.9990402Z.

The same 20 frozen events were measured with the production uncompressed MessagePack codec after the binary-only cutover. Original JSON results and fixtures were preserved. This run includes the null AggregateId envelope, ActorEntityId formatter and ITI nested metadata preservation changes.

Corpus SHA-256: `c0978204799bab73ee3679acb489f368cf302f316f016d4b8ca4579b6d241ac3`.

Percentage change = `(binary / JSON - 1) * 100`. **Negative means less time, allocation or payload size.** Timings are median microseconds per operation unless stated otherwise.

## Largest workflow snapshot (E20)

| Operation | JSON ms | Binary ms | Time change | Speedup |
|---|---:|---:|---:|---:|
| Serialize | 32.415 | 6.400 | -80.26% | 5.07x |
| Deserialize | 52.255 | 19.108 | -63.43% | 2.73x |
| RoundTrip | 72.680 | 28.160 | -61.25% | 2.58x |

## Percentage changes for all 20 events

| ID | Event | Serialize | Deserialize | Round trip |
|---|---|---:|---:|---:|
| E01 | YieldCurveRateRemovedEvent | -98.39% | -26.81% | -98.07% |
| E02 | YieldCurveRatesImportedEvent | -99.64% | -71.03% | -97.86% |
| E03 | StreamingRequestIdDeletedEvent | -99.63% | -72.05% | -97.89% |
| E04 | FuturesBarDataDeletedEvent | -87.82% | -71.69% | -78.04% |
| E05 | LookupTypeAddedEvent | -99.53% | -71.43% | -97.62% |
| E06 | OptionTradeSpreadBarDataInsertedEvent | -99.44% | -74.38% | -97.52% |
| E07 | FuturesMacdSignalStoppedEvent | -99.58% | -75.75% | -97.75% |
| E08 | FuturesContractAddedEvent | -99.40% | -74.23% | -97.37% |
| E09 | FuturesContractChangedEvent | -99.32% | -76.70% | -97.28% |
| E10 | FuturesRsiDailySignalGeneratedEvent | -99.33% | -76.60% | -97.08% |
| E11 | StrategyWorkflowStartRejectedEvent | -99.27% | -74.50% | -97.18% |
| E12 | FuturesOptionTickDataStreamingStartedEvent | -99.14% | -73.82% | -96.65% |
| E13 | StrategyWorkflowRiskManagementResultRecordedEvent | -99.08% | -71.43% | -96.37% |
| E14 | DatabaseOperationStartedEvent | -85.80% | -78.48% | -81.13% |
| E15 | FuturesVwapSignalUpdatedEvent | -82.36% | -75.66% | -78.33% |
| E16 | DatabaseBackupExecutionRequestedDomainEvent | -83.28% | -79.05% | -81.43% |
| E17 | FuturesAtrSignalGeneratedEvent | -97.38% | -78.78% | -93.15% |
| E18 | FuturesRsiDailySignalsGeneratedEvent | -96.24% | -83.65% | -92.46% |
| E19 | MarketConditionAssessmentCompletedEvent | -92.12% | -59.66% | -77.40% |
| E20 | WorkflowStrategyStateUpdatedEvent | -80.26% | -63.43% | -61.25% |

## Serialize: detailed timings

| ID | JSON us | Binary us | Time change | Allocation change |
|---|---:|---:|---:|---:|
| E01 | 180.003 | 2.901 | -98.39% | -93.83% |
| E02 | 177.039 | 0.646 | -99.64% | -87.54% |
| E03 | 172.573 | 0.640 | -99.63% | -87.61% |
| E04 | 6.824 | 0.831 | -87.82% | -87.39% |
| E05 | 175.462 | 0.830 | -99.53% | -87.42% |
| E06 | 176.598 | 0.981 | -99.44% | -88.41% |
| E07 | 175.037 | 0.735 | -99.58% | -88.31% |
| E08 | 175.553 | 1.050 | -99.40% | -87.98% |
| E09 | 178.179 | 1.208 | -99.32% | -88.36% |
| E10 | 186.786 | 1.244 | -99.33% | -88.74% |
| E11 | 174.615 | 1.267 | -99.27% | -78.22% |
| E12 | 181.270 | 1.562 | -99.14% | -84.68% |
| E13 | 188.656 | 1.727 | -99.08% | -83.65% |
| E14 | 13.443 | 1.908 | -85.80% | -85.57% |
| E15 | 13.292 | 2.345 | -82.36% | -85.59% |
| E16 | 14.554 | 2.434 | -83.28% | -85.73% |
| E17 | 227.439 | 5.951 | -97.38% | -84.21% |
| E18 | 230.990 | 8.674 | -96.24% | -91.70% |
| E19 | 265.815 | 20.950 | -92.12% | -73.45% |
| E20 | 32415.400 | 6399.525 | -80.26% | -67.95% |

## Deserialize: detailed timings

| ID | JSON us | Binary us | Time change | Allocation change |
|---|---:|---:|---:|---:|
| E01 | 15.065 | 11.027 | -26.81% | -82.17% |
| E02 | 11.980 | 3.471 | -71.03% | -81.79% |
| E03 | 12.400 | 3.466 | -72.05% | -80.82% |
| E04 | 13.458 | 3.809 | -71.69% | -79.29% |
| E05 | 13.487 | 3.853 | -71.43% | -78.74% |
| E06 | 14.880 | 3.813 | -74.38% | -81.23% |
| E07 | 15.041 | 3.647 | -75.75% | -80.79% |
| E08 | 16.031 | 4.132 | -74.23% | -78.53% |
| E09 | 18.379 | 4.282 | -76.70% | -79.08% |
| E10 | 19.981 | 4.676 | -76.60% | -78.08% |
| E11 | 17.187 | 4.383 | -74.50% | -80.48% |
| E12 | 20.215 | 5.292 | -73.82% | -71.78% |
| E13 | 19.457 | 5.559 | -71.43% | -73.51% |
| E14 | 28.039 | 6.033 | -78.48% | -73.74% |
| E15 | 25.387 | 6.179 | -75.66% | -67.18% |
| E16 | 32.452 | 6.799 | -79.05% | -72.05% |
| E17 | 48.585 | 10.309 | -78.78% | -80.70% |
| E18 | 100.624 | 16.447 | -83.65% | -69.61% |
| E19 | 257.689 | 103.948 | -59.66% | -50.19% |
| E20 | 52254.700 | 19108.425 | -63.43% | -50.00% |

## RoundTrip: detailed timings

| ID | JSON us | Binary us | Time change | Allocation change |
|---|---:|---:|---:|---:|
| E01 | 201.873 | 3.897 | -98.07% | -88.48% |
| E02 | 198.966 | 4.249 | -97.86% | -84.94% |
| E03 | 204.326 | 4.315 | -97.89% | -84.55% |
| E04 | 22.305 | 4.898 | -78.04% | -83.66% |
| E05 | 200.381 | 4.764 | -97.62% | -83.51% |
| E06 | 206.343 | 5.124 | -97.52% | -85.24% |
| E07 | 199.845 | 4.493 | -97.75% | -84.94% |
| E08 | 209.881 | 5.511 | -97.37% | -83.59% |
| E09 | 210.980 | 5.746 | -97.28% | -83.92% |
| E10 | 221.273 | 6.469 | -97.08% | -84.16% |
| E11 | 208.573 | 5.887 | -97.18% | -79.29% |
| E12 | 224.181 | 7.500 | -96.65% | -79.92% |
| E13 | 215.709 | 7.834 | -96.37% | -79.90% |
| E14 | 45.053 | 8.503 | -81.13% | -81.70% |
| E15 | 43.448 | 9.413 | -78.33% | -80.04% |
| E16 | 53.690 | 9.968 | -81.43% | -81.11% |
| E17 | 271.466 | 18.605 | -93.15% | -83.18% |
| E18 | 343.639 | 25.913 | -92.46% | -87.11% |
| E19 | 533.064 | 120.454 | -77.40% | -62.35% |
| E20 | 72679.500 | 28160.250 | -61.25% | -59.96% |

## Payload sizes

| ID | JSON bytes | Binary bytes | Change |
|---|---:|---:|---:|
| E01 | 1,103 | 199 | -81.96% |
| E02 | 1,169 | 221 | -81.09% |
| E03 | 1,242 | 240 | -80.68% |
| E04 | 1,321 | 264 | -80.02% |
| E05 | 1,367 | 288 | -78.93% |
| E06 | 1,427 | 252 | -82.34% |
| E07 | 1,511 | 270 | -82.13% |
| E08 | 1,596 | 307 | -80.76% |
| E09 | 1,744 | 320 | -81.65% |
| E10 | 1,882 | 390 | -79.28% |
| E11 | 1,922 | 524 | -72.74% |
| E12 | 2,135 | 456 | -78.64% |
| E13 | 2,279 | 689 | -69.77% |
| E14 | 2,666 | 542 | -79.67% |
| E15 | 2,811 | 662 | -76.45% |
| E16 | 3,217 | 743 | -76.90% |
| E17 | 4,648 | 1,310 | -71.82% |
| E18 | 9,821 | 1,680 | -82.89% |
| E19 | 21,059 | 5,287 | -74.89% |
| E20 | 3,557,103 | 1,062,416 | -70.13% |

## Method and limits

- Same machine/runtime configuration: .NET 10.0.10, Windows x64, 32 reported processors, workstation GC. Runtime/configuration fields and all 60 case/operation keys were checked before comparison.
- Same custom warmed harness: 300 ms per-case/operation warmup, calibrated batches targeting 50 ms (maximum 8,192 operations), nine measured batches. The raw files retain mean, sample standard deviation, allocations and every measured sample.
- All 20 fixture hashes, concrete event types and semantic round trips passed before timing. Only the two documented computed diagnostic values in E20 are excluded from value equality.
- These are separate-run codec timings against the saved JSON baseline, not a controlled simultaneous A/B test, BenchmarkDotNet statistical report, cold-start test or end-to-end database/workflow measurement. System load, JIT and GC can affect differences. Independently measured medians need not add up.
- Per-event percentages are shown rather than presenting a workload-wide percentage without an event-frequency distribution. The actual JSON and MessagePack event-log encodings have different representations and treatment of computed diagnostics; these are application-path results, not a universal serializer speed claim.

## Raw evidence

- [Original JSON baseline](../v1/results.json)
- [Production binary results and samples](results.json)
- [Production binary results CSV](results.csv)
- [Percentage comparison CSV](percentage-comparison.csv)
- [Compiled assemblies and codec source fingerprints](provenance.json)
