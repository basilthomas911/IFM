# UIR-5 through UIR-12 Development qualification - 2026-08-24

| Item | Result |
| --- | --- |
| Gates | UIR-5 Fund through UIR-12 legacy-resolution removal |
| Configuration | Debug, .NET 10 |
| Decision | Passed for non-live qualification; ready for UIR-13 live G0-G4 |

## Implemented boundary

- Moved Fund, Market Data, Feed, Analytics, Trade, application, and status execution adapters from `UI.Net.Models` into domain folders in `UI.Net.Services`.
- Renamed common execution to `IUiService<TService>`/`UiServiceBase<TService>` and isolated coded error callbacks with `AsyncLocal` so concurrent operations cannot overwrite each other's callback.
- Replaced `IAppRoot.GetModel<TModel>()` and Models assembly scanning with `IUiServiceCatalog`, whose named properties are registered explicitly in Startup.
- Added `IOptionPricingService` and a UI-owned option-Greeks result; ViewModels no longer reference `Framework.OptionPricer` directly.
- Removed command/query/event adapters, `BaseModel`, `IModel<>`, and `IEventModel` from `UI.Net.Models`.
- Reduced Models dependencies to the four shared value-contract projects currently required by UI records and policies.
- Registered forms as transient window lifetimes, disposed modal forms explicitly, and added a real Startup composition test. This corrected the singleton-form/transient-service lifestyle mismatch that previously caused `Startup.Configure()` verification to fail.
- Locked remaining rich backend read DTOs to the reviewed workflow-boundary inventory in [UI backend boundary DTO inventory](../UI-Backend-Boundary-DTOs.md).

## Test evidence

| Validation | Result |
| --- | ---: |
| Sequential full solution build | Passed; 0 warnings, 0 errors |
| Presentation unit and architecture | 232 passed |
| Fund BDD / unit / integration | 69 / 245 / 30 passed |
| Market Data BDD / unit / integration | 2 / 100 / 21 passed |
| Market Data Feed BDD / unit / integration | 314 / 482 / 46 passed; 4 integration tests skipped by fixture policy |
| Analytics BDD / unit / integration | 449 / 852 / 37 passed |
| OptionPricer BDD / unit / integration | 1 / 59 / 8 passed |
| Trade BDD / unit / integration | BDD command succeeded with zero scenarios discovered; 45 / 37 passed; 2 integration tests skipped by fixture policy |
| Application BDD / unit / integration | 1 / 5 / 1 passed |
| Non-live UI system and Startup composition | 27 passed |
| Total executable tests passed | 3,063 |
| `git diff --check` | Passed; line-ending notices only |

## Qualification boundary

The opt-in G0, G1, G2, G3, and G4 live process categories were deliberately not run in UIR-5 through UIR-12. They require approved Development services, credentials, deterministic data, exclusive process ownership, and an unlocked interactive Windows desktop. UIR-13 runs those live gates in order and records cleanup/evidence.

## Exit decision

UIR-5 through UIR-12 satisfy the non-live Development checkpoint. The solution builds, the real composition root verifies, all executable domain and presentation suites pass, generic model resolution is absent, and the remaining live-process qualification is isolated to UIR-13.
