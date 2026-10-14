# IFM Windows Rust UI Implementation Plan

**Version:** 0.2  
**Date:** 2026-10-13  
**Status:** Offline pilot and backend-first live history/reload integration verified; comparative timing/debugger gates remain  
**Specification:** [Windows Rust UI Host and Pilot Specification version 0.2](IFM-Windows-Rust-UI-Host-and-Pilot-Specification-v0.1.md)

## 1. Scope and completion

Implement a Windows x64 Rust egui/eframe host, a .NET 10 managed bridge, Strategy Details and a virtual grid containing up to 100,000 rows. Test widget behavior, visuals, native desktop integration and shared WinForms behavior. Finish with measured pilot results and a full UI migration plan.

The project folders are exactly `TomasAI.IFM.UI.Rust` and `TomasAI.IFM.UI.Rust.Bridge.Net`. The bridge folder, project file, assembly and namespace use the name `TomasAI.IFM.UI.Rust.Bridge.Net`. These names supersede the earlier proposed `TomasAI.IFM.UI.Bridge` folder.

Keep WinForms available and preserve existing C# behavior. This plan does not authorize the full migration itself, live order execution, database changes or automatic retries. Sections 14 and 15 record the historical fixture results and current completion evidence.

## 2. Target file structure

```text
TomasAI.IFM.UI.Rust/
  Cargo.toml
  Cargo.lock
  rust-toolchain.toml
  src/
    main.rs
    hosting/{mod.rs,runtime_host.rs}
    ffi/{mod.rs,api.rs}
    bridge/{mod.rs,client.rs,worker.rs,snapshot.rs}
    presentation/{mod.rs,shell.rs,state.rs}
    views/strategy_details/{mod.rs,tree.rs,inspector.rs}
    views/virtual_grid/{mod.rs,table.rs}
    catalog/{mod.rs,generated_ids.rs}
    diagnostics/mod.rs
  contracts/{abi-v1.md,snapshots-v1.md,actions-v1.md}
  catalog/{strings.en.json,string-ids.lock.json}
  tests/{strategy_details.rs,virtual_grid.rs,action_intents.rs}
  tests/snapshots/

TomasAI.IFM.UI.Rust.Bridge.Net/
  TomasAI.IFM.UI.Rust.Bridge.Net.csproj
  Hosting/{BridgeBootstrap.cs,BridgeSession.cs}
  Contracts/{BridgeApiV1.cs,BridgeStatus.cs,BridgeHandles.cs}
  Dispatching/ViewModelDispatcher.cs
  Adapters/StrategyDetails/StrategyDetailsAdapter.cs
  Adapters/VirtualGrid/VirtualGridAdapter.cs
  Snapshots/{SnapshotPublication.cs,SnapshotWriter.cs}
  Actions/{ViewActionRouter.cs,ActionResultStore.cs}
  Catalog/{StringCatalog.cs,GeneratedStringIds.cs}
  Diagnostics/BridgeDiagnostics.cs

TomasAI.IFM.UI.Rust.Bridge.Net.Tests/
  managed adapter, ABI and lifecycle tests
TomasAI.IFM.UI.Rust.SystemTests/
  Windows FlaUI compatibility and real-host tests
scripts/UI/Rust/
  Build-RustUi.ps1
  Test-RustUi.ps1
  Publish-RustUi.ps1
  Generate-UiCatalog.ps1
Documents/system/
  UI migration inventory, pilot evidence and full migration plan
```

Names below are proposed implementation files, to be adjusted when an existing equivalent can be reused. Add managed projects to `TomasAI.IFM.sln`; integrate Cargo through build tasks and scripts. Do not invent a C# project wrapper merely to make Cargo appear as managed compilation.

Store one canonical string catalog under the Rust project's catalog folder. Both languages consume generated output from it. The C# bridge is the .NET hosting boundary, not a replacement application/domain service layer.

## 3. Milestone 0 Dependency assessment and baseline

Inspect `TomasAI.IFM.UI.Net/Program.cs`, `Startup.cs`, view-model/service registrations, event consumer composition and NATS ownership. Record lifetimes, dispatcher requirements and shutdown order before moving startup code.

Inspect StrategyOperationsViewModel, StrategyWorkflowDetails and StrategyWorkflowDetailsAccordion, including current property filtering, asynchronous preparation, native tooltip mitigation and tests. Classify logic as shared application behavior, reusable presentation projection or WinForms-specific control code.

Deliver an inventory with component, dependencies, unchanged reuse, required adapter, required change, reason and verification. Enumerate forms and meaningful controls with source, bindings, commands, keyboard behavior, accessible semantics and replacement candidate. Initial discovery may be automated, but custom drawing and runtime-created controls require manual reconciliation.

Verify installed Rust/MSVC, Windows SDK and .NET 10 tools. Select compatible pinned egui, eframe, egui_extras, egui_kittest and Wgpu versions using official documentation. Evaluate egui_plot, egui_dock and rfd only as a screen requires them. Record licenses and required features. Commit lockfiles during implementation.

Capture WinForms baseline behavior and representative timings at a recorded default window size, DPI, dataset and machine configuration. Do not assume current tests prove the Rust app.

**Exit:** dependency report, initial inventory and baseline evidence; unsupported dependencies have an explicit adaptation decision.

## 4. Milestone 1 Shared composition and managed bridge

Reuse existing models, view models, services and messaging assemblies. Extract reusable DI registration/lifecycle operations from WinForms startup into an existing shared service assembly where suitable. WinForms form registrations and error dialogs remain in its host. Both clients initialize the same shared registrations with their respective presentation services.

Implement an explicit owner dispatcher before creating dependent view models. Preserve async continuations and any necessary STA semantics identified in Milestone 0. Do not simulate a WinForms message loop without a documented dependency requiring it.

Create the bridge as a .NET 10 class library, with runtime configuration generation and x64-compatible dependencies. Do not reference the WinForms executable to run Program.Main or create hidden forms. Preserve view-model source unchanged where adapters satisfy presentation dependencies; record any unavoidable shared change separately.

Implement a session registry and immutable publications. Proposed logical API:

| Entry | Contract |
| --- | --- |
| GetApiV1 | Caller supplies ABI version and structure capacity; receives metadata and unmanaged function table |
| CreateSession | Copies configuration input, returns opaque handle and structured failure |
| GetSessionStatus | Reports starting, ready, disconnected/degraded, failed or stopping |
| OpenView and CloseView | Own adapter scopes and subscriptions |
| CopySnapshot | Caller-owned buffer receives one complete publication or required capacity |
| SubmitAction | Copies request envelope, returns local acceptance/rejection |
| CopyResults and AcknowledgeResults | Retains outcomes until explicit acknowledgement |
| CopyStringCatalog | Copies versioned catalog |
| BeginShutdown and GetShutdownStatus | Stops local work without backend shutdown |
| DestroySession | Releases stopped session after in-flight calls complete |

Before coding FFI, specify exact unmanaged calling conventions, field widths/layout/offsets, status codes, timestamp units, GUID byte order and decimal encoding in abi-v1.md. Define maximum counts/bytes, bounded growth retries and per-call diagnostics. Validate layouts mechanically in both languages. Never expose a managed object pointer or .NET decimal layout.

Use session epoch and generation for stale publication rejection. No managed locks or live model traversal may run from the Rust drawing thread. Ordinary managed exceptions remain inside entry methods.

**Exit:** managed tests prove ownership, dispatcher behavior, lifecycle, errors and contracts; shared WinForms presentation tests pass.

## 5. Milestone 2 Rust runtime host and shell

Implement supported nethost/hostfxr discovery and .NET 10 initialization from the packaged runtimeconfig. Resolve bridge entry points through the runtime hosting API and negotiate the function table. Define explicit supported runtime/roll-forward settings. Do not treat the bridge DLL as a native export library.

Create eframe shell startup/error states, bridge worker, Rust-owned snapshot cache and bounded result queue. Establish one runtime per process. Startup, DI verification and messaging initialization stay outside rendering. Handle missing runtime, DLL, ABI and catalog failures distinctly.

Select Wgpu and verify the actual display adapter. Record GPU selection and fallback behavior. Keep optional Gemma activity outside UI responsibilities.

Add separate VS Code tasks for Rust build, launch, test and stop, plus native debugging and managed attachment to the same PID. Validate debug support without promising seamless mixed-language stepping. Preserve existing WinForms development scripts.

