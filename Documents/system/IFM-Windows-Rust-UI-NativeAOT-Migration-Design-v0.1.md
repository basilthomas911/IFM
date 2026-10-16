# IFM Rust UI NativeAOT Migration Design

**Version:** 0.1  
**Date:** 2026-10-15  
**Status:** Proposed migration roadmap based on source inspection; NativeAOT publishing has not been qualified  
**Target:** Windows x64, .NET 10, existing Rust egui/eframe UI  
**Related:** [Rust UI specification](IFM-Windows-Rust-UI-Host-and-Pilot-Specification-v0.1.md), [Rust UI implementation plan](IFM-Windows-Rust-UI-Implementation-Plan-v0.1.md), [current bridge ABI](../../TomasAI.IFM.UI.Rust/contracts/abi-v1.md)

## 1. Recommended approach

Publish `TomasAI.IFM.UI.Rust.Bridge.Net` and its reachable C# dependencies as a NativeAOT shared library. Load that DLL directly from Rust, using the existing C ABI and uncompressed MessagePack contract. Keep the current hostfxr build available during qualification and preserve shared view-model behavior with WinForms.

NativeAOT compiles the reachable C# application graph together. We cannot convert the view-model DLL alone and continue treating it as an ordinary assembly loaded by the existing managed host. The proposed deliverable is one native bridge library containing the required view models, models, services and runtime support. NativeAOT shared libraries expose named unmanaged entry points and do not require an installed .NET runtime. [Microsoft native library documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries)

NativeAOT retains garbage collection for managed objects. It removes runtime JIT compilation; it does not give C# classes Rust ownership semantics. Smaller memory use and faster startup are evaluation goals, not measured outcomes for this UI. Rust rendering, GPU resources and native allocations remain separate contributors to process memory.

This roadmap changes the client build and hosting path. Backend deployment, database schemas, trading policies and durable event formats are outside its scope.

## 2. Current architecture and target architecture

```text
Current
Rust executable
  -> hostfxr and installed .NET 10
  -> managed BridgeBootstrap.GetApi
  -> ABI v3 and uncompressed MessagePack
  -> shared C# view models and UI services
  -> NATS client -> API server

Target
Rust executable
  -> Windows loads NativeAOT bridge DLL
  -> exported IFM_GetApi
  -> same publication and request semantics
  -> compiled shared C# view models and UI services
  -> compatible NATS client -> existing API server
```

Keep the native library loaded for the entire process lifetime. `Destroy` releases a session, subscriptions, workers and service scopes; it must not unload the NativeAOT runtime. Microsoft explicitly states that unloading NativeAOT libraries with `FreeLibrary` is unsupported. Rust's current `libloading::Library` ownership must therefore be changed for the native-library branch. [Microsoft native library lifecycle](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries)

Select JIT or NativeAOT explicitly when launching. If NativeAOT initialization fails, report the failure; do not silently switch hosting modes. This makes comparisons reproducible and avoids masking missing native dependencies.

## 3. What is already reusable

- `BridgeBootstrap` exposes a bounded Cdecl function table with opaque session handles and caller-owned byte buffers.
- ABI v3 transports uncompressed MessagePack across all nine publication channels and editor requests.
- Bridge publication records already declare explicit MessagePack keys. `LookupWireFormatter` explicitly adapts the existing shared lookup model.
- Rendering uses Rust-owned snapshots and stays outside managed service calls.
- Shared view models target `net10.0`; the bridge project does not directly reference the WinForms views executable.
- `IAppRoot` exposes typed services and value dates without requiring a Simple Injector container in its interface. This gives an alternative composition root a practical integration boundary.
- Existing Rust widget tests, managed gateway tests, native desktop tests and embedded-runtime tests provide regression coverage to extend.

The recent MessagePack verification established the JIT-hosted binary boundary: 82 Rust tests, 34 selected managed tests and one actual Rust/embedded .NET integration test passed. Those results do not establish NativeAOT compatibility.

## 4. Source inventory and required changes

The bridge's source project-reference graph contains **28 projects including the bridge**. This count excludes NuGet dependencies and does not mean all 28 contain AOT defects. Publish analysis must establish which code is reachable.

