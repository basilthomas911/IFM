# UI process gates

`G0StartupAuditTests` is the executable, non-short-circuiting Development desktop process audit. It owns the API Server and WinForms PIDs that it starts, attaches through FlaUI, observes the same typed NATS event families used by the UI, executes all 25 registered steps, and always attempts evidence capture and cleanup.

`G1NavigationAndQueryAuditTests` uses the same ownership and evidence infrastructure for the 15-step navigation/read-only gate. It establishes expected selector and row state through typed NATS queries, then proves the real shell, status history, ES/VX charts, calendar ranges, supported modal catalogs, normal close, and bounded cleanup through UI Automation. Only one process gate may run at a time.

The live audit is opt-in so normal builds and unit-test runs never launch external processes. Run it from an unlocked interactive Windows session after building the API Server and desktop with the live DataBento native adapter:

```powershell
$env:FMP_API_KEY = '<credential>'
$env:IFM_RUN_UI_G0 = '1'
dotnet build TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-restore -p:DatabentoEnableLive=true
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-build --filter Category=G0Process

$env:IFM_RUN_UI_G0 = $null
$env:IFM_RUN_UI_G1 = '1'
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-build --filter Category=G1Process
```

The default configuration is Development with API readiness at `http://localhost:22543/health/ready`, NATS at `nats://localhost:4222`, PostgreSQL at `localhost:5432`, ScyllaDB at `localhost:9042`, and Redis at `localhost:6379`. Override these with `IFM_G0_API_URL`, `IFM_G0_NATS_URL`, `IFM_G0_POSTGRES`, `IFM_G0_SCYLLA`, and `IFM_G0_REDIS`. Per-operation readiness, startup, and shutdown limits use `IFM_G0_READINESS_TIMEOUT_SECONDS`, `IFM_G0_STARTUP_TIMEOUT_SECONDS`, and `IFM_G0_SHUTDOWN_TIMEOUT_SECONDS`. The non-short-circuiting audit has a separate 30-minute default ceiling controlled by `IFM_G0_AUDIT_TIMEOUT_SECONDS`, so failed operations still leave enough time for the remaining evidence steps and cleanup. Executable, results, build-configuration, environment, and actor-count overrides use the other `IFM_G0_*` names defined by `G0Configuration`.

Production FMP is the default and requires `FMP_API_KEY`. A deterministic adapter is accepted only when both `IFM_G0_FMP_ADAPTER=Deterministic` and `IFM_G0_APPROVED_ADAPTER=1` are explicitly supplied.

When the current ES contract has no latest Development EOD or bar record, G0 establishes those two prerequisites through `MarketDataFeedCommandApi` and confirms durability through `MarketDataFeedQueryApi`. The harness never writes the tables directly and never fabricates the required trade signal; that signal must already be available through its domain query path.
