# Scheduled Tasks development acceptance

Date: 2026-10-07. Environment: Development, host `development`, Quartz 3.19.1, America/New_York.

## Implemented and exercised

- Actor-owned catalog, definition and run decisions; durable events and Scylla query projections.
- One PostgreSQL lease owner, persistent Quartz jobs/triggers, readback before Applied receipts, restart reconciliation without business replay.
- Bounded processes, child containment, runtime/output limits, explicit uncertain-run resolution and artifact review digests.
- System Admin catalog/editor/preview/enable/disable/run output controls, owned notifications, Windows/Linux installers and deployment instructions.
- Stop-before-EOD workflow; dated immutable position rows; date rollover only after all EOD source commitments; healthy subscribed generation required for opening success.
- Serilog/OTLP logs and OTel metrics. Host metrics export returned HTTP 200; collector HTTP acceptance does not prove backend retention.

## Final test evidence

| Evidence | Result | Artifact |
| --- | --- | --- |
| Complete solution build | 0 warnings, 0 errors (final result recorded in build log) | `.artifacts/scheduled-tasks/final-solution-build.log` |
| Windows runtime / PostgreSQL restart / owner-death containment | 21 passed | `scheduler-final-windows.trx` |
| Linux runtime / PostgreSQL restart / owner-death containment | 21 passed | `scheduler-final-linux.trx` |
| Readiness and actual child environment, Windows | 14 passed | `final-readiness-environment-windows.trx` |
| Readiness and actual child environment, Linux | 14 passed | `final-readiness-environment-linux.trx` |
| Cron/manual occurrence regression, Windows | 3 passed | `cron-occurrence-regression.trx` |
| Cron/manual occurrence regression, Linux | 3 passed | `cron-occurrence-linux.trx` |
| Scheduled-task actor guards | 19 passed | `scheduler-final-actors.trx` |
| Futures / Vertical / Iron Condor daily rows and EOD sealing | 4 passed | `scheduler-final-lifecycle.trx` |
| FlaUI control regression | 1 passed | `scheduled-task-final-ui.trx` |
| Actual UI / NATS / Scylla screen | Initial persisted cron/zone/readiness readback passed; final screenshot shows all three schedules and applied revisions | `live-scheduled-tasks.png`, `restarted-main.png` |

TRX and live artifacts are under `.artifacts/scheduled-tasks`. Earlier value-date, EOD retry-identity and Scylla restart evidence remains in that directory. The full solution was built; these are focused tests, not a claim that every solution test was run.

## Real Windows business runs

### Close

Run `0220eec4-cf50-4242-bada-fe116337cdb7`, operation `61d7bf1e-6572-4b4c-b58d-ffa9afa073c3`, schedule `e296cd36-738e-47c9-b207-3fd7f8b6a0d0`, definition revision 2.

Quartz Run Now was explicitly requested after the October 7 session ended. The persisted result is Succeeded / BusinessCompleted, exit 0, completed EOD date 2026-10-07.

Observed order:

1. 21:54:49 UTC: correlated feed StoppedComplete received.
2. 21:54:50 UTC: both position EOD commands committed; PositionsFinalized receipt recorded.
3. Operational value date advanced to 2026-10-08; no active feed date remained.
4. Completed transport maintenance examined 106 streams and purged zero messages.
5. PostgreSQL backup `6eabbb63-97e8-4712-8308-256faff8ed2a` and Scylla backup `57341948-c491-440a-bb78-3b80ee9c985f` were accepted. Their completion/publication is separate from this task's submission result.

Evidence: `development/live-close-result.json`, `development/closed-market-session.json`, captured worker output.

### Open and automatic dispatch correction

The actual 18:00 daily cron fire exposed a missing manual-only `intendedFireUtc` key; no child process launched. Fixed occurrence construction now uses Quartz's ScheduledFireTimeUtc for automatic jobs, preserves manual IDs/correlation, and never substitutes the current clock for missing fire data. Both platform regressions pass.

Explicit current-session run `b57e41c8-62ad-4955-ae68-1fa5a0a77940` then completed successfully. Correlated startup, active date 2026-10-08, and healthy subscribed GLBX generation were checked before success.

