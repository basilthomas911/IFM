# Databento implementation of `IMarketDataApi`

**Status:** Phase A implemented and runtime-validated; FMP-dependent Phase B deferred

**Version:** 1.8

**Date:** 2026-08-18

**Contract:** `TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi`

**Implementation:** `TomasAI.IFM.Application.MarketData.Databento.DatabentoMarketDataApi`

## 1. Purpose

This specification defines a Databento-backed implementation of the exact
`IMarketDataApi` contract in
`TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs`.

The public interface is binding. This design does not add command-ID
parameters, model-shaped lookup arguments, or synchronous streaming controls.
Option Greeks do not add another `IMarketDataApi` method; the provider-neutral
`IFuturesOptionLastPriceReader` returned by the existing API now exposes atomic
quote/trade-with-Greeks reads. The same Greeks snapshots are carried on
transient option-chain quote/trade service events.

`DatabentoMarketDataApi` is an application orchestration service. It composes
the existing Databento contract-query client, provider-selected hot-price
readers, the multi-asset tick aggregation pipeline for futures and futures
options, and the transient option-chain processing pipeline.
It does not duplicate native feed, actor, or storage responsibilities.

`IMarketDataApi`, its options, and its concrete orchestration implementation
exist only in `Application.MarketData`.
`Framework.MarketData` exposes provider-neutral service contracts, not another
market-data API. Vendor projects such as `Framework.MarketData.DataBento`
implement those framework contracts and are composed by the application
implementation through dependency injection.

The older `Domain.MarketData.Feed.Shared.IMarketDataSnapshotApi` belongs to the
legacy Interactive Brokers actor path and is not referenced by this application
contract or its DataBento implementation. It remains only until that actor path
is migrated to the application `IMarketDataApi`.

## 2. Authoritative interface

```csharp
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public interface IMarketDataApi
{
    bool TryGetOnTheRunFuturesContract(
        string symbol,
        out FuturesContractV3ReadModel contract);

    Task<bool> UpdateOnTheRunFuturesContractAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default,
        bool forceProviderRefresh = false);

    bool TryGetFuturesTermStructureContracts(
        string symbol,
        out FuturesTermStructureContracts contracts);

    Task<bool> UpdateFuturesTermStructureContractsAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);

    bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot);

    bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot);

    bool TryGetFuturesSessionStatistics(
        string contractId,
        out FuturesSessionStatisticsSnapshot snapshot);

    bool IsTickDataStreamActive(string contractId);

    Task StartAsync(
        DateOnly valueDate,
        Func<Guid, int, string, Task>? errorMessageHandler = null,
        CancellationToken cancellationToken = default);

    Task StopAsync(DateOnly valueDate);

    Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        string futuresContractId);

    Task<FuturesContractV2ReadModel[]> GetFuturesContractsAsync(
        string[] futuresContractIds);

    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        string futuresOptionContractId);

    Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
        string[] futuresOptionContractIds);

    Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(
        string futuresContractId,
        DateOnly maturityDate);

    Task<decimal> GetFuturesPriceAsync(
        string futuresContractId);

    Task<decimal?> GetFuturesOptionPriceAsync(
        string futuresOptionContractId);

    IFuturesLastPriceReader GetFuturesLastPriceReader(
        string futuresContractId);

    IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(
        string futuresOptionContractId);

    Task<bool> StartStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null);

    Task<bool> StopStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null);

    Task<bool> StartStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null);

    Task<bool> StopStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null);

    Task<bool> StartStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds);

    Task<bool> StopStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate);
}
```

The `Guid` in the optional error callback is an implementation-generated
operation ID. No public method accepts a Guid command ID.

The interface block above is the authoritative application boundary for this
specification. Provider-only request and subscription models remain behind the
Databento adapter.

### 2.1 Last-price reader contracts

`Framework.MarketData/Contracts/LastPrice/ILastPriceReader.cs` defines:

```csharp
public interface IFuturesLastPriceReader
{
    string FuturesContractId { get; }
    DateOnly ValueDate { get; }
    bool TryGetLastTrade(out LastTradeTickSnapshot snapshot);
    bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot);
}

public interface IFuturesOptionLastPriceReader
{
    string FuturesOptionContractId { get; }
    DateOnly ValueDate { get; }
    bool TryGetLastTrade(out LastTradeTickSnapshot snapshot);
    bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot);
    bool TryGetLastTradeWithGreeks(
        out LastTradeTickWithGreeksSnapshot snapshot);
    bool TryGetLastQuoteWithGreeks(
        out LastQuoteTickWithGreeksSnapshot snapshot);
}
```

Reads are synchronous, non-consuming, thread-safe, and local-memory only. A
`false` result means no valid value is currently available; it never triggers
provider, actor, storage, Blackboard, or Redis access. Quote snapshots preserve
one-sided markets and provide `TryGetMidpoint`, which succeeds only for a valid,
positive, non-crossed two-sided quote.

The enriched option methods are atomic hot reads; they never calculate Greeks
inside the caller's `TryGet` operation. A `true` result means the combined tick
and calculation result is available, not necessarily valid. Callers inspect
`OptionGreeksSnapshot.IsValid`, `IsStale`, and `FailureReason`. A quote wrapper
contains the calculation for that exact quote source sequence. A trade wrapper
contains the most recent quote-derived Greeks state available when the trade
was processed. Before enrichment is available, or after epoch stop, the method
returns `false` and a default output snapshot.

### 2.2 Futures session-statistics snapshot

`TryGetFuturesSessionStatistics` is a provider-neutral, synchronous hot-cache
read. A successful result contains a coherent session open, high, and low for
one `ContractId + ValueDate`; it performs no provider, actor, storage,
Blackboard, or Redis access. Databento is the current implementation, but no
Databento schema or identifier crosses this interface.

The epoch requests an ES statistics replay from the official trading-session
start and continues with live statistics. Replay observations are coalesced
before the first snapshot becomes visible. The rolling EOD realtime actor uses
this method while processing trades so the first EOD row is never initialized
from yesterday's close.

## 3. Design summary

1. One `DatabentoMarketDataApi` instance owns one trading-date lifecycle epoch.
2. `StartAsync(valueDate)` resolves and validates the configured futures and
   futures-option universes for that date, builds immutable mappings, creates
   the multi-asset aggregation inputs and transient chain manager, and starts
   them.
3. Contract and price methods require a running epoch because the interface
   supplies no value date to those methods.
4. Single contract lookup returns `null` for a genuine provider miss.
5. Batch contract lookup is all-or-nothing: missing or ambiguous input fails
   the call; successful output has the same length and order as input.
6. Option-chain discovery returns all current call and put definitions for one
   domain futures contract and exact maturity. The application maps them to
   domain read models; filtering belongs to the domain actor.
7. Futures prices use Databento's latest qualifying trade and return exact
   fixed-point-to-decimal conversion.
8. Futures-option prices use a valid bid/ask midpoint and return `null` when no
   qualifying quote exists before the configured deadline.
9. `ITickAggregationService` is asset-neutral. It accepts every admitted raw
   futures and futures-option quote/trade, updates the corresponding hot slot,
   and owns the only raw-tick persistence path.
10. Futures and futures-option streaming methods control live-delivery
    activation over the already-running aggregation pipeline; they do not
    independently start, stop, or persist an asset feed.
11. Futures-option chain streaming uses the existing
    `IDatabentoOptionChainFeed`. The application resolves domain contract IDs
    into its required underlying, maturity, strikes, rights, and provider
    definitions; provider identifiers never cross the application contract.
    A dedicated framework `OptionChainTickService` converts the shared feed
    records into transient live quote/trade messages and maintains current
    chain state for the OptionSpread engine and UI. On valid quote updates it
    uses the Black-76 implementation in `Framework.OptionPricer` to derive
    implied volatility and Greeks from a non-blocking futures-price snapshot.
12. Databento subscriptions are immutable after configuration. The complete
    approved provider universe is configured during `StartAsync`; per-contract
    streaming methods activate or deactivate application delivery without
    rebuilding the provider feed.
    Each option-chain session is likewise immutable after it is started.
13. ES futures subscriptions include Databento session statistics. The epoch
    derives the replay start from `ValueDate` at the prior 18:00 New York
    session boundary and exposes complete open/high/low state through
    `TryGetFuturesSessionStatistics`. VX remains quote/trade only.
14. Start/stop streaming methods return `true` only when they change activation
    state and `false` when the requested state already exists.
15. Failures throw typed exceptions. `false` and `null` are not generic error
    results.
16. Option-chain state, Greeks snapshots, spread results, and UI messages are
    never persisted. Any admitted raw futures or futures-option tick persistence
    is an explicit responsibility of `ITickAggregationService` only.
17. DataBento maintains an epoch-local latest quote/trade slot for every
    configured futures and futures-option contract. Option slots can also hold
    an atomic tick-with-Greeks view. The application API exposes non-consuming
    `IFuturesLastPriceReader` and `IFuturesOptionLastPriceReader` handles over
    those hot values.
18. US Treasury curves and economic-calendar data are exposed through
    provider-neutral contracts in `Framework.MarketData.Contracts`. Their FMP
    implementations live in `Framework.MarketData.FinancialModelingPrep`.
    The application consumes the treasury abstraction, selects one session
    rate, and passes it into DataBento; DataBento does not implement or call FMP
    APIs. No Blackboard L1/L2 price cache is required by this implementation.
19. Every option-chain session is a hard dependent of its underlying futures
    ticker in the same epoch's `TickAggregationService`. Chain start never
    starts the underlying implicitly and fails before provider allocation when
    the service or ticker is not running. Loss of that dependency stops/faults
    the chain.
20. There is no `Framework.MarketData.MarketDataApi` namespace or folder.
    Framework vendor projects implement only the provider-neutral service
    contracts required by the application-owned API implementation.

## 4. Existing capabilities

### 4.1 Reused components

- `IDatabentoFeedFactory` creates ticker feeds, option-chain feeds, and
  `IDatabentoMarketDataQueries`. Its one-shot latest-price client remains a
  framework utility but is not used by this application API.
- `IDatabentoMarketDataQueries` resolves contract details, option chains, and
  canonical contract/instrument mappings.
- `IDatabentoOptionChainFeed` accepts one exact resolved chain, uses one shared
  reader for all selected contracts, and implements bounded start/stop/health.
- `ITickAggregationService` implements the futures path from a multiplexed
  reader through ordered actor publication.
- `ITickContractMappingStore` holds definition-date-scoped provider-to-domain
  mappings required before futures aggregation starts.
- `ITickAggregationEventPublisher` owns the bounded Core-NATS realtime bridge.
- the MarketData Feed realtime actors and projectors consume aggregation and
  session-statistics observations and apply one-attempt Scylla mutations.

### 4.2 Required additions

- `DatabentoMarketDataApi`;
- `DatabentoMarketDataApiOptions`;
- `IDatabentoMarketDataEpochFactory` and one disposable epoch object;
- `DatabentoContractResolver` and `DatabentoContractMapper`;
- `IDatabentoOperationRunner`, a bounded scheduler for blocking provider calls;
- `IDatabentoFuturesOptionStreamingService`;
- `IDatabentoOptionChainSessionManager`, which owns active framework
  `IDatabentoOptionChainFeed` sessions keyed by domain underlying contract and
  maturity;
- `IOptionChainTickService` and `OptionChainTickService` in
  `Framework.MarketData.DataBento`, with one instance per active chain session;
