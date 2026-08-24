# UIR-0 Development baseline - 2026-08-24

| Item | Result |
| --- | --- |
| Gate | UIR-0 - Inventory, decision, and baseline |
| Baseline commit | `5f7963e8` |
| Configuration | Release |
| Decision | Passed |
| Production code changed | No |

## Scope

UIR-0 records the existing UI dependency boundary before production classes move. It accepts the target Services/Models split, assigns every current Models source file to a migration gate or retained presentation policy, and qualifies the current non-live UI baseline.

## Inventory evidence

| Measure | Observed |
| --- | ---: |
| C# source files in `TomasAI.IFM.UI.Net.Models` | 37 |
| Classes derived from `BaseModel` | 32 |
| Project references in `TomasAI.IFM.UI.Net.Models.csproj` | 14 |
| `GetModel<TModel>()` call sites in ViewModels | 150 |
| ViewModel files containing `GetModel<TModel>()` | 25 |
| ViewModel files referencing backend `*ReadModel` contracts | 31 |
| ViewModel files directly invoking backend `ServiceApi` types | 0 |

The 37 Models files comprise 32 legacy backend adapter classes, `BaseModel`, two adapter contracts, and two pure UI policies (`EasternTime` and `PositionEntryWindow`). The detailed ownership and gate assignment is recorded in [UI service and model refactor plan](../UI-Service-Model-Refactor-Plan.md).

## Test evidence

### Presentation unit and architecture suite

```powershell
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests/TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj --configuration Release --nologo
```

Result: Passed - 213 passed, 0 failed, 0 skipped, 213 total.

### Non-process UI system infrastructure

```powershell
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --configuration Release --nologo --filter "Category=G0Infrastructure"
```

Result: Passed - 7 passed, 0 failed, 0 skipped, 7 total.

This documented category does not launch the API Server or desktop. Its build compiled the UI, Views, ViewModels, Models, API Server reference graph, and system-test assembly. Opt-in G0-G4 process gates were not run because UIR-0 changes documentation only and does not alter runtime behavior.

## Observations

- The initial sandboxed restore attempts could not access NuGet. The same commands passed after approved network access restored the required packages; this was an execution-environment restriction, not a product failure.
- The system-test build reported existing CS9107 actor-context capture warnings. UIR-0 introduced no actor or production source changes.
- The current implementation continues to use Models assembly scanning and `IAppRoot.GetModel<TModel>()`. Their presence is expected at this baseline and their removal is assigned to UIR-12.

## Exit decision

UIR-0 passes. The architecture decision, complete migration gate plan, inventory metrics, and green non-live baseline are recorded. UIR-1 may create the empty `TomasAI.IFM.UI.Net.Services` boundary and add staged dependency assertions.
