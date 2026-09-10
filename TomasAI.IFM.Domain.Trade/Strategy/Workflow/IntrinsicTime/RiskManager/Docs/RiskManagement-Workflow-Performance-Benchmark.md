# Repeated strategy workflow performance benchmark

The benchmark executes all five production pipeline actors and the actual Portfolio services through durable Authorized intent. Initial market evidence and funding are deterministic fixtures. It does not submit broker orders. Development latency is observed, with no latency qualification limit.

## Measured result - 2026-09-09

Overlapping supporting projection writes reduced observed warm median authorization latency by **11.87-15.26%** across the five scenarios. Four alternating Release batches completed **364 successful workflows**, including **300 measured warm workflows**: 30 samples per scenario per build, with warmups and first-workflow samples excluded. Every workflow retained positive Approved sizing, one reservation, persisted Fund RiskApproved authorization and matching durable receipts.

| Scenario | Baseline median ms | Optimized median ms | Reduction | Median-reduction 95% interval | Baseline p95 ms | Optimized p95 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Daily / LongFuture | 3,354.940 | 2,884.459 | 14.02% | 9.92-16.43% | 3,607.938 | 3,031.131 |
| Weekly / ShortFuture | 3,357.613 | 2,883.557 | 14.12% | 11.27-15.93% | 3,609.209 | 3,190.718 |
| Monthly / BullCallDebit | 3,751.624 | 3,178.965 | 15.26% | 12.37-17.22% | 3,981.428 | 3,382.826 |
| Daily / BearPutDebit | 3,735.833 | 3,270.796 | 12.45% | 9.86-14.65% | 3,931.190 | 3,436.356 |
| Weekly / ShortBalancedIronCondor | 3,574.485 | 3,150.267 | 11.87% | 10.03-14.41% | 3,782.897 | 3,399.836 |

Observed p95 reductions range from 10.13% to 15.99%; these tail estimates have no confidence interval. Independent bootstrap median intervals do not account for background activity or serial correlation. The second matched pair independently reproduced lower medians in every scenario (10.32-13.26%).

| Scenario | First pair median reduction | Second pair median reduction |
| --- | ---: | ---: |
| Daily / LongFuture | 14.51% | 10.32% |
| Weekly / ShortFuture | 14.97% | 12.38% |
| Monthly / BullCallDebit | 16.15% | 13.26% |
| Daily / BearPutDebit | 12.93% | 11.57% |
| Weekly / ShortBalancedIronCondor | 14.62% | 10.50% |

Cache-visibility medians also decreased in every scenario: respectively 3.391 to 2.931 s, 3.407 to 2.915 s, 3.851 to 3.202 s, 3.770 to 3.303 s, and 3.622 to 3.168 s. Allocation-reduction confidence intervals all span zero, so this change has no demonstrated allocation saving. Process CPU median reductions were 3.2-6.9%, with only LongFuture's interval entirely above zero. The strongest measured result is elapsed-time reduction.

First-workflow authorization remains substantially slower: baseline observations were 9,294.061 and 8,371.787 ms; optimized observations were 8,170.391 and 7,627.267 ms. These two observations per build do not establish a cold-start percentile. They show that the earlier concern about seven-second first workflows remains relevant. The historical 7.314-second integration observation also included a different polling/verification boundary and cannot be used as the baseline for this percentage claim.

Completed evidence, in execution order:

1. `TestResults/strategy-workflow-performance/baseline-fixed-01/samples.jsonl`
2. `TestResults/strategy-workflow-performance/candidate-02/samples.jsonl`
3. `TestResults/strategy-workflow-performance/baseline-fixed-02/samples.jsonl`
4. `TestResults/strategy-workflow-performance/candidate-03/samples.jsonl`

Each directory retains runner metadata, JSONL and passing TRX evidence. The [combined report](../../../../../../TestResults/strategy-workflow-performance/comparison-final/report.md) contains distributions, confidence intervals and all raw normalized samples. The [second-pair report](../../../../../../TestResults/strategy-workflow-performance/comparison-second/report.md) checks replication separately. All four runs passed metadata/completion validation.

All runs share integration harness SHA-256 `D7A8D5EE2575C902175427B6511282F2FBDEB0E136EC1DEE57F5453583D9B204`, the same Portfolio and Storage hashes, .NET 10.0.10, workstation GC, Release configuration, tracing disabled and 10 ms visibility polling. The preserved sequential Trade binary is `B83B3738DFF26728A5151851A5F47F3F59775B735AE472E526789A4B28AD521F`; the optimized Trade binary is `CFDFE82C7D46E18DC3095BFE0D92A9F7FF95A58072FABE6C38F6BDF2BE57FA39`. Both are based on commit `4b64d579b1f1ff60ed2b8e0d44d01bbe41d8ca6c`; hashes distinguish the uncommitted candidate from the instrumented baseline.

## Reproduction

