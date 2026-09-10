# Full Strategy Workflow to Risk integration qualification

The `FullWorkflowRisk` suite starts `ExecuteIntrinsicTimeStrategyWorkflowCommand` with an exact Regime profile, Market Assessment profile, and frozen selection authority. It never submits intermediate stage completions or calls calculation actors directly. The production workflow projector and realtime dispatcher drive all five Function actors over isolated NATS, using PostgreSQL event sourcing and Scylla projections/preparation storage.

## Wiring audit

| Boundary | Production implementation | Check |
|---|---|---|
| ITI event to workflow admission | `IntrinsicTimeStrategyWorkflowRealtimeActor.ExecuteWorkflowAsync` | Live routing is feature-gated; resolves effective Regime/Assessment profiles, an exact horizon activation, and Portfolio selection authority. Tests enter the resulting workflow-start command boundary. |
| Admission to Regime Discovery | `DispatchCommittedStateAsync`, `ExecuteRegimeDiscoveryAsync` | Dispatch follows a committed Started snapshot. |
| Regime to Market Assessment | `CompleteRegimeDiscovery`, `ExecuteAssessmentAsync` | Accepted Regime result and triggering horizon are preserved. |
| Assessment to Trade Selection | `CompleteMarketCondition`, `ExecuteSelectionAsync` | Saved selector invocation uses the actual accepted upstream results. |
| Selection to Composer | `ReserveSelectionAsync`, `CompleteTradeSelectionReservation`, `ExecuteOrderComposition` | Fund identities are reserved, market evidence is durably captured and accepted, then the saved Composer invocation runs. |
| Composer to Risk | `CompleteOrderComposition`, `ExecuteRiskManagement` | Accepted Composer result is reconstructed; Fund order must reach RiskPending before preparation. |
| Risk to Authorized intent | `CompleteRiskManagement`, `AdvanceRiskFinancialHandoff`, `ExecuteRiskFinancialHandoff` | Independent Risk verification, capacity reservation, and matching Fund authorization precede workflow completion. No consumption/submission. |

The audit found the Fund order transition was missing: reservation produced TemplateSelected, but no workflow handler called MarkComposing or RecordComposed before Risk required RiskPending. `EnsureFundCompositionAsync` now performs the missing transitions using the workflow-accepted Composer result, validates workflow/order ownership, and skips already committed transitions on replay. Focused tests cover all three replay checkpoints and a foreign-workflow order.

## Five scenarios

1. Daily LongFuture.
2. Weekly ShortFuture.
3. Monthly BullCallDebit.
4. Daily BearPutDebit.
5. Weekly ShortBalancedIronCondor.

Each scenario requires all five accepted stage results, Approved Risk with positive whole strategy units, matching upstream hashes, one reservation, RiskApproved Fund state, and final Authorized intent. Failures remain failures; a stale/rejected result cannot satisfy the happy-path assertion.

## Fixture boundaries

Market observations, quotes, calendar and rates are labelled synthetic inputs. The lab Market Assessment profile explicitly allows 15-second source ages; the Composer fixture uses its existing integration timing profile. Snapshot validity is intersected with the actual reservation deadline. Risk uses observation-only candidate/quote age for Development, Test, Emulator and Paper; Production and unknown environments retain the 1,000-ms check. Composer no longer derives a quote-age expiry when the exact frozen Risk policy identifies non-production. Explicit lifetimes remain enforced. The lab Composer candidate lifetime is 30 seconds, intersected with the actual upstream/snapshot deadlines.

The workflow tests now use the production Portfolio command, query, capacity-reservation, ledger and projection actors over isolated NATS. Fund composition transitions use the PostgreSQL event store and Scylla projections. Financial admission, development funding, capacity reservation, receipts and Fund authorization use the PostgreSQL financial boundary and authority fence. Initial Portfolio/Fund/policy authority is deterministic test setup outside the timer; market observations and composition quotes remain labelled synthetic inputs. The suite does not qualify live market subscriptions, broker margin or order execution, and it does not activate an application's live ITI subscription or publish a production strategy deployment.

## Run

Use a separate local NATS/JetStream broker, plus the repository's PostgreSQL, Scylla and Redis test databases:

```powershell
$env:IFM_FINANCIAL_TEST_NATS_URL = 'nats://127.0.0.1:14222'
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests --no-restore -m:1 --filter Category=FullWorkflowRisk --logger trx --results-directory TestResults/full-workflow
```

The Risk Manager delivery runner includes this category. Run these schema-sharing integration tests serially.

## Qualification result - 2026-09-09

All five happy paths reached Approved Risk, RiskApproved Fund state and Authorized intent. These are workflow correctness tests, with latency measured rather than asserted as a pass/fail threshold. No broker order was submitted.

