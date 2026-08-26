# Regime Discovery Market Signal Interface MDSI-0 Baseline

Baseline and Migration Inventory v1.0

| Item | Value |
| --- | --- |
| Gate | `MDSI-0 - Baseline and migration inventory` |
| Status | Complete |
| Date | 2026-08-25 |
| Runtime behavior changed | No |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |
| Accepted test result | 2,793 passed, 4 skipped, 0 failed |

## 1. Gate conclusion

MDSI-0 freezes the pre-migration Market Data Feed and Market Data Analytics
surfaces. It adds compatibility tests and records the existing actors,
messages, EOD coupling, timer routes, storage schemas, queries, caches,
Databento boundary, runtime ownership, and accepted tests.

No actor, formula, route, storage operation, schema, or runtime lifecycle was
changed. Later gates must compare their intended changes against this baseline
and must migrate identified callers before deleting a compatibility surface.

## 2. Inventory scope and evidence

The inventory was produced directly from these repository authorities:

- `TomasAI.IFM.Domain.MarketData.Analytics` and `.Shared`;
- `TomasAI.IFM.Domain.MarketData.Feed` and `.Shared`;
- `TomasAI.IFM.Application.MarketData`;
- `TomasAI.IFM.Framework.MarketData.DataBento` and
  `native/DatabentoFeed.Native`;
- `TomasAI.IFM.Application.Storage/MarketDataDb`;
- the UI Analytics service that currently owns signal activation; and
- the affected unit, BDD, integration, storage, and native test projects.

Generated `bin` and `obj` content was excluded from the source inventory.
The counts below describe the repository at this gate and are not architectural
targets for later gates.

## 3. Current actor inventory

### 3.1 Counts

Market Data Analytics currently contains 28 actor classes:

| Actor type | Count |
| --- | ---: |
| Command | 7 |
| Event | 7 |
| Query | 7 |
| Realtime | 7 |
| Total | 28 |

It also contains seven Command state repositories and six dedicated realtime
projectors. `MarketOutlookSnapshotRealtimeActor` performs its coordinated snapshot
upsert directly and therefore is the seventh realtime actor without a matching
`BaseRealtimeProjector<TActor>` implementation.

### 3.2 Actor family matrix

| Family | Command | Event | Query | Realtime | Current live trigger | Current projection |
| --- | --- | --- | --- | --- | --- | --- |
| RSI | Yes | Yes | Yes | Yes | Per-entity timer sends `FuturesRsiSignalSampledRealtimeEvent` | `FuturesRsiSignalRealtimeProjector` and durable Event projector |
| ATR | Yes | Yes | Yes | Yes | Shared period timer sends `FuturesAtrSignalSampledRealtimeEvent` | `FuturesAtrSignalRealtimeProjector` and durable Event projector |
| ADX | Yes | Yes | Yes | Yes | Shared period timer sends `FuturesAdxSignalSampledRealtimeEvent` | `FuturesAdxSignalRealtimeProjector` and durable Event projector |
| MACD | Yes | Yes | Yes | Yes | Shared period timer sends `FuturesMacdSignalSampledRealtimeEvent` | `FuturesMacdSignalRealtimeProjector` and durable Event projector |
| TDI | Yes | Yes | Yes | Yes | Routed RSI collection event | `FuturesTdiSignalRealtimeProjector` and durable Event projector |
| ITI | Yes | Yes | Yes | Yes | Routed `FuturesMarketPriceUpdatedRealtimeEvent` | `FuturesItiSignalRealtimeProjector` and durable Event projector |
| Trade Signal | Yes | Yes | Yes | No | Durable ITI/TDI orchestration | Command Event projector |
| Market Outlook | No | No | Query is handled by Trade Signal query actor | Yes | Completed component realtime events | Direct `market_outlook_snapshot` upsert |

All actor families use closed generic framework contexts plus their typed
domain context interfaces. This is the constructor/context convention retained
for new MDSI actors.

The durable Event actors in the first six established signal families are an
existing compatibility surface. They are not precedent for adding durable
Event actors to new realtime-only market signals. MDSI-0 does not remove or
refactor them.

## 4. Current message inventory

