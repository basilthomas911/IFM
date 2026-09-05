# Market Data Resiliency Stage 3 Implementation Record v1.0

| Item | Value |
| --- | --- |
| Record ID | `MDR-S3-REC` |
| Date | 2026-09-04 |
| Baseline commit | `0d7f74ba` |
| Integration remediation baseline | `dea86871`; remediation is recorded in the working tree, not yet committed |
| Implementation state | Offline containment, bounded publication, watchdog and central-health remediation implemented; latest evidence below; full Stage 3 acceptance remains open |
| Feature flag | `MarketDataRecovery:Stage3:Enabled` (default `false`) |

## Implemented behavior

- one console worker process owns one dataset's native feed, ring, drain, aggregation and publication state;
- a supervisor owns each exact child identity and uses three inherited anonymous pipes: command,
  response and realtime publication;
- MessagePack control protocol v2 and bounded publication frames carry worker, dataset, value-date,
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

## Original implementation verification evidence

These results describe the original implementation session. Current remediation results appear
below and supersede these counts; the original counts are retained as historical evidence.

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

The evidence above records the implemented synthetic Development subset, not completion of every
Stage 3 specification requirement. It does not authorize production enablement. Linux execution,
provider-connected canary, UI journey/soak, rollback rehearsal and explicit owner acceptance remain
outstanding. The entry audit below also identified missing implementation, not only missing
operational evidence. Its manifest and mirror findings are addressed by the remediation below;
this is not a claim that every Stage 3 requirement has passed. Keep the feature flag off until
implementation and acceptance gates pass.

## Stage 4 entry audit correction (2026-09-04)

The source audit at `dea86871` found that the previous statement that remaining work was solely
operational was too broad:

- `SupervisedDatabentoLifecycleRuntime` launches a static `--contracts` set with manifest revision
  one; `DatasetWorkerControlProtocol` has no dynamic desired-manifest apply/acknowledgment protocol.
- `DatasetWorkerProcessRecoveryService` replaces workers from their stored launch request, not a
  complete current desired-subscription registry as required by `S3-SUB-01` through `S3-SUB-03`.
- `DatasetPublicationIngress` fences and forwards publications but does not provide the stable
  host query mirrors required by the specification. `DatabentoMarketDataApi` readers still depend
  on its in-process epoch, which the supervised lifecycle does not create.
- `Startup` explicitly rejects non-Synthetic Stage 3 enablement. This guard must remain until the
  missing integration and its qualification are complete; removing the guard is not a fix.

Existing passing tests remain valid evidence for their tested subset. They must not be used as
proof of these missing behaviors or as implicit owner acceptance. See the
[Stage 4 entry record](Market-Data-Resiliency-Stage-4-Implementation-Record-v1.0.md) for the audit
checks and entry decision.

## Authorized integration remediation (2026-09-04)

The owner approved completing the missing Stage 3 integration before returning to Stage 4.
This work changes the disabled supervised runtime only; it does not enable live Stage 3,
accept Stage 3 on the owner's behalf, or implement Stage 4 option/strategy workflows.

### Desired subscriptions and native identity

- `DatasetDesiredSubscriptionRegistry` owns an immutable, current per-dataset/value-date manifest
  above the worker process. It retains only current state, not historical tick/subscription replay.
- A manifest contains the complete resolved domain ID, provider symbol, asset type, root, dataset,
  on-the-run and rollover fields. Revisions are monotonic; identical sets are idempotent, prior
  dates are rejected, and canonical fingerprints cover the entire manifest identity and content.
- Stage 3 intentionally accepts only a bounded core futures manifest: at most 16 datasets,
  16 contracts per dataset, and 240 KiB per manifest within the 256 KiB control-frame limit.
  Stage 4 option routes and owner leases are not approximated by this core manifest.
- Control protocol major version 2 adds full start/apply/acknowledgment messages. The worker
  acknowledges the actual native dataset generation, not its bootstrap generation. A duplicate
  apply is idempotent; an explicit reset reconstructs even an unchanged manifest. Mismatched,
  stale or partially realized manifests cannot be admitted.