| Scenario | Start to observed authorization | Candidate / oldest quote age at Risk | Result |
|---|---:|---:|---|
| Daily/BearPutDebit | 5.271 s | 1.419 s | Authorized |
| Daily/LongFuture | 5.197 s | 1.647 s | Authorized |
| Monthly/BullCallDebit | 8.935 s | 2.780 s | Authorized |
| Weekly/ShortBalancedIronCondor | 4.971 s | 1.359 s | Authorized |
| Weekly/ShortFuture | 10.771 s | 3.081 s | Authorized |

Evidence: `TestResults/full-workflow/full-workflow-observe-final.trx`. These are single-run development observations using real NATS, PostgreSQL and Scylla, controlled market/Portfolio services and the system clock. They are not production latency targets or percentiles. Earlier strict-age runs remain available as diagnostic artifacts.

Runtime telemetry records `risk.candidate.age`, `risk.quote.oldest_age` and `risk.workflow.duration` histograms in the `TomasAI.IFM.RiskManagement` meter. Structured logs include workflow/invocation identities, environment, ages and terminal financial phase. Decision explanations expose candidate/quote ages and the active age policy. Replay notifications can emit repeated observations; these are operational measurements, not exactly-once accounting counters.

Final regression verification: all 1,023 Trade unit tests passed (`trade-units-observe-final.trx`), and all 13 selected workflow/Risk runtime integration tests passed (`risk-workflow-regression.trx`), including the five full workflow scenarios. Reports are under `TestResults/full-workflow`.

## Successive stage endpoints

`Category=SuccessiveWorkflowStages` runs five independent Daily LongFuture workflows with one through five real pipeline actors. Every case starts at the workflow command boundary. For the first four, a test-only projector wrapper forwards preceding commits to the production projector and suppresses the endpoint commit's projection/notification. PostgreSQL still contains the accepted endpoint result; withholding the notification prevents the next actor from executing. Assertions verify every preceding result is accepted, all later results are absent, and no Risk capacity reservation occurs. These partial cases intentionally leave the production workflow nonterminal; they do not change business continuation rules.

The fifth case uses the complete production projection/dispatch chain and requires Approved Risk and Authorized intent. Timing starts immediately before sending the workflow-start command. The first four stop timing at the endpoint's authoritative commit callback. The repeated performance harness now records the fifth endpoint at the durable Authorized commit callback and reports active-projection cache visibility and verification separately. Fixture/host setup, including creation and funding of the isolated financial book, is excluded. Measurements have no latency qualification threshold and are separate observations, not additive stage timings. The production Portfolio services and controlled market boundary described above apply.

The historical results below, including the 7.314-second actual-Portfolio observation, used the older fifth-endpoint boundary: projection observation plus the verification state load. They cannot be directly compared with the new durable-commit measurements. See [the repeated benchmark protocol and results](RiskManagement-Workflow-Performance-Benchmark.md) for matched baseline/candidate evidence.

Run with the preceding command's filter changed to `Category=SuccessiveWorkflowStages`. The delivery runner includes both categories.

### Successive endpoint results - 2026-09-09

All five cases passed. Source: `TestResults/full-workflow/successive-stages.trx`.

| Actors executed from Regime Discovery | Endpoint | Elapsed | Result |
|---:|---|---:|---|
| 1 | Regime Discovery | 1.317 s | Accepted |
| 2 | Market Assessment | 0.610 s | Accepted |
| 3 | Trade Selection | 1.038 s | Accepted |
| 4 | Order Composer | 3.559 s | Accepted |
| 5 | Risk Manager and Authorized intent | 9.919 s | Approved / Authorized |

These independent cases execute serially in the test runner's order, not necessarily endpoint order. The one-stage case ran first; warmup and runtime variability mean the measured totals need not increase monotonically. Risk observed candidate/oldest-quote age of 3.161 seconds, with age-limit enforcement disabled. These single observations are not latency percentiles or production targets.

The subsequent same-run [trace diagnosis](RiskManagement-Trace-Diagnosis.md) separates Risk calculation, workflow persistence, projection and authorization time, and documents the verified W3C TraceId propagation path.

## Successive endpoint results with binary event storage - 2026-09-09

All five `SuccessiveWorkflowStages` cases passed using the production uncompressed MessagePack event-log codec and the binary-only PostgreSQL schema. The fifth case asserted Approved Risk, positive strategy units, RiskApproved Fund state and Authorized intent. No broker order was submitted. The first four verified accepted results only through their endpoint, with no subsequent stage result or Risk capacity reservation.

| Stages | Endpoint from Regime Discovery | JSON baseline | Binary event log | Time reduction | Result |
|---:|---|---:|---:|---:|---|
| 1 | Regime Discovery | 1.317 s | 1.127 s | 14.46% | Accepted |
| 2 | Market Assessment | 0.610 s | 0.490 s | 19.79% | Accepted |
| 3 | Trade Selection | 1.038 s | 0.923 s | 11.09% | Accepted |
| 4 | Order Composer | 3.559 s | 2.270 s | 36.23% | Accepted |
| 5 | Risk Manager / Authorized intent | 9.919 s | 6.204 s | 37.45% | Approved / Authorized |

