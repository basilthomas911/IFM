# IFM Windows Rust UI full migration plan

Updated: **2026-10-23**. Target project directory: **`TomasAI.IFM.UI.Rust.eGui`**. Target desktop artifact: **`TomasAI.IFM.UI.Rust.eGui.exe`**.

Status: revised implementation plan and source inventory. This update does not implement or qualify the standalone application.

## Current target and authority (2026-10-23)

This revision plans a complete standalone Windows frontend: **one Rust UI executable and one UI process**, with egui rendering, Rust presentation models, Rust view-state controllers, Rust API clients and Rust event consumers. The shipped UI must run without hostfxr, a CLR, managed UI/bridge assemblies, a local pricing DLL, or a helper UI process. Backend API, MarketData Host/workers, NATS, databases and broker services remain separate existing services.

The existing `TomasAI.IFM.UI.Rust` is a hybrid implementation: `src/app/hosting.rs` loads hostfxr, and `TomasAI.IFM.UI.Rust.Bridge.Net` supplies managed sessions and view-model lifetimes. Its rendered screens are reusable reference implementations; its managed ownership is **not** the final architecture. Creating or renaming an executable alone does not complete this migration.

The new folder will contain the Rust Cargo package, application state, transport, controllers, tests and embedded resources. Use a Rust-compatible package/library name such as `ifm-ui-egui` / `ifm_ui_egui`; Windows packaging assigns the requested executable filename. Assets and localized strings should be embedded where practical. External configuration, credentials, logs and user journals remain data files. OS-provided graphics/accessibility libraries are normal Windows dependencies. No application-specific .NET or pricing runtime is part of the final frontend package.

This section and the refreshed inventory supersede the older hybrid ownership, source counts and migration estimates below. The older entries remain a historical record of WinForms/native control work, not evidence that the standalone migration is complete. The [standalone migration design](IFM-Standalone-Rust-UI-Migration-Plan-v1.0.md) remains the detailed protocol reference; the executable/project naming and inventory in this revision take precedence.

### Refreshed inventory and evidence

- [Complete model/view-model inventory and source counts](IFM-Windows-Rust-UI-Model-Inventory-2026-10-23.md).
- [Machine-readable references and target disposition for every model/view-model source file](IFM-Windows-Rust-UI-Model-Inventory-2026-10-23.csv).
- [Supporting services, consumers, API clients and bridge model/controller inventory](IFM-Windows-Rust-UI-Dependency-Inventory-2026-10-23.csv).
- [Backend boundary audit: all 148 view-model, model and service source files](IFM-Rust-UI-Backend-Boundary-Audit-2026-10-23.md), with the [per-file backend/client decisions](IFM-Rust-UI-Backend-Boundary-Inventory-2026-10-23.csv). This audit supersedes earlier narrow claims about local financial calculations.
- Reproduce with `python scripts/UI/Rust/Update-RustUiModelInventory.py --date 2026-10-23`.

The inventory scans tracked source rather than leftover build directories. It separates direct Rust bridge references, WinForms references, indirect dependencies, registration-only references, and rows with no production caller found. These are conservative source-reference classifications, not runtime execution counts. Reflection, same-name types, dependency injection and method-level reachability need explicit review; a missing direct bridge reference is not permission to delete a model. Every row needs a final disposition and verification evidence.

The standalone behavior baseline is the current Rust UI. WinForms-only source rows are reachability/exclusion evidence; unused WinForms calculations do not require new backend equivalents. Preserve the current hybrid while building its standalone replacement.

| Refreshed model/view-model scope | Files |
| --- | ---: |
| `UI.Net.ViewModels` | 81 |
| `UI.Net.Models` | 15 |
| **Total model/view-model source files** | **96** |
| Direct Rust bridge reference | 58 |
| WinForms reference without a direct bridge reference | 18 |
| Indirect production reference requiring entry-path review | 16 |
| Test references only | 2 |
| No external type/method identifier reference found | 1 |
| C# global import infrastructure | 1 |

The test-only files are `PortfolioConcurrencyRefreshModel.cs` and `PortfolioNavigationModel.cs`. `Trade/IronCondor/IronCondorTradeInfoViewModel.cs` declares `IronCondorTradeInfoReadModel` and has no external identifier reference in this scan. `GlobalUsings.cs` is import infrastructure. These classifications identify review work; this plan does not authorize their deletion. Extension-method call identifiers are included so helpers such as `ObservableUiServiceExtensions` are not mistaken for unused classes.

The supplemental checklist covers **199 files**: 52 UI service files, 39 UI event-consumer files, 1 composition file, 53 NATS API client files and 54 managed bridge files. It includes publication and session models outside the primary model projects. The visual baseline contains 134 WinForms view-project source files and 49 Rust source files. These counts include helpers and partial/designer files and are not counts of distinct screens or wire operations.

### Ownership rules

| Responsibility | Final owner |
| --- | --- |
| Rendering, focus, tabs, selection, window geometry, colors, formatting and editable drafts | Rust egui thread and typed view state |
| Queries, commands, function requests, subscriptions, reconnect, correlation and client journals | Rust background runtime and scoped controllers |
| Authoritative valuation, Greeks, payoff/maximum loss, trade plans, risk decisions, financial posting validation and persistence | Existing backend actors/services; add backend operations for uncovered client calculations |
| ValueDate and market-session dates | API server exclusively; no local value-date provider or machine-date fallback |
| UTC instants, display-zone conversion and monotonic client durations | Preserve wire timestamps; convert for display; use monotonic time for client timing. Do not substitute UI time for server business dates |
| Option feed ownership, lease reconciliation, two-minute recovery and one-time API restart | Existing backend/ServerManager workflow; Rust presents state and errors and reconciles after reconnect |

All financial and identity values retain exact representations. Floats are permitted for plot coordinates and geometry, not authoritative currency, quantities, strike prices or canonical hashes. UI enable/disable rules can mirror server state for usability; backend authorization and business validation remain mandatory.

### Feature and behavior migration matrix

Supported current-Rust feature rows are **planned standalone Rust work**, even where the hybrid already renders the screen. Explicitly excluded WinForms-only rows are scope evidence.

