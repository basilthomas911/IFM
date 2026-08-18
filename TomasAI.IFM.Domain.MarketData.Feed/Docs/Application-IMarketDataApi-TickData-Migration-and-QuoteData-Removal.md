# Application `IMarketDataApi` tick-data migration and option-quote removal specification

**Version:** 1.2
**Status:** Proposed implementation specification; no production migration has been performed by this document
**Date:** 2026-08-10
**Primary provider:** Databento
**Affected assets:** Futures and futures options
**Revision 1.1:** Adds the required unit, hosted integration, bounded native,
live smoke, UI, storage, composition-root, and removal-gate test suites.
**Revision 1.2:** Makes complete legacy feed/snapshot API removal mandatory and
adds every remaining broker contract-detail, spread snapshot, transport, DI,
configuration, provider, caller, and test migration found by the source audit.

## 1. Purpose

This specification defines the complete migration of `FuturesTickData` and
`FuturesOptionTickData` from the legacy market-data interfaces to
`TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi`.

It also defines how live futures-option quote events from
`FuturesOptionTickData` replace the separate `FuturesOptionQuoteData` bounded
context. After every consumer has been migrated and the acceptance gates pass,
`FuturesOptionQuoteData` is removed completely from active code, API routes,
storage, configuration, tests, and UI wiring.

This is an implementation plan, not authorization to delete production data.
The ScyllaDB table-removal migration requires a separately reviewed retention
and backup decision.

## 2. Binding decisions

1. The only application-facing market-data contract is
   `TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi`.
2. Provider request IDs, Databento instrument IDs, and Interactive Brokers
   streaming IDs do not cross that application boundary. Start and stop calls
   use canonical domain contract IDs.
3. `TickAggregationService` processes both futures and futures-option quotes
   and trades.
4. `TickAggregationService` and its aggregation actors are the only raw-tick
   persistence path. A live-event consumer must never insert the same tick.
5. The futures-option live quote emitted by `FuturesOptionTickData` replaces
   `FuturesOptionQuoteDataUpdatedEvent` and its quote-specific stream.
6. Live quote and trade events remain separate because consumers have different
   latency and payload requirements.
7. A futures-option stream cannot start unless its underlying futures contract
   belongs to the running tick-aggregation epoch and is active.
8. A returned `false` from a start or stop route method is an idempotent outcome,
   not a failure: it means already started or already stopped.
9. Option Greeks are carried only when an exact-sequence enriched snapshot is
   available. Missing or invalid Greeks remain explicit and are never encoded
   as numeric zeroes.
10. Risk-free-rate-dependent, valid Black-76 Greeks remain deferred until the
    treasury-curve provider is available. That does not block the raw tick and
    quote migration.
11. No L1, Redis, or Blackboard cache is introduced for hot prices in this
    migration. The epoch-bound last-price readers are the authoritative hot
    values.
12. `FuturesOptionQuoteData` is deleted only after its final runtime consumer has
    been moved and the zero-reference gates pass.
13. The legacy `TomasAI.IFM.Domain.MarketData.Feed.Shared.IMarketDataApi`, its
    derived snapshot interface, option interfaces, stream-ID collection, and IB
    implementations are removed completely from active source. They are not
    retained as compatibility adapters.
14. The two broker-backed MarketDataFeed queries for an option definition and
    an option spread are migrated or replaced before the old interfaces are
    deleted. A missing FMP treasury rate may make Greeks unavailable, but it
    does not permit a fallback to the legacy API.
15. `IMarketDataQueryApi` and its `SecuritiesDb` contract catalog remain. They
    provide stored contract master data, editor/discovery queries, and off-epoch
    reads and are not the legacy provider API.
16. After the final cutover, production startup has no Interactive Brokers
    market-data feed or snapshot registration, connection setting, API route,
    NATS subject, project reference, or provider callback reachable from the
    market-data application.

## 3. Scope

### 3.1 Included

- Market-data epoch ownership in `MarketDataFeedEventActor`.
- Futures and futures-option stream command migration.
- Contract-ID-based route activation and deactivation.
- Mapping transient aggregation events to domain/UI events.
- Replacement of the Iron Condor order UI quote stream.
- Removal of duplicate legacy tick inserts.
- Preservation of required downstream tick side effects.
- Migration of remaining legacy contract-definition queries needed to remove
  the old snapshot interface.
- Replacement of the legacy option-spread snapshot query and its UI callers.
- Removal of both legacy IB market-data implementations, their DI/configuration,
  transport contracts, API routes, project references, and tests.
- Complete source removal of `FuturesOptionQuoteData`.
- Unit, integration, live-native, smoke, and benchmark acceptance gates.

### 3.2 Excluded

- A rename or redesign of `IronCondorSpreadDistributionJobService`.
- New spread-strategy calculations.
- FMP treasury-curve implementation or production Black-76 enrichment.
- Redis or general Blackboard caching changes.
- Stored contract catalog queries and contract editor workflows backed by
  `SecuritiesDb`.
- Historical migration from legacy option quote/tick tables into aggregation
  tables.
- Immediate destructive removal of production ScyllaDB tables.

## 4. Current-state problems

### 4.1 Futures ticks

`FuturesTickDataEventActor` uses the legacy
`TomasAI.IFM.Domain.MarketData.Feed.Shared.IMarketDataApi`, creates a numeric
streaming request ID, stores stream state in Blackboard, and handles
`FuturesTickBidAsk` by calling a legacy insert API.

Once the Databento aggregation epoch is active, that insert is a second
persistence path for the same market observation. It must be removed.

### 4.2 Futures-option ticks

`FuturesOptionTickDataEventActor` currently depends on both the legacy
`IMarketDataApi` and legacy `IMarketDataSnapshotApi`. It starts a provider stream
with a numeric request ID and accepts option-pricing inputs at stream-start
time. Its bid/ask handler calculates legacy Greeks, inserts option tick rows,
updates Blackboard, and produces trading/UI side effects.

The new Databento implementation already maintains raw and enriched hot-price
snapshots. Provider orchestration and legacy insert behavior must be removed
from the domain actor.

### 4.3 Futures option quotes

`FuturesOptionQuoteData` is a parallel quote-only pipeline. Its active UI path
allocates a quote ID and request IDs, starts a separate provider quote stream,
persists quote rows, and then returns `FuturesOptionQuoteDataUpdatedEvent` to the
UI. The active consumer only needs the option contract ID, bid, ask, and sizes.

Those fields already originate in the futures-option quote records processed by
`TickAggregationService`. Maintaining a separate quote connection, identifier,
actor hierarchy, API surface, and storage schema is unnecessary.

The 2026-08-10 source audit found the `FuturesOptionQuoteData` symbol in 73 C#
source files with 1,097 occurrences after excluding `bin` and `obj`. This is a
cross-solution migration, not only a deletion of the domain folder.

## 5. Target architecture

```text
Databento live feed
        |
        v
TickAggregationService
   |                         |
   | durable                 | transient, only for active routes
   v                         v
aggregation changed       ITickLiveEventPublisher
events                       |
   |                         v
   v                    ITickLiveEventSink
aggregation actors           |
   |                         +--> futures quote/trade domain events
   v                         +--> option quote/trade domain events
ScyllaDB tick tables                     |
                                        +--> UI / spread engine / trading
```

The two branches have deliberately different responsibilities:

- The durable branch provides ordered, idempotent persistence and durable
  inserted/completed events.
- The transient branch provides low-latency, non-event-sourced delivery to live
  consumers. It performs no raw-tick inserts.

The physical Databento feed is epoch-scoped. The individual stream methods on
`IMarketDataApi` activate or deactivate transient delivery for a canonical
contract; they do not create a second physical provider connection and do not
control persistence.

## 6. Application API usage

The existing application contract has all methods required by this migration.
No new method is required before implementation begins.