- `TickAggregationTickerStatus` and contract-ID status lookup on
  `ITickAggregationService`;
- a narrow `IOptionChainGreeksCalculator` adapter in
  `Framework.MarketData.DataBento` backed by
  `Framework.OptionPricer.FuturesOptionGreeksCalculator` and Black-76;
- an application-resolved, fixed `OptionChainRiskFreeRate` value passed into
  the DataBento chain session;
- one epoch-owned `IDatabentoLastPriceStore` updated by the managed futures,
  individual-option, and option-chain processors before event publication;
- provider-neutral `IFuturesLastPriceReader`,
  `IFuturesOptionLastPriceReader`, and snapshot contracts in
  `Framework.MarketData.Contracts.LastPrice`, implemented directly by
  DataBento and selected through startup DI;
- an epoch-owned DataBento reader provider/registry used by
  `DatabentoMarketDataApi` to resolve contract-bound readers;
- provider-neutral `ITreasuryCurve` and `IEconomicCalendar` contracts in
  `Framework.MarketData.Contracts`, implemented by
  `Framework.MarketData.FinancialModelingPrep`;
- an application-owned Treasury cache/decorator for rate resolution; no
  Blackboard price cache or Redis price dependency;
- an epoch-owned `IOptionChainStateStore` containing only current, transient
  option-chain quote/trade state;
- an `IOptionChainLivePublisher` for transient OptionSpread-engine messages
  and throttled UI deltas;
- `IFuturesOptionTickEventPublisher` and a Guid-independent, contract-keyed
  Databento option quote event;
- API health, error reporting, and metrics sampling.

## 5. Architecture

```text
MarketData Feed actors / application services
                    |
                    v
Application.MarketData.Contracts.IMarketDataApi
                    |
                    v
Application.MarketData.Databento.DatabentoMarketDataApi
       |                  |                 |
       v                  v                 v
contract resolver   hot-price readers   lifecycle epoch
                                               |
                         +---------------------+--------------------+
                         |                                          |
                         v                                          v
              futures aggregation service              option streaming service
                         |                                          |
                         v                                          v
               durable tick actor path                    live option events

            dynamic option-chain session manager
                         |
                         v
                  option-chain feed
                         |
                         v
               OptionChainTickService
                  |       ^      |
                  |       |      |
                  |  Black-76 +  |
                  | app rate +   |
                  | DataBento hot|
                  | futures price|
                  v              v
          OptionSpread engine   throttled UI updates
                  \______________/
                         |
                  transient only
```

The application layer depends on provider abstractions and coordinates them.
Domain shared-contract projects do not depend on Databento. Native handles,
feed readers, pooled batches, and publisher envelopes remain owned by their
framework services. The application passes a scalar risk-free rate into the
framework session. DataBento supplies current futures/option prices from its
own epoch-local latest-value store and never references
`Application.Blackboard`.

## 6. Project layout

```text
TomasAI.IFM.Application.MarketData/
  Contracts/
    IMarketDataApi.cs
    IMarketDataApiDiagnostics.cs
  Databento/
    DatabentoMarketDataApi.cs
    DatabentoMarketDataApiOptions.cs
    DatabentoMarketDataEpoch.cs
    DatabentoMarketDataEpochFactory.cs
    DatabentoContractResolver.cs
    DatabentoContractMapper.cs
    DatabentoOperationRunner.cs
    DatabentoMarketDataApiHealth.cs
    DatabentoMarketDataApiServiceCollectionExtensions.cs
    TreasuryCurveCacheDecorator.cs
  Docs/
    Databento-MarketDataApi-Implementation-Specification.md

TomasAI.IFM.Framework.MarketData.DataBento/
  LatestPrice/
    DatabentoLastPriceStore.cs
    DatabentoFuturesLastPriceReader.cs
    DatabentoFuturesOptionLastPriceReader.cs
    DatabentoLastPriceReaderProvider.cs
    Contracts/
      IDatabentoLastPriceStore.cs
      DatabentoLastTradeSnapshot.cs
      DatabentoLastQuoteSnapshot.cs
  FuturesOptionStreaming/
    DatabentoFuturesOptionStreamingService.cs
    Contracts/
      IDatabentoFuturesOptionStreamingService.cs
      FuturesOptionStreamActivation.cs
  OptionChainStreaming/
    DatabentoOptionChainSessionManager.cs
    DatabentoOptionChainSession.cs
    OptionChainTickService.cs
    OptionChainStateStore.cs
    Contracts/
      IDatabentoOptionChainSessionManager.cs
      IOptionChainTickService.cs
      IOptionChainGreeksCalculator.cs
      OptionChainRiskFreeRate.cs
      IOptionChainStateStore.cs
      OptionChainLiveModels.cs
    Black76OptionChainGreeksCalculator.cs

TomasAI.IFM.Framework.MarketData/
  Contracts/LastPrice/
    ILastPriceReader.cs
  Contracts/ReferenceData/
    ITreasuryCurve.cs
    IEconomicCalendar.cs
    TreasuryCurveSnapshot.cs
    TreasuryTenor.cs
    EconomicCalendarEntry.cs
  Contracts/FuturesOptionStreaming/
    IFuturesOptionTickEventPublisher.cs
  Contracts/OptionChainStreaming/
    IOptionChainLivePublisher.cs

  # No MarketDataApi folder: the API boundary is application-owned.

TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/
  TreasuryCurve/
    FinancialModelingPrepTreasuryCurve.cs
  EconomicCalendar/
    FinancialModelingPrepEconomicCalendar.cs

```

The option quote actor event belongs in
`TomasAI.IFM.Domain.MarketData.Feed.Shared`. It is keyed by stable contract and
value-date identity; it does not reintroduce integer or Guid request IDs.

Option-chain quote/trade messages are transient service messages. They are not
event-source events, commands, tick-aggregation messages, or storage models.

## 7. Configuration

```csharp
public sealed record DatabentoMarketDataApiOptions
{
    public required string Dataset { get; init; }

    public required IReadOnlyList<string> FuturesUniverse { get; init; }

    public required IReadOnlyList<string> FuturesOptionUniverse { get; init; }

    public TimeSpan ContractQueryTimeout { get; init; } =
        TimeSpan.FromSeconds(10);

    public TimeSpan FeedSubscribeTimeout { get; init; } =
        TimeSpan.FromSeconds(30);

    public TimeSpan FeedStopTimeout { get; init; } =
        TimeSpan.FromSeconds(5);

    public int MaximumConcurrentProviderOperations { get; init; } = 2;

    public int ProviderOperationQueueCapacity { get; init; } = 64;

    public int OptionOutputCapacity { get; init; } = 1_024;

    public int OptionChainLiveOutputCapacity { get; init; } = 4_096;

    public TimeSpan OptionChainUiPublishInterval { get; init; } =
        TimeSpan.FromMilliseconds(100);

    public int MaximumConcurrentOptionChains { get; init; } = 4;

    public TimeSpan MaximumLastPriceAge { get; init; } =
        TimeSpan.FromSeconds(2);

    public TimeSpan MaximumTreasuryCurveAge { get; init; } =
        TimeSpan.FromDays(7);
}
```

The universes contain canonical IFM contract IDs. `StartAsync` resolves them to
raw Databento symbols and instrument keys for the supplied value date before a
feed is created.

There is deliberately no option-chain persistence switch. Option-chain output
is live-only in every deployment profile. Durable tick collection is enabled
and configured only through the existing `TickAggregationService` path.

Validation rejects:

- blank dataset or contract IDs;
- duplicate IDs after ordinal normalization;
- default or invalid lifecycle value dates;
- non-positive timeouts/capacities;
- non-positive pricing-snapshot or Treasury-curve age limits;
- contracts whose provider kind does not match their configured universe;
- conflicting canonical/provider mappings;
- a production profile without approved restart and qualification gates.

Credentials remain in the native/provider configuration path. They never enter
application options, logs, exceptions, metrics, or test output.

## 8. Lifecycle epoch

### 8.1 State

```text
Stopped -> Starting -> Running -> Stopping -> Stopped
              |                       |
              +-------> Faulted <-----+
```

One API singleton owns at most one `DatabentoMarketDataEpoch`. A private
`SemaphoreSlim` serializes start and stop. The epoch contains:

- `ValueDate` and definition date;
- immutable futures and option contract indexes;
- dataset-bound query client and epoch-bound hot-price reader provider;
- the bounded provider operation runner;
- one configured futures feed and `ITickAggregationService` per provider
  dataset represented in the epoch;
- one configured futures-option feed and option streaming service;
- a bounded option-chain session registry keyed by domain underlying contract
  ID and exact maturity;
- one transient option-chain state store plus live engine/UI publishers;
- active futures and option contract sets;
- one reference-counted epoch publisher shared by those dataset-specific
  aggregation services;
- feed, publisher, and application health sources.

The current `TickAggregationOptions` and feed subscriptions are immutable, so a
new epoch creates fresh service instances. At most one epoch is active; the old
epoch is fully drained and disposed before another can start.

### 8.2 Operation IDs and error callback

Every public operation creates an internal `Guid operationId`. This ID is used
for structured diagnostics and is passed to the lifecycle-epoch callback on
failure. It is not a public command identity and is never used as a Databento
instrument, stream, actor, or persistence key.

The first non-null callback supplied for an epoch is retained until
`StopAsync` completes. A callback failure is recorded separately and never
hides the primary exception.

## 9. Method-by-method implementation

### 9.1 `StartAsync`

```csharp
Task StartAsync(
    DateOnly valueDate,
    Func<Guid, int, string, Task>? errorMessageHandler = null,
    CancellationToken cancellationToken = default);
```

Behavior:

1. Validate `valueDate` and observe cancellation.
2. Serialize through the lifecycle lock.
3. If already running for the same date, return successfully without creating
   another epoch. If running for a different date, throw and require an
   explicit `StopAsync` first.
4. Create a new operation ID and epoch-scoped callback.
5. Resolve every configured futures and option canonical contract ID through
   `IDatabentoMarketDataQueries`.
6. Verify kind, expiry/maturity, raw symbol, publisher/instrument identity, and
   definition-date consistency.
7. Populate the complete futures tick mapping store.
8. For each represented dataset, create a futures ticker feed, subscribe that
   dataset's resolved raw-symbol universe, and construct its
   `TickAggregationService`. All services acquire the same reference-counted
   epoch publisher, whose transport starts once.
9. Create the option ticker feed, subscribe the resolved option raw-symbol
   universe to quote data, and construct the option streaming service.
10. Create an empty, bounded option-chain session manager and transient state
    store for dynamic domain-selected chain sessions.
11. Start provider-independent event publishers.
12. Start both configured streaming services and the bounded provider
    operation runner.
13. Publish running/readiness state only after every enabled stage is ready.

Cancellation is honored during application-controlled setup. Once a synchronous
native call has been admitted, its configured timeout remains the termination
bound. Cancellation after partial start triggers reverse-order cleanup and the
original cancellation is propagated.

Concurrent starts do not create duplicate epochs. They serialize and observe
the running result. A failed start leaves the API stopped or faulted with all
partially created resources disposed.

### 9.2 `StopAsync`

```csharp
Task StopAsync(DateOnly valueDate);
```

Behavior:

1. Validate the date and serialize through the lifecycle lock.
2. If already stopped, return successfully.
3. Require the supplied date to equal the active epoch date; a mismatch throws
   instead of stopping an unrelated trading session.