| Area | Managed behavior to account for | Rust target / required gate |
| --- | --- | --- |
| Shell, navigation and startup | `IFMAppViewModel`, `IAppRoot`, navigator/dispatcher/interaction contracts, `RustShellViewModelSession`, dashboard/sidebar sessions | `app` and `state/shell`; API-owned ValueDate, confirmed feed state, recovery/initialization error dialogs, settings and shutdown |
| Market outlook, ITI and charts | EOD/trade-signal display models, `MarketDataFeedHealthMonitor`, `MarketChartSession`, `MarketOutlookPresentation`, graph-window models | `state/market`; preserve source timestamps, provisional indicators, baseline/stale status, graph gaps and colors |
| Economic calendar | Both calendar view models, editor/readonly views and calendar session | `state/reference/calendar`; same filters, CRUD, timestamp identity and cancellation |
| Contracts, provider mapping and yield curves | Contract editor/view models, instrument selector, convention editor, provider mappings and draft review | `state/market/reference`; qualified selections, paging, explicit Draft import confirmation and server dates |
| Reference manager | `ReferenceViewModel`, lookup editor, parameter-set editors and comparisons, strategy catalog session | `state/reference`; preserve Reference as the WinForms manager flow, without adding menu items underneath the Reference menu |
| Strategy/workflow details and Risk History | Strategy operations/details/presentation, assessment/risk presenters, inspection backend and sessions | `state/strategy`; stable identities, cached trees, exact server decisions and current evaluation-date flow |
| Portfolio/fund administration | Administration and metrics view models, navigation/identity/allocation/fund editor models, portfolio session partials | `state/portfolio`; all create/edit/allocation/risk-envelope/template-assignment paths and approved dialog layouts |
| Financial ledger and posting | Financial view models, drafts, control preparation, operation state and both pending-operation stores | `state/portfolio/financial` plus journal adapters; exact request identity/hash, concurrency revisions, uncertain outcomes and restart reconciliation |
| Order browser and fund orders | `TradeOrderEditorViewModel`, `FundOrderEditorViewModel`, order editor models and workspace session | `state/trading/orders`; server paging, selection, date filters, lifecycle and acknowledged actions |
| Broker/manual composition | `BrokerManualTradeOrderViewModel`, initialization/preview/leg-selection/mutation helpers, confirmation, `TradeComposerSession` | `state/trading/composer`; complete preview, account qualification, component identity, backend-calculated risk, broker acknowledgement and outcome reconciliation |
| WinForms-only Iron Condor entry (excluded) | `IronCondorTradeOrderViewModel`, legacy entry view, trade-info models and local Greeks calls | Current Rust uses `BrokerManualTradeOrderViewModel`; no backend clone or standalone port of the unused WinForms entry model is required |
| Established trades, positions and plans | `IronCondorViewModel`, trade-info/spread-bar models, monitor row/cache/update helpers and `TradeWorkspaceSession` | `state/trading/monitor`; API ValueDate, saved live-feed intent, P&L bars using maximum gain/loss, valid-value preservation, EOD refresh and error presentation |
| Spread history | Range query/client service and chart merge in the workspace | Seed six hours across trading-value-date boundaries using API time, preserve baseline on failure, merge buffered live updates, retain once-per-minute history sampling |
| Actor and market-data operations health | Actor control/health snapshots, operations-health view model/session and HTTP health adapters | `state/operations/health`; preserve permissions, generation checks, lifecycle actions and visible failures |
| Scheduled tasks, backups and System | Scheduled-task and backup view models/models/sessions, `SystemAdminViewModel` | `state/operations`; complete setup, scheduling, preview, execution confirmation, logs/history paging, progress and cancellation |
| Status console and notifications | Status-console model/services, terminal correlation, all UI event consumers and initialization/recovery notices | `services/notifications`; deduplicate per operation/outcome and scope, retain visible terminal failures, do not confuse indicator failure with feed shutdown |
| Shared lifecycle/presentation helpers | Observable/base-editor/async-operation classes, presentation errors/colors, lookup extensions, field editors and time conversions | Rust ownership, enums, state transitions and formatting; retire C# infrastructure only after each dependent feature is qualified |

### Concrete gaps found in the refreshed review

Execute the [current Rust UI backend processing implementation plan](IFM-Current-Rust-UI-Backend-Processing-Implementation-Plan.md) before standalone client cutover. It maps B1-B7 to existing actors and requires current API/managed-client/desktop verification.

1. **Close the seven reviewed backend boundary groups.** The full audit identifies B1 order candidate risk/deployment/tick preparation, B2 confirmation amounts/fees, B3 fund performance metrics, B4 realtime cache financial summaries, B5 saved-position/history valuations, B6 composer financial summaries, and B7 server-owned business clock/session/range policy. `SelectedOrderPriceRisk.MaximumOptionLoss` is one part of B1, not the complete list. Reuse existing backend preview and realtime calculations. The WinForms-only `OptionPricingService.CalculateGreeks` path does not require a new backend equivalent for this migration.
2. **Port financial request helpers to Rust.** `FinancialControlPreparation` and `FinancialPostingPreparation` assemble requests, prechecks, expected versions and canonical hashes; these can remain client-side. Existing backend ledger validation and persistence remain authoritative. Preserve accepted contracts, exact decimals and hashes; no new financial preparation endpoint is needed solely to replace these helpers.
3. **Pending journals require compatibility.** `PendingFinancialOperationStore` and `PendingFinancialConfigurationStore` retain exact requests and uncertain operation IDs under the user profile. Port loading, serialization, atomic replacement, locks, size bounds and reconciliation. Never generate a replacement operation ID because the UI restarted or transport timed out.
4. **Managed orchestration exists outside view models.** Bridge sessions directly access APIs, format publications, cache rows, apply update generations and dispatch commands. `UiServiceCatalog`, client composition, API clients and UI event consumers are part of the migration boundary. Porting only the model projects would leave a managed dependency.
5. **Preserve recent runtime fixes.** Include API-only ValueDate, durable live-feed restoration, two-minute option/API recovery messages, EOD updates, six-hour chart range/merge, latest-value queue behavior and daily indicator baseline/stale/error reporting in the acceptance registry.
6. **An old rendering port is not standalone qualification.** Most native views consume managed MessagePack publications today. Reuse their geometry/styles/widgets while replacing their provider and action owner. Fixture rendering does not verify broker actions, field validation, backend queries or subscription cleanup.

### Implementation phases

| Phase | Deliverable | Completion gate |
| --- | --- | --- |
| 0 — Freeze scope and contracts | Reconcile every inventory row against menu routes, construction/registration and bridge dispatch; record each command/query/function/event, endpoint, payload, identity, hash and owner | No unclassified active model, helper, bridge action or event path; ambiguous/dead-code candidates documented separately |
| 1 — Create the single Rust application | Create `TomasAI.IFM.UI.Rust.eGui`; one UI binary/event loop, embedded assets/catalog, typed feature modules, background task supervision and configuration | Starts and closes on Windows without loading hostfxr, CLR or any managed UI assembly; old hybrid stays available for comparison |
| 2 — Implement direct backend compatibility | Rust request/reply and subscriptions over existing NATS, MessagePack-CSharp/LZ4 compatibility, exact IDs/decimals/dates/enums and existing HTTP health adapters | Cross-language golden payloads and actual isolated NATS round trips, compression boundaries, cancellation, errors, tracing headers and reconnect |
| 3 — Close backend preparation gaps | Complete B1-B7 in the backend boundary audit; reuse existing preview/realtime engines and exclude unused WinForms pricing paths | UI consumes server-prepared financial values and business context; integrated tests cover risk/fees, metrics, saved/live projections and server-controlled dates; financial request helpers remain portable client code |
| 4 — Port shared client state and persistence | Scoped view lifetimes, operation correlation, unknown-outcome reconciliation, compatible journals, caches and queue policies | Deterministic restart/reconnect tests; latest market snapshots can coalesce while commands, financial outcomes and terminal errors remain reliable |
| 5 — Port feature groups | Shell/market/reference; strategy/inspection; operations; portfolio/financial; order composition; established monitoring, following the matrix | Each inventory row has a Rust owner or an approved replacement, and each supported view/action has source-linked parity evidence |
| 6 — Full integration and desktop qualification | Real API query/event workflows, isolated/emulator order and ledger tests, both recovery branches, startup/close/open/reconnect and all supported forms/dialogs | Existing API + Rust client integration passes; FlaUI/AccessKit UIA checks and Rust widget/visual checks cover geometry, controls, actions, accessibility and failures |
| 7 — Package and cut over | Produce `TomasAI.IFM.UI.Rust.eGui.exe`; update VS Code tasks, build/package scripts, ServerManager process paths and diagnostics | Package starts without .NET installed and has no managed/helper UI process; secrets remain external; full acceptance evidence recorded before legacy retirement approval |

