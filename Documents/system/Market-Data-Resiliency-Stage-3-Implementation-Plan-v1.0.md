# Market Data Resiliency Stage 3 Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Plan ID | `MDR-S3-IMP` |
| Status | Ready for review; implementation not started |
| Version | 1.0 |
| Date | 2026-09-04 |
| Specification | `Documents/system/Market-Data-Resiliency-Stage-3-Specification-v1.0.md` |
| Accepted baseline | `Documents/system/Market-Data-Resiliency-As-Built-Specification-v1.0.md` |
| Prerequisite | Stage 2 accepted by owner on 2026-09-04 |
| Rollout | One Development dataset, all Development datasets, production readiness, production enablement |

## 1. Objective

Implement every binding `S3-` requirement in the Stage 3 specification without weakening the
accepted Stage 1/2 behavior. The work is divided into gated packages so process extraction,
generation fencing, recovery timing and production rollout cannot be enabled partially or in an
unsafe order.

Stage 3 implementation is not complete when the worker executable merely starts. Completion
requires forced containment, central health, UI, both platform implementations, all fault evidence,
an updated as-built specification and explicit acceptance.

## 2. Execution rules

1. Only one gate is `In progress` at a time unless two gates are explicitly marked parallel-safe.
2. Every behavioral change begins with a failing characterization or requirement test.
3. The supervised runtime remains disabled by default until the Development canary gate.
4. One dataset has exactly one lifecycle owner in every mode; Stage 2 and Stage 3 runtimes never
   admit the same dataset concurrently.
5. Generation-fenced host ingress must exist before any worker publication can enter production
   caches or domain events.
6. A replacement worker is never launched before confirmed exit of the earlier process.
7. Tests use `TimeProvider`/fake monotonic time. Wall-clock sleeps are not accepted as policy proof.
8. C++ and Rust behavior changes are implemented and qualified together.
9. High-frequency recording remains non-blocking and contains no database/network call.
10. The Stage 2 feature flag is retained as the rollback path until Stage 3 acceptance.
11. Production enablement is a separate reviewed configuration change after technical acceptance.
12. Unrelated dirty-worktree changes are preserved and are not included in Stage 3 commits.

## 3. Proposed repository structure

Names are binding unless implementation discovers a project-layer cycle; any rename must be
recorded in the execution record.

```text
TomasAI.IFM.Application.MarketData/
  OperationsHealth/
    MarketDataOperationsHealthService.cs
    MarketDataOperationsHealthSnapshot.cs
    MarketDataOperationsHealthPolicy.cs
  DataBento/Workers/
    Contracts/
    DatasetWorkerSupervisor.cs
    DatasetWorkerIncidentStateMachine.cs
    DatasetWorkerAdmissionRegistry.cs
    DatasetDesiredSubscriptionRegistry.cs
    DatasetPublicationIngress.cs
    AnonymousPipeWorkerControlChannel.cs
    Platform/
      WindowsDatasetProcessContainment.cs
      LinuxDatasetProcessContainment.cs

TomasAI.IFM.Application.MarketData.Worker/
  Program.cs
  DatasetWorkerHost.cs
  DatasetWorkerControlService.cs
  DatasetWorkerRuntime.cs

TomasAI.IFM.Application.MarketData.Worker.IntegrationTests/
  worker/control/process/containment tests
```

Shared worker control contracts remain in `TomasAI.IFM.Application.MarketData` so the API Server and
worker can use the same assembly without a circular reference. Wire DTOs must not reference native
handles, database contexts, actors or UI types.

## 4. Gate summary

