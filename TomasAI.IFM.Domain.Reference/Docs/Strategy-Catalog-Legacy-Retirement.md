# Trade strategy family Legacy retirement register

Updated: 2026-09-06

ConfigurationDb is the active strategy catalog authority. Reference Data Manager, Fund mandate/assignment selectors and Portfolio financial-policy limit editors use its exact Deployment identities. Legacy components below remain in the repository for comparison, migration and historical replay until the updated UI has been verified by the user.

| Legacy component | Current treatment | Deletion condition |
| --- | --- | --- |
| `UI.Net.Views/Reference/TradeStrategyFamilyReferenceView.cs` | Not routed by ReferenceForm | User verifies replacement UI; retire direct legacy UI tests together |
| `UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs` and its control | Not used by the active catalog | Same UI verification |
| `Domain.Reference/TradeStrategyFamilies/TradeStrategyFamilyCreationService.cs` | Retained for isolated legacy verification; normal actor no longer calls it | Legacy tooling explicitly retired |
| `Application.Storage/ReferenceDb/TradeStrategyFamilyCatalogStore.cs`, v3/v4 family tables | Historical source; no new normal actor writes | Migration audit/export is complete and no retained evidence requires these rows |
| `TradeStrategyFamilyBootstrapper` | Removed from normal API startup; explicit legacy bootstrap-only mode retained | Legacy bootstrap tooling and qualification retired |
| `GetTradeStrategyFamiliesAsync` / old family query | Read-only compatibility | All historical/tooling callers migrated or replaced by a historical reader |
| Old Create/Change/Remove family commands | Still deserialize; return a clear read-only migration error | All clients upgraded and transport compatibility no longer required |
| `TradeStrategyFamilyReadModel`, legacy seeds/enums, integer family ID/version fields | Historical DTOs remain readable | Historical serialization/version migration explicitly designed and verified |

`TradeStrategyFamilyCommandActor` remains the command host name for compatibility. Its active **Catalog** verb uses `StrategyCatalogService` and ConfigurationDb; the class itself is not disposable legacy code. The `TradeStrategyFamilyReference` wrapper also remains live: new records carry `CatalogDeployment` and zero legacy integer fields; old records preserve their original fields. Do not remove that wrapper with the retired editor.

The instrument-family classification (`Futures`, `FuturesOption`), `GetTradeStrategySymbolsAsync`, `TradeStrategySymbolStore`, instrument definitions and symbol product IDs are **not Legacy**. They describe market-data products, not strategy variants, and remain the source of exchange/currency evidence.

## Migration and verification

Normal API startup creates the additive ConfigurationDb schema and runs `StrategyCatalogMigration`. Stable IDs make the operation repeatable. Three named default families (Futures, Vertical Spreads, Iron Condor), their three strategies, four structures and twelve variants are inserted only if missing. The former generic Directional/RegimeAligned starter entries are retained as historical data but are no longer seeded or shown in the default view. Each latest active legacy family creates a separate Draft deployment with exact legacy provenance, horizon and resolved product metadata. Ambiguous or missing products remain empty with an explicit review message; nothing is guessed or published. Existing catalog versions and Fund permissions are never overwritten.

Maintenance mode runs the same migration without starting HTTP listeners, actors, hosted feeds or downloads:

```powershell
dotnet run --project TomasAI.IFM.Application.Api.Server --no-launch-profile -- --environment Development --migrate-strategy-catalog-only
```

Use the intended environment's configured connections. Startup verification (`--verify-startup-only`) still takes precedence and performs no schema/data migration.

Before retiring historical storage, verify the new Reference editor, review each imported deployment and product, and explicitly replace mandate permissions and policy limits. Keep existing Funds non-active while their new configuration is incomplete. Qualification/activation requires published capability-supported deployments and exact pipeline profiles; the TradeSelection implementation remains on hold. User UI approval alone is sufficient for removing unused editor code, but not for deleting historical evidence referenced by persisted events or workflows.