Phases are dependency ordered. Feature groups can be implemented incrementally, but the final executable cannot call back into the old managed bridge. Move or reuse tested Rust view code without requiring the old hybrid executable as a production dependency. Do not remove WinForms or the hybrid as part of this planning update.

### Testing, acceptance and inventory closure

- **Protocol:** both directions for each reachable wire contract, MessagePack numeric/map keys, extension values, LZ4 framing, null/default fields, UTC/DateOnly/time spans, enum/union values, decimals and canonical hashes. Offline C# fixture generators may remain test tools; they are not runtime dependencies.
- **State:** pending/loading/success/failure/cancelled/unknown outcomes; request identity across retry; late results after closing/reopening a view; stable selections; subscriptions detached exactly once; completed requests removed from bounded history.
- **Market/monitor integration:** API-provided ValueDate at rollover and historical dates; six-hour database history before/alongside buffered live updates; no clearing a valid baseline on failed refresh; valid values survive N/A updates; independent dataset recovery; indicator baseline and error notices.
- **Trading/financial:** account/environment qualification, emulator isolation, immutable component IDs, broker acknowledgements, no blind retry after an uncertain submission, both journal formats and server rejection handling. Do not use real orders or financial writes merely to establish UI parity.
- **Desktop:** inventory all reachable forms and runtime-generated controls; preserve approved Rust color conventions, layout/dimensions, labels, selection, keyboard/focus, enabled states, scrolling and border containment. FlaUI is an external Windows acceptance runner; Rust widget tests and screenshots supplement controls that UIA cannot individually address. Missing accessibility evidence remains an open gate.
- **Packaging:** inspect imports and loaded modules; test on a clean Windows environment without .NET; verify exactly one UI process and no hostfxr/bridge load, no local pricing engine and no connection from the UI to a database. Configuration/journal/log files do not count as extra executable processes.
- **Performance:** keep I/O and expensive projection off the egui thread; measure allocation rate, resident memory, queue depth, frame time and end-to-end update latency. Bound grids by viewport and server paging. Use latest-value replacement only for disposable market display updates, with per-entity identity; never evict financial commands or terminal notifications as if they were quotes.
- **Closure:** every CSV row needs a final Rust module/backend owner or a justified inactive/merged disposition, its operation mapping and test evidence. Repeat the inventory before cutover to detect newly added models or routes. Static counts alone cannot certify full migration.

### Feasibility conclusion

**A complete migration to one Rust executable is feasible.** Existing native egui views provide a substantial rendering base, and the backend already supplies command/query/event boundaries. The work is a client orchestration, contract and state-lifetime migration as well as a rendering migration. The remaining local financial helpers and request/journal compatibility are concrete implementation dependencies, not evidence that a second UI process or a CLR must remain. Full standalone parity is not yet verified; closure requires the per-row and per-feature gates above.

## Historical WinForms/hybrid migration record

Date: 2026-10-13. Windows first; WinForms remains supported. Full migration implementation and testing authorized by the user on 2026-10-13. WinForms remains available during migration.

## Pilot architecture and ownership

The Rust executable hosts .NET 10 through hostfxr. C# owns service calls, workflow query hydration, notifications, reconciliation and view-model lifetimes. Rust owns immutable display snapshots, cached navigation, grid selection and rendering. StrategyOperationsComposition is shared by the WinForms Startup and live Rust bridge. The existing StrategyOperationsViewModel is reused; initialization now clears a previous backend error to allow reconnection status to recover.

The bridge owner publishes completed snapshots. Live selection uses stable per-workflow action tokens, rather than a row's transient index. Render functions never perform queries. Grid datasets transfer as one bounded publication (16 MiB maximum); future datasets exceeding that size need chunked, versioned transfer before adoption.

One canonical catalog generates C# and Rust keys. Financial display values retain invariant exact decimal strings; display sorting does not mutate them. Dates are ISO UTC strings. Microsoft Sans Serif 10pt is loaded from Windows, without redistributing the font.

## Dependency and lifecycle inventory

| Component | Reuse | Owner / shutdown |
| --- | --- | --- |
| StrategyOperationsViewModel | Existing source unchanged | Managed session; dispose subscriptions and reconciliation |
| StrategyOperationsService | Existing service through shared composition | Managed client lifetime |
| MarketDataAnalyticsQueryApi / IntrinsicTimeStrategyWorkflowQueryApi | Existing C# NATS clients | Managed transport lifetime |
| FuturesItiSignalUIEventConsumer / IntrinsicTimeStrategyWorkflowUIEventConsumer | Existing notification fanout | View scope; StopAsync before disposal |
| NatsActorProducer / NatsConnectionManager | Existing C# transport | Stop local producer, then dispose; no backend stop command |
| StrategyWorkflowPresentation | Existing immutable semantics | Adapted to typed display snapshots |
| StrategyWorkflowDetailsAccordion | WinForms-specific control | Retained; used by linked regression test |
| DarkTradingTheme | Existing WinForms implementation | Rust typography matches its font settings |
| hostfxr function table | New native boundary | Opaque handles; caller-owned buffers; contained errors |

C# owner preparation is bounded to 10,000 tree nodes, depth 12 and 200 collection entries per node, with visible truncation markers. Broad application navigation and editing are not exposed by the pilot ABI. They require view-specific action envelopes and acknowledged outcomes before those screens migrate.

## Control mapping

The source-linked [control occurrence inventory](IFM-WinForms-Control-Inventory.csv) enumerates constructors, declarations and uses throughout the view project. Occurrences are not a count of distinct runtime controls. Target-typed new expressions and custom controls must be reconciled as each screen migrates; custom rendering cannot be ported by mechanical replacement.