| Gate | Name | Depends on | Enablement effect |
| --- | --- | --- | --- |
| `S3G-00` | Baseline and acceptance record | Stage 2 | None |
| `S3G-01` | Characterization and architecture guards | `S3G-00` | None |
| `S3G-02` | Contracts, configuration and protocol | `S3G-01` | None |
| `S3G-03` | Central operations-health core | `S3G-02` | Query-only local snapshot |
| `S3G-04` | Incident state machine and session policy | `S3G-02` | Disabled policy implementation |
| `S3G-05` | Worker host extraction | `S3G-02` | Standalone synthetic worker only |
| `S3G-06` | Supervisor and local control channel | `S3G-04`, `S3G-05` | Disabled supervised worker |
| `S3G-07` | Generation-fenced publication and subscriptions | `S3G-06` | Synthetic end-to-end only |
| `S3G-08` | Windows/Linux forced containment | `S3G-07` | Fault-test only |
| `S3G-09` | Watchdog integration and persistence | `S3G-08` | Feature-flagged runtime |
| `S3G-10` | End-to-end instrumentation, queries and refresh | `S3G-03`, `S3G-09` | Stage 3 API available |
| `S3G-11` | Operational UI | `S3G-10` | Development UI available |
| `S3G-12` | One-dataset Development canary | `S3G-11` | One selected Development dataset |
| `S3G-13` | All-dataset/platform qualification | `S3G-12` | All Development datasets |
| `S3G-14` | Production readiness and rollback rehearsal | `S3G-13` | Production-ready, still disabled |
| `S3G-15` | Acceptance and as-built handoff | `S3G-14` | Reviewed production enablement |

`S3G-03`, `S3G-04` and `S3G-05` are parallel-safe after contracts freeze because they modify
separate responsibilities. Their integration still occurs only at `S3G-06`.

### Specification traceability

| Specification requirements | Primary implementation gate | Final proof |
| --- | --- | --- |
| `S3-OUT-*` | All gates | `S3G-15` acceptance mapping |
| `S3-ARC-01` through `S3-ARC-05` | `S3G-01`, `S3G-04`, `S3G-09` | Lifecycle ownership architecture gate |
| `S3-ARC-06` through `S3-ARC-14` | `S3G-02`, `S3G-05`, `S3G-06`, `S3G-07` | Worker/control/data-plane integration |
| `S3-FENCE-*` | `S3G-07` | Stale-generation and concurrent replacement fault suite |
| `S3-LIFE-*` | `S3G-05`, `S3G-06` | Worker lifecycle/control-disconnect suite |
| `S3-WD-*` | `S3G-04`, `S3G-09` | Deterministic fake-time and concurrency suite |
| `S3-PROC-*` | `S3G-08` | Native Windows/Linux process-tree suite |
| `S3-SUB-*` | `S3G-07` | Manifest restore and host-mirror suite |
| `S3-HEALTH-*` | `S3G-03`, `S3G-10` | Bounded registry and end-to-end reconciliation suite |
| `S3-API-*` | `S3G-10` | Actor/REST/query/refresh suite |
| `S3-STORE-*` | `S3G-09` | PostgreSQL integration and restart-hydration suite |
| `S3-UI-*` | `S3G-11` | Presentation unit and UI system journeys |
| `S3-SEC-*` | `S3G-02`, `S3G-06`, `S3G-08`, `S3G-14` | Protocol, secret-redaction and exact-target tests |
| `S3-ROLL-*` | `S3G-12`, `S3G-14`, `S3G-15` | Canary, rollback rehearsal and acceptance |

## 5. S3G-00 — Baseline and Stage 2 acceptance record

### Work

- record the 2026-09-04 Stage 2 owner acceptance and non-blocking elapsed-soak waiver;
- freeze the Stage 1/2 as-built specification as the comparison baseline;
- list current dirty-tree changes and assign ownership before Stage 3 edits;
- capture current API/UI build, managed tests, C++/Rust tests and native ABI manifest; and
- create a Stage 3 execution-record section in this plan when implementation starts.

### Exit evidence

- baseline commands, commit identity, platform and result artifacts are recorded;
- no Stage 2 current behavior is described as Stage 3 behavior; and
- lifecycle ownership architecture check passes before production changes.

## 6. S3G-01 — Failing characterization and architecture guards

### Work