4. Stop accepting new provider operations and stream activations.
5. Stop and drain every active option-chain session, close transient live
   delivery, and clear its in-memory chain state.
6. Clear option activations so no new option events are enqueued.
7. Stop native option intake, drain option batches and the bounded publisher,
   then release the option reader/feed.
8. Concurrently await each independent dataset aggregation service's data-safe
   `StopAsync`, including managed batches and partial quote buffers. The shared
   publisher remains running while any service holds a reference and is stopped
   once, by the final release, after every accepted publisher envelope drains.
9. Drain admitted query/snapshot operations within their provider timeouts.
10. Clear active sets, publish stopped health, dispose the epoch, and clear the
   stored callback.

Failures are aggregated after all cleanup stages are attempted. A completed
stop leaves no worker, reader, feed handle, batch, buffer, publisher envelope,
or live option-chain snapshot owned by the epoch. Stop performs no
option-chain storage read or write.

### 9.3 `GetFuturesContractAsync`

```csharp
Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
    string futuresContractId);
```

The API requires a running epoch, validates the canonical ID, creates an
operation ID, and admits the synchronous provider lookup through the bounded
operation runner.

Resolution:

1. Return the immutable epoch-catalog entry when already resolved.
2. Otherwise use `ContractIdToInstrumentId(contractId)`.
3. Parse the canonical ticker and query `GetContractDetails(ticker)`.
4. Select the definition whose instrument ID matches and whose kind is
   `ContractKind.Future`.
5. Return `null` for a confirmed provider miss.
6. Throw a typed mapping/ambiguity exception for inconsistent or multiple
   definitions.
7. Cache only a successful immutable mapping for the current epoch.

Mapping:

| `FuturesContractV3ReadModel` | Databento source |
| --- | --- |
| `ContractId` | requested canonical ID verified by reverse mapping |
| `Description` | deterministic ticker/expiry description |
| `Symbol` | `Ticker` |
| `LocalSymbol` | `RawSymbol` |
| `SecurityType` | stable IFM value `FUT` |
| `Currency` | `Currency` |
| `Exchange` | `Exchange` |
| `Multiplier` | invariant `ContractMultiplier` |
| `LastTradeDate` | `MaturityDate`, otherwise validated expiration timestamp |
| `OnTheRun`, `Rollover` | always `false/false`; operational selection is assigned only by rollover policy |

Missing required fields are mapping failures and are never guessed.

### 9.4 `GetFuturesContractsAsync`

```csharp
Task<FuturesContractV3ReadModel[]> GetFuturesContractsAsync(
    string[] futuresContractIds);
```

Rules:

- reject a null array or blank element;
- return `[]` for an empty array without provider access;
- preserve input order and duplicates;
- resolve duplicate IDs once per call;
- use the same resolver and mapper as the single method;
- bound provider concurrency through the shared operation runner;
- fail the entire call with a typed not-found exception if any input is missing;
- fail the entire call on mapping ambiguity or provider error;
- never return null array elements.

The method first checks the epoch catalog, then groups unresolved IDs by ticker
so one `GetContractDetails(ticker)` result can satisfy multiple inputs.

### 9.5 `GetFuturesOptionContractAsync`

```csharp
Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
    string futuresOptionContractId);
```

Resolution:

1. Check the epoch option catalog.
2. Resolve canonical ID to instrument ID.
3. Parse ticker, maturity, right, and strike from the canonical ID using the
   existing Databento contract-ID grammar.
4. Query the exact option chain with `GetChainDefinitions`.
5. Match instrument ID, maturity, right, and exact decimal strike.
6. Return `null` for a confirmed miss and throw on ambiguity/conflict.
7. Cache a successful immutable current-epoch mapping.

Mapping:

| `FuturesOptionContractReadModel` | Databento source |
| --- | --- |
| `ContractId` | requested canonical ID verified by reverse mapping |
| `Description` | ticker/maturity/right/strike description |
| `Symbol` | `Ticker` |
| `LocalSymbol` | `RawSymbol` |
| `SecurityType` | stable IFM value `FOP` |
| `Currency` | definition currency |
| `Exchange` | definition exchange |
| `Multiplier` | invariant multiplier |
| `ContractMonth` | exact option maturity |
| `StrikePrice` | fixed-point strike converted through decimal, then checked double |
| `OptionType` | stable IFM `Call` or `Put` |

### 9.6 `GetFuturesOptionContractsAsync`

```csharp
Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
    string[] futuresOptionContractIds);
```

This has the same all-or-nothing, ordering, duplicate, and empty-array behavior
as the futures batch method. Unresolved IDs are grouped by underlying/maturity
so one option-chain query can resolve multiple strikes and rights. Every result
uses the single-method mapper and canonical reverse-mapping validation.

### 9.7 `GetFuturesOptionChainContractsAsync`

```csharp
Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(
    string futuresContractId,
    DateOnly maturityDate);
```

This is the domain-facing option-chain discovery operation. It returns every
current call and put definition for the supplied domain futures contract and
exact maturity. It deliberately accepts no strike, right, liquidity, or spread
filters; those are domain policy and belong to the MarketData Feed actor and
OptionSpread engine.

Behavior:

1. Require a running epoch, validate the domain futures contract ID, and reject
   a default maturity date.
2. Resolve `futuresContractId` to the exact provider future definition.
3. Call `IDatabentoMarketDataQueries.GetChainDefinitions` with the configured
   dataset, exact maturity, `OptionUniversePolicy.UnderlyingFuture`, and
   `OptionRightSelection.Both`.
4. Verify every returned definition belongs to the resolved provider future
   and exact maturity. A mismatched definition is a provider-mapping failure;
   it is never silently filtered.
5. Hydrate any domain-required metadata not present in
   `OptionContractDefinition`, including currency and exchange, with one
   bounded batch `GetContractDetails` call rather than one provider call per
   option.
6. Convert every provider instrument to its canonical domain option contract
   ID and verify the forward/reverse mapping.
7. Map every definition through the same
   `FuturesOptionContractReadModel` mapper used by the single and batch lookup
   methods.
8. Deduplicate only exact duplicate provider instrument/raw-symbol entries and
   sort by strike, option type, then canonical contract ID.
9. Return `[]` when the provider confirms that the chain has no definitions.
   Throw on timeout, provider failure, ambiguity, incomplete required metadata,
   or conflicting identity.

The returned array is a discovery result, not a live snapshot and not a
persisted chain. The caller may filter it and pass the selected domain contract
IDs to `StartStreamingFuturesOptionChainDataAsync`.

### 9.8 `GetFuturesPriceAsync`

```csharp
Task<decimal> GetFuturesPriceAsync(string futuresContractId);
```

Require a running epoch and resolve the ID as a configured futures contract.
Obtain the same reader returned by `GetFuturesLastPriceReader` and call
`TryGetLastTrade`. Return its exact decimal trade price when the snapshot
belongs to the current contract/value date and is within
`MaximumLastPriceAge`. When no qualifying trade exists, call `TryGetLastQuote`
and return the exact midpoint of a fresh, positive, non-crossed, two-sided quote.

Because the return type is non-nullable, the absence of both a qualifying trade
and quote midpoint produces `FuturesLastPriceUnavailableException`; it never
returns zero and never falls back to a provider query, replay, actor, or storage
read. The
method retains `Task<decimal>` for application-contract compatibility even
though the completed implementation is an in-memory read.

### 9.9 `GetFuturesOptionPriceAsync`

```csharp
Task<decimal?> GetFuturesOptionPriceAsync(string futuresOptionContractId);
```

Require a running epoch, resolve the ID as a configured futures option, obtain
the same reader returned by `GetFuturesOptionLastPriceReader`, and call
`TryGetLastQuote`. The snapshot must confirm:

- bid and ask are both valid;
- neither side is Databento's undefined sentinel;
- bid is not greater than ask;
- contract ID and value date match the current epoch; and
- the quote is within `MaximumLastPriceAge`.

`LastQuoteTickSnapshot.TryGetMidpoint` performs the exact decimal midpoint.

Return behavior:

- return the decimal midpoint for a qualifying quote;
- return `null` when no quote exists or the latest quote is stale/one-sided;
- throw on crossed data or mapping/epoch identity conflict;
- never use zero as a missing-value sentinel.

There is no provider query, replay, actor, or storage fallback. If the option is
not present in a running individual-option or option-chain route, its reader
has no current quote and the method returns `null`.

### 9.9a Hot-cache snapshots and stream activity

```csharp
bool TryGetLastTickPrice(
    string contractId,
    out FuturesMarketPriceSnapshot snapshot);

bool TryGetLastOptionTickPrice(
    string contractId,
    out OptionTickerPriceSnapshot snapshot);

bool IsTickDataStreamActive(string contractId);
```

These are provider-neutral, stream-independent hot-cache reads for timer-derived and other sampling consumers. They delegate through the active epoch to TickAggregation and never register an owner, activate a transient route, or extend stream lifetime. The option operation returns the same normalized trade/quote view plus optional Greeks only when the enrichment sequence aligns with the selected observation.

TickAggregation stores the same normalized combined snapshot used by `FuturesMarketPriceUpdatedRealtimeEvent`. Quote observations refresh the cached quote side; accepted trade observations refresh the trade side and publish the Core NATS realtime event containing that exact snapshot. An accepted VX quote also publishes the event immediately with `UpdateSource = Quote`; this is the sparse-trade fallback used by VX EOD and UI bars and does not wait for the pooled quote-storage batch. ES and futures-option quotes do not add this realtime publication load. Duplicate or older observations do not replace the cached side. A price method returns `false` for an unknown contract or before the first observation. It performs no provider query, database access, or replay.

`IsTickDataStreamActive` checks the owner-keyed runtime registration pool. A client requiring live data checks it before using a cached snapshot, but this is not enforced by either price method. Therefore an inactive stream can still expose its last observation, while an active stream can temporarily have no snapshot before its first tick arrives.

### 9.9b On-the-run and rollover-set futures registry

```csharp
bool TryGetOnTheRunFuturesContract(
    string symbol,
    out FuturesContractV3ReadModel contract);

Task<bool> UpdateOnTheRunFuturesContractAsync(
    string symbol,
    DateOnly valueDate,
    CancellationToken cancellationToken = default,
    bool forceProviderRefresh = false);

bool TryGetFuturesTermStructureContracts(
    string symbol,
    out FuturesTermStructureContracts contracts);

Task<bool> UpdateFuturesTermStructureContractsAsync(
    string symbol,
    DateOnly valueDate,
    CancellationToken cancellationToken = default);
```

Startup reconciliation publishes one immutable runtime state per root containing DataBento registrations and the complete verified rollover set. `TryGetOnTheRunFuturesContract` is a case-insensitive, allocation-free in-memory lookup of the singular primary contract. The VX term-structure lookup derives front and back from the same snapshot. ES has exactly one `true/true` row; VX has a `true/true` front and `false/true` back. Updates consult DataBento and atomically persist a replacement only when the authoritative rollover pointer is incomplete or due. This permits restart from a valid persisted assignment when the historical provider is temporarily unavailable. Explicit operator workflows may force early provider revalidation. See `Documents/system/Futures-Contract-Rollover-Startup.md` for preparation, admission, persistence and effective-value-date rules.

The epoch builds tick mappings from the runtime feed mode. Live feeds use the
publisher/instrument identities returned by DataBento definitions. Synthetic
feeds assign deterministic publisher `1` and a dataset-local one-based
instrument sequence matching the immutable subscription order. The catalog
metadata remains live/provider-authored in Development, but synthetic records
must never be routed through those live instrument keys.

