# Standalone Rust UI (development)

This is an independent copy of the current Rust screens. The existing
`TomasAI.IFM.UI.Rust` application remains the comparison application and default
launch target. This project is **not yet feature complete or accepted for cutover**.

## Ownership

- `src/app/runtime.rs`: background runtime, bounded action/publication queues,
  cancellation and service lifetimes. Rendering does no backend I/O.
- `src/backend`: native NATS, exact MessagePack contracts, compiled schema metadata
  and the existing HTTP health transport.
- `src/services`: API clock/session, correlated commands, feed observations and
  notifications, and read-only chart history.
- `src/state`: scoped controllers, selections, generation checks and operation
  outcomes. Unknown write outcomes retain their identities and are not retried
  under a replacement ID.
- Copied feature folders: rendering and local display state. Backend actors own
  pricing, indicators, risk decisions, persistence and feed recovery.
- `tools/ContractFixtures`: development-only C# compatibility oracle and isolated
  protocol responder. Neither the CLR nor this tool is shipped with the UI.

`ValueDate` and chart query windows come exclusively from the API session. UTC
timestamps retain their precision; Eastern conversion is for display. Plotting
published prices as floating-point coordinates does not change their authoritative
financial representation.

## Verification

### Durable error evidence

The standalone client writes JSON Lines to
`%LOCALAPPDATA%/TomasAI/IFM/UI.Rust.eGui/Logs/ui-errors.jsonl`, even when the
API or NATS is unavailable. Four rotated archives (`.1` through `.4`) and the
current file are limited to 5 MiB each across UI restarts (single UI supported).
Writes are synced to disk on the service/controller error paths, and mirrored
to stderr for Server Manager. Logging failures go to stderr without failing a
backend operation. An interrupted final record is repaired on the next append.

Each record contains UTC diagnostic time, PID, UI version, event, request
subject (or HTTP method and sanitized route), operation ID and full details.
Large details are split into numbered parts with the same operation ID.
This diagnostic clock does not supply the trading ValueDate. Request payloads
and NATS credentials are not logged; HTTP URL credentials/query strings are omitted.

`backend.request_failed` covers request/reply, decode and deadline failures;
the operation ID also travels in the `IFM-UI-Operation-Id` NATS header.
`backend.failure_notification_received` records each terminal notification,
including repeated deliveries, with the backend command/event IDs and source.
`ui.error_queued` and `ui.error_handed_to_ui` describe UI delivery stages, **not**
proof of user acknowledgement. Backend API/Server Manager logs separately record
publication attempts and successful producer completion using the same backend IDs.
Request correlation does not bypass notification deduplication or retry writes.

### Application alert policy

Runtime recovery dialogs have one current message, with no pending-dialog queue.
A newer error, warning outcome or success outcome replaces the open message.
Pressing OK dismisses that message; retained older publications cannot reopen it.
Repeated backend RecordIds are deduplicated. Full recovery history remains in the
server recovery audit and durable diagnostics, independently of this display slot.

Global Application Error dialogs are reserved for confirmed terminal dataset/API
recovery outcomes, the explicit two-minute dataset reset escalation, and failures
that prevent UI startup (runtime, configuration, credentials, API business context
or compiled-contract metadata). The native dataset owner supplies recovery facts;
the UI does not infer fatality from a numeric error code or a NATS disconnect.

Daily EMA/Bollinger refresh failures, stale-baseline warnings, calendar refresh
failures, NATS warnings, malformed notifications, busy
or unavailable actions and recoverable controller failures remain visible in the
status console and durable logs. They cannot replace, clear or increment an
outstanding terminal dialog. Request-specific validation/submission failures stay
with the originating view. A later terminal event can escalate an operation that
previously produced a warning. Warnings do not change backend retry/recovery policy
or represent successful recovery. Stopped observers explicitly report that a view
must be reloaded; this presentation change does not invent automatic recovery.

