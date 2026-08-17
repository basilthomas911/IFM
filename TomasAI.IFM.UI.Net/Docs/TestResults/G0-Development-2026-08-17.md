# G0 accepted result — Development — 2026-08-17

| Field | Result |
|---|---|
| Gate | `G0` startup, initialization, and shutdown audit |
| Decision | **Passed** |
| Run ID | `20260817-010601-112b79e32ccb44fbb35cf651fc1e4d4a` |
| Environment | `Development` |
| Automated result | 25 passed, 0 failed, 0 blocked |
| Process-test duration | 48 seconds |
| Cleanup | Succeeded |
| Source revision | The commit containing this summary; the evidence run was produced from the pre-commit worktree based on `501957ee` |

The accepted run proved the real desktop and API process path. It observed NATS-only UI command/query/event traffic, 83 registered actor types, current ES rollover state, deterministic Development EOD/bar prerequisites through public APIs, correlated FMP reference-data import completion, economic-calendar rendering, all 24 intraday signal starts and stops, normal window close, the correlated market-data feed-stop completion event, no error-coded status messages, and removal of every harness-owned process and desktop network connection.

The DataBento prerequisite was separately qualified immediately before the run: both live ES (`GLBX.MDP3`) and VX (`XCBF.PITCH`) resolved-contract hydration checks passed. Development used provider-backed contract metadata with the configured paced synthetic tick source; accepted records drained before the shared publisher stopped.

There were no approved exceptions, failed steps, or blocked dependencies. Raw logs, UI Automation captures, process/network evidence, and the machine-readable result remain in the ignored run directory:

`TomasAI.IFM.UI.Net.SystemTests/TestResults/Runs/20260817-010601-112b79e32ccb44fbb35cf651fc1e4d4a-Development/`
