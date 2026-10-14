# IFM Windows Rust UI Host and Pilot Specification

**Version:** 0.2  
**Date:** 2026-10-12  
**Status:** Specification for review before the implementation plan  
**Platform:** Windows x64  
**Source:** [WinForms to Rust egui/eframe Porting Guide](WinForms-to-Rust-egui-eframe-Porting-Guide-v0.1.md)

## 1. Purpose

Introduce a native Rust egui/eframe UI that hosts .NET 10 and reuses existing C# view models, models, validation, dependency injection and NATS communication. Establish feasibility through a Strategy Details pilot and a grid containing up to 100,000 rows before preparing the full UI migration plan.

Keep the existing WinForms application available. Both clients must preserve shared application behavior. Windows is the only required platform; Linux GUI support is outside this specification.

This specification defines requirements and proposed boundaries. Exact ABI signatures, pinned dependency versions and a complete control inventory are implementation planning deliverables. Proposed projects and interfaces below are not claims that those components already exist.

## 2. Agreed scope and sequence

| Stage | Required outcome |
| --- | --- |
| Host foundation | Rust executable loads .NET 10, initializes the C# bridge and obtains initial state without blocking rendering |
| Strategy Details pilot | Workflow tree and read-only property inspector work from cached snapshots with stable selection and expansion |
| Virtual grid pilot | Up to 100,000 rows are available, with drawing limited to the visible window of rows |
| Full migration planning | Pilot evidence informs the inventory, control replacements, remaining compatibility work and migration milestones |

No production trading behavior, database schema, actor contract or strategy calculation changes are required by this UI experiment. Any necessary backend change must be identified separately. Do not introduce automatic trading retries.

## 3. Repository starting points

| Existing component | Relevant role |
| --- | --- |
| `TomasAI.IFM.UI.Net` | WinForms executable, targeting `net10.0-windows10.0.17763.0`, with Simple Injector dependencies |
| `TomasAI.IFM.UI.Net.ViewModels` | Existing view-model assembly targeting `net10.0` |
| `TomasAI.IFM.UI.Net.ViewModels/Operations/StrategyOperationsViewModel.cs` | Strategy operations view-model entry point to inspect for pilot wiring |
| `TomasAI.IFM.UI.Net.ViewModels/Operations/StrategyWorkflowDetails.cs` | Detail sections, workflow identity and revision, and an immutable workflow view reference |
| `TomasAI.IFM.UI.Net.Views/Strategy/StrategyWorkflowDetailsAccordion.cs` | Current WinForms TreeView, PropertyGrid and horizontal splitter, with asynchronous detail preparation |
| `TomasAI.IFM.UI.Net.SystemTests/Layout/StrategyDetailsFlaUiTests.cs` | Existing pilot behavior reference |
| `TomasAI.IFM.UI.Net.SystemTests/Layout/LiveStrategyDetailsFlaUiTests.cs` | Existing live detail interaction reference |

The current Details control stacks the tree above the property inspector. It subscribes to expansion and selection events and prepares data asynchronously. Rust must preserve the intended behavior while replacing control-specific preparation and binding with an adapter publication.

The view-model target framework permits modern .NET hosting. It does not establish that all transitive services are free of WinForms, COM or dispatcher dependencies. Inventory these dependencies before finalizing the composition root.

## 4. Proposed project organization

```text
TomasAI.IFM.UI.Rust/
  Cargo.toml
  Cargo.lock
  src/
    main.rs
    hosting/       .NET discovery, startup and shutdown
    ffi/           ABI declarations and bounded unsafe operations
    bridge/        safe client, worker, snapshots and results
    presentation/  shell, navigation and local UI state
    views/
      strategy_details/
      virtual_grid/
    catalog/       generated string identifiers and local lookup
    diagnostics/
  tests/

TomasAI.IFM.UI.Rust.Bridge.Net/
  TomasAI.IFM.UI.Rust.Bridge.Net.csproj
  Hosting/         managed bootstrap and application session
  Contracts/       ABI structures, handles and status codes
  Dispatching/     owner context for view-model work
  Adapters/
    StrategyDetails/
    VirtualGrid/
  Snapshots/       publication and copying
  Actions/         typed action mapping and retained results
  Catalog/         catalog loading and export
  Diagnostics/

Shared UI catalog and contract definitions/
  canonical static string catalog
  generated C# and Rust identifiers
  versioned snapshot and action schemas
```

