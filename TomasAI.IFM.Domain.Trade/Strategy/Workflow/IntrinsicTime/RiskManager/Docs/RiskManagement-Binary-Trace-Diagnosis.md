# Binary strategy workflow latency diagnosis

Successful standalone traced Daily/LongFuture run: **7.479 seconds**, Approved Risk / Authorized intent.

TraceId: `852976ebfab075887806b52145b92168`. WorkflowId: `01a087ed781673eabebf4b64eec79bdd`.

Evidence: `TestResults/full-workflow-binary/binary-workflow-trace.trx`, `binary-workflow-trace.json`, and `binary-workflow-profile-summary.json`. The trace-continuity assertion passed with one W3C TraceId.

## Same-run elapsed phases

| Phase | Seconds | Share of elapsed time |
|---|---:|---:|
| Regime Discovery | 0.812 | 10.9% |
| Market Assessment | 0.460 | 6.2% |
| Trade Selection | 0.578 | 7.7% |
| Selection reservation / Composer handoff | 0.296 | 4.0% |
| Order Composer preparation and execution | 1.878 | 25.1% |
| Risk preparation and result handling | 1.253 | 16.8% |
| Post-Risk financial authorization | 1.519 | 20.3% |
| Admission, final observation/read and other boundary overhead | 0.682 | 9.1% |

These phase windows use domain stage timestamps; they include orchestration around calculation and are not pure CPU measurements. The final residual includes admission before the workflow Started timestamp and observation/final state loading after terminal authorization. The phases are disjoint and reconcile to the stopwatch total. The three Composer/Risk/authorization phases account for approximately 62% of this run.

## Repeated work inside these phases

| Operation | Calls | Inclusive total |
|---|---:|---:|
| workflow.state.load | 24 | 1301.09 ms |
| workflow.state.deserialize | 23 | 552.38 ms |
| workflow.state.apply | 23 | 559.67 ms |
| workflow.state.save | 12 | 913.83 ms |
| workflow.project | 12 | 1196.55 ms |
| risk.financial_handoff | 3 | 769.37 ms |
| risk.prepare | 1 | 132.78 ms |
| risk.verify_handoff | 3 | 153.50 ms |
| risk.calculate | 1 | 30.16 ms |
| risk.fund_composition | 1 | 5.69 ms |

These are inclusive span totals, not additional time to add to the phase table. Decode and application/copying are inside state loading; financial handoff and actor spans contain other work and transport waits. The load/save/project intervals were checked for overlap: their union occupies approximately 3.411 seconds of this trace (about 46% of observed elapsed time). State loading includes the fixture final verification read and a preparation-fixture read. Every nonempty load read one snapshot; this is not replaying the complete event history.

## Function actor processing

| Pipeline Function actor | Processing duration |
|---|---:|
| RegimeDiscoveryPipelineFunction | 240.42 ms |
| MarketConditionPipelineFunction | 197.89 ms |
| TradeSelectionPipelineFunction | 341.39 ms |
| OrderCompositionPipelineFunction | 325.37 ms |
| RiskManagementPipelineFunction | 137.75 ms |

These five Function handlers total 1242.82 ms and execute sequentially. They include Function execution overhead and any work inside those handlers; only Risk has a separately instrumented calculator in this trace. Risk calculation itself took 30.16 ms, approximately 0.4% of total elapsed time.

## Interpretation from the trace and code

1. Five successful pipeline stages still cause twelve durable workflow revisions, twelve PostgreSQL snapshot saves and twelve Scylla projection/notification cycles. Composer preparation, independent Risk preparation/result acceptance, reservation and Fund authorization require extra checkpoints. Success does not bypass them.
2. Snapshot loading is now 1.301 seconds cumulatively. Of that, 0.552 seconds is MessagePack decoding and 0.560 seconds is state application/copying. The remaining approximately 0.189 seconds includes database retrieval and other load setup; database I/O is not separately instrumented.
3. `IntrinsicTimeStrategyWorkflowCommandState.Apply` clones the view and builds a legacy view; `CurrentView` clones again on access. Some clone helpers themselves use MessagePack serialization/deserialization. Getter copies elsewhere are not isolated by the apply span, so 0.560 seconds is not an exhaustive copying cost.
4. Realtime dispatch rereads authoritative snapshots to reject stale notifications, including repeated reads in Composer/Risk paths. Those consistency checks explain many of the 24 loads; reuse would need explicit revision and ownership guarantees.
5. `ProjectAsync` sequentially writes timeline, start-attempt and current-workflow projections before notifying the next actor. Its twelve calls cost 1.197 seconds including locking, validation, projection work and notification; the current trace cannot identify which Scylla write dominates.
6. Financial authorization takes another 1.519 seconds after the Risk result timestamp. Three handoff/verification cycles move through initial, ReservePending and FundPending phases before Authorized. The Portfolio adapter is controlled in this fixture, so this is not a production Portfolio network-latency measurement.

## Next optimization priorities

- Reduce repeated reconstruction/copying by retaining an immutable decoded snapshot within an operation, avoiding repeated getter clones and eager legacy-view creation where unused. Preserve optimistic revision checks, ownership and stale-notification rejection.
- Add per-query/write and copy-helper spans around the remaining 1.197-second projection cost and the large Composer-preparation/dispatch windows before choosing a database optimization.
- Reduce duplicated large payloads carried through successive snapshots using immutable references where recovery can reconstruct them safely. Keep durable financial verification checkpoints.

The previous staggered full run was 6.204 seconds; this standalone traced run was 7.479 seconds. Separate-process warmup/JIT, instrumentation and system variability prevent treating their difference as a regression or claiming steady-state latency. No latency threshold was enforced. Synthetic market inputs and controlled Portfolio adapters remain as documented in the workflow qualification. No broker order was submitted. No runtime implementation was changed for this diagnosis.

Follow-up: [detailed Composer, individual projection write and authorization timings](RiskManagement-Detailed-Timings.md) add finer spans and identify the main costs within these sections.