### 4.1 Shared contract file counts

`TomasAI.IFM.Domain.MarketData.Analytics.Shared` currently contains:

| Contract directory | Files |
| --- | ---: |
| `Commands` | 25 |
| `Events` | 33 |
| `Queries` | 21 |

One source file may define more than one public message, so these are frozen
source-file counts rather than CLR type counts.

### 4.2 Command files

- `ClearFuturesItiSignalHoldTradeCommand`
- `ClearFuturesTradeSignalMDIWatermarkCommand`
- `GenerateFuturesAdxDailySignalCommand`
- `GenerateFuturesAdxSignalCommand`
- `GenerateFuturesAtrDailySignalCommand`
- `GenerateFuturesAtrSignalCommand`
- `GenerateFuturesItiSignalCommand`
- `GenerateFuturesMacdDailySignalCommand`
- `GenerateFuturesMacdSignalCommand`
- `GenerateFuturesRsiDailySignalCommand`
- `GenerateFuturesRsiSignalCommand`
- `GenerateFuturesTdiSignalCommand`
- `GenerateFuturesTradeSignalLLMCommand`
- `SetFuturesItiSignalHoldTradeCommand`
- `StartFuturesAdxSignalCommand`
- `StartFuturesAtrSignalCommand`
- `StartFuturesMacdSignalCommand`
- `StartFuturesRsiSignalCommand`
- `StartFuturesTradeSignalLLMCommand`
- `StopFuturesAdxSignalCommand`
- `StopFuturesAtrSignalCommand`
- `StopFuturesMacdSignalCommand`
- `StopFuturesRsiSignalCommand`
- `StopFuturesTradeSignalLLMCommand`
- `UpdateFuturesTradeSignalCommand`

### 4.3 Event file families

The current event directory contains the following compatibility families:

- ADX generated/daily-generated/started/stopped;
- ATR generated/daily-generated/started/stopped;
- RSI generated, generated collection, daily-generated, daily-generated
  collection, started, and stopped;
- MACD generated/daily-generated/started/stopped;
- TDI generated;
- ITI generated, notification, and hold-trade changed/set/cleared;
- Trade Signal updated/notification, MDI watermark, LLM lifecycle/results,
  and metrics LLM results;
- intraday sampled realtime events for RSI, ATR, ADX, and MACD; and
- Market Outlook component and snapshot events.

### 4.4 Query files

- `GetFuturesAdxDailySignalQuery`
- `GetFuturesAdxSignalQuery`
- `GetFuturesAtrDailySignalQuery`
- `GetFuturesAtrSignalQuery`
- `GetFuturesItiMDIDistributionQuery`
- `GetFuturesItiSignalDataQuery`
- `GetFuturesItiSignalHistoryQuery`
- `GetFuturesItiSignalMDIByTrendQuery`
- `GetFuturesItiSignalMDIQuery`
- `GetFuturesItiSignalQuery`
- `GetFuturesItiTrendDirectionChangedSignalsQuery`
- `GetFuturesMacdDailySignalQuery`
- `GetFuturesMacdSignalQuery`
- `GetFuturesRsiDailySignalQuery`
- `GetFuturesRsiSignalQuery`
- `GetFuturesTdiSignalQuery`
- `GetFuturesTradeSignalBySymbolQuery`
- `GetFuturesTradeSignalIdsQuery`
- `GetFuturesTradeSignalQuery`
- `GetFuturesTrendDirectionFromRSISignalQuery`
- `GetMarketOutlookSnapshotQuery`

### 4.5 MessagePack compatibility anchors

The new MDSI-0 serialization tests freeze these public layouts:

| Contract | Frozen keys |
| --- | --- |
| `FuturesEodDataV2ReadModel` | 0 through 21 |
| `FuturesMarketTradeSnapshot` | 0 through 4 |
| `FuturesMarketPriceUpdatedRealtimeEvent` | 0 through 10 |

MDSI-1 may append keys 5 through 9 to `FuturesMarketTradeSnapshot`; it may not
reuse or reorder keys 0 through 4. Existing outer event keys also remain
append-only.

## 5. Futures EOD derived-field coupling

### 5.1 Current mixed model

