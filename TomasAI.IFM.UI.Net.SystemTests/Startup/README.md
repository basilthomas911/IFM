# Startup gate

`G0StartupAuditTests` is the executable, non-short-circuiting Development desktop process audit. It owns the API Server and WinForms PIDs that it starts, attaches through FlaUI, observes the same typed NATS event families used by the UI, executes all 25 registered steps, and always attempts evidence capture and cleanup.

The live audit is opt-in so normal builds and unit-test runs never launch external processes. Run it from an unlocked interactive Windows session after building the API Server and desktop:

```powershell
$env:FMP_API_KEY = '<credential>'
$env:IFM_RUN_UI_G0 = '1'
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-restore --filter Category=G0Process
```

The default configuration is Development with API readiness at `http://localhost:22543/health/ready`, NATS at `nats://localhost:4222`, PostgreSQL at `localhost:5432`, ScyllaDB at `localhost:9042`, and Redis at `localhost:6379`. Override these with `IFM_G0_API_URL`, `IFM_G0_NATS_URL`, `IFM_G0_POSTGRES`, `IFM_G0_SCYLLA`, and `IFM_G0_REDIS`. Executable, results, timeout, build-configuration, environment, and actor-count overrides use the `IFM_G0_*` names defined by `G0Configuration`.

Production FMP is the default and requires `FMP_API_KEY`. A deterministic adapter is accepted only when both `IFM_G0_FMP_ADAPTER=Deterministic` and `IFM_G0_APPROVED_ADAPTER=1` are explicitly supplied.
