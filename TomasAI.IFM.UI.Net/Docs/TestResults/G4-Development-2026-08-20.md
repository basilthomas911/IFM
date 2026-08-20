# G4 accepted result - Development - 2026-08-20

| Field | Result |
|---|---|
| Gate | G4 - resilience and lifecycle |
| Decision | **Passed** |
| Environment | `Development` |
| Process audit | 1 passed: G4-001 through G4-009 observed with owned API/desktop processes |
| Live NATS fault result | 1 passed: disconnect, reconnect, typed failure correlation, exactly-one delivery, and stop rejection |
| Lifecycle regression | 5 passed, including close while initialization owns the lifecycle gate |
| Full presentation regression | 194 passed, 0 failed |
| Cleanup | Succeeded; normal desktop exit, no forced kill, no surviving desktop TCP ownership |
| Source revision | The commit containing this summary |

## Accepted evidence

| Scenario | Accepted result |
|---|---|
| NATS unavailable at launch | A bounded diagnostic appeared, the main shell stayed hidden, evidence was captured, and the desktop exited with code 1. |
| Broker reachable/API unavailable | The responsive shell proved broker connectivity while query subjects recorded distinct `NatsNoRespondersException` failures. |
| Disconnect/reconnect | An isolated TCP fault proxy dropped only the tested client connection; the listener reconnected and retained exact command/error correlation. |
| Repeated lifecycle | Three fully initialized desktop cycles closed normally without process, connection, listener, or unexpected-window leakage. |
| In-flight close | The NATS path was severed during startup commands/queries; cooperative cancellation plus a bounded presentation drain prevented deadlock and allowed normal process exit. |
| Listener failure/restart | Error code `7404`, its message, and the original command ID survived reconnect; the next event was delivered exactly once. |
| Dispatcher burst | A live 10,000-event MessagePack status burst left the shell responsive and retained no more than the configured 500 rows. |
| Modal handling | The expected fatal startup modal was captured as text, screenshot, and automation tree before native deterministic dismissal. |
| Network cleanup | Every desktop process exited and owned no TCP connection afterward. |

The in-flight defect required two product safeguards. `AsyncLifecycleCoordinator.StopAsync` now signals lifetime cancellation before waiting for the lifecycle gate, so it cannot wait behind initialization without first asking initialization to stop. The top-level WinForms shutdown also bounds presentation cleanup at ten seconds; if an external dependency cannot acknowledge a best-effort stop after its connection is severed, the form records that condition and allows the application context to dispose the shared transport.

The G4 harness includes `IFM_UI_G4_INFLIGHT_ONLY=1` for fast reproduction of the severed-transport close case. The full process audit remains the acceptance command.

Commands used for acceptance:

```powershell
$env:IFM_RUN_UI_G4 = '1'
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-restore --filter FullyQualifiedName~G4ProcessAcceptanceTests
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --no-restore --filter FullyQualifiedName~G4LiveTransportResilienceTests

dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests/TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj --no-restore
```

G0 through G4 are accepted. Operator confirmation is still required to close Milestone A; G5 remains the future complete paper-trading qualification gate.