Add failing-first tests proving the gaps Stage 3 must close:

- a non-cooperative in-process generation cannot be safely killed without losing the API process;
- Stage 2 incident state is generation-scoped and must move above generation;
- a stale publication currently lacks host generation rejection;
- a worker exit needs immediate serialized watchdog delivery;
- current polling does not implement separate live/off-hours schedules; and
- existing local metrics do not produce one native-to-UI immutable snapshot.

Extend `scripts/Test-DatabentoLifecycleOwnership.ps1` so future code fails if:

- a worker restarts itself;
- the supervisor decides health/recovery independently;
- UI or an actor directly starts/kills a worker;
- a second component calls native start/stop outside the worker runtime; or
- process killing targets executable names rather than a supervisor-owned process identity.

### Exit evidence

- each required gap has a focused red test or architecture assertion;
- existing Stage 1/2 passing suites remain unchanged; and
- the tests identify the intended Stage 3 seam rather than merely asserting new type names.

## 7. S3G-02 — Contracts, configuration and protocol freeze

### Work

Implement wire-neutral contracts for:

- `DatasetWorkerIdentity`, `DatasetGenerationIdentity` and `DatasetIncidentIdentity`;
- immutable startup/subscription manifests and monotonic revisions;
- worker lifecycle states and bounded failure/reason enums;
- all control messages in `S3-ARC-11`;
- `DatasetWorkerHealthSnapshot` and bounded native/managed counters;
- `DatasetPublicationEnvelope` with typed payload discriminator;
- `DatasetWorkerAdmission`; and
- Stage 3 options from specification section 13.

Implement one frame codec with:

- 256 KiB maximum before allocation;
- protocol major/minor version;
- length prefix, message kind and sender sequence;
- MessagePack serializer options with explicit known types;
- bounded arrays/strings/error detail;
- cancellation-aware exact reads/writes; and
- deterministic malformed/truncated/unknown-frame errors.

### Tests

- round-trip every message and payload kind;
- golden compatibility frames for the frozen major version;
- maximum/minimum length, truncation, oversized collection and invalid enum;
- duplicate/decreasing sender sequence;
- wrong dataset/value date/worker/generation/correlation identity;
- option validation boundaries; and
- secret-redaction checks for manifest/log formatting.

### Exit evidence

- protocol v1 is frozen and documented;
- invalid configuration fails before launching a worker; and
- shared contracts introduce no project cycle or native/UI dependency.

## 8. S3G-03 — Central operations-health core

### Work

- implement `MarketDataOperationsHealthService` independently from the watchdog;
- preserve `IMarketDataOperationsRecorder` compatibility and adapt Stage 1/2 calls;
- register a bounded set of stage/dataset/role/update-kind keys;
- implement atomic counters, last timestamps, backlog and fixed-memory latency histograms;
- implement bounded latest-state coalescing if the internal lane saturates;
- publish one immutable combined snapshot with deterministic Green/Yellow/Orange/Red/Inactive
  evaluation; and
- retain bounded recent diagnostic identity separately from metric labels.

### Tests

- concurrent producers and immutable readers;
- recorder exception containment and zero synchronous I/O;
- saturation/coalescing visibility;
- fixed upper memory/cardinality under arbitrary input identity;
- exact latency-bucket boundaries;
- stopped Market Outlook processor and stopped worker remain visible/queryable; and
- overall status retains the exact failing stage/reason.

### Exit evidence

- health recording adds no wait to the market-data path;
- snapshot reads call neither native code nor a database; and
- resource tests demonstrate bounded keys and histograms.

## 9. S3G-04 — Incident state machine and session-aware policy

### Work

Implement a dataset incident state machine above generation with:

- incident ID, dataset/value date, monotonic opened/updated/healthy-since times;
- bounded reason, cooperative attempt count and process replacement count;
- current worker/process/generation identity;
- next probe/deadline and last outcome;
- process-replacement latch and authorized clearing reason; and
- deterministic session transition functions.

