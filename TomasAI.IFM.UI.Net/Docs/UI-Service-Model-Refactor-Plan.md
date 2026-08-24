# UI service and model refactor plan

This plan implements the boundary accepted in [UI service and model boundary decision record](UI-Service-Model-Refactor-Decision-Record.md). Gates are vertical slices so the application remains buildable and testable after each merge.

## UIR-0 baseline

UIR-0 is documentation and qualification only. It moves no production class and changes no runtime registration.

### Current coupling baseline

| Measure | Baseline |
| --- | ---: |
| C# files in `UI.Net.Models` | 37 |
| `BaseModel` derivations | 32 |
| Project references owned by `UI.Net.Models` | 14 |
| `GetModel<TModel>()` call sites in ViewModels | 150 |
| ViewModel files containing `GetModel<TModel>()` | 25 |
| ViewModel files referencing backend `*ReadModel` types | 31 |
| ViewModel files directly invoking a backend `ServiceApi` type | 0 |

The composition root currently assembly-scans `IModel<>` and `IAppRoot.GetModel<TModel>()` resolves the concrete adapter. These are tracked migration mechanisms and are removed in UIR-12.

The current project path is `UI.Net -> Views -> ViewModels -> Models`. ViewModels also reference `Framework.OptionPricer`. Models currently references the messaging/serialization infrastructure, status and shared event producers, `UI.EventConsumer`, application-shared contracts, Shared, and the Fund, Trade, Reference, SystemAdmin, OptionPricer, MarketData, MarketDataFeed, and MarketDataAnalytics shared domain assemblies. UIR-1 introduces Services between ViewModels and those backend-facing dependencies; it does not reverse the existing Views-to-ViewModels direction.

### Current Models inventory and assigned gate

| Gate | Current files | Classification and target |
| --- | --- | --- |
| UIR-2 | `BaseModel`, `Contracts/IModel`, `Contracts/IEventModel` | Legacy execution, error, and lifecycle infrastructure. Replace with shared Services results/subscription conventions; retain only until all slices migrate. |
| UIR-3 | `DatabaseBackupModel` | Command/query/event adapter plus four UI state records. Move backend behavior to System Administration services and UI records to Models. Pilot explicit DI and mapping. |
| UIR-4 | `ReferenceCommandModel`, `ReferenceQueryModel`, `LookupTypeEventModel`, `EconomicCalendarEventModel` | Reference command/query/event adapters. Move to Reference services and map UI-facing state. |
| UIR-5 | `FundCommandModel`, `FundQueryModel`, `FundEventModel`, `FundOrderEventModel` | Fund command/query/event adapters. Move to Fund services and subscriptions. |
| UIR-6 | `MarketDataCommandModel`, `MarketDataQueryModel`, `MarketDataEventModel`, `OptionTradeSpreadBarDataEventModel` | Market Data command/query/event adapters. Move to MarketData services and subscriptions. |
| UIR-7 | `MarketDataFeedCommandModel`, `MarketDataFeedQueryModel` | Mixed feed command/query/event behavior. Split into a feed service and explicit subscription boundary. |
| UIR-8 | `MarketDataAnalyticsCommandModel`, `MarketDataAnalyticsQueryModel`, `MarketDataAnalyticsEventModel`, `Operations/StrategyOperationsModel`, `SpreadDistributionJobModel` | Analytics and strategy command/query/event adapters. Move to domain services; keep presentation workflow in Operations ViewModels. |
| UIR-9 | Option-pricing dependencies currently used by ViewModels | Introduce an OptionPricer-facing UI service and UI models even though no dedicated legacy Models adapter exists. |
| UIR-10 | `TradeCommandModel`, `TradeQueryModel`, `TradePlacementCommandModel`, `TradePlacementEventModel`, `TradePlanQueryModel`, `TradePlanEventModel`, `TradePlanActionEventModel`, `TradePositionFeedEventModel`, `EndOfDayProcessEventModel` | Trade command/query/event adapters. Split by workflow while preserving correlation and subscription lifetime. |
| UIR-11 | `ApplicationEventModel`, `EventModel`, `StatusConsoleModel` | Application-wide response/event/status lifecycle. Move to application services with explicit lifetime ownership. |
| Retain in Models | `EasternTime`, `PositionEntryWindow` | Pure presentation policies. Keep in Models, optionally under a `Policies` folder, with no backend dependencies. |

### Service-locator hotspots

