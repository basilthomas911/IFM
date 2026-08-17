# G1 accepted result — Development — 2026-08-17

| Field | Result |
|---|---|
| Gate | `G1` shell navigation and read-only query audit |
| Decision | **Passed** |
| Run ID | `20260817-030000-3f87f23d187b4e8a8b015356b70ef75e` |
| Environment | `Development` |
| Automated result | 15 passed, 0 failed, 0 blocked |
| Process-test duration | 3 minutes 6 seconds |
| Cleanup | Succeeded |
| Source revision | The commit containing this summary; evidence was produced from its pre-commit worktree |

The accepted run started the real API and WinForms desktop, verified 83 registered actor types, and established its expected state through typed NATS queries. It proved initialized shell/status behavior, 19 visible status rows, populated market-outlook values, three ES chart points, one current VX/VIX chart point, and all five economic-calendar ranges.

The modal catalog audit rendered all three Market Data views (2,430 ES options, 7 futures contracts, and the yield-curve view), both Reference views (25 lookup names and the economic-calendar editor), 12 valid named funds with balance/transaction/profit-and-loss state, existing Trade order/trade state without submitting a command, and the supported Backup Databases System Administration view. Legacy `JobScheduler` reference data was intentionally not advertised because scheduled-task redesign remains deferred.

Market Data then reopened exactly once with the same three-item catalog and no unexpected modal. The desktop accepted a normal close; typed query/event sessions stopped; there were no error-coded status messages; and every harness-owned process was removed. There were no approved exceptions, failed steps, or blocked dependencies.

Raw screenshots, UI Automation trees, logs, typed event evidence, process evidence, and the machine-readable result remain in the ignored run directory:

`TomasAI.IFM.UI.Net.SystemTests/TestResults/Runs/20260817-030000-3f87f23d187b4e8a8b015356b70ef75e-Development/`
