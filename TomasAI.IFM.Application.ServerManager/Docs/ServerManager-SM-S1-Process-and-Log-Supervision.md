# IFM Server Manager SM-S1 Process and Log Supervision Gate

**Document type:** Implementation gate record

**Status:** Complete for entry into SM-S2

**Version:** 1.0

**Date:** 2026-08-20

**Owner:** IFM engineering

## 1. Gate decision

SM-S1 is complete. Server Manager now supervises API Server and UI.Net through configuration-driven process
definitions and provides a bounded combined Manager/lifecycle/stdout/stderr view. The obsolete Event, Telemetry, and
Predictive Model launch slots are no longer active.

This gate does not introduce Quartz, create schedules, install a Windows Service, or authorize paper/live trading.

## 2. Implemented behavior

- API starts before UI according to explicit `StartOrder` values.
- Each child has an explicit executable path and working directory.
- Arguments use `ProcessStartInfo.ArgumentList`; no shell is used.
- Server Manager never mutates `Environment.CurrentDirectory`.
- stdout and stderr are drained asynchronously and concurrently.
- process exit, nonzero exit code, start failure, graceful shutdown, timeout, and forced fallback are visible.
- Reset and exit retain and await owned lifecycle tasks.
- Stop occurs in reverse start order.
- API has an opt-in stdin shutdown message that enters normal ASP.NET/actor cleanup.
- UI uses its normal main-window close/NATS cleanup path.
- forced fallback terminates the owned process tree and is explicitly recorded.
- both pending and displayed UI logs are bounded; dropped pending entries are reported visibly.
- configuration loads the base file plus the selected `DOTNET_ENVIRONMENT` overlay from the deployment directory.

## 3. Configuration disposition

The default deployment paths are:

| Process | Working directory | Executable |
| --- | --- | --- |
| API Server | `C:\TomasAI\IFMAppDir\ApiServer` | `TomasAI.IFM.Application.Api.Server.exe` |
| UI.Net | `C:\TomasAI\IFMAppDir\UI.Net` | `TomasAI.IFM.UI.Net.exe` |

These paths are validated at launch rather than application configuration binding, so a missing deployment produces
visible per-process failure evidence instead of hiding the manager window or silently swallowing the exception.

## 4. Automated validation

Recorded Release validation:

```text
Server Manager build
Succeeded: 1 project, Warnings: 0, Errors: 0

API Server build (including stdin shutdown integration)
Succeeded, Warnings: 0, Errors: 0

Server Manager unit tests
Passed: 6, Failed: 0, Skipped: 0

Server Manager helper-process integration tests
Passed: 5, Failed: 0, Skipped: 0
```

The helper-process suite uses real redirected operating-system streams. Its high-output case writes 1,000 lines to
stdout and 1,000 lines to stderr and verifies both readers finish before exit completion.

## 5. Exit checklist

| Requirement | Result | Evidence |
| --- | --- | --- |
| Replace obsolete launch slots with API/UI definitions | Pass | `appsettings.json`, `ServerManagerOptions.cs` |
| Explicit paths and no global current-directory mutation | Pass | `ManagedProcessSupervisor.CreateStartInfo` |
| Concurrent stdout/stderr capture | Pass | output pumps and high-volume integration test |
| Bounded combined UI log | Pass | bounded pending/display queues and unit tests |
| API graceful shutdown | Pass | opt-in stdin protocol and helper integration test |
| UI graceful shutdown request | Pass | `CloseMainWindow` uses existing UI cleanup path |
| Bounded forced fallback with evidence | Pass | process-tree termination integration test |
| Reset stops old processes before replacements | Pass | restart integration test |
| Missing executable is visible and contained | Pass | failure integration test |
| Quartz/schedules introduced | Correctly not performed | Begins in SM-S2 |

## 6. Entry conditions for SM-S2

SM-S2 may now create the headless Scheduler Host and contracts projects, PostgreSQL schemas/migrations, Quartz
persistence, task catalog, durable run state, recovery, local IPC, and read-only Scheduled Tasks/Task Runs dashboard.

The scheduler is greenfield. Scheduler schemas start empty, and no Reference-domain schedule export or migration is
required.

## 7. References

- [Server Manager implementation details](ServerManager-Implementation-Details.md)
- [Scheduled-task supervision specification](ServerManager-Scheduled-Task-Supervision-Specification.md)
- [SM-S0 safety baseline](ServerManager-SM-S0-Safety-Baseline-and-Persistence-Proof.md)

## 8. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 1.0 | 2026-08-20 | Recorded SM-S1 API/UI supervision, bounded combined logs, graceful/forced shutdown evidence, and automated validation. |