Replace Stage 2 scheduled timing only behind the Stage 3 feature flag:

- LiveTrading: one-minute probe, one cooperative reset per unhealthy scheduled probe, five-minute
  or five-attempt escalation, one healthy minute to close;
- OffTrading: five-minute probe, 15 elapsed unhealthy minutes before cooperative reset, immediate
  process escalation if reset fails;
- Closed: stop and no recurring probe;
- terminal/exit events: immediate out-of-cycle evaluation; and
- exact OffTrading/LiveTrading transition behavior from `S3-WD-10` and `S3-WD-11`.

### Deterministic tests

- wall-clock jump forward/backward does not alter monotonic incident age;
- one live probe per minute and never more than one reset from that probe;
- attempt/deadline boundary and fifth-attempt race result in one escalation;
- healthy for 59.999 seconds does not close; full minute closes;
- new generation does not clear incident or attempts;
- quiet provider remains healthy;
- OffTrading 14:59 does not reset and 15:00 does;
- OffTrading reset success/failure branches;
- both session transition directions;
- Closed has no scheduled probe; and
- simultaneous timer/terminal/manual triggers serialize to one action.

### Exit evidence

- the state machine is pure/deterministic apart from injected `TimeProvider`;
- policy tests contain no real-time sleeps; and
- Stage 2 mode still uses its existing behavior when the feature flag is off.

## 10. S3G-05 — Single-dataset worker host extraction

### Work

- create `TomasAI.IFM.Application.MarketData.Worker` as a console executable;
- extract/reuse the dataset-generation portion of `DatabentoMarketDataEpoch` behind
  `IDatasetWorkerRuntime`;
- enforce exactly one dataset/start manifest per worker process;
- move native handle/ring, drain, channels, aggregation and local dataset state into the worker;
- implement cooperative reset as full same-process generation reconstruction;
- implement bounded start/qualification/stop and supervisor-channel-loss shutdown;
- keep contract authority, session authority, desired subscription state and lifecycle decisions out
  of the worker; and
- support synthetic C++ and Rust backends before live provider mode.

### Tests

- worker rejects a second dataset and mismatched value date;
- startup creates exactly one native dataset generation;
- cooperative reset replaces every dataset-scoped object and clears last values;
- desired manifest restores before qualification;
- cancellation unblocks native wait and managed channels;
- lost control channel causes bounded self-stop; and
- worker exit leaves no native handle/thread in its process test harness.

### Exit evidence

- standalone synthetic worker can start, qualify, reset and stop for both native backends;
- worker has no PostgreSQL contract-selection or watchdog decision dependency; and
- in-process Stage 2 remains the default API runtime.

## 11. S3G-06 — Supervisor and anonymous-pipe control

### Work

- implement two dedicated inherited anonymous pipes per worker;
- generate one random bootstrap token and worker identity per launch;
- use `ProcessStartInfo.ArgumentList`, no shell and a validated absolute executable path;
- implement handshake, command correlation, deadlines and single-reader/single-writer control loops;
- implement process-exit observation as an immediate watchdog signal;
- implement supervisor current-state snapshots independent of command waits;
- implement bounded shutdown of every supervised child; and
- add a fake process adapter for deterministic supervisor tests.

### Tests

- valid and invalid bootstrap handshake;
- child never connects, connects late, sends wrong identity or wrong version;
- fragmented and concurrent pipe traffic preserves frames/sequences;
- command timeout does not block health query;
- child exit and pipe disconnect signal watchdog exactly once;
- API shutdown stops all children; and
- no secret appears in command line, logs or returned snapshot.

### Exit evidence

- a synthetic child is supervised on Windows and Linux CI/runtime targets;
- monitoring remains queryable while a child ignores commands; and
- supervisor never decides or initiates an unrequested restart.

## 12. S3G-07 — Generation-fenced data ingress and subscriptions

### Work