The Rust project is a Cargo executable. It must have repository build and VS Code task integration rather than being represented as an ordinary C# project. The bridge is a C# class library with the runtime configuration required for native hosting. Final placement of shared definitions must follow existing repository conventions and avoid independently maintained copies.

Extract reusable C# registration and lifecycle code into a shared composition root if required. Both hosts must use those registrations. Keep WinForms form registrations separate from reusable services and view-model registrations.

## 5. Authority and reuse

| Responsibility | Owner |
| --- | --- |
| Trading, risk, authorization and broker execution | Existing backend |
| Client behavior, validation and commands | Existing C# view models and services |
| NATS serialization, subjects and connection lifecycle | Existing C# messaging layer |
| Display projection and managed resource ownership | C# bridge and adapters |
| Layout, focus, scrolling, expansion, selection and edit drafts | Rust |
| Native window and GPU painting | eframe and selected renderer |

Reuse existing view-model source unchanged wherever possible. Adapters observe properties, collections and commands through existing contracts. They must not change financial meaning or mutate models to simplify rendering.

If a dependency requires WinForms dispatching, dialogs, clipboard or navigation, implement its presentation abstraction for the Rust host. Any unavoidable view-model changes must be listed with the dependency, reason, WinForms impact and regression evidence. Unchanged reuse is a goal that must be proven for each component.

## 6. Runtime hosting and initialization

Use supported nethost/hostfxr hosting to initialize the .NET 10 runtime and load the managed bridge. An ordinary managed DLL is not loaded through native symbol lookup for its C# methods.

The proposed bootstrap returns a versioned function table of unmanaged-callable entry points. Negotiate ABI version, structure sizes, capabilities and payload limits before creating the application session.

Startup sequence:

1. Establish Rust diagnostics and resolve installed application paths.
2. Discover and initialize the supported .NET 10 runtime.
3. Load the bridge and negotiate compatibility.
4. Create the managed session and its view-model dispatcher.
5. Initialize and verify the shared Simple Injector composition root.
6. Start existing C# messaging services through their established lifecycle.
7. Load the static string catalog and initial snapshots.
8. Show ready, disconnected, degraded or failed state explicitly.

Initialization runs outside the rendering thread. A successful DI verification is distinct from backend connectivity and synchronized display state. Use one .NET runtime lifetime per UI process. NativeAOT and runtime unload/reload are outside the pilot.

## 7. Threading and publication

Mutable view models have an explicit serialized owner context. NATS callbacks enqueue or dispatch work to that owner. Preserve required synchronization-context and STA behavior when the dependency assessment identifies it.

The owner produces immutable display publications. A Rust bridge worker copies changed publications into Rust-owned buffers and requests repaint. Frames use one adopted snapshot for each consistency unit throughout all layout passes.

No rendering or tree expansion operation may query the backend, wait on C# locks, traverse managed objects, run reflection or format an entire grid. Property discovery and projection happen before publication. Coalesce replaceable display changes; retain important action outcomes.

Publish dirty state at a bounded cadence. An update arriving during publication must remain scheduled. Initial market display policy is approximately 20–30 Hz, with prompt publication of connection and important result changes. Idle views use demand-driven repaint.

## 8. Bridge contract

The logical operations are bootstrap, create session, query session status, open/close view, inspect/copy snapshot, submit action, retrieve/acknowledge results, copy string catalog, begin shutdown, query shutdown and destroy session.

Every snapshot includes schema version, declared byte length, session epoch, view ID, monotonic generation, publication and source timestamps, freshness indicators and stable entity IDs. Preserve workflow revision when supplied by existing models.

