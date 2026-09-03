# Databento Absolute Per-Dataset Watchdog and Reset Design v1.0

**Status:** In-process dataset watchdog implemented; Stage 3 process supervision remains deferred
**Date:** 2026-09-03
**Scope:** Databento transport, native drain, managed channels, aggregation, hot-cache ingress,
publication evidence, downstream correlation, recovery, and qualification
**Supersedes:** The health-trigger, core-failure isolation, and reset behavior in sections 2, 7, 8,
and 10 of `Databento-Market-Data-Service-Resiliency-System-Design-v0.1.md`

## 1. Decision

There shall be exactly one authority that decides whether a Databento dataset is Up or Down and
exactly one action that can reset it.

- `DatabentoMarketDataWatchdogService` is the sole decision and lifecycle authority.
- Health, terminal, manual, startup, and rollover requests all enter the same serialized operation
  queue.
- `ResetDatasetAsync` is the only reset primitive. No actor, UI component, hosted service, native
  callback, aggregation worker, or downstream consumer may stop and restart Databento directly.
- Health and recovery are evaluated independently for each dataset.
- If several datasets are Down in the same observation, one recovery operation resets exactly that
  affected set. Healthy datasets remain running.
- Overall market-data readiness is derived from dataset truth. It does not destroy a healthy
  dataset merely because another required dataset is Down.

The watchdog is fail-operational by dataset and fail-closed by dependent capability. For example,
an XCBF failure does not stop a healthy GLBX feed, but a feature requiring XCBF is unavailable until
XCBF qualifies again.

## 2. Incident correction

The current implementation is not an end-to-end datafeed watchdog:

1. The five/fifteen-minute thresholds inspect provider-message age only.
2. `AggregationWorkerRunning` is derived from a service-running flag, not worker progress.
3. `RecordsProduced`, `RecordsConsumed`, ring use, managed backlog, aggregation completion, and
   in-flight duration do not participate in the recovery decision.
4. Recovery calls the whole-epoch stop path.
5. The stop path requires the blocked managed pipeline to drain and can fail with
   `FeedStopDrainIncompleteException`.
6. Dataset aggregation services share epoch-owned publisher, route, and cache infrastructure, so
   safely replacing one dataset is not currently a complete lifecycle operation.

This design replaces freshness-only inference with causal progress accounting and replaces the
whole-epoch stop/start sequence with one generation-fenced dataset reset.

## 3. System invariants

1. One active generation exists per dataset.
2. A dataset generation owns one native feed/handle/ring, one managed drain, its managed channels,
   one multiplexed reader, and one aggregation worker.
3. A generation has a cancellation token used by every managed wait and publication initiated by
   that generation.
4. Once fenced, an old generation cannot mutate hot caches, enqueue publication, or reacquire a
   route.
5. A replacement generation is not admitted until the old generation is quiescent and absent from
   the native registry.
6. Watchdog measurement never performs network or database work on a market-data hot path.
7. Measurement failure cannot throw into the feed.
8. Reset evidence is captured before teardown begins.
9. Every reset attempt is correlated, persisted, bounded, and idempotent.
10. No downstream UI or analytics failure alone declares an otherwise progressing dataset Down.

## 4. Dataset pipeline and measurement boundaries

```text
Databento transport
  | heartbeat/provider messages/subscription acknowledgements
  v
Native producer -> native ring -> dbf_feed_read_batch64
                                      |
                                      | native read result
                                      v
Managed drain -> per-instrument bounded channels -> multiplexed reader
                                                       |
                                                       | leased managed batch
                                                       v
                                              TickAggregation worker
                                                       |
                            +--------------------------+------------------+
                            v                          v                  v
                       hot caches                local/live route    publisher ingress
                                                                            |
                                                                            v
                                                                     NATS/event delivery
                                                                            |
                                                                            v
                                                           analytics / projections / UI
```

Every boundary exposes a bounded, allocation-safe snapshot keyed by dataset and generation. The
watchdog reads those snapshots; producers do not call the watchdog.

### 4.1 Native transport

Required measurements:

- native state and terminal status;
- producer alive/completed;
- authenticated/transport running;
- expected and acknowledged subscriptions;
- heartbeat count and monotonic age;
- provider-message count and monotonic age;
- records produced;
- ring capacity, used, high-water, overruns, and producer backpressure waits;
- terminal error and warning counts.

### 4.2 Native read and managed drain