### 9.10 `GetFuturesLastPriceReader`

```csharp
IFuturesLastPriceReader GetFuturesLastPriceReader(
    string futuresContractId);
```

Require a running epoch, resolve the canonical ID as a futures contract, and
return the epoch's stable DI-selected DataBento reader over the matching
latest-value slot. Repeated calls for the same contract in one epoch return the
same reader instance. Getting a reader does not start or activate a provider
subscription. Until DataBento observes a qualifying record, both `TryGet`
operations return `false`.

### 9.11 `GetFuturesOptionLastPriceReader`

```csharp
IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(
    string futuresOptionContractId);
```

Behavior matches the futures reader but enforces futures-option contract kind.
The multi-asset aggregation path owns one slot per domain option contract.
Getting a reader has no subscription side effect. Its raw quote/trade methods
are available as soon as those ticks are admitted. Its enriched methods are
available only after a tick and its Greeks result have been atomically
published into the slot.

`TryGetLastQuoteWithGreeks` never combines a newer quote with an older
calculation. `TryGetLastTradeWithGreeks` returns the latest trade with the
quote-derived Greeks state current when that trade was processed; therefore
the trade source sequence and `Greeks.OptionPriceSourceSequence` normally
differ. A failed calculation is still an available enriched result and returns
`true` with `Greeks.IsValid == false`. No calculation work occurs in any reader
method.

Both reader types are epoch-bound. After epoch stop, every raw and enriched
operation on existing handles returns `false`; they never attach themselves to
a later value date. Unknown IDs,
wrong-kind IDs, stopped APIs, and reader-capacity exhaustion throw typed
exceptions from the `Get` method, not from a subsequent hot read.

### 9.11a Workflow-owned stream registration

The asset-specific start and stop methods accept an optional `TickerStreamOwner`. The stable tuple `(contract ID, workflow type, workflow ID, leg ID)` is the idempotency key. TickAggregation stores owners in a set: the first owner activates transient routing, overlapping owners share it, and the final owner removal deactivates it. Calls that omit an owner use the application compatibility owner and retain the existing idempotent single-caller behavior.

Stream registration and hot-cache access are independent. Actor handlers save the contract supplied by their streaming-started event and remove it on the corresponding stopped event. No disposable ticker reader, lease ID, or stream generation crosses the application boundary.

### 9.12 `StartStreamingFuturesTickDataAsync`

```csharp
Task<bool> StartStreamingFuturesTickDataAsync(
    string futuresContractId,
    TickerStreamOwner? owner = null);
```

The futures provider universe and aggregation service are already running for
the epoch. This method controls application activation:

1. Require a running epoch and validate the canonical contract ID.
2. Resolve it as a configured futures contract; unknown or option IDs throw.
3. Atomically add the contract to the active futures set.
4. Register the contract with the downstream live-delivery router.
5. Return `true` when inactive became active.
6. Return `true` when the supplied owner is added or `false` when that owner was already registered.

The existing `ITickAggregationService` continues system-wide ingestion and
persistence for every configured futures contract. Activation controls live
consumer delivery, not durable collection. No feed reconnection occurs.

The returned task completes only after downstream activation is visible. It
does not report success before the router is ready.

### 9.13 `StopStreamingFuturesTickDataAsync`

```csharp
Task<bool> StopStreamingFuturesTickDataAsync(
    string futuresContractId,
    TickerStreamOwner? owner = null);
```

This removes the contract from the active futures set and awaits downstream
router deactivation.

- return `true` when active became inactive;
- return `false` when already inactive;
- throw for an invalid or wrong-kind contract;
- do not stop the provider feed or futures persistence;
- allow events accepted before the deactivation linearization point to finish
  under normal at-least-once semantics;
- ensure no later event is routed as an active live update after completion.

### 9.14 `StartStreamingFuturesOptionTickDataAsync`

```csharp
Task<bool> StartStreamingFuturesOptionTickDataAsync(
    string futuresOptionContractId,
    TickerStreamOwner? owner = null);
```

The option feed is preconfigured and running, while option publication is
activation-gated per contract:

1. Require a running epoch.
2. Resolve the ID as a configured futures option.
3. Atomically add the instrument to the option service's active immutable
   snapshot.
4. Await publisher/router acknowledgement that activation is visible.
5. Return `true` when the supplied owner is added or `false` when that owner was already registered.

Once active, every accepted quote record for that instrument is converted to a
contract/value-date-keyed option bid/ask event and sent through a bounded,
ordered publisher. Underlying futures lookup, risk-free-rate lookup, option
pricing, and trade updates remain downstream concerns. This live option route
does not persist ticks; all durable tick persistence is explicitly owned by
`TickAggregationService`.

### 9.15 `StopStreamingFuturesOptionTickDataAsync`

```csharp
Task<bool> StopStreamingFuturesOptionTickDataAsync(
    string futuresOptionContractId,
    TickerStreamOwner? owner = null);
```

This removes the option instrument from the active snapshot and awaits the
deactivation barrier.

- return `true` when active became inactive;
- return `false` when already inactive;
- no new event for that contract may be enqueued after task completion;
- already enqueued/transport-accepted events complete normally;
- the preconfigured option provider feed remains running for other contracts
  and until epoch stop.

### 9.16 `StartStreamingFuturesOptionChainDataAsync`

```csharp
Task<bool> StartStreamingFuturesOptionChainDataAsync(
    string futuresContractId,
    DateOnly maturityDate,
    string[] optionContractIds);
```

All identifiers at this boundary are canonical domain identifiers:

- `futuresContractId` is the domain contract ID of the underlying futures
  contract;
- `maturityDate` is the exact option-chain maturity;
- every value in `optionContractIds` is a domain futures-option contract ID.

No Databento raw symbol, ticker, publisher ID, instrument ID, universe policy,
or provider request model is exposed to the domain caller.

Behavior:

1. Require a running epoch and a non-default maturity date.
2. Validate `futuresContractId` as a futures contract and resolve it through
   the epoch catalog to the exact Databento underlying definition.
3. Call `ITickAggregationService.GetTickerStatus(futuresContractId)`.
4. Throw `TickAggregationNotRunningException` when `ServiceRunning` is false.
   Throw `UnderlyingTickerNotRunningException` when the contract is not
   configured or `TickerRunning` is false. Do not create a chain feed, query
   Treasury data, or reserve chain capacity before these checks pass.
5. Reject a null/empty option array or blank option ID; clone the array before
   the first await so caller mutation cannot change the request.
6. Resolve distinct option IDs through the option catalog/batch resolver while
   preserving the caller's requested set for diagnostics.
7. Verify every option belongs to the supplied domain underlying, has exactly
   `maturityDate`, and has a valid call/put right, strike, raw symbol, and
   provider instrument key.
8. Derive the framework selectors from the resolved domain contracts:
   distinct strikes, combined call/put rights, and resolved contract
   definitions.
9. In `DatabentoMarketDataApi`, use the application-owned Treasury cache/API to
   resolve the latest curve whose curve date is not after `valueDate`, select
   the approved DTE tenor for `maturityDate`, and produce the fixed session
   risk-free rate. Neither call crosses into the DataBento framework.
10. Resolve the underlying's slot in the epoch-owned DataBento latest-value
   store and require a current quote midpoint or last trade within
   `MaximumLastPriceAge`.
11. Acquire the epoch's tick/chain admission lease and recheck the service and
    ticker status. This lease prevents underlying aggregation stop from racing
    chain admission.
12. While holding that bounded lease, create one `IDatabentoOptionChainFeed`,
    call `Subscribe` with an `OptionChainSubscription` containing only
    provider-resolved values, and create one `OptionChainTickService`. Pass the
    fixed risk-free-rate value,
    shared DataBento latest-value store, Black-76 calculator, transient state
    store, and live publisher, start its worker, then start the framework feed
    and verify health.
13. Publish the session atomically under
   `(futuresContractId, maturityDate)` only after all stages are ready.
14. Return `true` when a new session starts. Return `false` when an identical
    domain option set is already running.

If the same underlying/maturity key is running with a different option set,
the method throws a typed chain-conflict exception. The caller must stop the
old immutable provider session before starting a different selection.

An option contract cannot simultaneously publish through its individual
option-tick activation and an option-chain session. Both start paths check a
shared route-ownership registry and reject overlap, preventing duplicate domain
events from two provider subscriptions.

The chain subscribes to `MarketDataKinds.Quote | MarketDataKinds.Trade`.
`OptionChainTickService` maintains only the latest live quote/trade state per
domain option contract. It publishes separate transient option-chain quote and
trade messages to the OptionSpread engine and throttled UI delivery. It never
publishes tick-aggregation events and never invokes event-source, Scylla, or
other durable storage APIs. MBO remains outside this contract.

### 9.17 `StopStreamingFuturesOptionChainDataAsync`

```csharp
Task<bool> StopStreamingFuturesOptionChainDataAsync(
    string futuresContractId,
    DateOnly maturityDate);
```

The domain underlying contract ID plus exact option maturity forms the chain
session key.

Behavior:

1. Validate the domain futures contract ID and maturity.
2. Atomically remove the matching session from new route admission.
3. Return `false` when no session exists.
4. Stop native chain intake, drain the framework shared reader and transient
   live publisher, and return every batch/envelope.
5. Stop `OptionChainTickService` and clear the session's in-memory snapshot.
6. Dispose the `IDatabentoOptionChainFeed` only after its stop completes.
7. Release route ownership for every selected option.
8. Return `true` after the entire session is stopped and disposed.

Transient messages already accepted by live consumers may complete normally.
There is no durable replay or recovery for chain messages. No new chain message
may be enqueued after the returned task completes.

## 10. Contract resolver

`DatabentoContractResolver` is the single source of application/provider
mapping. It uses an immutable epoch catalog plus bounded provider queries.

Each resolved entry contains:

```text
CanonicalContractId
ContractKind
Dataset
DefinitionDate
RawSymbol
Ticker
Underlying
PublisherId
InstrumentId
MaturityDate
OptionRight (when applicable)
StrikePrice (when applicable)
Currency
Exchange
ContractMultiplier
Activation/expiration timestamps
```

Rules:

- canonical IDs use the existing Databento grammar and ordinal comparison;
- every forward mapping is verified by reverse mapping;
- Databento instrument ID is definition-date scoped, never permanent identity;
- no lookup guesses a raw symbol, contract kind, expiry, or exchange;
- successful entries are immutable for the epoch;
- misses and provider failures are not cached as successful values;
- conflicting mappings fault epoch readiness for configured contracts.

## 11. Futures-option and option-chain live streaming services

### 11.1 Responsibility

`DatabentoFuturesOptionStreamingService` is the bounded boundary between one
multiplexed Databento option ticker feed and MarketData Feed option events.

It owns:

- one `IDatabentoTickerFeed` configured with every approved option raw symbol;
- one `IMultiplexedTickerBatchReader`;
- one worker task;
- immutable instrument-to-contract mappings;
- an atomically replaced active-instrument snapshot;
- one bounded, single-reader option event publisher.

It does not own lifecycle dates, contract queries, Blackboard, Greeks, domain
commands, or storage.

### 11.2 Processing rules

- consume quote data only in V1;
- preserve valid source order per instrument;
- preserve one-sided quote semantics and raw fixed-point values;
- publish every accepted qualifying quote for an active contract;
- tag events with canonical contract ID, epoch value date, dataset, definition
  date, publisher ID, instrument ID, source/receive timestamps, bid/ask, and
  sizes;
