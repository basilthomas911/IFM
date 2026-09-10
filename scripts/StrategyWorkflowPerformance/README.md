# Strategy workflow performance evidence

`summarize.py` turns benchmark JSONL into inspectable distributions and trace
interval tables. It uses the Python standard library; no package installation is
required. It does not execute workflows or change databases.

From the repository root:

```powershell
# Requires the built Release integration assembly and isolated services.
./scripts/StrategyWorkflowPerformance/Invoke-Benchmark.ps1 `
  -Label baseline-01 -Samples 30 -Warmups 3

python scripts/StrategyWorkflowPerformance/summarize.py `
  --input TestResults/strategy-workflow-performance/baseline-01/samples.jsonl `
  --output TestResults/strategy-workflow-performance/baseline-report

python scripts/StrategyWorkflowPerformance/summarize.py `
  --baseline TestResults/strategy-workflow-performance/baseline-01/samples.jsonl `
  --candidate TestResults/strategy-workflow-performance/candidate-01/samples.jsonl `
  --output TestResults/strategy-workflow-performance/comparison
```

Each input argument can accept multiple files, allowing several alternating
baseline/candidate batches. Compare the same Release configuration, host shape,
scenario fixtures and tracing mode. Record current commit and whether the working
tree contains the candidate change. Repeated hosts and warmed hosts are different
experiments. Equivalent fixture funding does not make accumulated database history
identical, so record history growth and run order as possible confounders.
Choose a new label for every runner invocation; existing evidence is never overwritten.

The output directory contains:

- `report.md`: conditions, distributions, comparisons, trace windows and failures.
- `report.json`: all normalized evidence, metadata, statistics and validation notices.
- `samples.csv`: individual measurements, including failures.
- `summary.csv`: attempted/successful/failed counts, valid timing counts, mean,
  median, p95, range and sample standard deviation for every scenario/phase/metric.
- `comparisons.csv`: matched baseline/candidate medians, percentage reductions and
  deterministic bootstrap confidence intervals.
- `traces.csv`: observed trace windows, interval coverage and missing parents.
- `operations.csv`: per-trace operation calls, inclusive totals, direct-child
  exclusive totals and wall-clock interval unions.
- `runs.csv`: explicit completed/failed run records, including setup failures
  before a sample timer could start.

Cold, warmup, warm and fully traced samples stay in separate distributions.
Successful finite nonnegative timings enter statistics; failed samples remain in
the report. Missing measurements are shown instead of converted to zero. Invalid
JSONL lines, duplicate sample IDs, metadata mismatches and missing evidence become
explicit notices. The report exits with code 1 for failed samples or incomplete
runs and code 2 for an empty sample set, after writing the available evidence.
`run_failed` records make a batch incomplete even if every emitted sample succeeded.
Failed/incomplete runs are excluded from baseline/candidate comparisons; their
samples stay visible in the raw distributions and carry `ComparisonEligible=false`.
New JSONL runs also require an explicit `run_completed` marker whose iteration
count matches the captured samples. Missing markers, mismatched counts or a
completion marker accompanying failed samples make a run incomplete.
Supply the retained TRX when a constructor or setup failure prevented JSONL
creation: failed TRX tests with no timing rows become explicit failed runner samples.
When the TRX contains no metadata, its companion `run.json` supplies the runner's
recorded commit, configuration and conditions.
Check the test runner result as well: process failures may prevent all records
from being emitted. No latency value fails qualification.

`AuthorizedMilliseconds` measures workflow start to durable terminal Authorized
commit. `QueryVisibleMilliseconds` separately observes active-cache removal and
includes its polling interval. It does not measure every read model's visibility.
Verification queries and assertions are timed separately. Each
`CumulativeStageNMilliseconds` is an endpoint in the same full workflow; these are
not isolated actor execution costs. Use spans to distinguish work from waiting.

`ProcessAllocatedBytes`, `ProcessCpuMilliseconds` and collection counts summarize
process-wide deltas through observed visibility; concurrent/background work can
contribute. Working set, private memory and managed heap report sampled sizes,
not per-workflow allocations. These measurements supplement actor spans and CPU
profiles; they do not identify which actor allocated memory or consumed CPU.

The median-reduction interval uses 2,000 independent bootstrap resamples by
default and seed `20260909`. Positive percentages mean reduced latency. An
interval spanning zero is inconclusive. Tiny samples, especially cold runs, do not
establish an improvement; p95 is particularly unstable with few observations.
Independent resampling does not account for serial correlation or infrastructure
drift. Prefer alternating baseline/candidate batches and inspect their metadata.
The report includes a focused warm Authorized comparison table with p50, p95,
both observed percentage reductions and the median-reduction confidence interval.
There is no p95 confidence interval. Metadata checks flag GC/runtime/tracing and
polling changes, and differing integration-test assembly fingerprints require an
explanation that fixture behavior and timing boundaries remained equivalent.

Trace totals are elapsed time, not CPU time. Parent spans include child spans.
Exclusive time subtracts the union of observed direct children clipped to the
parent's interval. Missing instrumentation remains in exclusive time. A descendant
can continue after its parent returns. Operation interval unions avoid double
counting repeated concurrent instances of the same operation, but different
operations still overlap. Do not add rows to produce a workflow total. A causal
span tree and the actual terminal commit are needed to establish the critical
path; this report deliberately does not claim to reconstruct it from elapsed
durations alone.

Legacy `.trx` inputs are supported for inspection. Their `WorkflowMilliseconds`
metric is kept separate from the new durable-commit metric, because older full
workflow timings included polling and a state verification read. Legacy evidence
without run metadata or sample trace IDs will have incomplete attribution.

Report-math and failure-handling regressions use synthetic evidence and no services:

```powershell
python -m unittest discover -s scripts/StrategyWorkflowPerformance -p test_summarize.py -v
```

## JSONL records

Property names are case-insensitive. Each record must be a JSON object on one
line. Run IDs and sample IDs distinguish batches and prevent accidental duplicate
counting.

```json
{"RecordType":"metadata","RunId":"run-1","Commit":"commit-sha","BuildConfiguration":"Release","Machine":"machine","Runtime":".NET version","Conditions":{"Portfolio":"actual services","VisibilityPollingMilliseconds":10}}
{"RecordType":"sample","RunId":"run-1","SampleId":"1","Scenario":"Daily/LongFuture","Phase":"warm","Iteration":1,"Success":true,"Error":null,"StageCount":5,"Endpoint":"Risk Manager / Authorized intent","StartedAtUtc":"2026-09-09T14:00:00Z","AuthorizedMilliseconds":1000,"QueryVisibleMilliseconds":1050,"VerificationMilliseconds":20,"TraceId":"trace-1","StageAcceptedMilliseconds":[{"StageCount":1,"Endpoint":"Regime Discovery","Milliseconds":100,"WorkflowRevision":2}]}
{"RecordType":"span","RunId":"run-1","SampleId":"1","Operation":"workflow.state.load","TraceId":"trace-1","SpanId":"span-2","ParentSpanId":"span-1","StartTimeUtc":"2026-09-09T14:00:00.020Z","Milliseconds":25,"Tags":{}}
{"RecordType":"run_completed","RunId":"run-1","CompletedIterations":1}
```

The example numbers describe the format only, not measured workflow performance.
If setup, workflow verification or trace checks fail, the harness emits
`run_failed` with `RunId`, `CompletedIterations`, `Scenario`, `Phase`, `Iteration`
and `Error` instead of `run_completed`.