| Area | Observed source | Required work |
| --- | --- | --- |
| Native loader | `TomasAI.IFM.UI.Rust/src/hosting.rs` loads hostfxr, a runtime configuration file and an assembly delegate | Add direct native DLL loading, named export lookup, explicit mode selection and process-lifetime library ownership |
| Bootstrap | `UI.Rust.Bridge.Net/BridgeBootstrap.cs` uses `UnmanagedCallersOnly` without a named export | Export a stable `IFM_GetApi` symbol from the publishing assembly; preserve Cdecl, table size, handles and return codes |
| Bridge codec | `BridgeWire.cs` still includes `ContractlessStandardResolverAllowPrivate` | Use a complete generated/custom formatter set with no runtime-generation fallback; replace anonymous publication payloads with named records |
| Backend codec | `Framework.Serialization/MessagePackBinarySerializer.cs` uses a contractless resolver and LZ4 | Provide an AOT-compatible resolver for reachable backend contracts while preserving backend encoding and compression |
| NATS dispatch | `Framework.Messaging.Nats/NatsEventConsumer.cs` builds delegates using `MakeGenericMethod` and `Expression.Compile` | Register typed deserialization delegates for all client-consumed events at build time or through explicit generic registrations |
| Other NATS paths | `NatsEventProducer.cs` constructs generic methods dynamically | Determine reachability from this client; replace reachable runtime dispatch, rather than refactoring unused server paths automatically |
| Service composition | `RustClientServiceScope.cs`, `UiClientServiceRegistration.cs` and fixture `Session.Run` use Simple Injector construction and verification | Qualify the pinned version; if necessary supply explicit constructor factories and lifecycle ownership behind existing typed interfaces |
| Generic inspectors | `StrategyDetailsAdapter.cs` and `TradeComposerSession.Properties` use `TypeDescriptor` | Replace open-ended property discovery with generated descriptors or typed projectors for the supported screen types |
| Presentation mapping | `MarketOutlookPresentation.cs` uses runtime `GetProperties` | Generate mappings or use explicit typed accessors that preserve captions, values and semantic colors |
| Editor flattening | `ActorHealthSession`, `OperationsHealthSession`, `ProviderDefinitionSession`, `ReferenceReviewMapping` serialize arbitrary objects to JSON elements | Use typed projections or source-generated metadata for each concrete type |
| Remaining JSON | Parameter sets, nested trade/editor documents, logging, the string resource and HTTP health responses still use JSON | Inventory concrete serialization types; use generated JSON metadata, explicit converters or document APIs as appropriate |
| Configuration location | `RustClientServiceScope.cs` derives a settings path from `Assembly.Location` | Use an explicit host-supplied settings directory or another path mechanism validated in the native host |
| Configuration binding | `UI.Net.Composition` references configuration binder | Inspect binding call sites and use generated binding or explicit reads where analysis requires it |
| Package closure | Simple Injector 5.6.0, MessagePack 3.1.8, NATS.Net 3.0.1, Newtonsoft.Json 13.0.4 and other transitive packages | Record actual publish diagnostics and exercise used features; a package reference alone does not prove compatibility or incompatibility |

Paths abbreviated with `UI.Rust.Bridge.Net` refer to `TomasAI.IFM.UI.Rust.Bridge.Net`.

### 4.1 Dependency boundaries to review

The graph includes these edges:

- `UI.Net.Services -> Framework.OptionPricer`.
- `Domain.Reference.Shared -> Framework.MarketData`.
- `Domain.BrokerAccount.Shared -> Application.TradeBroker -> Framework.TradeBroker`.

Identify which shared contracts require these references. Move contract-only types to an appropriate shared assembly where that removes backend implementation dependencies from the client. Retain actual client pricing functionality. Do not remove a reference solely because its project name looks server-related.

### 4.2 Evidence that still requires a publish experiment