- implement atomic `DatasetWorkerAdmissionRegistry`;
- close admission before reset/stop/replacement;
- add generation headers/envelopes to every worker realtime output;
- implement `DatasetPublicationIngress` validation and translation to existing event/cache paths;
- reject stale value date, worker, generation, manifest revision and publication sequence;
- implement the supervisor-owned desired-subscription registry;
- clear only affected host latest-state mirror slots before replacement;
- preserve stable reader identities required by existing API consumers; and
- use realtime NATS, not JetStream, for worker output.

### Tests

- old publication arriving before, during and after replacement is rejected;
- duplicate/decreasing sequence is rejected;
- replacement cannot publish before qualification/admission;
- host mirror is empty between generations and repopulates from fresh data;
- healthy dataset cache/readers are unchanged;
- manifest revision application is idempotent and complete;
- NATS outage records failure and reconnection does not replay stale data; and
- existing Market Outlook and analytics consumers receive accepted translated events.

### Exit evidence

- no unfenced worker message can reach a production cache or domain event;
- contract and subscription intent survive worker replacement; and
- native-to-Market-Outlook synthetic flow passes for both backends.

## 13. S3G-08 — OS-enforced process containment

### Work

Implement `IDatasetProcessContainment` with:

- Windows Job Object assignment and `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`;
- Windows exact process-handle exit wait and explicit tree termination fallback;
- Linux new process group at launch, `SIGTERM`, deadline, group `SIGKILL` and child reap;
- process handle/start identity verification rather than PID/name matching;
- graceful-stop result, forced flag, exit code/signal and duration evidence; and
- parent-shutdown kill guarantee.

Implement crash-loop protection:

- replacement backoffs 5 seconds, 30 seconds and 2 minutes;
- maximum three failed replacements in a rolling 15-minute incident;
- dataset-only Failed latch;
- authorized manual, manifest-revision or new-value-date clearing; and
- unaffected dataset continuity.

### Fault tests

- worker ignores cancellation and control pipe;
- child creates a descendant that also ignores termination;
- graceful deadline forces entire tree exit;
- exit confirmation precedes replacement admission;
- repeated failures produce exact backoff/latch;
- manual reset is audited and serialized; and
- one dataset kill leaves API and healthy dataset PIDs/generations unchanged.

### Exit evidence

- Windows evidence passes on Windows;
- Linux evidence passes on Linux, not under a mocked Windows implementation;
- no process-name kill or workspace-wide cleanup script is used; and
- no orphan remains after each test.

## 14. S3G-09 — Watchdog integration and incident persistence

### Work

- adapt `IDatabentoLifecycleRuntime` to supervised dataset operations behind the feature flag;
- route scheduled, terminal, worker-exit, manual, rollover and shutdown actions through the existing
  watchdog serialization;
- persist dataset incident transitions and process outcomes in `MarketDataServiceDb`;
- reference incident identity from existing watchdog observations;
- hydrate open incidents/latches conservatively on API restart;
- never hydrate/admit an extinct worker generation; and
- retain Stage 2 whole-epoch rollback behavior while supervised mode is disabled.

### Schema work

Add schema-managed PostgreSQL objects for a bounded current incident plus transition history. Include
indexes for dataset/value date, open status, observed time and correlation ID. Use optimistic row
versioning/idempotent transition identity consistent with the Stage 2 service store.

### Tests

- persistence CRUD, idempotency, optimistic conflict and bounded detail;
- database unavailable while worker data path remains operational;
- API restart with open incident and dead worker;
- rollover closes old value-date incident and starts clean ownership;
- timer/terminal/manual concurrency remains maximum one lifecycle mutation; and
- feature-off path preserves Stage 2 behavior.

### Exit evidence

- every replacement has correlated persisted evidence;
- high-frequency health never writes PostgreSQL; and
- no code outside watchdog/supervisor execution boundary mutates lifecycle.

## 15. S3G-10 — End-to-end instrumentation, queries and refresh

### Work

