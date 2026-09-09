# Strategy workflow trace and Risk latency diagnosis

Measured 2026-09-09 with `Category=RiskWorkflowTrace`, Daily LongFuture, real Core NATS/PostgreSQL/Scylla and the controlled market/Portfolio fixture documented in the full workflow qualification report. Outcome: Approved Risk and Authorized intent.

- W3C TraceId: `91aae14da0b678149c16a14145d980ae`
- WorkflowId: `01a087b0dd917dfd99c5118fa511120d`
- Evidence: `TestResults/full-workflow/risk-trace-final.trx` and `.json`.

## What the timing means

The earlier 9.919-second result measured the whole workflow. It was not Risk calculation time; subtracting an independently run Composer test is not a stage measurement.

| Same-run measurement | Duration |
|---|---:|
| Start request to observed Authorized intent | 11.162 s |
| Composer completion to Risk result timestamp | 2.067 s |
| Risk result timestamp to terminal authorization timestamp | 2.632 s |
| Risk calculator itself | 35.78 ms |
| Prepare Risk handler | 175.21 ms |
| Fund composition transition fixture | 6.96 ms |

Stage result timestamps are domain timestamps, not exact post-commit stopwatch boundaries. The first row additionally includes admission, projections and the verification read. Instrumentation and cold-start/JIT work affect these single-run development observations. No latency qualification limit was asserted.

## Where the time goes

| Operation, whole observed workflow | Calls | Cumulative duration |
|---|---:|---:|
| Workflow state load | 24 | 3.606 s |
| JSON snapshot deserialization, inside those loads | 23 | 2.503 s |
| Snapshot application and defensive copying, also inside loads | 23 | 0.683 s |
| Workflow state save / projection enqueue | 12 | 1.536 s |
| Workflow projection / notification | 12 | 1.288 s |
| Financial handoff dispatch | 3 | 1.410 s |
| Financial receipt verification handlers | 3 | 156.68 ms |

These are inclusive operation durations; nested spans and overlapping actor requests must not be summed as exclusive wall time. The state-load count includes the test's final verification read.

`IntrinsicTimeStrategyWorkflowStateRepository.LoadStateAsync` calls the snapshot-aware database reader. The trace counters confirm one snapshot per nonempty load, not a replay of the whole history. `EventStreamReadModel.ToDomainEvent` reconstructs the snapshot with Newtonsoft JSON; state application then builds defensive copies and a legacy view. The `CurrentView` getter also clones. Repeating this on successive workflow and realtime commands accounts for substantial in-memory work. The separated spans identify JSON reconstruction as the larger cost, with additional copying overhead.

Risk deliberately passes through separate durable preparation, result acceptance, reservation receipt and Fund authorization checkpoints, each with command reads/writes and projection/notification. The controlled Portfolio fixture means these numbers do not measure a production Portfolio network call.

The highest-value optimization candidates are reducing repeated snapshot reconstruction/copying and reducing duplicated payloads carried in snapshots. Any reuse must preserve immutable ownership, latest-revision checks, legacy/mixed-stream rejection and expected-version concurrency guarantees. The database already selects from the latest snapshot; changing to a latest-snapshot query alone will not solve this. Avoid removing financial verification checkpoints to make a benchmark faster. This delivery adds measurement and tracing; it does not change persistence or financial correctness rules.

## Trace implementation

`TomasAI.IFM.ActorTracing` creates bounded actor-processing spans. Core NATS publishes and requests inject `traceparent` and optional `tracestate`; the legacy and owned command/query message adapters retain validated context independently of payload lifetime. Both actor schedulers restore it during processing. The conventional non-durable projector queue carries ActivityContext alongside its in-memory event and restores it in the worker. Business payloads, hashes and idempotency IDs are unchanged. No baggage is propagated.

`TomasAI.IFM.StrategyWorkflow` adds child spans for stage dispatch, state load/deserialization/application/save, projection, Risk calculation/preparation, Fund composition and financial handoff/verification. Tags include WorkflowId, CorrelationId, revision, stage and financial phase where available. TraceId remains an independent W3C identifier, never derived from a business ID. Risk and terminal workflow logs expose TraceId. Both ActivitySources and the Risk latency meter are registered with the existing OTLP telemetry pipeline (`Telemetry:Metrics:Enabled`). No collector or UI deployment is included.

The integration assertion requires a single TraceId across the captured workflow operations, including Risk calculation and financial handoff, and still requires Approved/Authorized results. Separate unit tests cover valid context roundtrip, absent/invalid context and exclusion of baggage. Tracing disabled leaves the business workflow functional.

This qualifies the workflow-start command through Authorized intent. Live ITI admission, pooled event fanout, durable JetStream recovery, full database-provider spans, real Portfolio service transport and trace-history UI are not claimed as qualified by this fixture. Recovery without incoming transport context starts a new trace; durable WorkflowId/CorrelationId joins separate attempts. Context is not stored in event payloads.

## Warm regression observation

The final 12-case regression run also passed its trace-continuity assertion after other workflows had warmed the process. Its trace is `b9e3ed15fcd9b18ea5ff564ccf25e5ca`, WorkflowId `01a087b2b58778e380259ea11a4eb3ef`, in `TestResults/full-workflow/trace-workflow-final.trx` and `.json`.

| Measurement | Standalone trace above | Later warm trace |
|---|---:|---:|
| Full start-to-observed-authorization | 11.162 s | 4.411 s |
| Composer completion to Risk result | 2.067 s | 0.870 s |
| Risk result to terminal authorization | 2.632 s | 1.242 s |
| Risk calculator | 35.78 ms | 0.66 ms |
| All 24 state loads, inclusive | 3.606 s | 1.464 s |
| JSON snapshot decode | 2.503 s | 0.823 s |
| State application/copying | 0.683 s | 0.384 s |

The difference is consistent with warmup/JIT and runtime variability; it is not an isolated benchmark proving the contribution of each. No performance optimization was made to the business pipeline between these observations. Both traces point to workflow orchestration and repeated reconstruction costs rather than an expensive Risk calculation.

## Final verification

All 12 selected workflow integration tests passed, including five full paths, five successive endpoints, the connected trace assertion and the Risk delivery runtime case. Unit suites passed: Trade 1,023; Shared runtime 234; Core NATS 98; Application actors 30. Reports are `trace-workflow-final.trx`, `trace-trade-units.trx`, `trace-shared-final.trx`, `trace-nats-final.trx` and `trace-actor-final.trx` under `TestResults/full-workflow`. `git diff --check` passed.
