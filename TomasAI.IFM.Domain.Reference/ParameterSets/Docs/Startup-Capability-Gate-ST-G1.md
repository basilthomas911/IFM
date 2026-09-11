# Startup capability gate ST-G1

Status: SUPERSEDED as a startup blocker by the user-approved optional-history policy. Historical preparation gaps below remain implementation findings; signals may start empty and warm from live data. See implementation plan version 1.2 section 26 for the RSI-first pilot. Recorded 2026-09-10. Parameter-set assignment ownership is approved separately as workflow definition + horizon.

## Required capability and evidence

The implementation plan Phase 6 requires startup steps to invoke existing public producer APIs. Specification ST-G1 requires verified initialization/warmup routes for each enabled signal, rather than assuming an enum entry is a producer.

The default Daily profile in `Domain.Trade.Shared/.../RegimeDiscoveryHorizonConfiguration.cs` requires FifteenMinutes and OneHour observations. The preserved Trend dependency matrix needs EMA20/50/200 and slopes, RSI and slope, ADX/+DI/-DI, MACD histogram and ATR on both required frames. Market Structure adds required inputs at the target evidence frame. Removing optional rows does not eliminate these inputs.

`Application.MarketData/Historical/HistoricalAnalyticsWarmupService.cs` reads valid Daily sessions and invokes `IHistoricalDailyReplayPublisher`. Its registered `Domain.MarketData.Analytics/HistoricalDataLoader/FuturesEmaBbHistoricalDailyReplayPublisher.cs` replays Daily EMA/Bollinger accumulators and publishes an active-contract Daily baseline. This does not prepare the required 15-minute/1-hour families.

The provider contracts include OhlcvOneMinute and Trades. Those data schema capabilities do not by themselves provide an Analytics-domain replay route, restore indicator checkpoints, or publish all required qualified signal observations. Existing live accumulators are not claimed to be absent; the missing capability is their complete historical preparation through the startup producer boundary.

The cache adapter exposes live producers for several families. No spot-VIX publication call was found in `RegimeDiscoverySignalCacheAdapter`; its VixLevel mentions are kind mappings. Spot VIX is optional and can be disabled, unlike the required intraday families. Volatility can use the existing positive VX-front price from the authoritative ITI trigger; no change to that evaluator behavior is proposed.

## Work needed to clear the gate

Supply a bounded Analytics historical preparation API that resolves the configured source, prepares the required timeframe observations, restores/replays the existing calculations, and publishes qualified cache observations with the correct identities. Verify its session/rollover boundaries and duplicate replay behavior. It must return a terminal outcome under deadline/data-cost budgets and must not restart itself because monitoring reports missing data.

Then register those verified capabilities with the parameter-set demand contributors, integrate startup assignment application, and run an end-to-end reduced-profile evaluation. Do not substitute Daily EMA/Bollinger readiness for intraday readiness, drop mandatory inputs, change specialist weights, or mark unsupported routes successful.

## Consequence for this implementation

No runtime activation has been connected. The workflow/horizon assignment and frozen-generation models are implemented and tested, but assignment actors/storage/UI integration, the other lifecycle/query/schema gates, startup execution and monitoring remain incomplete. This historical prerequisite no longer blocks startup under the approved policy. The release remains incomplete; RSI implementation and observation are the next gates.