Required measurements:

- native read-call count;
- cumulative records read;
- last non-empty read size, first sequence, and last sequence;
- drain stage;
- cumulative records routed to managed assembly batches;
- records routed from the current native read;
- current native-read record index, kind, publisher, instrument, and sequence;
- managed batch publication count;
- current managed batch identity and record count;
- current stage start time and duration;
- failures and last full exception detail.

The implemented `FeedDrainDiagnostics` is the initial snapshot for this boundary. Implementation
must add cumulative routed counts and stage duration so conservation and stall duration do not have
to be inferred from samples.

### 4.3 Managed channels and pools

Required measurements per dataset and, in bounded diagnostic detail, per active instrument:

- channel batch capacity and count;
- record capacity and estimated record count;
- batches published and leased;
- oldest queued batch age;
- full-wait count, current full-wait duration, and maximum full-wait duration;
- pool capacity/free count/misses;
- outstanding consumer lease age and batch identity;
- completion/fault state and last exception.

### 4.4 Aggregation

Required measurements:

- actual worker task state, not only the service-running flag;
- records started, completed, and failed;
- input counts by record kind;
- current record identity, stage, start time, and duration;
- last failure identity, stage, exception, and duration;
- duplicate, out-of-order, and missing source sequences;
- quote-buffer ownership and flush counts;
- hot-cache accepted/rejected update counts and timestamps;
- durable and non-durable publication attempts, completions, failures, latency, and pending depth.

The implemented `TickAggregationMetricsSnapshot` is the initial snapshot for this boundary.

### 4.5 Downstream correlation

Every downstream stage that can prove or disprove feed flow records:

- received, started, completed, failed, dropped/coalesced, and recovered counts;
- last receipt, completion, and failure times;
- input-to-stage and stage-processing latency;
- current and maximum pending depth;
- oldest pending age;
- last exception type/message and current operation;
- dataset, dataset generation, contract, value date, and source identity.

Covered stages include publisher ingress, publisher worker, NATS acknowledgement, tick/bar actors,
RSI, TDI, ITI, EMA, Bollinger, VWAP, VX term structure, trade signals, Market Outlook channel/cache,
realtime projection, and UI delivery where acknowledgement exists.

Dataset/generation/contract are bounded metric dimensions. Batch IDs, source sequences, event IDs,
and exception details belong in the current/last diagnostic record and traces, not metric labels.

Downstream evidence localizes a failure. It does not automatically blame Databento. If aggregation
and hot-cache progress while NATS or the UI does not, the dataset remains Up and the downstream
stage becomes Degraded or Down independently.

## 5. Causal progress model

For every dataset, the watchdog retains the previous observation and monotonic last-progress
deadlines for these independent boundaries:

- transport progress;
- native production progress;
- native consumption progress;
- managed routing progress;
- aggregation completion progress;
- required-route accepted-input progress;
- publisher progress.

Counter deltas and backlog establish causality:

```text
native backlog       = RecordsProduced - RecordsConsumed
drain in-flight      = RecordsReadCumulative - RecordsRoutedCumulative
managed backlog      = BatchesPublished - BatchesLeased
aggregation in-flight= RecordsStarted - RecordsCompleted - RecordsFailed
publication backlog  = PublicationEnqueued - PublicationCompleted - PublicationFailed
```

Counters are generation-scoped. A reset starts new counters at zero and cannot be confused with
counter regression.

## 6. Health states

Each dataset has this state machine:

```text
ScheduledStopped
       |
       v
    Starting -> Qualifying -> Up
                   |          |
                   |          v
                   +------> Suspect
                                |
                    evidence clears | hard deadline/terminal fact
                                v
                               Down
                                |
                                v
                            Resetting
                                |
                         Qualifying -> Up
                                |
                         retry exhausted
                                v
                              Failed
```

- `Up`: all required transport and managed invariants hold and progress is consistent with demand.
- `Suspect`: one boundary has stopped progressing but the hard-down predicate is not yet satisfied.
- `Down`: a terminal fact or a confirmed hard stall exists. Reset is queued immediately.
- `Resetting`: the single reset action owns the dataset.
- `Qualifying`: a new generation exists but is not yet admitted as Up.
- `Failed`: bounded recovery was exhausted. Other datasets continue.
- `ScheduledStopped`: inactivity is intentional and timers are disarmed.

## 7. Down predicates and timing

