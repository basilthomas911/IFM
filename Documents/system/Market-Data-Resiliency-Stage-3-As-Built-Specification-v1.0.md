# Market Data Resiliency Stage 3 As-Built Specification v1.0

| Item | Value |
| --- | --- |
| Document ID | `MDR-S3-AB` |
| Date | 2026-09-04 (America/Toronto) |
| State | Implemented offline-qualified subset; NOT full Stage 3 acceptance or production approval |
| Source baseline | Working-tree remediation after `dea86871`; not committed |
| Binding target | [Stage 3 specification](Market-Data-Resiliency-Stage-3-Specification-v1.0.md) |
| Test evidence | [Implementation record](Market-Data-Resiliency-Stage-3-Implementation-Record-v1.0.md), latest acceptance-work section |
| Default | `MarketDataRecovery:Stage3:Enabled=false`; non-Synthetic enablement rejected by Startup |

This describes executable behavior and its limits. It does not change the target specification,
waive an unmet requirement, imply live enablement, or supersede owner acceptance of Stage 2.

## 1. Ownership and actual data path

`DatabentoMarketDataWatchdogService` serializes lifecycle requests. In Stage 3 it delegates to
`SupervisedDatabentoLifecycleRuntime` and `DatasetWorkerProcessRecoveryService`. Each active dataset
has one supervised console worker containing its native feed, ring, managed drain and aggregation.
The API retains subscription authority, publication admission, latest-value mirrors and health.

The deployed publication topology is:

```text
Dataset worker: native feed -> managed drain/aggregation -> inherited publication pipe
API host: generation-fenced ingress -> current-value mirrors + bounded realtime publisher
Existing Core NATS actor routing -> analytics -> local Market Outlook processor/cache -> UI
```

The worker has no direct NATS connection. This differs from the proposed direct worker data plane
and needs explicit architecture acceptance. A host transport that ignores cancellation can still
require host recovery; killing a dataset worker cannot terminate a stuck API-host transport call.

The composed Market Outlook remains latest-state oriented. This implementation introduces no
JetStream snapshot-history replay queue and does not change latest-snapshot restart hydration.

## 2. Worker identity, subscriptions and containment

- Control protocol v2 uses bounded inherited command/response pipes, bootstrap authentication,
  worker instance, generation, value date, manifest revision/fingerprint and sequence checks.
- The authoritative desired-subscription registry reconstructs replacement manifests from current
  registrations, not the original launch list. Admission requires full manifest acknowledgment.
- Old-generation publication is rejected before host state mutation. Admission closure cancels
  queued generation output and clears the affected current-value mirrors. Other datasets retain
  their identities and values. Already-delivered network messages cannot be recalled.
- Health carries actual native subscription/heartbeat/ring evidence and managed drain/aggregation
  counters. Missing diagnostics are not fabricated as healthy.
- Windows uses exact owned process handles and kill-on-close Job Objects. Tests cover forced tree
  termination, abrupt owning-parent death, repeated replacement and rollback to Stage 2 synthetic.
- Linux requires a successfully established process group before accepting the worker handshake.
  Stop verifies the group, not just its leader: SIGTERM, bounded grace, SIGKILL if needed, and bounded
  confirmation that the group is absent. Failed confirmation retains ownership and prevents safe
  replacement from being reported. Tests cover a SIGTERM-resistant descendant after both hung and
  graceful leader exit, inside an isolated Linux container with an init/reaper.
- Linux abrupt supervisor death is NOT qualified. A process group alone is not the Windows Job
  Object parent-death guarantee. Deployment containment/reaping requirements remain open.

The `Application.MarketData.QualificationHost` executable is a test-only protocol/process helper,
not an alternative production worker and not evidence of Linux native-feed support.

## 3. Incident and watchdog policy

| Condition | Implemented behavior |
| --- | --- |
| LiveTrading unhealthy | One-minute scheduled cadence; cooperative reset; fifth failed attempt or five-minute policy window authorizes affected-process replacement |
| OffTrading unhealthy | Five-minute cadence; wait 15 monotonic minutes; one cooperative reset, then process replacement if it fails |
| Terminal failure | Immediate signal wakes the serialized watchdog; still-running terminal worker receives cooperative recovery, failed recovery escalates in that operation |
| Confirmed process exit | Exact-generation exit evidence skips futile cooperative reset |
| Healthy qualification | Incident remains open until a full healthy live minute or healthy off-hours observation interval |
| Session transition | Retain incident identity/history, reset session-specific timing/attempt window; wake at the authoritative next transition |
| Session closure / explicit stop | Stop ownership first, then persist incident closure |
| Failed replacement | Bounded 5-second / 30-second / 2-minute retry backoff; three failures in a rolling 15-minute window latch the incident |

Monotonic time drives incident, policy and backoff durations. Generation changes alone do not erase
incidents. Snapshots now persist session timing, backoff remaining and a bounded failure-age list.
Hydration supports elapsed time before a restarted process's timestamp origin; negative monotonic
origins are not confused with a closed incident. PostgreSQL JSON round-trip is tested.

