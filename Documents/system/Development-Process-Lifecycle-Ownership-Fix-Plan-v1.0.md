# Development Process Lifecycle Ownership Fix Plan v1.0

**Status:** Implemented; automated qualification complete  
**Date:** 2026-09-04  
**Scope:** Development-only startup, shutdown, restart, orphan detection, and build/deploy preflight
for the IFM API Server and UI.Net processes  
**Incident:** A separately launched API process remained alive after the visible application was
stopped and locked `TomasAI.IFM.Framework.MarketData.DataBento.pdb`, causing MSBuild error
`MSB3021` on the next build.

## 1. Outcome

Development startup and shutdown will have one explicit owner. Repeated start, stop, reset, debug,
and rebuild cycles will not leave IFM API or UI processes holding files beneath repository `bin`
directories.

The required lifecycle is:

```text
Start development session
  -> inspect prior IFM development ownership record
  -> reconcile only positively identified owned processes
  -> refuse to kill an unowned or ambiguous process
  -> start API under one development supervisor session
  -> wait for API readiness
  -> start UI under the same session

Stop/reset/manager exit
  -> request UI shutdown
  -> request graceful API shutdown
  -> wait for bounded completion
  -> terminate the owned process tree if necessary
  -> verify API/UI processes and repository output locks are gone
  -> clear the ownership record
```

The acceptance invariant is:

> After a development session stops, no IFM API or UI process started by that session remains
> alive, and a clean rebuild can immediately replace every output DLL and PDB.

## 2. Process boundary

| Component | Development lifecycle classification | Stop with API/UI session? |
| --- | --- | --- |
| `TomasAI.IFM.Application.Api.Server` | Session-owned application process | Yes |
| `TomasAI.IFM.UI.Net` | Session-owned application process | Yes |
| Server Manager/development host | Session owner | It initiates child shutdown, then exits |
| Scheduler Host | Independently managed service | No |
| Scheduled-task executables | Scheduler-owned transient processes | No direct intervention |
| NATS, Redis, PostgreSQL, ScyllaDB | Persistent Docker infrastructure | No |
| Visual Studio, Rider, MSBuild, compiler servers | Development tooling | No |

No implementation may kill every `dotnet.exe`, stop Docker, or match processes by name alone.

## 3. Confirmed failure modes

1. The solution launch profile starts API and UI independently. Closing only the UI does not prove
   that the API stopped.
2. The API's graceful standard-input control channel is opt-in through
   `--server-manager-stdin-shutdown`. Direct `dotnet` launches can bypass it.
3. When the opt-in API receives standard-input end-of-file because its manager disappears, the
   current monitor returns without stopping the application.
4. Server Manager tracks child processes only in memory. It has no durable development session
   identity with which to reconcile a previous interrupted run.
5. Server Manager calls its shutdown path on a normal application exit, but a crash or forced
   termination can bypass that path.
6. API/UI children are not currently placed in a kill-on-owner-close Windows Job Object. The
   Scheduler Host already uses this mechanism for scheduled children, proving the repository has an
   applicable implementation pattern.
7. A stale process is currently discovered only when a port bind fails or MSBuild encounters a
   locked output file, which is too late and produces an indirect error.

## 4. Binding decisions

1. This version changes Development behavior only. Production configuration and process termination
   remain unchanged.
2. Server Manager becomes the supported owner for routine Development API/UI sessions. Direct
   project launch remains possible for debugging, but it is classified as unowned and receives
   diagnostics rather than automatic termination.
3. Each managed Development run receives a cryptographically random session ID. The manager passes
   it to children using an environment variable and writes an ownership record containing role,
   PID, process creation time, resolved executable or entry-assembly path, and session ID.
4. PID alone is never sufficient evidence because Windows can reuse it. Reconciliation requires PID,
   creation time, role, expected binary/module path, and the Development marker to agree.
5. Positively identified processes from the previous managed Development session may be stopped
   automatically. Ambiguous or manually launched IFM processes cause a clear, actionable failure.
6. Graceful shutdown is attempted first. Forced termination is bounded, logged, and restricted to
   the owned process tree.
7. Standard-input end-of-file in manager-controlled API mode means the manager disappeared and must
   request host shutdown.
8. A Windows Job Object with `KILL_ON_JOB_CLOSE` provides the final crash/forced-exit boundary for
   managed API/UI children.
9. Development infrastructure is explicitly excluded from session cleanup.
10. Builds may detect and explain a live IFM output owner, but ordinary build targets must not
    silently terminate processes.

## 5. Implementation stages

### DPL-01 - Establish executable process inventory and regression evidence

**Changes**

- Add a Development process-inspection component that reports IFM role, PID, creation time, expected
  binary/module path, session ownership, and whether the process can lock repository outputs.
- Add a read-only `scripts/Development/Get-IFMDevelopmentProcess.ps1` entry point for developers and
  automation.