| Current control / behavior | Rust candidate | Acceptance |
| --- | --- | --- |
| TreeView | egui CollapsingHeader / stable node IDs | Cached branches, selection, expand/collapse and accessibility |
| PropertyGrid | Typed egui Grid; read-only pilot, explicit editors later | Exact values, units, null and read-only semantics |
| ListView / DataGridView | ScrollArea::show_rows or egui_extras TableBuilder | 100k addressable rows, viewport rendering, selection identity |
| Chart / custom plots | egui_plot | Same series, axes, UTC windows, cursor and data gaps |
| TabControl / SplitContainer | Selectable tabs / resizable panels; egui_dock when needed | Layout, resize, selected-state persistence |
| TextBox / RichTextBox | TextEdit | Validation, multiline behavior, keyboard and focus |
| ComboBox / DateTimePicker / NumericUpDown | ComboBox / egui_extras datepicker / DragValue | Typed values, limits and effective dates |
| Status circles / progress / log tree | Painter shapes / ProgressBar / tree | Exact state colors, percentage and scoped log selection |
| Modal forms / file dialogs | Explicit view states / native dialog crate | Cancellation, confirmation and owner lifetime |
| Buttons / menus / toolbar | egui widgets | Exactly one accepted intent per activation |

Compatibility metadata was checked against the crates.io registry: egui_extras 0.32.3 requires egui ^0.32.3; egui_plot 0.33.0 and egui_dock 0.17.0 require egui ^0.32. The pilot itself only needs egui/eframe 0.32.3 and egui_kittest 0.32.3. egui_plot 0.33.0 is now installed and tested by the native Strategy workspace. egui_extras 0.32.3 is now installed and tested for virtual tables and date selection. egui_dock remains a candidate. Sources: [egui_extras manifest](https://docs.rs/crate/egui_extras/0.32.3/source/Cargo.toml), [egui_plot manifest](https://docs.rs/crate/egui_plot/0.33.0/source/Cargo.toml), [egui_dock manifest](https://docs.rs/crate/egui_dock/0.17.0/source/Cargo.toml).

## Migration waves

| Wave | Scope | Main dependency / gate | Planning estimate |
| --- | --- | --- | --- |
| 0 | Shared shell, typography, catalog and host bridge | Runtime, owner thread, packaging and ABI tests | Pilot implemented |
| 1 | Strategy Details, workflow list and summaries | Real live backend / reconnect qualification | 1–2 weeks after live gate |
| 2 | Market selection, option-chain lists, ITI and chart panels | Series snapshots, volatility units, selected contract identity | 2–4 weeks |
| 3 | Reference and configuration editors | Validation and acknowledged save/cancel outcomes | 2–3 weeks |
| 4 | Trade entry/order submission, trade plans, fund and portfolio | Broker confirmations and financial action contracts | 4–6 weeks; highest-risk wave |
| 5 | Scheduled Tasks, Local/AWS Backups and System Admin | Progress/log streams and safe command outcomes | 2–4 weeks |
| 6 | Main navigation and remaining diagnostics | Cross-view lifecycle and feature parity | 1–2 weeks |

Estimates are planning ranges, not measured implementation commitments. Each wave must inventory runtime-generated controls, map bindings/handlers, define snapshots and actions, keep both clients operational and pass deterministic and desktop tests before review. Do not retire WinForms until all migrated screens have parity evidence and the user approves retirement.

## Screen allocation

The table lists view classes discovered from source inheritance. Shared base controls are included as foundations. The linked control inventory supplies the source expressions for concrete controls.

| Wave | View class | Source | Base |
| --- | --- | --- | --- |
| 6 | ActorHealthForm | [TomasAI.IFM.UI.Net.Views/App/ActorHealthForm.cs](../../TomasAI.IFM.UI.Net.Views/App/ActorHealthForm.cs) | DarkTradingForm, IForm<ActorHealthForm> |
| 6 | DarkTabControl | [TomasAI.IFM.UI.Net.Views/App/DarkTabControl.cs](../../TomasAI.IFM.UI.Net.Views/App/DarkTabControl.cs) | TabControl |
| 6 | IFMAppView | [TomasAI.IFM.UI.Net.Views/App/IFMAppView.cs](../../TomasAI.IFM.UI.Net.Views/App/IFMAppView.cs) | DarkTradingForm, IForm<IFMAppView>, IFormControl, IIFMAppLiveViewAdapter |
| 6 | MarketDataOperationsHealthForm | [TomasAI.IFM.UI.Net.Views/App/MarketDataOperationsHealthForm.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketDataOperationsHealthForm.cs) | DarkTradingForm, IForm<MarketDataOperationsHealthForm> |
| 6 | MarketDataView | [TomasAI.IFM.UI.Net.Views/App/MarketDataView.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketDataView.cs) | DarkTradingView |
| 6 | MarketEconomicCalendarView | [TomasAI.IFM.UI.Net.Views/App/MarketEconomicCalendarView.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketEconomicCalendarView.cs) | DarkTradingView, IAsyncFormControl |
| 6 | MarketOutlookView | [TomasAI.IFM.UI.Net.Views/App/MarketOutlookView.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketOutlookView.cs) | DarkTradingView |
| 6 | OperationsView | [TomasAI.IFM.UI.Net.Views/App/OperationsView.cs](../../TomasAI.IFM.UI.Net.Views/App/OperationsView.cs) | DarkTradingView |
| 6 | StatusConsoleView | [TomasAI.IFM.UI.Net.Views/App/StatusConsoleView.cs](../../TomasAI.IFM.UI.Net.Views/App/StatusConsoleView.cs) | DarkTradingView |
| 2 | FuturesContractEditorControl | [TomasAI.IFM.UI.Net.Views/MarketData/FuturesContractEditorControl.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/FuturesContractEditorControl.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 2 | FuturesOptionContractEditorControl | [TomasAI.IFM.UI.Net.Views/MarketData/FuturesOptionContractEditorControl.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/FuturesOptionContractEditorControl.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 2 | InstrumentDefinitionSelectorForm | [TomasAI.IFM.UI.Net.Views/MarketData/InstrumentDefinitionSelectorForm.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/InstrumentDefinitionSelectorForm.cs) | DarkTradingForm |
| 2 | MarketDataForm | [TomasAI.IFM.UI.Net.Views/MarketData/MarketDataForm.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/MarketDataForm.cs) | DarkTradingForm, IForm<MarketDataForm>, IFormControl |
| 2 | YieldCurveRateEditForm | [TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditForm.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditForm.cs) | DarkTradingForm, IForm<YieldCurveRateEditForm>, IFormControl |
| 2 | YieldCurveRateEditorControl | [TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditorControl.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditorControl.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 4 | FundFinancialForm | [TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialForm.cs) | DarkTradingForm |
| 4 | FundFinancialPostingForm | [TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialPostingForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialPostingForm.cs) | DarkTradingForm |
| 4 | FundMandateEditorForm | [TomasAI.IFM.UI.Net.Views/Portfolio/FundMandateEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/FundMandateEditorForm.cs) | DarkTradingForm |
| 4 | PortfolioAdministrationForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs) | DarkTradingForm, IForm<PortfolioAdministrationForm> |
| 4 | DeleteDraftPortfolioDialog | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs) | DarkTradingForm |
| 4 | PortfolioEditorForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioEditorForm.cs) | DarkTradingForm |
| 4 | PortfolioFinancialAuthorityForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioFinancialAuthorityForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioFinancialAuthorityForm.cs) | DarkTradingForm |
| 4 | PortfolioLedgerControlForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerControlForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerControlForm.cs) | DarkTradingForm |
| 4 | PortfolioLedgerSetupForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerSetupForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerSetupForm.cs) | DarkTradingForm |
| 4 | PortfolioLedgerVersionForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerVersionForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerVersionForm.cs) | DarkTradingForm |
| 4 | PortfolioRiskPolicyForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioRiskPolicyForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioRiskPolicyForm.cs) | DarkTradingForm |
| 0 | CheckedDropdown | [TomasAI.IFM.UI.Net.Views/Presentation/CheckedDropdown.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/CheckedDropdown.cs) | DarkTradingView |
| 0 | DarkHeaderSeparator | [TomasAI.IFM.UI.Net.Views/Presentation/DarkHeaderSeparator.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/DarkHeaderSeparator.cs) | Control |
| 0 | DarkTradingForm | [TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingForm.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingForm.cs) | Form |
| 0 | DarkTradingView | [TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingView.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingView.cs) | UserControl |
| 3 | EconomicCalendarEditorView | [TomasAI.IFM.UI.Net.Views/Reference/EconomicCalendarEditorView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/EconomicCalendarEditorView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 3 | LookupTypeEditorView | [TomasAI.IFM.UI.Net.Views/Reference/LookupTypeEditorView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/LookupTypeEditorView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 3 | ParameterSetsReferenceView | [TomasAI.IFM.UI.Net.Views/Reference/ParameterSets/ParameterSetsReferenceView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/ParameterSets/ParameterSetsReferenceView.cs) | DarkTradingView, IControlCommand |
| 3 | ReferenceForm | [TomasAI.IFM.UI.Net.Views/Reference/ReferenceForm.cs](../../TomasAI.IFM.UI.Net.Views/Reference/ReferenceForm.cs) | DarkTradingForm, IForm<ReferenceForm>, IFormControl |
| 3 | StrategyCatalogDefinitionEditor | [TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogDefinitionEditor.cs](../../TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogDefinitionEditor.cs) | DarkTradingView |
| 3 | StrategyCatalogReferenceView | [TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogReferenceView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogReferenceView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 3 | TradeStrategyFamilyEditorForm | [TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs) | DarkTradingForm |
| 3 | TradeStrategyFamilyEditorControl | [TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs) | DarkTradingView |
| 3 | TradeStrategyFamilyReferenceView | [TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyReferenceView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyReferenceView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 1 | RiskHistoryForm | [TomasAI.IFM.UI.Net.Views/Strategy/RiskHistoryForm.cs](../../TomasAI.IFM.UI.Net.Views/Strategy/RiskHistoryForm.cs) | DarkTradingForm |
| 1 | StrategyObservationForm | [TomasAI.IFM.UI.Net.Views/Strategy/StrategyObservationForm.cs](../../TomasAI.IFM.UI.Net.Views/Strategy/StrategyObservationForm.cs) | DarkTradingForm, IForm<StrategyObservationForm> |
| 1 | StrategyWorkflowDetailsAccordion | [TomasAI.IFM.UI.Net.Views/Strategy/StrategyWorkflowDetailsAccordion.cs](../../TomasAI.IFM.UI.Net.Views/Strategy/StrategyWorkflowDetailsAccordion.cs) | DarkTradingView |
| 5 | BackupDatabasesView | [TomasAI.IFM.UI.Net.Views/SystemAdmin/BackupDatabasesView.cs](../../TomasAI.IFM.UI.Net.Views/SystemAdmin/BackupDatabasesView.cs) | DarkTradingView, IAsyncFormControl |
| 5 | ScheduledTasksView | [TomasAI.IFM.UI.Net.Views/SystemAdmin/ScheduledTasksView.cs](../../TomasAI.IFM.UI.Net.Views/SystemAdmin/ScheduledTasksView.cs) | DarkTradingView, IAsyncFormControl |
| 5 | SystemAdminForm | [TomasAI.IFM.UI.Net.Views/SystemAdmin/SystemAdminForm.cs](../../TomasAI.IFM.UI.Net.Views/SystemAdmin/SystemAdminForm.cs) | DarkTradingForm, IForm<SystemAdminForm>, IFormControl |
| 5 | SystemErrorView | [TomasAI.IFM.UI.Net.Views/SystemInfo/SystemErrorView.cs](../../TomasAI.IFM.UI.Net.Views/SystemInfo/SystemErrorView.cs) | DarkTradingForm |
| 5 | SystemWaitView | [TomasAI.IFM.UI.Net.Views/SystemInfo/SystemWaitView.cs](../../TomasAI.IFM.UI.Net.Views/SystemInfo/SystemWaitView.cs) | DarkTradingForm |
| 4 | BrokerAccountQualificationDialog | [TomasAI.IFM.UI.Net.Views/Trade/BrokerAccountQualificationDialog.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerAccountQualificationDialog.cs) | DarkTradingForm |
| 4 | BrokerExecutionEvidenceControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerExecutionEvidenceControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerExecutionEvidenceControl.cs) | DarkTradingView |
| 4 | BrokerManualTradeOrderView | [TomasAI.IFM.UI.Net.Views/Trade/BrokerManualTradeOrderView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerManualTradeOrderView.cs) | DarkTradingView, ITradeOrderControl, ITradeExecutionSelectionControl, ITradeOrderPriceSelectionControl, ITradeOptionLegSelectionControl, ITradeQuantitySelectionControl, IFormControl |
| 4 | BrokerOrderFillsPreviewControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs) | DarkTradingView |
| 4 | BrokerTradePreviewControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs) | BrokerOrderFillsPreviewControl |
| 4 | OrderFillsPreviewControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs) | BrokerOrderFillsPreviewControl |
| 4 | EsTradeBlotterControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs) | DarkTradingView, ITradeOrderControl, IAsyncFormControl |
| 4 | BrokerTradeBlotterView | [TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs) | EsTradeBlotterControl |
| 4 | CreateFundOrderForm | [TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderForm.cs) | DarkTradingForm, IForm<CreateFundOrderForm>, IFormControl |
| 4 | CreateFundOrderTradeForm | [TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderTradeForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderTradeForm.cs) | DarkTradingForm, IForm<CreateFundOrderTradeForm>, IFormControl |
| 4 | DeleteFundOrderForm | [TomasAI.IFM.UI.Net.Views/Trade/DeleteFundOrderForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/DeleteFundOrderForm.cs) | DarkTradingForm |
| 4 | EstablishedTradeView | [TomasAI.IFM.UI.Net.Views/Trade/EstablishedTradeView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/EstablishedTradeView.cs) | DarkTradingView |
| 4 | IronCondorTradeOrderView | [TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeOrderView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeOrderView.cs) | DarkTradingView, IAsyncFormControl, ITradeOrderControl, ITradeExecutionSelectionControl, ITradeOrderPriceSelectionControl, ITradeOptionLegSelectionControl, ITradeQuantitySelectionControl |
| 4 | IronCondorTradeView | [TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeView.cs) | DarkTradingView, IAsyncFormControl |
| 4 | SetLocalSymbolForm | [TomasAI.IFM.UI.Net.Views/Trade/SetLocalSymbolForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/SetLocalSymbolForm.cs) | DarkTradingForm |
| 4 | TradeEndOfDayForm | [TomasAI.IFM.UI.Net.Views/Trade/TradeEndOfDayForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/TradeEndOfDayForm.cs) | DarkTradingForm, IFormControl |
| 4 | TradeOrderConfirmationForm | [TomasAI.IFM.UI.Net.Views/Trade/TradeOrderConfirmationForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/TradeOrderConfirmationForm.cs) | DarkTradingForm |
| 4 | TradeOrderEditorForm | [TomasAI.IFM.UI.Net.Views/Trade/TradeOrderEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/TradeOrderEditorForm.cs) | DarkTradingForm, IForm<TradeOrderEditorForm>, IFormControl |
| 4 | VolatilityContextHistoryControl | [TomasAI.IFM.UI.Net.Views/Trade/VolatilityContextHistoryControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/VolatilityContextHistoryControl.cs) | UserControl |