### 7.1 Immediate Down

No five-minute wait is applied when any of these facts is observed:

- native state is Stopped or Faulted unexpectedly;
- terminal status is not OK;
- producer thread completed unexpectedly;
- required subscription acknowledgement disappeared or startup qualification failed;
- ring overrun/data loss occurred;
- aggregation worker task completed unexpectedly or faulted;
- the dataset is missing from a complete registry snapshot;
- a generation invariant is violated.

### 7.2 Confirmed hard stall

The initial `HardStallTimeout` is five minutes. The watchdog polls every 15 seconds and uses
monotonic time. It does not allocate or reset a `System.Threading.Timer` for every batch; advancing
the atomic last-progress deadline has the same semantics without timer races.

A dataset becomes Down when any predicate remains true for five minutes:

| Evidence | Root-cause classification |
|---|---|
| `RecordsProduced` advances, `RecordsConsumed` does not, and ring use is nonzero/increasing | `NativeDrainStalled` |
| Native reads occur, routed count does not advance, or current read remains partially routed | `ManagedRoutingStalled` |
| Managed publish remains active and channel full/backlog persists | `ManagedChannelBlocked` |
| Managed backlog exists, but aggregation completion does not advance | `AggregationStalled` |
| One aggregation record remains in flight beyond the deadline | `AggregationRecordStalled` |
| Required live route receives provider flow but no accepted cache input | `RequiredRouteStalled` |
| Publisher ingress backlog blocks dataset-owned aggregation despite its required nonblocking contract | `PublisherIsolationViolation` |

The old fifteen-minute provider-freshness rule is not the hard-stall detector. It may remain a
freshness display threshold, but it cannot override contradictory progress/backlog evidence.

### 7.3 Legitimate quiet periods

Absence of records alone is not Down:

- During `ScheduledStopped`, all progress deadlines are disarmed.
- During an active but off-trading session, current heartbeat, transport, subscriptions, empty
  backlogs, and no producer progress constitute `UpQuiet`.
- During live trading, required-route freshness participates in health. The five-minute hard rule
  is armed only after startup qualification and only while the route is required.
- A fresh heartbeat with no records and no backlog is transport evidence, not proof of end-to-end
  market-data progress. It cannot conceal a backlog elsewhere.

### 7.4 Probe failure

One failed status read records `Suspect/WatchdogProbeFailed`. A terminal signal or two consecutive
failed probes makes the dataset Down. This prevents one transient observation failure from causing
destructive churn while ensuring an unobservable dataset cannot remain nominally Up.

## 8. Absolute watchdog evaluation

Every 15 seconds, and immediately on a terminal signal:

1. Enter the sole serialized lifecycle queue.
2. Read one coherent native bulk snapshot.
3. Join each entry with its dataset-generation managed snapshots.
4. Calculate deltas from the previous observation.
5. Update boundary-specific last-progress timestamps.
6. Evaluate terminal facts, backlog conservation, stage duration, and required-route freshness.
7. Assign one dataset state and one typed reason.
8. Persist the complete pre-action observation.
9. Publish state transitions without blocking the decision loop.
10. For every newly Down dataset, enqueue one coalesced reset request immediately.

The evaluation result contains both the root-cause stage and all corroborating downstream facts.
It never reports only a color.

## 9. One reset path

All automatic, manual, terminal, startup-recovery, and rollover replacement requests call:

```csharp
Task<DatasetResetResult> ResetDatasetAsync(
    DatasetResetRequest request,
    CancellationToken applicationStopping);
```

`DatasetResetRequest` contains dataset, expected generation, value date, reason code, evidence
observation ID, correlation ID, and requested time. Duplicate requests for the same dataset and
generation coalesce into the active reset. A stale-generation request completes as already
satisfied.

No public component receives the native feed or epoch mutation APIs. Architecture tests enforce
that only the watchdog-owned lifecycle implementation calls them.

## 10. Dataset reset algorithm

The reset action performs these phases:

### Phase A: fence and capture

1. Compare the expected generation with the active dataset generation.
2. Atomically transition the dataset to `Resetting`.
3. Fence old-generation cache mutation, route acquisition, and publication admission.
4. Capture native, drain, channel, aggregation, publisher, downstream, and exception evidence.
5. Persist the pre-reset snapshot before destroying volatile state.

### Phase B: bounded teardown

