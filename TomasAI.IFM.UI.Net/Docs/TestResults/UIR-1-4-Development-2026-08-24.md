# UIR-1 through UIR-4 Development qualification - 2026-08-24

| Item | Result |
| --- | --- |
| Gates | UIR-1 Services boundary; UIR-2 conventions; UIR-3 backup pilot; UIR-4 Reference domain |
| Implementation base | `1fbb38fb` |
| Configuration | Debug |
| Decision | Passed |

## Scope

- Added `TomasAI.IFM.UI.Net.Services` and enforced the one-way ViewModels-to-Services-to-Models dependency.
- Added transport-neutral operation results, coded errors, mapping helpers, cancellation behavior, and independently owned asynchronous event subscriptions.
- Migrated database-backup commands, queries, dashboard state, and notifications to `IDatabaseBackupService`.
- Migrated Reference commands, queries, lookup events, and economic-calendar events to `IReferenceDataService` and `IEconomicCalendarService`.
- Replaced Reference screen state with UI-owned records and passed the new services explicitly through composition and manually constructed WinForms workflows.
- Removed `DatabaseBackupModel`, `ReferenceCommandModel`, `ReferenceQueryModel`, `LookupTypeEventModel`, and `EconomicCalendarEventModel`.

## Test evidence

| Validation | Result |
| --- | --- |
| Sequential full solution build | Passed, 0 warnings and 0 errors |
| Presentation unit and architecture suite | Passed, 227/227 |
| Reference domain unit suite | Passed, 8/8 |
| Reference domain integration suite | Passed, 14/14 |
| Reference BDD project | Build/test command passed; zero tests discovered because both current test classes are empty |
| Non-process UI system coverage | Passed, 26/26 |
| Gate 9 backup dashboard desktop smoke test | Passed, 1/1 |

The first parallel solution build encountered a shared native DataBento build-state file lock. Re-running the solution build sequentially with `-m:1` passed cleanly; this was build concurrency contention rather than a source failure.

## Runtime behavior retained

- Command identifiers are correlated with exact terminal notifications, including completion-before-response delivery.
- Listener start, stop, repeated calls, cancellation, and asynchronous disposal remain explicitly owned.
- Coded backend failures are mapped to `UiOperationError` and `UiOperationException` without exposing Service API result types to ViewModels.
- Economic-calendar import failures remain owned by the initiating maintenance/startup workflow and do not create duplicate dashboard errors.
- The startup economic-calendar listener is disposed even when listener startup fails.

## Qualification boundary

Opt-in G0, G1, G2, G3, and G4 process categories were not run. They require approved Development services, credentials, test data, and an unlocked interactive Windows desktop. The non-process system coverage and the bounded Gate 9 desktop smoke test were run.

## Exit decision

UIR-1 through UIR-4 pass their Development checkpoint. The Services boundary and shared conventions are active, the backup and Reference slices use explicit DI and UI-owned state, superseded adapters are deleted, and the required non-live/domain qualification is green. UIR-5 may begin with the Fund domain vertical slice.