| ViewModel file | `GetModel<TModel>()` calls |
| --- | ---: |
| `App/IFMAppViewModel.cs` | 28 |
| `App/MarketEconomicCalendarViewModel.cs` | 2 |
| `App/StatusConsoleViewModel.cs` | 3 |
| `BaseEditorViewModel.cs` | 2 |
| `Contracts/IAppRoot.cs` | 1 |
| `Fund/AdjustFundTransactionViewModel.cs` | 2 |
| `Fund/CreateFundViewModel.cs` | 2 |
| `Fund/FundCashTransactionViewModel.cs` | 2 |
| `Fund/FundTransactionEditorViewModel.cs` | 3 |
| `MarketData/FuturesContractEditorViewModel.cs` | 4 |
| `MarketData/FuturesOptionContractEditorViewModel.cs` | 4 |
| `MarketData/MarketDataViewModel.cs` | 1 |
| `MarketData/YieldCurveRateEditorViewModel.cs` | 3 |
| `MarketData/YieldCurveRateEditViewModel.cs` | 1 |
| `Operations/StrategyOperationsViewModel.cs` | 1 |
| `Reference/EconomicCalendarEditorViewModel.cs` | 3 |
| `Reference/LookupTypeEditorViewModel.cs` | 3 |
| `Reference/ReferenceViewModel.cs` | 2 |
| `SystemAdmin/DatabaseBackupViewModel.cs` | 1 |
| `SystemAdmin/SystemAdminViewModel.cs` | 1 |
| `Trade/EndOfDayProcessViewModel.cs` | 5 |
| `Trade/FundOrderEditorViewModel.cs` | 2 |
| `Trade/IronCondor/IronCondorTradeOrderViewModel.cs` | 21 |
| `Trade/IronCondor/IronCondorViewModel.cs` | 47 |
| `Trade/TradeOrderEditorViewModel.cs` | 6 |
| **Total** | **150** |

### Backend read-model coupling

The following 31 ViewModel files contain backend `*ReadModel` types and must be reviewed in their assigned domain gate. A reference may be retained only when it is intentionally a transient boundary value rather than presentation state.

| Area | Files |
| --- | --- |
| App | `IFMAppViewModel`, `MarketEconomicCalendarViewModel`, `StatusConsoleViewModel` |
| Contracts and extensions | `ITradeOrderConfirmationService`, `LookupTypeListExtension` |
| Fund | `AdjustFundTransactionViewModel`, `CreateFundViewModel`, `FundCashTransactionViewModel`, `FundTransactionEditorViewModel`, `FundTransactionUIViewModel` |
| MarketData | `FuturesContractEditorViewModel`, `FuturesEodDataUIViewModel`, `FuturesOptionContractEditorViewModel`, `FuturesTradeSignalUIViewModel`, `FuturesTradeStatusUIViewModel`, `MarketDataViewModel`, `YieldCurveRateEditorViewModel`, `YieldCurveRateEditViewModel` |
| Operations | `FuturesItiSignalEventRow` |
| Reference | `EconomicCalendarEditorViewModel`, `LookupTypeEditorViewModel`, `MDIForwardLossRatioUIViewModel`, `ReferenceViewModel` |
| SystemAdmin | `SystemAdminViewModel` |
| Trade | `EndOfDayProcessViewModel`, `FundOrderEditorViewModel`, `IronCondorTradeInfoViewModel`, `IronCondorTradeOrderViewModel`, `IronCondorViewModel`, `TradeOrderConfirmationViewModel`, `TradeOrderEditorViewModel` |

## Gate sequence

### UIR-0 - Inventory, decision, and baseline

- Record project references, adapter classifications, service-locator usage, and backend-contract leakage.
- Accept the target dependency direction and mapping ownership.
- Run presentation unit tests and non-live system-test infrastructure coverage.
- Exit: the decision, plan, and dated test evidence are checked in; production code is unchanged.

### UIR-1 - Establish `UI.Net.Services`

- Create the Services project and add it to the solution.
- Define permitted project references and wire the one-way project dependency graph.
- Add architecture tests that prevent Models from acquiring transport, backend API, or event-consumer dependencies.
- Add staged assertions that prevent migrated ViewModels from using `GetModel<TModel>()` or backend read models.
- Exit: empty Services boundary builds and all architecture tests pass.

### UIR-2 - Shared service conventions

- Define UI operation result/error contracts, cancellation rules, mapping conventions, and subscription ownership.
- Add reusable helpers only where at least two domain slices need the same behavior.
- Define XML documentation expectations for public interfaces, methods, and model properties.
- Exit: conventions are unit-tested and do not require Views or the composition root.

### UIR-3 - System Administration backup pilot