Automatic one-time qualification schedule `ce666c01-31bd-40c3-8831-8d59fc0219f9` fired at 22:03:45 UTC. Its run `4ab250d8-dfc8-8313-25a0-41ef95cff6b9` is Succeeded / BusinessCompleted with Manual=False. The qualification definition was subsequently disabled and removed through commands; history was preserved.

Evidence: `development/live-open-result.json`, `development/open-market-session.json`, `development/automatic-open-result.json`, `development/qualification-removed.json`.

## Deployment and remaining acceptance boundaries

The daily close (`0 1 17 ? * MON-FRI`) and open (`0 0 18 ? * SUN-THU`) definitions are enabled, desired/applied revision 2. Set Closing Price remains disabled. No run reservations remain. API/UI/scheduler start through the normal development script.

Windows business close/open runs were exercised against the development system. Linux qualification used the actual portable process runner and persistent PostgreSQL Quartz engine; Linux business close/open against a separately deployed Linux API was not exercised.

The corrected daily cron's next scheduled close/open firings must still be observed in their normal future windows. Today's close was a manual Quartz occurrence; automatic opening was proven by a bounded one-time trigger following the actual daily cron defect. Do not describe these as an already-passed exact-time daily close/open acceptance gate. Keep this distinction when deciding full rollout acceptance under stage 7.

Real close receipts confirm EOD source commitments; the focused tests cover immutable history semantics. This report does not claim a separate readback of every development position's scalar history column, every seeded indicator, or completion of the asynchronously submitted backups.

## Final restart and UI layout verification

The final complete solution rebuild passed with zero warnings/errors. Normal development startup restored operational and active date 2026-10-08, EOD-pending false, and enabled market schedules at matching desired/applied revision 2. Evidence: `development/restarted-market-session.json`, `development/final-enabled-status.json`.

The administration window now defaults to a taller resizable layout and clamps its bounds after DPI scaling. The final live screenshot (`restarted-main.png`) visibly shows all three schedule rows, their persisted Enabled/Applied values, the editor and run-detail area. The final FlaUI control regression passed against rebuilt binaries.

The initial live FlaUI readback passed. Post-restart modal toolbar invocation timed out in UI Automation even though the dialog opened; the final screenshot confirms its display. That timeout is retained as an automation limitation, not counted as a passed post-restart navigation test. Probe processes were retired; API, UI and SchedulerHost remain running.

## Logs / Setup tab extension (2026-10-07)

Implemented the Logs-first main tab control, preserved Setup, task/year/month/day/occurrence tree, status circles, next scheduled and latest actual start timestamps, opaque-cursor history continuation, and retained stdout byte paging through query actors. Failed/rejected/uncertain outcomes are red; successful outcomes green, running yellow, enabled pending tasks blue, and disabled/removed tasks gray. Historical run colors preserve their own outcomes.

Evidence under `.artifacts/scheduled-tasks`:

- `logs-domain-tests.log`: 20 scheduled-task domain tests passed, including complete multi-page UTF-8 stdout, missing artifacts, invalid offset and path traversal.
- `logs-final-ui-tests.log`: 2 real WinForms/FlaUI tests passed, covering Logs/Setup, dated tree/status/paging, stdout page display and preserved Setup uncertainty resolution.
- `logs-final-api-build.log`, `logs-final-ui-build.log`: API/UI builds each succeeded with zero warnings/errors.
- `logs-live-query-result.log`: real API/NATS/Scylla queries read three persisted runs via native history paging and all three retained stdout artifacts (2005, 2005 and 2473 bytes). This probe was read-only.
- `logs-live-admin.png`: actual restarted System Admin screen visibly displays both tabs and persisted task roots, next scheduled dates and status circles.

The post-deployment UI Automation navigation encountered provider timeouts. The actual screen was captured independently; live tree-to-output selection was not counted as passed. The isolated FlaUI control tests and real actor/output reads passed separately. API/UI/SchedulerHost were restarted through the normal development script. No business schedules were run or changed for this extension.