Use fixed-width scalars, explicit layout, documented GUID byte order, UTF-8 text with lengths and opaque handles with stale-handle detection. Financial amounts require a defined exact decimal representation or scaled integer units. Approximate analytics may use floating point with documented meaning.

Rust owns output buffers. C# retains no caller pointer after returning. Asynchronous action inputs are copied into managed ownership before return. Insufficient capacity returns the required size without a partial successful snapshot; retries and maximum sizes are bounded.

Validate offsets, lengths, counts and overflow before decoding. Managed exceptions return per-call structured errors. Unsafe code is isolated in the Rust FFI module. Queue capacities, overflow behavior, cancellation and concurrent call rules must be specified before implementation.

A local action enqueue acknowledgement is not backend success. Retain request IDs and distinguish pending, rejected, completed, failed and unknown outcomes. Do not automatically resend a timed-out trading command. Rendering passes cannot generate duplicate side effects.

## 9. Static string catalog

Maintain one canonical catalog for fixed labels, headings, buttons, columns, tooltips, statuses and validation templates. Each entry has a stable ID/key, text and locale; the catalog has a version. IDs cannot be reassigned to another meaning.

Generate C# and Rust identifiers from the canonical source. Rust loads the catalog once per session or locale change and performs local lookup while rendering. Widget identity uses stable view/entity/field IDs, not text labels.

Dynamic workflow descriptions, broker errors and user values belong in snapshots or results. Parameterized messages require explicit placeholder rules. Units, precision, date/time display and monetary rounding are part of each field contract. Missing catalog entries display a diagnostic placeholder and are logged.

The pilot must demonstrate matching generated identifiers and detection of an incompatible catalog. Align WinForms with shared labels through its existing resource/presentation mechanisms during a controlled change, without changing command semantics.

## 10. Strategy Details pilot

Use the existing strategy operations and detail view models. Provide a workflow selector or read-only update list sufficient to select a real workflow. The pilot may expose Details independently of the full strategy graph; the eventual full screen preserves Strategy Updates, Details and Summary navigation.

The Details layout contains a resizable north/south split: object tree above, read-only property inspector below. Do not add a duplicate inspector panel or workflow identifier header above the tabs.

The tree exposes workflow, ITI signal and workflow pipeline status objects without duplicate pipeline branches. Inventory the exact object graph and current suppression rules. Preserve the removal of unwanted Stage Summary presentation and the avoidance of problematic native hover tooltips.

Prepare a bounded, cycle-safe tree and typed property rows asynchronously in the managed adapter. Stable node IDs derive from workflow identity and object path. Expanding cached nodes performs Rust-local work. While preparation is incomplete, show explicit loading state and remain interactive. Expansion must not initiate synchronous reflection or network work.

Preserve expansion, selected node and scroll state for revisions of the same workflow. Discard outdated preparation results when another workflow is selected. Show missing values and failures explicitly. The inspector must preserve property ordering, exact amounts, units, nulls, enums and meaningful collection summaries.

Acceptance includes ITI Signal and Regime Discovery selection/expansion, every supported expandable branch, repeated collapse/expand, live revisions, rapid workflow changes, deep/cyclic data protection and stale/disconnected state. Compare values and interaction behavior with WinForms; do not infer parity from a screenshot alone.

## 11. Virtual grid pilot

Provide a grid with 1,000, 10,000 and 100,000 rows. All 100,000 rows must be addressable; no arbitrary 200-row cap. Store the rows in Rust-owned snapshot state and draw only the viewport range plus a small documented overscan if needed.

Use representative trading row shapes: stable ID, timestamp, sequence, instrument/status, exact monetary values, approximate risk values and dynamic text. A deterministic managed fixture supplies scale testing; a selected real read-only view supplies semantic parity. Synthetic throughput is not a claim about backend query speed.

Project, transfer and load data asynchronously. Full dataset ownership is acceptable for the 100,000-row pilot, but virtual drawing does not remove transfer or allocation costs. Measure each cost separately. If snapshots require chunks, negotiate a generation and publish the completed dataset coherently rather than mixing versions.

