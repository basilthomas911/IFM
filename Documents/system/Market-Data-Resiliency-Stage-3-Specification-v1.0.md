# Market Data Resiliency Stage 3 Specification v1.0

| Item | Value |
| --- | --- |
| Specification ID | `MDR-S3` |
| Status | Ready for review; implementation not started |
| Version | 1.0 |
| Date | 2026-09-04 |
| Stage | Stage 3 — central operations health and per-dataset process containment |
| Accepted baseline | `Documents/system/Market-Data-Resiliency-As-Built-Specification-v1.0.md` |
| Roadmap authority | `Documents/system/Market-Data-Reliability-Three-Stage-Implementation-Plan-v1.0.md`, section `MDOH` |
| Implementation plan | `Documents/system/Market-Data-Resiliency-Stage-3-Implementation-Plan-v1.0.md` |
| Initial deployment | API Server supervisor plus one console worker process per active Databento dataset |
| Platforms | Windows and Linux |

## 1. Purpose

Stage 3 converts the accepted Stage 2 in-process dataset boundary into an operating-system-enforced
containment boundary and adds one independent, end-to-end operations-health view from native market
data through Market Outlook and UI delivery.

The primary guarantee is:

> A permanently blocked or corrupted dataset generation can be terminated and replaced without
> restarting the API Server, UI, health authority or any healthy dataset worker.

This specification defines required behavior. The implementation plan defines how the repository
will reach it. Requirements use `S3-` identifiers so tests, code review and final evidence can trace
back to one binding statement.

## 2. Scope

### 2.1 Included

- a dedicated console worker host for exactly one Databento dataset generation;
- a supervisor in the API Server deployment that owns worker processes;
- retention of `DatabentoMarketDataWatchdogService` as the sole lifecycle/recovery decision
  authority;
- cross-platform cooperative stop, forced process-tree termination and confirmed exit;
- generation-fenced worker publications, latest-state ingress and subscriptions;
- supervisor-owned incident identity across generation changes;
- session-aware LiveTrading, OffTrading and Closed watchdog policies;
- one independent central operations-health registry and immutable snapshot;
- end-to-end bounded instrumentation, typed refresh routing, queries and operational UI;
- persisted incident transitions and existing watchdog observation integration;
- Development canary activation followed by all-dataset activation; and
- deterministic, fault-injection, platform, runtime and soak qualification.

### 2.2 Excluded

- moving the supervisor out of the API Server into Aspire during Stage 3;
- replacing NATS or the existing actor/event architecture;
- durable replay of realtime ticks, worker publications or Market Outlook display snapshots;
- option-chain discovery, strategy-owned option ticker leases and selected option-leg recovery;
- order placement or order-execution recovery;
- high-frequency operational time-series persistence/export enabled by default; and
- restarting every dataset or the API Server as normal recovery for one dataset failure.

## 3. Required outcomes

| ID | Outcome |
| --- | --- |
| `S3-OUT-01` | Each active dataset runs in a separate child process with one generation identity. |
| `S3-OUT-02` | One failed dataset can be cooperatively reset or forcibly replaced without restarting healthy datasets or the API Server. |
| `S3-OUT-03` | No old and replacement generation can both publish or mutate accepted current state. |
| `S3-OUT-04` | The watchdog applies the approved session-aware cadence and escalation policy using monotonic elapsed time. |
| `S3-OUT-05` | A central immutable snapshot identifies health, freshness, backlog and latency from native receipt through UI delivery. |
| `S3-OUT-06` | Monitoring and process supervision remain usable when a monitored worker or Market Outlook processor stops. |
| `S3-OUT-07` | Recovery, coalescing, saturation, stale-generation rejection and forced termination are observable and never silent. |
| `S3-OUT-08` | Windows and Linux reclaim the entire failed worker process tree before a replacement is admitted. |

## 4. Binding architectural decisions

### 4.1 Authority boundaries

`S3-ARC-01` — `DatabentoMarketDataWatchdogService` remains the only component that decides to
start, cooperatively reset, stop, terminate or replace a dataset. Manual commands, scheduled probes,
session transitions, terminal faults and worker exits enter its existing serialized operation
boundary.

