# IFM UI system-test specification

| Item | Decision |
|---|---|
| Test asset owner | `TomasAI.IFM.UI.Net` |
| Initial system under test | Legacy WinForms executable `TomasAI.IFM.UI.Net` |
| Future system under test | Migrated WPF executable rooted at `TomasAI.IFM.UI` |
| Automation stack | xUnit, FlaUI 5.0.0, UI Automation 3 (UIA3) |
| First mandatory gate | G0 — complete startup and shutdown audit |
| WinForms restoration gate | G0 through G4 plus operator acceptance |
| Runtime evidence | `TomasAI.IFM.UI.Net.SystemTests/TestResults/Runs/<run-id>/` |
| Accepted summaries | `TomasAI.IFM.UI.Net/Docs/TestResults/` |

## Purpose

This specification defines the legacy WinForms system-test harness and the UI journeys that are sufficiently understood to automate. The executable and its UI test assets use the `TomasAI.IFM.UI.Net` ownership prefix. The executable is `TomasAI.IFM.UI.Net`, unit tests are in `TomasAI.IFM.UI.Net.Presentation.UnitTests`, and FlaUI system tests and raw results are in `TomasAI.IFM.UI.Net.SystemTests`.

`TomasAI.IFM.UI` is the pure WPF executable. Until WPF test projects are introduced, its only additional project folder is `Docs`, which owns the WPF migration information. The WinForms system-test implementation is not shared with that project by physical placement or project identity.

These are process-level UI system tests, not substitutes for Model, ViewModel, actor, or service unit and integration tests. They launch the real desktop executable, observe it through Windows UI Automation, and exercise the same NATS command, query, and event paths used by an operator.

The first implementation work package is G0. The remaining catalog is documented now so its data, diagnostic, and automation requirements are visible before implementation.

G0 through G4 validate Milestone A, the legacy operational restoration defined in [`IFM Operational Restoration and Trading Capability Roadmap`](../../Documents/system/IFM-Operational-Restoration-and-Trading-Capability-Roadmap.md). They restore and prove the previously existing system behavior; they do not constitute paper-trading readiness. Complete paper trading is a later system capability program requiring Milestones B through F.

## Supported topology

- `TomasAI.IFM.Application.Api.Server` remains a supported server. It hosts HTTP endpoints for other clients and the NATS actor runtime used by the desktop UI.
- The desktop UI uses `TomasAI.IFM.Application.Api.Nats.Client` for commands and queries and NATS event consumers for live events.
- A UI test must verify that the desktop process connects to NATS on the configured broker endpoint. It must not use or connect to the API Server HTTP endpoint for application operations.
- The readiness HTTP endpoint may be queried by the test harness to diagnose the API Server process; that is test orchestration, not desktop client transport.

## Automation rules

1. Run in an interactive, unlocked Windows desktop session at a known display scale. Windows services and headless agents are not valid unless an interactive desktop has been provisioned.
2. Do not run desktop tests in parallel. One harness owns the desktop, SUT process, and test data at a time.
3. Select controls by stable `AutomationId`, semantic role, label, or window identity. Screen coordinates and image matching are not primary selectors.
4. Use FlaUI retry and wait conditions for readiness. Do not use fixed sleeps as a correctness mechanism.
5. Capture evidence before teardown when an assertion, timeout, unhandled dialog, or process failure occurs.
6. Use only Development/test environments and reversible, uniquely prefixed records. Automated tests must never point at production trading infrastructure.
7. Destructive or order-producing journeys require explicit isolated fixtures and cleanup. Live broker order placement is outside the current gates.
8. Keep semantic page objects and workflow assertions independent of WinForms. The WPF implementation should expose the same logical automation identities where practical.

## Gate sequence

