# IFM Server Manager Implementation Details

**Status:** SM-S3 implemented; SM-S4 software modernized; SM-S5 deployment-ready

**Version:** 4.0

**Date:** 2026-08-20

## 1. Current responsibility

`TomasAI.IFM.Application.ServerManager` is the interactive WPF/tray supervisor for the current IFM API Server and
UI.Net desktop application and the operator client for the separate Scheduler Host. It starts the two interactive
processes, captures their output, displays bounded logs, and performs validated/audited scheduler operations through
the local pipe. It never reads scheduler PostgreSQL tables directly.

`TomasAI.IFM.Application.ServerManager.SchedulerHost` is the independent console/Windows Service process that owns
Quartz, PostgreSQL scheduler persistence, catalog snapshots, durable run/attempt state, recovery, and scheduled child
process ownership.

## 2. Runtime flow

```text
App startup
  -> load appsettings.json
  -> overlay appsettings.{DOTNET_ENVIRONMENT}.json when present
  -> bind and validate ServerManagerOptions
  -> create MainWindowViewModel and WPF window
  -> create tray context
  -> ManagedProcessSupervisor.StartAllAsync
       -> API Server
       -> UI.Net

Each process
  -> explicit executable and working directory
  -> no shell and no global current-directory mutation
  -> asynchronous stdout reader
  -> asynchronous stderr reader
  -> asynchronous exit monitor
  -> structured lifecycle/output entries
  -> bounded UI pending queue and bounded displayed collection
```

The tray menu provides `View Console`, `Minimize Console`, `Reset API and UI`, and `Exit Server Manager`. Reset stops
the currently owned processes in reverse start order before starting replacements in configured order.

## 3. Process definitions

The obsolete Event, Telemetry, and Predictive Model slots have been removed. `appsettings.json` now defines an
ordered `ServerManager:Processes` collection.

| Key | Executable | Start order | Graceful shutdown |
| --- | --- | ---: | --- |
| `api` | `TomasAI.IFM.Application.Api.Server.exe` | 10 | Write `shutdown` to redirected stdin |
| `ui` | `TomasAI.IFM.UI.Net.exe` | 20 | Request normal main-window close |

The API definition has an HTTP readiness gate. Server Manager does not launch the next process (UI.Net) until
`/health/ready` returns a successful status. Development uses `http://localhost:22543/health/ready`; the Production
overlay uses port `4096`. A bounded timeout fails startup visibly and prevents the UI from racing incomplete NATS
actor registration.

The API receives `--server-manager-stdin-shutdown`. Only when that opt-in argument is present does it monitor stdin
for the exact `shutdown` control message. It then calls `IHostApplicationLifetime.StopApplication()`, allowing the
web host and actor supervisor to shut down normally.

UI.Net already owns its asynchronous NATS shutdown through its WinForms application context. Closing its main window
enters that normal cleanup path.

If a graceful channel is unavailable or exceeds `ShutdownTimeoutSeconds`, Server Manager calls
`Process.Kill(entireProcessTree: true)` and emits explicit lifecycle evidence. It never silently treats a forced kill
as graceful shutdown.

## 4. Configuration

```json
{
  "ServerManager": {
    "MaximumLogEntries": 5000,
    "ShutdownTimeoutSeconds": 10,
    "Processes": [
      {
        "Key": "api",
        "DisplayName": "API Server",
        "WorkingDirectory": "C:\\TomasAI\\IFMAppDir\\ApiServer",
        "ExecutablePath": "TomasAI.IFM.Application.Api.Server.exe",
        "Arguments": [ "--server-manager-stdin-shutdown" ],
        "StartOrder": 10,
        "Enabled": true,
        "ReadinessUri": "http://localhost:22543/health/ready",
        "ReadinessTimeoutSeconds": 300,
        "ReadinessPollIntervalMilliseconds": 500,
        "ShutdownMode": "StandardInput",
        "ShutdownInput": "shutdown"
      }
    ]
  }
}
```

`Key`, `DisplayName`, `WorkingDirectory`, and `ExecutablePath` are required. Keys are unique without regard to case.
Positive log and shutdown limits are mandatory. `StandardInput` requires a non-empty `ShutdownInput` value. When
configured, `ReadinessUri` must be an absolute HTTP(S) address and its timeout/polling values must be positive.