- Reset and process replacement converge to the latest desired manifest before admission.
  The final desired-revision check and admission are atomic against a newer registry revision.
  Tests use a second ES contract to verify that recovery is not merely replaying launch intent.

### Stable host readers and realtime delivery

- `DatasetWorkerCurrentValues` supplies stable API-side futures last-price readers, normalized
  current prices and session statistics. Supervised `DatabentoMarketDataApi` reads these mirrors
  without constructing a second native epoch inside the API process.
- Dataset identity, generation, date, revision, membership and sequence are checked before mirror
  mutation. Closing admission clears the affected dataset's values under the reset boundary;
  already retained same-date readers become empty and subsequently receive the replacement's
  values. Another dataset's readers and native generation remain unchanged.
- Reference metadata comes from the authoritative registration catalog; missing metadata fails
  explicitly rather than inventing currency, expiry or multiplier. Options and transient ticker
  routes remain explicitly unsupported in this Stage 3 supervised API.
- Each worker epoch owns its own generation-bound publication writer. Startup events before
  native identity qualification are dropped; old writer output is never relabeled as a new epoch.
- Host ingress transfers a lasting generation-cancellation token to the existing realtime
  publisher. Queued old-generation sends are canceled when admission closes. Already completed
  network delivery cannot be recalled; this does not introduce a durable replay queue.
- The supervised lifecycle now starts the actual API-host `TickAggregationEventPublisher` before
  worker admission and stops it after closing/stopping workers. Startup failure rolls it back.
  Publisher shutdown has a five-second default deadline; an uncooperative transport cannot keep
  workers alive, and failed publisher shutdown latches a host-restart requirement before another
  supervised session. A real publisher with a controlled actor producer verifies this boundary.
- The actual deployment path is inherited local publication pipe, fenced API ingress, then the
  existing host realtime actor/Core NATS publisher. The worker does not directly own a NATS
  connection. This differs from the proposed direct worker data-plane topology and remains an
  explicit acceptance-review item, not an undocumented claim of exact specification parity.

### Watchdog diagnostics and shutdown containment

- Health observations now carry actual native subscription/heartbeat/ring counters and managed
  drain/aggregation progress, including the current in-flight record. Fabricated always-Up
  counters are removed. Missing diagnostic evidence is incomplete/Down; a responsive process
  reporting a failed feed is distinguishable from an unresponsive process.
- Native-drain, blocked-channel and in-flight aggregation stalls are tested through the same
  diagnostic conversion used by the host watchdog. Snapshot reads reject mixed-generation
  observations during concurrent manifest application.
- Reset/replacement/start operations recheck exact ownership after entering their serialized
  lifecycle boundary. Replacement queued behind shutdown cannot spawn an untracked child.
- Once shutdown owns its batch, caller cancellation does not abandon later datasets. A failed
  dataset shutdown does not prevent attempts to stop the others. If OS termination cannot verify
  exit, exact process ownership remains queryable for retry rather than freezing a disposed,
  apparently running process snapshot. OS refusal to terminate was not injected in these tests.
- Worker command deadlines include semaphore acquisition and pipe writes. A blocked host ingress
  callback cannot indefinitely prevent disposal of a successfully contained worker.

### Current verification evidence

| Check | Remediation result |
| --- | --- |
| Application.MarketData full suite | 250 passed, 0 failed, 0 skipped; includes 84 added tests; three consecutive final runs passed |
| Stage 3 focused worker/manifest/mirror/diagnostic/policy suite | 129 passed, 0 failed, 0 skipped via qualification script |
| Databento framework unit/native synthetic suite | 136 passed, 0 failed, 0 skipped |
| MarketData.Feed unit suite | 505 passed, 0 failed, 0 skipped |
| Real host realtime publisher integration | 3 passed, 0 failed, 0 skipped |
| MarketDataService PostgreSQL integration | 3 passed against the dedicated local test database |
| Lifecycle ownership architecture gate | Passed |
| Qualification script with PostgreSQL | Passed end to end after resource-test isolation |
| API Server build | Passed, zero warnings/errors; included again in final solution build |
| Full solution build | Final post-review build passed, zero warnings/errors |
| Post-test worker process check | No dataset worker processes remained running |
| Linux/live provider/NATS/UI/elapsed soak | Not run; not implied by the results above |