| Gate | Scope | Entry condition | Exit condition |
|---|---|---|---|
| G0 | Startup, initialization, and shutdown audit | Harness builds and prerequisites can be probed | Every required step passes; no required step is blocked |
| G1 | Shell navigation and read-only queries | G0 passes | Supported windows open, render queried state, and close cleanly |
| G2 | Reversible commands and maintenance workflows | G1 passes and isolated seed data exists | Create/change/remove and import workflows pass with cleanup |
| G3 | Live NATS events and streaming UI behavior | G2 passes and deterministic publishers/feeds exist | Ordering, coalescing, responsiveness, and teardown pass |
| G4 | Failure, reconnect, and lifecycle resilience | G3 passes | Supported recovery paths and repeated lifecycle tests pass |
| G5 | Reserved for future complete paper-trading qualification | System roadmap Milestones B through E pass and the paper environment is approved | Milestone F scenarios, reconciliation, risk, soak, and operator approval pass |

Later-gate tests may be developed independently, but a release cannot claim a later gate while an earlier required gate is red.

## G0 — startup and shutdown audit

### Non-short-circuiting behavior

G0 is an audit, not a single fail-fast assertion. It records every startup and shutdown step that can safely be evaluated in the run:

- A failed step does not stop independent probes.
- A fatal process failure blocks only the steps that require that process.
- A missing prerequisite is `BlockedDependency`, not silently skipped and not automatically classified as a product defect.
- The run remains unsuccessful while any required step is `Failed` or `BlockedDependency`.
- Cleanup and evidence capture run even when startup is incomplete.

Every step has one of these statuses:

| Status | Meaning |
|---|---|
| `Passed` | Expected behavior was observed |
| `Failed` | The prerequisite existed, but observed behavior violated the requirement |
| `BlockedDependency` | The step could not run because required environment, configuration, data, or an earlier capability was unavailable |
| `SkippedDependency` | The step was intentionally omitted because its optional condition was false |
| `NotRun` | The harness did not attempt the step; this is an audit defect unless the run was explicitly cancelled |

### G0 step register

| ID | Startup/shutdown check | Dependency and expected result |
|---|---|---|
| G0-001 | Validate test configuration and create the run evidence directory | Valid environment name, executable paths, endpoints, and writable results location; require `FMP_API_KEY` when the production FMP adapter is selected, or the explicit approved-adapter configuration otherwise, and record only credential presence—never the secret value |
| G0-002 | Probe the configured NATS endpoint | Broker accepts a connection on the configured endpoint, normally port 4222 |
| G0-003 | Probe required PostgreSQL, ScyllaDB, and Redis services | Each configured service reports reachable/ready, or the exact missing dependency is recorded |
| G0-004 | Start `TomasAI.IFM.Application.Api.Server` in Development | Process remains alive and logs are captured |
| G0-005 | Verify API readiness and actor runtime | Readiness succeeds and the expected actor types are registered; the current baseline is 83 registered actor types, independent of dynamically started entity instances |
| G0-006 | Launch the configured desktop executable | Initial target is `TomasAI.IFM.UI.Net`; PID and start time are recorded |
| G0-007 | Await desktop NATS readiness | A direct desktop-PID socket is recorded when the host exposes it; when a local container proxy owns the loopback socket, an active endpoint connection plus typed UI-initiated import traffic proves readiness without a fixed startup delay |
| G0-008 | Find the responsive main window | `IFMAppView` appears, has the expected title, and responds to UI Automation |
| G0-009 | Audit desktop network transport | Typed UI-initiated command/event traffic proves NATS use, the endpoint connection table is captured, and the desktop PID has no connection to API HTTP port 22543 |
| G0-010 | Observe initial status and reference-data command intake | Typed `Notify.StatusConsoleEvent` traffic and both parameter-only reference-data import events are observed; unexpected listener errors fail the relevant step |
| G0-011 | Query currently traded ES futures contracts | A configured current contract is returned; absence is currently `BlockedDependency` due to test data |
| G0-012 | Load latest EOD, signal, and bar state | Missing Development EOD and bar prerequisites are established through the public NATS command API and confirmed through queries; the trade signal remains an independently required durable domain result |
| G0-013 | Observe automatic FMP yield-curve import | The UI submits the parameter-only domain import command, retains its command ID, and observes the matching complete event; provider acquisition occurs behind `IReferenceDataApi`, and the durable MarketData query state matches the accepted provider result, including a valid zero-row result |
| G0-014 | Observe automatic FMP economic-calendar import | The UI submits the parameter-only domain import command, retains its command ID, and observes the matching complete event; provider acquisition occurs behind `IReferenceDataApi`, and the durable MarketData query state matches the accepted provider result, including a valid zero-row result |
| G0-015 | Verify startup-import lifecycle policy | Both terminal listeners started before command submission, matched only the exact command ID, stopped after the bounded attempt, and issued no retry; a failed or unobserved normal-path import fails G0 even though the UI must report it and continue startup |
| G0-016 | Query and render application value date | A valid value date is returned and shown in the shell state; reference-data imports were attempted before this live-trading-hours gate |
| G0-017 | Render economic-calendar country/date/list state | Requires imported or seeded data; selected filters and displayed result are recorded |
| G0-018 | Observe EOD, bar, trade-signal, placement, and feed-reset consumer startup | Each required consumer starts exactly once without an unhandled error; the retained legacy trade-placement actor request is bounded to five seconds, reports unavailable state, does not retry, and cannot block the remaining startup sequence |
| G0-019 | Start the configured current futures feed | Requires G0-011; command acceptance and correlated event/status are recorded |
| G0-020 | Start the authoritative intraday analytics profile | For the active ES contract and value date, observe exactly 24 typed `Started` events: RSI-13, ATR-14, ADX-14, and MACD-9/12/26 for 15 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour, and 4 hours; no daily-or-longer actor is started |
| G0-021 | Reach initialized shell state | Status reports that all 24 intraday signal actors started, then reports initialization complete and enables applicable toolbar actions; any partial signal-start result is a failed G0 step and must show that no retry occurred |
| G0-022 | Request main-window close | Normal UI close is accepted without force-killing the process |
| G0-023 | Observe analytics, listener, and producer shutdown | Observe matching stop completion for the same 24 intraday signal identities; owned consumers and shared producers stop without duplicate stops or a self-stop deadlock |
| G0-024 | Observe conditional feed-stop behavior | If a feed was started, stop command/event completion is captured |
| G0-025 | Verify bounded process exit and cleanup | Desktop exits within the approved threshold; no error-coded status message, orphan UI process, desktop network connection, listener, or signal timer remains |

