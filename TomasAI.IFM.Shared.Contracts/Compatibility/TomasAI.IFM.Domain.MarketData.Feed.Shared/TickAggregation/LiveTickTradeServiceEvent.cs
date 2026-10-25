namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>
/// Transient, non-event-sourced trade delivery for an activated asset route.
/// </summary>
public readonly record struct LiveTickTradeServiceEvent(
    Guid EventId,
    string ContractId,
    DateOnly ValueDate,
    AssetTypeId AssetTypeId,
    string Dataset,
    DateOnly DefinitionDate,
    ushort PublisherId,
    uint InstrumentId,
    FuturesTickTradeData Trade);