Sorting and filtering run outside the draw loop and preserve selection by entity ID. Support scrolling, keyboard movement, resizing and first/last-row access. New rows and updates must not overwrite active drafts or shift selection to another entity. Do not preformat every cell on every frame.

Instrument visible and rendered row counts to prove the draw range remains tied to viewport size. Record initial load time, transferred bytes, peak/resident memory, frame times, scrolling latency and update cost. Compare equal dataset sizes and equivalent operations in WinForms where supported.

## 12. Control inventory and crate assessment

Before planning the full migration, enumerate every current form, user control and meaningful child control. Record stable ID, source file, control type, view model, bindings/actions, lifecycle, formatting, validation, focus/keyboard behavior, accessibility, screenshot and replacement.

| Current control category | Proposed candidate |
| --- | --- |
| Labels, buttons, text entry, checkboxes and selectors | Built-in egui widgets |
| Split containers and tabs | egui panels and explicit persistent layout state |
| Data grids | egui_extras table with visible-row drawing |
| Object trees | egui collapsing nodes with stable IDs |
| Property grid | Custom read-only inspector from typed adapter metadata |
| Charts | Assess egui_plot |
| Docking | Assess egui_dock only where existing workflows require it |
| Native file selection | Assess rfd |
| Images and resources | Packaged assets and compatible egui image loaders |

These are candidates, not verified version selections. Pin a mutually compatible Rust toolchain, egui/eframe ecosystem and renderer after checking primary documentation, licensing, maintenance, Windows behavior and required functionality. Keep the minimum dependency set needed for the pilots. Accessibility, input methods, multi-window support and specialized grid behavior need explicit parity decisions.

## 13. Performance and comparison requirements

Use Wgpu as the proposed rendering baseline and verify actual selection of the RTX 3060 Ti. The RTX 4090 remains available for Gemma. Record selected adapter and any fallback; do not silently select the wrong GPU.

Provisional pilot goals are interactive redraw up to 60 Hz and cached expand/select feedback within 100 ms at the 95th percentile under the representative workload. These are acceptance goals to validate and refine, not measurements or guaranteed capabilities. Record stalls separately; no five-second freeze is acceptable as normal interaction.

Measure Rust frame and input latency, managed publication duration, FFI copy duration, snapshot age, queue depth, allocations and GC. Separate startup, first publication and first expansion from warm interactions. Test the 100,000-row grid at the default window size and while resizing, sorting and receiving updates.

Run controlled WinForms/Rust comparisons on the same machine, data and workload. Include a run with Gemma active. Judge usability and behavioral correctness together with performance; GPU painting alone does not establish improvement.

## 14. Lifecycle and operational behavior

UI close stops local actions and subscriptions, drains or records pending outcomes, disposes managed scopes and services, joins bridge workers and invalidates handles after calls finish. Bound shutdown and log outstanding work on timeout.

Closing Rust must not stop the backend, cancel orders or close positions. Reopening queries authoritative state through existing services. Both UI clients may coexist for read-only comparison, but tests must prevent competing submissions of the same trading actions.

Preserve existing authorized credential/configuration locations. Keep credentials out of catalogs, fixtures and Rust source. Diagnostics log runtime/ABI versions, session/view/request IDs, source freshness and GPU choice without secrets.

## 15. Build and debugging

Build Windows x64 Rust and the managed bridge through reproducible repository tasks. Package matching DLLs, dependencies, runtime configuration, assets and catalog. Resolve paths from the application installation root and diagnose missing runtimes, assembly failures and version mismatches clearly.

Provide Rust native debugging and managed attachment to the same UI process. Validate the debugger combination before promising simultaneous stepping. An optional bounded managed startup gate supports breakpoints before DI initialization; disable it normally. Existing backend debugging remains separate.

Keep lockfiles and selected versions reproducible. Do not modify the default WinForms start/stop workflow until the new host is explicitly integrated as an additional selectable client.

## 16. Verification and completion criteria