- count inactive-contract observations separately from loss/failure;
- never silently drop when the output channel is full;
- apply bounded backpressure and fault visibly on exceeded deadlines;
- never create a thread, feed, query, or task per option/tick.

### 11.3 Event identity

The provider event is keyed by stable
`FuturesOptionContractId + ValueDate`, not a request ID. Its event ID is created
once before publication and reused on retry. Downstream idempotency uses that
stable event identity plus provider source identity/timestamp according to the
approved option event schema.

This removes the legacy dependence on integer broker request IDs without
introducing public Guid parameters into `IMarketDataApi`.

### 11.4 Lifecycle

Start order:

1. validate complete mappings and fixed universe concurrently across the
   independent dataset runners;
2. start the provider-independent publisher;
3. acquire the multiplexed reader;
4. start the waiting worker;
5. start the Databento feed;
6. report running/readiness.

Stop performs the reverse data-safe order: stop native intake, drain managed
batches, close activation, drain publisher events, release reader/batches, stop
publisher, dispose feed. Independent dataset feeds stop concurrently with the
five-second actor default. An incomplete bounded feed stop returns its typed
failure without awaiting an output worker whose channel cannot yet complete;
the feed retains ownership and a later stop can retry. The API Server's
Development deployment paces each synthetic dataset at ten records per second,
preventing a qualification-style burst from masquerading as a workstation
lifecycle failure.

### 11.5 Option-chain session manager and tick service

`DatabentoOptionChainSessionManager` adapts the existing framework
`IDatabentoOptionChainFeed` to domain-level chain requests. It owns a bounded
dictionary keyed by:

```text
(UnderlyingDomainContractId, OptionMaturityDate)
```

For each session it owns exactly one framework chain feed, its shared batch
reader, one `OptionChainTickService`, one transient state partition, and one
bounded live publisher. The framework feed already supports many resolved
option contracts in one native session and preserves their session order
through the shared reader.

The manager never forwards domain contract IDs directly to Databento. It first
resolves the domain underlying and option IDs, verifies their relationship, and
constructs:

```csharp
new OptionChainSubscription
{
    Underlying = providerUnderlying,
    MaturityDate = maturityDate,
    Strikes = resolvedOptions
        .Select(option => option.StrikePrice)
        .Distinct()
        .ToArray(),
    Rights = resolvedRights,
    ResolvedContracts = providerDefinitions,
    DataKinds = MarketDataKinds.Quote | MarketDataKinds.Trade
};
```

The session count is limited by `MaximumConcurrentOptionChains`. Exceeding the
limit throws an explicit capacity exception before a feed is created. Starting
or stopping one chain never reconfigures another chain.

The authoritative dependency check is:

```csharp
public readonly record struct TickAggregationTickerStatus(
    string FuturesContractId,
    bool ServiceRunning,
    bool TickerConfigured,
    bool TickerRunning);

public interface ITickAggregationService
{
    bool IsRunning { get; }
    TickAggregationTickerStatus GetTickerStatus(
        string futuresContractId);
}
```

`TickerConfigured` means the canonical contract is mapped to one of the
futures instruments admitted from the current feed's registrations.
`TickerRunning` is true only when the aggregation service worker is live, not
stopping, and that contract is configured. This status is lifecycle/admission
state; it is not inferred from a cached price, because an otherwise healthy
futures contract may legitimately receive no trade for a period.

Each chain records its underlying dependency. If the aggregation worker faults,
the service begins stopping, or a future implementation removes the underlying
ticker, the session manager stops native chain intake, drains accepted chain
records, publishes a typed dependency-lost failure/stopped event, clears its
transient state, and releases the session. It never continues calculating from
the last cached underlying price.

Planned epoch shutdown stops all option-chain dependents before stopping
`TickAggregationService`. A per-ticker removal request is rejected while a
dependent chain exists; the caller must stop those chains first. The existing
`StopStreamingFuturesTickDataAsync` application-delivery gate does not by itself
remove the ticker from the fixed system aggregation feed, so it does not make
`TickerRunning` false.

`OptionChainTickService` demultiplexes each shared batch by provider instrument
identity, maps it back to the domain option contract ID, and maintains
per-contract ordering and source-sequence diagnostics. Quote records replace
the latest bid/ask state. Trade records replace the latest trade state. The
service performs no spread calculation, actor persistence, or database work.
The native callback and shared-reader drain remain limited to bounded record
transfer. Black-76 enrichment runs on the managed option-chain worker after a
record has been drained, so pricing cannot block the provider callback.

### 11.6 Transient option-chain state and messages

The epoch-owned `IOptionChainStateStore` is a single-writer, multi-reader live
view keyed first by `(FuturesContractId, MaturityDate)` and then by
`OptionContractId`. Each option entry contains its static strike/right plus the
latest quote, latest trade, provider timestamps, receive timestamp, and source
sequence. State replacement is atomic so the OptionSpread engine never sees a
partially updated entry.

Two separate transient message families are published:

```text
FuturesOptionChainQuoteChangedServiceEvent
FuturesOptionChainTradeChangedServiceEvent
```

Both identify the domain futures contract, maturity, domain option contract,
value date, and source sequence. They are live service events, not event-source
events. Both also carry one immutable `OptionGreeksSnapshot` value. The
OptionSpread engine receives ordered contract deltas and can filter candidates
directly by live Delta without another provider API. UI output is coalesced and
published at `OptionChainUiPublishInterval`, so the UI does not receive one
message for every provider record. A consumer that detects a sequence gap
reloads the current in-memory snapshot; no durable replay exists.

### 11.7 Black-76 implied volatility and Greeks enrichment

Greeks are derived analytics, not Databento fields. The raw
`IDatabentoOptionChainFeed` remains provider-only. The managed
`OptionChainTickService` invokes an allocation-free
`IOptionChainGreeksCalculator`; its production adapter calls the Black-76
`FuturesOptionGreeksCalculator.CalculateFromMarketPrice` implementation in
`TomasAI.IFM.Framework.OptionPricer`. The legacy QLNet calculator and
`IronCondorSpreadDistributionJobService` are outside this design.

Each transient quote/trade event contains this logical payload:

```csharp
public readonly record struct OptionGreeksSnapshot(
    bool IsValid,
    bool IsStale,
    OptionGreeksFailureReason FailureReason,
    OptionGreeksPriceSource PriceSource,
    string FuturesContractId,
    decimal? FuturesPrice,
    decimal? OptionMarkPrice,
    double? RiskFreeRate,
    double? TimeToExpiryYears,
    double? ImpliedVolatility,
    double? TheoreticalPrice,
    double? Delta,
    double? Gamma,
    double? Vega,
    double? Theta,
    double? Rho,
    int SolverIterations,
    long FuturesPriceSourceSequence,
    long OptionPriceSourceSequence,
    DateTimeOffset FuturesPriceTimestamp,
    DateTimeOffset OptionPriceTimestamp,
    DateTimeOffset CalculatedAtUtc);
```

This type, `OptionGreeksFailureReason`, `OptionGreeksPriceSource`,
`LastQuoteTickWithGreeksSnapshot`, and `LastTradeTickWithGreeksSnapshot` belong
to `Framework.MarketData.Contracts.LastPrice`. The transient option-chain
service events and the option hot reader use the same immutable value; neither
exposes `Framework.OptionPricer` implementation types. The failure enum covers
missing, invalid, and stale market inputs as well as mapped Black-76 solver
failures. Invalid results use nullable values and a typed failure reason. Zero
is never used as a missing, failed, or not-yet-calculated sentinel.

Calculation policy:

1. The current futures price comes directly from the underlying's slot in the
   epoch-owned DataBento latest-value store. The option-chain worker performs a
   synchronous, non-consuming local-memory read and never calls
   `GetFuturesPriceAsync`, Blackboard, Redis, a provider query, an actor, or
   storage per option update.
2. Strike, right, maturity, and multiplier metadata come from the immutable
   resolved option definition. Black-76 receives the unmultiplied option price.
3. V1 uses the Option Pricer's documented European futures-option and
   Actual/365 Fixed conventions. The application-selected immutable session
   rate is passed by value as `OptionChainRiskFreeRate` and supplied to
   Black-76 as an annual decimal rate under the approved V1 yield convention.
4. A quote calculation requires finite, positive, non-crossed bid and ask
   values and uses their midpoint as `OptionMarkPrice`.
5. The calculator first solves implied volatility, warm-started with that
   option's last valid implied volatility, and then calls `PriceWithGreeks`
   exactly once with the converged value.
6. A quote event carries the newly calculated snapshot. A trade event carries
   the latest quote-derived snapshot for that option and preserves its source
   and calculation timestamp; V1 does not invert volatility from the last
   trade because a stale or off-market trade is not a reliable valuation mark.
7. A trade received before a valid quote is still published, with
   `IsValid == false`, nullable calculated values, and a typed
   `NoValidQuote`/equivalent status.
8. When quote inputs or the futures-price snapshot are invalid or stale, the
   event is still published with an explicit failed/stale snapshot. The state
   store may retain the prior valid snapshot for display, but it must mark that
   snapshot stale and must not present it as calculated from the new record.

Closed-form Black-76 Greeks are inexpensive, but implied-volatility inversion
is iterative. The implementation therefore calculates only after a relevant
option price change, reuses the prior implied volatility as the solver's
initial guess, performs no allocation or task creation per record, and records
solver iterations/failures. Capacity and latency benchmarks, not assumption,
determine whether enrichment meets the qualified throughput targets.

### 11.8 DataBento hot-price readers and external rate inputs

#### DataBento latest-value store

One `IDatabentoLastPriceStore` belongs to the application epoch and is updated
by the asset-neutral `TickAggregationService` for admitted futures and
futures-option ticks. Option-chain enrichment augments the same option slot
with an atomic tick-with-Greeks view. It is keyed by:

```text
(DomainContractId, ValueDate, ContractKind)
```

Each bounded slot independently holds the latest trade and latest quote,
including raw fixed-point prices, sizes/counts, source sequence, provider event
and receive timestamps, and source instrument identity. An option slot also
holds the latest quote paired with the calculation for that exact quote, plus
the latest trade paired with the quote-derived Greeks state current when the
trade was processed. Managed processors replace the slot immediately after
record validation/demultiplexing and enrichment and before publishing their
actor or transient event. The native callback only transfers records and never
performs cache work.

Slots are created from the resolved epoch catalog or explicit chain admission;
there is no unbounded create-on-tick behavior. Route ownership guarantees one
writer for a contract at a time. Readers may be concurrent. An older source
sequence/timestamp cannot replace a newer value.

The implementation stores atomic raw integral fields and converts to the
provider-neutral decimal snapshot on read. A versioned/seqlock slot or an
equivalently benchmarked design prevents torn quote/trade or tick/Greeks reads
without a lock, heap allocation, task, or channel operation per update/read.

#### Framework contracts and DI-selected readers

`IFuturesLastPriceReader`, `IFuturesOptionLastPriceReader`, the raw snapshots,
`OptionGreeksSnapshot`, and the two tick-with-Greeks snapshots belong to
`Framework.MarketData.Contracts.LastPrice`. DataBento implements these
provider-neutral contracts directly:

```text
DatabentoFuturesLastPriceReader
    implements IFuturesLastPriceReader

DatabentoFuturesOptionLastPriceReader
    implements IFuturesOptionLastPriceReader
```