`ui.warning_replaced` and `ui.warning_handed_to_ui` identify non-modal diagnostics.
Request transport logs retain the subject, operation ID and full exception details.
Backend failure notifications remain audited even when classified as status only.

### Build and protocol checks

From the repository root:

```powershell
./scripts/UI/Rust/Verify-StandaloneRust.ps1
./scripts/UI/Rust/Build-StandaloneRust.ps1
```

Verification creates its own loopback NATS container and typed C# responder. It
does not connect test mutations to the Development broker or submit real orders.
The tests compare native projections with unchanged hybrid implementations where
an oracle exists. Headless projection and protocol tests are not desktop parity
acceptance. Complete the native feature owners before comparing both applications
with FlaUI, workflow by workflow.

Native controllers/services/consumers are implemented. Workstation qualification
passed 19 paired FlaUI scenarios (60 captures), plus the independent posting-date
correctness case. The current acceptance record in the full migration plan links
the exact executable hash, regressions, documented differences and remaining
clean-Windows/live-session gates. Run a named scenario after building the package
and acceptance tools:

```powershell
./scripts/UI/Rust/Test-StandaloneDesktopParity.ps1 -Scenario TomasAI.IFM.UI.Rust.eGui/tests/desktop/reference.json
```

This starts a fresh isolated responder for each application and compares actual
FlaUI control values, eligibility and relative geometry. Screenshots capture the
owned application window even when covered by another desktop window. Physical
input is withheld unless the test process owns foreground focus. Evidence,
executable hashes, loaded modules and shutdown results are retained beneath
`.artifacts/standalone-desktop-parity`. A passing navigation step alone does not
mark its data comparison or the whole migration accepted. Synthetic fixtures do
not establish live broker or live market-data acceptance.

Packaging creates one Windows executable and audits its PE imports for managed
runtime dependencies. The tested single executable is staged under
`.artifacts/standalone-rust-ui/app`. A Windows environment without .NET is still
required to establish the separate clean-machine gate; current workstation module
audits are not represented as that test.

The complete serial desktop suite can be run against a specific packaged file:

```powershell
./scripts/UI/Rust/Test-StandaloneDesktopSuite.ps1 -StandaloneExe .artifacts/standalone-rust-ui/app/TomasAI.IFM.UI.Rust.eGui.exe
```

The suite records that executable's hash and freezes its backend fixtures. Source
responsibilities and the untouched hybrid hashes are checked separately with
`python TomasAI.IFM.UI.Rust.eGui/tools/audit_native_ownership.py`. These are distinct
checks: file ownership does not certify an API method or a desktop action.

Build/isolated verification tasks are available in VS Code under **IFM Standalone
Rust UI**. Start the new client explicitly with an existing client profile:

```powershell
./scripts/UI/Rust/Start-StandaloneRust.ps1 -ConfigPath C:/path/to/ui-client.Development.json
```

This does not change the hybrid or ServerManager's default launch selection.

## Configuration

`IFM_UI_NATS_URL` selects the backend. The existing `AppSettings:NatsServerUri`
client setting is also supported; no implicit localhost backend is selected.
Read-only configuration loads `ui-client.json` and
`ui-client.<DOTNET_ENVIRONMENT>.json` beside the executable, or the existing
`managed` data directory when no direct profile exists. `IFM_UI_CONFIG_PATH`
selects a specific existing JSON file. Explicit environment overrides win.
The same profile supplies MarketData Host routing, TradeBroker account routing,
health endpoints, and supervisor settings. No managed assembly is loaded to read
those files. An explicit missing/malformed profile is an error.

`IFM_UI_ACTOR_HEALTH_URL` selects the existing actor health endpoint; absent an
explicit endpoint it is derived from the configured operations-health URL.
Scheduled-task operation metadata uses `IFM_UI_OPERATOR`, falling back to the
Windows `USERNAME`, matching the current operator identity. Run/resolution dates
come from the API session. Cron evaluation and job execution stay on the backend.

