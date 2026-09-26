namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>
/// Transient, non-event-sourced quote delivery for an activated asset route.
/// Durable persistence continues through the separate aggregation events.
/// </summary>
public readonly record struct LiveTickQuoteServiceEvent(
    Guid EventId,
    string ContractId,
    DateOnly ValueDate,
    AssetTypeId AssetTypeId,
    string Dataset,
    DateOnly DefinitionDate,
    ushort PublisherId,
    uint InstrumentId,
    FuturesTickQuoteData Quote);