| Area | Required evidence |
| --- | --- |
| Hosting | .NET 10 loads, bridge negotiates, DI initializes, managed breakpoint works and startup failures remain visible |
| ABI | Layout/encoding verification, invalid handle/payload tests, buffer growth and lifetime checks |
| Strategy Details | Value parity, responsive expand/collapse/selection, stable revisions and no stale result adoption |
| Grid | 100,000 addressable rows, viewport-bound rendering, correct sort/filter/selection and measured resource costs |
| Notifications | No lost dirty update during publication; bounded queues and explicit overflow behavior |
| Actions | Existing command mapping, availability checks, retained results and no duplicate rendering side effects |
| Recovery | Disconnect/resynchronization and shutdown preserve backend independence |
| WinForms | Shared changes retain existing presentation unit and relevant system-test behavior |
| Deployment | Clean launch from packaged artifacts with documented runtime prerequisites |

Use the layered FlaUI and egui_kittest approach in Section 18. Keep existing WinForms FlaUI regression checks. Rust desktop automation must prove the accessibility tree and supported actions for the selected dependency versions before claiming coverage.

Implementation completion for this scope means the host and both pilots pass their checks, results are recorded and a full migration plan is delivered. It does not mean all WinForms views have been ported.

## 17. Full migration plan requirements

After the pilots, produce a complete view/control inventory and group remaining migration work by screen. Identify unchanged reuse, adapter work, necessary shared refactoring, crate choices and unresolved parity gaps.

Each screen needs snapshot/action contracts, view-model lifetime, editing behavior, validation, keyboard/accessibility requirements, performance expectations and acceptance evidence. Include fixed-string adoption and a strategy for keeping both UI clients aligned during migration.

Record whether pilot results justify proceeding, which components need redesign and how WinForms remains available. The next implementation plan must name concrete files, dependencies, milestones, test commands and measurable exit criteria without treating proposed contracts as existing capabilities.


## 18. UI testing with FlaUI and egui_kittest

### 18.1 Research findings and chosen approach

Use both tools for complementary coverage. FlaUI tests the packaged Windows application from outside its process. egui_kittest tests the Rust presentation using controlled input and state inside a Rust harness. Neither replaces bridge, view-model or backend contract tests.

FlaUI wraps Windows UI Automation through UIA2 and UIA3 and can launch or attach to applications and interact with discovered automation elements. It is not restricted to applications written in C#. [FlaUI project documentation](https://github.com/FlaUI/FlaUI)