Supervisor mutation controls use the established `SupervisorControl__Enabled`,
`SupervisorControl__Requester` and `SupervisorControl__Timeout` settings, and also
require current backend authority. They are disabled by default.

`IFM_UI_OPERATIONS_HEALTH_URL` selects the operations endpoint.
`IFM_UI_LIVE_PIPELINE_HEALTH_URL` can explicitly select the dashboard diagnostic
endpoint; otherwise it is resolved as `live-health` relative to the operations
URL, matching the hybrid client. Missing diagnostics remain visibly unverified.

Use the migration plan in `Documents/system` and the generated operation inventory
in `tests/fixtures/backend/operations.json` to track remaining native owners.
An inventoried method or a copied screen is not evidence that its controller has
been migrated.

The broker route is explicit outside Development: set
`IFM_UI_BROKER_ACCOUNT_ALIAS` and `IFM_UI_BROKER_ENVIRONMENT` (`Paper`, `Live`,
or `Emulator`). The equivalent `TradeBroker__AccountAlias` and
`TradeBroker__Environment` environment keys are also accepted. Development uses
the existing emulator default when omitted and rejects Paper/Live routes. The
same resolved route is used for account reads, qualification, preparation and the
Blotter mode label; backend authorization remains authoritative.

## API-owned calendar dates

The standalone date picker is adapted from `egui_extras` 0.32.3. Its popup uses
the API session's Eastern civil date for the today marker. Until that session is
available it shows no today marker. Year navigation follows the selected date
and remains within the backend `DateOnly` range (years 1 through 9999). It never
substitutes the workstation date. The upstream MIT permission notice lives in
`src/date_picker/LICENSE-MIT`, is embedded in the executable, and is checked by
the package audit. The unchanged hybrid retains its existing calendar behavior;
desktop comparisons record that date-authority exception explicitly.

Sibling date pickers use independent widget and popup identities, so saving a
value date cannot change the accounting date. A two-picker interaction regression
and a native FlaUI case verify distinct selections. The unchanged hybrid's shared
popup identity defect is retained as baseline evidence, not copied as required
standalone behavior. The user approved this standalone-only parity exception on
2026-10-25.

### VS Code combined launch

Select `IFM: API + eGui Rust UI (Development)` in Run and Debug. It automatically
reads the existing `TomasAI.IFM.UI.Net/appsettings.Development.json` client
settings; no profile prompt or duplicate configuration is required. The preparation
task builds the API, Market Data Host, scheduler, Server Manager and standalone
UI; Server Manager then starts and owns the services and selected UI using the
existing API readiness and shutdown workflow. This explicitly selected launcher
does not change the default UI or the hybrid Rust launcher. Starting it replaces
the current managed Development session, just like the other combined launches.

### Trade-load measurements

Every standalone `load-trade` operation records total elapsed time and correlated
stage timings automatically. Records are written to stderr (captured by Server
Manager) and `%LOCALAPPDATA%/TomasAI/IFM/UI.Rust.eGui/Logs/trade-load-<pid>.jsonl`.
The file rotates at 5 MiB with one previous file per process. Each record includes
load ID, process ID, per-process load number, selected setup IDs, outcome, and
stage start/duration in ms;
it does not log prices, account balances, credentials or exception contents.

Run `python scripts/UI/Rust/Summarize-TradeLoadTimings.py` to see mean/median/max
totals and the slowest stages. Compare the first full load after startup with
close/reopen full loads and already-open tab activation. A full load can still
use backend caches; the diagnostic does not label that as a cold database.
Stages include API business context, contract registry decoding, established
trade, event subscriptions, parallel position history/risk limits, fund balance,
selected plan/leg quotes/chart, saved feed intent and any live restoration.
Isolated-test records are labelled and excluded from the summary.
Nested/parallel durations overlap and must not be summed. Error and interrupted
loads are retained; an optional balance/chart failure may coexist with a
successful overall load. Total covers the controller load through monitor
publication, not time waiting in the UI command queue or the next painted frame.
The clock is operational only and never supplies business dates. Rebuild/restart
the standalone UI to collect measurements; the hybrid is unchanged.