**Exit:** native executable loads .NET 10, reaches a managed breakpoint, receives catalog/state, remains responsive during startup and closes without stopping the backend.

## 6. Milestone 3 Catalog and Windows automation proof

Generate matching C# and Rust string identifiers from the canonical catalog. Preserve stable keys/IDs, validate duplicate IDs and version mismatch, and report missing entries. Separate dynamic data from fixed strings. Include field units/format contracts in snapshot schemas.

Enable AccessKit in eframe explicitly. Create a test shell with labelled button, text field, checkbox, collapsing node and virtual table region. Launch the packaged executable through FlaUI UIA3 and capture the actual UI Automation tree and supported patterns.

Verify focus, input, invocation and observed state changes. Determine stable role/name/ancestor locators; never assume egui IDs become AutomationId. Document missing ExpandCollapse, Value, Selection, Scroll or Grid patterns. Evaluate UIA2 only for a demonstrated compatibility issue.

Fix application-owned widget semantics using supported egui/AccessKit APIs. If a required external automation pattern remains unavailable, report the gap and use a documented input/state-assertion fallback where valid. Do not replace the real accessibility test with only a screenshot.

**Exit:** catalog generation is deterministic and the FlaUI compatibility report establishes what can actually be automated.

## 7. Milestone 4 Strategy Details pilot

Resolve the existing strategy operations view model through shared DI. Map selected workflow details to typed tree and property snapshots. Perform bounded/cycle-safe property discovery and preparation outside rendering and publish completed generations. Discard work for superseded workflow selections.

Render the tree above a single read-only inspector in a resizable north/south split. Keep stable node identity, expansion, scroll and selection for the same workflow. Preserve meaningful property order, exact values, null handling and current suppression of duplicated pipeline branches and unwanted Stage Summary. Avoid native tooltip dependencies.

Provide real read-only workflow selection plus deterministic fixture mode for repeatable tests. Separate fixture and live modes clearly; do not claim fixture data proves NATS integration.

Run egui_kittest against production view functions and an injected bridge abstraction. Cover every supported expandable branch, ITI Signal, Regime Discovery, revisions, rapid selection changes, loading, empty, failure and disconnected states. Verify expansion consumes cached state and submits no backend query.

Run FlaUI against the actual host and bridge. Test selecting a workflow, selecting leaves, inspector updates, repeated expand/collapse, hover, resizing and shutdown. Reuse existing WinForms cases as behavioral references rather than copying WinForms control-specific locators.

**Exit:** values and interactions match the intended existing behavior; repeated interaction has no freeze; warm interaction meets or explicitly revises the provisional 100 ms p95 target using measured evidence.

## 8. Milestone 5 Virtual grid pilot

Build representative datasets of 1,000, 10,000 and 100,000 rows through a deterministic managed adapter. Include stable entity IDs, timestamps, sequences, exact amounts, approximate risk values and dynamic text. Also map one real read-only row source selected from the inventory for semantic comparison.

Transfer/load asynchronously into Rust-owned state. Use viewport range rendering through the selected egui_extras API. All 100,000 rows remain addressable; the number drawn follows viewport height plus documented overscan. Instrument dataset count, visible range and drawn row count.

Move sorting/filtering outside layout. Preserve entity selection as ordering changes and values update. Handle first/last row, keyboard navigation, scrolling, resize and updates during interaction. Version chunks if needed so datasets are adopted coherently.

Test dataset correctness, viewport-bound rendering and selection using egui_kittest. Exercise scrolling and resize in the actual app with FlaUI and compare screen/state outcomes. Do not enumerate 100,000 UIA nodes or build 100,000 widgets for testing.

**Exit:** 100,000-row correctness and virtualization evidence, usable navigation and recorded load/memory/update/frame costs.

## 9. Full pilot UI testing matrix

Full testing here means every pilot interaction and supported state, plus host/bridge integration and WinForms regressions. It does not mean unimplemented screens are tested or every operating-system behavior is guaranteed.

| Area | Deterministic checks | Desktop checks |
| --- | --- | --- |
| Startup | ABI mismatch, missing catalog, initialization failure, bounded queues | Actual executable, .NET runtime, ready/error state |
| Details | All branches, selection, revisions, cached expansion, stale result rejection | ITI/Regime interaction, hover, resize, focus and inspector parity |
| Grid | 1k/10k/100k rows, sort/filter, IDs, updates, drawn range | Scroll, first/last row, keyboard, default size and resize |
| Actions | Captured intent, availability, pending/results and duplicate-pass protection | Safe fake-backed action and observed result |
| Connectivity | Controlled stale/disconnected/recovered snapshots | Controlled real C# reconnect/resync once lifecycle is mapped |
| Shutdown | Worker/adapter disposal, invalid handles, outstanding results | Close Rust; confirm backend remains healthy |
| Layout | Reviewed image baselines at fixed scale/data | Window/DPI behavior on the target workstation |
| Existing UI | Shared view-model/service regression suite | Existing StrategyDetailsFlaUiTests and relevant live tests |

Use egui_kittest with controlled snapshots, clock and input. Pin its version with egui. Set viewport and pixels-per-point explicitly. Enable wgpu/snapshot only for image test runs that need them. Review expected/new/diff images; automatic baseline updates are disabled in regression runs.

FlaUI runs in an interactive Windows session. Serialize physical-input tests, use bounded state waits and an outer process watchdog. Capture UIA trees, screenshots, logs and timing on failure. Stop only test-owned clients. Do not depend on unattended desktop focus or uncontrolled live data for deterministic assertions.

Managed tests cover decimal/GUID encodings, malformed payloads, handle lifetimes, required-capacity retries, publication races, ownership and cancellation. Rust tests cover safe decoding and action/result state. An egui harness fixture does not test hostfxr or the managed bridge; real-host tests are mandatory for those paths.

If FlaUI cannot operate a required custom widget, retain egui_kittest coverage, instrumented real-host input and an explicit remaining automation gap. Completion must identify the gap; an unsupported pattern cannot be recorded as a passed test.

## 10. Performance measurements and evidence

Run release builds with fixed workload, machine, DPI, viewport, selected GPU and data. Separate startup/first publication from warm interaction. Compare WinForms and Rust using equivalent data and operations. Repeat representative runs with Gemma active and record CPU/RAM/GPU contention.

Measure frame p50/p95/p99 and maximum, input-to-visible latency, long stalls, snapshot age, publication/copy duration, transferred bytes, queue depth, process memory, allocations and managed GC. Use instrumented timestamps and a controlled workload; harness animation/timing is not a substitute for packaged-app measurements.

Starting goals remain approximately 20–30 Hz display publication, up to 60 Hz interactive redraw and cached interaction within 100 ms p95. Record actual results and justified target revisions. Do not present these goals as achieved guarantees.

Archive reports under `.artifacts/rust-ui/` with commands, versions, fixture IDs, source revisions, UIA dumps, screenshots/diffs and timing data. Publish a concise reviewed evidence record in Documents/system. Redact secrets and avoid production trading data where unnecessary.

## 11. Build and test entry points

Build-RustUi.ps1, Test-RustUi.ps1 and Generate-UiCatalog.ps1 are available for the fixture pilot. Publish-RustUi.ps1 is implemented for version-matched release packaging. Their contracts are:

- Build-RustUi.ps1: generate/check catalog, build the quoted bridge project and Cargo host, stage matching artifacts.
- Test-RustUi.ps1: separate managed, Rust behavior, image, desktop and performance modes; report skipped prerequisites distinctly.
- Publish-RustUi.ps1: package matching host, bridge, managed dependencies, runtimeconfig, assets and catalog; run clean-launch smoke test.
- Generate-UiCatalog.ps1: validate canonical catalog and generate both language outputs; provide check-only mode for CI.

Representative direct commands after project creation:

```powershell
dotnet build 'TomasAI.IFM.UI.Rust.Bridge.Net/TomasAI.IFM.UI.Rust.Bridge.Net.csproj' -c Release
cargo test --manifest-path 'TomasAI.IFM.UI.Rust/Cargo.toml'
dotnet test 'TomasAI.IFM.UI.Rust.SystemTests/TomasAI.IFM.UI.Rust.SystemTests.csproj'
```

Define snapshot feature flags and desktop test filtering when the selected versions and test project structure are established. No broad dependency upgrade or strict-compilation policy change is needed.

## 12. Milestone 6 Regression and full migration planning