`S3-ARC-02` — `DatasetWorkerSupervisor` executes the watchdog's lifecycle decisions. It observes
process exit and reports it immediately, but it does not independently restart a dataset.

`S3-ARC-03` — `MarketDataOperationsHealthService` is independent of the watchdog, Market Outlook
processor and dataset workers. It records bounded measurements and publishes immutable health
snapshots without becoming a lifecycle owner.

`S3-ARC-04` — Session state and PostgreSQL contract-role assignments remain authoritative above
workers. A worker never selects its own value date, contract role or replacement contract.

`S3-ARC-05` — Desired subscription ownership is supervisor/API state above the replaceable worker.
A worker owns only the realized subscriptions for its current generation.

### 4.2 Process topology

```text
API Server process
  |- FuturesMarketSessionAuthority
  |- DatabentoMarketDataWatchdogService       decision authority
  |- DatasetWorkerSupervisor                  process execution
  |- DatasetPublicationIngress                generation admission/fencing
  |- DatasetDesiredSubscriptionRegistry       desired intent
  |- MarketDataOperationsHealthService        independent observation
  |- existing analytics / Market Outlook / API / actor / UI boundaries
  |
  +-- dataset worker process: GLBX.MDP3 generation G1
  |     `- native feed -> ring -> drain -> channels -> aggregation -> realtime output
  |
  `-- dataset worker process: XCBF.PITCH generation G2
        `- native feed -> ring -> drain -> channels -> aggregation -> realtime output
```

`S3-ARC-06` — One worker process owns exactly one dataset and at most one active generation. It must
not create, supervise or restart another worker.

`S3-ARC-07` — The worker process owns its native handle, producer threads, native ring, managed drain,
bounded channels, aggregation worker, local latest-state calculation and generation cancellation.
Operating-system exit must reclaim all of them.

`S3-ARC-08` — Stage 3 initially runs the supervisor inside the API Server deployment. A later Aspire
extraction may move orchestration without changing authority, protocol or fencing semantics.

### 4.3 Control plane and data plane

`S3-ARC-09` — Supervisor/worker control uses two dedicated inherited anonymous pipes with
length-prefixed MessagePack frames. It must work on supported Windows and Linux .NET runtimes and
must not share standard output/error logging streams.

`S3-ARC-10` — Control frames are bounded to 256 KiB, versioned and sequenced. Every frame carries
protocol version, message kind, worker instance ID, dataset, value date, generation ID, correlation
ID and monotonically increasing sender sequence. Unknown protocol major versions are rejected;
unknown optional fields in the same major version are ignored.

`S3-ARC-11` — The control protocol supports at least:

- `WorkerHello` / `SupervisorHello`;
- `StartManifest` / `StartAccepted` / `StartRejected`;
- `HealthSnapshot`;
- `ApplySubscriptionManifest` / `SubscriptionManifestApplied`;
- `CooperativeReset` / `ResetCompleted` / `ResetFailed`;
- `GracefulStop` / `Stopped`;
- `TerminalFault`; and
- `ProtocolError`.

`S3-ARC-12` — Worker realtime output uses versioned, generation-stamped dataset publication
envelopes over the existing NATS realtime infrastructure. It must not use JetStream durable replay.
The API-side ingress translates accepted envelopes into existing cache/event boundaries.

`S3-ARC-13` — Loss or unavailability of realtime transport is explicit publication failure and
health degradation. Recovery resumes from fresh provider data; it does not replay an old tick or
Market Outlook backlog into the live system.

### 4.4 Worker startup manifest

`S3-ARC-14` — A parent-generated immutable manifest supplies:

- protocol/configuration version;
- worker instance ID and generation ID;
- dataset, criticality and active value date;
- resolved contracts and role identities;
- desired subscription manifest and revision;
- native backend and validated feed options;
- publication subjects and bounded capacities; and
- qualification and shutdown deadlines.

Provider credentials and database connection strings must not appear in process command-line
arguments or diagnostic payloads. Secrets are resolved using the deployment's existing protected
configuration boundary and are never returned in health snapshots.

## 5. Identity, admission and generation fencing

### 5.1 Identity model

| Identity | Owner | Lifetime |
| --- | --- | --- |
| Dataset | Contract/session authority | Stable across generations |
| Worker instance ID | Supervisor | One OS process |
| Generation ID | Watchdog/supervisor | One admitted runtime generation |
| Incident ID | Watchdog | One continuous unhealthy episode across generations |
| Manifest revision | Desired-subscription registry | Monotonic for dataset/value date |
| Publication sequence | Worker generation | Monotonic within one generation |

`S3-FENCE-01` — Incident state belongs above generation state. A cooperative reset, process exit or
replacement never clears incident age or attempt count.

`S3-FENCE-02` — The API maintains an atomic admission record per dataset containing active value
date, admitted worker instance, admitted generation and minimum manifest revision.

`S3-FENCE-03` — Every worker publication, latest-state mutation, subscription acknowledgement and
health snapshot is rejected unless all admission identities match. Rejections increment a bounded
stale-generation counter and retain bounded diagnostic identity.

`S3-FENCE-04` — Before stopping a generation, the supervisor closes publication and cache admission
for that identity. During replacement, the dataset route is unavailable rather than served by stale
state.

`S3-FENCE-05` — A replacement is admitted only after:

1. the earlier process is confirmed exited;
2. expected contracts and subscriptions are present;
3. native producer, ring, managed drain and aggregation worker are operational;
4. no terminal state or ring overrun is present;
5. producer-to-consumer/aggregation progress qualifies when work exists; and
6. the host-side latest-state route is cleared for the replaced dataset.

`S3-FENCE-06` — Publication sequences cannot cross generations. Duplicate or decreasing sequence
within an admitted generation is rejected and measured.

## 6. Worker lifecycle

### 6.1 State machine

```text
Absent -> Starting -> Qualifying -> Running
   ^          |           |           |
   |          v           v           v
   +------ Failed <--- Stopping <- Resetting
   |                              |
   +---- Terminating <- StopTimedOut
