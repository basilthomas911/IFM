using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public readonly record struct OptionChainSessionKey(
    string FuturesContractId,
    DateOnly MaturityDate);

public sealed record DatabentoOptionChainRoute
{
    public required string FuturesOptionContractId { get; init; }
    public required OptionContractDefinition Definition { get; init; }
}

public sealed record DatabentoOptionChainSessionRequest
{
    public required string FuturesContractId { get; init; }
    public required DateOnly ValueDate { get; init; }
    public required OptionChainSubscription Subscription { get; init; }
    public required IReadOnlyList<DatabentoOptionChainRoute> Routes { get; init; }
}

public readonly record struct FuturesOptionChainQuoteChangedServiceEvent(
    Guid EventId,
    string FuturesContractId,
    string FuturesOptionContractId,
    DateOnly ValueDate,
    DateOnly MaturityDate,
    LastQuoteTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);

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

public readonly record struct OptionChainContractState(
    DatabentoOptionChainRoute Route,
    LastQuoteTickWithGreeksSnapshot? Quote,
    LastTradeTickWithGreeksSnapshot? Trade);

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