## Testing and performance gates

- egui_kittest: all supported states, keyboard/mouse input, selection, revisions, stale snapshots and reviewed images.
- FlaUI UIA3 / AccessKit: real host, runtime, controls, observed data, resize, close and local lifetime. Current header controls support Invoke; tests do not assume ExpandCollapse or Grid patterns.
- Existing WinForms: shared view-model / event consumer regressions and the linked StrategyDetailsFlaUiTests remain mandatory.
- 100k grid: first/last row, viewport-sized draw count, background sorting/filtering, exact values, stable selection and repeated navigation.
- Performance: report native CPU layout separately from GPU presentation and input-to-visible latency. A harness CPU timing is not an end-to-end benchmark. Record build profile, viewport, GPU, data count and process memory.
- Live: verify real history hydration, updates, disconnect/reconnect, stale state and no backend shutdown. No responders is a blocked qualification, not a passed empty-data test.

Current remaining qualification gates are recorded in the implementation plan. A complete roadmap does not imply the full UI has been migrated.

### Backend-first pilot evidence update (2026-10-13)

Persisted history through the ready Development API, real C# bridge and native Rust UI is now verified, including Daily/Weekly/Monthly switching, workflow selection, property inspection and client reconnect/reload. The backend-first test entry point passed and client shutdown left backend readiness healthy. The earlier missing API was a test setup omission. The owned backend interruption/restart gate has subsequently passed. Newly persisted business workflow delivery has now passed. Mixed-debugger breakpoints and controlled comparative input-to-visible timing remain separate gates. See implementation-plan section 16 for evidence and commands.

