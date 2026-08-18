# Domain Actor API Implementations

## Purpose

The domain `Actor*Api` implementations provide typed boundaries for communication between domain actors. They replace call-site extension methods with injectable contracts while preserving the required execution model:

- Query APIs execute in process against storage, Blackboard, or an approved snapshot service. They do not use HTTP, NATS, or actor messaging.
- Command APIs construct domain commands and use the calling actor's `IEventActorContext` for NATS request/reply messaging.
- Event APIs construct and send domain events through the calling actor's `IEventActorContext`.

The actor depends on an interface from the domain's `Shared/ServiceApi` folder. It does not depend directly on a concrete API class.

## Lifetime and construction

Query APIs are registered as singletons because their dependencies are application services and storage abstractions. Command and event APIs cannot be registered as context-free singletons because `IEventActorContext` exists only for a running event actor. Their factories are singletons; each actor creates and caches one context-bound API during actor startup.

| API kind | Concrete lifetime | Construction | Transport | Result behavior |
| --- | --- | --- | --- | --- |
| Query | Application singleton | Dependency injection | Direct/in-process | `ServiceOk<T>` or query-specific `ServiceFailed<T>` |
| Command | One instance per event-actor context | Singleton `IActor*CommandApiFactory` | NATS request/reply | Typed `ServiceResult<GuidResult>` from the command reply |
| Event | One instance per event-actor context | Singleton `IActor*EventApiFactory` | NATS send | Awaitable send; complete/fail events preserve correlation metadata |

## Error and correlation rules

1. Every query method owns its `try/catch`; shared async execution delegates are not used.
2. A successful query returns `ServiceOk<T>` using the contract's result type.
3. A failed query returns `ServiceFailed<T>(QueryType.ErrorId, ex.Message)`.
4. Command APIs populate the command subject, entity ID, and command error code before request/reply dispatch.
5. Event APIs derive complete/fail events from the source event or explicitly copy its command, aggregate, entity, and subject information.
6. Context-bound command and event API instances must not be shared between actors.

## Implementation catalog

### Fund

#### `ActorFundQueryApi`

- Contract: `IActorFundQueryApi`
- Source: `TomasAI.IFM.Domain.Fund/Query/Api/ActorFundQueryApi.cs`
- Dependencies: `IDbContextFactory`
- Execution: direct Fund storage access
- Operations: funds, fund orders and trades, transactions, opening/closing/current balances, P&L report, order-to-fund lookup, win/loss ratio, drawdown balances, and maximum-profit generation inputs
- Notes: report calculations and Sharpe-ratio calculation remain in process; each public query returns its own typed service result.

#### `ActorFundEventApi`

- Contract: `IActorFundEventApi`
- Factory: `ActorFundEventApiFactory`
- Source: `TomasAI.IFM.Domain.Fund/Event/Api/ActorFundEventApi.cs`
- Dependencies: actor-owned `IEventActorContext`
- Execution: NATS event send
- Operations: send completion or failure for `FundMaxProfitGeneratedEvent`
- Notes: complete/fail conversion preserves the originating event correlation and `FundId` entity identity.

### Market Data

#### `ActorMarketDataQueryApi`

- Contract: `IActorMarketDataQueryApi`
- Source: `TomasAI.IFM.Domain.MarketData/Query/Api/ActorMarketDataQueryApi.cs`
- Dependencies: `IDbContextFactory`
- Execution: direct Securities and Market Data storage access, plus the external yield-curve reader when configured
- Operations: current and historical futures contracts, futures-option contracts and IDs, yield-curve data, rate of return, trading dates/days, value date, and aggregated iron-condor market data
- Notes: `GetValueDateAsync` is synchronous internally but keeps the asynchronous service contract. External yield-curve queries return an empty successful result when the optional reader is unavailable.

### Market Data Analytics

#### `ActorMarketDataAnalyticsQueryApi`

- Contract: `IActorMarketDataAnalyticsQueryApi`
- Source: `TomasAI.IFM.Domain.MarketData.Analytics/Query/Api/ActorMarketDataAnalyticsQueryApi.cs`
- Dependencies: `IDbContextFactory`
- Execution: direct Market Data storage access
- Operations: trade signals; RSI, TDI, ITI, ATR, ADX, and MACD signals; ITI signal data; trend changes; and MDI distributions
- Notes: ITI MDI-by-trend queries combine up-trend and down-trend storage results before returning the typed result.

#### `ActorMarketDataAnalyticsCommandApi`

- Contract: `IActorMarketDataAnalyticsCommandApi`
- Factory: `ActorMarketDataAnalyticsCommandApiFactory`
- Source: `TomasAI.IFM.Domain.MarketData.Analytics/Command/Api/ActorMarketDataAnalyticsCommandApi.cs`
- Dependencies: actor-owned `IEventActorContext`
- Execution: NATS command request/reply
- Operations: generate RSI, TDI, MACD, ADX, ATR, and ITI signals and update the combined futures trade signal
- Notes: each method constructs the command subject and entity ID before dispatch and converts the actor reply into `ServiceResult<GuidResult>`.

### Market Data Feed

#### `ActorMarketDataFeedQueryApi`

- Contract: `IActorMarketDataFeedQueryApi`
- Source: `TomasAI.IFM.Domain.MarketData.Feed/Query/Api/ActorMarketDataFeedQueryApi.cs`
- Dependencies: `IDbContextFactory`, application-level `IMarketDataApi`, and `IBlackboardService`
- Execution: direct Market Data storage access, serialized broker snapshots, and in-process Blackboard sequence allocation
- Operations: futures and option ticks, EOD and bar data, moving averages, VX EOD data, iron-condor feed data, EOD parameters, broker option contracts/spreads, normal-curve data, risk-position classification, and streaming/quote IDs
- Notes: broker snapshot calls are serialized by a `SemaphoreSlim`. The semaphore is always released in `finally`. Sequence IDs are allocated synchronously but returned through the asynchronous service contract.