`FuturesEodDataV2ReadModel` currently combines raw session data and derived
Analytics output:

| Keys | Current responsibility |
| --- | --- |
| 0-7 | Contract, value date, symbol, OHLC, and volume |
| 8-10 | Daily percentage and standard-deviation values |
| 11-13 | Upper band, mean, and lower band |
| 14-18 | Market/price direction and volatility classifications plus MDI |
| 19 | Calculation window size |
| 20-21 | 50-day and 200-day moving averages |

`FuturesEodDataModel.CreateFuturesEodData` constructs this mixed record through
`BollingerBands`, using historical EOD rows, normal-curve data, and VIX EOD
data. This is the Feed-owned derived calculation that MDSI-4 will cut over.

### 5.2 Runtime caller groups

| Caller group | Derived fields used | Migration implication |
| --- | --- | --- |
| Analytics Trade Signal compute/state/model | Daily percent change, standard deviation, mean, MDI, 50/200 DMA | Move to typed Analytics signals before compatibility removal |
| Trade option algorithms/plans | Daily percent change, standard deviation, mean, market/price direction and volatility | Preserve an assembler or migrate the strategy inputs explicitly |
| Market Data Feed risk-position query | Market/price direction and volatility | Stop Feed from owning derived classifications |
| Trade persistence and commands | Whole EOD record and selected classifications | Version/migrate message and storage consumers before model reduction |
| UI Market Outlook and Iron Condor views/view models | Percentage, bands, classifications, MDI, and moving averages | Continue serving a compatibility view until UI models consume signal results |
| API clients, NATS/REST maps, and Blackboard caches | Whole `FuturesEodDataV2ReadModel` | Do not remove the transport type during the raw-table cutover |

The largest semantic callers are:

- `FuturesTradeSignalCompute`, `FuturesTradeSignalModel`, and
  `FuturesTradeSignalCommandState`;
- `ShortIronCondorTradePlan`, `LongIronCondorTradePlan`, and `TradePlan`;
- `GetFuturesRiskPositionType` and Market Data Feed query extensions;
- `FuturesEodDataUIViewModel`, `MarketOutlookView`, `IronCondorView`, and
  `FundOrderEditorViewModel`; and
- Trade command contracts carrying the complete EOD record.

### 5.3 Query-time moving-average enrichment

`GetFuturesEodData` and `GetLastFuturesEodData` currently query
`GetFuturesEodDataMovingAverages` and then set `FiftyDMA` and
`TwoHundredDMA` on the returned EOD record. The moving-average query contract,
service API, read model, and CQL remain compatibility surfaces until EMA
signals and their consumers are available.

## 6. Existing timer and realtime routes

### 6.1 Timer-owned sampling

RSI uses `FuturesRsiSignalTimer`, a dedicated static concurrent registry.
ATR, ADX, and MACD use typed wrappers over
`PeriodSignalTimerRegistry<TEntityId>`. Each timer currently:

1. starts from the corresponding durable `StartedEvent`;
2. requires an active tick stream;
3. reads the latest normalized trade through
   `IMarketDataApi.TryGetLastTickPrice`;
4. rejects mismatched contract, value date, or asset type;
5. deduplicates by trade source sequence; and
6. sends a signal-specific sampled realtime event to its realtime actor.

The authoritative UI activation profile starts RSI13, ATR14, ADX14, and MACD
for each of these six timeframes:

- 15 seconds;
- 1 minute;
- 5 minutes;
- 15 minutes;
- 1 hour; and
- 4 hours.

The timer registry also defines intervals for additional timeframe enum values,
including Daily, Weekly, and Monthly. Those definitions are not evidence of a
shared OHLCV observation or a complete scheduled Daily pipeline.

### 6.2 Non-timer realtime routing

- `FuturesItiSignalRealtimeActor` registers and releases the market-price
  update route during actor startup and shutdown.
- `FuturesTdiSignalRealtimeActor` registers and releases its RSI collection
  route during actor startup and shutdown.
- RSI, ATR, ADX, MACD, and ITI realtime projectors start with their realtime
  actors and perform the existing one-attempt projection contract.

### 6.3 Current lifecycle owner

