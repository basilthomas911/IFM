# Market Data Resiliency Stage 3 Implementation Record v1.0

| Item | Value |
| --- | --- |
| Record ID | `MDR-S3-REC` |
| Date | 2026-09-04 |
| Baseline commit | `0d7f74ba` |
| Implementation state | Development synthetic implementation complete; production enablement disabled |
| Feature flag | `MarketDataRecovery:Stage3:Enabled` (default `false`) |

## Implemented behavior

- one console worker process owns one dataset's native feed, ring, drain, aggregation and publication state;
- a supervisor owns each exact child identity and uses three inherited anonymous pipes: command,
  response and realtime publication;
- MessagePack v1 control/publication frames are bounded and carry worker, dataset, value-date,
  generation, manifest-revision and monotonic sequence identity;
- a random bootstrap token is inherited through the child environment and is never placed on the
  command line or returned in health snapshots;
- host ingress closes before reset/replacement and rejects stale, duplicate or decreasing
  generation publications before forwarding to the existing realtime publisher;
- cooperative reset reconstructs the complete dataset generation inside the worker;
- failed live recovery receives one attempt per one-minute probe and escalates after five attempts
  or five continuous unhealthy minutes;
- off-hours probes occur every five minutes, wait 15 unhealthy minutes, attempt one reset and then
  escalate on failure;
- planned closure stops workers and schedules no recurring closed-session probe;
- process replacement confirms old-process exit before launching and admitting a new identity;
- Windows uses a kill-on-close Job Object; Linux uses a dedicated process group with TERM/KILL;
- incident state lives above generation, is exposed through central immutable operations health,
  and is persisted as bounded current state plus append-only PostgreSQL transitions;
- open incident state hydrates conservatively after API restart; and
- `/api/market-data/operations-health` exposes the independent central snapshot.

## Deployment boundary

The new runtime is selected only when the Stage 3 flag is enabled. The default remains the accepted
Stage 2 runtime. The current Development configuration uses `DatabentoLive`; Startup therefore
rejects Stage 3 enablement until live-provider and host query-mirror qualification are accepted.
This prevents a partial live rollout. The worker manifest and executable support the live data-source
mode, but provider-connected evidence is intentionally not inferred from synthetic tests.

## Verification evidence

| Evidence | Result |
| --- | --- |
| Full solution build | Passed, zero warnings/errors |
| API Server build | Passed, zero warnings/errors |
| Application.MarketData unit tests | 166 passed |
| Stage 3 focused worker/policy/health/resiliency tests | 45 passed |
| Databento framework unit/native C++ suite | 136 passed |
| MarketData.Feed unit tests | 505 passed |
| MarketDataService PostgreSQL integration | 3 passed |
| Lifecycle ownership architecture gate | Passed |
| Windows forced hung-worker containment | Passed in worker process test |
| Linux process-group containment | Implemented; Linux runtime/CI evidence still required |
| Databento live provider | Not run in this implementation session |

Use `scripts/Test-DatabentoStage3.ps1 -IncludePostgres` to repeat the Windows synthetic and
PostgreSQL qualification performed for this record.

## Rollout status

This record completes the implementation and synthetic Development qualification boundary. It does
not authorize production enablement. The remaining acceptance evidence is operational rather than
an unimplemented fallback: Linux execution, provider-connected canary, UI journey/soak, rollback
rehearsal and explicit owner acceptance. Keep the feature flag off until those checks pass.