Startup registration selects the provider implementation. The DataBento
registration installs its epoch store/provider factory; `DatabentoMarketDataApi`
asks the current epoch provider for a contract-bound reader. DI does not create
one root singleton per contract, and readers are not service-locator lookups on
the tick path.

The readers map timestamps/fixed-point values to the provider-neutral raw and
enriched snapshots; provider identifiers do not become lookup arguments. Reads
do not consume or advance the slot. Greeks are calculated on admitted option
updates when a pricing context is available, never inside a reader call.

The same option slot is used whether the option participates in individual
live delivery or an option-chain session. Each returned reader is bound to one
contract and epoch. After stop/rollover all four option `TryGet` operations
return `false`, never a prior-date value and never data from a later epoch.

#### Black-76 forward-price selection

`OptionChainTickService` reads the underlying futures slot directly. For each
option valuation it selects:

1. the current valid, positive, non-crossed futures quote midpoint when both
   sides are within `MaximumLastPriceAge`;
2. otherwise the current valid futures last trade within that age; or
3. an explicit unavailable/stale Greeks result when neither input qualifies.

The selected source and timestamp are copied into `OptionGreeksSnapshot`.
There is no Blackboard, Redis, actor, provider query, or application callback
on this path.

#### Provider-neutral external-data contracts

`Framework.MarketData.Contracts` defines provider-neutral contracts equivalent
to:

```csharp
public interface ITreasuryCurve
{
    Task<TreasuryCurveSnapshot?> GetLatestAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

public interface IEconomicCalendar
{
    Task<IReadOnlyList<EconomicCalendarEntry>> GetAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string>? countryCodes = null,
        CancellationToken cancellationToken = default);
}
```

`FinancialModelingPrepTreasuryCurve` and
`FinancialModelingPrepEconomicCalendar` implement these contracts in
`Framework.MarketData.FinancialModelingPrep`. They own FMP HTTP/DTO/mapping
behavior. `Framework.MarketData.DataBento` does not implement these contracts,
because DataBento is not the source of FMP Treasury or economic-calendar data.
The application consumes `ITreasuryCurve`, selects the rate, and passes the
result into the DataBento session by value. The economic calendar is part of
the common FMP boundary but is not an input to Black-76.

The application may wrap `ITreasuryCurve` with its existing cache-aside
mechanism. This is a low-frequency application concern and is not part of the
DataBento latest-price reader. It caches the complete curve by curve date;
the existing date-only scalar `MarketData.RiskFreeRate` cache is not used for
option chains because it cannot distinguish maturities and treats a miss as
zero. A derived selected rate need not be cached separately; if it is, its key
must contain both value date and maturity/tenor.

#### Curve-date and tenor selection

Rate resolution is deterministic:

1. Calculate `DTE = maturityDate.DayNumber - valueDate.DayNumber`; reject
   non-positive DTE for a new live session.
2. Select the most recent available FMP curve date that is less than or equal
   to `valueDate`. Never use a later curve date, which would introduce
   look-ahead. Weekend/holiday value dates therefore use the preceding
   published curve subject to `MaximumTreasuryCurveAge`.
3. Select the shortest available Treasury tenor whose nominal day count is
   greater than or equal to DTE: 1M/30, 2M/60, 3M/90, 6M/180, 1Y/365,
   2Y/730, 3Y/1095, 5Y/1825, 7Y/2555, 10Y/3650, 20Y/7300, or 30Y/10950.
   Thus DTE 1-30 uses 1M, 31-60 uses 2M, and so on.
4. This is a ceiling-bucket rule, not ambiguous absolute-nearest selection.
   Linear interpolation between bracketing tenors may replace it in a later
   approved version without changing the provider contracts.
5. Convert the FMP percentage to a decimal exactly once. V1 documents that the
   selected Treasury yield is used as a continuously compounded Black-76 rate
   approximation. A bootstrapped zero/discount curve is a later quantitative
   enhancement; it must not be introduced silently.
6. DTE beyond the longest available tenor or a missing, stale, non-finite, or
   invalid curve fails chain start with a typed pricing-input exception. It
   never silently substitutes zero.

The selected curve date, tenor, raw percentage, converted rate, and resolution
timestamp are copied into the immutable chain session and included in Greeks
diagnostics. Rates are not fetched or reselected for every option record. V1
keeps the rate fixed for the session; applying a newly published curve requires
a controlled stop/start so one event stream never silently changes convention.

### 11.9 Persistence boundary

Option-chain processing has no persistence mode and no storage dependency. It
must not:

- publish `FuturesTickQuoteDataChangedEvent` or
  `FuturesTickTradeDataChangedEvent`;
- send messages to `TickAggregationEventActor` or
  `TickAggregationCommandActor`;
- call event-source, Scylla, Redis persistence, or tick-storage APIs;
- write chain membership, snapshots, quotes, trades, or calculated spreads.

`TickAggregationService` remains the only service authorized to produce
durable tick persistence. Sharing normalized payload structs does not share
message routing or persistence ownership. Strategy selections, signals,
orders, and trades may be persisted by their owning business domains after the
OptionSpread engine makes a decision; they are not option-chain market-data
persistence.

## 12. Provider operation runner

Databento contract-query APIs are synchronous by design, while the application
contract is asynchronous. `IDatabentoOperationRunner` provides:

- bounded admission;
- fixed maximum concurrency;
- stored and observed workers;
- per-operation provider timeout;
- explicit queue-overload exception;
- graceful stop and admitted-operation drain;
- queue wait, execution, timeout, and failure metrics.

It does not create an unbounded `Task.Run` per request. Contract lookups have no
cancellation parameter, so configured provider timeouts are their mandatory
completion bound. Price methods do not use this runner; they read current
epoch-local reader snapshots.

## 13. Error behavior

### 13.1 Result semantics

| Result | Meaning |
| --- | --- |
| single contract `null` | confirmed provider miss |
| option price `null` | confirmed absence of a qualifying option quote |
| streaming `true` | activation state changed |
| streaming `false` | requested activation state already existed |
| exception | invalid input, wrong contract kind, tick aggregation/ticker dependency unavailable, not running, overload, timeout, mapping conflict, native failure, or publication failure |

Batch methods never return null elements. Futures price never returns a
missing-value sentinel.

### 13.2 Error callback

Every operation creates an internal operation ID and records the error before
invoking the epoch callback:

```text
callback(operationId, stableErrorCode, safeMessage)
```

Suggested stable ranges:

| Range | Category |
| ---: | --- |
| 7100-7199 | lifecycle/configuration |
| 7200-7299 | contract lookup/mapping |
| 7300-7399 | snapshot price |
| 7400-7499 | futures activation |
| 7500-7599 | option activation/publication |

Callback failure is recorded separately. Provider credentials, raw payloads,
and high-cardinality collections never appear in error messages.

## 14. Health and metrics

Health distinguishes:

- application epoch state and value date;
- contract-catalog readiness;
- futures mapping and restart-recovery readiness;
- futures feed/aggregation/publisher state;
- option feed/worker/publisher state;
- active option-chain count and per-session feed/reader/live-publisher state;
- option-chain snapshot age, live consumer lag, and UI coalescing state;
- underlying futures-price input age and option-Greeks snapshot age;
- DataBento latest-value slot count, freshness, and reader availability;
- per-contract tick-aggregation configured/running status and chain dependency
  loss;
- operation-runner capacity, depth, and oldest wait;
- active futures and option counts;
- ring/channel utilization and backpressure duration;
- source sequence anomalies and publication failures;
- outstanding pooled buffers/batches;
- last successful lookup, price, activation, and stop.

Metrics include lifecycle duration/outcome, contract query duration/result,
batch size, hot-price read method/duration/result, operation queue pressure,
activation changes, option inactive observations, live publisher
latency/failure, option-chain quote/trade counts, stale snapshot counts, UI
coalescing counts, Greeks calculation duration, solver iteration count,
calculation success/failure reason, stale-input count, and sampled existing
Databento/aggregation metrics. Latest-value metrics include quote/trade slot
updates, stale/out-of-order rejection, read success/miss/retry, reader count,
slot capacity, and post-stop reads. Dependency metrics include chain-start
rejection by reason and running-chain dependency loss/forced-stop outcome. There
are no option-chain persistence metrics because the chain path performs no
storage operations.

Metric tags may include provider, dataset, operation, result, contract kind,
and deployment profile. Contract IDs, raw symbols, instrument IDs, operation
IDs, and value dates are structured logs, not metric tags.

## 15. Dependency injection

```csharp
services.AddDatabentoMarketDataServices(configuration);
services.AddApplicationMarketDataApi(configuration);
```

`AddDatabentoMarketDataServices` belongs to
`Framework.MarketData.DataBento` and binds framework service contracts to
DataBento implementations. `AddApplicationMarketDataApi` belongs to
`Application.MarketData`; it constructs the application orchestration service
from those abstractions and is the only registration that binds
`IMarketDataApi`.

| Service | Lifetime |
| --- | --- |
| `DatabentoMarketDataApi` | Singleton |
| `IMarketDataApi` | Same singleton |
| `IDatabentoFeedFactory` | Singleton |
| epoch factory | Singleton |
| operation runner factory | Singleton |
| diagnostics/metrics | Singleton |
| feeds, readers, aggregation services | One owned set per running epoch |
| option-chain session manager | One per running epoch; bounded child sessions |
| option-chain tick service | One per active option-chain session |
| option-chain state store | One transient store per running epoch |
| option-chain risk-free-rate value | One immutable value per active chain session |
| Black-76 option-chain calculator adapter | Singleton, stateless |
| `ITreasuryCurve` FMP implementation | Singleton typed HTTP client |
| optional `ITreasuryCurve` caching decorator | Singleton application service |
| `IEconomicCalendar` FMP implementation | Singleton typed HTTP client |
| DataBento last-price store/provider factory | Singleton, registered by provider startup extension |
| DataBento latest-value store | One bounded store per running epoch |
| framework futures/option last-price readers | One stable DataBento handle per admitted contract and epoch |

DI construction validates static configuration but performs no provider I/O
and starts no feed. `StartAsync(valueDate)` creates the date-specific epoch.

The framework vendor registration must not reference or register
`Application.MarketData.Contracts.IMarketDataApi`. This keeps dependency
direction as Application -> Framework contracts, with vendor implementation
selection performed only at the composition root.

Contract-definition queries, hot-price access, and live-stream controls use the
single application `IMarketDataApi`; there is no snapshot marker interface or
second application API registration. DataBento may still use separate internal
Historical/query clients and live-feed sessions when required by its protocols,
but those transports share one date-scoped application epoch and never cross the
application boundary. The startup extension registers only one provider
implementation of the Framework MarketData last-price contracts; mixed
DataBento/IBKR readers inside one API epoch are prohibited.

## 16. Concurrency and ownership invariants

1. One API singleton owns at most one live epoch.
2. One live epoch owns one futures feed/service, one configured individual
   option feed/service, and a bounded set of explicitly requested option-chain
   feed sessions.
3. Lifecycle transitions are serialized and fully awaited.
4. Contract catalogs and provider subscriptions are immutable while running.
5. Provider query concurrency and queue capacity are bounded.
6. Futures, individual-option, and option-chain activation changes are atomic
   and idempotent.
7. No provider callback invokes domain/application business logic.
8. No contract query, Blackboard access, storage call, or option calculation
   occurs on a native callback or shared-reader drain hot path. Black-76
   enrichment occurs only on the managed option-chain worker.