Simple Injector remains a compatibility risk, not a verified blocker. Its documentation describes expression construction/compilation during verification; NativeAOT supports interpreted expression execution but not arbitrary runtime code emission. Assess the actual container paths and constructor metadata before choosing to replace it. [Simple Injector verification behavior](https://docs.simpleinjector.org/en/latest/howto.html#verify-the-container-s-configuration), [NativeAOT limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

NATS dispatch and serializer fallback are similarly subject to reachability. Static source inspection identifies patterns to investigate; only publishing and exercising the native artifact establishes that every required closed generic type and formatter is available.

## 5. Serialization design

### 5.1 Rust to C# boundary

Preserve ABI v3 MessagePack field names, map/array structure, null handling, GUID strings, lookup timestamps and exact financial display strings. Compression remains `None` and remains independent of backend serializer defaults.

Replace contractless fallback with explicitly aggregated generated resolvers and custom formatters. Define named records for dashboard loading/error/full states and other anonymous wire values. Include nested arrays, dictionaries and every published subtype. MessagePack v3 supplies source-generated formatters and resolvers; the build should fail when a required type lacks an approved formatter. [MessagePack AOT generation](https://github.com/MessagePack-CSharp/MessagePack-CSharp#aot-code-generation)

### 5.2 C# to API server boundary

The NATS serializers currently use `MessagePackBinarySerializer.Options`. That boundary still uses LZ4 and existing C# contract conventions. Replacing the Rust bridge JSON envelope did not migrate this backend serializer.

Build a catalog of reachable request, response, event, identifier, result-wrapper and union types. Include each required closed generic combination, such as a specific `ServiceResult<T>`. Add generated or custom formatters for this catalog. Verify decoding against the current server and verify server decoding of AOT-generated requests. Keep this compatibility work separate from any future wire-schema redesign.

### 5.3 JSON documents that remain

Keeping JSON configuration or editable parameter documents is compatible with NativeAOT when their processing has an explicit supported implementation. Add generated `JsonSerializerContext` metadata for concrete models, AOT-compatible enum converters, and typed HTTP deserialization. Use `JsonDocument`/`JsonNode` where editing arbitrary document fields is the intended behavior.

Replace non-generic `JsonStringEnumConverter` usage in the AOT graph and runtime object serialization without registered metadata. Evaluate reachable Newtonsoft.Json usage individually. Disable reflection-based System.Text.Json defaults in the AOT profile so missing metadata fails during testing rather than remaining hidden by the JIT build. [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)

## 6. Service construction and shared behavior

Prefer constructor injection and explicit factories for the Rust client graph. Keep `IAppRoot`, `IUiServiceCatalog`, service contracts and shared view-model behavior stable.

If Simple Injector cannot be qualified, implement an AOT client composition path that creates the same service graph with explicit constructors. Account for singleton versus transient consumers, independent calendar listener lifecycles, NATS connection ownership, cancellation and async disposal. Replacing the container must not collapse separate subscriptions or change order acknowledgement behavior.

WinForms can keep its current container. Share business-facing services and view-model logic; use host-specific composition only where necessary. All shared changes require both UI regression suites.

For inspector screens, generate descriptors containing property name, display caption, typed getter, formatter and child traversal. Preserve visible property ordering, enum text, time formatting, truncation markers and stable node IDs. Replacing reflection must not reduce the information shown by the current UI.

## 7. Publishing and packaging

Use the existing bridge project as the publishing root. Add a Windows x64 NativeAOT publish profile instead of enabling AOT across the entire repository.

Illustrative profile properties:

```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <Configuration>Release</Configuration>
  <PublishAot>true</PublishAot>
  <NativeLib>Shared</NativeLib>
  <SelfContained>true</SelfContained>
  <EnableAotAnalyzer>true</EnableAotAnalyzer>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <TrimmerSingleWarn>false</TrimmerSingleWarn>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

The first audit should capture full diagnostics. Resolve unsupported reachable behavior, then make unresolved AOT/trimming warnings fail the dedicated publish gate. Do not restore repository-wide strict compilation as part of this effort. Review inherited `NoWarn` entries, including MessagePack diagnostics, so they cannot hide missing contract coverage. Mark owned libraries `IsAotCompatible` only as part of their verified compatibility work.

Windows publishing requires the supported C++ compiler/linker toolchain and Windows SDK. Pin the SDK and package versions used for comparison. [Microsoft NativeAOT publishing guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

Proposed artifacts:

```text
.artifacts/rust-ui/jit/
  ifm-ui-rust.exe
  managed/                         current .NET assemblies and runtime configuration

.artifacts/rust-ui/aot/
  ifm-ui-rust.exe
  native/TomasAI.IFM.UI.Rust.Bridge.Net.dll
  symbols/                        native bridge and Rust symbols
  configuration/                  UI client configuration
```

Keep package locations separate because a managed assembly and a native DLL can have the same filename. The native package includes required native dependencies and configuration; it does not rely on hostfxr, managed assembly discovery or an installed .NET runtime.

Use a named `UnmanagedCallersOnly` export in the publishing assembly. Factor shared bootstrap logic if needed, because an `UnmanagedCallersOnly` method cannot be invoked as an ordinary managed method. Preserve ABI v3 if its layout and semantics remain sufficient. If initialization must accept new configuration data through the function table, introduce a new ABI version explicitly.

## 8. Implementation milestones

| Milestone | Deliverable | Completion evidence |
| --- | --- | --- |
| 1. Audit and baseline | Dedicated analysis/publish profile, reachable package/type inventory and fresh JIT baseline after MessagePack migration | Complete warning log and a classified issue list; no declaration that the current build is already AOT-compatible |
| 2. Native host spike | Minimal exported DLL, Rust loader, create/copy/destroy lifecycle and permanent library ownership | Native Rust process loads the DLL without hostfxr; ABI size/version/buffer failures are correct; process shutdown succeeds |
| 3. Static bridge contracts | Named publications, generated resolver composition and approved custom formatters | All nine publication kinds and all four request gateways work in a published AOT fixture; 100,000 rows remain addressable |
| 4. Inspectors and remaining JSON | Typed/generated presentation mappings and JSON contexts | Strategy Details, property views, Market Outlook, provider/configuration editors, logging and health data retain their values |
| 5. Composition and transport | Qualified service construction, static NATS event routing and complete backend serializer catalog | An AOT client connects to the existing Development API, receives live updates, reconnects and disposes all subscriptions |
| 6. Shared UI parity | Complete AOT screen/action regression matrix with JIT and WinForms references | Widget tests and Windows UI tests verify controls, keyboard/mouse behavior, virtual lists, dialogs and notifications |
| 7. Measurements | Comparable JIT versus AOT startup, memory, allocation, GC, latency and package reports | Same Release workload, API data, window size and rendering backend; diagnostic tooling verified for the AOT process |
| 8. Development rollout | Explicit VS Code/build-script host modes and reproducible JIT/AOT packages | Development startup works in the chosen mode; switching back uses the JIT package without database or backend changes |

Milestone 1 should be the next implementation step. A small native host spike establishes the loader mechanics, but full view-model reuse is complete only after the live transport and shared UI milestones pass.

## 9. Validation matrix

| Area | Required checks |
| --- | --- |
| Native bootstrap | Named export exists; x64/Cdecl and table size are correct; version mismatch is explicit; invalid/stale handles and insufficient buffers return expected codes |
| Serialization | JIT/AOT/Rust compatibility; nulls, Unicode, GUIDs, enums, timestamps, maximum integer values, nested collections and exact financial strings; old JSON and malformed payload rejection |
| Published reachability | Exercise the actual trimmed native artifact; every screen, event type, formatter and generic response wrapper used by the client is covered |
| NATS lifecycle | Query success/failure, event delivery, timeouts, backend disconnect/reconnect, repeated screen opening and clean session shutdown |
| UI parity | Strategy tree and graphs, Market Outlook, status console, economic calendar, configuration, orders, fills and position history; preserve the current WinForms comparison tests |
| Trade submission | Use simulated broker acceptance/rejection, partial/final fills, timeout/unknown outcomes, duplicate requests and dialog sequencing; no real orders required for qualification |
| Virtual grids | 100,000 rows available, visible-range rendering, scrolling, sorting, selection and update stability |
| Host isolation | Multiple session create/destroy cycles; independent listeners; no library unload; no worker or subscription leak |
| Deployment | Start on a clean Windows test environment without installed .NET; correct settings location, locale, Eastern time behavior, native dependencies and symbols |

Extend `tests/bridge_messagepack.rs` to select both host implementations and point it at published AOT output. Keep native library loading separate from ordinary managed test loading: the NativeAOT DLL cannot be referenced by the C# test runner as a managed assembly. Tests may still reference the normal IL bridge build for unit coverage.

Use `egui_kittest` for widget behavior and existing FlaUI tests for Windows desktop accessibility and parity. Source-level tests and successful compilation do not replace execution of the native package.

## 10. Performance and diagnostics

Re-measure the current MessagePack/JIT build before comparing AOT. Earlier measurements taken before the binary transport migration are useful history but are not a controlled comparison baseline.

Collect startup to first frame and first usable snapshot, working set, private bytes, managed heap, allocation rate, GC frequency/pause time, rendering frame percentiles, bridge encode/decode latency, live-update latency, CPU use and package size. Use the same API dataset, visible screens, grid sizes, feed rate, GPU backend, DPI and Release configuration. Compare cold and warm launches separately.

NativeAOT may improve startup and JIT-related memory. It does not remove allocations from rebuilding snapshots, embedded JSON documents or managed model creation. AOT and allocation optimization should be measured separately.

Verify diagnostics against the native DLL hosted inside the Rust executable. Do not assume the earlier `dotnet-counters`/`dotnet-trace` workflow has identical support. Retain native PDBs, validate crash-dump stack quality, and add explicit managed GC/allocation counters at the bridge if the selected runtime/tooling does not expose the required data. [NativeAOT diagnostics](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/diagnostics)

## 11. Completion criteria and decision

The NativeAOT development option is ready when the published native package passes the transport, live API, UI parity, lifecycle and simulated trade tests; unsupported reachable behavior is resolved; package dependencies are reproducible; and performance has been compared against the fresh JIT baseline.

Retain the current JIT-hosted Rust UI until those results are available. Keep both WinForms and Rust behavior aligned throughout. The implementation choice can then be based on measured startup, memory, responsiveness, debugging and maintenance costs.

**Current conclusion:** NativeAOT is a feasible architecture to investigate, and the existing C ABI plus MessagePack contracts reduce the boundary work. The main unresolved work is the reachable C# service/serialization/reflection graph, not the Rust renderer. No NativeAOT application or compatibility certification is delivered by this design document.