- instrument native connection/ring, interop/drain, normalization, channels, aggregation and
  publication;
- instrument RSI, TDI, ITI, EMA, Bollinger, VX, EOD and Futures Trade Signal;
- retain Market Outlook queue/processor/cache/realtime measurements;
- instrument worker PID/generation, incident, stop/kill/restart/qualification and fenced rejection;
- add combined-health and stage-detail immutable queries;
- add typed refresh targets/results and external actor/REST boundaries where required; and
- add optional UI delivery acknowledgement without making it a worker recovery input.

### Tests

- counters reconcile across a known synthetic event set from native receipt to UI publication;
- deliberately stop/backlog every registered stage and identify exact stage/reason;
- query continues while worker, watchdog operation or Market Outlook processor is blocked;
- each refresh target reaches only its owner;
- reset/process replacement requires authorization while status refresh does not; and
- metric cardinality/memory remains bounded through repeated generations.

### Exit evidence

- one immutable query contains every required stage;
- no query invokes native/database code; and
- refresh, reset and recompose semantics remain distinct.

## 16. S3G-11 — Operational UI

### Work

- make the operations-health indicator clickable;
- show overall and per-stage rows with status, last observed/healthy/source times, counts, backlog
  and latency;
- add dataset worker detail: PID, worker/generation, session, incident age, attempts, replacements,
  next probe, graceful/forced result and bounded reason;
- distinguish status refresh, cooperative reset and authorized process replacement;
- preserve planned-closure Inactive behavior and navigation gates; and
- clearly separate Market Outlook source staleness from downstream/worker health.

### Tests

- Green/Yellow/Orange/Red/Inactive rendering;
- LiveTrading, OffTrading and Closed timing text;
- exact dataset/stage navigation and failure detail;
- authorization/confirmation/audit for recovery actions;
- UI remains responsive while one worker is hung/terminated; and
- accessibility and bounded long-detail rendering.

### Exit evidence

- runtime journey identifies an injected failure without reading logs; and
- UI never claims durable replay or uses Market Outlook time as the only health source.

## 17. S3G-12 — One-dataset Development canary

### Entry

- `S3G-00` through `S3G-11` pass;
- supervised mode is still off by default; and
- rollback is automated and tested with synthetic data.

### Work

- enable one selected dataset in Development only;
- leave every other dataset on Stage 2 in-process mode with an ownership assertion preventing
  overlap;
- run synthetic and provider-connected healthy flow;
- inject cooperative reset, non-cooperative hang, terminal exit and NATS outage;
- verify API, UI and unaffected dataset continuity; and
- rehearse rollback to Stage 2 ownership.

### Exit evidence

- old process exits before replacement admission;
- stale generation publications are rejected;
- exact session policy is visible in operations health;
- no process/thread/handle/native-memory growth after repeated replacement; and
- canary and rollback artifacts are retained.

## 18. S3G-13 — All-dataset and platform qualification

### Work

- enable all active datasets in Development;
- run C++ and Rust sequential qualification;
- execute Windows and Linux process-tree suites;
- inject failure in each dataset while checking every other PID/generation;
- execute session transitions and repeated value-date rollover;
- reconcile native-to-UI counters and latency; and
- run sustained LiveTrading and OffTrading soak.

### Required suites

```powershell
dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj
dotnet test TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj
dotnet test TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests.csproj
dotnet test TomasAI.IFM.Domain.MarketData.Feed.UnitTests/TomasAI.IFM.Domain.MarketData.Feed.UnitTests.csproj
dotnet test TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.csproj
dotnet test TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.csproj
dotnet test TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.csproj
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests/TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj
```

Also run native C++ synthetic/live builds, Rust `cargo test`, ABI/parity, lifecycle ownership and the
new worker/process-containment suites. Exact commands and artifact paths are added to the execution
record when implementation establishes them.

### Exit evidence

