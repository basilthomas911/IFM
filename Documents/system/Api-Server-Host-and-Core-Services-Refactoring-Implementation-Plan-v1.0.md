# IFM API Server Host and Core Services Refactoring Implementation Plan

**Version:** 1.0  
**Date:** 2026-10-09  
**Status:** Implemented and offline/Development verified on October 9, 2026; live trading-hour qualification remains deferred.  
**Source specification:** [API Server Host and Core Services Refactoring Specification v1.0](Api-Server-Host-and-Core-Services-Refactoring-Specification-v1.0.md)  
**Scope:** Extract host services into Core, simplify executable composition, preserve the current single API process, and establish a reusable host template.

## 1. Intended outcome

`TomasAI.IFM.Application.Api.Server` retains Program, Startup, appsettings, launch configuration and executable packaging. `TomasAI.IFM.Application.Api.Server.Core` contains supporting host classes in the capability folders defined by the specification. Program builds one provider and invokes an injected lifecycle service. Startup becomes a small DI composition facade.

This is an organizational refactor. Existing actors, contracts, financial state, realtime generation fencing, cache behavior, schedules, run modes and process boundaries remain unchanged. Aspire resource orchestration, Linux/WSL2 activation and additional process extraction are later deployment work.

The baseline is 57 root C# files, including 55 supporting files. Section 7 of the specification is the authoritative relocation manifest. Reconcile that manifest with the workspace at implementation time; record newly added files rather than silently leaving them behind.

## 2. Work rules and invariants

- Preserve unrelated workspace changes. Review the current diff and record an identifiable baseline before extraction; no blanket reset, clean or rollback.
- Perform physical extraction before broad namespace changes or lifecycle rewrites. Each stage must compile before the next dependent stage.
- Keep the executable project name, output identity, appsettings keys, environment gating, launch profiles, CLI flags and exit behavior stable.
- Registration has no database/provider startup side effects and does not build temporary service providers. Preserve existing actor container bridges and service lifetimes explicitly.
- Keep one owner for actor startup, StartApplication dispatch, shutdown, scheduled jobs, provider sessions and telemetry export.
- Preserve HTTP binding before actor subscriptions become externally consumable.
- No new recovery checks, drain waits, replay paths, financial bypasses or schema/data deletions are introduced.
- Queries and projections follow existing actor/event conventions. Moving host adapters does not move business handlers into Core.
- Run Development verification through existing VS Code/ServerManager entry points. Do not launch an additional host against a running owner's ports or subscriptions.
- Live fault injection is a separate, explicitly scheduled verification step; record existing authorization and environment before performing it.

## 3. Dependency and folder targets

```text
Api.Server executable
    ??? Api.Server.Core DLL
          ??? existing application capability libraries
          ??? existing domain actor/shared libraries
          ??? existing framework integration libraries
```

There is no Core-to-executable reference. Future satellite hosts use capability-specific Core libraries instead of referencing the complete central Api.Server.Core.

Core folders: DependencyInjection; Hosting/Contracts, Modes and ServerManager; Startup/Application, Actors, Readiness and Schema; Actors/Registration, Lifecycle and Recovery; Recovery/Databento and Shutdown; MarketData capability folders; Messaging/JetStream; Trading/Emulation; Http; Observability; Deployment; and Development. Create subfolders when implementations exist. Follow the full specification for individual class destinations.

## 4. Stage 1 ? Baseline and characterization

### Work

1. Read repository conventions and inspect current changes, solution/build settings and API project references.
2. Inventory every root file, registration, hosted service, actor singleton, subscription owner, run mode, HTTP mapping and logger override.
3. Trace actual bootstrap order from Program through Startup, hosted services, ActorMaps, supervisor startup, provisioning and ApplicationStartup dispatch.
4. Record shutdown paths: normal host cancellation, ServerManager standard-input stop, partial startup failure and fatal recovery.
5. Identify test/benchmark references, ApiServerEntryPoint use, generated JSON contexts, reflection/assembly scanning and friend assemblies.
6. Record worker build/publish assets, content-root resolution, current-working-directory assumptions and script launch paths.
7. Run the existing API Release build and relevant startup verification as a baseline. Characterize uncovered behavioral boundaries using existing test fixtures before changing their implementation.