```

The supervisor exposes `Absent`, `Starting`, `Qualifying`, `Running`, `Resetting`, `Stopping`,
`StopTimedOut`, `Terminating`, `Exited` and `Failed`. Watchdog incident state is separate.

`S3-LIFE-01` — Startup requires a successful protocol handshake before sending the manifest.
Handshake, start and qualification each have explicit deadlines.

`S3-LIFE-02` — Cooperative reset preserves the OS process when possible but completely recreates
the dataset generation: native feed, drain, buffers, aggregation/session/sequence state and local
latest values. Desired subscription intent is restored from the parent manifest.

`S3-LIFE-03` — A graceful stop closes admission, requests cancellation, forces native wake/unblock,
waits within its deadline and sends `Stopped` only after owned resources are quiescent.

`S3-LIFE-04` — A worker never treats loss of its supervisor control channel as permission to keep
running indefinitely. It begins bounded self-shutdown; the parent containment object remains the
final kill guarantee.

`S3-LIFE-05` — Unexpected process exit, protocol disconnect, native terminal fault and explicit
managed-worker terminal notification signal the watchdog immediately outside scheduled polling.

## 7. Session-aware watchdog policy

### 7.1 Policy table

| Session state | Dataset worker | Scheduled probe | Cooperative dataset reset | Process escalation |
| --- | --- | ---: | --- | --- |
| `LiveTrading` | Running | Every 1 minute | At most once per scheduled unhealthy probe | Terminate and replace only the affected worker after five continuously unhealthy minutes or five unsuccessful cooperative attempts, whichever occurs first |
| `OffTrading` | Running | Every 5 minutes | After the same causal unhealthy incident persists for 15 elapsed minutes | Terminate and replace only the affected worker if bounded cooperative reset cannot stop and qualify it |
| `Closed` | Stopped | None | None | None; session close owns normal shutdown |

`S3-WD-01` — All incident durations use `TimeProvider` monotonic elapsed time. Wall-clock changes and
probe count do not prove an elapsed deadline.

`S3-WD-02` — Scheduled probes read the supervisor's latest bounded worker snapshot. They do not call
Databento, rebuild Market Outlook or perform database I/O.

### 7.2 LiveTrading

`S3-WD-03` — The first causal unhealthy observation opens an incident and may issue one cooperative
reset. Each later scheduled unhealthy probe may issue at most one additional cooperative reset.

`S3-WD-04` — No more than five cooperative reset attempts may occur within one continuous live
incident. When either five unsuccessful attempts or five elapsed unhealthy minutes is reached, the
watchdog orders process replacement.

`S3-WD-05` — A cooperative reset that qualifies a replacement generation does not immediately erase
the incident. One complete healthy LiveTrading minute is required to close it and clear its attempt
count. Becoming unhealthy before qualification completes continues the same incident.

### 7.3 OffTrading

`S3-WD-06` — A causally unhealthy OffTrading observation opens/continues an incident but does not
reset until 15 monotonic minutes have elapsed. The scheduled cadence is five minutes.

`S3-WD-07` — At the 15-minute boundary, the watchdog issues one bounded cooperative reset. Success
must include replacement qualification. If it cannot stop and qualify, the watchdog immediately
orders affected-process replacement.

`S3-WD-08` — A quiet provider is healthy when there is no upstream/buffered work and all lifecycle,
subscription and worker evidence is operational. Message age alone does not reset an OffTrading
worker.

### 7.4 Closed and transitions

`S3-WD-09` — Closed has no active dataset worker and no recurring dataset health probe. The
supervisor may still observe expected process exit and retain an immutable Inactive snapshot.

`S3-WD-10` — On OffTrading to LiveTrading, the watchdog performs an immediate transition probe. If
unhealthy, it may cooperatively reset and starts the live escalation window; prior tolerated
OffTrading time does not cause an immediate process kill.

`S3-WD-11` — On LiveTrading to OffTrading, the live escalation clock stops advancing and the
OffTrading 15-minute policy applies while retaining incident identity and diagnostics.

`S3-WD-12` — Session close records and ends the active incident for that value date after stopping
the worker. A new active value date starts with a new incident scope; historical evidence remains
persisted.

### 7.5 Immediate terminal events

`S3-WD-13` — A still-running worker that reports native or managed terminal failure receives one
immediate cooperative reset. If that command times out/fails, the affected process is terminated
without waiting for the next scheduled deadline.

`S3-WD-14` — An already-exited worker cannot be cooperatively reset. The watchdog immediately enters
the process-replacement path, subject to crash-loop protection.

## 8. Forced process replacement

`S3-PROC-01` — Process replacement performs this exact sequence:

1. close publication, latest-state and subscription admission for the failed generation;
2. persist the incident transition and replacement reason;
3. issue bounded graceful stop when the process is responsive;
4. wait the configured graceful-stop deadline;
5. terminate the entire worker process tree when it has not exited;
6. wait for confirmed exit and record exit code, graceful outcome and forced-termination result;
7. clear host mirrors/latest values for only that dataset;
8. construct a new manifest/generation from authoritative contract and subscription state;
9. start and qualify the replacement; and
10. atomically admit its identity and realtime output.

`S3-PROC-02` — PID alone is insufficient identity. Termination targets the supervisor-held process
handle plus worker instance/start identity so PID reuse cannot kill an unrelated process.

`S3-PROC-03` — Windows containment uses a Job Object configured with
`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`; explicit tree termination is followed by handle-based exit
confirmation.

`S3-PROC-04` — Linux containment launches each worker in its own process group, sends `SIGTERM`, then
`SIGKILL` to the group after the graceful deadline, and waits/reaps the child before replacement.

`S3-PROC-05` — The worker executable path is a configured/validated absolute path. The supervisor
uses `ProcessStartInfo.ArgumentList` and never constructs a shell command.

`S3-PROC-06` — Parent shutdown first requests bounded graceful stop for every worker and then closes
containment handles/groups. Orphan dataset workers are not permitted.

### 8.1 Crash-loop protection

`S3-PROC-07` — Process replacement uses bounded backoff of 5 seconds, 30 seconds and 2 minutes for
successive replacement failures in the same incident.

`S3-PROC-08` — Three failed process replacements within a rolling 15-minute incident latch that
dataset `Failed` and stop automatic restart until an authorized manual reset, a material manifest
revision or a new active value date. Healthy datasets remain running.

`S3-PROC-09` — A manual reset enters the same serialized watchdog path. It clears the process
replacement latch only after recording operator identity/reason and never bypasses generation
fencing or qualification.

## 9. Desired subscriptions and host state

`S3-SUB-01` — `DatasetDesiredSubscriptionRegistry` owns a monotonic manifest per dataset/value date.
The fixed ES/VX contract role set is populated from Stage 2 contract authority. Later transient
option ownership is reserved for Stage 4.

`S3-SUB-02` — Subscription changes are idempotent by manifest revision. A worker acknowledges the
complete realized revision, not a partial best-effort state.

`S3-SUB-03` — After cooperative reset or process replacement, the complete current manifest is
reapplied before admission. A stale worker acknowledgement cannot advance desired state.

`S3-SUB-04` — API-side current-value mirrors retain stable reader identity where required but clear
all affected dataset values before admitting a replacement. Unaffected dataset mirrors are not
changed.

## 10. Central operations-health service

### 10.1 Recording boundary

`S3-HEALTH-01` — `MarketDataOperationsHealthService` implements/extends the existing
`IMarketDataOperationsRecorder` compatibility boundary. Recording is non-throwing, non-blocking and
does not perform network or database I/O.

`S3-HEALTH-02` — Current counters use atomics or a bounded local channel. Snapshot publication uses
immutable references. When a bounded lane saturates, the service coalesces latest state by a bounded
registered key and records saturation.

`S3-HEALTH-03` — Dimensions are registry controlled: stage, active contract role, dataset, bounded
update kind and bounded outcome/reason. GUIDs, process IDs, exception text, symbols not in the active
manifest and arbitrary user values are prohibited metric labels.

### 10.2 Required stages

The combined snapshot must independently address:

- native connection/heartbeat and provider receipt;
- native ring and consumer progress;
- managed interop/drain;
- normalization and bounded channels;
- tick aggregation and latest-state publication;
- RSI, TDI, ITI, EMA, Bollinger Band, VX, EOD and Futures Trade Signal;
- Market Outlook update queue, processor, cache and realtime publication;
- dataset worker process/control state;
- generation-fenced host ingress; and
- UI delivery acknowledgement where the client boundary supplies one.

`S3-HEALTH-04` — For every registered key, the snapshot exposes applicable totals, last receipt,
last success, last failure, source-as-of time, pending/backlog, oldest pending age, recent latency
distribution, current status and a bounded reason code.

`S3-HEALTH-05` — Latency uses fixed-memory histograms or fixed buckets. No per-event history grows
without bound.

### 10.3 Status evaluation

| Status | Meaning |
| --- | --- |
| Green | Required stage operational and within current session thresholds |
| Yellow | Suspect/freshness/backlog warning inside its confirmation window |
| Orange | Reset/replacement in progress or optional capability unavailable |
| Red | Required capability unavailable, failed or recovery-latched |
| Inactive | Intentionally stopped by authoritative session policy |

`S3-HEALTH-06` — Overall status is a deterministic composition of required stage statuses. It must
retain the exact failing stage/reason rather than collapse everything into generic stale data.

`S3-HEALTH-07` — The service remains queryable from its last immutable state if any one worker,
watchdog operation or Market Outlook processor is paused. A failure of the health service recorder
must not propagate into those components.

## 11. Refresh and query behavior

`S3-API-01` — Current combined-health and stage-detail queries read immutable service state. They do
not call a worker, native ABI or database synchronously.

`S3-API-02` — Refresh targets are typed and route to the existing owner:

| Target | Owning action |
| --- | --- |
| Status | Re-evaluate current central snapshot only |
| Dataset probe | Ask watchdog for serialized immediate probe |
| Dataset reset | Authorized watchdog cooperative reset |
| Dataset process replacement | Authorized watchdog escalation, never a health-service decision |
| Market Outlook | Existing local recompose |
| Individual analytic | Existing owning calculation/requery boundary |

`S3-API-03` — Requested, started, completed, rejected and failed refresh outcomes are measured and
correlated. An external command receives a typed completion/failure result; local refresh objects
remain local.

`S3-API-04` — Queries and UI must distinguish `last observed`, `last healthy`, `incident opened`,
`generation started` and `snapshot source-as-of` times.

## 12. Persistence

`S3-STORE-01` — High-frequency health samples, counters and latency buckets remain process-local.

`S3-STORE-02` — PostgreSQL persists bounded dataset incident transitions: incident/dataset/value
date, state, bounded reason, opened/updated/closed time, cooperative attempt count, process
replacement count, worker/generation identity, exit/graceful/forced result, correlation ID and
operator reason when applicable.

`S3-STORE-03` — Persistence occurs outside hot recording and worker data paths. Persistence failure
is visible but cannot block producer, aggregation or Market Outlook work.

`S3-STORE-04` — Existing watchdog observations continue. Stage 3 may reference an incident ID and
summary but must not place unbounded process logs, exception stacks or metric series in an
observation row.

`S3-STORE-05` — Restart hydration restores open incident/replacement-latch evidence conservatively.
It never admits a worker generation that no longer has a live, supervisor-owned process handle.

## 13. Configuration

The Stage 3 surface replaces the single Stage 2 scheduled poll/stall policy with explicit values.

| Setting | Default | Validation |
| --- | ---: | --- |
| `LiveTradingPollInterval` | 1 minute | Positive |
| `LiveTradingEscalationWindow` | 5 minutes | At least poll interval |
| `LiveTradingMaximumCooperativeAttempts` | 5 | 1-10 |
| `LiveTradingHealthyQualificationPeriod` | 1 minute | At least poll interval |
| `OffTradingPollInterval` | 5 minutes | Positive |
| `OffTradingStallTimeout` | 15 minutes | At least two poll intervals |
| `WorkerHandshakeTimeout` | 10 seconds | Positive |
| `WorkerStartTimeout` | 30 seconds | Positive |
| `WorkerCommandTimeout` | 10 seconds | Positive |
| `WorkerGracefulStopTimeout` | 10 seconds | Positive |
| `WorkerForceKillTimeout` | 5 seconds | Positive |
| `WorkerQualificationTimeout` | 30 seconds | Positive |
| `MaximumProcessReplacementsPerIncident` | 3 | 1-10 |
| `ProcessReplacementWindow` | 15 minutes | Greater than maximum configured backoff |
| `ControlFrameMaximumBytes` | 262,144 | Fixed upper safety bound |

Stage 2 compatibility keys remain readable for rollback during Development canary but cannot
silently override an enabled Stage 3 policy. Invalid mixed configuration fails startup with a clear
message.

## 14. Operational UI

`S3-UI-01` — Feed/operations health is clickable and shows overall status plus per-stage and
per-dataset rows.

`S3-UI-02` — Dataset detail shows worker PID/instance, generation, session state, incident age,
cooperative attempts, replacement count, next probe, native/managed progress, backlog, last source
time, graceful/forced outcome and bounded failure reason.

`S3-UI-03` — The UI separates status refresh, cooperative reset and authorized process replacement;
destructive recovery requires the existing authorization/audit boundary and confirmation.

`S3-UI-04` — Planned closure is Inactive, not Red. Existing market-hours navigation and dependent
capability gating remain in force.

`S3-UI-05` — The UI never infers current health solely from a realtime Market Outlook timestamp. It
uses the central operations-health query and displays Market Outlook source-as-of separately.

## 15. Security and safety

`S3-SEC-01` — Only the supervisor-created inherited pipe endpoints are accepted. The initial
handshake includes a random per-launch bootstrap token that is never logged.

`S3-SEC-02` — Control and health payloads have bounded lengths, enums and collection counts; invalid
payloads terminate the control session and are recorded.

`S3-SEC-03` — Worker arguments contain only non-secret bootstrap identity and inherited handle
references. Credentials, connection strings and API keys are redacted from process, health and
incident diagnostics.

`S3-SEC-04` — Forced termination validates the exact supervisor-owned process identity and target
group/job. It never enumerates and kills processes by executable name.

`S3-SEC-05` — Production process replacement commands and manual latch clearing are audited.

## 16. Compatibility and rollout

`S3-ROLL-01` — A feature flag chooses in-process Stage 2 runtime or supervised Stage 3 workers per
dataset. Mixed mode is allowed only during Development canary; one dataset has exactly one owner.

`S3-ROLL-02` — The first canary uses one dataset in Development. Fault injection and soak must pass
before enabling every dataset in Development.

`S3-ROLL-03` — Production remains on the Stage 2 path until Stage 3 acceptance evidence is reviewed.
Rollback stops supervised workers, confirms exit, clears their admission and starts the Stage 2
in-process owner through the same serialized watchdog boundary.

`S3-ROLL-04` — Existing external API, actor/query and UI contracts remain compatible unless a
versioned Stage 3 extension is explicitly documented.

## 17. Verification requirements

### 17.1 Deterministic tests

- state-machine tests for every worker and incident transition;
- fake-time tests for every LiveTrading/OffTrading deadline and transition;
- exactly one reset per unhealthy scheduled probe and maximum five per live incident;
- one full healthy live minute before incident closure;
- incident continuity across generation replacement;
- crash-loop backoff/latch and authorized clearing;
- stale/duplicate/decreasing publication rejection; and
- bounded dimension, payload, histogram and saturation tests.

### 17.2 Integration and fault injection

- worker handshake, malformed frame, disconnect and command timeout;
- native terminal, producer stop, lost drain progress, ring overrun, full managed channel and blocked
  aggregation;
- graceful reset success, graceful stop timeout and forced kill;
- unexpected worker exit and descendant process cleanup;
- NATS unavailable/reconnect without stale replay;
- incident persistence failure without hot-path failure;
- exact subscription-manifest restoration; and
- proof that healthy dataset PIDs/generations do not change during another dataset's replacement.

### 17.3 Platform evidence

Windows and Linux each require tests proving:

1. the complete process tree is terminated;
2. exit is confirmed before replacement admission;
3. no orphan remains after parent shutdown;
4. PID reuse cannot target another process; and
5. cancellation/deadlines do not hang shutdown.

### 17.4 Runtime and soak

- Development canary during LiveTrading and OffTrading;
- deliberate unresponsive worker replacement with API/UI and healthy datasets continuously usable;
- repeated replacement resource stability: managed/native memory, handles, threads, process count,
  ring and backlog;
- C++ and Rust worker parity; and
- sustained native-to-UI counter reconciliation and latency evidence.

## 18. Acceptance criteria

Stage 3 is accepted only when all are true:

1. each active dataset is contained in exactly one supervised process;
2. one combined immutable snapshot identifies health at every required native-to-UI stage;
3. watchdog policy matches section 7 under deterministic time;
4. an unresponsive dataset process is forcibly replaced without restarting API/UI or a healthy
   dataset;
5. the old process tree is confirmed exited before replacement admission on Windows and Linux;
6. stale generations cannot publish, mutate current state or acknowledge subscriptions;
7. incident age/attempts survive cooperative and process generation changes;
8. central monitoring remains queryable during each injected worker/processor failure;
9. all loss, coalescing, saturation and recovery paths are explicit and measured;
10. current metrics remain bounded and persistence stays off hot paths;
11. Development canary, all-dataset qualification, C++/Rust parity, UI journeys and soak pass;
12. production enablement and rollback are documented and rehearsed; and
13. the final as-built document and execution record contain no unexplained partial gate.

## 19. Stage 4 handoff

Stage 3 provides the worker containment, desired-subscription registry, generation fencing and
central health boundary that Stage 4 will extend. Stage 4 may add option-chain session and
reference-counted option ticker intent, but it must not weaken Stage 3 process ownership,
qualification, session policy or stale-generation rejection.
