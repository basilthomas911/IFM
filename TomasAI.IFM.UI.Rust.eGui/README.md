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