### Deliverables and gate

Store a baseline inventory and results under `.artifacts/api-server-core-refactoring/`. Record exact commands and environment without credentials. Gate: a documented runnable baseline, class manifest and lifecycle ordering. Existing failures are identified separately so they are not attributed to extraction without evidence.

## 5. Stage 2 ? Create Core and move supporting classes

### Work

1. Add `TomasAI.IFM.Application.Api.Server.Core.csproj` targeting the repository's current .NET version, with nullable settings and documentation conventions aligned with API. Use the class-library SDK plus ASP.NET Core framework reference; no executable entry point or appsettings.
2. Add Core to `TomasAI.IFM.sln` and reference it from the API executable.
3. Move the 55 supporting files using the specification's manifest. Move each type once; retain its namespace initially where necessary to keep the change reviewable.
4. Transfer package/project references needed by the relocated code. Resolve cycles by correcting the ownership boundary, not by adding reverse references.
5. Move assembly metadata for relocated internals/source generation to Core. Preserve host-entry identity separately; an executable marker may live in Program.
6. Relocate operational/performance docs to Documents/system and update links. Runtime artifacts are not moved as source.
7. Address executable-relative worker assets through build/publish ownership. Core must not infer the API project directory from its own assembly location.

### Deliverables and gate

Organized Core library; API root has only Program and Startup as hand-authored C# files; no duplicate types. Gate: API/Core and affected consumers build, no dependency cycle, all root-supporting files accounted for. Namespace modernization is not combined with this mechanical step.

## 6. Stage 3 ? Extract service-registration modules

### Work

1. Create `DependencyInjection/RegisterApiServerCore.cs` as the registration entry point.
2. Extract existing Startup registration groups into capability modules. Split the large RegisterBaseServices and RegisterHostedServices groups by responsibility rather than keeping them as a new monolithic file.
3. Preserve lifetime, registration order, factories, environment conditions and actor container bridging. Document any required ordering explicitly.
4. Move non-registration helpers, such as historical profile creation, dotnet-host-path resolution and import-policy parsing, beside the capability they configure.
5. Keep Startup as a facade that configures host options and calls Core extensions. Remove premature `BuildServiceProvider` usage using bootstrap logging and ordinary injected dependencies.
6. Verify one intended registration/owner for each lifecycle singleton, background service, exporter and actor runtime.

### Extraction guide

| Current responsibility | Core destination |
| --- | --- |
| ConfigureApiServer | Hosting/bootstrap configuration, Http and Observability registration helpers |
| RegisterBaseServices | Capability modules under DependencyInjection; shared identity/options under Deployment/Hosting |
| RegisterCommandApiServices, RegisterEventApiServices, RegisterQueryApiServices | DependencyInjection/Actors and Messaging |
| RegisterStorageServices | DependencyInjection/Storage |
| RegisterServiceHandlers and RegisterGenericTypes | DependencyInjection/Actors and explicit container bridge |
| RegisterEventProducers | DependencyInjection/Messaging |
| RegisterHostedServices | Capability registration modules with explicit lifecycle ownership |
| RegisterTradeBrokerEmulator | DependencyInjection/Trading and Trading/Emulation |
| ConfigureRequestPipeline | Http/Middleware and endpoint configuration |

### Deliverables and gate

Small Startup facade and cohesive registration modules. Gate: composition tests and verification mode resolve required services with the expected lifetimes, without duplicate registrations or registration-time operational work. Existing integration behavior remains unchanged.

## 7. Stage 4 ? Extract host lifecycle and execution modes

### Work

