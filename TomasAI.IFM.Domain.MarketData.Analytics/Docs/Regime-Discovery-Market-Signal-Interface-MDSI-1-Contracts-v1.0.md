# Regime Discovery Market Signal Interface MDSI-1 Contracts

Shared Identities, Observations, Metadata, and Event Evolution v1.0

| Item | Value |
| --- | --- |
| Gate | `MDSI-1 - Shared identities, observations, metadata, and event evolution` |
| Status | Complete |
| Date | 2026-08-25 |
| Runtime behavior changed | Market-price trade events now carry normalized semantics and exact delivery lineage |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |
| Accepted test result | 2,439 passed, 4 skipped, 0 failed |

## 1. Gate conclusion

MDSI-1 establishes the provider-neutral identity, provenance, observation, and
trade-lineage contracts required by subsequent signal actors. It does not add
a trade-session bar publisher, signal actor, historical provider, cache, schema,
or storage behavior. Those remain owned by later gates.

The gate preserves all existing MessagePack keys. A pre-MDSI-1 five-field
`FuturesMarketTradeSnapshot` remains readable and receives explicit
unknown/default lineage. A current snapshot appends keys 5 through 9 and the
outer `FuturesMarketPriceUpdatedRealtimeEvent` advances to schema version 2.

## 2. Contract ownership

The contracts are divided at a stable dependency boundary:

- `Domain.MarketData.Feed.Shared` owns the normalized live trade snapshot and
  provider-neutral trade enums because Tick Aggregation publishes them;
- `Domain.MarketData.Analytics.Shared` owns market-series identities, signal
  keys and metadata, immutable OHLCV observations, observation identities,
  validation rules, and the closed-observation realtime event; and
- the Databento adapter alone translates Databento wire characters and flags.

The observation contracts use the existing Analytics `TimeFrameType`. Keeping
them in Analytics Shared avoids adding a reverse dependency from Feed Shared
to Analytics Shared while allowing every analytics actor to consume one
coherent observation type.

## 3. Provider-neutral identities

### 3.1 Futures continuation identity

`FuturesSeriesId` is a readonly record struct containing:

| MessagePack key | Property | Meaning |
| ---: | --- | --- |
| 0 | `RootSymbol` | Canonical futures root |
| 1 | `RollRuleId` | Active-contract selection rule |
| 2 | `AdjustmentRuleId` | Cross-contract price-adjustment rule |
| 3 | `Revision` | Continuation-definition revision |

Its format/parse contract is escaped and provider neutral. Validation requires
all three names and a positive revision.

### 3.2 Explicit market-series discriminator

`MarketSeriesIdentity` distinguishes these variants without inspecting a
string pattern:

- `Contract` contains exactly one specific `ContractId`; and
- `FuturesContinuation` contains exactly one `FuturesSeriesId`.

Ambiguous instances containing both payloads and empty instances containing
neither payload are rejected. Long-window continuation observations retain the
actual source `ContractId` separately in their read model and metadata.

### 3.3 Signal and observation identities

`MarketAnalyticsSignalKey` combines series identity, signal kind, timeframe,
and calculation-configuration identity. It prevents different formulas or a
specific contract and a continuation series from overwriting one another.

`FuturesTradeSessionBarId` is deterministic over the exact series,
timeframe, UTC interval end, and last accepted source sequence.
`FuturesTradeSessionBarEntityId` identifies the coordinator route for one
series and timeframe and implements `IActorEntityId` as a readonly record
struct. Format/parse and malformed-input tests qualify both identities.

## 4. Common signal metadata

`MarketAnalyticsSignalMetadata` carries:

- exact signal key and actual source contract;
- value date and source observation identity;
- last exchange event time and local calculation time;
- last source sequence;
- schema, configuration, calculation-version, and calculation-method identity;
  and
- formula validity plus stable validation issue codes.

Warm-up state is intentionally excluded. It belongs to the mutable latest-value
cache introduced by MDSI-15, while historical signal records remain immutable.

## 5. Shared OHLCV observation

`FuturesTradeSessionBarReadModel` is one immutable closed bar with keys 0
through 24. It carries:

- explicit series identity, deterministic observation ID, and actual contract;
- value date, timeframe, and UTC interval boundaries;
- open, high, low, close, volume, trade count, and price-volume sum;
- first/last source sequence and first/last exchange event time;
- calculation time, schema, calculation version, and method; and
- complete, valid, and validation-issue state.

Validation enforces a valid discriminated series, non-empty observation and
contract identities, UTC timestamps, ordered intervals and source lineage,
consistent OHLC values, nonnegative volume/count/sums, version identity, and
the rule that a valid observation must be complete with no issues.

`FuturesTradeSessionBarClosedRealtimeEvent` uses the exact realtime
subject `Realtime.FuturesTradeSessionBar.Closed.{entity}`. Validation
requires its Subject, EntityId, AggregateId, timeframe, series identity, and
observation payload to agree. It is non-durable and has no complete/fail event
family.

## 6. Market-price trade event evolution

The pre-existing trade snapshot keys remain unchanged:

| Key | Existing property |
| ---: | --- |
| 0 | `LastPrice` |
| 1 | `LastSize` |
| 2 | `SourceSequence` |
| 3 | `EventTimestamp` |
| 4 | `ReceiveTimestamp` |

MDSI-1 appends:

| Key | New property |
| ---: | --- |
| 5 | `NormalizedTradeAction` |
| 6 | `NormalizedTradeSide` |
| 7 | `NormalizedTradeConditionFlags` |
| 8 | `StreamEpochId` |
| 9 | `TradeOrdinal` |

Unknown/default values preserve legacy deserialization compatibility. They do
not claim exact VWAP lineage and must not be treated as warm exact input by the
later VWAP actor.

## 7. Databento normalization and lineage

`DatabentoTradeNormalizer` translates provider values once at the adapter
boundary:

- action `T`, `F`, or `A` becomes `New`; `M` becomes `Change`; `C` becomes
  `Cancel`; `R` becomes `Clear`; and `N` becomes `None`;
- aggressor side `B` becomes `Buy`, `A` becomes `Sell`, and `N` becomes
  `Unspecified`; and
- native header plus DBN flags become named snapshot, replay, last-record,
  top-of-book, market-by-price, receive-timestamp, possible-book-error,
  publisher-specific, and undefined-price conditions.

No `EligibleForVwap` flag exists. The future VWAP actor owns eligibility and
correction behavior; Tick Aggregation only preserves normalized source facts.

Each ticker state creates a new `StreamEpochId` when its source stream is
constructed. `TradeOrdinal` resets for each contract value date and increments
only after the live non-replay trade is accepted by the market-price cache.
Stale trades do not consume an ordinal. Therefore the sequence observed in
published market-price events is gap-free unless delivery itself loses an
accepted event.

## 8. Accepted qualification

| Suite | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| Market Data Analytics unit | 884 | 0 | 0 |
| Market Data Feed unit | 488 | 0 | 0 |
| Databento unit and native build | 122 | 0 | 0 |
| Application Market Data unit | 77 | 0 | 0 |
| Market Data Analytics BDD | 462 | 0 | 0 |
| Market Data Feed BDD | 314 | 0 | 0 |
| Market Data Analytics integration | 39 | 0 | 0 |
| Market Data Feed integration | 46 | 4 | 0 |
| Databento integration | 7 | 0 | 0 |
| **Total** | **2,439** | **4** | **0** |

The four Feed integration skips are the pre-existing opt-in tick-insert cases
recorded by MDSI-0. The gate adds focused tests for old/new MessagePack
compatibility, exact keys, identity format/parse, deterministic observation
identity, validation failures, event route consistency, generated XML
documentation, and synthetic Databento mapping.

## 9. MDSI-2 readiness

MDSI-2 can now use `MarketSeriesIdentity`, `FuturesSeriesId`,
`FuturesTradeSessionBarId`, and the observation provenance vocabulary in
provider-neutral historical request/result contracts. It must not expose DBN,
Databento schema codes, batch IDs, Zstandard, native handles, or provider flag
constants outside the Databento implementation.

MDSI-2 does not create live observations or persist OHLCV bars. It delivers the
historical provider/application API, native lifetime boundary, deterministic
offline fixtures, and secure options needed before MDSI-3 performs the first
roll-aware normalized backfill.