`TomasAI.IFM.UI.Net.Services/Analytics/MarketDataAnalyticsCommandService`
currently starts and stops the four timer-based signal families for all six
configured timeframes. The UI therefore owns an important part of Analytics
activation today. Server-owned, headless activation is a later migration and
must not be inferred to exist at this baseline.

## 7. Current cache and state surfaces

The system does not yet contain the unified latest-signal cache required by
Regime Discovery.

Existing hot/state surfaces are:

- the application `IMarketDataApi` last-price and session-statistics readers;
- Feed TickAggregation last trade/quote/session accumulators;
- timer source-sequence state in the RSI and shared period registries;
- per-realtime-actor dictionaries for RSI, ATR, ADX, MACD, and TDI calculation
  windows/current values;
- ITI realtime state and durable timeframe state; and
- Blackboard EOD current/range caches for UI/application consumption.

These stores have different identities, lifetimes, and consistency semantics.
They must not be presented as an atomic Regime Discovery snapshot.

## 8. Current ScyllaDB schema and query inventory

### 8.1 Feed/EOD tables

- `futures_eod_data`
- `futures_eod_data_by_month`
- `futures_eod_data_index`
- `futures_intra_day_data`
- `vix_futures_eod_data`
- `vix_futures_contract_index`
- `futures_tick_data`
- `futures_tick_data_by_time`
- `tick_trade_data`
- `tick_quote_data`

The EOD schema registry applies separate 50-DMA and 200-DMA column additions
to both canonical and by-month EOD tables.

### 8.2 Analytics tables

- `futures_rsi_signal`
- `futures_atr_signal`
- `futures_adx_signal`
- `futures_macd_signal`
- `futures_macd_signal_v2`
- `futures_tdi_signal`
- `futures_traders_dynamic_index_signal`
- `futures_iti_signal`
- `futures_iti_signal_by_contract_day`
- `futures_iti_signal_by_contract_month`
- `futures_iti_signal_by_trend_mode_month`
- `futures_iti_timeframe_state`
- `futures_trade_signal`
- `futures_trade_signal_lookup_by_scope`
- `futures_trade_signal_quarantine`
- `market_outlook_snapshot`

The active MACD CQL writes and reads `futures_macd_signal_v2`. The registry
still contains both MACD table definitions. TDI similarly retains both
`futures_tdi_signal` and `futures_traders_dynamic_index_signal`, while current
insert/read CQL uses the latter. These are recorded development-schema cleanup
targets; MDSI-0 does not drop them.

### 8.3 Storage implementation surface

Current read/write behavior is concentrated in:

- `IMarketDataDbReadContext`;
- `IMarketDataDbWriteContext`;
- `MarketDataDbContext` and its partial files;
- `MarketDataDbCql`;
- `MarketDataDbParameters`;
- `MarketDataSchemaCql`; and
- the ordered `MarketDataSchemaDb` registry.

The later schema gates must preserve query-first partitions, named command
text, cancellation-aware APIs, and idempotent projection behavior.

Existing `_v2` and `_v3` tables are baseline legacy facts, not permission to
give new Market Signal tables `_vX` names.

## 9. Current Databento boundary

`IMarketDataApi` currently has 23 methods covering:

- current contract resolution and reconciliation;
- normalized last-price/session-statistics access;
- live epoch start/stop;
- futures and option definition queries;
- normalized price queries; and
- futures, option, and option-chain stream ownership.

The interface contains no Historical acquisition, cost estimation, provider
batch job, download, or archive method, and it exposes no Databento type. The
new application approval test freezes that separation.

The current Databento framework can perform live processing, replay/recovery,
definition hydration, current contract lookup, and option-chain definition
queries through its pinned native boundary. It does not provide the
provider-neutral bulk OHLCV/trade Historical API required by MDSI-2.

## 10. Migration dependency map