- all affected suites pass on the supported platform matrix;
- healthy dataset generations never change during another dataset failure;
- memory, handles, threads, child process count, ring and backlog remain bounded; and
- provider-connected evidence is explicitly identified rather than inferred from synthetic tests.

## 19. S3G-14 — Production readiness and rollback rehearsal

### Work

- validate worker executable deployment and permissions on Windows and Linux;
- validate secrets/configuration without command-line exposure;
- rehearse parent crash/orphan prevention and service shutdown;
- rehearse one-dataset and full Stage 2 rollback;
- define operator runbook for Green/Yellow/Orange/Red, Failed latch and manual reset;
- set alerts for forced termination, crash-loop latch, stale-generation rejection, control protocol
  failure and central-health saturation;
- review database migration/rollback; and
- keep production feature flag disabled pending acceptance.

### Exit evidence

- runbook includes exact observation, action and rollback commands;
- no recovery step kills by process name or restarts all datafeeds for one failure;
- production config validates on both platforms; and
- rollback restores one authoritative Stage 2 owner without overlapping workers.

## 20. S3G-15 — Acceptance and as-built handoff

### Work

- map every `S3-` requirement to code and passing evidence;
- record all gate results, commits, artifacts and accepted deviations;
- update the Stage 1/2 as-built specification only where shared boundaries changed;
- create `Market-Data-Resiliency-Stage-3-As-Built-Specification-v1.0.md` from actual implementation;
- update the four-stage roadmap and Stage 4 entry decision;
- complete final Development/provider/platform soak; and
- obtain explicit owner acceptance before production enablement or Stage 4 implementation.

### Acceptance checklist

- [ ] One OS process per active dataset and one admitted generation per dataset.
- [ ] Exact one-minute/five-minute live policy and healthy qualification pass under fake time.
- [ ] Exact five-minute/15-minute OffTrading policy passes under fake time.
- [ ] Closed workers stop with no recurring probe.
- [ ] Terminal and exit events trigger immediately.
- [ ] Cooperative reset fully clears dataset-generation state.
- [ ] Failed cooperation forcibly replaces only the affected process.
- [ ] Windows and Linux process trees exit before replacement admission.
- [ ] Stale generations cannot publish or mutate host state.
- [ ] Incident identity/attempts survive generation changes.
- [ ] Crash-loop protection and authorized reset pass.
- [ ] Central health remains queryable during injected failures.
- [ ] Metrics, persistence and diagnostic dimensions are bounded.
- [ ] Operational UI and refresh journeys pass.
- [ ] C++ and Rust parity passes.
- [ ] Development canary, all-dataset, rollback and soak evidence pass.
- [ ] Final as-built documentation is current.
- [ ] Owner explicitly accepts Stage 3.

## 21. Commit strategy

Keep review units aligned to gates. A recommended sequence is:

1. tests/guards and frozen contracts;
2. central health core;
3. incident policy;
4. worker host and IPC supervisor;
5. fenced data/subscription ingress;
6. platform containment;
7. watchdog persistence/integration;
8. instrumentation/API/UI;
9. canary/config/runbook; and
10. final evidence/as-built documents.

Do not combine production feature enablement with worker/process implementation. Each commit must
name its gate and include its focused verification result.

## 22. Rollback rules

Before Stage 3 acceptance, rollback is always available per dataset:

1. close Stage 3 admission;
2. request graceful stop and force containment if necessary;
3. confirm the worker process tree exited;
4. clear Stage 3 host mirrors and worker identity;
5. switch the dataset owner flag to Stage 2;
6. start/qualify through the serialized watchdog; and
7. record the rollback incident and verify no Stage 3 process remains.

Rollback never reuses a Stage 3 generation identity and never replays queued worker or Market
Outlook data.

## 23. First implementation action after approval

Begin only `S3G-00` and `S3G-01`: capture the current baseline, add the execution record and land the
failing characterization/architecture guards. Do not scaffold or launch the worker process until
the contracts, policy tests and project dependency direction have been reviewed at `S3G-02`.