The initial shutdown reference measurement is 5.4 seconds. The implemented default automated threshold is 15 seconds and can be changed explicitly with `IFM_G0_SHUTDOWN_TIMEOUT_SECONDS` when an approved environment requires a different bound. The entire non-short-circuiting audit has an independent 30-minute default ceiling, configurable with `IFM_G0_AUDIT_TIMEOUT_SECONDS`; this preserves evidence collection and cleanup when several bounded steps fail in one run.

The Development API host uses provider-backed DataBento contract discovery but
a deterministic synthetic tick source paced at ten records per second per
dataset. This exercises the complete durable tick path as a continuous feed
without injecting the unpaced qualification burst into the G0 lifecycle window;
normal stop still drains every accepted record and must emit its correlated
successful terminal event.

### Current expected baseline

The previous controlled verification established working end-to-end NATS command, query, and event connectivity. It also established that the desktop connected to port 4222 and not API HTTP port 22543, received status events, and shut down gracefully.

The FMP treasury-curve and economic-calendar adapters, provider-neutral `IReferenceDataApi`, parameter-only domain import flows, bulk MarketData storage writes, terminal complete/fail events, server dependency injection, and UI startup orchestration are implemented. The production adapter reads `FMP_API_KEY` from the host environment and sends it in a redacted request header. The UI attempts both imports once, reports only failure or an unobserved outcome, performs no retry, and continues startup so a user can import later.

G0 must determine environment readiness at run time instead of describing either FMP integration as an unimplemented dependency. A missing `FMP_API_KEY`, unreachable provider, unusable Development data store, absent current ES contract, or absent deterministic market data is recorded with its exact dependency status and prevents a green run. Provider or import failure after all declared prerequisites are present is `Failed`, not evidence that the integration is missing.