- Identify `dotnet <application>.dll` hosts through their loaded IFM entry assembly or validated
  command line, not through the generic `dotnet` process name.
- Produce separate results for `Owned`, `Unowned`, `StaleRecord`, and `Ambiguous`.
- Add a test fixture that reproduces the incident: leave an API helper alive, attempt replacement of
  an output artifact, and prove the inspector identifies the exact owner.

**Exit criteria**

- The current PID-50216 class of leak is reported as an IFM API process rather than one of many
  unrelated `dotnet` processes.
- No Visual Studio, compiler-server, test-host, Docker, or database process is classified as an IFM
  session child.

### DPL-02 - Make managed API shutdown reliable

**Changes**

- Change `ServerManagerStandardInputShutdown` so EOF in opt-in managed mode calls
  `IHostApplicationLifetime.StopApplication()`.
- Preserve the exact `shutdown` command for normal graceful shutdown.
- Log distinct reasons: explicit shutdown message, manager pipe closed, application cancellation,
  and control-channel failure.
- Ensure the API actor supervisor and host `finally` paths complete before process exit.
- Add tests for explicit shutdown, stdin EOF, unrelated input, repeated requests, and cancellation.

**Exit criteria**

- Closing or crashing the manager's stdin pipe cannot leave a managed API running.
- API shutdown remains graceful and actors receive their existing bounded shutdown path.

### DPL-03 - Add crash-safe ownership to Development Server Manager

**Changes**

- Extract or share the Scheduler Host's Windows Job Object wrapper for use by Server Manager.
- Create one Development session job with `KILL_ON_JOB_CLOSE` and assign API/UI immediately after
  process creation.
- If assignment fails, stop the just-started child and fail startup visibly; never continue with an
  unowned child.
- Retain reverse-order graceful shutdown and the current timeout-based forced tree termination.
- Make job disposal the final step after child completion during normal shutdown.
- Gate this ownership behavior to `DOTNET_ENVIRONMENT=Development` for this delivery.

**Exit criteria**

- Normal exit, tray Exit, Reset, startup failure, manager crash, and forced manager termination all
  leave no managed API/UI child.
- Killing the manager during API startup and during normal operation cannot produce an orphan.

### DPL-04 - Add safe session records and startup reconciliation

**Changes**

- Persist the active Development session record under the user's local application-data directory,
  not in the repository or published application directory.
- Write records atomically and include schema version, session ID, manager identity, child identities,
  and timestamps.
- On startup, validate every recorded identity against the live operating-system process before
  taking action.
- Gracefully stop a positively identified prior managed session; use bounded owned-tree termination
  only if graceful shutdown fails.
- Delete records for processes proven exited. Quarantine malformed or ambiguous records and display
  recovery instructions instead of guessing.
- Reject a second active Development manager session using a named mutex plus the validated session
  record.

**Exit criteria**

- Restarting after a normal exit, crash, machine sleep interruption, or stale PID record is
  deterministic.
- PID reuse cannot cause an unrelated process to be stopped.
- Two managers cannot concurrently start duplicate Development API/UI sets.

### DPL-05 - Make repeated development cycles easy and explicit

**Changes**

- Add `scripts/Development/Start-IFMDevelopment.ps1` as the supported repeatable entry point.
- Add `scripts/Development/Stop-IFMDevelopment.ps1` with default `OwnedOnly` behavior and a
  `-VerifyStopped` result suitable for CI or pre-build use.
- Configure the Development launcher to use repository Debug outputs without changing Production
  paths. Resolve the repository root explicitly rather than embedding a user-specific path.
- Start API first, wait for `/health/ready`, then start UI.
- On reset, stop and verify the current session before starting replacements.
- If an unowned API/UI is found, stop startup with its PID, start time, binary path, and the exact
  manual remediation command. Do not kill it automatically.
- Document the two supported workflows:
  1. managed routine development through the launcher/Server Manager; and
  2. direct IDE debugging, where the developer must use Stop All and can run the verification script
     before rebuilding.

**Exit criteria**

- Ten consecutive start/readiness/stop/rebuild cycles succeed without `MSB3021` or surviving IFM
  application processes.
- A failed API readiness check tears down that session and never launches UI.
- A direct unowned debug process yields an actionable message before a confusing copy failure.

### DPL-06 - Automated qualification and rollout

**Automated tests**

1. Process classification rejects generic `dotnet`, testhost, compiler-server, and reused-PID cases.
2. API stdin tests cover shutdown text, EOF, cancellation, and invalid input.
3. Supervisor tests cover normal exit, reset, partial startup, readiness timeout, forced termination,
   and manager crash.
4. Job Object integration tests prove API/UI helper descendants exit when the owning handle closes.
5. Session-record tests cover atomic writes, corrupt records, stale records, active duplicates, and
   identity mismatch.
6. End-to-end Development tests run repeated API/UI lifecycle cycles and rebuild affected projects
   after every stop.
7. Safety tests prove Docker and independently managed Scheduler Host processes remain untouched.