| Domain operation | `IMarketDataApi` method |
|---|---|
| Start market-data epoch | `StartAsync(valueDate, errorHandler, cancellationToken)` |
| Stop market-data epoch | `StopAsync(valueDate)` |
| Resolve futures contract | `GetFuturesContractAsync(futuresContractId)` |
| Resolve futures options | `GetFuturesOptionContractAsync` / `GetFuturesOptionContractsAsync` |
| Get complete option chain | `GetFuturesOptionChainContractsAsync(futuresContractId, maturityDate)` |
| Read latest futures price | `GetFuturesPriceAsync` / `GetFuturesLastPriceReader` |
| Read latest option price | `GetFuturesOptionPriceAsync` / `GetFuturesOptionLastPriceReader` |
| Activate futures live events | `StartStreamingFuturesTickDataAsync(futuresContractId)` |
| Deactivate futures live events | `StopStreamingFuturesTickDataAsync(futuresContractId)` |
| Activate option live events | `StartStreamingFuturesOptionTickDataAsync(futuresOptionContractId)` |
| Deactivate option live events | `StopStreamingFuturesOptionTickDataAsync(futuresOptionContractId)` |
| Activate a filtered chain | `StartStreamingFuturesOptionChainDataAsync` |
| Deactivate a chain | `StopStreamingFuturesOptionChainDataAsync` |

The application API implementation may use multiple bounded Databento query
clients and live transports internally. This remains an implementation detail;
consumers receive one coherent API and one epoch lifecycle.

## 7. Command contract migration

The domain start/stop commands must become provider-neutral as part of the same
change. Use the semantically specific property names `FuturesContractId` and
`FuturesOptionContractId`; do not add another generic `ContractId` or a numeric
stream/request identifier.

| Command | Target domain payload |
|---|---|
| `StartFuturesTickDataStreamingCommand` | `FuturesContractId`, `ValueDate`, and domain reset intent if reset remains supported |
| `StopFuturesTickDataStreamingCommand` | `FuturesContractId`, `ValueDate` |
| `StartFuturesOptionTickDataStreamingCommand` | `FuturesOptionContractId`, `ValueDate` |
| `StopFuturesOptionTickDataStreamingCommand` | `FuturesOptionContractId`, `ValueDate` or the existing entity ID containing it |

Remove full provider-oriented contract payloads, `BaseContract`,
`MaturityDate`, and `RiskFreeRate` from the option start command. The actor
resolves current contract definitions through `IMarketDataApi`; maturity and
underlying are facts of that definition, while a risk-free rate is a pricing
input rather than a stream-control input.

Update the corresponding command parameters, command-state events, constructors,
NATS/API serializers, UI command producers, fixtures, and tests together.
MessagePack keys must never be silently repurposed. Introduce a new command
version or append compatible fields and explicitly retire the old version after
all producers and consumers are deployed.

## 8. New live domain event contracts

Add provider-neutral contracts to
`TomasAI.IFM.Domain.MarketData.Feed.Shared`. If the events cross NATS, annotate
their payloads consistently with the repository's MessagePack conventions.

The recommended event names are:

- `FuturesTickQuoteUpdatedEvent`
- `FuturesTickTradeUpdatedEvent`
- `FuturesOptionTickQuoteUpdatedEvent`
- `FuturesOptionTickTradeUpdatedEvent`

Each event must have a stable `EventId`. Each payload must contain:

- canonical `ContractId`;
- `ValueDate`;
- provider source sequence;
- exchange event timestamp;
- local receive timestamp;
- the raw provider-neutral quote or trade values.

A quote payload contains nullable bid and ask prices plus sizes and counts. A
trade payload contains price and size. The option variants also identify the
underlying futures contract when it is known from the registered contract
definition.

The option variants may include an application-owned Greeks value object with:

- `IsAvailable`;
- `IsValid`;
- nullable implied volatility and Greek values;
- a nullable failure reason;
- the exact source sequence used by the calculation.

The mapper may attach an enriched option snapshot only when its source sequence
equals the raw quote or trade source sequence being published. It must not join
the latest tick to Greeks calculated for a different observation.

Do not make Domain Shared depend on Framework MarketData types. Define the
serializable event payloads in Domain Shared, then map
`LastQuoteTickSnapshot`, `LastTradeTickSnapshot`, and
`OptionGreeksSnapshot` at the application/host integration boundary.

The currently unused `FuturesOptionTickDataUpdatedEvent` should either be
replaced with the explicit quote/trade events above or versioned to the same
semantics. It must not remain as a second ambiguous public tick event.

## 9. Live-event bridge

Implement an `ITickLiveEventSink` adapter, named for example
`ActorTickLiveEventSink`, in the application integration/host layer. Its only
responsibilities are:

1. Inspect `AssetTypeId` on `LiveTickQuoteServiceEvent` or
   `LiveTickTradeServiceEvent`.
2. Map the common aggregation payload to the corresponding domain event.
3. For an option, obtain the epoch-bound reader through
   `IMarketDataApi.GetFuturesOptionLastPriceReader(contractId)` and attach
   enriched state only when the sequence matches.
4. Publish the mapped event through the existing actor/event-router transport.
5. Propagate cancellation, backpressure, and sink faults visibly.

The adapter must not:

- insert a futures or option tick;
- update legacy Blackboard tick or quote caches;
- calculate Greeks independently;
- allocate a provider request ID;
- start or stop a provider connection;
- swallow an unknown asset type or mapping failure.

Register `BoundedTickLiveEventPublisher` as `ITickLiveEventPublisher` and the
adapter as `ITickLiveEventSink`. The production container must not resolve the
null publisher. If the Databento registration uses `TryAdd`, register the real
publisher before it or explicitly replace the default afterward.

## 10. Market-data epoch lifecycle

`MarketDataFeedEventActor` owns the application API epoch:

### Start

1. Receive the feed-start event and its `ValueDate`.
2. Validate that every configured futures and option contract uses a canonical
   domain contract ID and has a Databento registration.
3. Call `IMarketDataApi.StartAsync` once for the epoch.
4. Route its provider error callback into the existing error event/log path.
5. Publish feed-start completion only after startup succeeds.

### Stop

1. Prevent new route-start commands.
2. Stop active domain routes or allow the epoch stop to clear the router.
3. Call `IMarketDataApi.StopAsync(valueDate)`.
4. Publish completion only after buffers are flushed and the epoch is stopped.

### Reset

Reset is an awaited stop followed by an awaited start. Remove the fixed delay
currently used by the legacy implementation.

Tick actors may call `StartAsync` as an idempotent epoch assertion before route
activation. They must never call `StopAsync`; stopping one route must not stop
the shared feed.

## 11. `FuturesTickData` migration

Change `FuturesTickDataEventActor` to inject the application-layer API, using a
type alias during the transition if both legacy interfaces are still compiled.

### Start command

1. Validate `ValueDate` and canonical `futuresContractId`.
2. Assert the matching epoch is running.
3. Call `StartStreamingFuturesTickDataAsync(futuresContractId)`.
4. Treat `true` and `false` as successful idempotent outcomes.
5. Publish the existing started-complete event after route activation.

Remove numeric stream-ID allocation and the provider streaming request from the
command state. If Blackboard still records domain UI status temporarily, store
only domain state and never use it as the routing authority.

### Stop command

Call `StopStreamingFuturesTickDataAsync(futuresContractId)`. Do not stop the
epoch and do not require a numeric request ID.

### Tick events

Replace the `FuturesTickBidAsk` insert handler with consumers of
`FuturesTickQuoteUpdatedEvent` and `FuturesTickTradeUpdatedEvent`. Live
consumers use those transient events directly.

Delete the call to `InsertFuturesTickDataAsync`. Historical reads should move to
the aggregation storage/query model, while current-price reads use
`GetFuturesLastPriceReader`.

### Existing side effects

The existing `FuturesTickDataInserted` path triggers futures EOD/VX behavior.
Preserve transaction-dependent behavior with a listener on the durable
aggregation inserted or insert-complete trade event filtered to
`AssetTypeId.Futures`. Map the aggregation trade to the existing downstream
input until those downstream contracts are independently modernized.