Build `TomasAI.IFM.Domain.Trade.IntegratedTests` in Release, without a debugger. Start the existing isolated NATS broker on `127.0.0.1:14222` and ensure local PostgreSQL, Redis and Scylla test stores are ready. The harness validates Development, loopback hosts, test database names, and the event database `localhost:5432/event-source-test-db`.

```powershell
dotnet build TomasAI.IFM.Domain.Trade.IntegratedTests -c Release --no-restore -m:1
./scripts/StrategyWorkflowPerformance/Invoke-Benchmark.ps1 -Label baseline-01 -Samples 15 -Warmups 3
python scripts/StrategyWorkflowPerformance/summarize.py --input TestResults/strategy-workflow-performance/baseline-01/samples.jsonl --output TestResults/strategy-workflow-performance/baseline-report
```

`Invoke-Benchmark.ps1` defaults to Daily/LongFuture, Weekly/ShortFuture, Monthly/BullCallDebit, Daily/BearPutDebit and Weekly/ShortBalancedIronCondor. It uses a new evidence directory per label and records the commit, actual DLL hashes and build configuration. `-TestAssembly` can select preserved baseline binaries for alternating baseline/candidate batches. `-Trace` is for separate diagnostic runs; traced samples do not enter the untraced warm distribution.

The opt-in test category is `WorkflowPerformanceBenchmark`. Environment variables and the report format are documented in [the runner documentation](../../../../../../scripts/StrategyWorkflowPerformance/README.md).

## Measurement contract

- One host, producer and set of actual actor services are reused throughout each batch.
- One first-workflow sample is labelled `cold`. Startup, schema creation and initial funding are outside its timer, so it is not a complete cold-process launch measurement.
- Each scenario then receives warmup runs, followed by measured runs in round-robin order.
- Every sample gets a fresh workflow, entity, policy, Portfolio and Fund with equivalent initial authority and capital. Setup and funding are outside the timer. Existing records are retained; total shared database volume grows.
- Composer market preparation remains a fixture boundary: on a preparation-store miss it reloads the accepted workflow, constructs synthetic quotes and saves the snapshot inside the timed workflow. This cost is identical for baseline and candidate but is not a measurement of live market capture.
- All five stage-acceptance times come from post-commit callbacks in the same full workflow. The callback forwards every full-workflow event to the production projector.
- `AuthorizedMilliseconds` ends when PostgreSQL has committed the Authorized workflow snapshot. This is the primary comparison metric.
- `QueryVisibleMilliseconds` observes the active workflow cache entry appearing and then disappearing after the terminal projection, at a 10 ms polling cadence. It measures this cache boundary; dedicated history queries are not timed by this metric.
- `VerificationMilliseconds` measures subsequent authoritative reload and assertions. It is excluded from both workflow latency metrics.
- Assertions require all five accepted stages, Approved positive sizing, matching hashes, one capacity reservation, the Fund's persisted RiskApproved authorization, and matching durable receipts.
- Allocation, CPU and GC counters cover the whole test process from workflow start through observed visibility. Background actor work is included; these are not thread-local business-calculation counters.
- Failed samples and incomplete runs remain in evidence. They do not become successful latency samples or disappear from the report.

Median, p95, minimum/maximum, mean, standard deviation and sample count are reported separately for cold, warmup, warm and traced runs. Comparisons use `(baseline - candidate) / baseline * 100`. Deterministic bootstrap intervals describe sample uncertainty; shared-host variation, run order and growing database history still limit causal attribution.

## Diagnostic baseline

The successful real-service trace batch is `TestResults/strategy-workflow-performance/baseline-trace-ready/samples.jsonl`: one first-host workflow, three warmups and eight diagnostic workflows, all Authorized. Its median diagnostic authorization time was 4,007.694 ms. A sampled-stack/GC profiler was attached during part of this batch, so these times are not the formal untraced baseline.

Representative trace `cb88567c0ff3c576db3f3797d83329c6` reached durable authorization in 4,045.284 ms. Intervals below are clipped to its start-to-authorization window. Parent spans include child work and must not be added together.

| Operation | Calls | Inclusive elapsed ms |
| --- | ---: | ---: |
| Workflow projection | 11 | 840.65 |
| Timeline/detail/active large writes within projection | 33 | 730.54 |
| Workflow state loading | 23 | 726.45 |
| Snapshot deserialization within loading | 22 | 288.73 |
| Snapshot database reading within loading | 22 | 106.68 |
| Workflow snapshot saving | 12 | 479.33 |
| Defensive view copying across loads/updates | 34 | 241.72 |
| Eager legacy-view construction across loads/updates | 34 | 223.07 |
| Composer execution construction | 1 | 68.95 |

Across the eight diagnostic workflows, median process allocation was approximately 509 MiB, with 57 Gen0, 19 Gen1 and 4 Gen2 collections. These figures motivate further copying/allocation work; they do not identify a single allocation owner.