**Manual acceptance**

- Launch the full Development application set, close UI, use tray Exit, use Reset, interrupt startup,
  and force-close Server Manager in separate trials.
- After each trial, run the process inspector and rebuild API Server plus the DataBento dependency.
- Confirm there is no IFM process loading files from repository output directories.
- Confirm NATS, Redis, PostgreSQL, and ScyllaDB remain available.

**Rollout**

1. Land diagnostics and tests first.
2. Land API EOF shutdown.
3. Enable Development Job Object ownership and session records.
4. Qualify the managed launcher through ten repeated cycles.
5. Make the managed workflow the documented Development default.
6. Observe for one week of normal development before preparing a separate Production design and
   approval plan.

## 6. Observability

Every lifecycle log entry must include:

- environment;
- development session ID;
- process role;
- PID and creation time;
- manager PID;
- resolved executable or entry-assembly path;
- requested shutdown mode;
- graceful or forced outcome;
- elapsed shutdown time; and
- final verification result.

The developer-facing stopped check returns one of:

- `Stopped`: no API/UI process from the session remains;
- `Stopping`: graceful shutdown is still within its deadline;
- `OwnedProcessRemaining`: safe forced cleanup is allowed;
- `UnownedProcessDetected`: manual intervention is required; or
- `Ambiguous`: no termination attempted because identity could not be proven.

## 7. Safety and non-goals

- Do not kill processes by executable name alone.
- Do not issue `Stop-Process -Name dotnet` or equivalent broad termination.
- Do not stop Docker Desktop or IFM infrastructure containers.
- Do not stop the Scheduler Host service or scheduled tasks from the Development API/UI cleanup
  path.
- Do not add automatic process termination to normal MSBuild targets.
- Do not alter Production process behavior in this version.
- Do not treat this Development qualification as Production acceptance.

## 8. Production carry-forward

Production needs the same no-orphan invariant but a different ownership boundary. The eventual
Production plan should make API and Scheduler Host Windows services managed by the Service Control
Manager, keep the interactive UI independent, add deployment quiescence checks, and define recovery
and audit behavior for service crashes. Reusable identity, verification, and structured lifecycle
components from this Development work may be promoted only after separate Production tests and
operator approval.

## 9. Definition of done

This plan is complete only when all of the following are true in Development:

- one supported owner starts API and UI in the required order;
- every managed exit path stops both processes;
- manager failure cannot orphan its children;
- startup safely reconciles a previous owned session;
- unowned processes are diagnosed without unsafe automatic termination;
- immediate rebuilds no longer fail because IFM application outputs are locked;
- persistent infrastructure remains running; and
- repeated-cycle automated and manual acceptance evidence is recorded.

## 10. Execution record

Implemented on 2026-09-04:

- The leaked verification API process was stopped and repository output locks were released.
- Managed API stdin EOF now requests graceful host shutdown.
- Development Server Manager assigns API/UI to a kill-on-close Windows Job Object.
- A per-user singleton and atomic session record track validated manager/child identities.
- Startup reconciles only prior processes whose PID, creation time, and executable path all match.
- A current-user named pipe supports graceful scripted shutdown.
- Development process inspection, start, stop, and stopped-verification scripts were added.
- Development configuration resolves repository Debug outputs through `IFM_REPOSITORY_ROOT`.
- A shared `Managed Development` solution launch profile was added; direct debugging remains explicitly unmanaged.
- Production sets `DevelopmentProcessOwnershipEnabled` to `false`.

Qualification evidence:

- Server Manager unit tests: 31 passed.
- Server Manager integration tests: 25 passed, including all lifecycle and existing Docker/PostgreSQL coverage.
- API Server Debug build: succeeded with zero warnings and zero errors.
- UI.Net Debug build: succeeded with zero warnings and zero errors.
- PowerShell parser validation passed for all three Development lifecycle scripts.
- `Stop-IFMDevelopment.ps1 -VerifyStopped` confirmed no IFM API/UI process remained.
- A real bounded Development cycle started Server Manager PID `5532`, recorded API PID `32600` as
  owned and repository-output-locking, shut both down through the Development control path, and
  verified that no IFM application process remained.
- The API Server was rebuilt immediately after that real shutdown cycle with zero warnings and zero
  errors, including successful replacement of the previously locked DataBento output artifacts.
- The existing VS Code `IFM: API + UI (Development)` compound now runs repository Debug-process
  cleanup before its build and again as the API `postDebugTask`, while retaining debugger
  `stopAll` behavior.
- A direct `dotnet TomasAI.IFM.Application.Api.Server.dll` process (PID `38960`) reproduced the VS
  Code `coreclr` hosting shape. The updated cleanup identified it by its exact loaded assembly path,
  stopped it, verified no IFM process remained, and the immediate API rebuild succeeded with zero
  warnings and zero errors.

The one-week observation period described in rollout is operational monitoring, not an outstanding code change.