Do not trigger the same side effect from both the transient and durable paths.

## 12. `FuturesOptionTickData` migration

Change `FuturesOptionTickDataEventActor` to inject only the application
`IMarketDataApi` for market-data access. Remove its dependency on the legacy
snapshot interface.

### Start command

1. Validate the canonical option contract ID and `ValueDate`.
2. Resolve the option definition with
   `GetFuturesOptionContractAsync(futuresOptionContractId)`.
3. Obtain its underlying canonical futures contract ID.
4. Call
   `StartStreamingFuturesOptionTickDataAsync(futuresOptionContractId)`.
5. Publish started completion for either idempotent result.

The Databento implementation of
`StartStreamingFuturesOptionTickDataAsync` must resolve the option's underlying
internally and inspect the epoch's `TickAggregationContractStatus` before route
activation. It throws `TickAggregationNotRunningException` if aggregation is
stopped and `UnderlyingTickerNotRunningException` if the underlying is absent
or not running. This check is an implementation invariant of the existing start
method; it does not require exposing framework status objects through the
application contract. "Running" here means the underlying is actively processed
by tick aggregation; it does not require a separate transient UI route for the
underlying.

The command no longer accepts a provider request ID, a risk-free rate, or a
provider-formatted contract merely to start streaming.

### Stop command

Call
`StopStreamingFuturesOptionTickDataAsync(futuresOptionContractId)` by canonical
contract ID. Stopping an option route does not stop its underlying futures route
or the epoch.

### Tick events

Replace `FuturesOptionTickBidAsk` processing with the explicit option quote and
trade events. Remove:

- `InsertFuturesOptionTickDataAsync`;
- `InsertFuturesOptionTickPriceDataAsync`;
- legacy per-event `OptionCalculator` execution;
- streaming-request Blackboard state;
- legacy tick-cache writes used only by market-data lookup.

The legacy `FuturesOptionTickBidAsk` handler and the futures/futures-option
per-tick Blackboard cache models have now been removed. The shared event
contract remains temporarily for wire compatibility but is not registered by
the futures-option event actor.

Live strategy and UI consumers use transient option events. Consumers that
require a durable transaction boundary listen to aggregation inserted-complete
events filtered to `AssetTypeId.FuturesOption`.

Publish `OptionTradeTickPriceDataUpdatedEvent` from exactly one migration
adapter if an existing trade workflow still needs it. Key idempotency by
`ContractId + ValueDate + SourceSequence` and remove the adapter when those
consumers adopt the new option trade event.

## 13. Replacing `FuturesOptionQuoteData`

`FuturesOptionTickQuoteUpdatedEvent` is the direct replacement for
`FuturesOptionQuoteDataUpdatedEvent`:

| Legacy field | Replacement |
|---|---|
| `QuoteId` | Removed; no domain meaning |
| `RequestId` | Removed; provider implementation detail |
| `ContractId` | Canonical option `ContractId` |
| `BidPrice` | Nullable bid price from the option quote tick |
| `AskPrice` | Nullable ask price from the option quote tick |
| `BidSize` | Bid size from the option quote tick |
| `AskSize` | Ask size from the option quote tick |
| none | Source sequence and timestamps |
| none | Optional exact-sequence Greeks state |

### Iron Condor order UI

Migrate `IronCondorTradeOrderViewModel` as follows:

1. Remove quote-ID and numeric request-ID allocation.
2. Maintain the set of canonical option contract IDs for the displayed legs.
3. Start each selected option with the existing
   `FuturesOptionTickData` start command after its underlying futures feed is
   active. A future chain-level UI can use the option-chain API instead.
4. Listen for `FuturesOptionTickQuoteUpdatedEvent` through the event router.
5. Match updates by canonical option contract ID and update bid, ask, and sizes.
6. Stop each route when the view is disposed or its leg is removed.
7. Remove the quote-specific UI event consumer and command-model methods.

This preserves the UI behavior without retaining a quote-specific provider
connection, quote identifier, insert command, or database table.

## 14. Dependency injection and startup

The composition root that hosts the actors must reference and register the new
application implementation and its Databento dependencies.

Add project references from `TomasAI.IFM.Domain.MarketData.Feed` to
`TomasAI.IFM.Application.MarketData` and `TomasAI.IFM.Framework.MarketData`.
Place `ActorTickLiveEventSink` under the domain feed's `TickAggregation/Live`
folder: the sink implements the framework transport boundary, maps into Domain
Shared events, and uses the application API only for an exact-sequence option
reader lookup. This placement avoids putting actor/event-router knowledge into
the reusable Databento framework project.

In the API Server composition root, invoke the existing
`AddDatabentoMarketDataServices` and `AddApplicationMarketDataApi` registration
extensions, then replace any null transport defaults with the production actor
publishers.

Required effective registrations are:

```text
IMarketDataApi                  -> DatabentoMarketDataApi (singleton)
DatabentoMarketDataApi          -> same singleton instance
ITickAggregationEventPublisher -> TickAggregationEventPublisher
ITickLiveEventSink              -> ActorTickLiveEventSink
ITickLiveEventPublisher         -> BoundedTickLiveEventPublisher
ITickLiveRouter                 -> TickLiveRouter
```

Add validated Databento runtime options and explicit registrations for every
futures and futures-option contract in the epoch. Contract validation must fail
startup rather than permit an option route that has no registered underlying.

Add container tests proving:

- interface and concrete API resolve to the same singleton;
- the production live publisher is not the null implementation;
- the real aggregation event publisher is registered;
- all migrated actors can be constructed;
- no actor receives the legacy snapshot API;
- no option starts when its underlying is absent or inactive.

## 15. Legacy contract-definition query migration

The source audit found two active broker-backed query operations. Both use the
legacy snapshot connection and must be removed before the old interfaces can be
deleted.

| Legacy operation | Active production consumer | Required outcome |
|---|---|---|
| Broker futures-option definition | Option tick startup in `MarketDataFeedCommandModel` | Delete the extra broker lookup; the option start command carries only the canonical option ID and the domain actor resolves it through application `IMarketDataApi` |
| Broker futures-option spread snapshot | Iron Condor order pricing UI | Replace with application contract/readers plus an application/domain spread-quote calculation; never fall back to the snapshot API |

The old provider `GetFuturesContractAsync` method has no active production
caller found by the audit. It is deleted with the interface rather than
migrated as a separate flow.

### 15.1 Option contract definition used by stream startup

The current flow is:

```text
UI MarketDataFeedCommandModel
  -> SecuritiesDb GetFuturesOptionContractAsync(id)
  -> MarketDataFeedQueryApi.GetFuturesOptionContractAsync(id, template)
  -> MarketDataFeedQueryActor / ActorMarketDataFeedQueryApi
  -> snapshot StartAsync
  -> allocate numeric stream ID
  -> IB GetFuturesOptionContractAsync(requestId, template)
  -> remove stream ID and stop snapshot connection
  -> start option tick command with full provider contract
```

Replace it with:

```text
UI MarketDataFeedCommandModel
  -> start option tick command(FuturesOptionContractId, ValueDate)
  -> FuturesOptionTickData actor
  -> application IMarketDataApi.GetFuturesOptionContractAsync(id)
  -> application IMarketDataApi.StartStreamingFuturesOptionTickDataAsync(id)
```

Required changes are:

1. Remove both `_marketDataQueryApi` and `_marketDataFeedQueryApi` from
   `MarketDataFeedCommandModel` if their only remaining use is this start flow.
2. Remove the stored-contract-to-broker-contract double lookup and fixed delay.
3. Change the option start command and its HTTP/NATS producers to carry only
   `FuturesOptionContractId` and `ValueDate`.
4. Resolve the current option definition in the domain actor through application
   `IMarketDataApi` and validate the underlying before route activation.