The authoritative intraday startup profile is also implemented and integration-tested through NATS. The G0 process test must still prove that the actual desktop startup selects the current ES contract, starts all 24 configured entity instances, reaches initialization complete, and stops the same instances on close. These dynamic entity instances do not change the API host's registered actor-type count.

The missing `YieldCurveRateEditViewModel` and `TimeProvider` composition registrations found during controlled runs have been corrected and are protected by architecture regression assertions. Yield-curve command validation owns its stateless validation rules directly, so startup imports no longer depend on an undeclared actor-container registration. Calendar controls expose stable accessibility names for process-level assertions, while automation-tree evidence tolerates unsupported provider properties. The retained legacy trade-placement signal start/stop calls now accept cancellation, use a five-second UI bound, and issue a stop only after a confirmed start, preventing an unavailable legacy actor from holding startup or shutdown for the transport's two-minute default request timeout.

The accepted Development baseline is run `20260817-010601-112b79e32ccb44fbb35cf651fc1e4d4a`: all 25 required checks passed in 48 seconds and cleanup succeeded. This includes unique audit identities for actor-internal signal generation, absence of error-coded status messages, and the correlated feed-stop terminal event after both dataset-specific aggregators drained through their shared epoch publisher. The reviewed result is retained in [`TestResults/G0-Development-2026-08-17.md`](TestResults/G0-Development-2026-08-17.md).

## Evidence and result contract

Every run creates `TomasAI.IFM.UI.Net.SystemTests/TestResults/Runs/<UTC timestamp>-<configuration>/` containing, as applicable:

```text
summary.md
result.json
screenshots/
automation-trees/
logs/ui/
logs/api-server/
network/
processes/
```

The machine-readable run and each step record include:

- run ID, step ID, status, dependency IDs, UTC start/end, and duration;
- environment/configuration name and relevant non-secret endpoint names;
- SUT, API Server, and owned dependency process IDs;
- window identity, `AutomationId`, control type, enabled/visible state, and focused state;
- expected behavior, actual behavior, exception, dialog text, and timeout reason;
- actor command/query name, correlation ID, and NATS subject where observable;
- relative paths to screenshots, UI Automation tree dumps, logs, and network/process evidence;
- required configuration/data prerequisite and whether it was present;
- cleanup result and any surviving owned process or connection.

Secrets, API keys, credentials, and full connection strings must be redacted. Raw run artifacts are ignored by Git. An approved human-readable gate summary is copied to `TomasAI.IFM.UI/Docs/TestResults/` when it needs to be retained with the source.

## Required deterministic test data

Before G0 can pass, the Development UI test fixture needs:

- a NATS endpoint and an API Server configuration that host the application actor runtime;
- reachable configured PostgreSQL, ScyllaDB, and Redis dependencies;
- a DataBento native/runtime build that supports the startup contract-catalog query required by `FuturesContractRolloverStartupService`;
- one known application value date;
- one currently traded ES futures contract;
- deterministic latest EOD, signal, and bar data for that contract where the startup branch requires them; G0 may create missing EOD and bar records through the domain command API and verifies them through queries, while a trade signal remains a prerequisite;
- FMP enabled in the API Server and a valid `FMP_API_KEY` available to that process, or an explicitly approved deterministic adapter with representative treasury and economic-calendar responses;
- representative imported or deterministic seeded data for rendered-state assertions that require non-empty rows; a valid 0-row provider import remains a successful domain outcome and is recorded as such;
- expected intraday signal identities derived from the active ES contract and value date using the authoritative 15-second, 1-minute, 5-minute, 15-minute, 1-hour, and 4-hour profile;
- unique record prefixes for reversible maintenance tests;
- cleanup ownership rules that distinguish harness-created state from existing operator state.

The API Server HTTP readiness URL may be present in harness configuration. It must not be injected into or used by the UI client.

## G1 — navigation and read-only query catalog

### Shell and navigation