Authorized operator identity/reason, dataset-scoped latch clearing and material-manifest latch
release are not yet wired as a complete command workflow. The legacy global reset entry point is
not evidence that `S3-PROC-09` is satisfied. Persistence still runs in the serialized watchdog path;
the specification's no-database-I/O scheduled-probe boundary remains an implementation gap.

## 4. Bounded host publication

`TickAggregationEventPublisher` uses `BoundedRealtimeTickPublisher` only when an explicit
`RealtimeTickPublisherPolicy` is injected. Startup injects it only for enabled Stage 3. The null
policy retains the existing Stage 2 publisher behavior.

| Setting under `MarketDataRecovery:Stage3:RealtimePublisher` | Default | Validation |
| --- | --- | --- |
| `Capacity` | 4096 queued publications | 1 to 65536; at most one additional in-flight send |
| `MaximumQueueAge` | 5 seconds | Positive, at most 5 minutes |
| `SendTimeout` | 2 seconds | Positive, at most 1 minute |
| `CancellationGracePeriod` | 100 milliseconds | 0 to 5 seconds |

Admission is nonblocking. Saturation rejects explicitly; it does not silently coalesce raw events.
A failed or expired session discards and counts its backlog. Explicit lifecycle recovery starts a
fresh session. Retired-generation queued work is canceled without faulting unaffected work.
Cancellation-unresponsive sends latch restart: no overlapping sender is launched. An in-flight
quote lease remains owned until the actual send finishes; queued leases are released, and rejection
leaves the lease with its caller. The wake-up signal is binary and cannot accumulate permits from
pruned generations.

Counters expose accepted, published, rejected, saturated, expired, failed, canceled and shutdown
discarded output, depth/age and uncontained work. The actual Core NATS outage test verifies delivery
before outage, bounded failure while disconnected, reconnect and fresh-only recovery. It does not
prove all half-open-network or slow-consumer cases, nor end-to-end UI consumption acknowledgment.

## 5. Independent operations health and UI

`MarketDataOperationsHealthService` owns fixed stage-counter/histogram arrays and at most 16 dataset
incident entries. `MarketDataOperationsHealthObserver` reads cached worker/watchdog/Market Outlook
state every five seconds. It performs no native/control command and cannot request recovery. This
observation interval is distinct from the one-minute/five-minute watchdog probe policy.

`GET /api/market-data/operations-health` returns schema-v1 bounded immutable snapshots. Current
failures remain visible even when runtime readiness gauges are green; later successes recover
status without erasing failure counters. Receipt alone is not successful progress. A pending
Market Outlook age over five seconds is Yellow and over one minute is Red; unavailable processor
is Red. Closed sessions are Inactive. Observer age over 15 seconds prevents a green aggregate.
Source timestamps are independent of processing status. Histogram percentiles are lifetime bucket
upper estimates, not exact sliding-window percentiles.

The real Market Outlook processor now records cache/composition/publication through the composite
recorder. RSI/TDI/ITI/EMA/BB/VIX/EOD/trade-signal output boundaries are counted when they reach the
local update channel. These are NOT instrumentation of every internal analytics calculation.

The WinForms shell adds a read-only **Operations health** panel with processing-stage and dataset
tabs, process/generation identity, queue/progress counters, incidents, recovery counts and UTC
timestamps. Its single-flight status query has a five-second deadline, one-MiB response cap,
schema/dimension validation and stale-observation rejection. Failures clear old green values.
Services read into UI-owned value records; ViewModels add no backend DTO boundary or framework
presentation dependency. Async-void is limited to the new form's WinForms event adapters.

Development config points at `http://localhost:22543/api/market-data/operations-health`. Other
environments require explicit endpoint configuration; no Development fallback is used. Query,
presentation, backend-to-UI JSON compatibility and isolated WinForms rendering are tested. The
full running API-to-UI operational journey is not yet qualified.

## 6. Remaining acceptance gates

| Requirement area | Remaining work / evidence |
| --- | --- |
| `S3-PROC-08/09`, refresh commands | Authorized, audited dataset-specific manual controls; material manifest latch release; complete typed refresh dispatcher |
| `S3-WD-02`, persistence/retention | Remove persistence I/O from the probe execution boundary; prove bounded asynchronous diagnostic persistence and retention |
| `S3-HEALTH`, `S3-UI` completeness | Per-dataset/contract analytics causality, exact failed-stage localization, UI-delivery observations and full refresh/recovery journeys |
| Linux native/platform parity | Linux native deployment and abrupt-parent-death containment; Rust native backend is currently Windows-only |
| Architecture deviation | Review host-owned Core NATS publisher versus proposed direct worker data plane |
| Live/provider qualification | Credentialed native live canary, multiple datasets, transitions and real provider failure recovery |
| Sustained operation | Elapsed production-shaped soak, rates/GC/latency/resource evidence and full operational rollback rehearsal |
| Acceptance / Stage 4 | Explicit owner acceptance or an explicitly approved scoped sequencing exception |

These include remaining engineering, not just missing user sign-off. Keep Stage 3 disabled by
default and the non-Synthetic Startup guard. Stage 4 runtime implementation remains gated.