5. Delete the broker-specific `GetFuturesOptionContractQuery`, parameter,
   handler, actor/API method, client methods, route, URI constant, semaphore,
   request-ID allocation, and tests after all in-repository and approved
   external consumers are migrated.

If a provider-resolved definition must remain remotely queryable for a future
consumer, expose a new application-market-data query containing only a canonical
option ID. Its server handler calls application `IMarketDataApi`; it must not
retain the legacy MarketDataFeed query contract or accept a provider template.
No current in-repository runtime consumer requires that replacement endpoint.

### 15.2 Futures-option spread snapshot replacement

The legacy spread query currently performs four distinct responsibilities:

1. resolves the short and long IB option definitions;
2. obtains two one-shot option price snapshots;
3. calculates two sets of Greeks using caller-supplied underlying price and
   risk-free rate;
4. returns a combined spread read model with zero-valued Greek fallbacks.

Replace it with a provider-neutral application/domain spread-quote reader. The
recommended contract is conceptually:

```text
GetFuturesOptionSpreadQuoteAsync(
    string shortFuturesOptionContractId,
    string longFuturesOptionContractId)
```

Its implementation:

1. resolves both definitions in one call to
   `IMarketDataApi.GetFuturesOptionContractsAsync`;
2. verifies the same underlying, maturity, and compatible option rights;
3. obtains each epoch-bound `IFuturesOptionLastPriceReader`;
4. reads the latest quote, falling back to the latest trade only according to a
   documented spread-pricing rule;
5. attaches Greeks only from an exact-sequence enriched snapshot;
6. returns explicit per-leg quote availability, Greek availability/validity,
   nullable values, failure reason, source sequence, and timestamps.

The new result must not use numeric zero to mean missing price or Greeks. Until
the FMP treasury curve is available, raw bid/ask spread pricing remains usable
and the Greeks state is explicitly unavailable or invalid. The UI disables or
labels any action that requires valid Greeks; it does not call the old snapshot
API.

Move the spread calculation out of the feed query bounded context. A suitable
owner is an application/domain option-spread service that depends on application
`IMarketDataApi`; it is not a method on the provider framework. Migrate both
Iron Condor call sites and then delete:

- `GetFuturesOptionSpreadDataQuery`;
- `GetFuturesOptionSpreadDataParameter`;
- `IMarketDataFeedQueryApi.GetFuturesOptionSpreadDataAsync`;
- HTTP and NATS client implementations;
- the API Server route and URI constant;
- the query actor/direct actor API handlers;
- the legacy option price and Greeks methods that become unreferenced;
- tests that assert snapshot request IDs or zero-sentinel Greeks.

### 15.3 Stored contract catalog remains independent

Do not replace the following `IMarketDataQueryApi`/`SecuritiesDb` capabilities
with the running Databento epoch:

- get a stored futures or futures-option contract by canonical ID;
- list all stored futures contracts;
- list stored futures options by symbol;
- identify currently traded futures contracts by symbol;
- find which proposed option IDs already exist;
- contract editor and contract-import workflows;
- off-epoch contract reads used by scheduled tasks, analytics, and UI editors.

These queries use `Domain.MarketData` actors and `SecuritiesDb`, not the legacy
feed or snapshot interface. Current consumers include the contract editors,
application startup, closing-price scheduled task, futures analytics,
`TradeLiveFeedAdded`, `AlgorithmBuilder`, and the general
`MarketDataQueryModel`.

The application `IMarketDataApi` has a different purpose: it returns
provider-resolved definitions from the active epoch catalog for explicitly
registered contract IDs and live option chains. Its current definition methods
require a running epoch. Never start or stop an epoch merely to serve a stored
contract editor query.

### 15.4 Production files in the legacy contract path

The migration must edit or delete every applicable member in:

- `TomasAI.IFM.Domain.MarketData.Feed.Shared/IMarketDataApi.cs`;
- `TomasAI.IFM.Domain.MarketData.Feed.Shared/IMarketDataApiOptions.cs`;
- `TomasAI.IFM.Domain.MarketData.Feed.Shared/ServiceApi/IMarketDataFeedQueryApi.cs`;
- the shared option contract/spread query and parameter files;
- `TomasAI.IFM.Domain.MarketData.Feed/Query/GetFuturesOptionContract.cs`;
- `TomasAI.IFM.Domain.MarketData.Feed/Query/GetFuturesOptionSpreadData.cs`;
- `MarketDataFeedQueryActor`, `MarketDataFeedQueryParameters`, and
  `ActorMarketDataFeedQueryApi`;
- HTTP and NATS `MarketDataFeedQueryApi` implementations;
- API Server `QueryMaps` and shared query-path constants;
- API Server startup market-data registrations;
- `MarketDataFeedCommandModel` and `MarketDataFeedQueryModel`;
- both Iron Condor spread-query call sites;
- Service and Framework Interactive Brokers market-data projects;
- integration-test startup and every legacy query fixture/test.

### 15.5 Application API registration and cutover

Before removing the old registrations, API Server startup must register the
DataBento framework services and application API:

```text
AddDatabentoMarketDataServices(...)
AddApplicationMarketDataApi(...)
```

The resolved application `IMarketDataApi` and concrete
`DatabentoMarketDataApi` must be the same singleton. All actors and application
services use the application namespace explicitly during the transition to
avoid binding accidentally to the identically named legacy interface.

Then remove both legacy registrations:

```text
IMarketDataApi         -> IBMarketDataApi
IMarketDataSnapshotApi -> IBMarketDataSnapshotApi
```

Remove `MarketDataFeedApi` and `MarketDataFeedSnapshotApi` host, port, and
client-ID configuration after deployment manifests and test hosts no longer
consume them.

### 15.6 Complete old-interface deletion rule

After lifecycle, tick, contract, and spread consumers are migrated, delete the
legacy Domain Shared interfaces and option types unconditionally. Do not leave
obsolete methods throwing `NotSupportedException`, forwarding adapters, empty
marker interfaces, or unused DI registrations. Compilation failures after the
deletion are treated as the authoritative final consumer inventory and must be
resolved before proceeding.

## 16. Persistence and cache rules

The following invariant is an acceptance requirement:

> Every persistable futures or futures-option ticker-feed observation has one
> raw-tick persistence route: `TickAggregationService` to the aggregation
> actors and aggregation storage.

This invariant applies to the configured ticker-feed universe handled by tick
aggregation. A separately requested option-chain session remains live-only and
does not persist its chain observations. If an option is present in both, only
the ticker-feed observation processed by `TickAggregationService` enters the
durable branch; the chain event must not create a second write.

Therefore the migrated live pipeline must have no call to:

- `InsertFuturesTickDataAsync`;
- `InsertFuturesOptionTickDataAsync`;
- `InsertFuturesOptionTickPriceDataAsync`;
- `InsertFuturesOptionQuoteDataAsync`;
- legacy event-source repositories for those raw tick/quote records.

Current values are read from `IFuturesLastPriceReader` and
`IFuturesOptionLastPriceReader`. Durable history is queried from tick
aggregation storage. Blackboard may continue to host unrelated application
state, but it is not the authoritative provider tick cache.

## 17. Ordered implementation phases

### Phase 1: Contracts and tests

- Add the four explicit live domain events and payloads.
- Add serialization round-trip and validation tests.
- Add mapping tests for missing quote sides, timestamps, sequences, and Greeks
  availability/validity.
- Resolve the unused ambiguous option tick event by replacement or versioning.

### Phase 2: Live bridge and composition

- Implement `ActorTickLiveEventSink`.
- Wire the bounded live publisher and real aggregation publisher.
- Add DI identity and non-null transport tests.
- Prove bounded backpressure and visible fault propagation.

### Phase 3: Epoch and futures migration

- Migrate `MarketDataFeedEventActor` lifecycle.
- Migrate `FuturesTickData` start and stop behavior.
- Remove its duplicate raw-tick insert.
- Move transaction-dependent EOD/VX behavior to the aggregation event.

