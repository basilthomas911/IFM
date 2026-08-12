# Local Workstation Database Backup and Restore Phase 9 Validation Report

**Gate:** 9 - Console and WinForms migration

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Replaced the placeholder Database Backup Console with a NATS-only operator client using
  `IDatabaseBackupCommandApi` and `IDatabaseBackupQueryApi`.
- Added deterministic parsing for query, backup, cancellation, restore, restore-drill, approval, retention, follow,
  verification-status, and reconciliation-status operations.
- Added the specified stable exit-code contract, caller cancellation handling, JSON output, bounded query page sizes,
  and explicit `--confirm` requirements for destructive restore, cutover approval, and retention execution.
- Kept the console on public actor contracts only. It cannot invoke native PostgreSQL or Scylla tools, construct
  native paths, or access host credentials.
- Implemented `IDatabaseBackupModel` and `DatabaseBackupModel` using only typed NATS command/query APIs and the public
  DatabaseBackup event observer.
- Added immutable UI records for protection sets, recent operations, restore-point summaries, and complete bounded
  dashboard snapshots.
- Added `DatabaseBackupViewModel` with source/protection-set selection, non-blocking command acceptance, safe error
  reporting, bounded query refresh, and lifecycle-managed event observation.
- Migrated the existing WinForms backup view from per-database full/differential behavior to protection-set and
  source selection with accepted operation status, progress, safe diagnostic references, latest verified point, and
  latest restore-tested point.
- Kept view changes intentionally small by reusing the existing controls. Production restore and cutover controls
  were not added or enabled.
- Updated the WinForms composition root to register the new DatabaseBackup command/query APIs.
- Limited `SystemAdminUIEventConsumer` to authorized public DatabaseBackup domain-event verbs. Event callbacks never
  mutate bound UI state; they signal the WinForms thread to issue a targeted bounded query refresh.
- Made duplicate and out-of-order event notifications harmless: notification content is never treated as complete UI
  history and repeated signals only request another authoritative query refresh.

The public contract currently has no standalone verification-request or reconciliation-request command. Therefore,
the console `verify` verb queries the latest verified restore point, while `reconcile` evaluates the projected service
health response and returns the reconciliation-mismatch exit code when any selected service is not ready. No new
message contracts were invented during the client migration.

## Messaging boundary

This phase does not implement the broader `Notify` refactor described in
`Documents/system/Actor-Message-Types-and-Delivery-Conventions.md`. The existing SystemAdmin UI listener remains an
observational live listener and uses public domain events only as refresh signals. Moving UI progress/status updates to
purpose-built `Notify` contracts is deliberately deferred until the later messaging refactor. The UI never treats the
live notification stream as authoritative state.

## Validation evidence

### Console parser, safety, API, and exit-code tests

```text
dotnet test TomasAI.IFM.Domain.SystemAdmin.UnitTests/
  TomasAI.IFM.Domain.SystemAdmin.UnitTests.csproj --no-restore

Passed: 99
Failed: 0
Skipped: 0
```

The suite includes six console-specific tests covering structured parsing, duplicate/positional argument rejection,
public command submission and operation identity output, destructive confirmation, reconciliation mismatch, and
terminal follow-mode failure.

### WinForms model, view-model, and architecture tests

```text
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests/
  TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj --no-restore

Passed: 126
Failed: 0
Skipped: 0
```

The suite proves bounded query mapping, immutable UI state, command submission that remains asynchronously observable
while actor acceptance is pending, operation-identity signaling after acceptance, duplicate/out-of-order notification
safety, and the existing framework-neutral presentation and async technical-debt baselines.

### FlaUI dashboard smoke

```text
dotnet test TomasAI.IFM.UI.Net.SystemTests/
  TomasAI.IFM.UI.Net.SystemTests.csproj --no-restore --filter Category=Gate9System

Passed: 1
Failed: 0
Skipped: 0
```

The smoke test launches the migrated dashboard on an STA WinForms thread against disposable typed command/query API
substitutes. FlaUI attaches to, focuses, and verifies the live top-level window remains enabled. The owning STA verifies
that the disposable backend's `core` protection set is rendered, LocalWorkstation is selected, and the Request Backup
control remains enabled. The smoke does not submit a backup or invoke native tools.

The current test-host UI Automation provider exposes the top-level WinForms window but not its descendant controls.
For that reason, descendant state is inspected on the owning STA rather than being misreported as a FlaUI application
failure. The window lifecycle and responsiveness checks remain real FlaUI automation.

### Real NATS/PostgreSQL host regression

```text
dotnet test TomasAI.IFM.Domain.SystemAdmin.IntegrationTests/
  TomasAI.IFM.Domain.SystemAdmin.IntegrationTests.csproj --no-restore --filter Category=Gate5Integration

Passed: 2
Failed: 0
Skipped: 0
```

The real backend regression confirms safety-critical host composition and a fake native operation across NATS
JetStream, actor state, host restart, outbox replay, and the PostgreSQL projection.

### Full solution

```text
dotnet build TomasAI.IFM.sln --no-restore

Build succeeded.
0 Warning(s)
0 Error(s)
```

## Gate result

Gate 9 passed because the Console and WinForms dashboard use the new typed DatabaseBackup commands and queries over
NATS, command submission returns after actor acceptance, presentation state is rebuilt through bounded queries, public
event notifications cannot become authoritative UI history, UI-thread ownership and responsiveness are tested, the
live dashboard passes its FlaUI smoke, the real NATS/PostgreSQL host path remains green, and the full solution builds
without warnings or errors.

Legacy SystemAdmin backup contracts and unused per-database model/view-model code remain present intentionally. Their
removal is Phase 10 work and occurs only after this replacement gate is recorded as passing.

Phase 10 (Ubuntu 24.04 Docker qualification, runtime validation, and legacy removal) is the next pending phase.