Relative executable paths resolve beneath their configured working directory. Arguments are passed through
`ProcessStartInfo.ArgumentList`; they are not concatenated into a shell command.

## 5. Log behavior

Every entry has:

- timestamp;
- process key and display name;
- stream: `Manager`, `Lifecycle`, `StandardOutput`, or `StandardError`; and
- message.

The DataGrid shows Time, Process, Stream, and Message columns. Newest entries appear first. Both the dispatcher-pending
queue and displayed collection are limited by `MaximumLogEntries`. If the UI cannot keep up, the oldest pending
entries are discarded and a visible manager entry reports the number dropped. One scheduled dispatcher drain handles
a batch, so a fast child cannot enqueue one WPF dispatcher operation per line indefinitely.

Logs in SM-S1 are an in-memory operational view. Durable files, paging, retention, and scheduled-task run correlation
are SM-S2/SM-S3 work.

## 6. Failure and concurrency behavior

- A missing working directory or executable becomes a visible `Start failed` lifecycle entry.
- One process failing to start does not prevent other enabled definitions from being attempted.
- Duplicate starts for an already-owned key are skipped and recorded.
- stdout and stderr are drained concurrently before exit completion is reported.
- Reset and exit are serialized through one lifecycle semaphore.
- Stop order is the reverse of start order.
- A nonzero exit code is shown explicitly; Server Manager does not reinterpret it as success.
- The manager changes no process-wide current directory.

## 7. Main implementation files

| File | Responsibility |
| --- | --- |
| `App.xaml.cs` | Configuration overlay, validation, dependency setup, and application lifetime |
| `ServerManagerOptions.cs` | Process definitions, shutdown modes, and validation |
| `ManagedProcessSupervisor.cs` | Start/restart/stop serialization, process ownership, output pumps, and fallback termination |
| `ManagedProcessLogEntry.cs` | Structured lifecycle and stream entries |
| `MainWindowViewModel.cs` | CommunityToolkit.Mvvm state and bounded dispatcher/display queues |
| `ServerLauncherContext.cs` | Tray/window actions and supervisor orchestration |
| `MainWindow.xaml` | Combined virtualized log grid |
| `ServerManagerStandardInputShutdown.cs` in API Server | Opt-in graceful API shutdown protocol |

## 8. Automated evidence

`TomasAI.IFM.Application.ServerManager.UnitTests` verifies option validation, API/UI definition acceptance, structured
entry mapping, displayed-log bounds, pending-queue bounds, and visible drop evidence.

`TomasAI.IFM.Application.ServerManager.IntegrationTests` launches a real .NET helper process and verifies:

- 1,000 stdout plus 1,000 stderr lines are drained without deadlock;
- nonzero exit-code evidence;
- graceful stdin shutdown;
- forced process-tree fallback;
- stop-before-restart ordering and replacement startup;
- readiness-gated downstream startup; and
- safe missing-executable failure.

Release Development acceptance on 2026-08-20 exercised two complete API/UI cycles through the real supervisor. The
API reached HTTP readiness before each UI launch, UI.Net displayed its main window, both applications exited with
code 0 through their graceful paths, and no managed child process remained after final shutdown.

## 9. Remaining staged work

SM-S3 is complete. SM-S4 real-task enablement still requires approved Development/paper-trading execution and
calendar/idempotency review. SM-S5 still requires target-machine reboot/sign-out/outage/backup/soak evidence and named
operator approval. Production-grade topology and observability remain part of the later Aspire transition.

## 10. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 4.1 | 2026-08-20 | Added API readiness gating and recorded successful two-cycle real API/UI lifecycle acceptance. |
| 4.0 | 2026-08-20 | Added SM-S3 operations/UI, SM-S4 .NET 10 task modernization and disabled templates, and SM-S5 Windows Service/ACL/health/backup acceptance tooling. |
| 3.0 | 2026-08-20 | Added the implemented SM-S2 Scheduler Host, PostgreSQL/Quartz authority, local pipe client, and read-only scheduler dashboard boundary. |
| 2.0 | 2026-08-20 | Replaced the obsolete three-server description with the implemented SM-S1 API/UI supervisor, structured bounded logs, graceful shutdown protocol, tests, and staged boundaries. |