The native-generation regression was observed failing against the bootstrap-ID behavior before
the fix. A real-publisher characterization reproduces the prior missing-start exception. The new
process integration checks manifest growth, cooperative reset, forced replacement, unexpected
exit, continued VX progress, retained ES reader recovery, startup rollback, canceled shutdown,
and noncooperative actor delivery. No provider key or live connection is needed for these tests.

The expanded script exposed a process-resource soak test running concurrently with child-process
tests. That collection is now isolated from parallel tests; allocation/handle limits are unchanged.
The existing publisher integration tests were also corrected to assert/mock the cancellation-aware
send overload, so the slow-delivery test actually blocks transport again. A new real-publisher
test proves that canceling a retired generation skips its queued send without faulting current
delivery. The accelerated managed soak is not a substitute for elapsed provider-connected soak.

Repeat the focused qualification with:

```powershell
& ./scripts/Test-DatabentoStage3.ps1 -IncludePostgres
dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj --no-restore --verbosity quiet
dotnet test TomasAI.IFM.Domain.MarketData.Feed.UnitTests/TomasAI.IFM.Domain.MarketData.Feed.UnitTests.csproj --no-restore --verbosity quiet
dotnet build TomasAI.IFM.sln --no-restore --verbosity quiet
```

`-IncludePostgres` requires `IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION` pointing to the dedicated
local `event-source-test-db` (`localhost:5432`). The fixture truncates four test tables; never
point it at an application database. The connection was verified without logging credentials.

### Remaining acceptance boundaries

Keep `MarketDataRecovery:Stage3:Enabled=false` and the Startup non-Synthetic guard unchanged.
The following acceptance work supersedes the earlier unbounded-publisher and no-Linux/NATS/UI-test
limitations. Full remaining engineering and operational gates are recorded in the
[Stage 3 as-built specification](Market-Data-Resiliency-Stage-3-As-Built-Specification-v1.0.md),
section 6. Stage 4's entry gate remains open; no requirement is silently waived.

## Offline acceptance work, 2026-09-04

The owner's request to finish what can be completed led to these additional changes:

- Opt-in bounded Stage 3 publication with explicit saturation, expiration, transport deadlines,
  generation cancellation, fresh-only recovery, fault counters and quote-lease ownership tests.
  Null policy preserves Stage 2 behavior. Real Core NATS disconnect/restart is tested against an
  owned loopback-only Docker broker, never the shared Development broker.
- Actual worker process-tree/descendant and Windows parent-death checks; accelerated real native
  replacements and a rollback into a real Stage 2 synthetic epoch.
- A stronger Linux test initially FAILED: SIGTERM exited the leader while its resistant child
  survived. Linux shutdown now qualifies group ownership, terminates remaining descendants even
  after graceful leader exit, and verifies group absence. Both Linux regressions pass.
- Corrected watchdog terminal/confirmed-exit routing, same-operation fifth-failure escalation,
  session-specific incident windows, bounded rolling replacement backoff and persisted closure.
  Restart hydration no longer mistakes a negative reconstructed timestamp for a closed incident.
- Independent central runtime projection and readonly HTTP/UI health panel. The real processor's
  composite instrumentation and analytics-output boundaries are exercised. Earlier tests reproduced
  false health from receipt-only observations and a permanent historical-failure warning; both were
  fixed. Green readiness cannot mask a processing failure, and stale central responses are rejected.
- UI-owned value records preserve the Services-to-Models architecture. The full architecture suite
  caught and drove correction of the initial backend-DTO leak; only the new form's event adapters
  were added to the explicit async-void allowlist. The rendered panel was visually inspected.

### Repeatable qualification