| Baseline surface | First modifying gate | Required protection |
| --- | --- | --- |
| Trade snapshot keys 0-4 and price event keys 0-10 | MDSI-1 | Append-only MessagePack tests |
| Live-only `IMarketDataApi` | MDSI-2 | Add a separate Historical boundary; retain provider neutrality |
| Current roll/current-contract resolution | MDSI-3 | Add historical roll identity without changing live semantics |
| Mixed EOD model/tables and derived callers | MDSI-4 | Raw cutover plus explicit compatibility assembler |
| Four independent timer routes | MDSI-5 and MDSI-6 | Run old/new paths under controlled activation; prevent duplicate observations |
| RSI13 used by TDI | MDSI-7 | Identity/configuration isolation from RSI14 |
| Moving-average EOD query and DMA callers | MDSI-8 | Migrate to EMA signals before removal |
| Existing Feed Bollinger calculation | MDSI-9 | Replace only after BB signal projection and consumers exist |
| ATR read model/table | MDSI-10 | Append/version required ratio and baseline semantics |
| VIX/VX EOD inputs | MDSI-12 | Preserve historical contract identity and atomic rollover |
| Market-price trade snapshot | MDSI-13 | Exact normalized trade lineage and gap recovery |
| Disjoint hot/state stores | MDSI-15 | Unified bounded cache with explicit revision/health |
| Direct caller reads | MDSI-16 | Atomic snapshot provider; no hot-path storage/provider reads |
| Duplicate/legacy development tables | MDSI-17 | Remove only after new read/write/query tests pass |
| UI-owned activation | MDSI-18 | Headless server lifecycle qualification before UI dependency removal |

## 11. MDSI-0 compatibility tests added

`FuturesEodDataBaselineContractTests` adds four behavior-neutral tests:

1. exact EOD MessagePack keys 0-21;
2. round-trip preservation of raw and derived EOD values;
3. exact trade snapshot keys 0-4; and
4. exact market-price updated event keys 0-10.

`MarketDataApiContractApprovalTests` now also proves the existing live
application boundary has no Historical acquisition operation or Databento
parameter type.

These tests express migration rules, not a requirement to preserve the mixed
EOD architecture permanently. A later gate may introduce a new raw model and a
compatibility assembler while the existing serialized contract remains valid.

## 12. Accepted baseline tests

All tests were executed on 2026-08-25 from `C:\repos\IFM` against the current
development infrastructure.

| Suite | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| `TomasAI.IFM.Application.MarketData.UnitTests` | 77 | 0 | 0 |
| `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests` | 120 | 0 | 0 |
| Native `databento_feed_native_tests` | 1 | 0 | 0 |
| `TomasAI.IFM.Domain.MarketData.Feed.UnitTests` | 486 | 0 | 0 |
| `TomasAI.IFM.Domain.MarketData.Feed.BDDTests` | 314 | 0 | 0 |
| `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests` | 46 | 4 | 0 |
| `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests` | 876 | 0 | 0 |
| `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests` | 462 | 0 | 0 |
| `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests` | 39 | 0 | 0 |
| `TomasAI.IFM.Application.Storage.IntegrationTests` | 372 | 0 | 0 |
| **Total** | **2,793** | **4** | **0** |

The four Feed integration skips are pre-existing explicitly skipped tick and
option-tick insertion cases. They are recorded, not silently counted as passes.

An initial parallel invocation caused a compiler output file-lock error while
two test projects compiled the same Analytics assembly. The affected Analytics
unit suite was rerun sequentially and passed all 876 tests. This was runner
contention rather than a product test failure and is not part of the accepted
failure count.

The native CTest executable was not on `PATH`; the Visual Studio CMake `ctest`
executable was invoked by absolute path and passed the registered native test.

## 13. Gate checklist

- [x] Freeze actor inventory.
- [x] Freeze Command/Event/Query inventory.
- [x] Freeze relevant Scylla schema and query inventory.
- [x] Characterize mixed EOD derived fields and runtime callers.
- [x] Characterize old RSI/ATR/ADX/MACD timer routes.
- [x] Record disjoint hot-cache/state surfaces.
- [x] Record current Databento Historical gap.
- [x] Add baseline serialization tests.
- [x] Add application boundary approval test.
- [x] Record and pass all affected baseline suites.
- [x] Confirm no runtime behavior change.

MDSI-0 is complete. The next implementation gate is `MDSI-1 - Shared
identities, observations, metadata, and event evolution`.