9. No feed, thread, blocking reader, or task is created per contract/tick.
10. Every reader, batch, buffer, publisher envelope, worker, and feed has one
    owner and is released exactly once.
11. There is no synchronous-over-async bridge or unobserved fire-and-forget
    work.
12. A new value-date epoch cannot start until the prior epoch is fully drained.
13. Option-chain messages and state are transient and have no durable replay.
14. No option-chain component sends tick-aggregation actor messages or calls a
    persistence API.
15. `TickAggregationService` is the exclusive owner of durable tick
    persistence.
16. Each latest-value slot has one admitted writer and any number of
    non-consuming readers; quote and trade snapshots cannot tear.
17. A reader is bound to one contract and epoch and never returns a value from
    a prior or later value date.
18. A chain exists only while the same epoch's aggregation service and its
    canonical underlying ticker both report running.
19. Underlying ticker/service stop and chain admission are serialized; shutdown
    drains chain dependents before futures aggregation.

## 17. Implementation work packages

### WP0 - Contract and project readiness

- preserve the exact interface in section 2;
- remove duplicate imports without altering signatures;
- add required project references and solution membership;
- migrate consumers from duplicate legacy interface definitions;
- add an API-approval test for the exact public surface.

**Gate:** solution builds with one authoritative application contract and no
behavior change.

### WP1 - Contract resolver and batch APIs

- implement options, epoch catalog, resolver, and mappers;
- implement single and batch futures lookups;
- implement single and batch option lookups;
- implement domain-facing full option-chain discovery with provider metadata
  hydration and stable ordering;
- add ordering, duplicate, miss, ambiguity, and mapping tests.

**Gate:** all five contract-discovery methods pass deterministic and
provider-contract tests.

### WP2 - Bounded price operations

- implement the provider operation runner;
- define/register the Framework MarketData last-price reader contracts and the
  DataBento epoch store/provider factory;
- implement exact-decimal futures last-trade price through its reader;
- implement nullable exact-decimal option quote midpoint through its reader;
- implement atomic option quote/trade-with-Greeks slot reads, with enrichment
  remaining unavailable until a pricing context has produced a result;
- add freshness, unavailable, crossed/one-sided, conversion, identity,
  epoch-lifetime, and no-provider-fallback tests.

**Gate:** both price methods have bounded completion and exact result semantics.

### WP3 - Date lifecycle and futures integration

- implement epoch factory and serialized lifecycle;
- compose the existing futures feed/aggregation path;
- populate all futures mappings before start;
- implement futures activation/deactivation and live router;
- wire health and metrics.

**Gate:** start/stop, rollover, failure injection, futures ordering, and drain
tests pass without duplicate services or resource leaks.

### WP4 - Futures-option streaming

- define the contract/value-date-keyed option event;
- implement the multiplexed option streaming service;
- implement bounded ordered publisher and activation barriers;
- migrate the MarketData Feed option event workflow from broker request IDs;
- implement both option streaming interface methods.
- adapt domain underlying/option contract IDs to the existing framework
  `IDatabentoOptionChainFeed`;
- implement the bounded option-chain session manager,
  `OptionChainTickService`, transient state store, and both chain methods;
- implement separate transient quote and trade service messages;
- update futures, individual-option, and option-chain managed processors to
  replace their admitted contract slots before event publication;
- implement `GetTickerStatus` on `ITickAggregationService`, the shared
  admission/stop guard, typed chain-start dependency exceptions, and forced
  chain shutdown on dependency loss;
- implement the two DataBento Framework-contract readers, DI-selected epoch
  reader provider, and both `IMarketDataApi` reader-acquisition methods;
- pass only the application-selected immutable risk-free-rate value into each
  DataBento option-chain session;
- define the provider-neutral `ITreasuryCurve` and `IEconomicCalendar`
  contracts and implement them in Financial Modeling Prep;
- implement deterministic no-look-ahead curve-date and DTE ceiling-tenor
  selection;
- enrich both transient message families with the quote-derived immutable
  `OptionGreeksSnapshot`;
- publish the same atomic quote/trade-with-Greeks state through
  `IFuturesOptionLastPriceReader` for individual high-level consumers;
- implement ordered OptionSpread-engine delivery and throttled UI deltas;
- prevent overlapping individual/chain route ownership.

**Gate:** every accepted active chain quote/trade updates live state, exposes
the defined Greeks validity semantics, and is available to the correct live
consumer path; inactive behavior is explicit, and tests prove that no chain
component calls tick aggregation, event source, or storage.

### WP5 - End-to-end verification

- exercise futures ticks through provider, application API, tick actors, event
  source, and storage;
- exercise option chains through provider, application API, transient live
  state, OptionSpread-engine delivery, and throttled UI delivery only;
- run MarketData Feed unit/integration suites;
- run all ten domain integration projects;
- execute benchmarks and record environment/result evidence.

**Gate:** the exact application contract passes full regression and performance
validation.

### WP6 - Staged rollout

- activate lookups and snapshots first;
- canary futures collection and live activation;
- canary option activation after domain event migration;
- verify mapping, latency, allocation, backpressure, ordering, and drain;
- complete SWO-10 pre-production and soak requirements;
- retain whole-provider rollback without mixing Databento and IBKR inside one
  live API instance.

## 18. Required tests

### 18.1 Contract approval

- exact reflection/API snapshot of every method in section 2;
- no Guid method parameters;
- assignability of the concrete class to both marker interfaces;
- no duplicate authoritative application contract.

### 18.2 Lifecycle

- same-date start idempotency;
- different-date start rejection while running;
- cancellation during every controlled start phase;
- failure rollback at every start phase;
- date-matched stop and mismatched-date rejection;
- full drain/disposal and next-date restart;
- error callback behavior and failure isolation.

### 18.3 Contract APIs

- single and grouped provider resolution;
- input-order and duplicate preservation;
- empty batch without provider access;
- all-or-nothing batch miss behavior;
- canonical forward/reverse mapping;
- future versus option kind enforcement;
- fixed-point strike, expiry, multiplier, currency, and exchange mapping;
- full option-chain discovery returns both calls and puts for only the exact
  underlying and maturity;
- option-chain discovery has stable strike/type/contract ordering and returns
  an empty array for a confirmed empty chain;
- chain metadata hydration is batched and performs no per-option provider
  query loop;

### 18.4 Prices

- exact positive, zero, and boundary fixed-point conversion;
- futures no-result exception;
- option no-quote null result;
- crossed/undefined quote rejection;
- result instrument identity verification;
- provider admission, timeout, and overload behavior.

### 18.5 Streaming

- activation/deactivation true/false semantics;
- wrong-kind and unknown-contract failures;
- concurrent duplicate activation;
- futures collection continues independently from live activation;
- option events only while active;
- option-chain domain-ID resolution and provider subscription translation;
- chain start rejects a stopped aggregation service before any chain feed,
  Treasury query, or capacity reservation;
- chain start rejects an underlying contract that is absent from the current
  aggregation ticker set or is not running;
- status distinguishes service running, ticker configured, and ticker running
  and never infers lifecycle state from last-price availability;
- concurrent aggregation stop versus chain start has one deterministic winner
  and cannot leave an orphan chain;
- aggregation worker fault/ticker removal forces dependent chain drain and
  typed stop/failure publication;
- planned epoch stop drains chains before stopping aggregation, and ticker
  removal is rejected while dependents exist;
- distinct live quote and trade message routing;
- identical-chain idempotency and conflicting-chain rejection;
- multiple option contracts through one framework shared reader;
- individual/chain overlap rejection and bounded chain-session capacity;
- deactivation barrier behavior;
- multi-contract isolation and source ordering;
- bounded backpressure and publisher failure;
- graceful stop with accepted events;
- atomic current-state replacement and stale-data detection;
- valid quote midpoint produces implied volatility and Black-76 Greeks with
  the documented units;
- quote updates warm-start implied-volatility inversion from the prior valid
  value and expose solver failures without zero sentinels;
- trade events reuse the latest quote-derived Greeks snapshot and correctly
  report no valid snapshot before the first qualifying quote;
- stale/missing futures-price inputs fail explicitly without provider, actor,
  or storage access from the option-chain worker;
- futures and option reader acquisition enforces running epoch, domain ID,
  contract kind, capacity, stable same-epoch identity, and no subscription side
  effect;
- every admitted futures/option quote and trade updates the correct DataBento
  slot in source order, and an older record cannot overwrite a newer value;
- concurrent non-consuming reads are coherent and allocation-free after
  warm-up; missing, one-sided, crossed, stale, and post-stop behavior is
  explicit;
- option quote-with-Greeks reads atomically pair the exact quote source
  sequence with its calculation; trade-with-Greeks reads pair the trade with
  the latest quote-derived calculation available at trade processing time;
- enriched-reader availability is distinct from Greeks validity, failed
  calculations retain typed reasons and nullable outputs, and raw replacement
  cannot expose an older enriched snapshot as current;
- individual-option and option-chain routes share one option slot without
  competing writers;
- Treasury lookup chooses the latest curve date not after value date, including
  weekend/holiday fallback within the configured age;
- DTE boundary tests at 30/31, 60/61, 90/91, 180/181, and every remaining
  supported tenor boundary;
- missing/stale curves, non-finite rates, DTE <= 0, DTE > 30Y, percentage-to-
  decimal conversion, and prohibition of a zero-on-miss rate;
- FMP treasury/calendar contract tests and proof that DataBento performs no FMP
  HTTP request or Blackboard/Redis call per option record;
- put/call, expiry, invalid/crossed quote, no-arbitrage bound, and non-finite
  input cases;
- OptionSpread-engine ordered delta delivery and UI output throttling;
- zero option-chain calls to tick actors, event source, Redis persistence,
  Scylla, or tick-storage APIs;
- proof that durable tick writes originate only from `TickAggregationService`;
- no per-contract threads or per-tick tasks.

## 19. Benchmarks and production qualification

Benchmark:

- catalog lookup and mapping allocation;
- batch resolution at 1, 10, 100, and representative contract counts;
- operation-runner admission and completion;
- futures trade and option quote/midpoint API facade overhead over hot readers;
- activation/deactivation;
- multiplexed option dispatch at representative ticker counts;
- option-chain quote/trade demultiplexing, state replacement, and UI
  coalescing at representative chain sizes;
- Black-76 implied-volatility plus Greeks enrichment at representative chain
  sizes, quote rates, moneyness, and volatility regimes, recording p50/p95/p99
  latency, allocations, solver iterations, convergence failures, and the
  enriched-versus-unenriched throughput delta;
- DataBento quote/trade slot update and reader throughput, seqlock retry rate,
  allocation, contention, slot count, and post-stop behavior;
- lifecycle start/stop by universe size;
- existing futures aggregation suites without regression.

Every result records commit, hardware, OS, runtime, deployment profile, dataset,
universe size, input shape, absolute values, allocation, and variance.

Production qualification inherits SWO-10:

- 1 million records/second sustained;
- 5 million records/second sustained;
- 10 million records/second burst;
- 2x replay load;
- 30-minute strict pre-production run;
- 24-hour production soak;
- zero unexplained loss, ordering errors, handle growth, or post-warm-up
  allocation on qualified hot paths.

## 20. Acceptance criteria

Implementation is complete when:

- `DatabentoMarketDataApi` implements the exact interface in section 2;
- no framework project defines or implements `IMarketDataApi`; the
  application implementation composes DI-selected framework services;
