# Detailed Composer, projection and authorization timings

Measured 2026-09-09. Successful Daily/LongFuture binary event-log workflow: **7.343 seconds**, Approved Risk / Authorized intent. No order submitted.

TraceId: `9b1015dac88a3e6249b4cb7236e7fc49`. WorkflowId: `01a087f686837394b82800ca03553370`.

## Projection: 12 committed workflow revisions

| Operation | Calls | Total ms |
|---|---:|---:|
| workflow.project.lock_wait | 12 | 0.074 |
| workflow.project.timeline_serialize | 12 | 63.952 |
| workflow.project.state_serialize | 12 | 54.048 |
| workflow.project.timeline_write | 12 | 329.637 |
| workflow.project.detail_write | 12 | 315.919 |
| workflow.project.active_write | 11 | 305.188 |
| workflow.project.entity_write | 12 | 20.641 |
| workflow.project.status_write | 12 | 18.381 |
| workflow.project.start_attempt_write | 1 | 5.493 |
| workflow.project.active_delete | 1 | 4.368 |
| workflow.project.notify | 12 | 66.241 |

Total projection duration: **1192.868 ms**. The three large-payload write paths account for **950.744 ms (79.7%)**. Serialization accounts for **118.000 ms**. The entity and status index writes together cost **39.022 ms**. Lock contention was negligible in this run.

The timeline stores the event snapshot; detail and active projections store the workflow payload. Payloads grow from roughly 35 KB to 881 KB, with a large increase around Composer preparation acceptance. Detail and active writes reuse the same serialized bytes but persist them in separate tables. Write spans include the storage API, parameter binding, driver/network and database completion; they are not server-only query timings.

## Composer

| Operation | Calls | Total ms |
|---|---:|---:|
| composer.prepare_or_dispatch | 2 | 1450.969 |
| composer.preparation.read | 1 | 206.457 |
| composer.preparation.validate | 1 | 0.324 |
| composer.acceptance.send | 1 | 469.535 |
| composer.accept.current_view | 1 | 3.989 |
| composer.acceptance | 1 | 341.479 |
| composer.accept.preparation_read | 1 | 10.443 |
| composer.accept.validate_and_build | 1 | 79.322 |
| composer.accept.create_execution | 1 | 210.020 |
| composer.accept.state_update | 1 | 41.586 |
| composer.function_dispatch | 1 | 682.991 |

The 206.457-ms initial preparation read uses the controlled workflow market fixture. On a miss that adapter loads workflow state, resolves bindings, constructs synthetic market evidence and commits it to the preparation store. It is **not a 206-ms database SELECT**. The separate production `composer.market.prepare` path was instrumented but not exercised by this fixture. The acceptance handler subsequently reads the saved preparation directly in 10.443 ms.

`composer.accept.create_execution` includes binding resolution, request construction, fingerprinting and validation in `CompositionDispatch.Create`. This is the largest measured local Composer acceptance substep (210.020 ms); those internal components are not yet separately timed.

The 469.535-ms acceptance send includes the receiving command: 20.85 ms state loading, 3.99 ms CurrentView copying, 341.48 ms acceptance work and 78.99 ms saving. The acceptance substeps are nested inside that 341.48 ms, so do not add them again. Function dispatch is likewise an inclusive request/response orchestration interval, not pure Composer calculation.

## Risk preparation

| Operation | Calls | Total ms |
|---|---:|---:|
| risk.prepare | 1 | 119.809 |
| risk.prepare.current_view | 1 | 13.904 |
| risk.prepare.candidate_decode | 1 | 0.045 |
| risk.prepare.policy_read | 1 | 7.683 |
| risk.prepare.admission_read | 1 | 0.934 |
| risk.prepare.build_request | 1 | 66.631 |
| risk.prepare.state_update | 1 | 27.410 |
| risk.dispatch.current_view | 5 | 84.661 |
| risk.calculate | 1 | 31.877 |

## Financial authorization

| Operation | Calls | Total ms |
|---|---:|---:|
| risk.financial_handoff | 3 | 766.356 |
| authorization.reserve_call | 1 | 9.364 |
| authorization.prior_receipt_read | 1 | 0.648 |
| authorization.fund_authorize_call | 1 | 2.445 |
| authorization.build_advance | 3 | 1.686 |
| authorization.advance_send | 3 | 751.691 |
| authorization.verify.current_view | 3 | 70.365 |
| risk.verify_handoff | 3 | 134.300 |
| authorization.verify.order_read | 1 | 0.193 |
| authorization.verify.admission_read | 1 | 0.029 |
| authorization.verify.reservation_receipt | 1 | 0.749 |
| authorization.verify.fund_receipt | 1 | 0.060 |
| authorization.verify.build_order | 1 | 7.491 |
| authorization.verify.state_update | 3 | 100.824 |

`EventActorContext.SendAsync` calls the producer RequestAsync and awaits its result. The three advance-send spans therefore measure remote command processing and acknowledgement, not just message transmission. Of their 751.691 ms, 737.38 ms overlaps traced receiving actor processing. Inside those waits: state loads 241.11 ms, CurrentView copies 70.37 ms, verification handlers 134.30 ms (including 100.82 ms state updates), and snapshot saves 256.41 ms. Other actor/transport work accounts for the remainder.

These breakdowns use causal ancestry **and intersection with the caller time window**. Later asynchronous descendants can outlive the original send; they are excluded rather than incorrectly adding the rest of the workflow to that send duration.

The reservation, authorization and authority/receipt APIs are controlled Portfolio fixture adapters. These results do not benchmark production Portfolio network/database latency. They show that local financial API execution does not explain this test workflow's long handoff time.

## Conclusions and next targets

1. Prioritize the three full-payload projection write paths. They dominate measured projection time; serialization and index-only writes are materially smaller. Investigate payload duplication and whether independent projection writes can safely overlap without changing notification-after-projection ordering.
2. Profile Composer invocation construction internally before changing it: binding resolution, fingerprinting and repeated validation are grouped in the measured 210-ms span. Keep the 206-ms fixture preparation result separate from production market capture latency.
3. Reduce repeated state reconstruction/copying and persistence overhead in authorization command handling. Making the controlled 9-ms reserve or 2-ms Fund authorization call faster would barely affect this run. Preserve receipt verification, optimistic concurrency, immutability and durable checkpoints.

## Validation and limits

- Integration trace test passed, including Approved/Authorized assertions, single W3C TraceId, required detailed-span coverage and positive serialized-payload byte tags.
- 45 focused Composer preparation, Risk preparation and financial/Fund handoff regression tests passed with normal tracing configuration.
- Integration project build passed with zero warnings/errors. No business behavior or latency qualification limits changed; changes add observational spans only.
- Single standalone development observation, not a warmed percentile benchmark or causal proof of production latency. The synthetic market and controlled Portfolio boundaries still apply. Span times are elapsed, not CPU time; parent totals contain child totals.
- Raw evidence: `TestResults/full-workflow-binary/detailed-binary-workflow.trx`, `.json`, `detailed-binary-workflow-operations.csv`, and `detailed-timing-regression.trx`.
