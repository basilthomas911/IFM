# Scheduled Tasks deployment and recovery

## Development on Windows

Run `scripts/ScheduledTasks/Install-IFMDevelopmentScheduler.ps1` with the existing `ifm_db` PostgreSQL container running. This creates only the scheduler role/database, publishes approved projects, and writes credentials into the ignored `.artifacts/scheduled-tasks/development/scheduler.settings.json`, restricted to the current Windows identity. Repeat deployment preserves that credential and never overwrites actor-owned definitions. Stop the scheduler before republishing its running artifacts.

The standard `scripts/Development/Start-IFMDevelopment.ps1` starts the prepared scheduler alongside Server Manager/API/UI. Readiness is persisted by the host; process existence alone does not mean ready. The owner lock is a PostgreSQL advisory lock for the Quartz scheduler identity. Legacy seeding and legacy settings mutations are disabled in ActorManaged mode.

Use the published `Administration/TomasAI.IFM.Application.ScheduledTask.Administration.exe` with `scheduler.settings.json` as its first argument:

| Operation | Arguments after settings | Result |
| --- | --- | --- |
| Inspect | `status [schedule UUID]` | Persisted catalog and definitions; selected schedule includes run history |
| Market session | `market-status` | Held operational date, active date and EOD-pending state |
| Create missing defaults | `prepare` | Disabled definitions; existing settings retained |
| Adopt legacy | `adopt export.json` | Export first; preserve IDs and cron/zone; create disabled for review |
| Activate | `enable <schedule UUID>` | Review current artifact digest, host readiness and revision |
| Disable | `disable <schedule UUID>` | Desired disable; wait for matching Applied revision |
| Run current task | `run <schedule UUID> "operator reason"` | Explicit Quartz manual occurrence; inspect persisted outcome |
| Opening qualification | `qualify-open` | One automatic opening in 60 seconds; review then disable/remove its returned ID |
| Remove disabled task | `remove <schedule UUID> "operator reason"` | Remove definition/trigger after disable and reservation resolution; retain history |

Market close ID: `e296cd36-738e-47c9-b207-3fd7f8b6a0d0`, cron `0 1 17 ? * MON-FRI`.
Market open ID: `9a93a75d-7404-4d61-812e-a996ca39ef31`, cron `0 0 18 ? * SUN-THU`.
Both use `America/New_York`. Both development market schedules are enabled, with desired and applied revision 2. Set Closing Price is separately reviewed. No startup re-enables a disabled definition.

## Cutover and rollback

1. Stop the legacy host/settings writer. Export its definitions and retain its run history and prior artifacts/settings.
2. Deploy the actor-managed host with the same Quartz database/identity. Do not start another owner.
3. Run `adopt`; review exported prior enabled states and any one-time/interval schedules requiring explicit conversion. The tool refuses to replay an old one-time occurrence.
4. Inspect actor projection and desired/applied revisions, then explicitly enable reviewed schedules. Do not delete Quartz or historic run tables.
5. To roll back, put the current host in standby and stop it before selecting one prior ownership mode. Never run legacy and actor writers together; never rerun an uncertain business operation automatically.

## Linux and Windows services

Quartz cron expressions and IANA zones are identical on both platforms. Provision PostgreSQL separately and supply a protected settings JSON with `ConnectionStrings:SchedulerDbConnection`, host/environment identity, endpoints, and NATS/OTLP settings. Run `scripts/ScheduledTasks/Install-IFMLinuxScheduler.sh <deployment-root> <protected-settings.json>` on Linux; it publishes native portable apphosts and converts approved artifact paths. Use a dedicated account with access only to deployment, run output and its database.

Example systemd unit (adjust roots/account):

```ini
[Unit]
Description=IFM actor-managed scheduler
After=network-online.target
[Service]
User=ifm
WorkingDirectory=/opt/ifm/scheduler/Host
Environment=DOTNET_ENVIRONMENT=Development
ExecStart=/opt/ifm/scheduler/Host/TomasAI.IFM.Application.ServerManager.SchedulerHost --settings /opt/ifm/scheduler/scheduler.settings.json
Restart=on-failure
KillMode=control-group
TimeoutStopSec=60
[Install]
WantedBy=multi-user.target
```

On Windows, install the published host as a Windows Service with `--settings <protected settings path>` and a dedicated service identity. Grant that identity access to its settings/run folder, the scheduler database and selected operator pipe group. Configure deployment paths explicitly; do not share two service owners.

## Interrupted runs and logs

Restart reconciliation releases confirmed terminal runs. Requested/admitted/running reservations without a process owner become Uncertain; business work is not replayed. In System Admin, inspect stage/value date/source outcomes and output, enter a review reason, and use **Resolve as Failed**. The command requires the current run revision and releases the matching reservation. It does not certify EOD, advance a date, or roll back already completed work.

Run details display bounded stdout/stderr tails, identities and relative output directory; complete artifacts remain under TaskRunRoot. Logs use Serilog and the existing bounded OTLP sink; metrics use the shared OTel pipeline. Credentials and entire message payloads are not logged.

Close stops all Databento feeds before dated EOD commands. Operational date advances only after position finalization is committed; backup/maintenance failure does not undo completed EOD. Open requires correlated startup plus active-date and subscribed GLBX.MDP3 readiness. Late market marks cannot modify a sealed EOD position.

Relative executable paths are relative to each task working directory, not the deployment root. Child processes receive the configured host identity in `DOTNET_ENVIRONMENT`, `ASPNETCORE_ENVIRONMENT` and `IFM_ENVIRONMENT`, together with run and operation correlation IDs. Reapply installer settings after publishing an individual task so its local log paths and OTLP settings are retained.

## Retained stdout for the Logs tab

Set API configuration `ScheduledTasks:OutputRoot` to the SchedulerHost `TaskRunRoot` directory. Development startup inherits `IFM_REPOSITORY_ROOT` and defaults to `.artifacts/scheduled-tasks/development/TaskRuns` under that repository. A remote scheduler needs its retained artifacts mounted on the API host and this setting pointed to the mount. The UI requests persisted run identities and bounded 64 KiB output pages; it cannot supply arbitrary paths. If a retained artifact expired or is missing, Logs identifies that condition and falls back to the persisted stdout tail. Capture/retention limits still apply, including any truncation marker written by the runner.
