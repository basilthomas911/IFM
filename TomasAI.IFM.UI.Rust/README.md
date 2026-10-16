# IFM Rust Strategy Details pilot

Windows x64 egui/eframe host with .NET 10 hosted in-process. The managed project is `TomasAI.IFM.UI.Rust.Bridge.Net`.

## Source layout

`src/main.rs` and `src/lib.rs` are the executable and library entry points. Source modules are grouped by responsibility:

- `app/`: native application hosting and the managed runtime loader.
- `bridge/`: wire decoding and publication transfer.
- `editors/`: calendar, contract, lookup, and yield curve editors.
- `strategy/`: strategy workspace, inspection, catalog, and parameter sets.
- `trading/`: trading workspace, blotter, and order layout.
- `market/`: market charts, outlook, and provider browsing.
- `operations/`: actor health, operations health, and scheduled tasks.
- `ui/`: shared presentation, controls, and virtual grid.
- `navigation/`: application shell and sidebar.
- `localization/`: generated string identifiers.
- `diagnostics/`: latency tracing.

The library re-exports the existing module names at its root so callers retain their current import paths. Generate string identifiers with `scripts/UI/Rust/Generate-UiCatalog.ps1`.

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

The bridge exposes fixture selection/refresh and live Lookup, Futures/Option Contract, Yield Curve and Economic Calendar editors, plus provider catalog browsing, Draft mapping preview/confirmation and contract convention review. It publishes immutable uncompressed MessagePack snapshots through ABI v4/v5, with exact display values formatted as strings. Rust and the managed bridge must be rebuilt together; JSON ABI v1/v2 clients are rejected. See [the bridge contract](contracts/abi-v1.md) for encoding, ownership and test details. Rendering and tree expansion read Rust-owned cached nodes; managed display serialization and property display use generated accessors. Fixture-only DispatchProxy support remains separate from the live path.

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


## Viewmodel performance pass (2026-10-16)

See [the measured results](../TomasAI.IFM.UI.Rust.Benchmarks/Results/2026-10-16.json) for the before/after matrix, GC/memory probes, live timing, limitations and ten-area implementation inventory.

The default ABI v5 path polls revisions and copies/decodes only changed publications. ABI v4 clients remain supported. Changed snapshots retain exact MessagePack value representations and caller-owned buffers. Missing revisions always recover with a full snapshot. UI delivery is bounded; a full native queue retains the latest pending publication for retry. Source business events, broker acknowledgements and mutation deduplication are preserved.

`IFM_UI_INCREMENTAL_PATCHES=1` enables field patches, including embedded binary row documents and newest-first log splices. This is opt-in because the measured generic diff/reconstruction cost exceeds a full in-process copy for changing publications. Unchanged detection is enabled by default and does not require patch generation.

Display property accessors and primitive writers are generated at compile time by `TomasAI.IFM.UI.Rust.Generators`. Descriptor instances are created lazily for the types actually displayed. New document shapes must be visible at a `BridgeDocument`/`BridgeWire` call, a row helper, an anonymous construction in the bridge, or an explicit `DisplayDescriptors.Include<T>()` root. Unknown shapes fail explicitly; runtime reflection serialization is not a fallback. Editor input has explicit typed bindings in `BridgeInput`. This work does not certify the full dependency graph for NativeAOT.

### Repeat the measurements

```powershell
./scripts/UI/Rust/Measure-BridgePerformance.ps1 -Label my-after
```

For a preserved older bridge build, set `IFM_DISPLAY_SCHEMA_GENERATOR_ENABLED=false`, pass `-BridgeAssemblyRoot` and `-SkipBridgeBuild`, then clear that environment variable before building current code. `-Filter '*BridgeBenchmarks*'` limits the matrix to the comparable encoding/publication cases. Transfer benchmarks compare the additional changed/full/patch paths separately.

For optional live timing against an already running Development API:

```powershell
$env:DOTNET_ENVIRONMENT='Development'
$env:IFM_RUST_UI_MODE='live'
$env:IFM_UI_LATENCY_OUTPUT="$PWD/.artifacts/rust-ui/performance/my-live-latency.json"
$env:IFM_UI_PROFILE_TRADE_SECONDS='60'
& ./.artifacts/rust-ui/app/ifm-ui-rust.exe
```

The profiler opens Trade Orders, composes the currently selected trade and enables live chain display. It never submits, cancels, reprices or creates an order. It requires a selected editable option trade and suitable backend data. The window closes automatically after the requested interval (10?300 seconds). Clear the profiling environment variables afterward for normal use.

Timing follows accepted publications into the UI and records an egui WGPU render-pass callback. Samples are bounded at 10,000; file output occurs at shutdown. It measures CPU paint-command recording, excluding GPU completion and physical display presentation. Quote receipt age includes backend/cache age and refresh cadence; it is reported separately from native receipt-to-paint time. The instrumentation is off by default. Profile runs add timestamp scanning and callback overhead.