Time reduction is `(1 - binary / JSON) * 100`, using the full-precision saved observations. The JSON baseline is `TestResults/full-workflow/successive-stages.trx`; new evidence is `TestResults/full-workflow-binary/successive-stages-binary-development.trx`, with extracted observations in `successive-stages-binary.json` and comparison CSV/JSON alongside it. The run executed serially in runner order 1, 5, 4, 3, 2, matching the baseline's execution order.

These are independent Daily/LongFuture workflow observations, not incremental stage costs or latency percentiles. Timing boundaries and controlled market/Portfolio fixture boundaries remain those documented above. Host setup is excluded; actual NATS, PostgreSQL and Scylla work inside the timed workflow is included. The event log was reset during the cutover, so database history volume, caches, warmup and system load differ from the saved JSON run. The observed improvement cannot be attributed exclusively to serialization.

Risk observed candidate/oldest-quote age of 1,923.323 ms versus 3,160.917 ms previously. Age-limit enforcement remained disabled (`AgeLimitEnforced=false`); latency was measured, not used as a qualification threshold.

Run environment: both `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` were `Development`, as required by the integration host's configuration. The host's connection settings target only local test databases/keyspaces, including `event-source-test-db`. NATS used the existing isolated `ifm-risk-resize-20260909` broker bound to `127.0.0.1:14222`. It was restored to its original stopped state after the run. Initial attempts with conflicting/missing environment configuration failed during host setup and produced no workflow timings; their diagnostic TRX files are retained separately.

Reproduce from the repository root:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:IFM_FINANCIAL_TEST_NATS_URL = 'nats://127.0.0.1:14222'
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=event-source-test-db'
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests --no-restore -m:1 --filter Category=SuccessiveWorkflowStages --logger trx --results-directory TestResults/full-workflow-binary-new
```

Start the isolated test broker before running. No production code or timing thresholds were changed for this measurement.

The subsequent [binary workflow trace diagnosis](RiskManagement-Binary-Trace-Diagnosis.md) breaks a successful same-run timeline into stage, state-reconstruction, projection and authorization costs, with W3C TraceId and raw evidence.

## Successive endpoint results with production Portfolio services - 2026-09-09

All five `SuccessiveWorkflowStages` cases passed after replacing the in-memory Portfolio adapters with the production Portfolio services. The fifth case verified the persisted Fund order, durable capacity-reservation receipt, positive approved units, RiskApproved state and final Authorized intent. The financial APIs crossed actual NATS request/reply boundaries; Fund state used PostgreSQL event sourcing plus Scylla projections, and capacity/ledger state used the PostgreSQL financial stores and authority fence.

| Stages | Endpoint from Regime Discovery | Elapsed | Result |
|---:|---|---:|---|
| 1 | Regime Discovery | 1.028 s | Accepted |
| 2 | Market Assessment | 0.506 s | Accepted |
| 3 | Trade Selection | 0.880 s | Accepted |
| 4 | Order Composer | 2.554 s | Accepted |
| 5 | Risk Manager / Authorized intent | 7.314 s | Approved / Authorized |

Risk observed candidate and oldest-quote age of 2,245.571 ms. Development age-limit enforcement remained disabled, so this was recorded rather than used as a qualification threshold. The real-service five-stage result was 17.90% slower than the earlier 6.204-second controlled-Portfolio binary observation. Single-run variation also affects the first four cases, so that difference is diagnostic rather than a benchmark percentile.

Evidence: `TestResults/full-workflow-binary/successive-stages-actual-portfolio.trx` and `successive-stages-actual-portfolio.log`. The isolated NATS broker was `127.0.0.1:14222`; application configuration targeted the repository's development test databases and keyspaces.

## Successive endpoint checks after projection optimization - 2026-09-09

All five `SuccessiveWorkflowStages` cases passed with the optimized projector and actual Portfolio services. Timers now stop at the persisted endpoint commit; the final case separately records active-cache visibility. These boundaries differ from the earlier inclusive polling/verification observations, so no percentage comparison is made against them.

| Stages | Endpoint from Regime Discovery | Persisted endpoint | Result |
| ---: | --- | ---: | --- |
| 1 | Regime Discovery | 1.058 s | Accepted |
| 2 | Market Assessment | 0.370 s | Accepted |
| 3 | Trade Selection | 0.765 s | Accepted |
| 4 | Order Composer | 2.225 s | Accepted |
| 5 | Risk Manager / Authorized intent | 6.871 s | Approved / Authorized |

The final active-cache visibility observation was 6.898 s. These are independent workflows executed once in runner order 1, 5, 4, 3, 2, with different first-use effects; they are neither incremental stage costs nor percentiles. The [repeated Release benchmark](RiskManagement-Workflow-Performance-Benchmark.md) supplies matched before/after evidence across five full-workflow scenarios, with cold and warm observations separated.

Evidence: `TestResults/strategy-workflow-performance/successive-stages/successive-stages-optimized.trx` and `timings.json`. Risk age limits remained disabled in Development. No broker trade was submitted.