6. Cancel the dataset-generation token.
7. Stop native production so no new ring records are admitted.
8. Wake and complete the dataset readers/channels.
9. Cancel aggregation and publisher-ingress waits.
10. Discard generation-stale ephemeral market-data batches and safely return their leases.
11. Wait at most five seconds for the managed drain and aggregation worker to quiesce.
12. Capture terminal native state.
13. Dispose the dataset native feed, handle, ring, drain resources, channels, reader, and
    aggregation service.
14. Verify the old feed instance is absent from the native registry.

Every await in the dataset path must accept generation cancellation or have a strict upper bound.
Market-data publication must use a nonblocking/bounded ingress queue. It may not hold a native or
managed batch lease while waiting indefinitely for NATS, an actor response, analytics, or UI work.

### Phase C: recreate

15. Re-read the current dataset contract assignments and intended route ownership.
16. Create a new dataset generation, native feed, ring, drain, channels, reader, and aggregation
    worker.
17. Restore subscriptions and previously active route intents for that dataset.
18. Start native transport and managed consumers in the established consumer-before-producer order.

### Phase D: qualify and admit

19. Require Running/OK native state, live producer, authenticated transport, and subscription
    acknowledgements.
20. Require the managed drain and aggregation task to be running under the new generation.
21. Require no old native registry entry and no old-generation publication admission.
22. During live trading, require native read, aggregation completion, and accepted hot-cache
    progress within the qualification window.
23. During legitimate quiet periods, require current heartbeat, empty backlogs, and valid workers.
24. Atomically admit the new generation and transition the dataset to `Up`.
25. Persist and publish the post-reset result with recovery duration and counter baselines.

If teardown or qualification fails, the same reset action retries according to the configured
bounded recovery policy. It never starts a replacement beside an unquiesced old generation.

## 11. Hard-reset guarantee and process escalation

.NET cannot forcibly terminate an arbitrary blocked `Task`, and managed code cannot safely kill a
native thread. Therefore an absolute reset requires cancellation compliance at every wait.

If a dataset generation does not quiesce inside the five-second teardown bound:

1. record `DatasetTeardownUnresponsive` with the complete evidence snapshot;
2. do not create an overlapping replacement generation;
3. request supervised termination and replacement of the affected dataset worker process.

While Databento remains hosted inside the API server, this escalation necessarily recycles the API
process and is broader than one dataset. Resiliency Stage 3 moves each dataset generation into a
supervised worker process, making forced termination a contained last-resort reset that preserves
the API/Core host and unaffected datasets. The later Aspire Market Data Feed extraction orchestrates
this established process boundary. There is no honest in-process design that can guarantee
destruction of uncancellable managed/native execution.

Process escalation is part of the one reset action, not a competing reset mechanism.

## 12. Required ownership refactoring

True per-dataset replacement requires these changes to the current epoch:

- replace the single epoch generation with a generation per dataset;
- make feed, drain, channels, reader, aggregation, and cancellation source dataset-owned;
- keep the publisher worker service-owned; expose generation-fenced, nonblocking dataset ingress;
- make hot-cache invalidation and admission dataset/contract scoped;
- make live-route detach/restore dataset scoped;
- atomically replace one dataset runtime in the epoch lookup maps;
- prevent one dataset's `StopAsync` from stopping a publisher shared by other datasets;
- expose actual worker `Task` state and generation, not only `IsRunning`;
- remove all direct stop/start calls outside the watchdog lifecycle implementation.

## 13. Typed reason model

Minimum reason codes:

- `NativeTerminalFailure`
- `NativeProducerStopped`
- `NativeHeartbeatExpired`
- `NativeRingOverrun`
- `NativeDrainStalled`
- `ManagedRoutingStalled`
- `ManagedChannelBlocked`
- `AggregationWorkerCompleted`
- `AggregationRecordStalled`
- `RequiredRouteStalled`
- `PublisherIsolationViolation`
- `WatchdogSnapshotIncomplete`
- `DatasetTeardownUnresponsive`
- `DatasetQualificationFailed`
- `DownstreamPublicationDegraded`
- `DownstreamAnalyticsDegraded`
- `ScheduledStopped`

Every Down/Resetting observation identifies the dataset, generation, root-cause reason, first
suspect time, hard-down time, evidence counters, active stage/record, and reset correlation.

## 14. Configuration baseline