1. Define `Hosting/Contracts/IApiServerLifecycle.cs` and implement ApiServerLifecycle. It receives the built host and orchestrates registered components; Program remains the host/provider disposal owner.
2. Move identity validation, HTTP mapping, bootstrap preparation, actor runtime coordination, provisioning and shutdown orchestration out of Program.
3. Preserve the existing registered dispatcher as the sole owner of StartApplication handoff. The coordinator must not dispatch a second command in parallel with a hosted service.
4. Move CLI parsing and one-shot branches to Hosting/Modes with explicit mode selection, precedence, mutation permissions and exit behavior.
5. Preserve startup verification precedence over mutating modes. Maintenance modes do not start unrelated actors, HTTP listeners or feeds where the existing mode forbids them.
6. Route ServerManager stop, Ctrl+C and terminal recovery through the existing termination semantics without duplicate shutdown/disposal or added drain waits.
7. Reduce Program to builder creation, composition, build, resolved lifecycle invocation and final process cleanup.

### Mode compatibility inventory

Capture behavior for normal run; startup verification; recovery-infrastructure qualification; schema initialization; catalog migration; strategy-family bootstrap; instrument-definition refresh; current and dated option-pricing reference publication; Iron Condor risk initialization; risk-history backfill; event-log qualification settings; and Development provisioning verification. Discover all additional flags and script expectations during Stage 1. Do not retire any flag incidentally.

### Deliverables and gate

Lean Program, explicit lifecycle and mode dispatch. Gate: characterization checks preserve binding/actor exposure order, single application-startup handoff, no-write verification behavior, partial-start cleanup and exit codes. Fatal shutdown still terminates according to the implemented guarantee.

## 8. Stage 5 ? Update consumers, namespaces and publish assets

### Work

1. Update test/benchmark references and InternalsVisibleTo based on actual type ownership. Existing friend assemblies include Application actor tests, Securities integration tests, LivePipeline integration tests and API benchmarks; confirm the complete list.
2. Preserve the executable assembly used by host integration fixtures. Moved marker types must not accidentally make WebApplicationFactory target Core.
3. Move internal namespaces to capability namespaces under Api.Server.Core only after checking serialization, persisted type names, actor metadata, reflection and logger filters. Leave externally meaningful identities stable unless separately migrated.
4. Update source-generated serializers and assembly scans to include the owning Core assembly exactly once.
5. Update documentation and scripts only where paths truly change. VS Code and ServerManager keep targeting the API executable; they never attempt to launch the Core DLL.
6. Build and publish from a clean dedicated artifact directory. Verify appsettings, Core dependencies and worker binaries in the published output.
7. Verify supported one-shot startup from a different working directory, using explicit content-root/configuration paths.

### Deliverables and gate

Updated consumers and self-contained publish layout in the existing deployment model. Gate: clean Release solution build plus API publish succeed; affected tests discover the correct entry point; published host and worker assets resolve without repository-relative assumptions.

## 9. Stage 6 ? Offline and Development behavior verification

### Required verification

- Existing startup/actor/DI tests and meaningful checks for missing registrations or wrong lifetimes.
- Port-bind failure prevents exposing actors from a failed host.
- Application startup is handed off once, with failures/partial cleanup reported accurately.
- Verification modes preserve their documented write/start restrictions.
- Development schema/default provisioning remains additive, exact-version-aware and idempotent; no duplicate capital posting.
- Cache facade identity, accepted composition input, generation fencing and recovery service ownership remain stable.
- Scheduled market close/open behavior and scheduler ownership remain intact; market close keeps API and backup availability.
- Normal VS Code/ServerManager API and UI startup/shutdown works; inspect for orphan owned processes and duplicate subscriptions.
- Health/status/log export and lifetime GC reporting retain service/deployment/process-run identity.
- Recovery unit/component tests retain attempt/escalation/fatal behavior; no extra waits or checks have been inserted.

Prefer existing tests and reuse isolated infrastructure where needed. Do not perform destructive database restoration, change live financial state, force a market-close transition or inject a recovery fault merely to prove folder organization.