### Phase 4: Futures-option migration

- Migrate `FuturesOptionTickData` start and stop behavior.
- Enforce the running-underlying prerequisite.
- Remove legacy local Greeks and duplicate inserts.
- Migrate trade/UI side effects to the new transient or durable event according
  to their transaction requirement.

### Phase 5: UI quote replacement

- Move the Iron Condor order view to option tick quote events.
- Remove quote and request IDs from the view model.
- Remove the quote-specific UI consumer and command methods.
- Run UI lifecycle tests for start, updates, leg changes, and disposal.

### Phase 6: Legacy query and interface removal

- Remove the option-start broker definition lookup and pass only the canonical
  option ID through the command path.
- Implement the provider-neutral option-spread quote reader and migrate both
  Iron Condor UI callers without a legacy fallback.
- Delete the two broker-backed MarketDataFeed queries from Domain Shared,
  actors, direct APIs, HTTP/NATS clients, server maps, and URI constants.
- Register the DataBento application API in every production and integration
  composition root.
- Delete the legacy snapshot API, old domain feed API, option types, stream IDs,
  options, IB implementations, project references, configuration, and tests.
- Prove the `SecuritiesDb` contract catalog and its callers remain operational.

### Phase 7: `FuturesOptionQuoteData` deletion

- Execute the deletion matrix in section 18.
- Run all zero-reference and route/schema gates.
- Keep the production table-drop migration separate and explicitly approved.

### Phase 8: Validation and cleanup

- Run bounded unit/integration gates, live-native tests, smoke tests, and
  benchmarks.
- Compare transient event counts and durable aggregation counts by contract and
  sequence.
- Remove temporary compatibility adapters after all consumers have migrated.

Each phase must compile and pass its bounded tests before the next phase begins.

## 18. Complete legacy API and `FuturesOptionQuoteData` deletion matrix

Delete or edit the following categories after their replacement paths succeed.

### Legacy feed and snapshot API

- Delete `TomasAI.IFM.Domain.MarketData.Feed.Shared.IMarketDataApi` and
  `IMarketDataSnapshotApi`.
- Delete the unused `IMarketDataServerApi` and
  `IMarketDataServerSnapshotApi`; they expose the same provider request-ID
  model and have no active implementation or consumer.
- Delete `IMarketDataApiOptions`, `IMarketDataSnapshotApiOptions`, and
  `IBrokerDataApiOptions` when their final legacy consumer is gone.
- Delete `IStreamIdCollection`, `StreamIdCollection`, and stream-ID tests if the
  final repository reference is part of the old provider path.
- Delete `RequestID`, `StreamingRequestId`, provider streaming-parameter types,
  and their Blackboard models when the post-migration reference audit shows no
  non-legacy use.
- Delete `IMarketDataApiEventProducer`, `IMarketDataFeedEventProducer`, its NATS
  implementation/registration, and denormalizer marker if IB removal leaves
  them unreferenced. The new live bridge uses `ITickLiveEventPublisher` and the
  actor/event-router sink instead.
- Delete old option price/Greeks provider contracts and callback payloads that
  have no non-legacy consumer.

### Legacy broker contract and spread queries

- Delete the feed-specific option contract and spread query/parameter types.
- Remove their methods from `IMarketDataFeedQueryApi`, HTTP/NATS clients,
  query actor, direct actor API, API Server maps, and shared URI paths.
- Remove the snapshot semaphore and per-query provider lifecycle code.
- Remove the broker template, risk-free-rate, time-value, full contract, and
  request-ID parameters from all migrated callers.

### Interactive Brokers market-data implementations

- Delete the market-data classes and options from
  `TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers` after confirming that
  project contains no separately retained capability.
- Delete the duplicate market-data classes/options from
  `TomasAI.IFM.Framework.MarketData.InteractiveBrokers` under the same rule.
- Remove both project references from API Server and test projects.
- Remove the projects from the solution if empty; otherwise rename/re-scope
  them so no legacy market-data API remains.
- Interactive Brokers trade/order functionality outside these market-data
  projects is out of scope and must not be removed accidentally.

### Composition and configuration

- Remove the two legacy DI registrations from API Server and integration hosts.
- Remove feed/snapshot host, port, and client-ID configuration, validation,
  secrets, deployment variables, and sample settings.
- Remove legacy provider status/error routing that becomes unreachable.
- Add a container approval test that fails if any legacy interface or IB
  market-data implementation is registered.

### Domain feed

- Delete the entire
  `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionQuoteData` folder.
- Delete quote insert/start/stop commands, parameters, models, validation,
  exceptions, actor state, repositories, events, extensions, and actors from
  Domain Shared and associated projects.
- Delete `FuturesOptionQuoteDataReadModel`, `FuturesOptionQuoteReadModel`,
  `FuturesOptionQuoteDataUpdatedEvent`, and streaming quote events.
- Delete `QuoteId`, `FuturesOptionQuoteId`, `GetOptionQuoteIdQuery`, and their
  parameters only after separate zero-reference checks.

### API client, NATS, and server

- Remove insert/start/stop quote methods from public interfaces and clients.
- Remove NATS subjects, command maps, API routes, parameters, and result helpers.
- Remove server handlers and integration tests for those routes.
- Remove quote cases from shared event-producer switches.

### UI

- Delete `FuturesOptionQuoteDataUIEventConsumer` and its registration.
- Remove quote-specific methods from `MarketDataFeedCommandModel`.
- Delete `MarketDataFeedEventModel` if it has no remaining responsibility.
- Remove all quote-ID/request-ID state from `IronCondorTradeOrderViewModel`.

### Blackboard

- Delete `FuturesOptionQuoteDataCacheModel` and
  `FuturesOptionQuoteCacheModel`.
- Remove their root properties, initialization, cache-name enum entries, cache
  commands, and tests.

### Storage and schema

- Remove quote and quote-data insert/read/delete methods and CQL parameters.
- Remove active schema registration/create statements for
  `futures_option_quote` and `futures_option_quote_data`.
- Add a separately reviewed database migration that archives or drops those
  tables only after the approved retention period.

### Legacy provider

- Remove quote-only start/stop methods from the legacy service API.
- Remove IB client add/remove quote subscriptions, callback queues, and quote
  callback handling when reference checks prove no remaining consumer.
- Do not remove other IB functionality solely because it shares a project.

### Enums, IDs, and configuration

- Remove the quote bounded-context name, log-source value, data-cache name,
  sequence name, command/query path constants, and obsolete error/status text.
- Remove legacy snapshot connection configuration only after every old snapshot
  consumer has migrated.

### Tests and documentation

- Delete legacy quote actor, BDD, integration, API, storage, and UI tests.
- Replace or delete the audited snapshot/contract-query tests in:
  `Application.Actor.IntegrationTests`, `Application.Api.IntegrationTests`,
  `Domain.MarketData.Feed.BDDTests`, `Domain.MarketData.Feed.IntegrationTests`,
  and `Domain.MarketData.Feed.UnitTests`.
- Delete `StreamIdCollectionTests` and `StreamIdCollectionBenchmarks` after the
  stream-ID types are removed.
- Replace them with option tick quote tests before deletion.
- Update architecture and operations documentation so it no longer presents
  `FuturesOptionQuoteData`, an IB feed/snapshot API, or broker-backed contract
  query as an active subsystem.

## 19. Tests and acceptance gates

### 19.1 Contract tests

- Canonical contract IDs survive every mapping unchanged.
- Quote and trade events remain distinct.
- Missing bid or ask remains nullable.
- Prices, sizes, counts, sequences, and timestamps map exactly.
- Option Greeks are attached only to the matching source sequence.
- Invalid or unavailable Greeks never become valid zero values.

### 19.2 Lifecycle tests

