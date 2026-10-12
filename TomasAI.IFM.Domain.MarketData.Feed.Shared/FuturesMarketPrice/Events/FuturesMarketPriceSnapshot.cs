using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Provides a provider-neutral futures price snapshot for actor-domain realtime processing.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesMarketPriceSnapshot(
    [property: Key(0)] string ContractId,
    [property: Key(1)] uint InstrumentId,
    [property: Key(2)] ushort PublisherId,
    [property: Key(3)] AssetTypeId AssetTypeId,
    [property: Key(4)] DateOnly ValueDate,
    [property: Key(5)] FuturesMarketQuoteSnapshot? Quote,
    [property: Key(6)] FuturesMarketTradeSnapshot? Trade);