### Deliverables and gate

Record build, test, process and log evidence with exact commands. Classify tests as Passed, Failed, Blocked or Deferred, including reasons. Gate: all applicable offline checks pass, normal Development script behavior is verified and concrete remaining risks are reported. Passing compile alone is insufficient.

## 10. Stage 7 ? Live qualification during trading hours

Run after fresh live data is available and the test environment is confirmed. This stage remains distinct from offline refactoring acceptance.

1. Start through the supported development entry point and verify single API/scheduler/provider ownership.
2. Verify fresh futures inputs, background option-cache readiness and manual UI coexistence.
3. Exercise authorized soft/hard recovery using the established controlled test path; correlate operation/actor/dataset identities in logs and confirm continued current-generation processing.
4. Verify market-session close/open ordering at the natural transition or an isolated workflow test: stop feed, seal EOD, advance value date only after successful EOD; reopen validates the date and starts the feed without restarting application bootstrap.
5. Confirm no duplicate subscriptions, stranded workers, changed financial semantics or telemetry identity loss.

Gate: recorded live evidence. If trading is closed, explicitly mark this stage Deferred with a follow-up window; do not call live qualification complete. The organizational refactor can be reported implemented/offline-verified while this live gate remains open.

## 11. Stage 8 ? Documentation and reusable template acceptance

Update the source specification with actual paths, lifecycle API and extraction evidence. Publish the API/Core pattern for future hosts. Record exceptions for existing host names rather than renaming them automatically.

Document Windows/Linux dependencies identified during extraction, but leave Aspire orchestration and WSL2 activation for a separate plan. Future hosts use capability-specific Core libraries; any shared hosting helpers require demonstrated reuse and cannot pull central actor/database composition into satellites.

Gate: the accepted folder pattern, migration record, operational instructions and outstanding deployment decisions are discoverable and agree with source.

## 12. Rollback and blocker policy

Create reviewable stage boundaries in the change set. If a stage fails, diagnose the concrete dependency or behavior and repair it before continuing to dependent work. Typical blockers include circular project dependencies, persisted type identity changes, wrong executable assembly selection, missing worker publish assets or an unavailable required verification environment.

An unexpected behavior regression is a blocker until repaired or explicitly accepted. A closed market is a deferred live verification gate, not a reason to undo successful offline extraction. Linux-only or future Aspire questions do not block the current Windows organizational work.

Rollback restores the prior API source/project composition and registration/lifecycle path for the affected stage only, preserving unrelated workspace changes. This refactor makes no data/schema deletions and has no database rollback step. Remove extracted duplicate registrations before resuming; do not leave two startup owners enabled. Commit/push remains a separate user instruction.

## 13. Completion record

| Stage | Status |
| --- | --- |
| 1. Baseline/characterization | Complete |
| 2. Core project and file extraction | Complete |
| 3. Registration modules and lean Startup | Complete |
| 4. Lifecycle/mode extraction and lean Program | Complete |
| 5. Consumers, namespaces and publish assets | Complete; Core capability namespaces and consumers updated |
| 6. Offline/Development verification | Complete |
| 7. Live qualification | Deferred; futures market closed |
| 8. Documentation/template acceptance | Complete |

Implementation completion requires stages 1-6 and 8, with the live stage's actual status reported explicitly. Full behavioral qualification additionally requires Stage 7. Aspire deployment is outside this plan. The implementation record below distinguishes completed source work from deferred live verification.


## 14. Implementation record: October 9, 2026

### Delivered structure