Run the combined pilot suites and existing affected WinForms tests. Verify packaging and repeat a defined update/interaction soak, recording its duration and rates in the evidence. Resolve regressions before marking pilot implementation complete.

Complete the control inventory across all WinForms screens. Produce the full migration plan with screen order, shared dependencies, selected crates, snapshot/action/edit contracts, accessibility gaps, test cases, estimates and WinForms coexistence rules. Use pilot measurements to decide which components are ready and which require redesign.

Do not retire WinForms as part of the pilot. Present the migration plan and evidence for review before expanding implementation to all screens.

## 13. Blockers and completion checklist

A blocker is a concrete prerequisite preventing a milestone: unavailable native build tools, incompatible managed dependencies, unresolved required owner-context behavior, a runtime hosting failure, or an automation gap that prevents the agreed acceptance evidence. Record cause, attempted resolution and affected milestones; continue independent work where possible.

- [x] Dependency/lifecycle report and baseline recorded.
- [x] Requested project folders created with reproducible toolchain/dependencies.
- [x] Shared composition and unchanged view-model reuse verified.
- [x] Versioned bridge, catalog and runtime bootstrap tested.
- [x] FlaUI/AccessKit compatibility proven with actual widgets.
- [x] Strategy Details behavioral, visual and desktop checks pass.
- [x] 100,000-row grid correctness and virtualization checks pass.
- [ ] Real-host timing, reconnect and shutdown evidence recorded.
- [x] Affected WinForms regression tests pass.
- [ ] Packaged launch and debugging prerequisites verified.
- [x] Full UI inventory and migration plan delivered with remaining gaps explicit.

The implementation is complete only when the checklist is satisfied or a concrete blocker is reported. Future full-screen migration remains a separate reviewed scope.



## 14. Strategy Details fixture pilot implementation record (2026-10-12)

The bridge folder, project, assembly, namespace, solution reference, scripts and runtime loader all use **TomasAI.IFM.UI.Rust.Bridge.Net**. The native Cargo executable resides in **TomasAI.IFM.UI.Rust**. No old bridge project is required.

### Implemented path

1. The Windows x64 executable creates an eframe/Wgpu window on the RTX 3060 Ti display adapter using DirectX 12.
2. A Rust worker initializes the installed .NET 10 runtime through hostfxr, negotiates the versioned Cdecl function table and creates an opaque managed session.
3. The managed owner thread verifies SimpleInjector registration and constructs the unchanged StrategyOperationsViewModel and StrategyOperationsService with offline query/event adapters.
4. Selecting one of two fixtures or refreshing its revision passes a workflow notification through the existing service/view-model path. A bounded, cycle-safe adapter prepares all display nodes on that managed thread.
5. Rust copies a complete immutable display snapshot on its worker. Tree expansion, collapse and selection operate entirely on Rust-owned cached data. A single read-only property inspector sits below the resizable tree.
6. Closing the native window joins the worker, releases the managed session and disposes its local services. No backend shutdown command is issued.

The catalog is a canonical versioned UTF-8 file with generated C# and Rust identifiers. Display values use invariant strings; static UI captions use catalog IDs. AccessKit exposes egui buttons and labels to FlaUI UIA3. Collapsing headers are exposed as buttons; desktop tests use clicks followed by assertions on child presence rather than assuming an ExpandCollapse pattern.

### Pilot control inventory

| WinForms responsibility | Rust pilot implementation | Verification |
| --- | --- | --- |
| Workflow selection | Two labelled fixture buttons | FlaUI invocation and revision change |
| Workflow / ITI / pipeline navigation | Cached egui collapsing headers and selectable nodes | egui_kittest and native child presence checks |
| PropertyGrid | Read-only striped egui grid, exact formatted values | Selected-node state and native property text |
| North/south split | Resizable top panel and lower inspector | Reviewed image baseline and native screenshot |
| Refresh / status | Fixture refresh button and revision/status labels | Managed publication and native UIA checks |
| Form lifecycle | eframe window plus hostfxr/session ownership | Native close and process exit assertion |

The bridge references view-model/service libraries rather than the WinForms executable. Existing WinForms view-model and service source files are unchanged. The fixture adapters deliberately replace network query/event boundaries, so this is not evidence of live NATS integration or full application DI extraction.

### Run and evidence