- Verify startup actions are disabled until initialization permits them and then enabled consistently.
- Open and close Market Data, Funds, Reference, System Administration, trade, and other available shell destinations.
- Verify selector/editor dialogs expose their expected controls and cancel without mutation.
- Verify add/change/remove enablement follows selection and validation state.
- Reopen singleton/modeless screens and assert no stale selection, duplicate window, or duplicate event subscription.
- Detect, record, and close unexpected modal error dialogs so the audit can continue.

Current WinForms identities include `IFMAppView`, `tradeButton`, `marketDataButton`, `fundButton`, `referenceButton`, `systemAdminButton`, `lblStatus`, `tabTradeBlotter`, and the status-console tab. The page-object layer should provide semantic names instead of exposing these legacy field names to tests.

### Read-only queries

- Shell: current contract, value date, latest EOD/signal/bar branches, initialization status, and bounded status history.
- Economic calendar: Today, Yesterday, Tomorrow, This Week, Next Week, country selection, date selection, and rendered results.
- Market data: definition lists, futures contracts, futures options, current/selected contract details, and yield-curve date/existence state.
- Reference data: lookup types, lookup values, and default-definition selections.
- Funds: fund list/detail, transactions, orders, trades, balance, and profit/loss views.
- Trading: plans, positions, trade state, placement state, option spreads, and read-only Iron Condor state when deterministic seed data exists.
- System administration: configured database names and available non-mutating status/details.

Each query test validates both actor success/correlation evidence and user-visible rendered state. A successful backend response with a stale or empty UI is a test failure.

## G2 — reversible command catalog

The following workflows are understood well enough to specify, but they require isolated seed/cleanup fixtures before automation:

- Start and stop the current market-data feed and observe correlated status/events.
- Create, change, and remove futures contract definitions.
- Create, change, and remove futures option definitions.
- Create, change, and remove yield-curve records; exercise the provider import with the configured FMP credential or approved deterministic adapter.
- Create, change, and remove economic-calendar records; exercise the provider import with the configured FMP credential or approved deterministic adapter.
- Create, change, and remove lookup types/values where domain rules permit.
- Create a test fund and reversible transactions, orders, and trades under a unique run prefix.
- Run supported EOD actions only against an isolated deterministic dataset.
- Run database backup only against an approved non-production test database and verify completion without changing the default operator environment.

Existing Iron Condor screens, plans, positions, calculations, and stored order information are tested for restored behavior in G1 through G4. New broker-integrated order placement, broker interaction, simulated fills, automated execution, and paper-trade qualification remain G5 and depend on system roadmap Milestones B through E. They are not general CRUD smoke tests and are not required to accept Milestone A.

Command tests record the command name, correlation ID, terminal command response/event, visible UI state change, and cleanup result. Clicking a button without observing the command outcome is insufficient.

## G3 — NATS event and streaming catalog

- Status console: publish/receive representative statuses, preserve the newest-first bounded history, and prevent duplicate deliveries after reopening.
- Application lifecycle and command responses: correlate terminal state with the initiating UI operation.
- Economic calendar and market-data maintenance: reflect create/change/remove events exactly once.
- Futures EOD, futures bar, RSI, ATR, ADX, MACD, TDI, and trade-signal events: update the intended state in order and preserve timeframe identity.
- Trade plan, position, placement, fund, order, trade, and state events: preserve lossless ordering for state transitions.
- Market-data feed-reset events: reflect reset state without leaving controls incorrectly enabled.
- Futures-option ticks and option-spread bars: exercise latest-value/coalesced behavior so a slow UI does not accumulate stale display work.
- System-administration events: display correlated completion/failure and retain diagnostics.

Burst scenarios measure dispatcher responsiveness, bounded grids/history, event ordering for lossless streams, and coalescing for latest-value streams. They must also prove that closing a screen stops its listener and that reopening starts exactly one listener.

## G4 — resilience and lifecycle catalog