- Epoch start and stop are awaited and idempotent.
- Route start/stop is idempotent.
- Stopping one contract does not stop the epoch.
- An option cannot start before its registered underlying is running.
- An underlying mismatch or unknown contract fails with a typed error.
- Epoch stop flushes aggregation buffers and clears transient routes.

### 19.3 Persistence tests

- Futures and futures-option ticks each reach aggregation storage once.
- Activating a transient route does not change durable tick counts.
- Deactivating a transient route stops live events but not configured epoch
  persistence.
- Replayed aggregation events remain idempotent.
- No legacy raw-tick or option-quote insert method is invoked.

### 19.4 Consumer tests

- The Iron Condor order UI receives bid/ask changes by option contract ID.
- Adding and removing a leg activates/deactivates the expected route.
- Disposing the UI leaves no live listener or route owned by that view.
- Required EOD/VX and option-trade side effects occur once.
- Duplicate or out-of-order source sequences are handled deterministically.

### 19.5 Runtime gates

- Solution build passes.
- Bounded DataBento integration tests pass.
- Live-native DataBento tests pass with configured credentials/data.
- Application API smoke tests pass for definitions, prices, readers, and routes.
- A futures-plus-option soak test reports no publisher loss, silent fault, or
  unbounded memory growth.
- Benchmarks show the live bridge and reader lookup stay within the existing
  Phase A latency/allocation budget; record the before/after results.

### 19.6 Zero-reference gates

Run repository-wide reference checks after excluding this specification,
approved historical migrations, and archived documentation. Active source must
contain zero references to:

```text
FuturesOptionQuoteData
FuturesOptionQuoteReadModel
FuturesOptionQuoteId
GetOptionQuoteId
MarketDataFeedSnapshotApi
TomasAI.IFM.Domain.MarketData.Feed.Shared.IMarketDataSnapshotApi
TomasAI.IFM.Domain.MarketData.Feed.Shared.IMarketDataApi
IMarketDataSnapshotApiOptions
GetFuturesOptionContractFromBrokerAsync
GetFuturesOptionSpreadData
IStreamIdCollection
IMarketDataServerApi
IMarketDataServerSnapshotApi
IBMarketDataSnapshotApi
IBMarketDataApi
IMarketDataFeedEventProducer
StreamingRequestId
```

Also verify:

- no quote-specific API route or NATS subject is registered;
- no `/api/marketdata/feed/futures/option/contract` or
  `/api/marketdata/feed/futures/option/spread` route/subject is registered;
- no active schema bootstrap creates the two legacy quote tables;
- no production DI registration resolves the legacy snapshot interface;
- no production or test DI registration resolves the old domain feed API or an
  Interactive Brokers market-data implementation;
- no active configuration contains `MarketDataFeedSnapshotApi` or the legacy
  Interactive Brokers feed connection settings;
- no active project references either legacy IB market-data project after its
  removal;
- no new event consumer calls a legacy tick/quote insert command;
- no UI flow allocates quote IDs or provider request IDs.

### 19.7 Test project ownership

Use the repository's existing xUnit, FluentAssertions, and NSubstitute
conventions. Put a test at the lowest layer that can prove the behavior without
unnecessary infrastructure.

| Test project | New or revised responsibility |
|---|---|
| `TomasAI.IFM.Application.MarketData.UnitTests` | Application API lifecycle, route semantics, contract mapping, readers, and underlying admission |
| `TomasAI.IFM.Domain.MarketData.Feed.UnitTests` | Command contracts, actors, live-event mapping, downstream adapters, and absence of legacy inserts |
| `TomasAI.IFM.Framework.MarketData.UnitTests` | Bounded live publisher ordering, drain, backpressure, and sink failures |
| `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests` | Tick aggregation, option/futures route behavior, last-price ordering, and internal status validation |
| `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests` | Hosted command/event flow over the actor/NATS boundary and aggregation persistence |
| `TomasAI.IFM.Application.Api.IntegrationTests` | Production composition-root and public API/route removal checks |
| `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests` | Bounded managed/native contract mapping and ticker-feed behavior |
| `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests` | Credential-gated live DataBento validation only |
| New `TomasAI.IFM.UI.Net.ViewModels.UnitTests` | Iron Condor option-route ownership, quote updates, and disposal |

The new UI test project targets `net10.0`, references
`TomasAI.IFM.UI.Net.ViewModels`, and uses the same test package versions as the
Domain Feed unit-test project. It must not start a UI process or require a
desktop session.

### 19.8 Required application API unit tests

Extend `MarketDataApiStreamingContractTests` and
`DatabentoProductionEpochTests` with these cases:

| Test | Required assertion |
|---|---|
| `IndividualOptionRouteRejectsStoppedAggregation` | `StartStreamingFuturesOptionTickDataAsync` throws `TickAggregationNotRunningException`; no route is allocated |
| `IndividualOptionRouteRejectsMissingUnderlyingTicker` | It throws `UnderlyingTickerNotRunningException`; no option route is retained |
| `IndividualOptionRouteRejectsStoppedUnderlyingTicker` | A configured but non-running underlying is rejected |
| `IndividualOptionRouteAcceptsRunningUnderlying` | The first call returns `true`, the second returns `false` |
| `IndividualOptionStopDoesNotStopUnderlyingOrEpoch` | Option stop removes only the transient option route |
| `OptionDefinitionResolvesCanonicalUnderlying` | Provider symbols never replace either canonical domain ID |
| `OptionReaderRequiresRunningEpochAndKnownOption` | Stopped epoch and unknown option produce the documented typed failures |
| `OptionReaderReturnsExactSequenceEnrichment` | Raw and enriched snapshots report the same source sequence |
| `OptionReaderDoesNotSynthesizeGreeks` | Missing/invalid enrichment remains explicitly unavailable or invalid |
| `EpochStopInvalidatesExistingReaders` | A reader retained by a consumer cannot expose a later epoch as if it were the old one |

Retain the existing concurrency test pattern and add a 32-caller concurrent
individual-option start case with the underlying running. Exactly one call
returns `true`; the rest return `false`.

Update `DeterministicMarketDataApi` and `MarketDataApiTestContext` so tests can
independently set:

- aggregation service running/stopped;
- underlying configured/unconfigured;
- underlying running/stopped;
- route ownership;
- raw and enriched reader sequences.

The deterministic harness must implement the same individual-option admission
rules as production so contract tests cannot pass against behavior that the
production API does not provide.

### 19.9 Required command and serialization unit tests

Add `TickStreamCommandContractTests` under
`TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Contracts`.

Test all four migrated commands for:

- MessagePack round-trip of command ID, subject, entity ID, canonical contract
  ID, value date, route, and error code;
- `FuturesContractId` and `FuturesOptionContractId` property names;
- rejection of null, empty, or whitespace IDs;
- preservation of the canonical ID without uppercasing or provider formatting;
- correct actor mailbox identity for contract plus value date;
- absence of numeric request/stream IDs;
- absence of `RiskFreeRate`, `BaseContract`, and provider contract payloads from
  the option start command;
- explicit compatibility handling for the old MessagePack command version.

Add compile-time approval tests to
`MarketDataApiContractApprovalTests` proving the public API still has exactly
the approved application methods and exposes no legacy snapshot type or
provider-specific identifier.

### 19.10 Required domain actor unit tests

Revise `FuturesTickDataEventActorTests`,
`FuturesOptionTickDataEventActorTests`, and `MarketDataFeedEventActorTests`.

#### Feed lifecycle actor

- Start calls application `IMarketDataApi.StartAsync` once with the event value
  date and publishes completion only afterward.
- A matching repeated start is idempotent.
- A start exception publishes the typed failure and no completion.
- Stop calls `StopAsync` once and waits for it before completion.
- Reset awaits stop before start and uses no delay/timer.
- A child route stop never invokes epoch `StopAsync`.

#### Futures tick actor

- Start passes only `FuturesContractId` to
  `StartStreamingFuturesTickDataAsync`.