- Separate the four backup UI state records from `DatabaseBackupModel` transport behavior.
- Add an explicit backup service interface, implementation, mapping, and subscription lifetime.
- Inject that interface into `DatabaseBackupViewModel`; remove its `GetModel` call.
- Preserve the accepted Gate 9 backup dashboard behavior.
- Exit: pilot unit, architecture, and `Gate9System` tests pass; legacy backup adapter is removed.

### UIR-4 - Reference domain

- Migrate reference commands, queries, lookup events, and economic-calendar events.
- Replace backend read models in Reference ViewModels with UI models where they are screen state.
- Exit: Reference unit/integration coverage and relevant G0-G2 UI journeys pass.

### UIR-5 - Fund domain

- Migrate Fund commands, queries, Fund events, and Fund-order events.
- Convert fund, transaction, order, balance, and profit/loss screen state to UI-owned models.
- Exit: Fund unit/integration coverage and reversible Fund UI journeys pass.

### UIR-6 - Market Data domain

- Migrate Market Data commands, queries, events, and spread-bar events.
- Convert contract, curve, bar, signal, and status presentation state to UI models.
- Exit: Market Data unit/integration coverage and applicable G0-G3 journeys pass.

### UIR-7 - Market Data Feed domain

- Migrate feed command/query behavior and expose event flow as explicit subscriptions.
- Verify idempotent start/stop and dispose behavior.
- Exit: feed unit/integration coverage and accepted feed lifecycle UI journeys pass.

### UIR-8 - Analytics and strategy operations

- Migrate analytics commands, queries, events, spread-distribution jobs, and strategy operations.
- Organize Operations by domain workflow; do not turn Services into presentation coordinators.
- Exit: analytics/strategy unit and integration tests plus related G0/G3/G4 journeys pass.

### UIR-9 - OptionPricer boundary

- Replace direct OptionPricer dependencies in ViewModels with a UI-facing service.
- Map pricing results to UI models required by editor and trade workflows.
- Exit: OptionPricer and affected ViewModel tests pass with no direct backend dependency in migrated ViewModels.

### UIR-10 - Trade domain

- Migrate Trade, placement, plan, action, position-feed, and end-of-day workflows.
- Preserve command correlation, confirmation, failure reporting, and listener teardown.
- Exit: Trade unit/integration coverage and all applicable G1-G4 journeys pass.

### UIR-11 - Application and status lifecycle

- Migrate shared command-response, application-event, and status-console behavior.
- Give the application lifetime explicit ownership of long-lived subscriptions.
- Separate UI session state from transport listener implementations.
- Exit: startup, status, shutdown, reconnect, and repeated-lifecycle tests pass.

### UIR-12 - Remove legacy model resolution

- Remove `BaseModel`, `IModel<>`, `IEventModel<>`, Models assembly scanning, and `IAppRoot.GetModel<TModel>()`.
- Remove superseded Models project references and all compatibility shims.
- Turn staged architecture rules into project-wide assertions.
- Exit: zero `GetModel<TModel>()` calls, zero backend API/event-consumer references from Models, and zero backend read-model screen state in ViewModels unless explicitly documented as a boundary DTO.

### UIR-13 - Full qualification and documentation closeout

- Run solution build, all presentation/unit/integration suites, and accepted G0-G4 system gates in order.
- Update implementation and test specifications to describe the completed structure.
- Record final dependency metrics against the UIR-0 baseline.
- Exit: all required tests pass, cleanup succeeds, and the decision record is marked Implemented.

## Per-gate working rules

Every implementation gate must:

1. Move a complete vertical workflow rather than only renaming a class.
2. Add the explicit service interface before changing its ViewModel consumer.
3. Put backend-to-UI mapping in Services and retain presentation workflow in ViewModels.
4. Preserve cancellation, command correlation, error semantics, and event cleanup.
5. Add XML documentation to changed public classes, interfaces, methods, and properties.
6. Remove the superseded adapter after all consumers in that slice migrate.
7. Run domain unit/integration tests, presentation architecture tests, and relevant UI system categories.

## Validation matrix

| Change type | Minimum validation |
| --- | --- |
| Project/dependency boundary | Solution build and presentation architecture tests |
| Mapping or UI model | Mapper/service unit tests and ViewModel tests |
| Command/query service | Domain-specific client integration tests plus affected ViewModel tests |
| Event subscription | Lifecycle, correlation, cancellation, repeated start/stop, and teardown tests |
| Startup/application lifetime | G0 and G4 process gates |
| Screen workflow | Relevant G1, G2, or G3 process gate |

Live process categories remain opt-in and require the approved Development services, data, credentials, and an unlocked interactive Windows desktop. A routine refactor run must always execute the non-live unit and infrastructure coverage; it must not silently claim an opt-in process gate.
