# G3 accepted result - Development - 2026-08-20

| Field | Result |
|---|---|
| Gate | G3 - NATS event and streaming catalog |
| Decision | **Passed** |
| Environment | `Development` |
| Live NATS result | 1 passed: 128 ordered correlated events, typed failure, stopped-listener rejection, and exactly-one delivery after reopen |
| Presentation/catalog result | 47 passed, 0 failed |
| Bounded-channel result | 26 passed, 0 failed |
| Cleanup | Succeeded; the live listener stopped and its NATS client was disposed |
| Source revision | The commit containing this summary |

## Accepted catalog

| Area | Accepted evidence |
|---|---|
| Status console | Newest-first 500-row bound, ordered batch ingestion, latest status line, no detached callback surface, and explicit status NATS route |
| Application and command response | Startup/shutdown routes, exact terminal-correlation tests, MessagePack runtime event deserialization, coded failure preservation, and a live correlated command-response burst |
| Economic calendar and market-data maintenance | Add/change/remove/import complete/fail routes; exact command matching; early-event buffering; unrelated-event rejection; durable refresh only after completion |
| EOD, bars, analytics, and trade signal | ES contract filtering, ordered/bounded bar snapshots, RSI/ATR/ADX/MACD activation for all six configured intraday timeframes, derived TDI delivery, and latest-value trade-signal display |
| Trade plan, position, placement, fund, order, trade, and state | Explicit consumer routes; lossless ordered batching for placement/plan history; exact fund/order/trade terminal correlation; bounded UI histories |
| Feed reset | Explicit reset route, terminal-aware feed state, and shutdown-owned listener lifecycle |
| Option ticks and spread bars | Awaitable callbacks, keyed latest-value coalescing for ticks, latest-value coalescing for spread bars, capacity-one behavior, and post-stop rejection |
| System administration | Backup event catalog includes completion, failure, error diagnostics, progress, verification, cancellation, policy, capability, and reconciliation; model correlation tests pass |

The live test uses the real local Development NATS broker and `CommandResponseUIEventConsumer`. It publishes 128 MessagePack `FuturesContractAddedCompleteEvent` messages on one subject and proves FIFO event IDs and command IDs, then publishes a typed failure and preserves its error correlation. After `StopAsync`, a published event is ignored. Reopening the same consumer creates one listener and the next command ID is delivered exactly once.

The deterministic channel suite exercises a ten-thousand-event fixed-capacity FIFO burst, capacity backpressure, retry/fault behavior, stop-and-drain behavior, serialized readers, concurrent writers, capacity-one latest-value replacement, independent per-key coalescing, convergence to each key's newest value, throttling, metrics, and rejection after stop. The presentation suite maps every G3 family to an explicit UI consumer and validates bounded observable state, correlation, failure diagnostics, timeframe identity, and lifecycle ownership.

Commands used for acceptance:

```powershell
$env:IFM_RUN_UI_G3_EVENTS = '1'
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj --configuration Debug --no-build --filter FullyQualifiedName~G3LiveNatsAcceptanceTests

dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests/TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~G3EventCatalogContractTests|FullyQualifiedName~IFMAppViewModelTests|FullyQualifiedName~StatusConsoleViewModelTests|FullyQualifiedName~EconomicCalendarEditorViewModelTests|FullyQualifiedName~TradeOrderEditorViewModelTests|FullyQualifiedName~IronCondorMonitorViewModelTests|FullyQualifiedName~IronCondorTradeOrderViewModelTests|FullyQualifiedName~DatabaseBackupModelTests|FullyQualifiedName~MarketDataAnalyticsCommandModelIntradayStartupTests"

dotnet test TomasAI.IFM.Shared.UnitTests/TomasAI.IFM.Shared.UnitTests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~EventChannel
```

G3 is accepted together with the already accepted live G1 and G2 desktop evidence. G4 resilience/lifecycle fault scenarios and operator confirmation remain open, so Milestone A is not yet complete.