- NATS unavailable at launch: display a diagnosable startup failure and do not show a falsely initialized main window.
- Broker reachable but actor API unavailable: record actor readiness/query failures separately from broker connectivity.
- Supported disconnect/reconnect: recover once, without duplicate consumers or stale operation state.
- Repeated launch/initialize/close cycles: no process, connection, consumer, or window leak.
- Close during an in-flight query/command: cancel or finish according to the operation policy and exit without deadlock.
- Listener failure and restart paths: retain coded error and correlation information.
- UI dispatcher safety: no cross-thread exception, frozen window, or unbounded queued visual updates during bursts.
- Unexpected modal dialog: capture text, screenshot, and automation tree before deterministic dismissal.
- Process/network cleanup: no orphan desktop process and no desktop HTTP transport regression.

## G5 — future complete paper-trading qualification

G5 is a reserved future gate for Milestone F. It will cover complete manual and automated strategy, account, broker execution, reconciliation, monitoring, exit, and paper-trade journeys only after G0 through G4 and system roadmap Milestones B through E pass, required reference/market data exists, and the paper environment is explicitly approved. Exact scenarios, risk limits, expected simulated fills, soak duration, and stop conditions require a separate acceptance specification.

## Explicitly deferred or not yet testable

- Pixel-perfect WPF/QTS visual conformance and discretionary WinForms visual changes.
- Production orders or production broker connectivity.
- Independent financial correctness certification of FMP's live values beyond IFM's implemented authentication, contract validation, mapping, import, storage, and rendering responsibilities.
- Long-duration paper-trading soak, Milestone F qualification, and live-trading readiness gates.
- Coordinate/image-based visual regression unless a later visual specification explicitly requires it.

## Automation identity policy

WinForms control `Name` values generally surface as UI Automation IDs. Tests should bind those IDs inside semantic page objects and expose operations such as `OpenMarketData` or `ReadStatus`, not designer field names. If a critical control lacks a stable identity, add the minimum automation metadata necessary; do not redesign the legacy view merely to make it easier to test.

The WPF migration should preserve semantic identities or update only the adapter/page-object mapping. Business workflow assertions and result schemas must remain reusable across the two executables.

## Project and folder structure

```text
TomasAI.IFM.UI.Net/
  Docs/
    UI-System-Test-Specification.md
    TestResults/

TomasAI.IFM.UI.Net.Presentation.UnitTests/
  Architecture/
  Lifecycle/
  Models/
  Operations/
  Presentation/
  TestDoubles/
  ViewModels/

TomasAI.IFM.UI.Net.SystemTests/
  Infrastructure/
  Startup/
  Queries/
  Commands/
  Events/
  Navigation/
  Lifecycle/
  Diagnostics/
  TestResults/
    Runs/
```

The category folders contain executable tests and support code as implementation proceeds. Cross-cutting fixtures, readiness probes, FlaUI application/window ownership, waits, screenshots, tree capture, and result writers belong in `Infrastructure` and `Diagnostics` rather than individual tests.

## Build and execution

The package and compile validation command is:

```powershell
dotnet restore TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj
dotnet build TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-restore -p:DatabentoEnableLive=true
```

G0 is implemented as an opt-in process test. Execute it in an unlocked interactive Windows session with the approved Development services and deterministic data:

```powershell
$env:FMP_API_KEY = '<credential>'
$env:IFM_RUN_UI_G0 = '1'
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-build --filter Category=G0Process
```

Normal test runs do not launch the API Server or desktop. The non-process infrastructure coverage is selected with `--filter Category=G0Infrastructure`. Defaults and the complete `IFM_G0_*` override schema are documented in `TomasAI.IFM.UI.Net.SystemTests/Startup/README.md` and enforced by `G0Configuration`.

## Acceptance

G0 is the first restoration gate. It passes only when every required G0 step is `Passed`, cleanup succeeds, and no step is `Failed`, `BlockedDependency`, or `NotRun`. The FMP implementation is present; the run must supply valid provider credentials or the approved deterministic adapter, usable Development infrastructure, a current ES contract, and deterministic current-market data. It must also observe successful terminal completion for both startup imports and all 24 intraday analytics actor starts. Existing NATS transport and lower-level integration verification are prerequisites, not substitutes for this desktop process evidence. Milestone A acceptance requires G0 through G4 and operator confirmation; G5 is outside the restoration milestone.