## Current implementation progress

Full migration started on 2026-10-13. The Strategy workflow list, ITI plot and Summary have been implemented alongside Details. A shared client composition DLL now serves both hosts. The first mutable native screen, Lookup Definitions, uses the existing Reference service and lookup view model with acknowledged ABI v2 actions. Development create/change/remove qualification passed and cleaned up its disposable lookup category. See implementation plan section 17 for evidence and remaining gates. Unimplemented screen allocations above remain outstanding; the roadmap itself is not implementation evidence.


### Current port and qualification ledger

Strategy workspace/Details, Lookup Definitions, Strategy Observation and Risk History have native ports with the recorded behavior and backend evidence. Futures/Option Contract manual editors and Yield Curve Rates are implemented; provider Draft mapping/confirmation and convention review are implemented. Native/backend preview and cancellation passed; isolated import-save parity remains open. Live contract loading, option paging and native selection/removal-cancel checks have passed. Economic Calendar CRUD/filtering and native cancel checks have passed, with the timestamp identity regression covered in isolated PostgreSQL. Yield backend and native selection/cancellation tests passed. The 100,000-row fixtures are rendering tests, not fabricated business history.

Reference restoration is explicitly authorized for Development. The full Databento catalog refresh timed out without publishing a partial catalog; bounded ES parent-symbol restoration and exact multiplier verification are documented in implementation-plan section 18. Draft reference imports must not be represented as reviewed, trading-ready contracts.

All later wave allocations remain outstanding until their controls, existing actions, validation, lifecycle behavior and parity tests are implemented. This ledger supersedes older pilot-only status text; it does not imply full migration completion.


### Infrastructure pause in live qualification

The authorized Databento ES restoration completed and contract loading/paging plus native selection checks passed. Calendar CRUD/filtering and isolated fractional-timestamp regression passed. Provider browser and convention review are implemented with offline and recorded native-browser evidence. On 2026-10-13 Docker failed because WSL became unresponsive; Windows service permissions prevent recovery from the agent process. See implementation-plan section 19 for exact diagnostics, recovery attempts and remaining verification. No full-migration completion is claimed.


### Recovery and provider mapping continuation

The 2026-10-13 WSL/Docker outage is resolved after the user rebooted Windows. PostgreSQL, ScyllaDB and NATS responded, the API rebuilt cleanly, and live contract/provider/convention tests passed. Provider mapping now has a bridge-owned preview and explicit Draft import confirmation through existing view models. Native/backend preview and cancellation passed without Development reference writes. Full-screen wave allocations and remaining acceptance gates are still open; see implementation-plan section 20.


### Verified recovered package and business-data qualification gap

Release packaging, its 132-file SHA256 manifest and two FlaUI tests using the actual packaged executable passed after Docker recovery. Corrected native Summary and provider mapping tests passed (2/2); Rust widget tests passed (26/26), and isolated PostgreSQL VX checkpoint persistence/replay passed. See implementation-plan section 20 for exact evidence and first-run failures.

Live new-workflow delivery is verified. The captured Development workflow has known Regime Discovery input/schema errors (missing VIX/VX and intraday EMA/RSI metrics, and ATR14 schema unsupported). The user explicitly accepted that shared Development defect on 2026-10-13: it is not a UI migration blocker. Both UIs must display the same real result; a port must not fabricate successful pipeline output. Later-wave controls/actions and the remaining acceptance gates are still outstanding.

### Administrative ports and paired desktop checks (2026-10-13)

Scheduled Tasks and Market Data Operations Health are native ports through the existing C# view models. Scheduled Tasks includes Logs/Setup, cached task/year/month/day/run navigation, exact run output with continuation, Quartz daily/time/day helper, schedule editing/preview/enable/disable and confirmation of on-demand execution/removal/uncertain-run resolution. Operations Health includes the stage and dataset-worker panels, complete immutable metric values and automatic refresh/failure clearing.