eframe documents platform accessibility through its `accesskit` feature. AccessKit provides a Windows UI Automation adapter, but its adapters do not support every element type or schema property. **Inference:** this establishes a credible FlaUI-to-egui path, not verified compatibility for IFM or every widget. Enable accessibility explicitly when configuring a reduced feature set. [eframe feature documentation](https://docs.rs/eframe/latest/eframe/) and [AccessKit platform adapters](https://github.com/AccessKit/accesskit#platform-adapters)

egui_kittest is part of the upstream egui repository and uses AccessKit-based semantic testing. Its harness supports UI closures and eframe application construction, input simulation and state assertions. Image regression testing is available with its `wgpu` and `snapshot` features. [Upstream egui_kittest README](https://github.com/emilk/egui/blob/main/crates/egui_kittest/README.md) and [Harness API](https://docs.rs/egui_kittest/latest/egui_kittest/struct.Harness.html)

Research date is 2026-10-12. The documentation inspected reports egui_kittest 0.36.2. This is research context, not a dependency pin. Select matching egui, eframe, egui_extras and egui_kittest versions together and recheck the selected APIs before writing tests.

| Test layer | Tool | Required evidence |
| --- | --- | --- |
| Managed projection and ABI | C# and Rust contract tests | Values, encodings, generations, ownership and action mapping |
| Rust widget behavior | egui_kittest | Expansion, selection, focus, sorting and rendering from controlled snapshots |
| Rust visual layout | egui_kittest image snapshots | Default size, resizing, clipping and representative error/loading states |
| Packaged Windows application | FlaUI with UIA3 as first candidate | Real startup, accessibility, input, hosted .NET bridge and shutdown |
| Existing Windows application | Existing WinForms FlaUI suite | Shared behavior remains intact |
| Responsiveness and scale | Instrumented application runs | Frame/input latency, stalls, copying and viewport-bound row counts |

### 18.2 FlaUI compatibility proof

The host foundation must include a small Windows accessibility proof before building the complete desktop test suite:

1. Launch the actual Rust executable from a C# test using FlaUI Application.Launch and attach UIA3Automation to its window.
2. Capture the UI Automation tree after readiness. Verify a labelled button, text field, checkbox, collapsing node and scrollable table region.
3. Record name, control type, bounds, enabled/focus state and available patterns for each element. Invoke supported actions and assert the resulting visible state.
4. Test keyboard focus and pointer interaction where required, checking the actual state transition rather than only whether an input call succeeded.
5. Record missing properties/patterns and evaluate UIA2 in a separate compatibility run if UIA3 exposes a concrete problem. Retain the existing WinForms suite's proven backend choice.

Do not assume egui IDs become Windows AutomationId values, or that an egui table exposes DataGrid/Grid/Selection patterns. Use verified role/name/ancestor locators, with stable accessible names and explicit entity context. Where the chosen version exposes a usable stable automation identifier, document the mapping. Avoid ambiguous labels and row-index identity.

Custom tree, inspector and grid widgets must provide accessible roles, names, state and actions appropriate to the selected egui/AccessKit APIs. Pure painting does not establish an accessible control. Verify these properties through the live UIA tree. Missing semantic exposure is an accessibility gap to fix or document, not a reason to claim a successful full test.

Run desktop tests in an interactive Windows session at a recorded DPI and window size. Serialize tests that use the physical pointer/keyboard. Use bounded condition waits instead of fixed long sleeps. A stuck automation call must have an outer test-process watchdog that captures diagnostics and terminates only the test-owned client. Leave the backend and unrelated application processes running.

### 18.3 egui_kittest harness design

Put presentation behavior tests under `TomasAI.IFM.UI.Rust/tests/`, with shared deterministic fixtures and an injected bridge client abstraction. Use the production view functions and state transitions. The normal widget test harness supplies already-decoded snapshots and captures action intents; it does not require live NATS, broker connectivity or the hosted .NET runtime.

Use Harness constructors suitable for the selected version, query nodes by verified semantic roles and labels, simulate input, advance the harness and assert selected IDs, expansion state, inspector values and recorded intents. Set the viewport size and pixels-per-point explicitly. Bound harness steps so a repaint loop fails with useful evidence.

For Strategy Details, cover every supported branch plus ITI Signal and Regime Discovery, repeated expansion/collapse, inspector selection, no duplicate pipeline branches, revision refresh, preservation of selection and rapid workflow replacement. Include loading, empty, error and disconnected snapshots. Verify that expansion emits no backend query and consumes prepared Rust state.

For the grid, inject 1,000, 10,000 and 100,000 representative rows. Assert viewport instrumentation, first/last-row reachability, scrolling, sorting/filtering and stable selection after updates. Off-screen rows need not exist as accessibility nodes until drawn: scroll them into view before querying them. Do not create 100,000 widgets merely to make test enumeration convenient.

Use a controlled clock and deterministic updates for notification/action tests. Verify that extra layout passes cannot emit duplicate actions. A fixture-backed harness proves presentation behavior; a separate integration test must exercise the real managed bridge and runtime.

### 18.4 Image regression and performance limits

Enable egui_kittest's `wgpu` and `snapshot` features for image comparisons. Its Harness documentation describes reference images, newly rendered images and difference images, with configurable comparison options. [Harness snapshot API](https://docs.rs/egui_kittest/latest/egui_kittest/struct.Harness.html#method.try_snapshot)

Keep reviewed image baselines for the default screen size, smaller/larger layouts, tree expanded/collapsed, selected inspector object, grid beginning/end and failure states. Control fixture timestamps, fonts, theme, scale and dynamic values. Document any masked regions and tolerances. Baseline updates require review; normal regression runs must not use automatic baseline-update mode.

Image comparisons detect visual changes, not financial correctness or absence of freezes. egui_kittest's default harness modifies some animation behavior for deterministic testing; real application timing must be measured separately. Report frame and interaction measurements from the packaged executable with the actual renderer, managed worker and publication cadence, both with and without Gemma activity.

### 18.5 End to end pilot cases and evidence

FlaUI must exercise startup/readiness, workflow selection, cached tree expand/collapse, inspector changes, resizing, grid scrolling and client shutdown against the actual Rust host. Use read-only or fake-backed deterministic scenarios. Add a controlled reconnect case once its existing C# lifecycle is mapped. Assert that UI exit leaves the separate backend healthy.

A desktop test passes only after observing the expected state. Checking that a process exists, clicking a coordinate or waiting for a screenshot is insufficient. If a widget lacks an automation pattern, a documented keyboard/pointer fallback may cover interaction, but the resulting state must still be asserted. Keep the accessibility limitation visible in the coverage report.

Archive test results, dependency versions, UIA tree dumps, screenshots/diffs, selected workflow/row IDs, session/snapshot generations and application timing logs. Correlate bridge failures and UI failures through existing request/session diagnostics. Ensure fixtures and artifacts contain no credentials.

The implementation plan must include the compatibility proof, Rust harness setup and real-host FlaUI tests as separate milestones. A successful egui_kittest run cannot substitute for hostfxr, native window, DPI, Windows accessibility or shutdown verification. A successful FlaUI run cannot establish 100,000-row virtualization without rendered-row instrumentation.




## 19. Implemented pilot and qualification record

The current implementation uses shared service and view-model composition factories while preserving the existing C# service/view-model behavior. Strategy Details and a separately labelled 100,000-row grid fixture run in the native host. Typography uses the installed Microsoft Sans Serif font at the logical equivalent of WinForms 10 pt. Static captions are generated from a single versioned UTF-8 string catalog.

The real read-only NATS bridge is implemented; live acceptance is blocked by a verified workflow query “No responders” response. Fixture success must not substitute for live/reconnect proof. See [current milestone evidence](IFM-Windows-Rust-UI-Implementation-Plan-v0.1.md#15-current-milestone-evidence-2026-10-13) and the [full migration roadmap](IFM-Windows-Rust-UI-Full-Migration-Plan-v0.1.md). Later screen migration, general financial action acknowledgments, mixed-debugger breakpoints and controlled input-to-visible comparisons remain separate gates.

### Backend-first verification update

The initial “No responders” attempt lacked a running API Server. Subsequent testing built and started the Development API, verified launch readiness, and proved persisted workflow history through the real bridge and native FlaUI window, including client reconnect/reload and shutdown. See implementation-plan section 16. Backend startup/readiness is now an explicit integration-test prerequisite; server-crash recovery and newly generated business-event delivery remain distinct tests.


## 20. Main application shell parity

The native main host must use the running WinForms main window as its layout reference, including its version/environment title, top menu, bottom status bar, Operations/strategy pane on the left, central workspace, and Market Outlook pane on the right. Both side panes start at approximately 22% of the client width with draggable vertical boundaries; changing the window size reapplies the reference proportions. Existing Strategy Updates, Details and Summary remain in the Operations pane, with the graph above them and the Details tree/property layout preserved. Use the shared 10 pt Microsoft Sans Serif typography and dark trading palette.

The right pane reserves sections for Market Outlook, Market Data, Economic Calendar and Status Console. Dashboard reads run asynchronously through the existing C# query APIs and shared display view model. Showing the shell does not start feeds or submit commands. Qualification requires actual paired FlaUI windows at the same physical size/DPI, measured pane widths, screenshots and successful splitter drags, with backend readiness checked before and after. This shell qualification does not qualify every toolbar action, chart, editor or dialog as a completed WinForms port.