```powershell
./scripts/Test-DatabentoStage3.ps1 -IncludePostgres -IncludeWindowsUi -IncludeIsolatedNats
```

The complete script passed. It includes lifecycle ownership, worker/manifest/policy/central-health,
native framework, real publisher, Market Outlook processor, UI query/presentation, optional Windows
rendering, isolated NATS outage and dedicated PostgreSQL tests. `-IncludeIsolatedNats` requires the
cached `nats:2.12.0-alpine` image and Docker; the test removes its exact broker afterward. No image
pull, shared-broker restart, live provider connection or application startup is performed.

Broader suites and final build results are recorded below. Generated TRX files and the inspected
`operations-health.png` are under ignored `artifacts/Stage3Acceptance/`; these are local evidence,
not committed artifacts. Linux tests ran using cached `mcr.microsoft.com/dotnet/sdk:10.0-noble`,
read-only repository mount, no network, tmpfs scratch and `--init` for descendant reaping.

The Linux test executable is the managed protocol qualification helper, not the native feed.
Windows C++ and Rust synthetic testing does not imply live-provider or Linux Rust parity.

### Latest verification results

| Check | Result |
| --- | --- |
| Application.MarketData full suite, default C++ | 266 passed; 1 Linux-only case skipped on Windows and passed on Linux |
| Application.MarketData full suite, Rust-selected | 266 passed; the same Linux-only skip; no live provider connection |
| Databento framework suite, C++ | 136 passed |
| Databento framework suite, Rust | 136 passed |
| Rust Cargo offline native tests | 6 passed; existing unused-function warning |
| MarketData.Feed full unit suite | 505 passed |
| MarketData.Analytics full unit suite | 1,032 passed |
| UI presentation/architecture full suite | 303 passed |
| Host publisher integration, legacy and bounded | 10 passed |
| Actual Core NATS isolated outage/restart | 1 passed; exact test broker removed |
| PostgreSQL incident/assignment/history integration | 3 passed; includes new timing/backoff JSON fields |
| WinForms readonly health rendering | 1 passed; panel image inspected |
| Linux resistant-descendant process containment | 2 passed: forced stop and graceful leader exit |
| Complete optional qualification script | Passed, including PostgreSQL, Windows UI and isolated NATS |
| Final solution build | Passed, 0 warnings, 0 errors |
| Final default native runtime hashes | API Server, worker and application tests match the default C++ live-capable DLL |
| Final cleanup inventory | 0 dataset/qualification worker processes; 0 isolated NATS test containers |

Repeated backend/platform runs overlap and must not be added as distinct test cases. The Linux
case skipped on Windows was explicitly executed successfully on Linux. The Rust-selected
application test runtime matched the Rust **live-capable** build artifact, but every feed in these
tests was Synthetic; the final solution build restored default C++ artifacts. No source/runtime
configuration was switched to live Stage 3, and nothing was committed or pushed.

Repeat the two Linux managed containment cases after building the application tests:

```powershell
docker run --rm --init --pull never --network none --read-only --tmpfs /tmp `
  -e DOTNET_CLI_HOME=/tmp/ifm-dotnet `
  --mount type=bind,source=C:\repos\IFM,target=/workspace,readonly -w /tmp `
  mcr.microsoft.com/dotnet/sdk:10.0-noble dotnet vstest `
  /workspace/TomasAI.IFM.Application.MarketData.UnitTests/bin/Debug/net10.0/TomasAI.IFM.Application.MarketData.UnitTests.dll `
  '--TestCaseFilter:FullyQualifiedName~Forced_stop_terminates_exact_worker_and_descendant_processes|FullyQualifiedName~Graceful_leader_exit_still_terminates_sigterm_resistant_descendant' `
  --ResultsDirectory:/tmp/ifm-test-results
```

This checkout's WSL distribution has only .NET 7 and no SDK; the cached .NET 10 container avoided
machine-level installation or profile changes. Linux native/C++ feed deployment and Windows-to-
Linux native parity remain separate from these managed OS-containment tests. See the as-built
section 6 for the remaining implementation and acceptance gates; they are not merely sign-off.