Paired live FlaUI tests passed for both screens (2/2, `live-admin-routing-fixed.trx`). The current Development/development scheduler has no schedules: the live test verifies that genuine state, and fixture tests verify populated hierarchy/output/status behavior. No scheduled job was created or run during the live read-only checks. Native widget checks passed 7/7 and the initial shared/WinForms scheduler suite passed 11/11. Early failures exposed and fixed a double CentralPanel draw and the Rust health endpoint; those failures are retained in the evidence folder.

Actor Health has a native port and a shared `ActorHealthControlModel` used by WinForms and the Rust bridge for authority, operational reason and exact observed generation checks. Shared model/WinForms rendering tests passed 4/4 and native cached-tree/cancel tests passed 2/2. Live actor-tree qualification is tracked separately in the implementation plan; lifecycle commands are tested with isolated service fixtures, without pausing or restarting Development actors.

The existing 66-view inventory remains the completion checklist. These additions do not mark unported trading, portfolio, market selection, parameter/catalog, backup, shell and remaining dialogs as complete. The user has authorized implementation of all remaining views and paired tests.

For ongoing synchronization, business rules and command/query/service contracts remain shared C#. Every changed supported screen requires a matching native renderer and tests for both hosts, including enabled state, validation, cancellation, stable selection and lifecycle behavior. The known backend defects must remain visibly consistent across hosts.


### Parameter and catalog ports (2026-10-13)

Parameter Sets and Strategy Catalog now have native working-copy editors through the existing C# domain/service contracts. Paired live FlaUI rendering and native catalog editing/discard checks passed. Shared/WinForms/bridge tests cover lossless parameter editing, exact saved-version verification, immutable catalog identities, failed-save retry, lifecycle hashes and cancellation. See implementation record section 22 for the passing and failed-run evidence and the action-specific gates still open. These ports do not imply completion of the remaining trading, financial, backup or application-shell views.


## Required control equivalence (2026-10-13)

Every migrated form, view, and control must use the closest native egui equivalent to its actual WinForms control. Matching data alone is insufficient. Inspect runtime-generated controls as well as designer files; preserve placement, physical dimensions, font, colors, selection semantics, enabled states, and command behavior. Do not declare parity from a generic property list or a screenshot with only the same labels.

| WinForms control | Native Rust equivalent | Required behavior |
| --- | --- | --- |
| ListView, Details, FullRowSelect | egui_extras TableBuilder report list | One selected row across all cells and unused row width; original headers and column widths; viewport rows only; horizontal overflow scrolls inside the list |
| DataGridView | egui_extras TableBuilder | Match the source cell/row selection mode, formatting, column sizing, editing and validation; virtualize large datasets |
| DateTimePicker | egui_extras DatePickerButton | Calendar dropdown; selected date and format; same dependent-date behavior and date-range queries |
| ComboBox, DropDownList | egui ComboBox | Selection from the source choices; no arbitrary text entry |
| NumericUpDown | egui DragValue | Match precision, increment and min/max; retain exact managed decimal values at the bridge |
| TextBox | egui TextEdit | Single/multiline, read-only state and validation match the source |
| Button | egui Button | Same caption, placement, enabled state, confirmation and acknowledged application action |
| CheckBox | egui Checkbox | Same checked/enabled state and change handler |
| TreeView | egui CollapsingHeader hierarchy | Same branches, selected object, expand/collapse behavior and cached data; no service calls during rendering |
| PropertyGrid | Read-only egui Grid with scroll area | Selected object's named properties and formatting; editing only where WinForms permits it |
| TabControl | Native tab selector and exclusive content region | Same tab order and one active page; hidden source tabs stay hidden |
| SplitContainer | Resizable egui panels/split regions | Same initial orientation, proportions, minimum sizes and splitter behavior |
| MenuStrip / StatusStrip | Top/bottom egui panels | Same commands, ordering and status publication |
| Graph/chart | Native plotted series | Source series, scales, time zone, legend, annotations and empty-state behavior |
| Modal form | Modal native egui window | Parent-centered bounds, focus, Save/Cancel and pending/error behavior |

The report-list implementation is shared in `TomasAI.IFM.UI.Rust/src/control_parity.rs`. Orders and Trades use it; existing specialized data-grid ports require their own source-specific selection rules. Trading dialogs and nested blotter views remain subject to per-control desktop verification before full trading parity can be claimed.


### Current trading-control evidence

- Orders and Trades use the shared virtual report list with one continuous selected row, WinForms headers/column widths, non-clickable headers, internal horizontal scrolling, and no data-row cap.
- Native tests cover clicking empty row space, wide-column containment, 100,000-row viewport rendering, and calendar selection (From updates To to month end and sends the range intent).
- Main editor control bounds were compared to the runtime WinForms capture: all twelve selectors, dates, lists and action buttons differ by less than two physical pixels. The outer editor is within one pixel after accounting for CenterParent placement when parent screen height changes from 1408 to 1458 pixels.
- Final desktop checks: `ActualTradeOrderGeometryTests` and `TradeOrderNativeControlParityTests` passed; the live bridge load and generation/deduplication checks passed separately. Rust native test suite passed.
- Windows was locked during final screenshot acquisition. `trade-control-parity.png` captures the lock screen and is **not** valid visual parity evidence. UIA bounds and calendar invocation remain valid interaction evidence. Reacquire an unlocked desktop screenshot before claiming visual equivalence.
- These results qualify the main editor controls; they do not establish complete visual or action parity for every nested trading view, qualification dialog, EOD view, or established-trade monitor. Continue their per-control comparisons using the mappings above. No live trade was submitted, cancelled or repriced during qualification.


### Trade Order editor adjustments ? 2026-10-14

The Rust editor now draws the horizontal divider below Fund and above the workspace tabs, suppresses the generic Completed labels, and allows report-list columns to resize by dragging their dividers. Preserve initial WinForms widths during a new window's sizing pass; retain resized widths during subsequent frames. Keep actionable error messages visible.

Both clients use these presentation rules: Create Order is always enabled; Delete Order is enabled when the visible order list has a row; Load Trade requires a selected trade whose state is not NewTrade; Add Trade requires a selected order; Remove Trade requires a nonempty trade list. Command execution still reports missing context or rejected business operations through the existing validation paths. Source filtering must be accounted for when deriving visible-list button states.

Regression coverage includes the shared view-model states, empty editor buttons, calendar date ranges, 100,000-row virtualization, divider dragging/width retention, and initial widths in a newly sized window.


### Trade Blotter control parity ? 2026-10-14

The native Trade Blotter now follows the runtime WinForms control layout rather than a generic editor. Iron Condor has Market Selection, Broker Trade and Order Fills; the other supported blotters retain their source staging/evidence tabs. Parent trade/date/action/live controls, identity fields, grid columns, numeric settings, selection colors, execution fields, financial rows, qualification controls and order/fill inspector use the source placement and dimensions. Preserve the existing shared dark theme and regular tab typography.