The real Fund API waits include a 50 ms projection polling interval in `PortfolioCommandApis.SendAndReadOrder`. Representative MarkComposing, RecordComposed and authorization requests needed a second read. Their waits include this polling interval and cannot be interpreted as pure Fund calculation time.

## Optimized diagnostic trace

The separate `candidate-diagnostic-final` run passed all 12 workflows, including W3C trace-propagation and service-boundary assertions. Its report parsed 8,294 spans with no evidence notices. The ten-second sampled-thread/GC capture and bounded top-method report both exited successfully; the 53,294,222-byte profile is retained at `TestResults/strategy-workflow-performance/candidate-diagnostic-final/workflow-copying-10s.nettrace`.

Representative warmed sample `0a2626fa2d11417c8cdaf6bd90353e30`, TraceId `5b00d4e1f362a94e0aa8b6bc1fa6b43f`, reached durable authorization in 3,188.221 ms. Clipped to that boundary, projection covered 435.974 ms across 11 revisions; state loading totalled 555.138 ms across 23 calls and state saving 398.951 ms across 12 calls. Detail, timeline and active write totals were 123.075, 84.938 and 249.089 ms. Concurrent child spans overlap, so those rows cannot be summed into a workflow total. Terminal projection work after authorization is excluded from these clipped values.

This traced run is excluded from formal latency distributions. Sampled thread waits dominate the top-method report, which does not establish CPU utilization or allocation ownership. The diagnostic orchestration wrapper reported exit 1 because its completed process object's exit-code property was unavailable; the underlying test passed, all 12 JSONL samples and the completion marker were present, and collector/report exits were zero. `diagnostic-validation.json` records that distinction without altering the raw evidence.

## Implemented projection optimization

The selected change overlaps independent Scylla timeline, start-attempt, detail, entity-index and status-index writes within the existing per-entity gate. Those writes must all succeed before the active-row upsert/delete, cache publication and next realtime notification. The active row remains last because a cache-miss query can read it directly. A failed supporting write must drain its siblings before releasing the gate, suppressing active-row publication and notification. Historical records, financial checks and durable checkpoints remain part of the workflow.

Projection ordering tests cover every write barrier/failure, terminal active-row/cache removal, same-entity revision ordering, sibling draining and different-entity progress. They explicitly verify overlap of the five independent writes and delayed active-row publication. The Release workflow unit suite passed 1,005 tests, including 21 projection-ordering cases and six calendar-cutoff regressions.

Final validation also passed all five `SuccessiveWorkflowStages` integration cases, all 12 diagnostic workflows with trace assertions, and all 18 reporting-math/failure-handling tests. The Release build completed with no warnings or errors. [Staggered endpoint observations](RiskManagement-Full-Workflow-Qualification.md#successive-endpoint-checks-after-projection-optimization---2026-09-09) retain the individual timings; they are single qualification observations, separate from the repeated benchmark.

## Evidence exclusions and environment limits

The original `baseline-01` batch stopped when synthetic option data selected its Treasury tenor from UTC calendar DTE instead of the pricing engine's exchange value-date count. At 18:00 ET, a DST-crossing Monthly expiry required the two-month tenor instead of three-month. The fixture now derives the tenor from the production calendar calculation. Six deterministic before/after-cutoff cases validate context and successful composition. Production pricing validation, the synthetic 4% rate and risk rules were retained. That incomplete batch is preserved and excluded from comparisons.

Earlier startup attempts failed while local Redis/Scylla services were unavailable. Docker was recovered and existing test containers restarted; no database was cleared for these benchmarks. The incomplete sampled-stack capture in `baseline-trace-ready/workflow-cpu.nettrace` is retained as failed-capture evidence, not a CPU profile result.

The workstation also had installer activity during the first matched pair: Git installations disappeared from their previous locations and free disk space changed. The second pair repeats the comparison after the main observed installer exited. Remaining background work, database growth and serial correlation limit causal precision; bootstrap intervals quantify sample variation and do not account for those factors. These are local development observations, not production latency guarantees.

## Next experiments

1. Profile the first workflow separately from steady state to attribute first-use runtime, serializer and numerical initialization. Evaluate explicit startup preparation only after identifying its cost; report startup duration separately rather than moving it outside the workflow timer without disclosure.
2. Use allocation stacks and the existing snapshot/copy spans to reduce repeated view cloning and eager legacy-view construction. Preserve defensive isolation, persisted snapshot compatibility and legacy read behavior, with the same financial assertions.
3. Investigate the repeated 50 ms Fund projection polling waits. Any acknowledgement/read change must still verify committed Fund outcomes, recovery behavior and the existing Authorized-intent boundary.

Make one production change per comparison and rerun these same five scenarios. Preserve failures and report both cold and warm observations; development latency remains a measurement rather than a qualification gate.