- Api.Server root now has only Program.cs and Startup.cs as hand-authored C# files. The executable retains configuration, launch/build metadata and its integration-test entry marker (declared in Program).
- Api.Server.Core is a net10.0 DLL with an ASP.NET Core framework reference and the moved implementation dependencies. It is included in TomasAI.IFM.sln and referenced by the executable, with no reverse dependency.
- All supporting root classes follow the specification manifest. The four existing ParameterSets helpers also moved to Core/Startup/ParameterSets. Operational, performance and legacy implementation documents moved under Documents/system.
- CoreServiceRegistration is organized into partial registration modules. Base wiring delegates to host options/health, caching/serialization, recovery, workflow and actor runtime groups. Hosted wiring separates market-data runtime setup from import/trade services. Partial methods share implementation helpers without constructing an additional service provider.
- ApiServerLifecycle is resolved through DI. It delegates normal host execution to ApiServerRuntime and one-shot modes to Hosting/Modes. Operational HTTP mappings are in Http/Endpoints. Process GC/logging/failure helpers are in Hosting.
- Core implementation namespaces now follow capability ownership under `TomasAI.IFM.Application.Api.Server.Core`. Consumers and reflected startup type names are updated. Executable entry-marker, service/assembly identity and existing metric names remain associated with the API host.
- ApiServerEntryPoint stays in the executable for existing host integration fixtures. Friend access for moved classes is present in Core, and the storage constructor used by registration grants access to Core. Existing tests/benchmarks continue to compile through the host's transitive Core reference.
- Deployment identity now includes the Core DLL hash in build and publish manifests. Standalone-process detection still recognizes the API executable rather than comparing its entry assembly to the moved service assembly.

### Verification evidence

All paths below are relative to the repository and refer to .artifacts/api-server-core-refactoring/:

| Evidence | Result |
| --- | --- |
| baseline-build.log | Original API Release build passed |
| solution-build.log | Refactored full solution Release build passed, zero warnings/errors |
| application-tests.log | 73 application/startup/recovery/identity tests passed |
| cache-tests.log | 30 option-cache/composition preparation tests passed |
| startup-verification.log | Real composition-root verification passed without schemas, actors, feeds or HTTP listeners starting |
| publish.log | Dedicated API Release publish passed; Core DLL and worker DLL/deps/runtimeconfig assets included |
| published-startup-verification.log | Published host verified from a different working directory with explicit content root |
| development-start.log | Existing Development start script built and started the managed session |
| development-processes.json | One owned manager, API, UI and scheduler observed |
| development-readiness.json | Launch-readiness endpoint returned HTTP 200 |
| development-stop.log | Existing Development stop script verified all managed application processes stopped |

No new provider recovery fault was injected and no database data was deleted for this refactor. Normal Development startup retains its existing additive schema/default provisioning behavior. Live quote freshness, sustained recovery and natural market-session transitions require the next trading-hour window; offline tests and managed launch readiness do not prove those live conditions.

The verification session was stopped through the supported script. No commit or push was performed.

## 15. Core namespace migration

At the user's request, every Core source namespace was migrated to `TomasAI.IFM.Application.Api.Server.Core` with its capability/folder suffix, including `Startup.ParameterSets`. Imports and fully qualified references were updated in the executable, tests, integration infrastructure, recovery probes and benchmarks. The integration factory's reflected `IActorRuntimeStartupSignal` name now resolves to `Core.Startup.Actors`.

Partial classes use the namespace of their owning class across all source files: `CoreServiceRegistration` uses `Core.DependencyInjection` (including builder and request-pipeline partials), `ApiServerLifecycle` uses `Core.Hosting`, and `ApiServerModes` uses `Core.Hosting.Modes`. Capability subfolders organize these partial implementations without splitting a single class between namespaces.

The executable `Startup` facade and `ApiServerEntryPoint` remain in `TomasAI.IFM.Application.Api.Server`. Executable filenames, deployment identity, telemetry service names and startup-handoff meter name are unchanged. No application settings require a class-specific logger category update.

Verification: the complete Release solution builds with zero warnings/errors; 73 application/startup/recovery/identity tests and 30 option-chain/composition tests pass; the Development `--verify-startup-only` composition check passes. Evidence is in `namespace-build.log`, `namespace-application-tests.log`, `namespace-cache-tests.log`, and `namespace-startup-verification.log` under `.artifacts/api-server-core-refactoring/`.