#### `ActorMarketDataFeedCommandApi`

- Contract: `IActorMarketDataFeedCommandApi`
- Factory: `ActorMarketDataFeedCommandApiFactory`
- Source: `TomasAI.IFM.Domain.MarketData.Feed/Command/Api/ActorMarketDataFeedCommandApi.cs`
- Dependencies: actor-owned `IEventActorContext`
- Execution: NATS command request/reply
- Operations: live-feed on/off, start/stop bar and tick streams, insert bar/tick/option/EOD/VX data, delete streaming request IDs, and insert option quotes
- Notes: callers receive the command actor's typed `GuidResult`; command and entity correlation are created before dispatch.

#### `ActorMarketDataFeedEventApi`

- Contract: `IActorMarketDataFeedEventApi`
- Factory: `ActorMarketDataFeedEventApiFactory`
- Source: `TomasAI.IFM.Domain.MarketData.Feed/Event/Api/ActorMarketDataFeedEventApi.cs`
- Dependencies: actor-owned `IEventActorContext`
- Execution: NATS event send
- Operations: bar/tick/option-stream complete/fail events, option-trade tick-price updates, feed reset/start/stop events, live-feed failures, quote updates, and EOD updates
- Notes: generic internal complete/fail helpers perform event conversion only; public operations remain strongly typed. Custom update events explicitly set actor subject and correlation fields.

### Option Pricer

#### `ActorOptionPricerQueryApi`

- Contract: `IActorOptionPricerQueryApi`
- Source: `TomasAI.IFM.Domain.OptionPricer/Query/Api/ActorOptionPricerQueryApi.cs`
- Dependencies: `IDbContextFactory`
- Execution: direct Option Pricer storage access
- Operations: available pricing devices, spread distribution, and spread-distribution job-in-progress status

#### `ActorOptionPricerCommandApi`

- Contract: `IActorOptionPricerCommandApi`
- Factory: `ActorOptionPricerCommandApiFactory`
- Source: `TomasAI.IFM.Domain.OptionPricer/Command/Api/ActorOptionPricerCommandApi.cs`
- Dependencies: actor-owned `IEventActorContext`
- Execution: NATS command request/reply
- Operations: submit, complete, and fail spread-distribution jobs

### Reference

#### `ActorReferenceQueryApi`

- Contract: `IActorReferenceQueryApi`
- Source: `TomasAI.IFM.Domain.Reference/Query/Api/ActorReferenceQueryApi.cs`
- Dependencies: `IDbContextFactory`
- Execution: direct Reference storage access, plus the external economic-calendar reader when configured
- Operations: lookup collections/names/short codes, seed IDs, futures defaults, option strike definitions, economic calendars and country codes, calendar dates, and MDI forward-loss ratios
- Notes: external calendar queries return an empty successful result when the optional reader is unavailable. Convenience lookup methods share only raw lookup construction; each public method owns its typed error handling.

### System Administration

#### `ActorSystemAdminQueryApi`

- Contract: `IActorSystemAdminQueryApi`
- Source: `TomasAI.IFM.Domain.SystemAdmin/Query/Api/ActorSystemAdminQueryApi.cs`
- Dependencies: none; reads `SystemAdminQueryState`
- Execution: direct/in-process
- Operations: retrieve configured database names

### Trade

#### `ActorTradeQueryApi`

- Contract: `IActorTradeQueryApi`
- Source: `TomasAI.IFM.Domain.Trade/Query/Api/ActorTradeQueryApi.cs`
- Dependencies: `IDbContextFactory` and `IBlackboardService`
- Execution: direct Trade storage and Blackboard access
- Operations: history, option-leg IDs, trade/type limits, quantity, option trades, spread/bar data, positions and position types, iron-condor price, and MDI limits
- Notes: `GetTradePlanSummaryAsync` intentionally throws `NotImplementedException` until the obsolete UI contract is removed.

#### `ActorTradeCommandApi`

- Contract: `IActorTradeCommandApi`
- Factory: `ActorTradeCommandApiFactory`
- Source: `TomasAI.IFM.Domain.Trade/Command/Api/ActorTradeCommandApi.cs`
- Dependencies: actor-owned `IEventActorContext`
- Execution: NATS command request/reply
- Operations: change option-leg data, update spread-distribution statistics, and change computed spread-distribution statistics

## Dependency-injection registration

The API Server and actor integration-test startup register:

- each `IActor*QueryApi` directly as a singleton;
- each `IActor*CommandApiFactory` as a singleton; and
- each `IActor*EventApiFactory` as a singleton.

Do not register context-bound command or event implementations themselves as singletons. The concrete instance captures `IEventActorContext` and therefore belongs to exactly one running actor context.

## Adding another actor-only API

1. Put the contract in the domain's `Shared/ServiceApi` folder.
2. Put the concrete implementation in `Query/Api`, `Command/Api`, or `Event/Api`.
3. For queries, inject application dependencies and return explicit typed `ServiceOk<T>`/`ServiceFailed<T>` values in every public method.
4. For commands or events, inject `IEventActorContext`, add a factory contract and concrete factory, and create the API during actor startup.
5. Register the query API or factory in API Server and actor integration-test DI.
6. Add focused tests for success, failure/error-code mapping, subject/entity routing, and context binding.
7. Confirm the actor depends only on the shared interface, not the concrete implementation.