Use the VS Code task **IFM Rust UI: Run Strategy Details**, or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/UI/Rust/Build-RustUi.ps1 -Run
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/UI/Rust/Test-RustUi.ps1 -Desktop -Visual
```

The build stages the executable and matching managed dependencies under `.artifacts/rust-ui/app`. Tests write TRX, UIA tree and native screenshots under `.artifacts/rust-ui/tests`. Rust visual baselines are tracked under the project's `tests/snapshots` directory. Baseline updates are disabled during ordinary regression runs.

Pinned versions: Rust 1.97.1, eframe/egui/egui_kittest 0.32.3, Wgpu 25.0.2 and FlaUI UIA3 5.0.0. The managed target is .NET 10. Builds and tests were run on the interactive Windows workstation; desktop automation requires an unlocked session.

### Verified results

- Build-RustUi.ps1 completed: managed bridge build reported zero warnings and errors; the native executable and dependencies were staged successfully.
- Rust behavior and visual suite: 3 passed, including baseline image comparison without UPDATE_SNAPSHOTS.
- Managed/FlaUI suite: 3 passed, including ABI validation, existing view-model projection, all three root interactions, all five pipeline stage expand/collapse operations, visible inspector values, fixture selection/refresh and native process shutdown.
- Catalog check and cargo fmt --check passed. The native screenshot was reviewed for the north/south layout and selected Regime Discovery properties.
- FlaUI uses the supported Invoke pattern for header buttons. Physical mouse coordinate automation showed an offset in this desktop setup; it is not counted as passed native mouse coverage. Rust widget tests cover click input deterministically.

### Remaining scope

This was the remaining scope on October 12. Section 15 supersedes this status with the implemented grid, live composition, release packaging and full migration roadmap. Live/reconnect qualification, mixed-debugger breakpoint verification and comparative input-to-visible timing remain unqualified. General financial editing/action contracts belong to the future screen migration waves.
## 15. Current milestone evidence (2026-10-13)

| Milestone | Implemented and verified | Remaining acceptance gate |
| --- | --- | --- |
| 0: Dependency/lifecycle baseline | Existing service and view-model ownership mapped; native memory and CPU-layout measurements recorded | Controlled WinForms/Rust input-to-visible and Gemma comparison not measured |
| 1: Host, runtime and bridge | Windows x64 hostfxr/.NET 10 bootstrap, bounded queues, versioned ABI, static catalog, owner-thread disposal | None for fixture startup/shutdown |
| 2: Shared composition | Shared StrategyOperationsComposition and StrategyOperationsViewModelComposition factories; unchanged existing service/view-model source; fixture adapters and real NATS clients | Backend-first live history and client reload passed; new business-event delivery remains distinct |
| 3: Strategy Details | Cached Workflow, ITI Signal and pipeline tree; north/south read-only inspector; Microsoft Sans Serif 10 pt equivalent | Client reload verified; server-crash recovery/new business-event delivery not tested |
| 4: UI verification | egui_kittest behavior/image baseline plus actual packaged-host FlaUI AccessKit checks; linked WinForms desktop coverage | Native physical mouse coordinate coverage remains limited; no mixed-debugger breakpoint proof |
| 5: Virtual grid | 1,000/10,000/100,000 rows, viewport-only drawing, stable row IDs, asynchronous filter/sort, first/last and keyboard navigation | Grid is explicitly labelled synthetic fixture data; no production portfolio query is claimed |
| 6: Regression/package/roadmap | Release package with managed dependencies, native symbols and SHA-256 manifest; desktop navigation soak; existing WinForms regressions; control inventory and migration waves | Client reload verified; controlled comparative performance/debugger gates remain open |

### Test and measurement record

- Six Rust checks passed: cached details behavior, reviewed image baseline and virtual-grid correctness/performance instrumentation.
- Seven managed/native desktop checks passed: projection, ABI, three dataset sizes, packaged Rust host, and linked existing WinForms Strategy Details FlaUI regression.
- Thirty-four existing shared WinForms view-model/event-consumer regression tests passed. These are 47 distinct offline checks; repeated runs are not added to this count.
- Native desktop testing verified all root and five pipeline stage expansion/collapse operations, workflow refresh/selection, inspector values, 100,000-row first/last reachability, 120 grid navigation transitions and client shutdown.
- The packaged native host rendered 31 visible rows from 100,000. A recorded warm CPU update/layout sample measured approximately 1.3 ms p95. This excludes GPU presentation and input latency and is not an end-to-end responsiveness claim.
- Resident memory in repeated release measurements was approximately 232–246 MiB before loading the grid and 311–322 MiB afterward. Private committed memory is separately recorded; no controlled WinForms memory superiority claim is made.
- Evidence files: `.artifacts/rust-ui/final-rust-tests.log`, `composition-tests.log`, `final-qualified-package-tests.log`, `winforms-regression.log`, `tests/native-frame-metrics.json`, `tests/native-memory.json`, and reviewed native screenshots under `tests`.

### Initial live qualification attempt (superseded by section 16)

The live bridge successfully connects to local NATS, but its actual workflow history query returns **Monthly Strategy Workflow Reconciliation Unavailable: No responders**. The separate live qualification test fails on this response rather than accepting an empty fixture as success. The captured publication has `mode: Live`, empty workflow choices and roots, and the explicit unavailable status (`tests/live-qualification.json`). No database, broker or feed-start command was issued to manufacture live proof.

Read-only live wiring supports Daily/Weekly/Monthly selection, stable workflow selection tokens and reconnect/reload. End-to-end history, event updates and reconnect remain blocked until the matching API Server query/subscription backend is running. Therefore the whole acceptance checklist is not declared complete.

### Deliverables and commands

- [Full UI migration roadmap](IFM-Windows-Rust-UI-Full-Migration-Plan-v0.1.md): 66 discovered view classes assigned to waves, shared dependencies, action/edit requirements, testing and coexistence gates.
- [WinForms control inventory](IFM-WinForms-Control-Inventory.csv): 2,299 source occurrences across 95 files and 23 control types. Occurrences are not unique runtime controls; reconcile custom/dynamic widgets per screen.
- `Build-RustUi.ps1 -Run -Release`: run the fixture Strategy Details/grid host.
- `Build-RustUi.ps1 -Run -Release -Live`: run read-only live workflow mode; supply the desired value date through `IFM_RUST_VALUE_DATE`.
- `Test-RustUi.ps1 -Desktop -Visual`: run offline behavior/image/desktop checks.
- `Test-RustUi.ps1 -Live`: run the explicit backend qualification gate.
- `Publish-RustUi.ps1 -Destination <new-workspace-directory>`: produce a version-matched release package and hash manifest. Packaged desktop smoke tests are a separate verification step.

The delivered roadmap is a plan for migrating all screens, not a claim that those screens are already ported. WinForms remains available. General acknowledged financial actions and editable property grids require the reviewed contracts and financial tests in those later waves.

## 16. Backend-first live integration correction (2026-10-13)

The earlier “No responders” result came from running without the API Server. It was an unmet test prerequisite, not an implementation blocker. The current Development API was built and started; `/health/launch-ready` returned HTTP 200 before testing.

The real bridge loaded a persisted ES Daily workflow, its ITI signal and three display roots. The API logged successful Daily, Weekly and Monthly `GetHistoryPage` requests and workflow hydration. Selection and reconnect/reload republished the same workflow identity with a newer generation. Both live tests passed. Native FlaUI verified Daily/Weekly/Monthly switching, live history rendering, tree expansion, selected-node properties, workflow selection and reload completion. Testing found and fixed a missing UI Automation name on the workflow selector and a dropdown that stayed open after selection. Closing the Rust client left API readiness healthy. No synthetic workflow or trading order was submitted by the tests. The API ran its normal Development startup activities.

`Test-RustUiLiveIntegration.ps1` starts or reuses the Development API, waits for launch readiness, builds the Rust host and runs the live tests. `-NoBuild` reuses built artifacts. The API is deliberately left running for further inspection. The `-NoBuild` entry point was itself executed successfully: Rust widget checks and both live tests passed. `Test-RustUi.ps1 -Live` also requires API readiness; the managed tests assert it independently.

Evidence: `.artifacts/rust-ui/live-api-build.log`, `live-api-readiness.log`, `live-api-stdout.log`, `live-backend-native-test.log`, `tests/live-qualification.json`, `tests/live-reloaded.json`, `tests/live-native-uia.txt`, and reviewed `tests/live-strategy-details.png`.

This proves backend-to-bridge-to-native history and client lifecycle reload, not server-crash recovery or delivery of a newly generated business workflow event. The historical workflow's stored `PipelineFailed` outcome is rendered faithfully; its business failure is separate from UI integration success. Mixed-debugger breakpoint verification and controlled comparative input-to-visible measurements remain separate acceptance work.

## 17. Full migration implementation in progress (2026-10-13)

The full migration is authorized. The milestone table above records the earlier pilot state; the following evidence supersedes its older backend/recovery limitations.

- A real Development API interruption/restart test passed: the bridge displayed backend unavailability, then reloaded persisted workflow details after the owned API process was restarted and readiness returned. The existing view model now clears its previous error when initialization begins. Evidence: `.artifacts/rust-ui/backend-recovery-test.log` and `tests/backend-recovery.json`.
- The native Strategy workspace now includes an ITI price plot above Strategy Updates, Details and Summary. It uses the existing workflow rows, graph window, actor states, summaries and paging operation. Two native widget tests passed, including 100,000 workflow rows with viewport-only drawing. The existing WinForms Strategy Details desktop regression and native Strategy desktop tests passed.
- Service-only client registrations were extracted into `TomasAI.IFM.UI.Net.Composition`. WinForms keeps its form/navigation registrations; both hosts consume the shared service registrations. The WinForms host compiled with zero warnings/errors. A composition test verifies the existing Strategy, Reference, Backup and Scheduled Task services are available without constructing forms.
- ABI v2 adds a bounded UTF-8 action envelope while retaining ABI v1. Lookup actions carry request IDs, expected display generations and exact typed input. Duplicate requests share their acknowledgement; stale edits and conflicting request-ID reuse are rejected. Pending becomes Completed only after the unchanged lookup view model receives its correlated backend completion. Failures remain visible. Three gateway/ABI tests passed.
- The native Lookup editor supports load, selection, new/add, change and confirmed removal. Three widget tests passed, including 100,000-row virtualization. A real Development test created, changed and removed one uniquely named disposable lookup category; the final read verified no test lookup remained. Evidence: `.artifacts/rust-ui/live-editor-tests.log` and `tests/live-lookup-qualification.json`.

The full migration is still in progress. Strategy Observation and Risk History now have native query/inspection ports; the remaining market/reference editors, trade/financial screens, administration screens and main navigation still need their native ports and parity tests. Mixed-debugger breakpoint proof and comparative end-to-end input latency remain open. No claim of full migration completion is made.


## 18. Market editor migration and reference restoration (2026-10-13)

Implemented native Strategy Observation and Risk History with the existing authenticated query contracts, exact workflow/invocation selection and evidence presentation. The PostgreSQL risk-history query now supports fenced keyset paging; the isolated nine-row/tied-timestamp regression passed. Live observation/risk queries and native FlaUI navigation passed. Empty risk history is shown explicitly and is not represented as an executed financial action.

The Futures and Option Contract editors reuse the existing C# view models for loading, paging and correlated command completion. Native controls support typed manual drafts, preserved provider metadata, immutable saved option identity, selection, confirmed removal/cancellation and viewport-only tables. Provider definition selection and full convention review remain to be ported. Manual entry is disabled with an explicit explanation when required lookup categories are absent. Completion timeouts are reported as unknown outcomes requiring reload, rather than inviting a blind retry.

The Yield Curve Rates port includes all twelve tenors, current/annual period selection, duplicate-date validation, add/change, confirmed removal and the existing external import operation. Two live backend tests passed for yield data/period selection and Strategy history. Two native FlaUI tests passed for yield selection/all twelve rate fields/removal cancellation and Observation/Risk navigation. Import controls were verified, but no external import or mutation of existing Development yield rows was performed. Screenshot review found and corrected a clipped date field; the corrected release must be restaged before final desktop acceptance.

The tree panel now fills its requested height instead of shrinking to its minimum. The original Details image baseline passed without replacement. All 19 Rust interaction/visual tests and eight managed bridge regressions passed. The live session now resolves the operational value date and on-the-run contract through existing backend queries, preserves explicit test overrides, and cancels startup before disposing its service scope.

### Provider restoration evidence and limits

Development contained 40 futures rows, no ES option rows, and missing Symbol/SecurityType/Exchange/Multiplier/OptionType lookup categories. The user authorized restoration from Databento. The existing global definition refresh reached its 30-minute deadline after storing 1.5 million raw records; it did not replace the active catalog. A bounded parent-symbol reader now supports exact ES.FUT/ES.OPT restoration without replacing the global catalog.

Preflight exposed an incorrect selection multiplier mapping: Databento contract_multiplier is deliverable count, not the financial price-point multiplier. For ES USD/index-point definitions, the mapper now checks exact contract quantity against venue tick value, display factor and tick size. Unsupported or conflicting economics remain unknown. Nine definition/mapping regressions passed. Raw definitions retain their original bytes and digest; the explicit Development maintenance tool checks those digests before repairing selection projections, preserves existing contracts and creates new records through normal event-sourced commands. Newly imported references remain Draft until exchange conventions are reviewed; provider facts alone do not establish exercise, settlement, premium or calendar conventions.

Evidence: `.artifacts/rust-ui/tests/managed-ui-regression.trx`, `definition-mapping.trx`, `live-yield-and-strategy.trx`, `native-yield-observation.trx`, and reviewed `live-native-yield-curve.png`. The reference restoration terminal result is recorded separately; neither the failed global refresh nor a preflight is completion evidence.

### Remaining acceptance work

Delivery of a newly generated workflow, full provider convention review, mixed native/managed debugger breakpoint proof, controlled input-to-visible/DPI measurements, rebuilt package qualification, and all remaining full-migration waves are still open. The live workflow delivery test currently fails when no new Daily workflow arrives within its observation window; historical hydration/recovery passing does not close that gate. Do not mark the entire UI migration complete from these partial ports.


## 19. Bounded Databento restoration and Economic Calendar port (2026-10-13)

The authorized Development restoration completed using `ES.FUT` / `ES.OPT`, snapshot `19b10b06-d41a-411a-a9a1-c001651208a3`. It retained 8,816 raw records, selected 2,903 current definitions, added seven missing lookup choices and 2,882 missing ES option drafts, with zero underlying-resolution failures. All existing contracts were preserved. Provider identities, digests and exact decimal strikes were checked after persistence. The prior timed-out full catalog refresh did not publish a partial catalog. See `tools/DevelopmentReferenceRestore/README.md` and `.artifacts/rust-ui/tests/databento-reference-restoration.json`.

These are Draft references, not trading qualification. Existing legacy underlying futures and missing option exercise/settlement/calendar/variable-tick conventions still require review. The corrected ES multiplier mapper uses provider unit quantity and price/tick valuation evidence rather than the deliverable-count field. Ten definition/refresh tests and five bounded provider-client tests passed. The API-backed Futures/ES option load and duplicate-free paging test passed after restoration.

Economic Calendar now has a native date/country filter, UTC event fields, bounded visible-row rendering, add/change/remove confirmation, import control and acknowledged completion adapter using the existing view model. Three new Rust widget tests passed; all 22 Rust widget/visual tests passed. Live add/change/remove/filter qualification passed, including cleanup of all disposable rows. Native Calendar and Futures/Options selection/removal-cancel tests passed. Yield passed after a bounded transient UIA retry. An isolated PostgreSQL regression passed for fractional timestamp lookup/update/delete and preservation of the neighboring event (1 test).

The live calendar test found a timestamp identity defect: actor routes retain whole seconds but the economic-calendar identifier previously compared fractional ticks. Identity now uses its existing whole-second route precision while the event/read model retains the full timestamp. Serialization, validation and command convention regression tests passed (10). Editor completion, idle state and final generation now publish atomically. The owned Development API was restarted with the verified identity fix; other worker processes were preserved.

Remaining migration waves and pilot performance/debug/live-delivery gates are still open. This progress record does not mark the full migration complete.


### Provider-definition browser progress

The native provider browser now queries the published stored catalog through the unchanged `InstrumentDefinitionSelectorViewModel`, with 100-row pages, snapshot-pinned continuation, exact provider facts and viewport-only rendering. It creates no subscriptions and rejects write actions. Two Rust widget tests passed, including 100,000 fixture rows; the live catalog paging test and six gateway/mapping tests passed. Native desktop verification is next. Provider-to-contract mapping and editable convention review remain outstanding; this browser alone is not full selector parity.


### Native provider and convention review checks

All four native market/reference tests passed (Provider browser, Calendar, Yield, Futures/Options); all 24 Rust widget/visual regressions passed. Provider convention review is now implemented as an explicitly confirmed editor operation using the existing `ReferenceConventionEditor` and qualification validators. Raw provider identity, strike, economics and underlying identity are not editable. Reviewed status requires complete qualification; legacy references require provider import first. A new widget confirmation/cancel test passed, and nine offline gateway/mapping tests passed. Unknown completion now invalidates mutation eligibility until reload.

The subsequent live regression encountered an infrastructure failure: Docker Desktop reports `stopping`, the Linux VM is absent, engine requests return HTTP 500, and NATS accepts TCP but sends no protocol handshake. HTTP launch readiness alone still reports 200 and is insufficient to establish current dependency health. Docker recovery is in progress; no live convention-review success is claimed from offline tests. Provider-to-editor mapping, the remaining migration waves, mixed debugging, comparative latency and new business-workflow delivery remain outstanding.


### Current operational blocker (2026-10-13, 08:07 UTC)

Docker recovery reached a confirmed WSL failure, not a disk-capacity limit: `DockerDesktop/Wsl/CommandTimedOut` for `wsl.exe -l -v --all`. Docker reports WSL unresponsive. The bounded Desktop restart failed with remaining desktop processes; those verified components were restarted without removing containers or volumes. Docker then reported unable to start. A bounded `wsl --shutdown` also timed out. `WslService` is StopPending, and restarting that service was denied by Windows service permissions. Administrator recovery of WSL or a Windows restart is required before further backend/live qualification.

Current disk availability: C approximately 231 GiB; D approximately 1,285 GiB. All disposable calendar rows were verified removed before the outage. The final Rust suite passed 25 widget/visual tests; nine offline mapping/gateway tests passed. The updated Release host/bridge is staged. The convention dialog's native live test is implemented but has not been run successfully after this infrastructure failure. Full migration is not complete; later wave allocations remain open.


## 20. Post-reboot recovery and provider mapping (2026-10-13)

Docker/WSL communication recovered after the user restarted Windows. PostgreSQL readiness, Scylla CQL and NATS health checks passed. The current Development API rebuilt with zero warnings/errors and started before live tests. The former WSL outage is resolved.

The native provider browser now previews and explicitly confirms Draft contract imports. Mapping reads the selected definition from the bridge-owned stored page, uses the existing domain importer, checks exact provider identity and option underlying, and preserves existing contract IDs and futures operational flags. The preview is a one-time bridge-owned token fenced by selection and generation; save rechecks the original record and uses unchanged contract editor view models/correlated terminal outcomes. Convention qualification is a separate operation. Existing Reviewed contracts cannot be replaced by this Draft import path.

Fourteen managed mapping/gateway/review tests passed, including a real backend preview of the legacy ES December future with no write. Three provider egui widget tests passed. Five live backend/native tests passed, covering stored snapshot paging, contract loads, convention review cancellation and provider mapping preview/cancellation. Evidence: `.artifacts/rust-ui/tests/provider-import-native.trx`, `provider-mapping.trx`, `live-native-provider-mapping.png`. Development contracts were not changed by these preview/cancel tests. The Release host is staged under `.artifacts/rust-ui/app`.

The live API also exposed checkpoint-only VX updates being routed to a non-null signal insert, rolling back legitimate source events. The targeted isolated PostgreSQL atomic-persistence regression passed: a checkpoint-only event commits/replays and does not insert or erase a paired signal. The rebuilt test-owned API is running the fix and VX command persistence errors ceased in the observed window. Real new-workflow delivery passed after enabling the workflow start policy only in the test-owned Development API with the registered offline broker emulator. A newly persisted workflow was queried by ID and appeared in the live bridge without history reload (`live-workflow-delivery.json`). Full migration waves, debugger/performance/package gates and full provider import-save qualification remain open.

The live regression runner now selects all `Live*` suites, including Calendar CRUD, backend restart and new-workflow delivery, rather than omitting these newer gates. Its hang timeout is three minutes to accommodate the explicitly bounded 90-second business workflow observation test. Individual UI waits remain bounded.

The complete live runner passed 34 of 35 managed/native tests on the first run. Its one failure was a stale/corrupted Unicode Summary header literal that assumed the selected Regime Discovery stage was always Failed. The native test now selects the actual Regime Discovery Summary header regardless of its current backend state. Screenshot review also prompted wider mapping inputs and a stronger keyboard-entry assertion: preview/cancel must show Change mode for the entered existing contract ID. The new-workflow test and owned-backend restart both passed in this complete run; the corrected native Summary and mapping tests subsequently passed (2/2), recorded in `native-summary-mapping-corrected.trx`. The first-run failure is retained as evidence rather than represented as a clean first run.


### Recovered package verification and remaining live qualification boundary

The Release package at `.artifacts/rust-ui/package-20261013-recovered` built with zero managed warnings/errors. All 132 SHA256 manifest entries matched their packaged files. FlaUI launched the packaged executable (not the build staging executable): the managed runtime/bridge, repeated tree expansion/contraction and first/last navigation through 100,000 fixture rows passed; live provider mapping entered the existing contract ID, displayed Change mode and cancelled without publishing. Both packaged-host tests passed (`tests/package-recovered/package-recovered.trx`). The package still requires an installed .NET 10 x64 runtime and Windows Microsoft Sans Serif; it is not a self-contained .NET distribution.

The Rust widget suite passed 26 tests. The isolated PostgreSQL VX checkpoint-only projection test passed and verifies source-event commit plus replay without deleting a paired signal. The broad live runner passed 34/35 on its first run; the Unicode/status-dependent native Summary test was corrected and its targeted rerun passed alongside provider mapping (2/2). New business workflow delivery and the owned API restart passed in the broad run. Evidence files are under `.artifacts/rust-ui/tests`; this section does not imply every later-wave screen has been implemented.

A current persisted workflow (`01a1354b-36e3-75dd-b024-68d189c659cd`, started 2026-10-13 12:44:44 UTC) reached Regime Discovery and failed with `RD.DATA.REQUIRED_MISSING;RD.DATA.SCHEMA_UNSUPPORTED`. The captured `live-qualification.json` identifies missing Daily VIX/VX observations and FiveMinutes/FifteenMinutes EMA/RSI metrics, plus ATR14 reported as schema unsupported. Receiving/rendering that legitimate failed workflow is a passed delivery test, not a successful trading-pipeline test. Required financial observations must be restored/generated from their authoritative sources and schema compatibility resolved before trading end-to-end qualification. No metrics or trading qualification have been fabricated, and required business checks have not been bypassed.

The Development API is left running with workflow delivery enabled only in its owned process environment and its existing offline broker emulator. This does not change global appsettings or enable real broker order execution. Remaining work includes full-screen migration waves, isolated provider import-save parity, mixed managed/native debugger proof and controlled WinForms-versus-Rust input/performance comparisons.

## 21. Administrative migration continuation and Development parity

User acceptance: the known Regime Discovery input/schema defect is shared Development behavior and does not block full UI migration. No fabricated workflow successes or reference facts are introduced to hide it.

Implemented: ScheduledTaskSession/scheduled_tasks.rs and OperationsHealthSession/operations_health.rs, using unchanged existing WinForms view models; ActorHealthSession/actor_health.rs with ActorHealthControlModel shared by the WinForms form. All async work occurs through managed gateway workers; tree expansion consumes immutable cached nodes. Actor operations retain Supervisor authority, reason, exact mailbox/incident identity and generation checks. Confirmation becomes unavailable if the observed publication changes before confirmation.

The package extracts only SupervisorControl, the operations-health endpoint and NATS server URI from the existing WinForms Development settings into `managed/ui-client.Development.json`; it does not copy full application settings. Both renderers use the same authorized Development requester and command service. The native endpoint must be `/api/market-data/operations-health`, not the server root.

Evidence (`.artifacts/rust-ui`):

- `tests/live-admin-routing-fixed.trx`: 2/2 live paired FlaUI tests, real WinForms + native Release executable + ready API.
- `tests/scheduler-parity.trx`: 11/11 initial scheduler/shared/WinForms regression tests.
- `admin-final-widget-tests.log`: 7/7 native scheduler/health tests, including Quartz helper and exact cached tree leaf selection.
- `tests/actor-health-shared.trx`: 4/4 shared actor command checks and existing WinForms rendering.
- `actor-health-widget-tests.log`: 2/2 cached actor tree and canceled confirmation.
- Live scheduler JSON and screenshots record an empty Development/development dashboard with three available projects, and lease-owned Quartz runtime Ready. No disposable schedule is silently seeded to change that evidence.
- First live actor-tree attempts found a test's collapsed-child assumption and first native load's blank From date. Corrected test and bridge use the actual default one-hour interval. Final live evidence is recorded when the corrected run completes.

Desktop parity tests use `DesktopViewTestHost`: a dedicated STA message loop for the real WinForms view, separate UIA3 automation, then the native AccessKit UIA3 surface. Automation reads optional Name properties safely and selects tabs before inspecting lazily materialized grids. Assertions use recorded backend facts. Tests never stop the backend or dispatch live Supervisor lifecycle operations.

`scripts/UI/Rust/Start-DevelopmentUis.ps1` starts both interactive Development frontends after API readiness, with owned frontend PID evidence. It does not start or stop the backend. Launch/build verification remains required before claiming two running desktop hosts.

Remaining implementation: parameter sets and strategy catalog editors; market selection/market data panels; order/trade/position/plan and broker dialogs; fund/portfolio/financial screens; Local/AWS backup logs/setup/actions; main application navigation/status/remaining dialogs. Their existing inventory allocations remain open until controls, supported actions and paired tests are implemented. This section records progress, not full migration completion.

Final Actor Health evidence: `tests/actor-health-default-date.trx` passed 5/5 (one paired live FlaUI navigation test, three shared bridge/command tests and one WinForms rendering test). Both actual desktop trees expanded, selected the same real mailbox, exposed its properties and collapsed without dispatching any Supervisor operation. The initial UIA collapsed-child assumption and native blank From default were corrected rather than bypassed.


## 22. Parameter Sets and Strategy Catalog continuation (2026-10-13)

The native Parameter Sets screen now uses the existing `ParameterSetEditorModel`, lossless `ParameterFieldEditorModel`, comparison model and `IParameterSetsApi`. It includes component/version navigation, new/edit/save/discard, Signals/Observations/Calculation/Intervals, validation, schema and exact payload inspection, comparison/audit, deployment assignment, startup preview/report/monitoring, and bounded historical import preview/confirmation. A historical preview is fenced by its exact legacy identity; import requires no outstanding working copy. WinForms has the same guard. Saving verifies the committed operation receipt and version before displaying a successful save. Unsupported schemas/fields remain read-only. Read-only reports and failed saves preserve native unsaved fields and metadata. Accessible text-field labels support UI Automation.

Evidence: `parameter-admin-final.trx` passed 16/16 (shared parameter editor/bridge regressions plus paired live Parameter Sets, Scheduled Tasks, Operations Health and Actor Health). `parameter-final-widget-tests.log` passed 5/5. These live tests inspect real Development state; they do not save parameters, run schedules or supervise actors. Parameter mutation tests use isolated service fixtures. Assignment/startup/import action-specific desktop qualification is still open.

The native Strategy Catalog screen covers catalog section/all-data filters, exact definitions, default/custom templates, new version/change/save/discard, metadata, relationships, topology/variant legs, capabilities, products, pipeline profiles, parameter bindings, settings/schema objects, and publish/retire confirmation. Persistence and validation use the existing Reference query/command APIs. Original keys and existing codes are immutable; failed save retries retain the operation identity, and lifecycle operations carry the observed content hash. Acknowledged saves/lifecycle changes are followed by exact-version/state verification. Rust JSON numbers retain arbitrary decimal precision. A central ABI/gateway view allowlist prevents routing drift.

Evidence: `catalog-regressions.trx` passed 8/8 (new bridge tests and existing WinForms catalog tests); `catalog-widget-fixed.log` passed 3/3; `live-catalog-abi-fixed.trx` passed 7/7 (paired live catalog and ABI regressions); `live-catalog-edit-discard.trx` passed 1/1 with real native name editing/discard and both rendered surfaces. The initial live test failed because the ABI dispatcher omitted the new view; that failure is retained in `live-catalog-parity.trx`, then corrected and retested. Initial native fixtures omitted required publication fields; those failed fixtures were corrected. No Development catalog version was saved, published or retired by live desktop tests. Mutations use isolated service fixtures. All catalog-kind/dependency action-specific desktop gates are still open.

The full native regression suite passed after decimal handling changed (`native-final-regressions.log`). Build outputs and evidence live under `.artifacts/rust-ui`. Paired screenshots are `tests/live-parameter-sets-{winforms,rust}.png` and `tests/live-strategy-catalog-{winforms,rust}.png`.

### Remaining scope

Full migration is not complete. Market Selection/option-chain panels, order/trade/position/plan and broker actions/dialogs, Fund/Portfolio financial and risk editors, Backup Logs/Setup/actions, full application shell/status/diagnostics and remaining dialogs still require ports and matched tests. Previously recorded provider import-save and broader lifecycle/action gates remain open. Shared Regime Discovery defects are accepted Development defects, not a reason to block migration. Both frontend projects remain supported; no WinForms screen is retired.


### Two running Development hosts

`Start-DevelopmentUis.ps1` rebuilt both Release frontends with zero warnings/errors and launched WinForms PID 47244 and Rust live PID 52200 against the existing ready Development API. The launcher records the current PIDs in `.artifacts/rust-ui/development-ui-processes.json`. Both main windows responded. `development-host-pair-targeted.trx` passed 1/1: FlaUI verified each real application's navigation, captured both running windows and confirmed backend readiness before/after. Both frontends and the API were left running. The initial whole-tree property enumeration failed with UI Automation COM HRESULT 0x80040201; targeted navigation queries passed, and the failed run remains in `development-host-pair.trx`.

The full native suite passed 42 tests in `native-final-regressions.log`. These successful checks qualify the implemented ports and the two-host startup only; they do not qualify or implement the outstanding screens listed above.


## 23. WinForms visual layout baseline (2026-10-13)

The user requires the existing WinForms presentation as the reference for all Rust screens: the same component positions, default dimensions, tab ordering, split proportions, typography and dark trading theme wherever feasible. Functional tests alone do not qualify visual parity. Compare both applications at the same client size and Windows DPI, including default, resized and maximized layouts. Preserve the WinForms application while bringing the Rust layouts into alignment.

`src/presentation.rs` centralizes the WinForms palette and logical measurements. Microsoft Sans Serif 10 pt is 13.333 logical pixels at 96 DPI; egui applies display DPI separately. It supplies black window/editor/grid surfaces, white text, gray borders, flat command/hover/pressed surfaces, square corners and shared control spacing. The installed Windows font is used rather than redistributed. The live native window defaults to maximized, with the same `IFM_UI_START_MAXIMIZED` override used by the WinForms launcher. The restored client size comes from the WinForms main designer.

Verified source measurements applied in this change: Details tree/property split 140/300; Parameter Sets navigation 430/1200 with a 280-pixel minimum; Strategy Catalog navigation 29%; Scheduled Tasks Logs tree 40%. The Details property display uses a full-width, resizable two-column table in the lower panel. The typography helper applies the palette to every existing native view and dialog. Individual status and chart colors are preserved.

Verification: native regression suite 44 tests passed before the final property-grid adjustment; targeted presentation tests include two initial viewport sizes and a GPU-rendered fixture screenshot at `.artifacts/rust-ui/tests/dark-trading-details.png`. This fixture is not live backend evidence or a paired WinForms screenshot comparison. The existing main navigation shell and remaining individually arranged forms still require layout alignment; this change does not qualify complete screen-by-screen visual parity.

Final verification for this presentation change: 46 native tests passed with `visual-tests` enabled, including cached tree interactions, viewport virtualization, two-window-size layout checks and GPU rendering. The old Details image baseline failed because its colors and split were intentionally changed; the replacement was visually inspected before being accepted. The updated Release executable is packaged in `.artifacts/rust-ui/app`. No backend business actions were performed.


## 24. Main application shell implementation and verification (2026-10-13)

The Rust main host now uses the WinForms application title (`Investment Fund Manager - v1.0.0.0 - DEV`), top navigation and bottom status bar. Two resizable side panels place Operations/strategy on the left, the workspace in the center, and Market Outlook on the right. Side panels initialize at 22% of the client width and reset those proportions when the viewport changes. Strategy Updates is the initial strategy tab; Details and Summary remain available with the existing cached tree/property-grid implementation. The graph keeps its reserved one-third height when the initial snapshot has no signals.

The right pane contains Market Outlook, Market Data, Economic Calendar and Status Console sections. A cancellable background bridge session reads market session, current ES contract, market outlook and calendar data through existing query contracts. It formats the supported outlook fields through `FuturesEodDataUIViewModel`, caches UTF-8 publications and never performs these queries on the renderer thread. The native worker only posts changed dashboard publications. Microsoft Sans Serif 10 pt and the dark trading palette remain shared across the shell and implemented editors.

Live verification used a ready Development API and both actual frontends, maximized at the same 3458 ? 1408 physical window bounds at 125% DPI. `main-shell-parity-final.trx` passed 2/2 paired FlaUI tests: exact title match, main navigation, top/bottom placement, side ratios, actual native left/right splitter drags, responsiveness and API readiness before/after. WinForms side ratios were 21.986%; Rust side ratios were 22.00%. Rust widths changed from 605.44 logical pixels to 652.81 left and 653.59 right during the drags. Screenshots and measurements are under `.artifacts/rust-ui/tests/main-shell-{winforms,rust,rust-resized}.png` and `main-shell-parity.json`.

The native regression suite passed 48 tests, including a regression for an empty initial graph retaining its height, cached tree interaction, strategy tabs and viewport-only grid rendering. Earlier failed FlaUI attempts are preserved: they exposed incorrect accessibility-heading assumptions, integer division in test ratios and an unmapped `pixels_per_point` test field. Physical DPI awareness and client-origin coordinates now prevent incorrect mouse positioning and obscured captures. Screenshot inspection also found and corrected the initial graph shrink and compressed Outlook columns.

Scope: this completes the requested main shell arrangement, not full pixel equivalence for every WinForms control or full-screen migration. The right Market Data chart/tab functionality and complete Status Console stream are still pending ports; the current dashboard shows supported market values and a dashboard status. Trade Orders/Portfolio toolbar entries currently identify the central workspace; their full editors/actions remain in the outstanding migration scope. Feed mutation is disabled in this shell. The existing reference/system editors remain available through their menus. No live trade, backup, reference import, or feed mutation was performed by these tests.

Final fresh-launch verification also passed 2/2 in `main-shell-parity-final.trx`. The size comparison uncovered and corrected a conflict between a forced initial inner size and Windows maximized placement: maximized launches now omit the forced inner size, while restored launches retain the WinForms reference size. The test waits for actual matching physical window bounds and restores its thread DPI context after desktop operations. The packaged executable was relaunched through the managed API + Rust UI launcher; the temporary standalone WinForms test instance was closed afterward.


### Main menu bar correction (2026-10-13)

The Rust main shell uses `egui::MenuBar` in a persistent 27 logical-pixel top panel, with no vertical frame padding that can clip the toolbar. Navigation order is Trade Orders, Market Data, Portfolio, Reference, System, feed control, Feed Health, Operations health, Actor Health. Feed runtime status is read asynchronously from the existing feed query API. Unknown status is displayed explicitly; running does not imply healthy quotes. The Feed Health link opens Operations health. Changing a top-level workspace clears the previous editor selection.

`MainMenuParityTests` verifies actual WinForms and Rust toolbar button types, order, bounds, feed-health links, responsiveness, and Rust Reference/System popup contents. It deliberately excludes the Windows title-bar System menu and does not invoke feed/trading commands.

This correction is visual/navigation parity only. Rust feed commands remain disabled. Trade Orders and Portfolio remain workspace headings rather than equivalent WinForms editors. These gaps must not be described as full functional UI migration.

Verification: Release bridge build succeeded with zero warnings/errors; Rust Release package rebuilt; paired live FlaUI menu test passed (1/1) and main-shell/splitter regression passed (1/1). Evidence: `.artifacts/rust-ui/tests/main-menu-parity.trx`, `main-shell-menu-regression.trx`, and DPI-correct `WinFormsProcessId-menu-bar.png` / `RustProcessId-menu-bar.png`. Both clients used the running Development backend, whose launch-ready endpoint remained successful throughout the tests.


### Main toolbar visual parity: feed colors and alignment

Rust now uses the exact WinForms RGB pairs for Stop Market Feeds (red/white), Start Market Feeds (lime green/black), and Feed Health (lime green/black, yellow/black, red/white, dark orange/black, steel blue/white, dim gray/white). The health caption follows the same Green/Yellow/Red/Off-hours terminology. It reads the configured backend live-pipeline health endpoint; stale or unavailable evidence is shown as Unknown rather than inferred as healthy from a running feed. The read-only feed command keeps its current disabled semantics while rendering the requested state palette. A tooltip explains that the command is not wired.

The top separator aligns with the Operations pane boundary, including after splitter movement. Native tests cover every palette state; the paired FlaUI test checks exact background and foreground RGB pixels in both running applications.

Verified: native presentation tests 3/3 passed; paired live FlaUI toolbar test with exact background/foreground pixel checks passed 1/1 (`.artifacts/rust-ui/tests/feed-color-parity.trx`). Both current screenshots show Stop Market Feeds red/white and Feed Health: Green lime-green/black. Release bridge build completed with zero warnings/errors. The rebuilt Rust Development window remains running for comparison.

### Main form heading removal

Removed the Operations title and its 25-pixel header panel from WinForms, and the corresponding title/separator from Rust. The strategy content now starts at the top of the left pane. Rust explicitly retains the selected pane width instead of shrinking to the remaining content. Native shell and paired FlaUI tests assert that the Operations heading is absent and the three-pane/splitter layout is preserved.

### Strategy observation pane parity (2026-10-13)

The Rust left pane now uses Strategy, Latency, Traffic, Errors and Saturation tabs. Strategy has a lime-green contract heading and a right-aligned Daily/Weekly/Monthly selector, initially Daily. The selector uses the existing shared view-model history actions. The chart displays ITI Signal Price and Market Time (ET) axes, the four WinForms series in a bottom legend, and direction-change triangles above their price points. The bridge publishes Eastern wall-clock plot coordinates using the existing daylight-saving-aware EasternTime conversion while retaining UTC event timestamps. The other four tabs retain the same placeholder messages as WinForms.

### Strategy Updates grid parity

The Rust grid uses the six WinForms headers in the same order, with no page selector, row count or load-more button. History pages load automatically when scrolling near the end of available rows; visible-row virtualization remains enabled. Daily timestamps use hh:mm:ss.fff tt and weekly/monthly use dd-MMM-yyyy hh:mm:ss.fff tt, all through the shared EasternTime conversion. Pipeline State contains 12-point circles in stage order: yellow processing, lime continue, red failure/stopped, ending inclusively at the first red circle. Hovering a circle shows its stage status.

### Market Outlook visual parity

Rust now mirrors the WinForms MarketOutlookView: four market classifications, snapshot status, three six-column data rows, five TDI fields and five trend fields. Values use flat rectangular borders, centered Microsoft Sans Serif 10-point text, and the existing semantic WinForms color palette. Both FuturesEodDataUIViewModel and FuturesTradeSignalUIViewModel are populated from the same composite MarketOutlookReadModel in the bridge, including EMA, RSI, TDI and ITI limits. The live dashboard continues to refresh outside the renderer; missing fields display the shared view-model unavailable/warming states. The separate Rust-only Market Outlook heading is removed.

### ES / ES-BB / VX chart tabs

Rust now mirrors the three MarketDataView tabs under Market Outlook. ES and VX read the existing shared six-hour bar-window query across value dates, retain 15-second bars, and plot Eastern wall-clock time on the horizontal axis with prices on the right. ES has yellow close, lime upper trigger and red lower trigger series; VX is fuchsia. ES-BB reads 40 daily observations and plots yellow close, blue 20 EMA, green upper band and red lower band. Axis bounds and single-observation extension follow WinForms. A separate background bridge session refreshes optional chart history every 15 seconds so fetching history does not block rendering or dashboard publication. Empty/failed histories show an explicit status instead of generated data.

### Economic Calendar and Status Console parity

The Rust sidebar reuses MarketEconomicCalendarViewModel for country loading, Today/Yesterday/Tomorrow/This Week/Next Week queries, ET event times, selected Actual/Forecast/Prior values and calendar refresh notifications. Calendar height follows the WinForms 120-to-245 logical-pixel range with at least 80 pixels reserved for Status Console. Status Console uses the shared live status-event subscription, keeps the same newest-first 500-entry buffer, and draws a virtual table with Status Time and Status columns and the dark ListView header palette. The bottom application status bar reads the first row of that same publication, so it shows the exact newest console message. Sidebar publications use copy kind 7 and update independently of chart/dashboard queries. Range, country and event selection actions run on a background owner; rendering does no network work.


### Shared shell console logging (2026-10-13)

Rust's sidebar now owns a background `IFMAppViewModel` through `RustShellViewModelSession` using the existing client service scope. Initialization attaches the same Outlook, futures-bar, trade-placement, application-lifecycle and feed-status observers as WinForms. Their actual handlers retain the existing console wording and sources, including ES Market Outlook refresh messages, history loading, session transitions, observer lifecycle and failure messages. This does not invoke feed toggle, trading, reference-import or application-shutdown operations merely to produce logs.

The native console publishes `IFMAppViewModel.StatusLogs` directly. Its shared ordered buffer is newest first and bounded to 500 entries; native rendering remains virtual. The latest message remains the bottom status-bar text. Do not register a second status-console listener in the same service scope: the underlying consumer has one callback, and another registration replaces it. All backend and other-client status messages continue to arrive through the shared subscription. Existing editor view-model writers keep their own operation-specific logging.

Verification: 26 shared shell view-model tests passed. A live FlaUI test read an actual `ES20261218 Market Outlook refreshed by Component @ 7859.25` message in Rust. Artifacts: `.artifacts/rust-ui/tests/shared-shell-console.trx`, `shared-shell-console-message.txt` and `shared-shell-console-rust.png`.


### ES-BB history freshness and chart parity (2026-10-13)

Rust's ES-BB history refresh now merges the current Market Outlook Bollinger point using the same WinForms rules: valid Daily points only, no future value dates, newest market timestamp per date, ascending dates, latest 40 dates. Intraday bands cannot replace a Daily chart point.

Historical warmup now acquires a stale trailing range even when more than 200 valid daily sessions already exist. One trailing unpublished session remains permitted; a larger stale tail starts acquisition at the first missing trading date rather than reloading the full year. Initial insufficient-depth acquisition retains the one-year behavior.

Development repair recovered 11 missing ES sessions from September 28 through October 12 via the existing Databento historical loader. An audited warmup/replay completed for attempt `1c386521-5b27-4acc-81c9-2d25bf878a08`. Read-only API verification returned 40 historical Daily Bollinger points ending October 12 and a valid current Daily point dated October 14. Regression evidence: 8 warmup tests and 2 chart projection tests passed. The stale-tail startup change is compiled; the already-running API uses its original binaries until rebuilt/restarted.

### Trade Order window geometry correction ? 2026-10-13

The native editor previously used a generic 1100 by 800 floating window. It now draws after the shell side panes are laid out, aligns to the central trade pane, and reserves the WinForms client-height target while fitting the available viewport. Empty order/trade lists retain the 54/46 allocation. Child dialog sizes are taken from the WinForms forms (including the runtime 620 by 405 commission confirmation layout) and centered over the editor. Three native editor tests pass, including pane alignment and 100,000-row virtualization; live paired FlaUI structural checks passed against the running API. These checks establish editor navigation and geometry, not completion of all trading-view visual or command parity.

### Actual menu-opened Trade Order geometry ? 2026-10-13

The previous structural test resized a standalone WinForms form and did not establish actual dialog placement. FlaUI measurements from the real main menu at 125% scaling showed WinForms screen bounds (764, 121, 1912, 1147), versus the previous Rust bounds (756, 62, 1926, 1313). The editor now preserves the measured 1100 screen-pixel WinForms client-height target, accounts for display scaling/window chrome, and centers vertically on the parent rather than stretching to pane height. The current Rust bounds are (763, 121, 1912, 1146); the comparison asserts every coordinate and dimension differs by no more than three pixels and passes. Evidence: `.artifacts/rust-ui/tests/actual-trade-editor-geometry-before.json`, `actual-trade-editor-geometry-after.json`, and `actual-trade-editor-rust-after.png`. This proves editor outer geometry at the measured display configuration; it does not establish complete child-control visual parity.