### Optimized Development builds

The combined VS Code eGui launcher uses `-Profile Development`: application
optimization level 1, incremental compilation, line-level debug information,
256 code-generation units and no LTO. Dependencies retain optimization level 3
for responsive rendering. Cargo uses up to four parallel compiler jobs (override
with `-Jobs`). The first build warms this separate profile cache; subsequent
source edits can reuse incremental compiler work.

Use the `IFM Standalone Rust UI: Build Release` task, or
`powershell -File scripts/UI/Rust/Build-StandaloneRust.ps1 -Profile Release`, for
full optimization level 3 and release qualification. Direct script calls still
default to Release; existing packaging/acceptance commands keep that behavior.
The legacy `-DebugBuild` switch remains available for unoptimized debugging.
All profiles retain static CRT linking, executable packaging and dependency
audits. Launch preparation prints the selected profile to the task terminal.

### Progressive trade loading

Core trade details, saved position history, risk limits and balances load first.
The selected position is published immediately; chart history and saved-position
plan/leg observations hydrate concurrently in the background. Their loading or
error messages stay within the relevant section and never remove the loaded tab.
Automatic saved-feed restoration is a separate second phase: it waits for the
stored chart and selected-position reads to finish (or report their local errors),
then checks/restores saved intent using the API-provided ValueDate. Host connection
setup and subscription restoration never delay the completed trade-open command.
A timeout or rejection leaves the tab, saved position, saved plans and chart visible,
with the restoration failure shown in monitor status. A successful Load already
reconciles subscriptions, so the UI does not send a duplicate Enable command.
Stored values remain visible until authoritative live values replace them. Explicit
Live Feed ON/OFF cancels the pending automatic task and takes precedence.
Each optional read
uses the backend's normal bounded request budget independently of the completed
open command. Selecting another position, changing live mode or closing/reopening
a tab fences late results; replacement jobs are cancelled. Live chart samples
remain separate from loaded history and are merged by the existing renderer.

Foreground timings now measure core readiness. Optional reads emit separate
`trade_load_background_timing` records with `parent_load_id` correlation when
started by the load command; the summary reports those separately. Do not compare
this core-ready total to the older all-sections-ready total as identical metrics.

### Health dialogs with an open trade

Operations Health and Actor Health open independent resizable dialogs. The
loaded trade remains drawn underneath and keeps consuming live updates. Closing
the dialog returns to that trade without reloading it or toggling Live Feed.
`tests/desktop/health-navigation-loaded-trade.json` exercises both dialogs through
FlaUI with an already loaded live trade and requires actual Window accessibility
nodes, not just refresh buttons appearing somewhere in the main pane.

Health observations do not depend on market-session activity. Operations freshness
uses the API `X-IFM-Server-Utc` operational clock (supplied through server
TimeProvider), or the deployed server's standard HTTP Date header for compatibility.
It never compares health against the last market-session transition or the UI
workstation clock. Missing/invalid server time and genuinely stale observations
remain explicit failures. Actor history uses the supervisor API's observation
and default time window; it loads automatically when its dialog first opens.
Inactive market stages remain visible alongside required API/Host health checks.

### Live system console

Console traffic uses Core NATS `Notify.StatusConsoleEvent.Logged.>` directly.
There is no JetStream console stream, catch-up request, render receipt, ACK or
redelivery. Messages sent before subscribing or while disconnected are not replayed.

Retention is twice the number of visible rows: `2 * ceil(body_height / (18 + row_spacing))`.
The first layout measures the viewport; resize updates capacity. Newest rows appear
at the top, oldest rows are evicted, and growing the window never restores evicted rows.
Intake hands bounded latest snapshots to a separate GUI relay, so painting cannot
block console reception. Repeated sidebar snapshots coalesce before GUI delivery.
Warnings occupy one replaceable row and cannot evict recovery deduplication identities.
Only an actual disconnected-to-connected transition initiates connection refresh work.