Financial preview calculations are centralized in `BrokerTradePreviewPresentation`, used by both WinForms and the managed Rust bridge. Quotes, balances, limits and eligibility come from the existing managed queries and models. Missing quotes or fees remain unavailable. Manual leg roles follow strategy and strike order through `BrokerTradeLegSelection`, independent of click order; automatic selection retains the shared delta and minimum-wing-width rules.

The bridge wires expiry/live changes, manual leg selection/removal, quantity and execution choices, staging, confirmations, submission, execution refresh, broker selection, bounded repricing, cancellation and qualification actions to the existing managed services. Established trades retain their submitted contracts and execution identities. Loading an established trade opens its monitor separately from the Blotter. Chain owners are released on subscription changes and disposal. The Blotter tab waits for selected-trade loading to finish before allowing composer initialization.

Working-order repricing and cancellation were explicitly authorized by the owner. `BrokerOrderMutationRules` limits these to selected Working/PartiallyFilled orders with valid bounds/ticks, no terminal execution and no pending acknowledgement. A rejected mutation clears its pending marker; an accepted request waits for its matching broker operation acknowledgement. These checks remain enforced in the managed bridge even when UI controls are disabled.

A live parity check exposed an existing broker-account PostgreSQL adapter still passing CQL-style anonymous parameters. `BrokerAccountReadStore` now uses typed PostgreSQL positional parameters, qualified tables and revision-ordered reads. Its regression test creates and removes a uniquely named isolated database; application account data is preserved.

Verification:

- Rust full suite: 74 passed; final Blotter suite after dialog/startup corrections: 11 passed.
- Shared financial preview, strike/role selection and mutation eligibility tests: 26 passed.
- Broker-account tests, including isolated PostgreSQL replay/latest-revision checks: 9 passed.
- WinForms source capture tests cover the original Blotter, real editor parent and qualification dialog.
- Final desktop checks: main editor/calendar parity 1 passed; Blotter native FlaUI plus bridge tests 5 passed. The native comparison covers header fields, Market Selection, 41 Broker Trade controls, Order Fills navigation and qualification dimensions/controls. Asserted geometry tolerance is 3 pixels (3.01 for fractional DPI rounding).
- Final managed builds and native Release build succeeded. The rebuilt Rust executable and managed bridge/view-model assemblies were deployed to the Development launch directory and the live API was verified ready.

Evidence is under `.artifacts/rust-ui/tests/`: `blotter-winforms-*`, `blotter-rust-{Market-Selection,Broker-Trade,Order-Fills}.png`, `blotter-*-control-parity.json`, `blotter-qualification-control-parity.json` and `blotter-live-*.json`. Final test records are `TomasAI.IFM.UI.Rust.SystemTests/TestResults/blotter-parent-final.trx` and `blotter-final.trx`.

Limits: Development's selected trade 1201 has an October 9 effective/requested date and no eligible expiries in the source WinForms query range on October 14. The native client preserves that source behavior; it does not invent chain data. Successful live option selection/submission/fill is therefore not established by these runs. Financial mutation tests use simulated actions; no live order was submitted, cancelled or repriced. These checks qualify the Blotter controls and shared behavior, not every established-trade monitor or every screen in the broader migration.


### Frozen emulator strategy selection ? 2026-10-14

A shared selector regression rejected all frozen preview legs because the frozen data correctly retains `IsStale=true`. Both WinForms and Rust now explicitly opt into frozen selection only when the client is in emulator/Development mode, Live Feed is off and the snapshot identifies itself as a frozen emulator preview. Live selection retains its original stale-data rejection. Quotes remain marked frozen/stale; finite qualified deltas, the delta ceiling and minimum wing width remain required. The original four-argument selector contract is preserved for existing callers.

Live backend checks now return a complete four-leg frozen Iron Condor for the current Development trade; the earlier empty-expiry observation above is no longer the current qualification result. WinForms FlaUI loads the frozen November 20 chain and confirms four selected rows with Live Feed off. Rust FlaUI confirms four selected legs with its checkbox unchecked. Bridge assertions verify frozen provenance, delta ceilings and wing widths. Seven selector regression cases and eleven native Blotter tests pass. No order was submitted or changed by these checks. The selected-leg strip also uses full chain-column widths rather than header text widths.


### Stable live Market Selection updates ? 2026-10-14

One-second quote/execution reads no longer dim Blotter controls or show a transient Pending caption. User edits made while a read is in progress retain their drafts and wait for its acknowledgement before being submitted with the current generation; mutation serialization and validation remain in force. Closing the editor cancels deferred input. Read polling outcomes have bounded retention so the gateway does not exhaust its 256-request capacity after a few minutes; mutation deduplication remains intact. Read timeouts preserve the displayed view and allow a subsequent refresh.

The managed bridge merges display cells by contract ID, overwrites non-null incoming values (including numeric zero), and retains rows and cells absent from a partial snapshot. Eligibility and submission use the unmerged raw snapshot. Missing rows remain visibly cached and stale. Explicit underlying/expiry/frozen-live changes reset the display cache. Selected contract identities remain fixed once complete, quote updates retain their row markers, and the chain scrolls to a new selection once instead of jumping during polling. Selected-leg values use the same display data and formatting as the chain.

Verification: twelve Rust Blotter tests, two display-cache tests and four gateway/live-bridge tests passed. A live FlaUI check enabled Live Feed, observed active background polling over 79 samples with zero disabled-control samples, confirmed all four selected legs and duplicate role markers in the chain and selected-leg grids, and passed. Evidence: `.artifacts/rust-ui/tests/blotter-live-refresh-observation.json`, `blotter-live-nonflashing.png`, and `TomasAI.IFM.UI.Rust.SystemTests/TestResults/live-nonflashing.trx`. No trade order was submitted or modified.


### Underlying Last and full-row price marker ? 2026-10-14

`Last` now reads the exact underlying contract's latest accepted futures trade from `IMarketDataApi.TryGetLastTickPrice`. `EvaluatedOptionChainReadModel` appends nullable `LastUnderlyingPrice` and `LastUnderlyingTradeAtUtc` at MessagePack keys 13/14. The pricing/window `UnderlyingPrice` remains separate and retains its established freshness and stored-input fallback rules. Both clients display the explicit last trade, calculate Daily Price Change from it, and locate the yellow strike row using it. No underlying trade produces N/A. A quote-only cache, a different contract, cancelled/cleared trades and nonpositive prices cannot populate Last.

The Rust marker now paints the full row yellow, including gaps, with black text. Six source tests, twelve Rust Blotter tests and two live API/FlaUI checks passed. A live observation confirmed pricing input 7849.50 versus actual last trade 7798.00; the displayed Last was 7798.00. The Development API and Rust client were redeployed. Evidence: `underlying-last-price-ui.trx`, `underlying-last-trade.trx`, and `.artifacts/rust-ui/tests/blotter-live-nonflashing.png`.