- Both `true` and `false` results publish successful start completion.
- Stop passes only the canonical ID and treats already-stopped as success.
- Unknown contract and stopped epoch exceptions map to the existing actor
  failure contract.
- No start/stop path allocates or reads a numeric request ID.
- A quote or trade live event produces no legacy insert command and no
  Blackboard tick-cache write.

#### Futures-option tick actor

- Start resolves the option through the application API and activates it by
  `FuturesOptionContractId`.
- Underlying-not-running and aggregation-not-running exceptions publish the
  expected typed failure.
- Start does not pass maturity, risk-free rate, or a provider contract.
- Stop removes only the canonical option route.
- Quote and trade events produce no legacy option tick/price insert command.
- No event invokes the legacy `OptionCalculator` or snapshot API.
- A compatibility trade event is published once when a remaining trade
  consumer requires it.
- Duplicate `ContractId + ValueDate + SourceSequence` observations do not
  duplicate compatibility side effects.

Use strict NSubstitute assertions for prohibited calls. A test that only checks
the desired outgoing event is insufficient; it must also assert zero calls to
legacy insert, Blackboard routing-state, and snapshot dependencies.

### 19.11 Required live-event bridge unit tests

Add `ActorTickLiveEventSinkTests` under the Domain Feed unit-test project's
`TickAggregation/Live` folder.

Test the complete mapping matrix:

| Input | Expected output |
|---|---|
| Futures quote | One `FuturesTickQuoteUpdatedEvent` |
| Futures trade | One `FuturesTickTradeUpdatedEvent` |
| Futures-option quote | One `FuturesOptionTickQuoteUpdatedEvent` |
| Futures-option trade | One `FuturesOptionTickTradeUpdatedEvent` |
| Unknown asset type | Visible mapping failure; no event |

For every row, assert event ID, canonical contract ID, value date, source
sequence, event/receive timestamps, prices, sizes, and counts.

Additional option cases are mandatory:

- matching enriched sequence attaches its Greeks state;
- mismatched enriched sequence publishes the raw tick with Greeks unavailable;
- an available but invalid calculation preserves its failure reason and nullable
  values;
- a one-sided quote preserves the missing side as null;
- zero bid or ask is not converted into a valid midpoint;
- reader lookup failure is visible and does not publish a fabricated event;
- actor publisher failure propagates to the bounded publisher;
- no mapper branch performs persistence or Blackboard writes.

### 19.12 Required UI ViewModels unit tests

Add `IronCondorTradeOrderViewModelTests` to the new
`TomasAI.IFM.UI.Net.ViewModels.UnitTests` project. Use substituted command API
and event-router/listener dependencies so the tests remain headless.

Required cases are:

- initialization starts one option tick route for each distinct leg contract;
- duplicate leg contracts share one owned route rather than issuing duplicate
  starts;
- an option quote updates only the matching leg's bid, ask, and sizes;
- an event for an unowned contract is ignored;
- a one-sided quote clears or preserves the missing side according to the
  documented UI rule and never displays a fabricated zero price;
- removing one leg stops its route only when no remaining leg owns that contract;
- changing a leg stops the old route before starting the new route;
- partial startup failure rolls back routes already owned by the view model;
- disposal unregisters the event listener and stops every route it owns exactly
  once;
- late events after disposal do not mutate view-model state;
- UI-thread dispatch is used when an event arrives on a background thread;
- no path calls a `FuturesOptionQuoteData` command, allocates a quote ID, or
  stores a provider request ID.

### 19.13 Required framework and DataBento unit tests

Extend `BoundedTickLiveEventPublisherTests` with:

- accepted quote/trade events drain in source order on dispose;
- a full bounded queue applies backpressure rather than dropping;
- sink failure is observed by the publisher and subsequent shutdown;
- dispose rejects new events and completes accepted events;
- separate futures and option contract events retain their individual order;
- cancellation/shutdown cannot deadlock a waiting producer.

Extend `TickAggregationServiceTests`, `DatabentoLastPriceStoreTests`, and route
registry tests with:

- futures and option quote/trade records update the correct typed reader;
- the hot reader is updated before the corresponding live event reaches its
  sink;
- inactive routes still aggregate and persist configured ticker-feed records;
- inactive routes publish no transient events;
- activating a route publishes subsequent observations without creating a
  second physical subscription;
- deactivating a route stops transient delivery but durable aggregation
  continues;
- individual option activation performs the underlying status check atomically
  before route ownership is committed;
- a failed prerequisite leaves no option route and can be retried successfully;
- stopping/faulting aggregation clears individual and chain route ownership;
- option chain observations never enter aggregation persistence;
- an option present in ticker aggregation and a chain cannot be persisted twice.

Use synthetic records with deterministic source sequences. Unit tests must not
contact DataBento, NATS, ScyllaDB, Redis, or Postgres.

### 19.14 Required hosted integration tests

Add a new `ApplicationMarketDataMigration` folder to
`TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests`. Its fixture hosts the API
Server, actor supervisor, NATS transport, deterministic Databento epoch, and a
test aggregation repository. It may reuse `WebApplicationFactory<Program>` and
`NatsActorEventListener`, but must replace native/live provider dependencies.

Required scenarios are:

#### Futures route end to end

1. Start the epoch and a futures route through the public command API.
2. Inject one synthetic quote and one trade.
3. Observe the two new domain events over NATS in source order.
4. Observe the aggregation inserted-complete events.
5. Verify exactly one durable quote batch/trade write and zero legacy futures
   tick inserts.
6. Stop the route, inject another tick, and verify durable aggregation continues
   while no transient event is delivered.

#### Futures-option route end to end

1. Attempt option start before the underlying is running and assert the typed
   failure event.
2. Start aggregation with the underlying and retry successfully.
3. Inject one option quote and trade with known sequences.
4. Observe the explicit option quote/trade events with canonical IDs.
5. Verify exact-sequence Greeks mapping when enrichment is supplied and explicit
   unavailability when it is not.
6. Verify aggregation storage has one durable observation and the legacy option
   tick/price/quote repositories have no calls.
7. Stop the option and prove the underlying and epoch remain running.

#### Four-leg option quote delivery

1. Start four option routes for two calls and two puts.
2. Publish interleaved option quote events.
3. Use a headless test consumer to verify each leg receives only its contract's
   bid/ask update.
4. Remove one consumer-owned leg and prove its route/listener is stopped without
   affecting the other three.
5. Dispose the test consumer and prove no listener or owned route remains.
6. Assert that no quote ID, request ID, or quote-only API endpoint was used.

The corresponding `IronCondorTradeOrderViewModel` behavior is proven in the new
UI ViewModels unit-test project with substituted command and event-router
dependencies. Do not add a UI project reference to the Domain Feed integration
test project.

#### Failure and recovery

- Sink failure surfaces through the host health/error path while durable
  aggregation remains observable.
- Aggregation failure surfaces even if a previously queued UI event drains.
- Restart creates a new epoch, invalidates old readers, and restores routes only
  through explicit domain commands.
- Re-delivering the same source sequence does not duplicate durable or
  compatibility side effects.

Do not retain the existing `InsertFuturesOptionTickData_Ok`-style integration
tests as proof of the new path. Replace them with aggregation-driven tests; the
legacy insert endpoint is supposed to disappear.

### 19.15 Required composition-root integration tests

In `TomasAI.IFM.Application.Api.IntegrationTests`, build the production service
graph and assert:

- `IMarketDataApi` and `DatabentoMarketDataApi` resolve to the same singleton;
- `ITickLiveEventPublisher` is `BoundedTickLiveEventPublisher`, not a null
  publisher;
- `ITickLiveEventSink` resolves to `ActorTickLiveEventSink`;
- `ITickAggregationEventPublisher` resolves to the production publisher;
- all three migrated event actors construct successfully;
- no constructor or registration requires the legacy snapshot interface;
- the application API receives every required Databento epoch dependency;
- invalid or incomplete contract registration fails host startup.

