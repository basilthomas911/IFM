# Rust.eGui performance results

Date: 2026-10-31. Implementation and verification complete for the recorded scope.

The implementation covers 15 changes or focused pilots. Four items were already
satisfied by the monitor design; additional chart sampling is deferred because
the existing six-hour/minute aggregation bounds the spread chart to 361 points.
The [implementation plan](Rust-eGui-Performance-Implementation-Plan-2026-10-31.md)
records all 20 dispositions and the precise scope of each change.

## Measured stages

These are medians of five alternating comparisons against commit
`1be8410ce4ce2bbc4f740e4370649b6e581d95b1`. Units are milliseconds per operation.

| Operation | Before | After | Speedup |
| --- | ---: | ---: | ---: |
| Prepare option grid and 30 visible rows, 100 contracts | 0.191658 | 0.111505 | 1.72× |
| Prepare option grid and 30 visible rows, 1,000 contracts | 1.601119 | 0.518402 | 3.09× |
| Prepare option grid and 30 visible rows, 10,000 contracts | 15.675420 | 5.208606 | 3.01× |
| Select history, 100 snapshots | 0.164287 | 0.005280 | 31.11× |
| Select history, 1,000 snapshots | 1.992005 | 0.060860 | 32.73× |
| Select history, 10,000 snapshots | 28.440555 | 0.855278 | 33.25× |
| Prepare visible output slices, 100 lines | 0.003945 | 0.000087 | 45.39× |
| Prepare visible output slices, 10,000 lines | 0.344521 | 0.000087 | 3,946× |
| Prepare visible output slices, 100,000 lines | 4.065374 | 0.000089 | 45,536× |
| Decode option chain, 100 contracts | 0.460543 | 0.437481 | 1.05× |
| Decode option chain, 1,000 contracts | 5.007280 | 4.256053 | 1.18× |
| Decode option chain, 10,000 contracts | 56.679937 | 44.335357 | 1.28× |
| Publish and obtain one native history-plan display | 0.016895 | 0.002715 | 6.22× |
| Merge chain, 100 contracts | 0.094330 | 0.109594 | 0.86× |
| Merge chain, 1,000 contracts | 1.706243 | 1.294793 | 1.32× |
| Merge chain, 10,000 contracts | 89.633760 | 19.570017 | 4.58× |
| Obtain unchanged 1,000-contract market section | 2.565290 | 0.000015 | Cached lookup |
| Two-worker service timer lateness during large decodes | 43.592500 | 11.466600 | 3.80× |
| Diagnostic producer, per record in a 64-record batch | 0.335861 | 0.000322 | Queue offer |

Unchanged output/market operations now read prepared data. Their very large ratios
describe eliminated preparation, not application-wide gains. Initial indexes,
wire storage and prepared records still have a cost. Diagnostic queue-offer time
excludes the owned worker's flush and startup; raw results include both separately.
The timer test uses actual 10,000-contract decodes on a synthetic two-worker
runtime, rather than live trading traffic.

The 100-contract indexed merge costs about 15 microseconds more. The index has a
fixed construction/lookup cost, while the old repeated search scales quadratically.
The larger-chain reductions justify retaining one predictable indexed path; this
small-input regression is explicitly retained in the results.

## Overall fixture results

Portfolio publication fields match in all five comparisons. One-fund publication
p50 is 0.2665 → 0.2701 ms; 100-fund p50 is 10.6165 → 10.6558 ms. This is effectively
unchanged, as expected for a monitor-focused native-publication pilot.

| Sparse plans | Preparation p50 before/after | Pointer p95 before/after | First prepared frame before/after |
| ---: | ---: | ---: | ---: |
| 100 | 0.1263 / 0.0827 | 3.8246 / 5.0488 | 3.1172 / 3.3572 |
| 1,000 | 1.0151 / 0.5918 | 3.6583 / 3.7747 | 3.5800 / 3.2954 |
| 10,000 | 10.7489 / 5.7987 | 4.2830 / 4.2113 | 4.6368 / 3.9745 |

The second executable in every initial pair was slower for the 100-plan p95,
irrespective of which version ran second. A separate ten-pair frame-only follow-up
balanced BA/AB order and allowed one second between executable launches, using the
same preserved binaries and unchanged harness. Its medians are:

| Sparse plans | Preparation p50 before/after | Pointer p50 before/after | Pointer p95 before/after | First prepared frame before/after |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 0.1207 / 0.0785 | 3.0867 / 3.2201 | 3.7044 / 3.6356 | 3.0897 / 3.1912 |
| 1,000 | 0.9566 / 0.5518 | 3.0003 / 2.9848 | 3.6145 / 3.3904 | 3.2598 / 3.0364 |
| 10,000 | 10.6780 / 5.5524 | 3.3845 / 3.7023 | 4.0637 / 4.1110 | 4.4312 / 3.9763 |

The initial 100-plan p95 regression did not reproduce. Follow-up frame p95 and
first prepared frame medians have no greater-than-5% regression at any tested
size. Pointer p50 remains mixed, including a 9.4% increase at 10,000 sparse plans;
no general pointer-frame speedup is claimed. The preparation reduction is
consistent across both comparisons. This follow-up establishes sensitivity to
launch order/measurement variability, not a proven operating-system root cause.
Both datasets are retained rather than replacing the initial unfavorable result.

## Evidence and reproduction

Local raw evidence is under `.artifacts/egui-performance-plan-20261031/`.
`performance-plan-final.json` records the 19 stage cases, frame and portfolio
medians, all five raw stage values, source/fixture/configuration/executable hashes,
compiler and baseline manifest. Original binaries and source archive remain intact.
`frame-investigation/summary.json` retains all ten follow-up pairs, medians and
executable hashes; `investigate-frames.ps1` reproduces that follow-up locally.

Run from the repository root:

```powershell
powershell -NoProfile -File TomasAI.IFM.UI.Rust.eGui/tools/measure_performance_plan.ps1
powershell -NoProfile -File scripts/UI/Rust/Verify-StandaloneRust.ps1
python TomasAI.IFM.UI.Rust.eGui/tools/generate_typed_trade_contracts.py --check
python TomasAI.IFM.UI.Rust.eGui/tools/audit_native_ownership.py
```

Stage measurements use repeated batches of at least 50 ms and five warmups, with
AB/BA order alternation. The original frame harness measures 60 pointer frames and
five publication-preparation/first-frame samples per case. These are development
profile CPU fixture measurements, not Criterion confidence intervals, GPU/window
presentation timings, allocation counts, retained-heap measurements or live
broker/network latency. Weak-ownership and cancellation tests check lifecycle
release and bounded concurrency; they do not establish a process-RSS improvement.

## Verification

The final standard verification script exited successfully:

- 315 native tests passed across the library and 80 integration-test executables.
  The native run's 70 ignored cases include the separately executed protocol tests
  and opt-in timing/visual/environment-specific cases.
- All 55 isolated backend NATS tests and the console-intake test passed.
- The unchanged managed stores reopened both native financial journals and all
  234 resulting operation journals.
- The managed fixture tool built with zero warnings/errors and exported 50
  fixtures and 2,707 compiled schemas. Regenerated fixture hashes still match
  the measured inputs.
- The all-target check, 109-test focused gate (including desktop shutdown), nine
  optimized parity tests, contract generation checks and ownership audit passed.
  The audit maps 297 C# source files and verifies 79 protected original hashes.

The full log is `final-compatibility.log` in the artifact directory. The isolated
responder and NATS container were cleaned up by the runner. Changes are uncommitted.