```json
{
  "DatabentoWatchdog": {
    "PollIntervalSeconds": 15,
    "HardStallSeconds": 300,
    "ProbeFailureThreshold": 2,
    "DatasetTeardownSeconds": 5,
    "DatasetStartupSeconds": 30,
    "DatasetQualificationSeconds": 30,
    "AttemptTwoDelaySeconds": 5,
    "AttemptThreeDelaySeconds": 15
  }
}
```

Configuration is validated at startup. Production may choose different route-freshness thresholds,
but no threshold disables terminal, overrun, unexpected worker completion, or generation-invariant
triggers.

## 15. Verification gates

### Gate 1: health accounting

- Deterministic tests reconcile every adjacent counter under normal flow.
- A blocked native drain, managed publish, aggregation stage, and publisher ingress each produce a
  distinct root-cause reason.
- Quiet-market tests do not reset a healthy empty pipeline.
- Downstream NATS/UI failure does not mark a progressing dataset Down.

### Gate 2: sole authority

- Architecture tests fail if any type other than the watchdog lifecycle implementation calls
  Databento start, stop, reset, replace, or native destruction.
- Manual and automatic reset requests converge on `ResetDatasetAsync`.
- Duplicate/stale reset requests are idempotent.

### Gate 3: per-dataset isolation

- Inject a GLBX stall while XCBF continues; only GLBX generation changes.
- Inject an XCBF stall while GLBX continues; only XCBF generation changes.
- Inject simultaneous failures; exactly the affected set resets once.
- Healthy-dataset counters and publication continue during another dataset's reset.

### Gate 4: bounded teardown

- Every aggregation and publication await responds to generation cancellation.
- Backpressured channel teardown returns all leases and removes the native registry entry.
- No replacement starts while an old generation is present.
- An intentionally uncancellable wait triggers process-supervisor escalation rather than an
  overlapping generation.

### Gate 5: qualification

- A replacement is not Up until transport, subscriptions, worker tasks, generation fencing, and
  session-appropriate progress qualify.
- Reset success restores prior dataset route intents.
- Failed qualification is persisted and retried without affecting healthy datasets.

### Gate 6: forensic completeness

- The persisted pre-reset snapshot alone identifies which adjacent boundary stopped progressing.
- A reproduction of the 10,562-record incident identifies last native read size, drain stage,
  channel backlog, in-flight aggregation record/stage/duration, and downstream publisher state.
- Logs and status queries share observation and reset correlation IDs.

## 16. Implementation order

1. Introduce dataset-generation identity and the causal progress snapshot.
2. Complete cumulative drain/channel/publisher counters and stage-duration instrumentation.
3. Propagate dataset/generation identity through downstream operational measurements.
4. Implement the pure per-dataset health evaluator and deterministic timing tests.
5. Refactor shared epoch resources into service-owned infrastructure plus dataset-owned runtimes.
6. Make every generation-owned wait cancellable and bounded.
7. Implement the sole `ResetDatasetAsync` action and idempotent request coalescing.
8. Replace the current freshness-only recovery predicate with the new evaluator.
9. Add pre/post-reset persistence, structured status events, and query/UI detail.
10. Pass all six verification gates, then enable automatic per-dataset reset.

Automatic reset must not be enabled after only the detector is implemented. Detection, bounded
teardown, generation fencing, reconstruction, and qualification are one safety unit.

## 17. Implemented verification evidence

The in-process implementation now provides:

- a 15-second watchdog poll and five-minute causal-stall confirmation window;
- dataset-local generation identity in runtime health snapshots;
- native-ring, managed-channel, drain-stage, and in-flight `ProcessRecordAsync` evidence;
- immediate terminal/overrun/worker-completion classification;
- cancellation from dataset teardown through the managed reader, aggregation stages, live router,
  publisher ingress, and NATS send;
- one serialized, generation-checked `ResetDatasetAsync` replacement path;
- preservation and restoration of active stream-owner intent;
- pre-reset diagnosis and post-reset qualification observations with one correlation ID;
- deterministic tests for quiet markets, timer reset on progress, five-minute causal stalls,
  healthy-dataset isolation, stale reset idempotency, stream restoration, and cancellation of a
  blocked `ProcessRecordAsync` publication.

The inability to forcibly terminate arbitrary native or third-party uncancellable work remains a
Stage 3 concern. The required escalation is the already-approved supervised child process per
dataset; the in-process implementation never starts an overlapping generation after an incomplete
teardown.
