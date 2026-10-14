# IFM Rust Strategy Details pilot

Windows x64 egui/eframe host with .NET 10 hosted in-process. The managed project is `TomasAI.IFM.UI.Rust.Bridge.Net`.

## Run

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/UI/Rust/Build-RustUi.ps1 -Run
```

Or run the VS Code task **IFM Rust UI: Run Strategy Details**. The staged executable is `.artifacts/rust-ui/app/ifm-ui-rust.exe` and its managed assemblies are alongside it in `managed`.

By default, the pilot uses two offline fixture workflows through the unchanged production `StrategyOperationsViewModel` and `StrategyOperationsService`. It does not start NATS, a broker, market feed, application server or databases. Select Workflow 1/2, expand the cached tree and select a node to see its read-only properties below. Refresh fixture publishes a new revision of the same workflow.

.NET 10 x64, Rust 1.97.1/MSVC and the Windows native build tools are required. Wgpu uses DirectX 12 and selects a compatible display adapter whose name contains 3060; startup fails rather than silently moving onto the LLM GPU if that adapter is unavailable. WinForms remains unchanged. Pilot text uses the installed Windows Microsoft Sans Serif font at 10pt (13.333 logical pixels at 96 DPI), matching DarkTradingTheme. Windows display scaling is applied by egui. The font file is read from the Windows Fonts directory and is not bundled.

## Live workflows and virtual grid

Build and launch read-only live mode with `Build-RustUi.ps1 -Run -Live`. It uses the existing C# NATS query/event clients and shared StrategyOperationsComposition. Configure IFM_UI_NATS_URL, IFM_RUST_SYMBOL, IFM_RUST_CONTRACT and IFM_RUST_VALUE_DATE before launch. Defaults are localhost:4222 and ES. When contract/value-date overrides are absent, the bridge queries the backend for the on-the-run contract and operational value date; pass explicit overrides when inspecting another scope.

Live mode exposes Daily/Weekly/Monthly selection, workflow selection and reconnect/reload. Backend query failures are visible. Fixture mode remains available without a server.

The Virtual Grid tab asynchronously loads 100,000 deterministic managed fixture rows. All rows remain addressable; only the visible range is rendered. Filtering and sequence ordering run in a background worker and retain selected row identity. First row / Last row support bounded navigation. This dataset is labelled as a pilot fixture, not application portfolio data.

## Release package

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/UI/Rust/Publish-RustUi.ps1 -Destination .artifacts/rust-ui/packages/my-release
```

The destination must be new and inside the repository. The package contains matching host/bridge assemblies, native symbols, managed PDBs and a SHA-256 manifest. It requires installed .NET 10 x64 and the Windows font. Use `-Release` with Build-RustUi.ps1 for an optimized native executable.

## Test

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/UI/Rust/Test-RustUi.ps1 -Desktop -Visual
```

FlaUI requires an interactive Windows desktop. Rust widget and visual tests use egui_kittest. Results are stored in `.artifacts/rust-ui/tests`. The desktop suite includes the existing WinForms Strategy Details test and a Rust grid navigation soak. Use Test-RustUi.ps1 -Live to qualify real backend workflow hydration separately; No responders fails this gate. Do not set UPDATE_SNAPSHOTS for normal regression runs. Image baseline changes require review.

## Debug

Use **IFM Rust UI: Native Strategy Details** for native debugging (Microsoft C/C++ extension required). Launch the view, then use **IFM Rust UI: Attach .NET bridge** and choose its `ifm-ui-rust` PID for managed breakpoints. These are separate configurations; simultaneous debugger support must be checked on the workstation.

## Boundaries

The bridge exposes fixture selection/refresh and live Lookup, Futures/Option Contract, Yield Curve and Economic Calendar editors, plus provider catalog browsing, Draft mapping preview/confirmation and contract convention review. Trading submission actions have not yet been ported. It publishes immutable UTF-8 JSON display snapshots with exact values formatted as strings. Rendering and tree expansion read Rust-owned cached nodes; managed reflection happens during preparation on the managed owner thread.

Live composition, the virtual grid, release packaging and the source-linked migration inventory are implemented. Backend-first live history, client reconnect/reload and an owned backend restart have been verified against the Development API. Delivery of a newly persisted business workflow without a history reload has passed. A successful complete trading pipeline remains unqualified: current Regime Discovery reports missing required observations and an unsupported ATR14 schema. Editor action contracts are implemented for the listed ports; trading actions and the remaining full-migration screens remain outstanding. See the implementation record and migration roadmap for measured evidence and limitations.

## Backend-first live integration

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/UI/Rust/Test-RustUiLiveIntegration.ps1
```

This starts or reuses the Development API, waits for HTTP launch readiness, builds the host and runs all live suites, including real history/reload, backend restart, Calendar CRUD, new business workflow delivery and native FlaUI tests. Actual backend queries must succeed; HTTP readiness alone is insufficient. Use `-NoBuild` to reuse artifacts. The API remains running afterward. `Test-RustUi.ps1 -Live` requires an already-running ready API. An empty or unavailable backend does not pass the live history test.

### Administrative screens and paired Development hosts

Native Scheduled Tasks (Logs/Setup), Market Data Operations Health and Actor Health use the existing C# services/view models. `LiveScheduledTaskParityTests`, `LiveOperationsHealthParityTests` and `LiveActorHealthParityTests` exercise the actual WinForms and native desktop surfaces against the ready Development API. Fixture tests cover mutations without operating real jobs or actors.

Run both frontends with `scripts/UI/Rust/Start-DevelopmentUis.ps1` once the Development API is ready. The launcher records its frontend process IDs and leaves the backend lifecycle alone. Full view migration is still in progress; see implementation plan section 21 for the open screen inventory and evidence.


### Parameter and strategy catalog editors

Parameter Sets and Strategy Catalog have native working-copy editors through shared C# APIs. Paired FlaUI tests verify real Development definitions and native catalog editing/discard without saving business data. Isolated service tests cover save/version/lifecycle behavior. See implementation record section 22 for evidence and remaining migration work.


### Managed API + Rust launch from VS Code

Select **IFM: API + Rust UI (Development)** in Run and Debug and press F5. It uses the same Server Manager as **IFM: API + UI (Development)**: build after stopping the existing managed session, start API, await its launch-readiness endpoint, start the live Rust desktop, and start SchedulerHost. Stopping debugging shuts down the managed session. The WinForms launcher remains the default.

**Terminal ? Run Task ? IFM Rust UI: Run Live (Development)** also starts this managed API/Rust/Scheduler session. Stop it with **IFM: Stop API + UI (Development)**. The separate **IFM Rust UI: Live (Development; wait for API)** debugger entry attaches a newly launched Rust desktop to an already-running backend; it does not start the backend.

The selector `IFM_DEVELOPMENT_UI=Rust` applies only to Development and replaces the `ui` process target with `.artifacts/rust-ui/app/ifm-ui-rust.exe`; preserving the `ui` key lets Server Manager track ownership and close the correct desktop. API and scheduler definitions retain their configured readiness and shutdown behavior.
