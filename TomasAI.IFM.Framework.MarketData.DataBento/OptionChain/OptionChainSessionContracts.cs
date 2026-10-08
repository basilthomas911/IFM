using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>Initializes a new OptionChainSessionKey instance.</summary>
/// <param name="FuturesContractId">The futures contract id.</param>
/// <param name="MaturityDate">The maturity date.</param>
/// <param name="OptionContractId">The exact leg contract for an isolated connection; empty for a chain connection.</param>
public readonly record struct OptionChainSessionKey(
    string FuturesContractId,
    DateOnly MaturityDate,
    string OptionContractId = "");

public sealed record DatabentoOptionChainRoute
{
    public required string FuturesOptionContractId { get; init; }
    public required OptionContractDefinition Definition { get; init; }
}

public sealed record DatabentoOptionChainSessionRequest
{
    /// <summary>Gets the single option contract owning this physical connection; empty selects chain mode.</summary>
    public string OptionContractId { get; init; } = string.Empty;
    public required string FuturesContractId { get; init; }
    public required DateOnly ValueDate { get; init; }
    public required OptionChainSubscription Subscription { get; init; }
    public required IReadOnlyList<DatabentoOptionChainRoute> Routes { get; init; }
}

/// <summary>Initializes a new FuturesOptionChainQuoteChangedServiceEvent instance.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="FuturesContractId">The futures contract id.</param>
/// <param name="FuturesOptionContractId">The futures option contract id.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="MaturityDate">The maturity date.</param>
/// <param name="Tick">The tick.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
public readonly record struct FuturesOptionChainQuoteChangedServiceEvent(
    Guid EventId,
    string FuturesContractId,
    string FuturesOptionContractId,
    DateOnly ValueDate,
    DateOnly MaturityDate,
    LastQuoteTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);

/// <summary>Initializes a new FuturesOptionChainTradeChangedServiceEvent instance.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="FuturesContractId">The futures contract id.</param>
/// <param name="FuturesOptionContractId">The futures option contract id.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="MaturityDate">The maturity date.</param>
/// <param name="Tick">The tick.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
public readonly record struct FuturesOptionChainTradeChangedServiceEvent(
    Guid EventId,
    string FuturesContractId,
    string FuturesOptionContractId,
    DateOnly ValueDate,
    DateOnly MaturityDate,
    LastTradeTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);

public interface IOptionChainTransientEventPublisher
{
    ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent @event);
    ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent @event);
}

public interface IOptionChainTransientEventSink
{
    ValueTask OnQuoteAsync(FuturesOptionChainQuoteChangedServiceEvent @event);
    ValueTask OnTradeAsync(FuturesOptionChainTradeChangedServiceEvent @event);
}

/// <summary>
/// Phase B supplies the Black-76 implementation and immutable session rate.
/// The Phase A session runtime depends only on this synchronous boundary.
/// </summary>
public interface IOptionChainGreeksEnricher
{
    OptionGreeksSnapshot EnrichQuote(
        DatabentoOptionChainRoute route,
        LastQuoteTickSnapshot tick);
    OptionGreeksSnapshot EnrichTrade(
        DatabentoOptionChainRoute route,
        LastTradeTickSnapshot tick);
}

/// <summary>Optional retained-trade boundary. Await completion before acknowledging an observed source trade.</summary>
public interface IRetainedOptionTradeEnricher
{
    ValueTask<OptionGreeksSnapshot> EnrichTradeAsync(DatabentoOptionChainRoute route, LastTradeTickSnapshot tick,
        long eventNanoseconds, long receiveNanoseconds, CancellationToken cancellationToken);
}

/// <summary>Initializes a new OptionChainContractState instance.</summary>
/// <param name="Route">The route.</param>
/// <param name="Quote">The bid and ask snapshot, when available.</param>
/// <param name="Trade">The last trade snapshot, when available.</param>
/// <param name="SessionVolume">The session volume.</param>
/// <param name="OpenInterest">The open interest.</param>
/// <param name="StatisticsAtUtc">The statistics at utc.</param>
/// <param name="SessionVolumeOfficial">The session volume official.</param>
public readonly record struct OptionChainContractState(
    DatabentoOptionChainRoute Route,
    LastQuoteTickWithGreeksSnapshot? Quote,
    LastTradeTickWithGreeksSnapshot? Trade,
    long? SessionVolume = null,
    long? OpenInterest = null,
    DateTimeOffset? StatisticsAtUtc = null,
    bool SessionVolumeOfficial = false);

public interface IOptionChainStateStore
{
    bool TryGet(
        OptionChainSessionKey session,
        string futuresOptionContractId,
        out OptionChainContractState state);
    IReadOnlyList<OptionChainContractState> GetSession(OptionChainSessionKey session);
}

public interface IDatabentoOptionChainSessionManager : IAsyncDisposable
{
    int ActiveSessionCount { get; }
    Task<bool> StartAsync(
        DatabentoOptionChainSessionRequest request,
        CancellationToken cancellationToken = default);
    Task<bool> StopAsync(
        string futuresContractId,
        DateOnly maturityDate);
}