Add route-surface tests that enumerate registered endpoints/NATS mappings and
fail if any insert/start/stop `FuturesOptionQuoteData` route remains.

### 19.16 Required storage integration tests

Use the existing local test ScyllaDB fixture only for the durable aggregation
branch. Generate a unique contract/value-date/sequence namespace per test and
clean it in fixture disposal.

Prove:

- a futures trade and option trade are stored under distinct asset identities;
- quote batches preserve contract, asset type, ordering, and exact decimal
  prices;
- replay of the same aggregation event is idempotent;
- transient route activation does not alter durable row counts;
- no rows are written to `futures_option_quote` or
  `futures_option_quote_data`;
- after schema-bootstrap cleanup, a fresh test schema does not create either
  legacy quote table.

The final table-absence test belongs after the source schema deletion. Until
then, mark it as a Phase 7 expected failure or keep it in a separately invoked
removal gate; do not make earlier phases permanently red.

### 19.17 Bounded native integration and live smoke tests

Extend `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests` using its
existing managed/native fixture:

- resolve one futures and one option definition into canonical IDs;
- feed bounded quote/trade records for both asset types;
- verify mapping, last-price snapshots, source sequences, and timestamps;
- verify route activation controls transient delivery only;
- verify option admission uses the mapped underlying contract status;
- stop and restart without leaking a native subscription or stale reader.

These tests must be deterministic and must not require a live API key. Tests
that contact DataBento belong in
`TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests` and remain guarded by
the existing live-test gate. The live smoke adds one current futures contract
and one current option, proves quote/trade receipt and reader updates, and stops
cleanly. Never assert that a trade must arrive inside a very short fixed window;
market inactivity is not a code failure.

### 19.18 Test reliability and cleanup rules

- Use `TaskCompletionSource` with `RunContinuationsAsynchronously` and bounded
  `WaitAsync`; do not use `Thread.Sleep` or fixed actor-start delays.
- Use `IAsyncLifetime` or `await using` so routes, listeners, publishers, epochs,
  and hosts are stopped even after an assertion fails.
- Give parallel tests unique canonical IDs, value dates, event IDs, and source
  sequences. Put shared NATS/Scylla fixtures in a non-parallel xUnit collection.
- Capture background task failures and assert them at test teardown.
- Assert both the expected event and the absence of forbidden duplicate events
  or writes during a bounded observation window.
- Live credential-gated tests may be skipped with an explicit reason;
  deterministic unit and hosted integration tests may not be skipped.
- Every legacy test deleted in Phase 7 must have a named replacement test or be
  documented as testing behavior that was intentionally removed.

### 19.19 Minimum test execution gates by phase

| Phase | Required test command scope |
|---|---|
| 1 | Domain Feed unit tests plus serialization/contract approval tests |
| 2 | Framework MarketData, Domain Feed live bridge, and composition-root tests |
| 3 | Application MarketData plus futures actor and hosted futures integration tests |
| 4 | Application MarketData plus option actor/DataBento and hosted option integration tests |
| 5 | UI ViewModels plus four-leg hosted option-delivery integration tests |
| 6 | Domain Feed query/API tests and legacy-interface approval gates |
| 7 | Full solution build, removal scans, route/schema absence tests |
| 8 | All deterministic tests, bounded native integration, smoke, soak, and benchmarks |

Record the exact commands and results in the implementation report for each
phase. A live smoke-test skip does not block intermediate development, but the
final production-ready gate requires an explicitly approved live result.

### 19.20 Required contract-detail and legacy-removal tests

Add deterministic tests covering the complete contract cutover:

- application `GetFuturesContractAsync` and
  `GetFuturesOptionContractAsync` return canonical configured definitions from
  the running epoch;
- batch definition calls preserve input order and fail atomically for unknown
  IDs;
- definition calls reject wrong asset kinds and a stopped epoch with typed
  errors;
- an option start command reaches the actor with only
  `FuturesOptionContractId` and `ValueDate` and never calls a broker-definition
  query;
- the option actor resolves the definition once and validates its underlying
  before activating the route;
- the new spread reader rejects mixed underlying, maturity, or invalid leg
  combinations;
- it maps two-sided and one-sided quotes without zero sentinels;
- matching enriched sequences return their Greeks, mismatches report Greeks
  unavailable, and missing FMP inputs never invoke a legacy fallback;
- both Iron Condor UI paths consume the replacement spread result;
- stored `IMarketDataQueryApi` futures/option contract reads still work while
  the market-data epoch is stopped;
- contract editor, currently-traded-contract, and option-ID existence queries
  remain backed by `SecuritiesDb`;
- HTTP and NATS route enumeration contains neither legacy broker contract nor
  spread endpoint;
- production and integration service graphs contain no old domain feed API,
  snapshot API, legacy options, stream-ID service, or IB market-data type;
- a reflection/assembly approval test fails if any public production type
  implements or exposes the deleted legacy contracts;
- a repository/project-reference approval test fails if API Server or tests
  regain a reference to either removed IB market-data project;
- configuration approval tests reject the old feed/snapshot connection keys.

The final deletion build is itself a required test: delete the old interface
and provider source before the full solution build, then fix every compilation
failure rather than reintroducing a compatibility shim.

## 20. Observability and failure behavior

Record metrics per asset type and contract for:

- Databento observations received;
- durable aggregation events published and completed;
- transient events routed, ignored because inactive, and delivered;
- live publisher queue depth and backpressure duration;
- mapping or sink failures;
- underlying-prerequisite rejections;
- enriched option snapshots available, valid, invalid, or sequence-mismatched.

Logs must include canonical contract ID, value date, asset type, source sequence,
and event ID. Provider instrument IDs may appear as diagnostic fields but never
as the domain routing key.

A live sink failure must fault or visibly degrade the transient pipeline; it
must not silently stop durable aggregation. Conversely, a durable aggregation
failure must be surfaced even if UI updates continue.

## 21. Rollout and rollback

Before deletion, deploy the new option tick quote event with a temporary
comparison observer. Compare its contract, bid, ask, sizes, and update cadence
against the legacy quote event without performing a second insert.

Move consumers one at a time. The rollback before Phase 7 is to restore the
consumer to the legacy event while leaving aggregation unchanged. After source
deletion, rollback uses version control and deployment rollback; database tables
must remain untouched until the separate retention migration is approved.

Do not run legacy and new persistence for comparison. Compare observations and
event counts, not duplicate database writes.

## 22. Definition of done

The migration is complete only when all of the following are true:

1. `FuturesTickData` and `FuturesOptionTickData` use the application
   `IMarketDataApi` for lifecycle, definition, reader, and route operations.
2. No migrated actor depends on the legacy snapshot API or numeric provider
   stream IDs.
3. Futures and futures-option raw ticks persist exclusively through
   `TickAggregationService`.
4. The option underlying prerequisite is enforced in production and tests.
5. UI and strategy consumers receive option quotes from
   `FuturesOptionTickQuoteUpdatedEvent`.
6. Required durable and transient side effects each occur exactly once.
7. All `FuturesOptionQuoteData` active source, APIs, caches, schema bootstrap,
   provider callbacks, registrations, and tests are removed.
8. The legacy Domain Feed `IMarketDataApi`, snapshot interface, option
   interfaces, stream-ID service, IB market-data implementations, DI,
   configuration, routes, transports, project references, and tests are absent
   from active source.
9. The legacy broker option-definition and spread queries are removed, and both
   their UI flows use canonical IDs plus the application API/readers.
10. Stored contract editor, discovery, currently-traded, and off-epoch reads
    continue to use and pass against `IMarketDataQueryApi`/`SecuritiesDb`.
11. The zero-reference, build, integration, live-native, smoke, soak, and
   benchmark gates pass.
12. Operations documentation reflects the new single-feed, dual-branch
   architecture.
13. Any physical legacy table removal is tracked as a separately approved and
    recoverable database change.
