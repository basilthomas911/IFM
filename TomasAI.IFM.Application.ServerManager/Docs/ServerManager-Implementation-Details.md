# IFM Server Manager Implementation Details

**Status:** SM-S1 implemented

**Version:** 2.0

**Date:** 2026-08-20

## 1. Current responsibility

`TomasAI.IFM.Application.ServerManager` is the interactive WPF/tray supervisor for the current IFM API Server and
UI.Net desktop application. It starts the two configured processes, owns their lifecycle while the manager is
running, captures standard output and standard error concurrently, and displays a bounded combined log.

It is not yet the scheduled-task engine. Quartz, scheduler persistence, task definitions, scheduled-run history, and
the Scheduler Host Windows Service begin in SM-S2.

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
        "ShutdownMode": "StandardInput",
        "ShutdownInput": "shutdown"
      }
    ]
  }
}
```

`Key`, `DisplayName`, `WorkingDirectory`, and `ExecutablePath` are required. Keys are unique without regard to case.
Positive log and shutdown limits are mandatory. `StandardInput` requires a non-empty `ShutdownInput` value.

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
- stop-before-restart ordering and replacement startup; and
- safe missing-executable failure.

## 9. Remaining staged work

SM-S2 creates the separate Scheduler Host, Quartz/PostgreSQL persistence, scheduler contracts, task catalog, durable
run state, recovery, and a read-only scheduled-task dashboard. It starts with an empty scheduler store; there is no
legacy schedule export or import prerequisite.

SM-S3 adds schedule editing, manual execution, cancellation, durable stdout/stderr history, retention, and broader
failure injection. Production-grade topology and observability remain part of the later Aspire transition.

## 10. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 2.0 | 2026-08-20 | Replaced the obsolete three-server description with the implemented SM-S1 API/UI supervisor, structured bounded logs, graceful shutdown protocol, tests, and staged boundaries. |