- lifecycle is date-scoped, idempotent, cancellable on start, and fully drained
  on stop;
- single, batch, and option-chain discovery results follow the documented
  miss/order rules;
- futures and option prices use exact decimal semantics without missing-value
  sentinels;
- `GetFuturesPriceAsync` reads the current futures last-trade reader and
  `GetFuturesOptionPriceAsync` reads the current option quote/midpoint reader;
  neither invokes the one-shot provider latest-price client or storage;
- provider calls have bounded concurrency, capacity, and timeouts;
- the existing futures aggregation pipeline is reused without duplicate work;
- option streaming uses one multiplexed bounded service and stable
  contract/value-date event identity;
- each option-chain session uses one dedicated `OptionChainTickService` and
  separate transient quote/trade messages;
- both transient option-chain message families carry an immutable,
  quote-derived Black-76 Greeks snapshot with explicit validity, source, units,
  and input timestamps;
- Greeks calculation uses a non-blocking current futures-price snapshot and
  never performs a per-record provider query, actor call, or storage read;
- DataBento owns one bounded epoch-local latest quote/trade store and the Greeks
  hot path reads its underlying futures slot directly;
- `IMarketDataApi` exposes contract-bound Framework MarketData futures and
  futures-option reader handles implemented directly by the DI-selected
  DataBento provider, without starting a subscription or leaking provider
  lookup identifiers;
- option-chain admission requires `ServiceRunning`, `TickerConfigured`, and
  `TickerRunning` for the canonical underlying in the same epoch, and throws a
  typed exception before chain allocation when any condition fails;
- a running chain is stopped/faulted and drained when its underlying aggregation
  dependency is lost; it never continues from a cached last price;
- FMP implements the provider-neutral Treasury-curve and economic-calendar
  contracts; the application resolves and passes the selected rate, while
  DataBento does not implement or call either external-data API;
- risk-free-rate resolution uses a no-look-ahead curve date and documented DTE
  ceiling-tenor rule, with no zero fallback;
- current option-chain state serves the OptionSpread engine and throttled UI
  delivery without durable reads or writes;
- option-chain components never publish tick-aggregation messages and never
  call persistence APIs;
- durable tick persistence is performed only by `TickAggregationService`;
- all six streaming methods implement deterministic true/false state-change
  semantics;
- no provider subscription is mutated after feed start;
- errors remain typed and callback operation IDs do not become domain identity;
- health distinguishes lifecycle, lookup, price, futures, option, publication,
  and recovery readiness;
- all deterministic, integration, regression, benchmark, and rollout gates pass;
- production restart remains gated until durable same-value-date futures
  sequence recovery is approved.

## 21. Approval decisions before coding

1. Confirm the exact interface and result semantics in sections 2 and 13.
2. Confirm that configured futures and option universes are fixed for one
   value-date epoch.
3. Confirm that futures streaming activation controls live delivery while the
   configured system feed continues durable collection.
4. Confirm batch lookup is all-or-nothing with input-order preservation.
5. Confirm futures no-price throws while option no-qualifying-quote may return
   `null`.
6. Approve the contract/value-date-keyed option event migration away from
   broker request IDs.
7. Confirmed: one option-chain request supplies a domain underlying contract
   ID, exact maturity, and selected domain option contract IDs, and subscribes
   to live quote and trade data.
8. Confirm the maximum concurrent option-chain session budget.
9. Confirm the configuration source for the two universes and whether lifecycle
   `valueDate` is also the Databento definition date.
10. Confirmed: option-chain membership, state, quotes, trades, and messages are
    transient only; `TickAggregationService` exclusively owns durable tick
    persistence.
11. Confirmed: `GetFuturesOptionChainContractsAsync` exposes all domain-mapped
    calls and puts for the exact underlying/maturity without domain filtering;
    the domain actor owns filtering and passes selected IDs to streaming start.
12. Confirmed: option-chain quote/trade service events carry Black-76 implied
    volatility and Greeks; quote midpoint is the valuation mark, while trade
    events reuse the latest quote-derived snapshot rather than invert the last
    trade.
13. Confirmed: DataBento owns the epoch-local latest futures/option quote and
    trade store; `IMarketDataApi` exposes non-consuming contract-bound reader
    handles through the two application reader interfaces.
14. Confirmed: `ITreasuryCurve` and `IEconomicCalendar` are provider-neutral
    `Framework.MarketData.Contracts`; Financial Modeling Prep implements them,
    while the application selects and passes the rate into DataBento.
15. Confirmed: rate resolution uses the latest curve date not after value date
    and the shortest Treasury tenor covering DTE (1-30 days = 1M, 31-60 = 2M,
    and so on), without a zero fallback.
16. Confirm the maximum permitted quote/trade age and maximum admitted
    futures/option latest-value slot counts per epoch.
17. Confirmed: an option chain is a hard dependent of its underlying canonical
    futures ticker in the same running `TickAggregationService`; start throws
    before allocation when the service/ticker is unavailable, and dependency
    loss drains and stops/faults the chain.
18. Confirmed: the two last-price reader contracts live in
    `Framework.MarketData.Contracts.LastPrice`, DataBento implements them
    directly through startup DI, and both application price methods use those
    readers without a provider-query fallback.

Coding begins after these decisions are accepted.

## 22. Related documents

- `Documents/system/System-Wide-Optimization-Plan.md`
- `TomasAI.IFM.Framework.MarketData.DataBento/Docs/Databento_Market_Data_Specification_v1.1.md`
- `TomasAI.IFM.Framework.OptionPricer/Docs/QLNet-to-Black76-Migration-Plan.md`
- `TomasAI.IFM.Domain.MarketData.Feed/Docs/Databento-Futures-Tick-Aggregation-Specification-v1.md`
- `TomasAI.IFM.Domain.MarketData.Feed/Docs/Tick-Aggregation-Implementation-Details.md`
- `docs/Domain-Actor-Api-Implementations.md`

## 23. Revision history

| Version | Date | Change |
| --- | --- | --- |
| 1.8 | 2026-08-18 | Made VX terminology authoritative for the VX futures contract; added immediate accepted-VX-quote market-price publication, trade-or-valid-quote midpoint futures-price resolution, and quote-driven VX EOD/UI behavior without waiting for quote-batch storage. |
| 1.7 | 2026-08-17 | Made multi-dataset epoch startup/catalog hydration and feed shutdown concurrent, established a five-second feed-stop bound, and made the shared tick-event publisher reference-counted so its transport starts once and remains available until the final dataset aggregation drains. Recorded the accepted 25/25 Development G0 lifecycle result. |
| 1.6 | 2026-08-10 | Removed the redundant application `IMarketDataSnapshotApi`; contract-definition queries, hot-price access, and live controls now use only `IMarketDataApi`. Clarified that one application API may own multiple protocol-specific DataBento query/feed transports inside the same epoch. |
| 1.5 | 2026-08-10 | Recorded the Phase A production implementation: application-owned epoch/API orchestration, bounded bidirectional contract resolution, multi-asset aggregation, allocation-free DataBento hot readers, transient activation routing and publishers, non-persistent option-chain sessions/state/drain, separate DI boundaries, health, unit/live gates, and benchmark evidence. FMP rate selection, Black-76 production enrichment, and public option-chain start remain Phase B. |
| 1.4 | 2026-08-10 | Extended `IFuturesOptionLastPriceReader` with atomic quote/trade-with-Greeks reads; made the provider-neutral Greeks and enriched snapshot contracts authoritative in Framework MarketData; specified availability-versus-validity, exact quote sequence coherence, quote-derived trade Greeks, post-stop invalidation, and ingestion-time rather than reader-time calculation. Also clarified that `TickAggregationService` is the multi-asset raw-tick pipeline for futures and futures options. |
| 1.3 | 2026-08-10 | Made `IMarketDataApi` exclusively application-owned; removed the framework `MarketDataApi` contract location; specified that DataBento and other vendor projects implement provider-neutral `Framework.MarketData.Contracts` services and are composed into the application API through separate startup DI registrations. |
| 1.2 | 2026-08-10 | Moved futures and futures-option last-price reader/snapshot contracts to `Framework.MarketData.Contracts.LastPrice`; specified direct DataBento implementations selected by startup DI, an epoch store/provider factory, and changed both application price methods to use the corresponding hot reader with no provider-query, replay, actor, or storage fallback. |
| 1.1 | 2026-08-10 | Made every option chain a hard lifecycle dependent of its underlying futures ticker in the same epoch's `TickAggregationService`; added contract-ID ticker status, fail-fast typed admission checks, start/stop race serialization, forced chain drain on dependency loss, shutdown ordering, tests, metrics, and acceptance rules. |
| 1.0 | 2026-08-10 | Replaced the proposed Blackboard L1/L2 price path with a bounded epoch-local DataBento latest quote/trade store; added application `IFuturesLastPriceReader` and `IFuturesOptionLastPriceReader` contracts plus `IMarketDataApi` acquisition methods, specified thin DataBento adapters, coherent allocation-free hot reads, shared option slots, epoch lifetime, and passing only the application-resolved risk-free rate into option-chain sessions. |
| 0.9 | 2026-08-10 | Moved all Blackboard and Treasury access to application orchestration; specified an application listener that updates `MarketDataFeed.FuturesLastPrice` from inserted futures trades, bounded L1 memory plus Redis L2 behavior, an L1-only `IFuturesLastPriceReader` passed to DataBento, and a session-fixed risk-free rate passed by value without changing `IMarketDataApi`. |
| 0.8 | 2026-08-10 | Added Blackboard futures-pricing and Treasury-curve caches, an atomic hot-path price mirror, provider-neutral Treasury/economic-calendar contracts with FMP ownership, and deterministic no-look-ahead curve-date plus DTE ceiling-tenor selection for Black-76 session rates. |
| 0.7 | 2026-08-10 | Added live Black-76 implied-volatility and Greeks enrichment for transient option-chain quote/trade events; defined the managed-worker boundary, quote-midpoint/trade-snapshot policy, non-blocking futures/rate inputs, typed invalid/stale semantics, performance guardrails, tests, metrics, and acceptance criteria. |
| 0.6 | 2026-08-10 | Added domain-facing `GetFuturesOptionChainContractsAsync`; specified full call/put discovery through the existing framework definitions API, exact underlying/maturity validation, batched metadata hydration, canonical mapping, stable ordering, empty-chain behavior, and domain-owned filtering. |
| 0.5 | 2026-08-10 | Made option-chain processing live-only: added the dedicated framework `OptionChainTickService`, quote-and-trade transient state/messages for the OptionSpread engine and throttled UI, prohibited all option-chain storage and tick-actor routing, and reserved durable tick persistence exclusively for `TickAggregationService`. |
| 0.4 | 2026-08-10 | Added domain-ID-based futures-option chain start/stop methods and specified their translation to the existing framework `IDatabentoOptionChainFeed`, immutable session identity, shared-reader processing, capacity, lifecycle, route ownership, tests, and acceptance behavior. |
| 0.3 | 2026-08-10 | Rewritten against the updated date-scoped, string-contract-ID `IMarketDataApi`; added single/batch resolver behavior, exact decimal price semantics, fully asynchronous lifecycle and stream activation, date-specific epoch ownership, and contract-keyed multiplexed option streaming. |
| 0.2 | 2026-08-10 | Superseded: targeted an earlier Guid-command interface revision. |
| 0.1 | 2026-08-10 | Superseded initial application-layer design. |
